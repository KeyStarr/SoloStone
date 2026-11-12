using EngineController = SabberStoneCore.Model.Entities.Controller;
using EngineMinion = SabberStoneCore.Model.Entities.Minion;
using EngineSpell = SabberStoneCore.Model.Entities.Spell;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;
using Zone = SabberStoneCore.Enums.Zone;
using Entity = SabberStoneCore.Model.Entities.Entity;

internal static class QuestProof
{
    private static void Check(bool value, string message) => SelfChecks.Check(value, "Quest " + message);
    private sealed class Fixture
    {
        internal readonly Game Game;
        internal readonly NativeSession Session;
        internal readonly List<PowerHistoryData> Wire = new();
        internal Fixture(Game game = null)
        {
            Game = game ?? new Game(new GameConfig { RandomSeed = 19, StartPlayer = 1, Shuffle = false,
                SkipMulligan = true, History = true, Logging = false, FormatType = FormatType.FT_WILD,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            if (game == null) Game.StartGame();
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => {
                session.Receive(id, packet);
                if (packet is PegasusGame.PowerHistory history) Wire.AddRange(history.List);
            });
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, Game);
            Session = session = new NativeSession(match);
            Flush();
        }
        internal void Flush() { Session.Flush(); Game.PowerHistory.Last.Clear(); }
        internal IPlayable Add(EngineController p, string id)
        {
            var card = Entity.FromCard(p, Cards.FromId(id), zone: p.HandZone);
            Flush(); return card;
        }
        internal IPlayable Play(EngineController p, string id)
        {
            p.BaseMana = 10; p.UsedMana = 0;
            var card = Add(p, id);
            Session.Process(p.Options().First(t => t.Source?.Id == card.Id));
            Game.PowerHistory.Last.Clear(); return card;
        }
        internal void Summon(EngineController p, string id)
        {
            var card = (EngineMinion)Entity.FromCard(p, Cards.FromId(id));
            Generic.SummonBlock(Game, card, -1, null); Game.DeathProcessingAndAuraUpdate(); Flush();
        }
    }

    private static void Metadata(IDictionary<GameTag, int> tags)
    {
        foreach (var pair in new[] { (462, 1), (534, 0), (535, 6), (1089, 121956), (4238, 1) })
            Check(tags.TryGetValue((GameTag)pair.Item1, out int value) && value == pair.Item2,
                "reveal metadata " + pair.Item1);
    }
    private static void Metadata(PegasusGame.PowerHistoryEntity entity) =>
        Metadata(entity.Tags.ToDictionary(t => (GameTag)t.Name, t => t.Value));

    private static void AutomaticQuestReveal()
    {
        foreach (bool opponent in new[] { false, true })
        foreach (string origin in new[] { "generated", "hand", "deck" })
        {
            var f = new Fixture(); var p = opponent ? f.Game.Player2 : f.Game.Player1;
            var quest = (EngineSpell)Entity.FromCard(p, Cards.FromId("TLC_426"),
                zone: origin == "deck" ? p.DeckZone : origin == "hand" ? p.HandZone : null);
            f.Flush();
            new SabberStoneCore.Tasks.SimpleTasks.PlayTask(SabberStoneCore.Tasks.SimpleTasks.PlayType.SPELL)
                .Process(f.Game, p, p.Hero, null, new TaskStack { Playables = new[] { (IPlayable)quest } });
            f.Flush();
            var shown = f.Session.Client.Entities[quest.Id];
            Check(quest.Zone == p.SecretZone && shown.CardId == "TLC_426",
                "auto-cast identity public: " + origin + "/" + p.PlayerId);
            Check(shown.Get(GameTag.QUEST_PROGRESS) == 0 && shown.Get(GameTag.QUEST_PROGRESS_TOTAL) == 6,
                "auto-cast progress metadata");
            Metadata(f.Wire.Last(e => e.HasShowEntity && e.ShowEntity.Entity == quest.Id).ShowEntity);
            Check(!Invariants.Compare(f.Game, f.Session.Client).Any(), "auto-cast native parity: "
                + string.Join("; ", Invariants.Compare(f.Game, f.Session.Client)));
            f.Summon(p, "EX1_506");
            Check(quest.QuestProgress == 1 && shown.Get(GameTag.QUEST_PROGRESS) == 1,
                "auto-cast listener advances public quest");
        }
    }

