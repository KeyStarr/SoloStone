using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    private const GameTag SummonSourceTag = (GameTag)1254;
    // Retail hsreplay-test-data: 186852_hearthstone_access_eoe.power.log:3333-3338 (Battlecry), :3402-3407
    // (trigger); 34104_subspell.power.log:10709-10711 (Deathrattle), :11268-11359 (spell at row end).
    // Neighbours shift right-most first BEFORE FULL_ENTITY, which already carries the summon's slot; no SHOW_ENTITY.
    private static void EffectSummonSlotHistory()
    {
        var failures = new List<string>();
        var forwarded = (List<PegasusGame.Tag>)typeof(NativeMatch).GetMethod("ToNativeTags", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { new Dictionary<GameTag, int> { [SummonSourceTag] = 42, [(GameTag)15000] = 43 } });
        Check(forwarded.Any(t => t.Name == (int)SummonSourceTag && t.Value == 42)
            && forwarded.Any(t => t.Name == 15000 && t.Value == 43), "ToNativeTags forwards 1254 and unknown numeric tags");
        int cases = 0;
        foreach (bool bot in new[] { false, true })
        foreach (var (id, finalCharge) in new[] { "JAIL_877", "JAIL_511", "FIREBALL_GOLEM", "CS2_027", "EX1_025", "CATA_725", "REBORN", "CAP_804" }
            .SelectMany(id => (id.StartsWith("JAIL_") ? new[] { false, true } : new[] { false }).Select(final => (id, final))))
        {
            string name = $"{id}-{(bot ? "opponent" : "owner")}-final={finalCharge}";
            var (match, capture) = AnimationMatch(bot);
            var game = match.Game; var owner = game.CurrentPlayer;
            foreach (var old in owner.HandZone.Concat(owner.BoardZone).Concat(owner.Opponent.BoardZone).ToArray())
                old.Controller.SetasideZone.Add(old.Zone.Remove(old));
            for (int i = 0; i < 3; i++) EngineEntity.FromCard(owner, Cards.FromId("CS2_231"), zone: owner.HandZone);
            var enemy = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_182"), zone: owner.Opponent.BoardZone);
            enemy.BaseHealth = 100; enemy.AttackDamage = 0; // Spire's forced attack must leave both minions alive.
            EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.BoardZone); // A
            bool location = id.StartsWith("JAIL_");
            bool playedMinion = id is "EX1_025" or "CATA_725" or "CAP_804";
            IPlayable victim = null;
            IPlayable source;
            if (id is "FIREBALL_GOLEM" or "REBORN")
            {
                victim = EngineEntity.FromCard(owner, Cards.FromId(id == "REBORN" ? "CS2_182" : "EX1_556"), zone: owner.BoardZone);
                if (id == "REBORN") victim[GameTag.REBORN] = 1;
                source = EngineEntity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.HandZone);
            }
            else if (location)
                source = EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.BoardZone);
            else
            {
                if (!playedMinion) EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.BoardZone);
                source = EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone);
            }
            EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.BoardZone); // B
            EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.BoardZone); // C
            if (id == "CAP_804") owner.BoardZone[0][GameTag.REBORN] = 1;
            int formerSlot = source.ZonePosition + 1;
            if (finalCharge) source[GameTag.DAMAGE] = source[GameTag.HEALTH] - 1;
            var existing = owner.BoardZone.Select(m => m.Id).Append(source.Id).ToHashSet();
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            var task = location ? (PlayerTask)owner.Options().OfType<ActivateLocationTask>().First(t => t.Source == source)
                : PlayCardTask.Any(owner, source, victim as ICharacter ?? (id == "CAP_804" ? owner.BoardZone[0] : null), zonePosition: 1);
            if (bot) InvokeProcess(match, task);
            else
            {
                typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
                var options = capture.Last<PegasusGame.AllOptions>(14);
                int index = options.Options.FindIndex(o => o.MainOption?.Id == source.Id);
                Check(index >= 0, name + ": real native option exists");
                match.SendOption(options.Id, index, task.ActionTarget?.Id ?? 0, -1, playedMinion ? 2 : 0);
            }
            FlushFieldHistory(match);
            var rows = History(capture, cursor);
            var summons = owner.BoardZone.Where(m => !existing.Contains(m.Id)).ToArray();
            void Expect(bool ok, string message) { if (!ok) failures.Add(name + ": " + message); }
            Expect(summons.Length == (id == "CS2_027" ? 2 : 1), "expected summon count");
            foreach (var summon in summons)
            {
                int at = rows.FindIndex(e => e.HasFullEntity && e.FullEntity.Entity == summon.Id);
                Expect(at >= 0, summon.Card.Id + " has FULL_ENTITY");
                if (at < 0) continue;
                Expect(SnapshotTag(rows[at], GameTag.ZONE) == (int)SabberStoneCore.Enums.Zone.PLAY
                    && SnapshotTag(rows[at], GameTag.ZONE_POSITION) == summon.ZonePosition + 1,
                    summon.Card.Id + " FULL_ENTITY carries final nonzero slot");
                int creator = victim?.Id ?? source.Id;
                Expect(SnapshotTag(rows[at], GameTag.CREATOR) == creator, summon.Card.Id + " FULL_ENTITY carries source CREATOR=" + creator);
                Expect(SnapshotTag(rows[at], SummonSourceTag) == creator, summon.Card.Id + " FULL_ENTITY 1254 equals CREATOR/source");
                Expect(summon[GameTag.CREATOR] == 0, "wire CREATOR leaves engine state unchanged");
                Expect(summon[SummonSourceTag] == 0, "wire 1254 leaves engine state unchanged");
                if (victim != null)
                {
                    int deaths = rows.FindIndex(e => e.HasPowerStart && (int)e.PowerStart.Type == (int)BlockType.DEATHS);
                    Expect(deaths >= 0 && deaths < at, "real damage resolves DEATHS before summon");
                    if (id == "FIREBALL_GOLEM") Expect(rows.Take(at).Any(e => e.HasPowerStart
                        && (int)e.PowerStart.Type == (int)BlockType.TRIGGER && e.PowerStart.Source == victim.Id), "dead minion owns TRIGGER");
                }
                if (location)
                {
                    Expect(SnapshotTag(rows[at], GameTag.ZONE_POSITION) == formerSlot + (finalCharge ? 0 : 1),
                        "FULL_ENTITY is right of living location or in its former slot on final charge");
                    Expect(finalCharge ? source.Zone == owner.GraveyardZone : source.ZonePosition + 1 == formerSlot,
                        "final charge retires location; living location keeps its slot");
                }
                if (id == "JAIL_877") Expect(!rows.Any(e => e.HasSubSpellStart
                    && e.SubSpellStart.SpellPrefabGuid.Contains("ReuseFX_Beast_OverrideSpawn_Paw_Super")),
                    "standard spawn has no Paw override sub-spell");
                var open = new Stack<int>();
                for (int i = 0; i < at; i++)
                {
                    if (rows[i].HasPowerStart) open.Push(i);
                    else if (rows[i].HasPowerEnd) open.Pop();
                }
                Expect(open.Count > 0, "summon belongs to an effect block");
                if (open.Count == 0) continue;
                int start = open.Peek(), end = start + 1, depth = 1;
                Expect((int)rows[start].PowerStart.Type == (int)BlockType.POWER
                    || (int)rows[start].PowerStart.Type == (int)BlockType.TRIGGER
                    || id == "REBORN" && (int)rows[start].PowerStart.Type == (int)BlockType.PLAY, "summon stays in its effect or Reborn resolution block");
                for (; end < rows.Count; end++)
                {
                    if (rows[end].HasPowerStart) depth++;
                    if (rows[end].HasPowerEnd && --depth == 0) break;
                }
                // Ignore retirement/death left shifts before the summon's insertion shifts.
                if (id == "REBORN" || location && finalCharge)
                {
                    int deaths = rows.FindIndex(e => e.HasPowerStart && (int)e.PowerStart.Type == (int)BlockType.DEATHS);
                    start = deaths + 1; int deathDepth = 1;
                    for (; start < at; start++)
                    {
                        if (rows[start].HasPowerStart) deathDepth++;
                        if (rows[start].HasPowerEnd && --deathDepth == 0) break;
                    }
                }
                var block = rows.Skip(start + 1).Take(end - start - 1).ToArray();
                Expect(!block.Any(e => e.HasTagChange && e.TagChange.Entity == summon.Id
                    && e.TagChange.Tag == (int)GameTag.ZONE_POSITION), "no summon's own position TAG_CHANGE");
                Expect(!rows.Any(e => e.HasShowEntity && e.ShowEntity.Entity == summon.Id), "no summon SHOW_ENTITY");
                var right = Enumerable.Range(summon.ZonePosition + 1, owner.BoardZone.SlotCount - summon.ZonePosition - 1)
                    .Select(i => owner.BoardZone.GetSlot(i)).Where(m => existing.Contains(m.Id)).Reverse().ToArray();
                var shifts = block.Where(e => e.HasTagChange && e.TagChange.Tag == (int)GameTag.ZONE_POSITION
                    && right.Any(m => m.Id == e.TagChange.Entity)).ToArray();
                Expect(shifts.Select(e => e.TagChange.Entity).SequenceEqual(right.Select(m => m.Id)),
                    "every neighbour shift, right-most first");
                Expect(shifts.All(e => rows.IndexOf(e) < at), "neighbours shift before FULL_ENTITY");
                Expect(shifts.All(e => e.TagChange.Value == right.Single(m => m.Id == e.TagChange.Entity).ZonePosition + 1),
                    "neighbours shift to final slots");
            }
            capture.SerializeAll();
            cases++;
        }
        foreach (bool bot in new[] { false, true }) EffectSummonCreatorPaths(failures, bot);
        EffectSummonCreatorPrivacy(failures);
        Check(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine($"PASS effect summon slots: {cases} cases, both seats, location/Deathrattle/spell/Battlecry/Herald/Reborn/copy");
    }
    private static void EffectSummonCreatorPrivacy(List<string> failures)
    {
        foreach (bool bot in new[] { false, true })
        foreach (var zone in new[] { SabberStoneCore.Enums.Zone.HAND, SabberStoneCore.Enums.Zone.DECK })
        {
            var (match, capture) = AnimationMatch(bot);
            var game = match.Game; var owner = game.CurrentPlayer;
            var source = EngineEntity.FromCard(owner, Cards.FromId("CS2_182"),
                zone: zone == SabberStoneCore.Enums.Zone.HAND ? owner.HandZone : owner.DeckZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            new SummonTask("CS2_231").Process(game, owner, source, null);
            var summon = owner.BoardZone.Last();
            FlushFieldHistory(match);
            var rows = History(capture, cursor);
            var full = rows.Single(e => e.HasFullEntity && e.FullEntity.Entity == summon.Id);
            bool hidden = bot || zone == SabberStoneCore.Enums.Zone.DECK;
            if (summon[GameTag.CREATOR] != 0 || SnapshotTag(full, GameTag.CREATOR) != (hidden ? 0 : source.Id))
                failures.Add($"creator privacy {bot}/{zone}: engine unchanged; transport masks hidden source");
            var raw = game.PowerHistory.Full.OfType<PowerHistoryFullEntity>().Single(e => e.Entity.Id == summon.Id);
            if (!raw.Entity.Tags.TryGetValue(GameTag.CREATOR, out int creator) || creator != source.Id)
                failures.Add($"creator privacy {bot}/{zone}: history FULL lacks source CREATOR");
            if (!raw.Entity.Tags.TryGetValue(SummonSourceTag, out int summoner) || summoner != source.Id
                || SnapshotTag(full, SummonSourceTag) != (hidden ? 0 : source.Id) || summon[SummonSourceTag] != 0)
                failures.Add($"summon source privacy {bot}/{zone}: raw FULL 1254 names source; wire masks hidden source; engine unchanged");
            if (hidden && rows.Any(e => e.HasTagChange && e.TagChange.Entity == summon.Id
                && e.TagChange.Tag == (int)GameTag.CREATOR && e.TagChange.Value != 0))
                failures.Add($"creator privacy {bot}/{zone}: CREATOR tag change leaked hidden source");
            var state = FairBotState.Create(game, owner.Opponent.PlayerId, 77, DeckStyle.Value);
            var view = (SabberStoneCore.Model.Game)typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state);
            if (view.IdEntityDic[summon.Id][GameTag.CREATOR] != 0) failures.Add("AI fair view retained hidden CREATOR");
            // An existing provenance link survives a later summon, even when its immediate summoner differs.
            var copy = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_231"),
                new Dictionary<GameTag, int> { [GameTag.CREATOR] = owner.Hero.Id });
            Generic.SummonBlock(game, copy, -1, source);
            if (copy[GameTag.CREATOR] != owner.Hero.Id) failures.Add("summon overwrote existing CREATOR");
            FlushFieldHistory(match);
            var preserved = History(capture, cursor).Single(e => e.HasFullEntity && e.FullEntity.Entity == copy.Id);
            if (SnapshotTag(preserved, GameTag.CREATOR) != owner.Hero.Id) failures.Add("wire overwrote existing CREATOR");
            if (SnapshotTag(preserved, SummonSourceTag) != owner.Hero.Id) failures.Add("FULL 1254 differs from preserved CREATOR");
            // Already registered public actors exercise TAG_CHANGE rather than a folded FULL_ENTITY.
            FlushFieldHistory(match); cursor = capture.Packets.Count;
            game.PowerHistory.Add(PowerHistoryBuilder.TagChange(summon.Id, GameTag.CREATOR, source.Id));
            copy[GameTag.CREATOR] = source.Id;
            game.PowerHistory.Add(PowerHistoryBuilder.TagChange(copy.Id, SummonSourceTag, source.Id));
            game.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(copy, source.Id, source.Id));
            FlushFieldHistory(match);
            var injected = History(capture, cursor).Single(e => e.HasTagChange && e.TagChange.Entity == summon.Id
                && e.TagChange.Tag == (int)GameTag.CREATOR);
            if (injected.TagChange.Value != (hidden ? 0 : source.Id) || summon[GameTag.CREATOR] != 0)
                failures.Add("wire-only CREATOR delta must mask hidden source without changing engine state");
            var change = History(capture, cursor).Single(e => e.HasTagChange && e.TagChange.Entity == copy.Id
                && e.TagChange.Tag == (int)GameTag.CREATOR);
            if (change.TagChange.Value != source.Id) failures.Add("engine-held CREATOR delta must pass through");
            var summonChange = History(capture, cursor).Single(e => e.HasTagChange && e.TagChange.Entity == copy.Id
                && e.TagChange.Tag == (int)SummonSourceTag);
            if (summonChange.TagChange.Value != (hidden ? 0 : source.Id) || copy[SummonSourceTag] != 0)
                failures.Add("1254 delta must mask hidden source without changing engine state");
            var shown = History(capture, cursor).Single(e => e.HasShowEntity && e.ShowEntity.Entity == copy.Id);
            if (SnapshotTag(shown, GameTag.CREATOR) != source.Id || SnapshotTag(shown, SummonSourceTag) != (hidden ? 0 : source.Id))
                failures.Add("engine-held CREATOR snapshot must pass through while 1254 masks hidden source");
            var returning = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_231"), zone: owner.HandZone);
            FlushFieldHistory(match); cursor = capture.Packets.Count;
            owner.HandZone.Remove(returning);
            Generic.SummonBlock(game, returning, -1, source);
            FlushFieldHistory(match);
            var revealed = History(capture, cursor).Last(e => e.HasShowEntity && e.ShowEntity.Entity == returning.Id);
            var rawReveal = game.PowerHistory.Full.OfType<PowerHistoryShowEntity>().Last(e => e.Entity.Id == returning.Id);
            if (SnapshotTag(revealed, GameTag.CREATOR) != 0 || SnapshotTag(revealed, SummonSourceTag) != (hidden ? 0 : source.Id)
                || !rawReveal.Entity.Tags.TryGetValue(SummonSourceTag, out int revealSource) || revealSource != source.Id
                || returning[SummonSourceTag] != 0)
                failures.Add($"summon source privacy {bot}/{zone}: SHOW 1254 masks hidden source; CREATOR and engine unchanged");
            state = FairBotState.Create(game, owner.Opponent.PlayerId, 77, DeckStyle.Value);
            view = (SabberStoneCore.Model.Game)typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state);
            if (view.IdEntityDic[copy.Id][GameTag.CREATOR] != 0) failures.Add("AI fair view retained existing hidden CREATOR");
            capture.SerializeAll();
        }
    }

    private static void EffectSummonCreatorPaths(List<string> failures, bool bot)
    {
        var (match, capture) = AnimationMatch(bot);
        var game = match.Game; var owner = game.CurrentPlayer; var source = owner.Hero;
        void ClearBoard()
        {
            foreach (var old in owner.BoardZone.Concat(owner.Opponent.BoardZone).ToArray())
                old.Controller.SetasideZone.Add(old.Zone.Remove(old));
        }
        void Created(IPlayable minion, string path, int creator, int previous = 0)
        {
            FlushFieldHistory(match);
            var full = History(capture, 0).Single(e => e.HasFullEntity && e.FullEntity.Entity == minion.Id);
            if (SnapshotTag(full, GameTag.CREATOR) != creator || minion[GameTag.CREATOR] != previous)
                failures.Add(path + ": FULL_ENTITY CREATOR=" + creator + "; engine CREATOR remains " + previous);
            if (SnapshotTag(full, SummonSourceTag) != creator || minion[SummonSourceTag] != 0)
                failures.Add(path + ": FULL_ENTITY 1254 equals CREATOR; engine 1254 remains zero");
        }
        ClearBoard();
        var untouchable = EngineEntity.FromCard(owner, Cards.FromId("CS2_231"),
            new Dictionary<GameTag, int> { [GameTag.UNTOUCHABLE] = 1 });
        var stack = new TaskStack(); stack.AddPlayable(untouchable);
        new SummonTask().Process(game, owner, source, null, stack);
        Created(untouchable, "Untouchable bypass", source.Id);
        new SummonOpTask("CS2_231").Process(game, owner, source, null);
        Created(owner.Opponent.BoardZone.Last(), "SummonOpTask", source.Id);
        var original = EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.BoardZone);
        var copy = Generic.Copy(owner, source, original, SabberStoneCore.Enums.Zone.PLAY);
        Created(copy, "Generic.Copy to PLAY", source.Id);
        var khadgar = EngineEntity.FromCard(owner, Cards.FromId("CORE_DAL_575"), zone: owner.BoardZone);
        new SummonTask("CS2_231").Process(game, owner, source, null);
        Check(owner.BoardZone.Count == 6, "Khadgar makes one deferred copy");
        Created(owner.BoardZone[4], "Khadgar original", source.Id);
        Created(owner.BoardZone[5], "Khadgar deferred copy", khadgar.Id);
        ClearBoard();
        foreach (var zone in new[] { owner.HandZone, (SabberStoneCore.Model.Zones.Zone<IPlayable>)owner.SetasideZone })
        {
            original = EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: zone);
            new SummonCopyTask(EntityType.TARGET).Process(game, owner, source, original);
            Created(owner.BoardZone.Last(), "SummonCopyTask off-board", source.Id);
        }
        original = EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.BoardZone);
        new SummonCopyTask(EntityType.TARGET).Process(game, owner, source, original);
        Created(owner.BoardZone.Last(), "SummonCopyTask board", source.Id);
        ClearBoard();
        // Existing public provenance must survive a different summoner on both state and wire.
        var inherited = EngineEntity.FromCard(owner, Cards.FromId("CS2_231"),
            new Dictionary<GameTag, int> { [GameTag.CREATOR] = owner.Opponent.Hero.Id });
        Generic.SummonBlock(game, (Minion)inherited, -1, source);
        Created(inherited, "existing CREATOR", owner.Opponent.Hero.Id, owner.Opponent.Hero.Id);
        var observed = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_231"),
            new Dictionary<GameTag, int> { [GameTag.ZONE] = (int)SabberStoneCore.Enums.Zone.PLAY });
        game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, source.Id, "", -1, 0));
        game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
        Generic.SummonBlock(game, observed, -1, source);
        Created(observed, "PLAY FULL across block edge", source.Id);
        ClearBoard();
        // Retail recruits reveal with CREATOR=0 (34104_subspell.power.log:38866-38885).
        void Revealed(Minion minion, int cursor, string path, int summoner, int? visibleSummoner = null)
        {
            FlushFieldHistory(match);
            var rows = History(capture, cursor);
            var shows = rows.Where(e => e.HasShowEntity && e.ShowEntity.Entity == minion.Id).ToArray();
            var raw = game.PowerHistory.Full.OfType<PowerHistoryShowEntity>().LastOrDefault(e => e.Entity.Id == minion.Id);
            if (minion.Zone != owner.BoardZone || shows.Length == 0 || shows.Any(e => SnapshotTag(e, GameTag.CREATOR) != 0)
                || raw == null || raw.Entity.Tags.TryGetValue(GameTag.CREATOR, out int creator) && creator != 0
                || minion[GameTag.CREATOR] != 0)
                failures.Add($"{path}/{(bot ? "opponent" : "owner")}: summon SHOW_ENTITY must not acquire CREATOR");
            if (rows.Any(e => e.HasFullEntity && e.FullEntity.Entity == minion.Id))
                failures.Add(path + ": existing entity must not be recreated");
            // Zone entry can reveal first; the final SHOW is the summon fallback carrying its effect source.
            if (shows.Length == 0 || SnapshotTag(shows.Last(), SummonSourceTag) != (visibleSummoner ?? summoner)
                || raw == null || !raw.Entity.Tags.TryGetValue(SummonSourceTag, out int rawSource) || rawSource != summoner
                || minion[SummonSourceTag] != 0)
                failures.Add($"{path}/{(bot ? "opponent" : "owner")}: summon SHOW 1254 names summoner; engine unchanged");
        }
        // Hidden-zone and flushed no-zone creations cannot fold; neither fallback invents provenance.
        foreach (bool bypass in new[] { false, true })
        foreach (bool fromHand in new[] { true, false })
        {
            var returning = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_231"),
                new Dictionary<GameTag, int> { [GameTag.UNTOUCHABLE] = bypass ? 1 : 0 }, zone: fromHand ? owner.HandZone : null);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            if (fromHand) owner.HandZone.Remove(returning);
            stack = new TaskStack(); stack.AddPlayable(returning);
            new SummonTask().Process(game, owner, source, null, stack);
            Revealed(returning, cursor, $"refused fold/fromHand={fromHand}/Untouchable={bypass}", source.Id);
        }
        ClearBoard();
        foreach (var old in owner.DeckZone.ToArray()) owner.SetasideZone.Add(owner.DeckZone.Remove(old));
        var recruit = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_231"), zone: owner.DeckZone);
        var gather = EngineEntity.FromCard(owner, Cards.FromId("LOOT_370"), zone: owner.HandZone); // Gather Your Party: RecruitTask.
        FlushFieldHistory(match);
        int recruitCursor = capture.Packets.Count;
        InvokeProcess(match, PlayCardTask.Any(owner, gather));
        Revealed(recruit, recruitCursor, "Gather Your Party deck recruit", gather.Id);
        var recruitRows = History(capture, recruitCursor);
        int revealAt = recruitRows.FindIndex(e => e.HasShowEntity && e.ShowEntity.Entity == recruit.Id);
        // 1254 names only the public recruiting spell, never the pulled card; it reveals nothing before SHOW_ENTITY does.
        if (gather.Zone.Type != SabberStoneCore.Enums.Zone.GRAVEYARD || gather.Id == recruit.Id
            || !recruitRows.Take(revealAt).Any(e => e.HasPowerStart && e.PowerStart.Source == gather.Id
                && (int)e.PowerStart.Type == (int)BlockType.PLAY)
            || !recruitRows.Take(revealAt).Any(e => e.HasTagChange && e.TagChange.Entity == gather.Id
                && e.TagChange.Tag == (int)GameTag.ZONE && e.TagChange.Value == (int)SabberStoneCore.Enums.Zone.PLAY))
            failures.Add("deck recruit: 1254 source must be public/in cast before pulled card SHOW_ENTITY");
        foreach (var zone in new[] { owner.HandZone, (SabberStoneCore.Model.Zones.Zone<IPlayable>)owner.DeckZone })
        {
            var deferred = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_231"), zone: zone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            SwapRules.DeferSummon(deferred);
            game.DeathProcessingAndAuraUpdate(); // Resolves the queued hand/deck summon with itself as summoner.
            Revealed(deferred, cursor, "ResolveDeferredSummons from " + zone.Type, deferred.Id);
        }
        var outgoing = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.BoardZone);
        var incoming = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_231"), zone: owner.Opponent.HandZone);
        FlushFieldHistory(match);
        int swapCursor = capture.Packets.Count;
        Check(SwapRules.ExchangeBoardAndHand(outgoing, incoming), "board/hand exchange succeeds");
        Revealed(incoming, swapCursor, "SwapRules.ExchangeBoardAndHand", outgoing.Id, bot ? outgoing.Id : 0);
        var simulation = new SabberStoneCore.Model.Game(new SabberStoneCore.Config.GameConfig {
            RandomSeed = 77, StartPlayer = 1, SkipMulligan = true, History = false, Logging = false });
        simulation.StartGame();
        var simulatedOwner = simulation.CurrentPlayer;
        var simulated = (Minion)EngineEntity.FromCard(simulatedOwner, Cards.FromId("CS2_231"));
        Generic.SummonBlock(simulation, simulated, -1, simulatedOwner.Hero);
        if (simulation.History || simulated[GameTag.CREATOR] != 0 || simulated[SummonSourceTag] != 0)
            failures.Add("History=false leaves engine CREATOR/1254 unchanged");
        capture.SerializeAll();
    }

}
