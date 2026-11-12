using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using SabberStoneCore.Kettle;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    // FIR_909 live cleanup clears direct spell missiles; non-spell/reveal/death routes still need live acceptance.
    // CATA_582/JAIL_307 keep area pulses; their single-target fallback uses the admitted missile.
    private static readonly HashSet<string> QuarantinedFireRoutes = new HashSet<string>(("CATA_586 CORE_WC_701 "
        + "FIR_778 JAIL_007 TLC_249 JAIL_458t2 FIR_955 TLC_220 FIR_904 FIR_902 END_024").Split(' '));
    private static bool FireQuarantined(string id) => QuarantinedFireRoutes.Contains(id);

    private static void ModernSubSpellHistory()
    {
        WispHeroPowerHistory();
        VolcanoAndCannoneerVisuals();
        SleetStormVisual();
        DrinkBloodVisual();
        FireSpellVisuals();
        DamageWaveVisuals();
        VariableAreaVisuals();
        SylvanasFullBoardVisual();
        ChooseDamageVisuals();
        AdditionalDamageRoutes();
        BattlecryDamageVisuals();
        GeneratedDamageVisuals();
        Issue26EffectRoutes();
        RottenAppleVisual();
        DeathrattleDamageVisuals();
        WeaponDamageVisuals();
        ConditionalBattlecryVisuals();
        EventTriggerDamageVisuals();
        DelayedDamageVisuals();
        SkeletonKeyRefreshVisual();
        PublicTriggerRevealBoundaries();
        DamageGroupScopeBoundaries();
        VulcanosEndTurnVisual();
        foreach (bool bot in new[] { false, true })
        foreach (string card in new[] { "TIME_001", "JAIL_225", "END_000" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot, deck: "showcase-rogue-imbue");
            var owner = match.Game.CurrentPlayer;
            Character victim = owner.Opponent.Hero;
            if (card == "JAIL_225")
                victim = (Character)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
            var source = EngineEntity.FromCard(owner, Cards.FromId(card), zone: owner.HandZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id
                && (t.Target == null || t.Target.Id == victim.Id)));
            FlushFieldHistory(match);
            var entries = History(capture, cursor);
            int hits = card == "TIME_001" ? 3 : 1;
            Check(entries.Count(e => e.HasSubSpellStart) == hits, card + " resolved hits each have a sub-spell");
            int depth = 0, damage = 0;
            foreach (var entry in entries)
            {
                if (entry.HasSubSpellStart)
                {
                    Check(depth++ == 0 && entry.SubSpellStart.TargetEntityIds.SequenceEqual(new[] { victim.Id }), card + " ordered resolved target");
                    Check(entry.SubSpellStart.SourceEntityId == owner.Hero.Id, card + " visual source is public hero");
                }
                if (entry.HasMetaData && (int)entry.MetaData.Type == 1)
                { Check(depth == 1, card + " damage inside scope"); damage++; }
                if (entry.HasSubSpellEnd) Check(--depth == 0, card + " balanced end");
            }
            Check(depth == 0 && damage == hits, card + " all hits complete");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS modern sub-spell hits: Chrono Daggers, Nab, Eventuality; both players");
        ModernSubSpellFamilies();
        SubSpellTargetPrivacy();
        SubSpellDeterminism();
        SubSpellScopeBoundaries();
        SubSpellTriggeredCreate();
    }

    private static void PublicTriggerRevealBoundaries()
    {
        var (match, _) = AnimationMatch(); var owner = match.Game.CurrentPlayer;
        var secret = EngineEntity.FromCard(owner.Opponent, Cards.FromId("END_024"), zone: owner.Opponent.HandZone);
        FlushFieldHistory(match);
        var track = typeof(NativeMatch).GetMethod("TrackVisibility", BindingFlags.Instance | BindingFlags.NonPublic);
        var convert = typeof(NativeMatch).GetMethod("ToNativeHistory", BindingFlags.Instance | BindingFlags.NonPublic);
        var show = new PegasusGame.PowerHistoryData { ShowEntity = new PegasusGame.PowerHistoryEntity { Entity = secret.Id, Name = "END_024" } };
        var cue = new SabberStoneCore.Kettle.PowerHistorySubSpellStart { Source = owner.Opponent.Hero.Id, Targets = new List<int> { owner.Hero.Id },
            SpellPrefabGuid = "ReuseFX_Fire_Missile_Fireball_Medium_Super_Simultaneous:161d5760bd9de1e41baf4c5611e61eed", RequirePublicOrigin = true };
        void Track(PegasusGame.PowerHistoryData row) => track.Invoke(match, new object[] { row });
        bool Trigger()
        {
            convert.Invoke(match, new object[] { PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, secret.Id, "", -1, 0) });
            bool visible = convert.Invoke(match, new object[] { cue }) != null;
            convert.Invoke(match, new object[] { new SabberStoneCore.Kettle.PowerHistorySubSpellEnd() });
            convert.Invoke(match, new object[] { PowerHistoryBuilder.BlockEnd() });
            return visible;
        }
        Track(show); Check(Trigger(), "named reveal survives packet boundary until its trigger");
        Check(!Trigger(), "named reveal is consumed after one trigger");
        Track(show);
        convert.Invoke(match, new object[] { PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, secret.Id, "", -1, 0) });
        convert.Invoke(match, new object[] { PowerHistoryBuilder.BlockEnd() });
        Check(!Trigger(), "trigger without cue consumes its reveal");
        Track(show);
        convert.Invoke(match, new object[] { PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, secret.Id, "", -1, 0) });
        Track(new PegasusGame.PowerHistoryData { HideEntity = new PegasusGame.PowerHistoryHide { Entity = secret.Id, Zone = (int)SabberStoneCore.Enums.Zone.HAND } });
        Check(convert.Invoke(match, new object[] { cue }) == null, "hide inside trigger revokes its active reveal");
        convert.Invoke(match, new object[] { new SabberStoneCore.Kettle.PowerHistorySubSpellEnd() });
        convert.Invoke(match, new object[] { PowerHistoryBuilder.BlockEnd() });
        foreach (var revoke in new[] {
            new PegasusGame.PowerHistoryData { TagChange = new PegasusGame.PowerHistoryTagChange { Entity = secret.Id, Tag = (int)GameTag.ZONE, Value = (int)SabberStoneCore.Enums.Zone.DECK } },
            new PegasusGame.PowerHistoryData { ShowEntity = new PegasusGame.PowerHistoryEntity { Entity = secret.Id, Name = "" } },
            new PegasusGame.PowerHistoryData { ChangeEntity = new PegasusGame.PowerHistoryEntity { Entity = secret.Id, Name = "FIR_902" } },
            new PegasusGame.PowerHistoryData { ShowEntity = new PegasusGame.PowerHistoryEntity { Entity = secret.Id, Name = "FIR_902" } }
        })
        {
            Track(new PegasusGame.PowerHistoryData { HideEntity = new PegasusGame.PowerHistoryHide { Entity = secret.Id, Zone = (int)SabberStoneCore.Enums.Zone.HAND } });
            Track(show);
            convert.Invoke(match, new object[] { PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, secret.Id, "", -1, 0) });
            Track(revoke);
            Check(convert.Invoke(match, new object[] { cue }) == null, "identity or privacy change revokes active reveal");
            convert.Invoke(match, new object[] { new SabberStoneCore.Kettle.PowerHistorySubSpellEnd() });
            convert.Invoke(match, new object[] { PowerHistoryBuilder.BlockEnd() });
            Check(!Trigger(), "identity or privacy change cannot authorize another trigger");
        }
        Track(new PegasusGame.PowerHistoryData { HideEntity = new PegasusGame.PowerHistoryHide { Entity = secret.Id, Zone = (int)SabberStoneCore.Enums.Zone.HAND } });
        Track(show); Track(new PegasusGame.PowerHistoryData { HideEntity = new PegasusGame.PowerHistoryHide { Entity = secret.Id, Zone = (int)SabberStoneCore.Enums.Zone.HAND } });
        Check(!Trigger(), "HIDE invalidates pending reveal");
        Track(show); Track(new PegasusGame.PowerHistoryData { TagChange = new PegasusGame.PowerHistoryTagChange { Entity = secret.Id, Tag = (int)GameTag.ZONE, Value = (int)SabberStoneCore.Enums.Zone.SECRET } });
        Check(!Trigger(), "private-zone move invalidates pending reveal");
        Track(show); Track(new PegasusGame.PowerHistoryData { ShowEntity = new PegasusGame.PowerHistoryEntity { Entity = secret.Id, Name = "" } });
        Check(!Trigger(), "anonymous snapshot invalidates pending reveal");
        Track(show); Track(new PegasusGame.PowerHistoryData { ChangeEntity = new PegasusGame.PowerHistoryEntity { Entity = secret.Id, Name = "END_024", Tags = new List<PegasusGame.Tag> { new PegasusGame.Tag { Name = (int)GameTag.ZONE, Value = (int)SabberStoneCore.Enums.Zone.DECK } } } });
        Check(!Trigger(), "private snapshot invalidates pending reveal");
        Track(show); Track(new PegasusGame.PowerHistoryData { ResetGame = new PegasusGame.PowerHistoryResetGame() });
        Check(!Trigger(), "reset invalidates pending reveal");
        Console.WriteLine("PASS causal reveal chunks, consumption, hide, private move, anonymous and reset");
    }

    private static void SkeletonKeyRefreshVisual()
    {
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: bot, deck: "showcase-rogue-imbue"); var owner = match.Game.CurrentPlayer;
            foreach (var old in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(old));
            var source = SabberStoneCore.Actions.Generic.DrawCard(owner, Cards.FromId("JAIL_319")); source.Cost = 0;
            FlushFieldHistory(match); int initial = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match); int attempts = 0;
            while (owner.Hero.Damage == 0 && attempts++ < 30)
            {
                var choice = owner.Choice; var offers = choice.Choices.ToArray();
                int refresh = offers.Single(id => match.Game.IdEntityDic[id].Card.Id == "JAIL_319t");
                int cursor = capture.Packets.Count;
                var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
                foreach (var game in new[] { on, off }) game.Process(ChooseTask.Pick(game.CurrentPlayer, refresh));
                Check(on.Hash() == off.Hash(), "Skeleton Key refresh cue leaves state unchanged");
                var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
                var left = random.GetValue(on); var right = random.GetValue(off); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
                for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), "Skeleton Key refresh cue leaves RNG unchanged");
                InvokeProcess(match, ChooseTask.Pick(owner, refresh)); FlushFieldHistory(match); var entries = History(capture, cursor);
                var shots = entries.Where(e => e.HasSubSpellStart).Select(e => e.SubSpellStart).ToArray();
                Check(shots.Length == (owner.Hero.Damage > 0 ? 1 : 0), "Skeleton Key only cues damaging refresh");
                if (shots.Length > 0) Check(shots[0].SourceEntityId == owner.Hero.Id && shots[0].TargetEntityIds.SequenceEqual(new[] { owner.Hero.Id })
                    && shots[0].SpellPrefabGuid == "ReuseFX_Physical_Impact_Slam_Super:4d1b99bee3bc05d489e03086bf27e295", "Skeleton Key impact uses public hero");
                if (bot) Check(!entries.Any(e => (e.HasShowEntity && offers.Contains(e.ShowEntity.Entity) && !string.IsNullOrEmpty(e.ShowEntity.Name))
                    || (e.HasFullEntity && offers.Contains(e.FullEntity.Entity) && !string.IsNullOrEmpty(e.FullEntity.Name))), "opponent refresh offers remain anonymous");
            }
            Check(owner.Hero.Damage == 5 && owner.Choice != null, "Skeleton Key damage and next decision preserved");
            var legal = owner.Choice.Choices.First(id => match.Game.IdEntityDic[id].Card.Id != "JAIL_319t");
            InvokeProcess(match, ChooseTask.Pick(owner, legal)); FlushFieldHistory(match);
            Check(owner.Choice == null, "Skeleton Key non-refresh choice finishes");
            Check(History(capture, initial).Any(e => e.HasPowerStart && e.PowerStart.Source == source.Id && (int)e.PowerStart.Type == (int)BlockType.POWER),
                "Skeleton Key original public power spans continuation");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Skeleton Key public continuation, private refresh choices and RNG, both seats");
    }

    private static void DelayedDamageVisuals()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string id in new[] { "FIR_902", "END_024", "JAIL_881t", "JAIL_443t", "TIME_025t" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot); var owner = match.Game.CurrentPlayer;
            var enemy = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone); enemy.BaseHealth = 30;
            bool drawn = id is "JAIL_881t" or "JAIL_443t" or "TIME_025t";
            if (drawn) foreach (var old in owner.DeckZone.ToArray()) owner.SetasideZone.Add(owner.DeckZone.Remove(old));
            var source = drawn ? EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.DeckZone)
                : SabberStoneCore.Actions.Generic.DrawCard(owner, Cards.FromId(id));
            source.Cost = 0;
            FlushFieldHistory(match);
            if (!drawn) { InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id)); FlushFieldHistory(match); }
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.END_TURN));
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var nextPlayer = match.Game.CurrentPlayer;
            var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
            foreach (var game in new[] { on, off }) game.Process(game.CurrentPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.END_TURN));
            Check(on.Hash() == off.Hash(), id + " delayed cue leaves state unchanged");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
            var left = random.GetValue(on); var right = random.GetValue(off); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), id + " delayed cue leaves RNG unchanged");
            InvokeProcess(match, nextPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.END_TURN));
            FlushFieldHistory(match); var entries = History(capture, cursor);
            int begin = entries.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == source.Id && (int)e.PowerStart.Type == (int)(id == "TIME_025t" ? BlockType.POWER : BlockType.TRIGGER));
            Check(begin >= 0, id + " actual delayed source block"); int depth = 1, end = begin + 1;
            for (; end < entries.Count && depth > 0; end++) { if (entries[end].HasPowerStart) depth++; if (entries[end].HasPowerEnd) depth--; }
            var body = entries.Skip(begin + 1).Take(end - begin - 2).ToArray();
            var shots = body.Where(e => e.HasSubSpellStart).Select(e => e.SubSpellStart).ToArray();
            var damageList = new List<int>(); int nested = 0;
            foreach (var entry in body)
            {
                if (entry.HasPowerStart) nested++;
                if (nested == 0 && entry.HasMetaData && (int)entry.MetaData.Type == 1) damageList.Add(entry.MetaData.Info.Single());
                if (entry.HasPowerEnd) nested--;
            }
            var damage = damageList.ToArray();
            bool hidden = drawn && bot;
            Check(damage.Length > 0 && shots.Length == (hidden || FireQuarantined(id) ? 0 : damage.Length), $"{id} bot={bot} damage={damage.Length} shots={shots.Length}: only public cues");
            if (!hidden && !FireQuarantined(id)) Check(shots.All(s => s.SourceEntityId == owner.Hero.Id) && shots.Select(s => s.TargetEntityIds.Single()).SequenceEqual(damage), id + " preserves delayed targets");
            if (id == "END_024")
            {
                var all = History(capture, 0);
                int reveal = all.FindIndex(e => e.HasShowEntity && e.ShowEntity.Entity == source.Id && e.ShowEntity.Name == id);
                int trigger = all.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == source.Id && (int)e.PowerStart.Type == (int)BlockType.TRIGGER);
                Check(reveal >= 0 && reveal < trigger, "secret reveals before its cue");
            }
            capture.SerializeAll();
        }
        Console.WriteLine("PASS delayed and hidden-origin damage cues, both seats and RNG");
    }

    private static void EventTriggerDamageVisuals()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string id in new[] { "EDR_815", "FIR_955", "TLC_220", "TLC_107", "TLC_840", "FIR_904", "TIME_101" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot); var owner = match.Game.CurrentPlayer;
            var enemy = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone); enemy.BaseHealth = 30;
            var sourceOwner = id == "EDR_815" ? owner.Opponent : owner;
            var source = (Minion)EngineEntity.FromCard(sourceOwner, Cards.FromId(id), zone: sourceOwner.BoardZone); source.BaseHealth = 30; source.IsExhausted = false;
            if (id == "EDR_815") { typeof(SabberStoneCore.Model.Game).GetProperty("EnableCorpses").SetValue(match.Game, true); sourceOwner[GameTag.CORPSES] = 2; }
            bool attack = id is "TLC_107" or "TLC_840";
            IPlayable cause = source;
            if (!attack)
            {
                string causeId = id is "EDR_815" or "TLC_220" ? "CS2_042" : id == "FIR_955" ? "CS2_029" : id == "FIR_904" ? "EX1_596" : "CS2_023";
                if (id == "TIME_101")
                {
                    foreach (var old in owner.DeckZone.ToArray()) owner.SetasideZone.Add(owner.DeckZone.Remove(old));
                    EngineEntity.FromCard(owner, Cards.FromId("CATA_489"), zone: owner.DeckZone);
                }
                cause = EngineEntity.FromCard(owner, Cards.FromId(causeId), zone: owner.HandZone); cause.Cost = 0;
            }
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var choices = owner.Options().Where(t => t.PlayerTaskType == (attack ? PlayerTaskType.MINION_ATTACK : PlayerTaskType.PLAY_CARD) && t.Source?.Id == cause.Id).ToArray();
            var task = id == "FIR_955" ? choices.First(t => t.Target?.Id == owner.Hero.Id)
                : choices.FirstOrDefault(t => t.Target?.Id == enemy.Id) ?? choices.First();
            var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
            foreach (var game in new[] { on, off })
                game.Process(game.CurrentPlayer.Options().First(t => t.PlayerTaskType == task.PlayerTaskType && t.Source?.Id == cause.Id && (t.Target?.Id ?? 0) == (task.Target?.Id ?? 0)));
            Check(on.Hash() == off.Hash(), id + " trigger cue leaves state unchanged");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
            var left = random.GetValue(on); var right = random.GetValue(off); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), id + " trigger cue leaves RNG unchanged");
            InvokeProcess(match, task); FlushFieldHistory(match); var entries = History(capture, cursor);
            int begin = entries.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == source.Id && (int)e.PowerStart.Type == (int)BlockType.TRIGGER);
            Check(begin >= 0, id + " actual source trigger"); int depth = 1, end = begin + 1;
            for (; end < entries.Count && depth > 0; end++) { if (entries[end].HasPowerStart) depth++; if (entries[end].HasPowerEnd) depth--; }
            var body = entries.Skip(begin + 1).Take(end - begin - 2).ToArray();
            var shots = body.Where(e => e.HasSubSpellStart).Select(e => e.SubSpellStart).ToArray();
            var damage = body.Where(e => e.HasMetaData && (int)e.MetaData.Type == 1).Select(e => e.MetaData.Info.Single()).ToArray();
            string prefab = id switch {
                "EDR_815" => "ReuseFX_Decay_Missile_AcidGlob_Super_Simultaneous:1e8b03e89cf4f7a4d8dc823cf8132ad1",
                "TLC_107" or "TLC_840" => "ReuseFX_Physical_Missile_Generic_Super_Simultaneous:6ff0a03c3b353254d887e154c0f94e33",
                "TIME_101" => "ReuseFX_Arcane_Missile_PurpleLightning_Super_Simultaneous:a40e6c32f4253314db432d7808a092de",
                _ => "ReuseFX_Fire_Missile_Fireball_Medium_Super_Simultaneous:161d5760bd9de1e41baf4c5611e61eed" };
            if (FireQuarantined(id)) Check(damage.Length > 0 && shots.Length == 0, id + " quarantined trigger emits no cue");
            else Check(damage.Length > 0 && shots.Length == damage.Length && shots.All(s => s.SourceEntityId == (id == "FIR_904" ? owner.Hero.Id : source.Id)
                && s.SpellPrefabGuid == prefab) && shots.Select(s => s.TargetEntityIds.Single()).SequenceEqual(damage), id + " trigger source and target order");
            if (attack)
            {
                var blocks = new Stack<int>();
                foreach (var entry in entries)
                {
                    if (entry.HasPowerStart) blocks.Push((int)entry.PowerStart.Type);
                    if (entry.HasSubSpellStart) Check(blocks.Count > 0 && blocks.Peek() != (int)BlockType.ATTACK, id + " combat gets no duplicate cue");
                    if (entry.HasPowerEnd && blocks.Count > 0) blocks.Pop();
                }
            }
            capture.SerializeAll();
        }
        Console.WriteLine("PASS event trigger damage distinct from cause and combat, both seats and RNG");
    }

    private static void ConditionalBattlecryVisuals()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string id in new[] { "CORE_RLK_505", "CS3_022", "DINO_138", "EDR_472", "JAIL_379", "JAIL_432", "JAIL_455", "JAIL_501", "TIME_427", "TLC_633", "TLC_825", "EDR_860", "TIME_019", "TLC_987" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot, deck: "showcase-rogue-imbue"); var owner = match.Game.CurrentPlayer;
            var ally = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_200"), zone: owner.BoardZone); ally.BaseHealth = 30;
            var enemy = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_171"), zone: owner.Opponent.BoardZone); enemy.BaseHealth = 30;
            var enemy2 = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone); enemy2.BaseHealth = 30;
            if (id == "CORE_RLK_505") { typeof(SabberStoneCore.Model.Game).GetProperty("EnableCorpses").SetValue(match.Game, true); owner[GameTag.CORPSES] = 2; }
            if (id == "EDR_860") owner[GameTag.IMBUE] = 2;
            if (id is "DINO_138" or "TLC_825") typeof(Controller).GetField("_cardsPlayedPreviousTurn", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, new[] { Cards.FromId(id) });
            if (id == "EDR_472") { var held = EngineEntity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.HandZone); held.Cost = 6; }
            if (id == "JAIL_379")
            {
                foreach (var oldCard in owner.DeckZone.ToArray()) owner.SetasideZone.Add(owner.DeckZone.Remove(oldCard));
                var held = EngineEntity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.DeckZone); held.Cost = 6;
            }
            if (id is "CS3_022" or "TIME_019" or "TLC_987")
            {
                string setup = id == "CS3_022" ? "CS2_106" : id == "TIME_019" ? "TIME_700" : "TLC_426";
                var previous = EngineEntity.FromCard(owner, Cards.FromId(setup), zone: owner.HandZone); previous.Cost = 0;
                FlushFieldHistory(match);
                InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == previous.Id));
            }
            var source = EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone); source.Cost = 0;
            if (id == "JAIL_432") source[SabberStoneCore.Actions.PayloadRules.HeldCopyPlayed] = 1;
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var choices = owner.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id).ToArray();
            var task = choices.FirstOrDefault(t => t.Target?.Id == enemy.Id) ?? choices.First();
            var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
            foreach (var game in new[] { on, off })
                game.Process(game.CurrentPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id && (t.Target?.Id ?? 0) == (task.Target?.Id ?? 0)));
            Check(on.Hash() == off.Hash(), id + " conditional Battlecry cue leaves state unchanged");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
            var left = random.GetValue(on); var right = random.GetValue(off); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), id + " conditional Battlecry cue leaves RNG unchanged");
            InvokeProcess(match, task); FlushFieldHistory(match); var entries = History(capture, cursor);
            string prefab = id switch {
                "CORE_RLK_505" => "ReuseFX_Necrotic_Missile_Bone_Small_Super:d25c28f8f4b12964d8ab765bac643495",
                "DINO_138" => "ReuseFX_Decay_Missile_AcidGlob_Super_Simultaneous:1e8b03e89cf4f7a4d8dc823cf8132ad1",
                "JAIL_432" => "ReuseFX_Shadow_Missile_Blast_Small_Super_Simultaneous:e39b07044149c034d9b1d70182afe5b0",
                "EDR_472" or "TIME_019" or "TLC_987" => "ReuseFX_Arcane_Missile_PurpleLightning_Super_Simultaneous:a40e6c32f4253314db432d7808a092de",
                "TIME_427" or "EDR_860" => "ReuseFX_Holy_Missile_Smite_Medium_Super_Quick_FastActorTriggers:30caf938696e3224bb610c73b35469e6",
                _ => "ReuseFX_Physical_Missile_Generic_Super_Simultaneous:6ff0a03c3b353254d887e154c0f94e33" };
            var shots = entries.Where(e => e.HasSubSpellStart && e.SubSpellStart.SpellPrefabGuid == prefab).Select(e => e.SubSpellStart).ToArray();
            var damage = entries.Where(e => e.HasMetaData && (int)e.MetaData.Type == 1).Select(e => e.MetaData.Info.Single()).ToArray();
            Check(damage.Length > 0 && shots.Length == damage.Length && shots.All(s => s.SourceEntityId == source.Id)
                && shots.Select(s => s.TargetEntityIds.Single()).SequenceEqual(damage), id + " conditional Battlecry source and target order");
            Check(entries.Count(e => e.HasSubSpellStart) == entries.Count(e => e.HasSubSpellEnd), id + " scopes balanced");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS conditional Battlecry damage routes, both seats, state and RNG");
    }

    private static void WeaponDamageVisuals()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string id in new[] { "EDR_842", "TLC_478", "JAIL_458t2" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot); var owner = match.Game.CurrentPlayer;
            foreach (var player in new[] { owner, owner.Opponent })
                for (int i = 0; i < 2; i++)
                {
                    var minion = (Minion)EngineEntity.FromCard(player, Cards.FromId("CS2_200"), zone: player.BoardZone);
                    minion.BaseHealth = 30;
                }
            var weapon = EngineEntity.FromCard(owner, Cards.FromId(id == "JAIL_458t2" ? "JAIL_458" : id), zone: owner.HandZone); weapon.Cost = 0;
            FlushFieldHistory(match);
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == weapon.Id));
            if (id == "JAIL_458t2") {
                int fire = owner.Choice.Choices.Single(choice => match.Game.IdEntityDic[choice].Card.Id == id);
                InvokeProcess(match, ChooseTask.Pick(owner, fire));
            }
            owner.Hero[GameTag.EXHAUSTED] = 0; FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
            foreach (var game in new[] { on, off })
                game.Process(game.CurrentPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == game.CurrentPlayer.Opponent.Hero.Id));
            Check(on.Hash() == off.Hash(), id + " weapon cue leaves game state unchanged");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
            var left = random.GetValue(on); var right = random.GetValue(off); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), id + " weapon cue leaves RNG unchanged");
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == owner.Opponent.Hero.Id));
            FlushFieldHistory(match); var entries = History(capture, cursor);
            int begin = entries.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == weapon.Id && (int)e.PowerStart.Type == (int)BlockType.TRIGGER && (id != "JAIL_458t2" || e.PowerStart.EffectIndex == -1));
            Check(begin >= 0, id + " actual post-attack trigger"); int depth = 1, end = begin + 1;
            for (; end < entries.Count && depth > 0; end++) { if (entries[end].HasPowerStart) depth++; if (entries[end].HasPowerEnd) depth--; }
            var body = entries.Skip(begin + 1).Take(end - begin - 2).ToArray();
            var shots = body.Where(e => e.HasSubSpellStart).Select(e => e.SubSpellStart).ToArray();
            var damage = body.Where(e => e.HasMetaData && (int)e.MetaData.Type == 1).Select(e => e.MetaData.Info.Single()).ToArray();
            string prefab = id == "JAIL_458t2" ? "ReuseFX_Fire_Missile_Fireball_Medium_Super_Simultaneous:161d5760bd9de1e41baf4c5611e61eed" : id == "TLC_478" ? "ReuseFX_Physical_DemonHunter_AE_Whirlwind_BothSides_Small_Super_Quick:bc23f3d70f7ba4a4aa4ec2f8b4163e46"
                : "ReuseFX_Decay_Missile_AcidGlob_Super_Simultaneous:1e8b03e89cf4f7a4d8dc823cf8132ad1";
            Check(damage.Length > 0 && shots.Length == (FireQuarantined(id) ? 0 : 1) && shots.All(shot => shot.SourceEntityId == owner.Hero.Id && shot.SpellPrefabGuid == prefab)
                && (FireQuarantined(id) || (id == "TLC_478" ? shots[0].TargetEntityIds.Count == 0 : shots.SelectMany(shot => shot.TargetEntityIds).SequenceEqual(damage))),
                id + " triggered cue keeps origin and targets, separate from combat");
            Check(!entries.Take(begin).Any(e => e.HasSubSpellStart), id + " combat attack receives no substitute cue");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS weapon-trigger damage distinct from combat, both seats and RNG");
    }

    private static void DeathrattleDamageVisuals()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string id in new[] { "CAP_803", "CATA_586", "CORE_WC_701", "EDR_110", "EDR_421", "FIR_778", "JAIL_007", "JAIL_398", "TLC_249", "TLC_401" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot); var owner = match.Game.CurrentPlayer;
            var attacker = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_200"), zone: owner.BoardZone);
            attacker[GameTag.ATK] = 20; attacker.BaseHealth = 30; attacker.IsExhausted = false;
            var ally = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_200"), zone: owner.BoardZone); ally.BaseHealth = 30;
            var dead = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId(id), zone: owner.Opponent.BoardZone); dead.BaseHealth = 1; dead.Damage = 0;
            var other = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone); other.BaseHealth = 30;
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var task = owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == attacker.Id && t.Target?.Id == dead.Id);
            var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
            foreach (var game in new[] { on, off })
                game.Process(game.CurrentPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == attacker.Id && t.Target?.Id == dead.Id));
            Check(on.Hash() == off.Hash(), id + " Deathrattle cue leaves state unchanged");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
            var left = random.GetValue(on); var right = random.GetValue(off); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), id + " Deathrattle cue leaves RNG unchanged");
            InvokeProcess(match, task); FlushFieldHistory(match); var entries = History(capture, cursor);
            int begin = entries.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == dead.Id && (int)e.PowerStart.Type == (int)BlockType.TRIGGER
                && e.PowerStart.TriggerKeyword == (int)GameTag.DEATHRATTLE);
            Check(begin >= 0, id + " actual Deathrattle trigger"); int depth = 1, end = begin + 1;
            for (; end < entries.Count && depth > 0; end++) { if (entries[end].HasPowerStart) depth++; if (entries[end].HasPowerEnd) depth--; }
            var body = entries.Skip(begin + 1).Take(end - begin - 2).ToArray();
            var shots = body.Where(e => e.HasSubSpellStart).Select(e => e.SubSpellStart).ToArray();
            var damage = body.Where(e => e.HasMetaData && (int)e.MetaData.Type == 1).Select(e => e.MetaData.Info.Single()).ToArray();
            bool group = id == "JAIL_398";
            string prefab = id switch {
                "CAP_803" => "ReuseFX_Holy_Missile_Smite_Medium_Super_Quick_FastActorTriggers:30caf938696e3224bb610c73b35469e6",
                "EDR_110" => "ReuseFX_Decay_Missile_AcidGlob_Super_Simultaneous:1e8b03e89cf4f7a4d8dc823cf8132ad1",
                "EDR_421" => "ReuseFX_Shadow_Missile_Blast_Small_Super_Simultaneous:e39b07044149c034d9b1d70182afe5b0",
                "TLC_401" => "ReuseFX_Frost_Missile_Snowball_Medium_Fast_Super_Simultaneous:4fecda861d7d78b4186c3a3c3091c1e5",
                "JAIL_398" => "ReuseFX_Fire_AE_Flood_BothSides_Super:1d77444aaeef20d4494e2ad82c551341",
                _ => "ReuseFX_Fire_Missile_Fireball_Medium_Super_Simultaneous:161d5760bd9de1e41baf4c5611e61eed" };
            bool quarantined = FireQuarantined(id);
            Check(damage.Length > 0 && shots.Length == (quarantined ? 0 : group ? 1 : damage.Length), id + " Deathrattle cue count");
            if (!quarantined) Check(shots.All(s => s.SourceEntityId == owner.Opponent.Hero.Id && s.SpellPrefabGuid == prefab)
                && (group ? shots[0].TargetEntityIds.Count == 0 : shots.Select(s => s.TargetEntityIds.Single()).SequenceEqual(damage)),
                id + " Deathrattle keeps public hero origin and target order");
            Check(body.Count(e => e.HasSubSpellStart) == body.Count(e => e.HasSubSpellEnd), id + " Deathrattle scopes balanced");
            if (!group && !quarantined) Check(match.Game.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistorySubSpellStart>()
                .Any(s => s.RequirePublicOrigin && s.Source == owner.Opponent.Hero.Id), id + " requires public Deathrattle source");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Deathrattle damage source, targets and public-origin guard, both seats");
    }

    private static void RottenAppleVisual()
    {
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: bot); var owner = match.Game.CurrentPlayer;
            owner.Hero.Damage = 20;
            var source = EngineEntity.FromCard(owner, Cards.FromId("EDR_482"), zone: owner.HandZone); source.Cost = 0;
            FlushFieldHistory(match); int castCursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match);
            var timer = match.Game.IdEntityDic.Values.OfType<Enchantment>().Single(e => e.Card.Id == "EDR_482e" && e.Creator?.Id == source.Id);
            Check(History(capture, castCursor).Any(e => e.HasShowEntity && e.ShowEntity.Entity == timer.Id && e.ShowEntity.Name == "EDR_482e"),
                "Rotten Apple timer is publicly shown before tick");
            int cursor = capture.Packets.Count;
            var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
            foreach (var game in new[] { on, off }) game.Process(game.CurrentPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.END_TURN));
            Check(on.Hash() == off.Hash(), "Rotten Apple timer cue leaves state unchanged");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
            var left = random.GetValue(on); var right = random.GetValue(off); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), "Rotten Apple timer cue leaves RNG unchanged");
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.END_TURN));
            FlushFieldHistory(match); var entries = History(capture, cursor);
            var shot = entries.Single(e => e.HasSubSpellStart).SubSpellStart;
            Check(shot.SourceEntityId == owner.Hero.Id && shot.TargetEntityIds.SequenceEqual(new[] { owner.Hero.Id })
                && shot.SpellPrefabGuid == "ReuseFX_Physical_Impact_Slam_Super:4d1b99bee3bc05d489e03086bf27e295"
                && owner.Hero.Damage == 11, "Rotten Apple tick keeps hero source, target and damage");
            Check(match.Game.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistorySubSpellStart>()
                .Any(s => s.RequirePublicOrigin && s.Source == owner.Hero.Id), "timer cue requires public causal origin");
            var known = (HashSet<int>)typeof(NativeMatch).GetField("revealedEntities", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match);
            var convert = typeof(NativeMatch).GetMethod("ToNativeHistory", BindingFlags.Instance | BindingFlags.NonPublic);
            var start = PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, timer.Id, "", -1, 0);
            var cue = new SabberStoneCore.Kettle.PowerHistorySubSpellStart { Source = owner.Hero.Id,
                SpellPrefabGuid = shot.SpellPrefabGuid, Targets = new List<int> { owner.Hero.Id }, RequirePublicOrigin = true };
            convert.Invoke(match, new object[] { start }); known.Remove(timer.Id);
            Check(convert.Invoke(match, new object[] { cue }) == null, "hidden timer suppresses entire visual");
            convert.Invoke(match, new object[] { new SabberStoneCore.Kettle.PowerHistorySubSpellEnd() });
            known.Add(timer.Id);
            Check(convert.Invoke(match, new object[] { cue }) != null, "public timer admits visual");
            convert.Invoke(match, new object[] { new SabberStoneCore.Kettle.PowerHistorySubSpellEnd() });
            convert.Invoke(match, new object[] { PowerHistoryBuilder.BlockEnd() });
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Rotten Apple creator attribution, public-origin privacy, both seats and RNG");
    }

    private static void GeneratedDamageVisuals()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string id in new[] { "ONY_018t2", "BE_086", "BE_554", "BE_071", "BE_153", "BE_153t", "BE_284", "BE_854", "BE_559", "WW_001t", "TLC_630t", "EDR_813b", "EDR_820b", "EDR_570A", "EDR_460t", "EDR_263a", "JAIL_941t", "CATA_464t", "CATA_489t", "CATA_489t2", "CORE_SW_088", "CAP_006", "CAP_101", "CATA_308", "CATA_498", "FIR_916", "FIR_939", "JAIL_307", "JAIL_445", "JAIL_515", "TLC_227", "TLC_620", "CATA_785", "EDR_460" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot, deck: "showcase-rogue-imbue"); var owner = match.Game.CurrentPlayer;
            var ally = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_200"), zone: owner.BoardZone);
            var enemy = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
            var enemy2 = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
            ally.BaseHealth = enemy.BaseHealth = enemy2.BaseHealth = 30;
            if (id == "EDR_813b") { typeof(SabberStoneCore.Model.Game).GetProperty("EnableCorpses").SetValue(match.Game, true); owner[GameTag.CORPSES] = 2; }
            if (id == "TLC_620") owner.Hero.Armor = 5;
            if (id == "CATA_785") owner.IsComboActive = true;
            var source = EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone); source.Cost = 0;
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var choices = owner.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id).ToArray();
            var task = choices.FirstOrDefault(t => t.Target?.Id == enemy.Id) ?? choices.FirstOrDefault(t => t.Target?.Id == ally.Id) ?? choices.First();
            var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
            foreach (var game in new[] { on, off })
                game.Process(game.CurrentPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id
                    && (t.Target?.Id ?? 0) == (task.Target?.Id ?? 0)));
            Check(on.Hash() == off.Hash(), id + " generated-route cue leaves state unchanged");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
            var left = random.GetValue(on); var right = random.GetValue(off); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), id + " generated-route cue leaves RNG unchanged");
            InvokeProcess(match, task); FlushFieldHistory(match); var entries = History(capture, cursor);
            string prefab = id switch {
                "ONY_018t2" or "BE_554" or "BE_153" or "BE_153t" or "CAP_101" or "FIR_916" or "FIR_939" or "TLC_227" => "ReuseFX_Fire_Missile_Fireball_Medium_Super_Simultaneous:161d5760bd9de1e41baf4c5611e61eed",
                "JAIL_307" => "ReuseFX_Fire_AE_Flood_BothSides_Super:1d77444aaeef20d4494e2ad82c551341",
                "EDR_820b" or "EDR_570A" => "ReuseFX_Shadow_Fire_AE_Shockwave_Small_BothSides_Super:1f77cfbb2d94e3f4982cf07e849fa699",
                "BE_086" or "BE_071" or "JAIL_941t" or "CATA_498" or "JAIL_515" or "CATA_785" => "ReuseFX_Shadow_Missile_Blast_Small_Super_Simultaneous:e39b07044149c034d9b1d70182afe5b0",
                // Issue #26: Demonic Assault uses its CardDef's fel missile, Gorishi Stinger a glaive, Medivh's Triumph one shockwave.
                "CORE_SW_088" => "ReuseFX_Fel_Fire_Missile_Small_Super_Quick:c09acf66646f50c4f9396a863c1e8b90",
                "TLC_630t" => "ReuseFX_Physical_DemonHunter_Missile_Glaive_Small_Super:636507efb4db26a4a9437ed0561e110e",
                "CATA_308" => "ReuseFX_Arcane_AE_Shockwave_Small_BothSides_Super:8e1bfdd78bbd7b3499685e8d9fd21417",
                "BE_284" or "CATA_464t" => "ReuseFX_Frost_Missile_Snowball_Medium_Fast_Super_Simultaneous:4fecda861d7d78b4186c3a3c3091c1e5",
                "EDR_263a" => "ReuseFX_Lightning_Missile_Bolt_Small_Super_Simultaneous:4e176ec954bdc6f4bb7494e521d35c8a",
                "EDR_813b" => "ReuseFX_Decay_Missile_AcidGlob_Super_Simultaneous:1e8b03e89cf4f7a4d8dc823cf8132ad1",
                "BE_559" or "WW_001t" or "CAP_006" or "TLC_620" => "ReuseFX_Physical_Missile_Generic_Super_Simultaneous:6ff0a03c3b353254d887e154c0f94e33",
                "JAIL_445" => "ReuseFX_Necrotic_Missile_Bone_Small_Super:d25c28f8f4b12964d8ab765bac643495",
                _ => "ReuseFX_Arcane_Missile_PurpleLightning_Super_Simultaneous:a40e6c32f4253314db432d7808a092de" };
            var shots = entries.Where(e => e.HasSubSpellStart && e.SubSpellStart.SpellPrefabGuid == prefab).Select(e => e.SubSpellStart).ToArray();
            var damage = entries.Where(e => e.HasMetaData && (int)e.MetaData.Type == 1).Select(e => e.MetaData.Info.Single()).ToArray();
            bool group = id is "JAIL_307" or "EDR_820b" or "EDR_570A" or "CATA_308";
            bool quarantined = FireQuarantined(id) && !group;
            Check(damage.Length > 0 && (quarantined ? shots.Length == 0 : group ? shots.Length == (id == "JAIL_307" ? 2 : 1) : shots.Length == damage.Length),
                id + " has one cue per hit or exact wave (quarantined fire: none)");
            if (group)
            {
                string perHit = id == "JAIL_307" ? "ReuseFX_Fire_Missile_Fireball_Medium_Super_Simultaneous:161d5760bd9de1e41baf4c5611e61eed"
                    : id == "CATA_308" ? "ReuseFX_Arcane_Missile_PurpleLightning_Super_Simultaneous:a40e6c32f4253314db432d7808a092de"
                    : "ReuseFX_Shadow_Missile_Blast_Small_Super_Simultaneous:e39b07044149c034d9b1d70182afe5b0";
                Check(!entries.Any(e => e.HasSubSpellStart && e.SubSpellStart.SpellPrefabGuid == perHit), id + " suppresses grouped per-hit cues");
            }
            Check(shots.All(s => s.SourceEntityId == (group ? source.Id : owner.Hero.Id)), id + " visual source correct");
            Check(quarantined || (group ? shots.All(s => s.TargetEntityIds.Count == 0) : shots.Select(s => s.TargetEntityIds.Single()).SequenceEqual(damage)), id + " target order correct");
            Check(entries.Count(e => e.HasSubSpellStart) == entries.Count(e => e.HasSubSpellEnd), id + " scopes balanced");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS standalone/generated damage routes, both seats, state and RNG");
    }

    // Issue #26 routes that aren't per-hit damage: Flame Imp's Fireblast at its own hero, Warchief Rafaam's armor impact,
    // Calamitous Rafaam's fire wave, Mend's heal, Anti-Magic Shell on each friendly minion, and the summons of Demonic
    // Assault, Nerubian Swarmguard and a Portal to Argus, then Raze through Deathwing's choice.
    private static void Issue26EffectRoutes()
    {
        const string fireblast = "ReuseFX_Missile_Fire_Fireball_Small_Super:b8e7bb6e0944d204297efdc937967085";
        const string armor = "ReuseFX_Physical_BuffImpact_Armor_Super_SourceHero:bf7e3e26888c9d94894b6d54a3ac6ba6";
        const string flood = "ReuseFX_Fire_AE_Flood_BothSides_Super:1d77444aaeef20d4494e2ad82c551341";
        const string heal = "ReuseFX_Holy_BuffImpact_Heal_Small_Super:98a6ddb5b34da2a419a16dcab9a2ef2c";
        const string shell = "ReuseFX_Necrotic_DeathKnight_BuffImpact_AntiMagicShell_Super_WithIdle:c08f3d01cb170ce4d900696be4d8f13c";
        const string runeCircle = "ReuseFX_Fel_Fire_OverrideSpawn_RuneCircle_Hop_Super_Random:b6770809cbf2a2b4a8a9e1bcbba84460";
        const string fromGround = "ReuseFX_Necrotic_DeathKnight_OverrideSpawn_FromGround_Super_Random:42069975192ed4a4fb2d84d4925509b7";
        const string portal = "TIME_020_Broxigar_Fab_OverrideSpawn_Super:3274770da35581d46b774f32fa26880d";
        foreach (bool bot in new[] { false, true })
        foreach (var (id, prefab) in new[] { ("CORE_EX1_319", fireblast), ("TIME_005t4", armor), ("TIME_005t6", flood), ("CATA_302", heal),
            ("RLK_048", shell), ("CORE_SW_088", runeCircle), ("CORE_RLK_062", fromGround), ("TIME_020t2", portal) })
        {
            var (match, capture) = AnimationMatch(botTurn: bot, deck: "showcase-rogue-imbue"); var owner = match.Game.CurrentPlayer;
            var allies = Enumerable.Range(0, 2).Select(_ => (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_200"), zone: owner.BoardZone)).ToArray();
            var enemy = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
            foreach (var m in allies.Append(enemy)) m.BaseHealth = 30;
            allies[0].Damage = 5;
            var source = EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone); source.Cost = 0;
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var choices = owner.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id).ToArray();
            Check(choices.Length > 0, id + " is playable");
            var task = (id == "CATA_302" ? choices.FirstOrDefault(t => t.Target?.Id == allies[0].Id) : choices.FirstOrDefault(t => t.Target?.Id == enemy.Id))
                ?? choices.First();
            var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
            foreach (var game in new[] { on, off })
                game.Process(game.CurrentPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id
                    && (t.Target?.Id ?? 0) == (task.Target?.Id ?? 0)));
            Check(on.Hash() == off.Hash(), id + " cue leaves game state unchanged");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
            var left = random.GetValue(on); var right = random.GetValue(off); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), id + " cue leaves RNG unchanged");
            InvokeProcess(match, task); FlushFieldHistory(match); var entries = History(capture, cursor);
            var shots = entries.Where(e => e.HasSubSpellStart && e.SubSpellStart.SpellPrefabGuid == prefab).Select(e => e.SubSpellStart).ToArray();
            var damage = entries.Where(e => e.HasMetaData && (int)e.MetaData.Type == 1).Select(e => e.MetaData.Info.Single()).ToArray();
            Check(shots.Length > 0, id + " plays its #26 effect");
            switch (id)
            {
                case "CORE_EX1_319":
                    Check(shots.Length == damage.Length && shots.All(s => s.SourceEntityId == source.Id && s.TargetEntityIds.SequenceEqual(new[] { owner.Hero.Id })),
                        id + " Fireblast goes from the Imp to its own hero, once per hit");
                    break;
                case "TIME_005t4":
                    Check(shots.Length == 1 && shots[0].SourceEntityId == owner.Hero.Id && shots[0].TargetEntityIds.SequenceEqual(new[] { owner.Hero.Id })
                        && owner.Hero.Armor > 0, id + " one armor impact on the hero, from the hero");
                    break;
                case "CATA_302":
                    Check(shots.Length == 1 && shots[0].SourceEntityId == owner.Hero.Id && shots[0].TargetEntityIds.SequenceEqual(new[] { allies[0].Id })
                        && allies[0].Damage < 5, id + " one heal impact on the damaged minion");
                    break;
                case "CORE_SW_088" or "CORE_RLK_062":
                    Check(shots.Length == 2 && shots.All(s => s.TargetEntityIds.Count == 0), id + " one summon effect per summoned minion");
                    break;
                case "TIME_020t2":
                    Check(shots.Length == 1 && shots[0].TargetEntityIds.Count == 0, id + " one portal summon effect");
                    break;
                case "TIME_005t6":
                    Check(shots.Length == 1 && shots[0].SourceEntityId == source.Id && shots[0].TargetEntityIds.Count == 0 && damage.Length > 0,
                        id + " one fire wave from Rafaam over both boards");
                    break;
                case "RLK_048":
                    Check(shots.Select(s => s.TargetEntityIds.Single()).OrderBy(x => x).SequenceEqual(allies.Select(a => a.Id).OrderBy(x => x)),
                        id + " one shell on each friendly minion");
                    break;
            }
            Check(entries.Count(e => e.HasSubSpellStart) == entries.Count(e => e.HasSubSpellEnd), id + " scopes balanced");
            capture.SerializeAll();
        }
        foreach (bool bot in new[] { false, true })
        {
            // Raze: Deathwing, Worldbreaker's half-board effect, one pulse from Deathwing over the enemy board.
            const string halfBoard = "CATAFX_Deathwing_AE_HalfBoard_Super:6f6e7e32f2a0f2a4d906ef4aa79b185e";
            var (match, capture) = AnimationMatch(botTurn: bot); var owner = match.Game.CurrentPlayer;
            var enemies = Enumerable.Range(0, 2).Select(_ => (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone)).ToArray();
            foreach (var m in enemies) m.BaseHealth = 30;
            var source = EngineEntity.FromCard(owner, Cards.FromId("CATA_190h"), zone: owner.HandZone); source.Cost = 0;
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match);
            int raze = owner.Choice.Choices.First(c => match.Game.IdEntityDic[c].Card.Id == "CATA_190t12");
            InvokeProcess(match, ChooseTask.Pick(owner, raze)); FlushFieldHistory(match);
            var entries = History(capture, cursor);
            var shots = entries.Where(e => e.HasSubSpellStart && e.SubSpellStart.SpellPrefabGuid == halfBoard).Select(e => e.SubSpellStart).ToArray();
            var damage = entries.Where(e => e.HasMetaData && (int)e.MetaData.Type == 1).Select(e => e.MetaData.Info.Single()).ToArray();
            Check(shots.Length == 1 && shots[0].SourceEntityId == source.Id && shots[0].TargetEntityIds.Count == 0
                && enemies.All(m => damage.Contains(m.Id)), "CATA_190h Raze plays one half-board pulse from Deathwing over the enemy minions");
            Check(entries.Count(e => e.HasSubSpellStart) == entries.Count(e => e.HasSubSpellEnd), "CATA_190h Raze scopes balanced");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS issue #26 effect routes: Fireblast, armor, fire wave, heal, shell, summons and Raze; both seats, state and RNG");
    }

    private static void BattlecryDamageVisuals()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string id in new[] { "CATA_304", "CATA_552", "DINO_413", "EDR_459", "EDR_468", "TIME_004", "TIME_858", "TLC_600", "TLC_606", "TLC_452t13", "TLC_452t22", "TLC_452t26", "TLC_452t27" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot); var owner = match.Game.CurrentPlayer;
            foreach (var player in new[] { owner, owner.Opponent })
                for (int i = 0; i < 2; i++)
                {
                    var minion = (Minion)EngineEntity.FromCard(player, Cards.FromId("CS2_200"), zone: player.BoardZone);
                    minion.BaseHealth = 30;
                }
            var source = EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone); source.Cost = 0;
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var choices = owner.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id).ToArray();
            var task = choices.FirstOrDefault(t => t.Target?.Id == owner.Opponent.BoardZone.First().Id) ?? choices.First();
            var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
            foreach (var game in new[] { on, off })
                game.Process(game.CurrentPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id
                    && (t.Target?.Id ?? 0) == (task.Target?.Id ?? 0)));
            Check(on.Hash() == off.Hash(), id + " Battlecry cue leaves game state unchanged");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
            var left = random.GetValue(on); var right = random.GetValue(off); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), id + " Battlecry cue leaves RNG unchanged");
            InvokeProcess(match, task); FlushFieldHistory(match); var entries = History(capture, cursor);
            string prefab = id switch {
                "CATA_304" => "ReuseFX_Physical_Impact_Slam_Super:4d1b99bee3bc05d489e03086bf27e295",
                "DINO_413" => "ReuseFX_Frost_Missile_Snowball_Medium_Fast_Super_Simultaneous:4fecda861d7d78b4186c3a3c3091c1e5",
                "EDR_459" => "ReuseFX_Shadow_Missile_Blast_Small_Super_Simultaneous:e39b07044149c034d9b1d70182afe5b0",
                "TIME_004" or "TIME_858" or "TLC_452t27" => "ReuseFX_Arcane_Missile_PurpleLightning_Super_Simultaneous:a40e6c32f4253314db432d7808a092de",
                _ => "ReuseFX_Physical_Missile_Generic_Super_Simultaneous:6ff0a03c3b353254d887e154c0f94e33" };
            var shots = entries.Where(e => e.HasSubSpellStart && e.SubSpellStart.SpellPrefabGuid == prefab).Select(e => e.SubSpellStart).ToArray();
            var damage = entries.Where(e => e.HasMetaData && (int)e.MetaData.Type == 1).Select(e => e.MetaData.Info.Single()).ToArray();
            Check(shots.Length > 0 && shots.Length == damage.Length && shots.Select(s => s.TargetEntityIds.Single()).SequenceEqual(damage)
                && shots.All(s => s.SourceEntityId == source.Id), id + " Battlecry cue preserves minion source and target order");
            Check(entries.Count(e => e.HasSubSpellStart) == entries.Count(e => e.HasSubSpellEnd), id + " Battlecry scopes balanced");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS normal-actor Battlecry damage cues, both seats, state and RNG");
    }

    private static void AdditionalDamageRoutes()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string id in new[] { "CATA_533", "TLC_901", "JAIL_805", "CATA_475", "CATA_999", "TIME_441", "CATA_489", "EDR_476", "MEND_500", "TIME_810", "TIME_810t1", "TIME_810t2", "CAP_001", "CATA_156", "CATA_581", "CORE_CATA_007", "DINO_132" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot, deck: "showcase-rogue-imbue"); var owner = match.Game.CurrentPlayer;
            var ally = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_200"), zone: owner.BoardZone);
            var enemy = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
            var enemy2 = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
            ally.BaseHealth = enemy.BaseHealth = enemy2.BaseHealth = 30;
            if (id == "CAP_001") ally.HasStealth = true;
            bool location = id.StartsWith("TIME_810", StringComparison.Ordinal);
            bool trigger = id is "CATA_475" or "CATA_999" or "DINO_132";
            var source = id == "CATA_489" ? SabberStoneCore.Actions.ShatterRules.GiveCombined(owner, id, owner.Hero)
                : EngineEntity.FromCard(owner, Cards.FromId(id), zone: location || trigger ? owner.BoardZone : owner.HandZone);
            source.Cost = 0; FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var choices = owner.Options().Where(t => trigger ? t.PlayerTaskType == PlayerTaskType.END_TURN
                : t.PlayerTaskType == (location ? PlayerTaskType.ACTIVATE_LOCATION : PlayerTaskType.PLAY_CARD) && t.Source?.Id == source.Id).ToArray();
            var task = id == "CAP_001" ? choices.First(t => t.Target?.Id == ally.Id)
                : choices.FirstOrDefault(t => t.Target?.Id == enemy.Id) ?? choices.First();
            var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
            foreach (var game in new[] { on, off })
                game.Process(game.CurrentPlayer.Options().First(t => trigger ? t.PlayerTaskType == PlayerTaskType.END_TURN
                    : t.PlayerTaskType == (location ? PlayerTaskType.ACTIVATE_LOCATION : PlayerTaskType.PLAY_CARD) && t.Source?.Id == source.Id
                        && (t.Target?.Id ?? 0) == (task.Target?.Id ?? 0)));
            Check(on.Hash() == off.Hash(), id + " route presentation leaves game state unchanged");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
            var left = random.GetValue(on); var right = random.GetValue(off); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), id + " route presentation leaves RNG unchanged");
            InvokeProcess(match, task); FlushFieldHistory(match); var entries = History(capture, cursor);
            string prefab = id switch {
                "CATA_533" => "ReuseFX_Frost_Missile_Snowball_Medium_Fast_Super_Simultaneous:4fecda861d7d78b4186c3a3c3091c1e5",
                "TLC_901" => "ReuseFX_Decay_Missile_AcidGlob_Super_Simultaneous:1e8b03e89cf4f7a4d8dc823cf8132ad1",
                "JAIL_805" or "CATA_475" or "CATA_999" => "ReuseFX_Lightning_Missile_Bolt_Small_Super_Simultaneous:4e176ec954bdc6f4bb7494e521d35c8a",
                "CAP_001" or "CATA_156" or "CORE_CATA_007" or "DINO_132" => "ReuseFX_Shadow_Missile_Blast_Small_Super_Simultaneous:e39b07044149c034d9b1d70182afe5b0",
                "CATA_581" => "ReuseFX_Shadow_Fire_AE_Shockwave_Small_BothSides_Super:1f77cfbb2d94e3f4982cf07e849fa699",
                _ => "ReuseFX_Arcane_Missile_PurpleLightning_Super_Simultaneous:a40e6c32f4253314db432d7808a092de" };
            var shots = entries.Where(e => e.HasSubSpellStart && e.SubSpellStart.SpellPrefabGuid == prefab).Select(e => e.SubSpellStart).ToArray();
            var damage = entries.Where(e => e.HasMetaData && (int)e.MetaData.Type == 1).Select(e => e.MetaData.Info.Single()).ToArray();
            Check(damage.Length > 0 && shots.Length == (id == "CATA_581" ? 1 : damage.Length), id + " has one cue per hit or bounded area pulse");
            Check(shots.All(s => s.SourceEntityId == (location || trigger || id == "CATA_581" ? source.Id : owner.Hero.Id)), id + " correct public visual source");
            Check(id == "CATA_581" ? shots[0].TargetEntityIds.Count == 0 : shots.Select(s => s.TargetEntityIds.Single()).SequenceEqual(damage), id + " correct ordered targets");
            Check(entries.Count(e => e.HasSubSpellStart) == entries.Count(e => e.HasSubSpellEnd), id + " balanced scopes");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS additional damage routes, both seats, source, target, state and RNG");
    }

    private static void ChooseDamageVisuals()
    {
        foreach (bool bot in new[] { false, true })
        foreach (int mode in new[] { 0, 1, 2 })
        foreach (string id in new[] { "EDR_263", "EDR_570", "EDR_813", "EDR_820", "CORE_ONY_018" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot); var owner = match.Game.CurrentPlayer;
            var ally = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_200"), zone: owner.BoardZone);
            var enemy = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
            ally.BaseHealth = enemy.BaseHealth = 30; ally.Damage = 1;
            if (mode == 2) EngineEntity.FromCard(owner, Cards.FromId("OG_044"), zone: owner.BoardZone);
            if (id == "EDR_813") { typeof(SabberStoneCore.Model.Game).GetProperty("EnableCorpses").SetValue(match.Game, true); owner[GameTag.CORPSES] = 2; }
            var source = EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone); source.Cost = 0;
            string child = id switch { "EDR_263" => "EDR_263a", "EDR_570" => "EDR_570A", "EDR_813" => "EDR_813b", "CORE_ONY_018" => "ONY_018t2", _ => "EDR_820b" };
            int damageBranch = Array.FindIndex(source.ChooseOnePlayables, c => c.Card.Id == child) + 1;
            int branch = mode == 2 ? 0 : mode == 0 ? damageBranch : 3 - damageBranch;
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var choices = owner.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id && (mode == 2 || t.ChooseOne == branch)).ToArray();
            var task = choices.FirstOrDefault(t => t.Target?.Id == enemy.Id) ?? choices.FirstOrDefault(t => t.Target?.Id == ally.Id) ?? choices.First();
            var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
            foreach (var game in new[] { on, off })
                game.Process(game.CurrentPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id
                    && (mode == 2 || t.ChooseOne == branch) && (t.Target?.Id ?? 0) == (task.Target?.Id ?? 0)));
            Check(on.Hash() == off.Hash(), id + " Choose presentation leaves game state unchanged");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
            var left = random.GetValue(on); var right = random.GetValue(off); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), id + " Choose presentation leaves RNG unchanged");
            InvokeProcess(match, task); FlushFieldHistory(match); var entries = History(capture, cursor);
            var damage = entries.Where(e => e.HasMetaData && (int)e.MetaData.Type == 1).ToArray();
            var shots = entries.Where(e => e.HasSubSpellStart).Select(e => e.SubSpellStart).ToArray();
            Check(mode == 1 ? damage.Length == 0 && shots.Length == 0 : damage.Length > 0 && shots.Length == (FireQuarantined(id) ? 0 : 1),
                id + " only damage branch emits one projectile or area pulse (quarantined fire: none)");
            if (mode != 1 && !FireQuarantined(id))
            {
                bool area = id is "EDR_570" or "EDR_820";
                string prefab = id switch {
                    "EDR_263" => "ReuseFX_Lightning_Missile_Bolt_Small_Super_Simultaneous:4e176ec954bdc6f4bb7494e521d35c8a",
                    "EDR_813" => "ReuseFX_Decay_Missile_AcidGlob_Super_Simultaneous:1e8b03e89cf4f7a4d8dc823cf8132ad1",
                    "CORE_ONY_018" => "ReuseFX_Fire_Missile_Fireball_Medium_Super_Simultaneous:161d5760bd9de1e41baf4c5611e61eed",
                    _ => "ReuseFX_Shadow_Fire_AE_Shockwave_Small_BothSides_Super:1f77cfbb2d94e3f4982cf07e849fa699" };
                Check(shots[0].SourceEntityId == (area ? source.Id : owner.Hero.Id)
                    && shots[0].SpellPrefabGuid == prefab
                    && (area ? shots[0].TargetEntityIds.Count == 0 : shots[0].TargetEntityIds.SequenceEqual(damage[0].MetaData.Info)), id + " correct Choose damage source, targets and prefab");
            }
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Choose damage/non-damage/both branches and both seats");
    }

    private static void SylvanasFullBoardVisual()
    {
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: bot); var owner = match.Game.CurrentPlayer;
            for (int i = 0; i < 7; i++)
            {
                var minion = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
                minion.BaseHealth = 30;
            }
            var victim = owner.Opponent.BoardZone.First();
            var prior = EngineEntity.FromCard(owner, Cards.FromId("CATA_557"), zone: owner.HandZone); prior.Cost = 0;
            FlushFieldHistory(match);
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == prior.Id && t.Target?.Id == victim.Id));
            var source = EngineEntity.FromCard(owner, Cards.FromId("CATA_557"), zone: owner.HandZone); source.Cost = 0;
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match); var entries = History(capture, cursor);
            var shots = entries.Where(e => e.HasSubSpellStart).Select(e => e.SubSpellStart).ToArray();
            var damage = entries.Where(e => e.HasMetaData && (int)e.MetaData.Type == 1).Select(e => e.MetaData.Info.Single()).ToArray();
            Check(shots.Length == 8 && shots.Select(s => s.TargetEntityIds.Single()).SequenceEqual(damage)
                && shots.All(s => s.SourceEntityId == owner.Hero.Id && s.SpellPrefabGuid ==
                    "ReuseFX_Shadow_Missile_Blast_Small_Super_Simultaneous:e39b07044149c034d9b1d70182afe5b0"),
                "powered Sylvanas targets only eight enemies; native pacing remains pending");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Sylvanas full-board enemy target order (native pacing pending)");
    }

    private static void DamageGroupScopeBoundaries()
    {
        var (match, _) = AnimationMatch(); var game = match.Game; var owner = game.CurrentPlayer;
        var outer = EngineEntity.FromCard(owner, Cards.FromId("CATA_581"), zone: owner.HandZone);
        var child = EngineEntity.FromCard(owner, Cards.FromId("TIME_001"), zone: owner.HandZone);
        var history = game.PowerHistory;
        var phase = Enum.Parse(typeof(SabberStoneCore.Model.Game).Assembly.GetType("SabberStoneCore.Presentation.VisualPhase"), "DamageGroup");
        var visual = typeof(PowerHistory).GetMethod("Visual", BindingFlags.Instance | BindingFlags.NonPublic);
        history.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, outer.Id, "", -1, 0)); int cursor = history.Full.Count;
        try
        {
            using (var scope = (IDisposable)visual.Invoke(history, new object[] { game, phase, outer, null }))
            {
                owner.Opponent.Hero.TakeDamage(outer, 1);
                history.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, child.Id, "", -1, 0));
                owner.Opponent.Hero.TakeDamage(child, 1);
                history.Add(PowerHistoryBuilder.BlockEnd());
                scope.Dispose();
                throw new InvalidOperationException("group fixture unwind");
            }
        }
        catch (InvalidOperationException error) when (error.Message == "group fixture unwind") { }
        owner.Opponent.Hero.TakeDamage(outer, 1);
        history.Add(PowerHistoryBuilder.BlockEnd());
        var starts = history.Full.Skip(cursor).OfType<SabberStoneCore.Kettle.PowerHistorySubSpellStart>().ToArray();
        Check(starts.Length == 3 && starts[0].Source == outer.Id && starts[1].Source == owner.Hero.Id
            && starts[2].Source == owner.Hero.Id, "group suppresses only its own hit; nested and later hits retain their own cues");
        Check(history.Full.Skip(cursor).Count(e => e is SabberStoneCore.Kettle.PowerHistorySubSpellEnd) == 3,
            "group unwind and double dispose preserve balanced scopes");
        history.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, outer.Id, "", -1, 0)); cursor = history.Full.Count;
        using ((IDisposable)visual.Invoke(history, new object[] { game, phase, outer, null }))
        {
            owner.Opponent.Hero.TakeDamage(outer, 1);
            history.Add(new SabberStoneCore.Kettle.PowerHistoryResetGame());
        }
        owner.Opponent.Hero.TakeDamage(outer, 1);
        history.Add(PowerHistoryBuilder.BlockEnd());
        starts = history.Full.Skip(cursor).OfType<SabberStoneCore.Kettle.PowerHistorySubSpellStart>().ToArray();
        Check(starts.Length == 1 && starts[0].Source == owner.Hero.Id, "reset drops abandoned group and restores later hit cue");
        Console.WriteLine("PASS damage-group nested source and unwind boundaries");
    }

    private static void VariableAreaVisuals()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string id in new[] { "CATA_582" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot); var owner = match.Game.CurrentPlayer;
            foreach (var player in new[] { owner, owner.Opponent })
                for (int i = 0; i < 7; i++)
                {
                    var minion = (Minion)EngineEntity.FromCard(player, Cards.FromId("CS2_200"), zone: player.BoardZone);
                    minion.BaseHealth = 30;
                }
            var source = EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone); source.Cost = 0;
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
            foreach (var game in new[] { on, off })
                game.Process(game.CurrentPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            Check(on.Hash() == off.Hash(), id + " area presentation leaves state unchanged");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
            var left = random.GetValue(on); var right = random.GetValue(off); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), id + " area presentation leaves RNG unchanged");
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match); var entries = History(capture, cursor);
            int expectedHits = 14;
            Check(entries.Count(e => e.HasMetaData && (int)e.MetaData.Type == 1) == expectedHits, id + " full-board damage targets preserved");
            var starts = entries.Where(e => e.HasSubSpellStart).Select(e => e.SubSpellStart).ToArray();
            string prefab = "ReuseFX_Fire_AE_Flood_BothSides_Super:1d77444aaeef20d4494e2ad82c551341";
            Check(starts.Length == 1 && entries.Count(e => e.HasSubSpellEnd) == 1 && starts[0].SourceEntityId == source.Id
                && starts[0].TargetEntityIds.Count == 0 && starts[0].SpellPrefabGuid == prefab, id + " full-board wave uses one area pulse");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS variable single/area spells bound full-board visual work");
    }

    private static void DamageWaveVisuals()
    {
        foreach (bool bot in new[] { false, true })
        foreach (bool immune in new[] { false, true })
        foreach (bool full in new[] { false, true })
        foreach (string id in new[] { "TLC_601", "CATA_526", "CATA_491" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot); var owner = match.Game.CurrentPlayer;
            var first = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_200"), zone: owner.BoardZone);
            var second = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
            var third = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
            first.BaseHealth = 1; second.BaseHealth = third.BaseHealth = 30;
            first.IsImmune = second.IsImmune = third.IsImmune = immune; owner.Hero.Armor = 5;
            if (full)
                foreach (var player in new[] { owner, owner.Opponent })
                    while (player.BoardZone.Count < 7)
                    {
                        var extra = (Minion)EngineEntity.FromCard(player, Cards.FromId("CS2_200"), zone: player.BoardZone);
                        extra.BaseHealth = 30; extra.IsImmune = immune;
                    }
            var source = EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone); source.Cost = 0;
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
            foreach (var game in new[] { on, off })
                game.Process(game.CurrentPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            Check(on.Hash() == off.Hash(), id + " wave cue leaves gameplay unchanged");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
            var left = random.GetValue(on); var right = random.GetValue(off); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), id + " wave cue leaves RNG unchanged");
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match); var entries = History(capture, cursor);
            var starts = entries.Select((entry, index) => (entry, index)).Where(x => x.entry.HasSubSpellStart).ToArray();
            int waves = immune ? 0 : id == "TLC_601" ? 5 : id == "CATA_491" ? 3 : 1;
            Check(starts.Length == waves && entries.Count(e => e.HasSubSpellEnd) == waves, id + " emits at most one pulse per damaging wave");
            string prefab = id == "CATA_491" ? "ReuseFX_Shadow_Fire_AE_Shockwave_Small_BothSides_Super:1f77cfbb2d94e3f4982cf07e849fa699"
                : "ReuseFX_Physical_DemonHunter_AE_Whirlwind_BothSides_Small_Super_Quick:bc23f3d70f7ba4a4aa4ec2f8b4163e46";
            foreach (var shot in starts)
                Check(shot.entry.SubSpellStart.SourceEntityId == source.Id && shot.entry.SubSpellStart.TargetEntityIds.Count == 0
                    && shot.entry.SubSpellStart.SpellPrefabGuid == prefab && entries[shot.index + 1].HasSubSpellEnd, id + " emits a source-only pulse");
            if (!immune && waves > 1)
                Check(entries.Skip(starts[0].index + 1).Take(starts[1].index - starts[0].index - 1)
                    .Any(e => e.HasPowerStart && (int)e.PowerStart.Type == (int)BlockType.DEATHS), id + " retains death phase between waves");
            Check(immune || second.Damage > 0, id + " damage remains effective");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS bounded damage-wave pulses, death boundaries, prevention, both seats and RNG");
    }

    private static void FireSpellVisuals()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string id in new[] { "CATA_585", "DINO_406", "END_025", "EDR_255", "JAIL_801", "CATA_582", "CATA_303", "JAIL_891", "JAIL_892", "CATA_557", "TIME_218", "EDR_262", "TIME_750", "TIME_600", "EDR_941", "CATA_978", "EDR_814" })
        foreach (bool excess in id == "CATA_585" ? new[] { false, true } : new[] { false })
        {
            var (match, capture) = AnimationMatch(botTurn: bot); var owner = match.Game.CurrentPlayer;
            var enemy = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
            enemy.BaseHealth = excess ? 6 : 30; enemy.Damage = 1;
            var source = EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone); source.Cost = 0;
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var choice = owner.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id).ToArray();
            var task = choice.FirstOrDefault(t => t.Target?.Id == enemy.Id) ?? choice.FirstOrDefault();
            Check(task != null, id + " fixture has legal play");
            var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
            foreach (var game in new[] { on, off })
                game.Process(game.CurrentPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id && (t.Target?.Id ?? 0) == (task.Target?.Id ?? 0)));
            Check(on.Hash() == off.Hash(), id + " presentation leaves gameplay unchanged");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
            var left = random.GetValue(on); var right = random.GetValue(off); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), id + " presentation leaves RNG unchanged");
            InvokeProcess(match, task); FlushFieldHistory(match); var entries = History(capture, cursor);
            string prefab = id switch {
                "JAIL_891" or "JAIL_892" or "CATA_557" => "ReuseFX_Shadow_Missile_Blast_Small_Super_Simultaneous:e39b07044149c034d9b1d70182afe5b0",
                "TIME_218" or "EDR_262" => "ReuseFX_Lightning_Missile_Bolt_Small_Super_Simultaneous:4e176ec954bdc6f4bb7494e521d35c8a",
                "TIME_750" or "TIME_600" => "ReuseFX_Generic_Missile_Arrow_Super_Simultaneous:bbd7f9a5ebabd8e45b0a22cb4446f1d9",
                "EDR_941" or "CATA_978" => "ReuseFX_Arcane_Missile_PurpleLightning_Super_Simultaneous:a40e6c32f4253314db432d7808a092de",
                "EDR_814" => "ReuseFX_Decay_Missile_AcidGlob_Super_Simultaneous:1e8b03e89cf4f7a4d8dc823cf8132ad1",
                _ => "ReuseFX_Fire_Missile_Fireball_Medium_Super_Simultaneous:161d5760bd9de1e41baf4c5611e61eed" };
            var shots = entries.Where(e => e.HasSubSpellStart && e.SubSpellStart.SpellPrefabGuid == prefab).Select(e => e.SubSpellStart).ToArray();
            var damage = entries.Where(e => e.HasMetaData && (int)e.MetaData.Type == 1).Select(e => e.MetaData.Info.Single()).ToArray();
            Check(damage.Length > 0 && shots.Length == (FireQuarantined(id) ? 0 : damage.Length) && entries.Count(e => e.HasSubSpellEnd) == entries.Count(e => e.HasSubSpellStart),
                id + " has one cue per resolved hit and balanced scopes");
            if (!FireQuarantined(id)) Check(shots.Select(s => s.TargetEntityIds.Single()).SequenceEqual(damage) && shots.All(s => s.SourceEntityId == owner.Hero.Id
                && s.SpellPrefabGuid == prefab), id + " preserves cue target order");
            if (id == "CATA_585")
            {
                int start = entries.FindIndex(e => e.HasSubSpellStart), end = entries.FindIndex(e => e.HasSubSpellEnd);
                Check(start >= 0 && end > start && entries.Skip(start + 1).Take(end - start - 1)
                    .Count(e => e.HasMetaData && (int)e.MetaData.Type == 1) == 1, "Torch damage stays inside public hero missile scope");
                var copies = owner.HandZone.Where(c => c.Card.Id == id).ToArray();
                Check(excess ? copies.Length == 1 && copies[0][GameTag.TAG_SCRIPT_DATA_NUM_1] == 3
                    && enemy.Zone.Type == SabberStoneCore.Enums.Zone.GRAVEYARD : copies.Length == 0 && enemy.Damage == 9,
                    "Torch excess copy payload and non-excess damage unchanged");
                Check(shots.All(s => s.SourceEntityId != source.Id && !s.TargetEntityIds.Any(t => copies.Any(c => c.Id == t))),
                    "Torch missile never references source spell or private hand copy");
            }
            capture.SerializeAll();
        }
        Console.WriteLine("PASS fire and shadow spell cues, targets, state and RNG for both seats");
    }

    private static void DrinkBloodVisual()
    {
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: bot); var owner = match.Game.CurrentPlayer;
            owner.Hero.Damage = 8;
            var victim = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
            victim.BaseHealth = 30;
            var spell = EngineEntity.FromCard(owner, Cards.FromId("JAIL_441"), zone: owner.HandZone);
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
            foreach (var game in new[] { on, off })
                game.Process(game.CurrentPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id && t.Target?.Id == victim.Id));
            Check(on.Hash() == off.Hash(), "Drink Blood presentation leaves outcome unchanged");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
            var left = random.GetValue(on); var right = random.GetValue(off); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), "Drink Blood presentation leaves RNG unchanged");
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id && t.Target?.Id == victim.Id));
            FlushFieldHistory(match); var entries = History(capture, cursor);
            int start = entries.FindIndex(e => e.HasSubSpellStart), end = entries.FindIndex(e => e.HasSubSpellEnd);
            Check(start >= 0 && end > start && entries.Count(e => e.HasSubSpellStart) == 1, "Drink Blood has one resolved damage cue");
            var shot = entries[start].SubSpellStart;
            Check(shot.SourceEntityId == owner.Hero.Id && shot.TargetEntityIds.SequenceEqual(new[] { victim.Id }) && shot.SpellPrefabGuid ==
                "ReuseFX_Shadow_Missile_Blast_Small_Super_Simultaneous:e39b07044149c034d9b1d70182afe5b0", "Drink Blood uses shadow cue and resolved target");
            Check(entries.Skip(end + 1).Any(e => e.HasPowerStart && (int)e.PowerStart.TriggerKeyword == (int)GameTag.LIFESTEAL),
                "Drink Blood lifesteal remains outside damage scope");
            Check(victim.Damage == 3 && owner.Hero.Damage == 5, "Drink Blood damage and lifesteal unchanged");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Drink Blood cue and independent lifesteal, both seats");
    }

    private static void SleetStormVisual()
    {
        foreach (bool bot in new[] { false, true })
        foreach (bool survivor in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: bot); var owner = match.Game.CurrentPlayer;
            var first = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
            var second = survivor ? (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone) : null;
            first.BaseHealth = 2; if (second != null) second.BaseHealth = 30;
            var spell = EngineEntity.FromCard(owner, Cards.FromId("CATA_485"), zone: owner.HandZone);
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
            foreach (var game in new[] { on, off })
                game.Process(game.CurrentPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id && t.Target?.Id == first.Id));
            Check(on.Hash() == off.Hash(), "Sleet presentation leaves outcome unchanged");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
            var left = random.GetValue(on); var right = random.GetValue(off); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), "Sleet presentation leaves RNG unchanged");
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id && t.Target?.Id == first.Id));
            FlushFieldHistory(match); var entries = History(capture, cursor);
            var starts = entries.Select((entry, index) => (entry, index)).Where(x => x.entry.HasSubSpellStart).ToArray();
            Check(starts.Length == (survivor ? 2 : 1) && starts[0].entry.SubSpellStart.TargetEntityIds.SequenceEqual(new[] { first.Id }),
                "Sleet emits a cue only for each resolved target");
            Check(starts.All(x => x.entry.SubSpellStart.SourceEntityId == owner.Hero.Id && x.entry.SubSpellStart.SpellPrefabGuid ==
                "ReuseFX_Frost_Missile_Snowball_Medium_Fast_Super_Simultaneous:4fecda861d7d78b4186c3a3c3091c1e5"), "Sleet uses frost cue from hero");
            if (survivor)
            {
                Check(starts[1].entry.SubSpellStart.TargetEntityIds.SequenceEqual(new[] { second.Id }), "Sleet second cue targets survivor");
                Check(entries.Skip(starts[0].index + 1).Take(starts[1].index - starts[0].index - 1)
                    .Any(e => e.HasPowerStart && (int)e.PowerStart.Type == (int)BlockType.DEATHS), "Sleet death phase remains between cues");
            }
            Check(first.Zone.Type == SabberStoneCore.Enums.Zone.GRAVEYARD && (second == null || second.Damage == 1), "Sleet gameplay unchanged");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Sleet Storm ordered hits, death boundary, both seats and RNG");
    }

    private static void VolcanoAndCannoneerVisuals()
    {
        foreach (bool bot in new[] { false, true })
        foreach (bool volcano in new[] { false, true })
        foreach (bool fire in volcano ? new[] { false, true } : new[] { false })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var owner = match.Game.CurrentPlayer;
            if (fire)
            {
                var victim = (Character)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
                victim.BaseHealth = 30; victim.Damage = 1;
                var torch = EngineEntity.FromCard(owner, Cards.FromId("CATA_585"), zone: owner.HandZone);
                InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == torch.Id && t.Target?.Id == victim.Id));
                owner.Opponent.SetasideZone.Add(owner.Opponent.BoardZone.Remove((Minion)victim));
            }
            var source = EngineEntity.FromCard(owner, Cards.FromId(volcano ? "CATA_584" : "CAP_107t"), zone: owner.BoardZone);
            if (volcano) source[GameTag.EXHAUSTED] = 0;
            else owner.ExtraEndTurnEffect = true;
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            var task = volcano
                ? owner.Options().First(t => t is ActivateLocationTask && t.Source?.Id == source.Id)
                : owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.END_TURN);
            var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
            foreach (var game in new[] { on, off })
                game.Process(volcano ? game.CurrentPlayer.Options().First(t => t is ActivateLocationTask && t.Source?.Id == source.Id)
                    : game.CurrentPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.END_TURN));
            Check(on.Hash() == off.Hash(), "shot presentation leaves gameplay state unchanged");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
            var left = random.GetValue(on); var right = random.GetValue(off);
            var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), "shot presentation leaves RNG unchanged");
            InvokeProcess(match, task);
            FlushFieldHistory(match);
            var entries = History(capture, cursor);
            var shots = entries.Where(e => e.HasSubSpellStart).Select(e => e.SubSpellStart).ToArray();
            int hits = volcano ? (fire ? 6 : 3) : 2, count = hits; // CATA_584 re-admitted 2026-11-04: one missile per resolved hit
            Check(shots.Length == count && entries.Count(e => e.HasSubSpellEnd) == count, "each resolved shot has balanced scope");
            Check(shots.All(s => s.SourceEntityId == source.Id && s.TargetEntityIds.SequenceEqual(new[] { owner.Opponent.Hero.Id })),
                "shot origin and resolved target preserved for both seats");
            Check(shots.All(s => s.SpellPrefabGuid == (volcano
                ? "ReuseFX_Fire_Missile_Fireball_Medium_Super_Simultaneous:161d5760bd9de1e41baf4c5611e61eed"
                : "ReuseFX_Generic_Missile_Cannon_Super_Quickest:ed9fdef57cbdcbb44bbfd9f06c1c8d24")), "shot uses intended prefab");
            Check(owner.Opponent.Hero.Damage == hits, "presentation leaves damage unchanged");
            capture.SerializeAll();
        }
        CannoneerHandCannonVisual();
        CannoneerPreventionVisual();
        Console.WriteLine("PASS Volcano and doubled Cannoneer shots, both seats");
    }

    private static void CannoneerPreventionVisual()
    {
        var (match, _) = AnimationMatch(); var game = match.Game; var owner = game.CurrentPlayer;
        var cannon = EngineEntity.FromCard(owner, Cards.FromId("CAP_107t"), zone: owner.BoardZone);
        var victim = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
        victim.HasDivineShield = true;
        var history = game.PowerHistory; int start = history.Full.Count;
        history.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, cannon.Id, "", -1, 0));
        victim.TakeDamage(cannon, 1);
        victim.IsImmune = true;
        victim.TakeDamage(cannon, 1);
        history.Add(PowerHistoryBuilder.BlockEnd());
        Check(!victim.HasDivineShield && victim.Damage == 0, "shield absorbed shot and immunity prevented next hit");
        Check(history.Full.Skip(start).Count(e => e is SabberStoneCore.Kettle.PowerHistorySubSpellStart) == 1,
            "shield impact has a cue, immune attempt has none");
    }

    private static void CannoneerHandCannonVisual()
    {
        var (match, capture) = AnimationMatch(); var owner = match.Game.CurrentPlayer;
        var cannon = EngineEntity.FromCard(owner, Cards.FromId("CAP_107t"), zone: owner.BoardZone);
        var weapon = EngineEntity.FromCard(owner, Cards.FromId("CAP_103"), zone: owner.HandZone);
        FlushFieldHistory(match);
        InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == weapon.Id));
        owner.Hero[GameTag.EXHAUSTED] = 0;
        FlushFieldHistory(match); int cursor = capture.Packets.Count;
        InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == owner.Opponent.Hero.Id));
        FlushFieldHistory(match); var entries = History(capture, cursor);
        var shot = entries.Single(e => e.HasSubSpellStart).SubSpellStart;
        Check(shot.SourceEntityId == cannon.Id && shot.TargetEntityIds.SequenceEqual(new[] { owner.Opponent.Hero.Id }), "Hand Cannon attributes shot to Cannoneer");
        Check(shot.SpellPrefabGuid == "ReuseFX_Generic_Missile_Cannon_Super_Quickest:ed9fdef57cbdcbb44bbfd9f06c1c8d24", "Hand Cannon uses cannon cue");
        Check(entries.Any(e => e.HasPowerStart && e.PowerStart.Source == cannon.Id && (int)e.PowerStart.Type == (int)BlockType.TRIGGER
            && e.PowerStart.EffectIndex == -1), "Hand Cannon shot is contributor trigger");
    }

    private static void VulcanosEndTurnVisual()
    {
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var owner = match.Game.CurrentPlayer;
            var source = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CATA_488"), zone: owner.BoardZone);
            var friendly = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_200"), zone: owner.BoardZone);
            var enemy = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
            friendly.BaseHealth = enemy.BaseHealth = 30;
            owner.ExtraEndTurnEffect = true;
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.END_TURN));
            FlushFieldHistory(match);
            var entries = History(capture, cursor);
            Check(entries.Count(e => e.HasPowerStart && e.PowerStart.Source == source.Id
                && (int)e.PowerStart.Type == (int)BlockType.TRIGGER) == 2,
                "Vulcanos doubled end turn has two effect triggers, excluding appendage setup");
            Check(entries.Count(e => e.HasSubSpellStart && e.SubSpellStart.SourceEntityId == source.Id
                && e.SubSpellStart.TargetEntityIds.Count == 0) == 2,
                "Vulcanos sends one public area effect per end turn invocation on both seats");
            Check(friendly.Damage == 6 && enemy.Damage == 6 && source.Damage == 0,
                "Vulcanos area effect preserves both boards and source exclusion");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Vulcanos doubled end turn: both seats, source-only fire effect and unchanged damage");
    }

    private static void ModernSubSpellFamilies()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string card in new[] { "TIME_015", "DINO_417", "TIME_601", "CATA_556", "JAIL_454", "TIME_609" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var owner = match.Game.CurrentPlayer;
            foreach (var old in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(old));
            owner.Hero.Damage = 5;
            var friends = new[] { "CS2_182", "CS2_231" }.Select(id =>
                EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.BoardZone)).ToArray();
            var victim = (Character)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
            victim[GameTag.HEALTH] = 30;
            var source = EngineEntity.FromCard(owner, Cards.FromId(card), zone: owner.HandZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id
                && (t.Target == null || t.Target.Id == victim.Id)));
            FlushFieldHistory(match);
            var entries = History(capture, cursor);
            int expected = card == "DINO_417" || card == "TIME_609" ? 2 : card == "TIME_601" ? 3 : card == "JAIL_454" ? 4 : 1;
            Check(entries.Count(e => e.HasSubSpellStart) == expected, card + " each actual semantic phase is animated");
            var scopes = new Stack<PegasusGame.PowerHistorySubSpellStart>();
            int powerDepth = 0;
            foreach (var entry in entries)
            {
                if (entry.HasPowerStart) powerDepth++;
                if (entry.HasSubSpellStart)
                {
                    Check(powerDepth > 0, card + " scope belongs to causal POWER");
                    scopes.Push(entry.SubSpellStart);
                    Check(entry.SubSpellStart.SourceEntityId == owner.Hero.Id, card + " public visual origin");
                    if (card is "TIME_601" or "CATA_556" or "JAIL_454")
                        Check(entry.SubSpellStart.TargetEntityIds.Count == 0,
                            card + " future/private targets omitted; no invented origin target");
                }
                if (entry.HasSubSpellEnd) Check(scopes.Count > 0 && scopes.Pop() != null, card + " scope closes");
                if (entry.HasPowerEnd) { Check(scopes.Count == 0, card + " scopes never cross block end"); powerDepth--; }
                if (entry.HasMetaData && (int)entry.MetaData.Type == 2 && card == "TIME_015")
                    Check(scopes.Peek().TargetEntityIds.SequenceEqual(new[] { owner.Hero.Id }), "heal targets resolved hero");
                if (card == "DINO_417" && entry.HasTagChange && entry.TagChange.Tag == (int)GameTag.ATK
                    && friends.Any(f => f.Id == entry.TagChange.Entity))
                    Check(scopes.Peek().TargetEntityIds.SequenceEqual(new[] { entry.TagChange.Entity }), "board buff encloses its own recipient stats");
                if (card == "JAIL_454" && entry.HasFullEntity && entry.FullEntity.Name == "JAIL_454t")
                    Check(scopes.Count == 1, "new summon FULL_ENTITY inside zero-target scope");
            }
            Check(scopes.Count == 0 && powerDepth == 0, card + " balanced history");
            Check(!NativeAdmission.ValidatePackets(capture.HistorySnapshots()
                .Select(p => (IReadOnlyList<PegasusGame.PowerHistoryData>)p.List)).Any(f => f.Rule == "R1"),
                card + " semantic phases respect effect-specific target admission");
            if (card == "TIME_601")
            {
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i].HasSubSpellStart) Check(entries[i + 1].HasSubSpellEnd, "draw source pulse precedes private draw mutation");
                Check(owner.HandZone.Count == 3, "draw outcome unchanged");
            }
            if (card == "TIME_015") Check(owner.Hero.Damage == 2 && owner.Hero.HasDivineShield, "heal and shield unchanged");
            if (card == "CATA_556")
            {
                Check(owner.HandZone.Count == 1, "one generated card in hand");
                if (bot) Check(!entries.Any(e => e.HasShowEntity && e.ShowEntity.Entity == owner.HandZone[0].Id
                    && !string.IsNullOrEmpty(e.ShowEntity.Name)), "opponent create remains anonymous");
            }
            capture.SerializeAll();
        }
        foreach (string card in new[] { "TIME_015", "DINO_417", "TIME_601", "TIME_614" })
        {
            var (match, capture) = AnimationMatch();
            var owner = match.Game.CurrentPlayer;
            owner.Hero.Damage = 0;
            var source = EngineEntity.FromCard(owner, Cards.FromId(card), zone: owner.HandZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match);
            Check(!History(capture, cursor).Any(e => e.HasSubSpellStart || e.HasSubSpellEnd), card + " no-op emits no visual scope");
        }
        Console.WriteLine("PASS modern families: heal, board buff, draw pulse, create, summon, per-victim AoE and no-ops");
    }

    private static void SubSpellTargetPrivacy()
    {
        var (match, capture) = AnimationMatch();
        var owner = match.Game.Player1;
        var hidden = EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_029"), zone: owner.Opponent.HandZone);
        var deck = EngineEntity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.DeckZone);
        FlushFieldHistory(match);
        int hero = owner.Hero.Id, other = owner.Opponent.Hero.Id;
        var list = new[] { other, hero, other };
        int cursor = capture.Packets.Count;
        var history = match.Game.PowerHistory;
        history.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, hero, "", -1, 0));
        history.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellStart { SpellPrefabGuid = HandBuffPrefab, Source = 0, Targets = list.ToList() });
        FlushFieldHistory(match); // An open visible scope survives packet boundaries.
        history.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellEnd());
        foreach (var targets in new[] { Array.Empty<int>(), new[] { hidden.Id }, new[] { other, hidden.Id }, new[] { deck.Id } })
        {
            history.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellStart { SpellPrefabGuid = HandBuffPrefab, Source = targets.Length == 0 ? 0 : hero, Targets = targets.ToList() });
            if (targets.Length == 0)
            {
                history.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellStart { SpellPrefabGuid = HandBuffPrefab, Source = hero, Targets = new List<int> { other } });
                history.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellEnd());
            }
            history.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellEnd());
        }
        history.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellStart { SpellPrefabGuid = HandBuffPrefab, Source = hidden.Id, Targets = new List<int> { other } });
        history.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellEnd());
        history.Add(PowerHistoryBuilder.BlockEnd());
        history.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, hidden.Id, "", -1, 0));
        history.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellStart { SpellPrefabGuid = HandBuffPrefab, Targets = new List<int> { other } });
        history.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellEnd());
        history.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellStart { SpellPrefabGuid = HandBuffPrefab, Source = hero,
            Targets = new List<int> { other }, RequirePublicOrigin = true });
        history.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellEnd());
        history.Add(PowerHistoryBuilder.BlockEnd());
        FlushFieldHistory(match);
        var entries = History(capture, cursor);
        var starts = entries.Where(e => e.HasSubSpellStart).Select(e => e.SubSpellStart).ToArray();
        Check(starts.Length == 3 && entries.Count(e => e.HasSubSpellEnd) == 3, "nested public scopes survive; mixed/private targets or private source suppress whole balanced scopes");
        Check(starts[0].SourceEntityId == 0 && starts[0].TargetEntityIds.SequenceEqual(list), "source zero and ordered duplicate targets survive transport");
        Check(starts[1].TargetEntityIds.Count == 0 && starts[1].SourceEntityId == 0, "public causal origin allows zero-target inherited-source scope");
        using (var stream = new System.IO.MemoryStream())
        {
            starts[0].Serialize(stream); stream.Position = 0;
            var roundtrip = new PegasusGame.PowerHistorySubSpellStart(); roundtrip.Deserialize(stream);
            Check(roundtrip.SourceEntityId == 0 && roundtrip.TargetEntityIds.SequenceEqual(list), "native protobuf roundtrip preserves arity/order/repeats");
        }
        var convert = typeof(NativeMatch).GetMethod("ToNativeHistory", BindingFlags.Instance | BindingFlags.NonPublic);
        bool rejected = false;
        try { convert.Invoke(match, new object[] { new SabberStoneCore.Kettle.PowerHistorySubSpellEnd() }); }
        catch (TargetInvocationException error) { rejected = error.InnerException is InvalidOperationException; }
        Check(rejected, "orphan END rejected as invalid internal history");
        capture.SerializeAll();
        Console.WriteLine("PASS sub-spell ordered zero/many targets, chunked scopes, private source/hand/deck and orphan END");
    }

    private static void SubSpellDeterminism()
    {
        foreach (string card in new[] { "TIME_001", "TIME_015", "DINO_417", "TIME_601", "CATA_556", "JAIL_454" })
        {
            var (match, _) = AnimationMatch();
            var owner = match.Game.CurrentPlayer;
            foreach (var old in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(old));
            EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.BoardZone);
            owner.Hero.Damage = 5;
            var victim = (Character)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
            var source = EngineEntity.FromCard(owner, Cards.FromId(card), zone: owner.HandZone);
            var on = match.Game.CloneExact(history: true); var off = match.Game.CloneExact(history: false);
            foreach (var game in new[] { on, off })
                game.Process(game.CurrentPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id
                    && (t.Target == null || t.Target.Id == victim.Id)));
            Check(on.Hash() == off.Hash(), card + " history on/off identical gameplay hash");
            Check(on.PowerHistory.Full.Any(e => e is SabberStoneCore.Kettle.PowerHistorySubSpellStart), card + " determinism fixture exercises visual path");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.NonPublic | BindingFlags.Instance);
            var left = random.GetValue(on); var right = random.GetValue(off);
            var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), card + " unchanged RNG continuation");
        }
        Console.WriteLine("PASS presentation determinism across all semantic families");
    }

    private static void SubSpellScopeBoundaries()
    {
        var (match, _) = AnimationMatch();
        var game = match.Game; var owner = game.CurrentPlayer;
        var source = EngineEntity.FromCard(owner, Cards.FromId("TIME_001"), zone: owner.HandZone);
        var child = EngineEntity.FromCard(owner, Cards.FromId("CORE_CS2_029"), zone: owner.HandZone);
        var history = game.PowerHistory;
        var phase = Enum.Parse(typeof(SabberStoneCore.Model.Game).Assembly.GetType("SabberStoneCore.Presentation.VisualPhase"), "Damage");
        var visual = typeof(PowerHistory).GetMethod("Visual", BindingFlags.Instance | BindingFlags.NonPublic);
        IDisposable Scope() => (IDisposable)visual.Invoke(history, new object[] { game, phase, source, owner.Opponent.Hero });
        history.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, source.Id, "", -1, 0));
        int cursor = history.Full.Count;
        using (Scope()) owner.Opponent.Hero.TakeDamage(child, 1);
        Check(!history.Full.Skip(cursor).Any(e => e is SabberStoneCore.Kettle.PowerHistorySubSpellStart),
            "nested contributor damage cannot make a no-op outer phase applicable");
        cursor = history.Full.Count;
        try
        {
            using (var scope = Scope())
            {
                owner.Opponent.Hero.TakeDamage(source, 1);
                scope.Dispose(); // Re-disposal on unwind must not duplicate either annotation.
                throw new InvalidOperationException("fixture failure after effect");
            }
        }
        catch (InvalidOperationException error) when (error.Message == "fixture failure after effect") { }
        history.Add(PowerHistoryBuilder.BlockEnd());
        var full = history.Full.Skip(cursor).ToArray();
        Check(full.Count(e => e is SabberStoneCore.Kettle.PowerHistorySubSpellStart) == full.Count(e => e is SabberStoneCore.Kettle.PowerHistorySubSpellEnd),
            "scope exception and double disposal leave balanced history");
        Check(history.Last.SequenceEqual(history.Full.Skip(history.Full.Count - history.Last.Count)), "Full/Last annotation order remains identical");
        foreach (var row in new[] {
            (Source: source, Effect: "CORE_CS2_029", Index: 0, Scopes: 0),
            (Source: child, Effect: "TIME_001", Index: 0, Scopes: 1),
            (Source: source, Effect: "TIME_001", Index: 1, Scopes: 0) })
        {
            cursor = history.Full.Count;
            history.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, row.Source.Id, row.Effect, -1, 0, effectIndex: row.Index));
            owner.Opponent.Hero.TakeDamage(row.Source, 1);
            history.Add(PowerHistoryBuilder.BlockEnd());
            Check(history.Full.Skip(cursor).Count(e => e is SabberStoneCore.Kettle.PowerHistorySubSpellStart) == row.Scopes,
                "recipe uses executed EffectCardId and exact effect index");
        }
        Console.WriteLine("PASS synchronous scope boundaries, independent contributor, exception disposal and Full/Last indices");
    }

    private static void SubSpellTriggeredCreate()
    {
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var owner = match.Game.CurrentPlayer;
            var source = (Minion)EngineEntity.FromCard(owner, Cards.FromId("DINO_434"), zone: owner.BoardZone);
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            source.ToBeDestroyed = true; match.Game.DeathProcessingAndAuraUpdate();
            FlushFieldHistory(match);
            var entries = History(capture, cursor);
            Check(entries.Count(e => e.HasSubSpellStart) == 1 && entries.Count(e => e.HasSubSpellEnd) == 1,
                "Raptor Nest Nurse Deathrattle has its own completed create phase");
            var start = entries.Single(e => e.HasSubSpellStart).SubSpellStart;
            Check(start.TargetEntityIds.Count == 0
                && start.SourceEntityId == owner.Hero.Id, "triggered creation preserves private identity without invented targets");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS distinct Deathrattle create recipe, both players");
    }
}