    private static void Unpinned_RepeatableWrapHistory(Fixture f, EngineSpell quest)
    {
        int cursor = f.Wire.Count;
        for (int i = 0; i < 6; i++)
        {
            f.Summon(quest.Controller, "EX1_506");
            Check(f.Session.Client.Entities[quest.Id].Get(GameTag.QUEST_PROGRESS) == (i + 1) % 6,
                "each causal progress update reaches either-side client");
        }
        var history = f.Wire.Skip(cursor).ToArray();
        var progress = history.Where(e => e.HasTagChange && e.TagChange.Entity == quest.Id
            && e.TagChange.Tag == 534).Select(e => e.TagChange.Value).ToArray();
        Check(progress.SequenceEqual(new[] { 1, 2, 3, 4, 5, 6, 0 }), "Unpinned_RepeatableWrapHistory: exact progress deltas");
        Check(!history.Any(e => (e.HasShowEntity && e.ShowEntity.Entity == quest.Id)
            || (e.HasFullEntity && e.FullEntity.Entity == quest.Id) || (e.HasHideEntity && e.HideEntity.Entity == quest.Id)
            || (e.HasTagChange && e.TagChange.Entity == quest.Id && e.TagChange.Tag == (int)GameTag.ZONE)),
            "wrap keeps actor/slot with progress deltas only");
        int threshold = Array.FindIndex(history, e => e.HasTagChange && e.TagChange.Entity == quest.Id
            && e.TagChange.Tag == 534 && e.TagChange.Value == 6);
        int rewardId = quest.Controller.AppliedEnchantments.Single(e => e.Card.Id == "TLC_426e").Id;
        int reward = Array.FindIndex(history, e => e.HasFullEntity && e.FullEntity.Entity == rewardId);
        int reset = Array.FindIndex(history, e => e.HasTagChange && e.TagChange.Entity == quest.Id
            && e.TagChange.Tag == 534 && e.TagChange.Value == 0);
        Check(threshold < reward && reward < reset, "threshold, reward creation, reset preserve causal order");
        var blocks = new Stack<PowerHistoryStart>();
        foreach (var entry in history)
        {
            if (entry.HasPowerStart) blocks.Push(entry.PowerStart);
            if (entry.HasTagChange && entry.TagChange.Entity == quest.Id && entry.TagChange.Tag == 534)
                Check(blocks.Any(b => b.Source == quest.Id && (int)b.Type == (int)BlockType.TRIGGER),
                    "progress remains inside causal quest trigger");
            if (entry.HasPowerEnd) { Check(blocks.Count > 0, "balanced wrap blocks"); blocks.Pop(); }
        }
        Check(blocks.Count == 0 && quest.QuestProgress == 0 && QuestRules.PlayerEffectStacks(quest.Controller, "TLC_426e") == 1,
            "threshold yields one reward and visible reset");
    }

    private static void ReferencesAndPositions(Fixture f, int questId, Zone zone = Zone.SECRET)
    {
        var known = new HashSet<int>(); var shown = new HashSet<int>(); int depth = 0;
        foreach (var entry in f.Wire)
        {
            if (entry.HasCreateGame)
            {
                known.Add(entry.CreateGame.GameEntity.Id);
                foreach (var p in entry.CreateGame.Players) known.Add(p.Entity.Id);
            }
            var entity = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : null;
            if (entity != null)
            {
                if (entry.HasShowEntity) Check(known.Contains(entity.Entity), "show follows full entity");
                known.Add(entity.Entity);
                if (entity.Name.Length > 0) shown.Add(entity.Entity);
                if (entity.Tags.Any(t => t.Name == (int)GameTag.DISPLAYED_CREATOR && t.Value == questId))
                    Check(shown.Contains(questId), "quest shown before displayed-creator reference");
            }
            if (entry.HasTagChange) Check(known.Contains(entry.TagChange.Entity), "tag follows entity");
            if (entry.HasPowerStart)
            {
                depth++;
                foreach (int id in new[] { entry.PowerStart.Source, entry.PowerStart.Target }.Where(id => id > 0))
                    Check(known.Contains(id), "block follows entity");
                if (entry.PowerStart.Source == questId) Check(shown.Contains(questId), "quest revealed before play/trigger reference");
            }
            if (entry.HasPowerEnd) Check(--depth >= 0, "balanced blocks");
        }
        Check(depth == 0, "all blocks close");
        var quest = f.Game.IdEntityDic[questId]; var visible = f.Session.Client.Entities[questId];
        Check(visible.Get(GameTag.ZONE) == (int)zone && visible.Get(GameTag.CONTROLLER) == quest.Controller.PlayerId
            && visible.Get(GameTag.ZONE_POSITION) == quest.ZonePosition + 1, "public quest position/controller/zone");
    }

    private static void CloneAndSnapshot()
    {
        var f = new Fixture(); var quest = (EngineSpell)f.Play(f.Game.Player1, "TLC_426");
        for (int i = 0; i < 4; i++) f.Summon(f.Game.Player1, "EX1_506");
        var clone = f.Game.CloneExact(history: true);
        var restored = Game.LoadSnapshot(f.Game.SaveSnapshot());
        foreach (var resumed in new[] { clone, restored })
        {
            var copied = resumed.Player1.SecretZone.ModernQuests.Single();
            Check(copied.QuestProgress == 4 && !ReferenceEquals(copied.QuestState, quest.QuestState), "mid-progress clone/restore equal and independent");
            // Snapshot helpers retain history, letting a fresh native session replay the same public state.
            resumed.PowerHistory.Last.Clear(); resumed.PowerHistory.Last.AddRange(f.Game.PowerHistory.Full);
            var next = new Fixture(resumed);
            Check(next.Session.Client.Entities[quest.Id].Get(GameTag.QUEST_PROGRESS) == 4, "resumed native progress");
            for (int i = 0; i < 2; i++) next.Summon(resumed.Player1, "EX1_506");
            Check(copied.QuestProgress == 0 && QuestRules.PlayerEffectStacks(resumed.Player1, "TLC_426e") == 1,
                "resume grants once at threshold");
            Check(next.Session.Client.Entities[quest.Id].Get(GameTag.QUEST_PROGRESS) == 0, "resume publishes wrap");
        }
        Check(quest.QuestProgress == 4 && QuestRules.PlayerEffectStacks(f.Game.Player1, "TLC_426e") == 0,
            "resumed games cannot change original");
    }

    private static void MetadataEmissionScope()
    {
        var f = new Fixture();
        foreach (string id in new[] { "TLC_426", "TLC_987", "TLC_426t", "UNG_942" })
        {
            var entity = f.Add(f.Game.Player1, id);
            int before = f.Game.PowerHistory.Full.Count;
            entity[GameTag.QUEST_REWARD_DATABASE_ID] = 121957;
            entity[(GameTag)4238] = 2;
            var changes = f.Game.PowerHistory.Full.Skip(before).OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>().ToArray();
            Check(id == "TLC_426"
                ? changes.Select(e => (e.Tag, e.Value)).SequenceEqual(new[] { (GameTag.QUEST_REWARD_DATABASE_ID, 121957), ((GameTag)4238, 2) })
                : changes.Length == 0, "high-tag emission exception scoped to modern quest " + id);
            before = f.Game.PowerHistory.Full.Count;
            entity[GameTag.QUEST_REWARD_DATABASE_ID] = 121957;
            entity[(GameTag)4238] = 2;
            Check(before == f.Game.PowerHistory.Full.Count, "unchanged quest metadata emits nothing");
        }
    }

    private static void Unpinned_NonrepeatableCompletionOrder(string cardId, bool opponent)
    {
        var f = new Fixture();
        if (opponent) { f.Session.Process(EndTurnTask.Any(f.Game.Player1)); f.Game.PowerHistory.Last.Clear(); }
        var p = opponent ? f.Game.Player2 : f.Game.Player1;
        var quest = (EngineSpell)f.Play(p, cardId);
        int cursor = f.Wire.Count;
        switch (cardId)
        {
            case "TLC_239":
                for (int turn = 0; turn < 3; turn++)
                {
                    if (turn > 0)
                    {
                        f.Session.Process(EndTurnTask.Any(p)); f.Game.PowerHistory.Last.Clear();
                        f.Session.Process(EndTurnTask.Any(p.Opponent)); f.Game.PowerHistory.Last.Clear();
                        p.BoardZone.First().ToBeDestroyed = true;
                        f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
                    }
                    while (!p.BoardZone.IsFull) f.Summon(p, "CS2_231");
                    Check(quest.QuestProgress == turn + 1, "board progress settles before decision");
                    f.Game.SaveSnapshot();
                }
                break;
            case "END_017":
                while (p.HandZone.Count < 9) f.Add(p, "CS2_032");
                Generic.Draw(p); f.Flush();
                Check(quest.QuestProgress == 1, "draw reaches hand threshold without manual drain");
                f.Game.SaveSnapshot();
                foreach (var card in p.HandZone.ToArray())
                {
                    p.BaseMana = 10; p.UsedMana = 0;
                    f.Session.Process(p.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id));
                    f.Game.PowerHistory.Last.Clear();
                }
                break;
            case "TLC_631":
                for (int hit = 0; hit < 12; hit++)
                {
                    p.Opponent.Hero.TakeDamage(p.Hero, 2); f.Flush();
                    Check(quest.QuestProgress == hit + 1, "damage progress settles before decision");
                    f.Game.SaveSnapshot();
                }
                break;
        }
        string rewardCardId = cardId + "t";
        var reward = p.HandZone.Single(c => c.Card.Id == rewardCardId);
        var history = f.Wire.Skip(cursor).ToArray();
        int Index(GameTag tag, int value) => Array.FindIndex(history, e => e.HasTagChange
            && e.TagChange.Entity == quest.Id && e.TagChange.Tag == (int)tag && e.TagChange.Value == value);
        int threshold = Index(GameTag.QUEST_PROGRESS, quest.QuestTotalProgress);
        int created = Array.FindIndex(history, e => e.HasFullEntity && e.FullEntity.Entity == reward.Id);
        int revealed = Index(GameTag.REVEALED, 1);
        int moved = Index(GameTag.ZONE, (int)Zone.GRAVEYARD);
        Check(threshold >= 0 && threshold < created && created < revealed && revealed < moved,
            "Unpinned_NonrepeatableCompletionOrder: progress/reward/reveal/graveyard " + cardId);
        var snapshots = history.Where(e => e.HasFullEntity || e.HasShowEntity)
            .Select(e => e.HasFullEntity ? e.FullEntity : e.ShowEntity).Where(e => e.Entity == reward.Id).ToArray();
        Check(reward[GameTag.DISPLAYED_CREATOR] == quest.Id, "reward attributed to completed quest");
        if (opponent)
            Check(snapshots.All(e => e.Name == "" && e.Tags.All(t => t.Name != (int)GameTag.DISPLAYED_CREATOR)),
                "opponent reward identity/creator follow hidden hand policy " + cardId + ": "
                    + string.Join("; ", snapshots.Select(e => e.Name + " [" + string.Join(",", e.Tags.Select(t => t.Name + "=" + t.Value)) + "]")));
        else
        {
            Check(snapshots.Any(e => e.Name == rewardCardId && e.Tags.Any(t => t.Name == (int)GameTag.DISPLAYED_CREATOR && t.Value == quest.Id)),
                "owner reward reveal carries quest creator");
            int shown = Array.FindIndex(history, e => e.HasShowEntity && e.ShowEntity.Entity == reward.Id
                && e.ShowEntity.Name == rewardCardId);
            Check(created <= shown && shown < revealed, "owner reward show precedes quest reveal");
        }
        var blocks = new Stack<PowerHistoryStart>();
        foreach (var entry in history)
        {
            if (entry.HasPowerStart) blocks.Push(entry.PowerStart);
            if ((entry.HasTagChange && entry.TagChange.Entity == quest.Id
                    && (entry.TagChange.Tag == (int)GameTag.QUEST_PROGRESS || entry.TagChange.Tag == (int)GameTag.REVEALED
                        || entry.TagChange.Tag == (int)GameTag.ZONE))
                || (entry.HasFullEntity && entry.FullEntity.Entity == reward.Id))
                Check(blocks.Any(b => b.Source == quest.Id && (int)b.Type == (int)BlockType.TRIGGER),
                    "completion/progress inside quest-sourced trigger " + cardId);
            Check(!(entry.HasHideEntity && entry.HideEntity.Entity == quest.Id), "quest stays public through graveyard move");
            var snapshot = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : null;
            if (snapshot?.Entity == quest.Id) Check(snapshot.Name == cardId, "consumed quest snapshot stays public");
            if (entry.HasPowerEnd) { Check(blocks.Count > 0, "completion blocks balanced"); blocks.Pop(); }
        }
        Check(blocks.Count == 0 && quest.Zone == p.GraveyardZone
            && f.Session.Client.Entities[quest.Id].CardId == cardId, "consumed quest remains public");
        ReferencesAndPositions(f, quest.Id, Zone.GRAVEYARD);
        Check(!Invariants.Compare(f.Game, f.Session.Client).Any(), "strict completion parity " + cardId + ": "
            + string.Join("; ", Invariants.Compare(f.Game, f.Session.Client)));
    }

    private static void PinnedRule_RewardWaitsForHandSpace(bool opponent)
    {
        var f = new Fixture();
        if (opponent) { f.Session.Process(EndTurnTask.Any(f.Game.Player1)); f.Game.PowerHistory.Last.Clear(); }
        var p = opponent ? f.Game.Player2 : f.Game.Player1;
        var quest = (EngineSpell)f.Play(p, "TLC_631");
        while (!p.HandZone.IsFull) f.Add(p, "CS2_231");
        int entities = f.Game.IdEntityDic.Count;
        for (int hit = 0; hit < 12; hit++) p.Opponent.Hero.TakeDamage(p.Hero, 2);
        f.Flush();
        Check(quest.Zone == p.GraveyardZone && f.Game.IdEntityDic.Count == entities
            && !f.Game.IdEntityDic.Values.Any(c => c.Card.Id == "TLC_631t"), "pending reward has no native entity");
        f.Session.Process(EndTurnTask.Any(p)); f.Game.PowerHistory.Last.Clear();
        int cursor = f.Wire.Count;
        Generic.DiscardBlock(p, p.HandZone[0]); f.Flush();
        var reward = p.HandZone.Single(c => c.Card.Id == "TLC_631t");
        Check(f.Game.CurrentPlayer == p.Opponent && p.PendingQuestRewards.Count == 0
            && p.GraveyardZone.All(c => c.Card.Id != "TLC_631t"), "opponent-turn slot delivers without burn");
        var snapshots = f.Wire.Skip(cursor).Where(e => e.HasFullEntity || e.HasShowEntity)
            .Select(e => e.HasFullEntity ? e.FullEntity : e.ShowEntity).Where(e => e.Entity == reward.Id).ToArray();
        Check(snapshots.Length >= 2 && snapshots.All(e => e.Tags.Any(t => t.Name == (int)GameTag.ZONE && t.Value == (int)Zone.HAND)),
            "delayed reward created as ordinary hand card");
        Check(opponent
            ? snapshots.All(e => e.Name == "" && e.Tags.All(t => t.Name != (int)GameTag.DISPLAYED_CREATOR))
            : snapshots.All(e => e.Name == "TLC_631t" && e.Tags.Any(t => t.Name == (int)GameTag.DISPLAYED_CREATOR && t.Value == quest.Id)),
            "delayed reward respects owner/opponent hand privacy");
        Check(!Invariants.Compare(f.Game, f.Session.Client).Any(), "delayed native hand projection parity");
    }

    private static void ColossusExtraHit(bool opponent)
    {
        var f = new Fixture();
        if (opponent) { f.Session.Process(EndTurnTask.Any(f.Game.Player1)); f.Game.PowerHistory.Last.Clear(); }
        var p = opponent ? f.Game.Player2 : f.Game.Player1;
        f.Play(p, "TLC_631t");
        var effect = p.AppliedEnchantments.Single(e => e.Card.Id == "TLC_631e");
        int cursor = f.Wire.Count, damage = p.Opponent.Hero.Damage;
        p.Opponent.Hero.TakeDamage(p.Hero, 2); f.Flush();
        var history = f.Wire.Skip(cursor).ToArray();
        Check(p.Opponent.Hero.Damage == damage + 4, "Colossus original hit plus exactly one extra hit");
        var blocks = new Stack<PowerHistoryStart>();
        int extraHits = 0;
        foreach (var entry in history)
        {
            if (entry.HasPowerStart) blocks.Push(entry.PowerStart);
            if (entry.HasTagChange && entry.TagChange.Entity == p.Opponent.Hero.Id
                && entry.TagChange.Tag == (int)GameTag.DAMAGE && entry.TagChange.Value == damage + 4)
            {
                Check(blocks.Any(b => b.Source == effect.Id && (int)b.Type == (int)BlockType.TRIGGER),
                    "extra hit sourced from controller enchantment TLC_631e");
                extraHits++;
            }
            if (entry.HasPowerEnd) { Check(blocks.Count > 0, "extra-hit blocks balanced"); blocks.Pop(); }
        }
        Check(blocks.Count == 0 && extraHits == 1, "extra hit finite and balanced");
        int shown = f.Wire.FindIndex(e => (e.HasFullEntity && e.FullEntity.Entity == effect.Id && e.FullEntity.Name == "TLC_631e")
            || (e.HasShowEntity && e.ShowEntity.Entity == effect.Id && e.ShowEntity.Name == "TLC_631e"));
        int referenced = f.Wire.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == effect.Id);
        Check(shown >= 0 && shown < referenced, "controller enchantment shown before trigger reference");
        Check(!Invariants.Compare(f.Game, f.Session.Client).Any(), "strict Colossus extra-hit parity");
        f.Game.SaveSnapshot();
    }

    private static void AutomaticDeckQuest()
    {
        foreach (bool opponent in new[] { false, true })
        {
            var f = new Fixture();
            if (opponent) f.Session.Process(EndTurnTask.Any(f.Game.Player1));
            var owner = opponent ? f.Game.Player2 : f.Game.Player1;
            var quest = (EngineSpell)Entity.FromCard(owner, Cards.FromId("END_017"), zone: owner.DeckZone);
            f.Flush();
            Check(f.Session.Client.Entities[quest.Id].CardId == "", "deck Quest starts hidden");
            f.Play(owner, "TLC_438");
            Check(quest.Zone == owner.SecretZone && f.Session.Client.Entities[quest.Id].CardId == "END_017"
                && f.Session.Client.Entities[quest.Id].Get(GameTag.QUEST) == 1,
                "automatically cast deck Quest becomes public on either side");
            int reveal = f.Wire.FindIndex(e => e.HasShowEntity && e.ShowEntity.Entity == quest.Id);
            int move = f.Wire.FindIndex(e => e.HasTagChange && e.TagChange.Entity == quest.Id
                && e.TagChange.Tag == (int)GameTag.ZONE && e.TagChange.Value == (int)Zone.SECRET);
            Check(reveal >= 0 && reveal < move, "automatic deck Quest is named before its public zone move");
            var violations = Invariants.Compare(f.Game, f.Session.Client).ToArray();
            Check(violations.Length == 0, "automatic deck Quest strict parity: " + string.Join(';', violations.Select(v => v.ToString())));
        }
    }

    internal static void Run()
    {
        AutomaticQuestReveal();
        AutomaticDeckQuest();
        foreach (bool opponent in new[] { false, true })
        {
            var f = new Fixture();
            if (opponent) { f.Session.Process(EndTurnTask.Any(f.Game.Player1)); f.Game.PowerHistory.Last.Clear(); }
            var p = opponent ? f.Game.Player2 : f.Game.Player1;
            var quest = (EngineSpell)f.Play(p, "TLC_426");
            Metadata(PowerHistoryBuilder.FullEntity(quest).Entity.Tags);
            Metadata(PowerHistoryBuilder.ShowEntity(quest).Entity.Tags);
            var reveal = f.Wire.Where(e => e.HasFullEntity || e.HasShowEntity)
                .Select(e => e.HasFullEntity ? e.FullEntity : e.ShowEntity)
                .Last(e => e.Entity == quest.Id && e.Name == "TLC_426");
            Metadata(reveal);
            Check(f.Session.Client.Entities[quest.Id].CardId == "TLC_426", "quest public on either side");
            f.Summon(p, "CS2_182"); f.Summon(p.Opponent, "EX1_506");
            Check(f.Session.Client.Entities[quest.Id].Get(GameTag.QUEST_PROGRESS) == 0, "nonqualifying events do not progress");
            Unpinned_RepeatableWrapHistory(f, quest);
            ReferencesAndPositions(f, quest.Id);
            if (opponent)
            {
                var secret = f.Play(p, "EX1_287");
                var hidden = f.Session.Client.Entities[secret.Id];
                Check(hidden.CardId == "" && !hidden.Tags.ContainsKey(1089) && !hidden.Tags.ContainsKey(534),
                    "real secret stays hidden alongside public quest");
                Check(f.Wire.Where(e => e.HasFullEntity || e.HasShowEntity)
                    .Select(e => e.HasFullEntity ? e.FullEntity : e.ShowEntity)
                    .Where(e => e.Entity == secret.Id).All(e => e.Name == ""), "secret never revealed on wire");
                Check(hidden.Get(GameTag.ZONE) == (int)Zone.SECRET && hidden.Get(GameTag.CONTROLLER) == 2,
                    "secret back remains public");
            }
        }
        foreach (string id in new[] { "TLC_426t", "TLC_987", "UNG_942" })
        {
            var tags = PowerHistoryBuilder.FullEntity(Entity.FromCard(new Fixture().Game.Player1, Cards.FromId(id))).Entity.Tags;
            Check(!tags.ContainsKey(GameTag.QUEST) && !tags.ContainsKey((GameTag)4238), "legacy/non-quest snapshot unchanged " + id);
        }
        foreach (bool opponent in new[] { false, true })
        {
            foreach (string id in new[] { "TLC_239", "END_017", "TLC_631" })
                Unpinned_NonrepeatableCompletionOrder(id, opponent);
            ColossusExtraHit(opponent);
            PinnedRule_RewardWaitsForHandSpace(opponent);
        }
        CloneAndSnapshot();
        MetadataEmissionScope();
        Console.WriteLine("PASS quest transport: metadata; public/private identity; causal progress; Unpinned_RepeatableWrapHistory; Unpinned_NonrepeatableCompletionOrder (3 quests, both sides); TLC_631e extra hits; clone/snapshot");
    }
}
