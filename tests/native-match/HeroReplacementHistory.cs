using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEnchantment = SabberStoneCore.Model.Entities.Enchantment;

internal static partial class Program
{
    // ZoneMgr pairs a dragged hero card's local prediction (position 0) with the ZONE change only if the next position is 0.
    private static void CheckHeroSlotCleared(List<PowerHistoryData> history, SabberStoneCore.Model.Entities.IPlayable hero, string label)
    {
        int entered = history.FindIndex(e => IsZone(e, hero.Id, SabberStoneCore.Enums.Zone.PLAY));
        Check(entered >= 0 && entered + 1 < history.Count && history[entered + 1].HasTagChange
            && history[entered + 1].TagChange.Entity == hero.Id
            && history[entered + 1].TagChange.Tag == (int)GameTag.ZONE_POSITION
            && history[entered + 1].TagChange.Value == 0, label + " clears the hand slot right after entering PLAY");
        Check(hero[GameTag.ZONE_POSITION] == 0, label + " engine tag agrees with the client");
    }

    // Every way a hero card reaches PLAY, not only from the human's hand: the NPC's seat, a token, a card whose owner changed, a card
    // transformed in hand, and one already at position 0. Then a Rewind must not hand the client the old slot back.
    private static void HeroSlotAcrossRoutes()
    {
        foreach (string cardId in new[] { "CORE_EX1_323", "CATA_190h", "TLC_513t" })
        {
            var (match, capture) = AnimationMatch(botTurn: true, deck: NpcCatalog.All[0].DeckId);
            var bot = match.Game.Player2;
            bot.BaseMana = 10; bot.UsedMana = 0;
            var source = SabberStoneCore.Model.Entities.Entity.FromCard(bot, Cards.FromId(cardId), zone: bot.HandZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            var task = bot.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id);
            match.ActNpc(new GoldenDecision(BotAction.From(task), 0, 1, 1, "Exhausted", null, null));
            var history = History(capture, cursor);
            CheckHeroSlotCleared(history, source, cardId + " played by the NPC");
        }
        {
            var (capture, match) = ChoiceRetirementMatch(CardClass.WARLOCK);
            var owner = match.Game.Player1; var enemy = match.Game.Player2;
            var stolen = SabberStoneCore.Model.Entities.Entity.FromCard(enemy, Cards.FromId("CORE_EX1_323"), zone: enemy.HandZone);
            enemy.HandZone.Remove(stolen);
            stolen.Controller = owner;
            owner.HandZone.Add(stolen);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
            var options = capture.Last<AllOptions>(14);
            match.SendOption(options.Id, options.Options.FindIndex(o => o.MainOption?.Id == stolen.Id && o.MainOption.PowerKeyword == 0), 0, -1, 0);
            CheckHeroSlotCleared(History(capture, cursor), stolen, "a hero card taken from the opponent's hand");
        }
        {
            var (capture, match) = ChoiceRetirementMatch(CardClass.WARLOCK);
            var owner = match.Game.Player1;
            var held = SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            var hero = Generic.ChangeEntityBlock.Invoke(owner, held, Cards.FromId("CORE_EX1_323"), true);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
            var options = capture.Last<AllOptions>(14);
            match.SendOption(options.Id, options.Options.FindIndex(o => o.MainOption?.Id == hero.Id && o.MainOption.PowerKeyword == 0), 0, -1, 0);
            CheckHeroSlotCleared(History(capture, cursor), hero, "a card transformed in hand into a hero");
        }
        {
            // Not from a hand: already at position 0, so no tag change is needed and none may name another slot.
            var (capture, match) = ChoiceRetirementMatch(CardClass.WARLOCK);
            var owner = match.Game.Player1;
            var hero = (SabberStoneCore.Model.Entities.Hero)SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("TLC_513t"), zone: owner.SetasideZone);
            hero[GameTag.ZONE_POSITION] = 0;
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            match.Game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(SabberStoneCore.Enums.BlockType.PLAY, hero.Id, "", -1, 0));
            typeof(Generic).GetMethod("PlayHeroCore", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                new object[] { owner, match.Game, hero, null, 0, true, false });
            match.Game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockEnd());
            FlushFieldHistory(match);
            var history = History(capture, cursor);
            Check(owner.Hero == hero && hero[GameTag.ZONE_POSITION] == 0, "a token hero from SETASIDE enters PLAY at position 0");
            Check(!history.Any(e => e.HasTagChange && e.TagChange.Entity == hero.Id && e.TagChange.Tag == (int)GameTag.ZONE_POSITION && e.TagChange.Value != 0),
                "a token hero never names a slot");
        }
        {
            // A Rewind FULL restores the engine's tags, so the slot must already be gone from the engine.
            var (capture, match) = ChoiceRetirementMatch(CardClass.WARLOCK);
            var owner = match.Game.Player1; var game = match.Game;
            for (int i = 0; i < 4; i++) SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            var hero = SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CORE_EX1_323"), zone: owner.HandZone);
            var coin = SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("GAME_005"), zone: owner.HandZone);
            coin[GameTag.REWIND] = 1;
            FlushFieldHistory(match);
            typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
            var options = capture.Last<AllOptions>(14);
            match.SendOption(options.Id, options.Options.FindIndex(o => o.MainOption?.Id == hero.Id && o.MainOption.PowerKeyword == 0), 0, -1, 0);
            Check(owner.Hero == hero, "the Rewind fixture's hero is in play");
            var choice = ChoiceRetirementPlay(match, capture, coin, 0);
            int rewind = owner.Choice.Choices.Single(id => game.IdEntityDic[id].Card.Id == "TIME_000tb");
            int cursor = capture.Packets.Count;
            match.SendChoices(choice.Id, new List<int> { rewind });
            var history = History(capture, cursor);
            Check(history.Any(e => e.HasResetGame), "the Rewind fixture emits a real GAME_RESET");
            var restored = history.Where(e => e.HasFullEntity && e.FullEntity.Entity == hero.Id).ToList();
            Check(restored.Count > 0 && restored.All(e => SnapshotTag(e, GameTag.ZONE_POSITION) == 0), "a Rewind restores the played hero at position 0");
            Check(!history.Any(e => e.HasTagChange && e.TagChange.Entity == hero.Id && e.TagChange.Tag == (int)GameTag.ZONE_POSITION && e.TagChange.Value != 0),
                "nothing after a Rewind gives the played hero a slot");
            Check(game.IdEntityDic[hero.Id][GameTag.ZONE_POSITION] == 0, "the restored engine hero keeps position 0");
        }
        Console.WriteLine("PASS hero slot: NPC seat, token, taken and transformed cards, position 0, Rewind");
    }

    private static void HeroReplacementHistory(bool dump = false)
    {
        HeroSlotAcrossRoutes();
        foreach (var (cardId, generated) in new[] { ("CORE_EX1_323", true), ("CORE_EX1_323", false),
            ("EX1_323", false), ("TLC_513t", false), ("CATA_190h", false) })
        {
            var card = Cards.FromId(cardId);
            var (capture, match) = ChoiceRetirementMatch(cardId == "CATA_190h" ? CardClass.WARLOCK : card.Class);
            var owner = match.Game.Player1;
            int damage = generated ? 0 : 11, armor = generated ? 0 : 3;
            owner.Hero.Damage = damage;
            owner.Hero.Armor = armor;
            int oldHero = owner.Hero.Id, oldPower = owner.Hero.HeroPower.Id;
            var source = generated
                ? SabberStoneCore.Model.Entities.Entity.FromCard(owner, card, zone: owner.HandZone)
                : Generic.DrawCard(owner, card);
            FlushFieldHistory(match);
            typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
            var options = capture.Last<AllOptions>(14);
            int play = options.Options.FindIndex(o => o.MainOption?.Id == source.Id && o.MainOption.PowerKeyword == 0);
            Check(play >= 0, cardId + " native play option");
            int cursor = capture.Packets.Count;
            match.SendOption(options.Id, play, 0, -1, card.Type == CardType.MINION ? 1 : 0);
            while (owner.Choice != null)
            {
                var choice = capture.Last<EntityChoices>(17);
                match.SendChoices(choice.Id, new List<int> { choice.Entities[0] });
            }
            var history = History(capture, cursor);
            string label = cardId + (generated ? " (Entity.FromCard, undamaged)" : " (drawn, damaged)");
            if (dump) DumpHeroReplacement(label, source.Id, oldHero, oldPower, history);
            var hero = owner.Hero;
            Check(owner.Choice == null && !match.Game.Resolution.HasSuspendedEffect, cardId + " finishes engine resolution");
            Check(history.Any(e => IsZone(e, oldHero, SabberStoneCore.Enums.Zone.SETASIDE))
                && history.Any(e => IsZone(e, oldPower, SabberStoneCore.Enums.Zone.SETASIDE)), cardId + " retires old hero and power");
            Check(history.Count(e => e.HasFullEntity && e.FullEntity.Entity == hero.HeroPower.Id) == 1,
                cardId + " creates replacement power exactly once");
            Check(history.Any(e => IsZone(e, hero.Id, SabberStoneCore.Enums.Zone.PLAY)), cardId + " moves replacement hero to PLAY");
            // The legacy EX1_323 recipe replaces the hero with a new entity, so only a card that becomes the hero itself counts.
            if (hero.Id == source.Id) CheckHeroSlotCleared(history, source, cardId);
            if (cardId == "CORE_EX1_323" || cardId == "EX1_323")
            {
                int power = history.FindIndex(e => e.HasPowerStart && e.PowerStart.Type == HistoryBlock.Type.POWER && e.PowerStart.Source == source.Id);
                Check(power >= 0, cardId + " opens weapon Battlecry POWER");
                int end = history.FindIndex(power + 1, e => e.HasPowerEnd);
                Check(end > power, cardId + " closes weapon Battlecry POWER");
                var body = history.Skip(power + 1).Take(end - power - 1).ToList();
                Check(body.Count(e => e.HasFullEntity && e.FullEntity.Name == "EX1_tk33"
                    && SnapshotTag(e, GameTag.CARDTYPE) == (int)CardType.HERO_POWER) == 1,
                    "JaraxxusHeroSpell receives INFERNO! FULL_ENTITY inside its POWER task list");
                Check(body.Count(e => e.HasFullEntity && e.FullEntity.Name == "EX1_323w"
                    && SnapshotTag(e, GameTag.CARDTYPE) == (int)CardType.WEAPON) == 1,
                    "JaraxxusHeroSpell receives Blood Fury FULL_ENTITY inside its POWER task list");
                CheckJaraxxusTaskList(history, power, cardId);
                if (cardId == "CORE_EX1_323") CheckJaraxxusSpawnZones(body, owner.PlayerId);
                int weaponCreate = body.FindIndex(e => e.HasFullEntity && e.FullEntity.Entity == hero.Weapon.Id);
                int weaponPlay = body.FindIndex(e => IsZone(e, hero.Weapon.Id, SabberStoneCore.Enums.Zone.PLAY));
                Check(weaponCreate >= 0 && weaponPlay > weaponCreate && hero.Weapon.AttackDamage == 3 && hero.Weapon.Durability == 8,
                    cardId + " creates and equips 3/8 Blood Fury in order");
                Check(hero.HeroPower.Card.Id == "EX1_tk33", cardId + " installs INFERNO!");
            }
            if (cardId != "EX1_323")
            {
                Check(hero.Id == source.Id && hero.Health == 30 - damage && hero.Armor == armor + card[GameTag.ARMOR],
                    cardId + " retains damaged hero health, adds armor, uses played entity");
                Check(!history.Any(e => e.HasFullEntity && e.FullEntity.Entity == source.Id),
                    cardId + " never recreates existing hand HERO");
            }
            else Check(hero.Card.Id == "EX1_323h" && hero.Health == 15, "legacy Jaraxxus retains legacy minion replacement rules");
            if (cardId == "TLC_513t") Check(owner.BoardZone.Count == 2, "Master Dusk completes both Ninja summons");
            if (cardId == "CATA_190h") Check(owner.BoardZone.Any(m => m.Card.Id == "CATA_190t14"), "Deathwing choice resumes and summons dragon");
            int depth = 0;
            foreach (var row in history)
            {
                if (row.HasPowerStart) depth++;
                if (row.HasPowerEnd) Check(--depth >= 0, cardId + " has no unmatched block end");
            }
            Check(depth == 0, cardId + " closes all PLAY/POWER blocks");
            // Packet contract only: Unity actors/coroutines cannot run in this harness.
            capture.SerializeAll();
            Console.WriteLine("PASS hero replacement " + label + ": lifecycle, Battlecry scope, state and balanced history");
        }
        JaraxxusOnPlayTriggerHistory(bot: false, holmes: true, dump);
        JaraxxusOnPlayTriggerHistory(bot: true, holmes: true, dump);
        JaraxxusOnPlayTriggerHistory(bot: false, holmes: false, dump);
        JaraxxusOnPlayTriggerHistory(bot: true, holmes: false, dump);
        JaraxxusPowerCreationScopes();
    }

    private static void JaraxxusOnPlayTriggerHistory(bool bot, bool holmes, bool dump)
    {
        var (match, capture) = AnimationMatch(botTurn: bot, deck: NpcCatalog.All[0].DeckId);
        var game = match.Game;
        var owner = game.CurrentPlayer;
        var watcher = holmes ? owner.Opponent : owner;
        var source = Generic.DrawCard(owner, Cards.FromId("CORE_EX1_323"));
        var watch = EngineEnchantment.GetInstance(watcher, watcher.Hero, watcher, Cards.FromId(holmes ? "JAIL_851e" : "BE_784t4e"));
        if (holmes)
        {
            watch[DiscoverRealRules.WatchedDefinition] = source.Card.AssetId;
            watch[DiscoverRealRules.WatchedController] = owner.Id;
            watch[DiscoverRealRules.WatchedTurn] = game.HistoryFacts.Query.TurnsTaken(owner.Id);
        }
        watch.Power.Trigger.Activate(watch);
        int coins = watcher.HandZone.Count(c => c.Card.Id == "GAME_005");
        FlushFieldHistory(match);
        int cursor = capture.Packets.Count;
        if (bot)
        {
            var task = owner.Options().Single(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id);
            match.ActNpc(new GoldenDecision(BotAction.From(task), 0, 1, 1, "Exhausted", null, null));
        }
        else
        {
            typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
            var options = capture.Last<AllOptions>(14);
            int play = options.Options.FindIndex(o => o.MainOption?.Id == source.Id && o.MainOption.PowerKeyword == 0);
            Check(play >= 0, "watched Jaraxxus native play option");
            match.SendOption(options.Id, play, 0, -1, 0);
        }
        var history = History(capture, cursor);
        string label = (bot ? "NPC" : "human") + " Jaraxxus with " + watch.Card.Id;
        int trigger = history.FindIndex(e => e.HasPowerStart && e.PowerStart.Type == HistoryBlock.Type.TRIGGER && e.PowerStart.Source == watch.Id);
        int power = history.FindIndex(e => e.HasPowerStart && e.PowerStart.Type == HistoryBlock.Type.POWER && e.PowerStart.Source == source.Id);
        Check(trigger >= 0 && power > trigger && history.Skip(trigger + 1).Take(power - trigger - 1).Any(e => e.HasPowerEnd),
            label + " has a completed on-play sibling TRIGGER before weapon POWER");
        Check(!watcher.AppliedEnchantments.Contains(watch), label + " consumes active watch");
        if (holmes) Check(watcher.HandZone.Count(c => c.Card.Id == "GAME_005") == coins + 3, label + " pays three Coins");
        int newPower = owner.Hero.HeroPower.Id;
        int create = history.FindIndex(e => e.HasFullEntity && e.FullEntity.Entity == newPower);
        Check(history.Count(e => e.HasFullEntity && e.FullEntity.Entity == newPower) == 1, label + " creates INFERNO! once");
        int end = history.FindIndex(power + 1, e => e.HasPowerEnd);
        Check(create > power && create < end && history[create].FullEntity.Name == "EX1_tk33"
            && SnapshotTag(history[create], GameTag.CARDTYPE) == (int)CardType.HERO_POWER, label + " admits INFERNO! in weapon POWER");
        Check(history.Skip(create + 1).Take(end - create - 1).Count(e => e.HasFullEntity && e.FullEntity.Name == "EX1_323w"
            && SnapshotTag(e, GameTag.CARDTYPE) == (int)CardType.WEAPON) == 1, label + " creates weapon after INFERNO!");
        CheckJaraxxusTaskList(history, power, label);
        CheckJaraxxusSpawnZones(history.Skip(power + 1).Take(end - power - 1).ToList(), owner.PlayerId);
        Check(!history.Take(create).Any(e => e.HasTagChange && e.TagChange.Entity == newPower), label + " has no power tags before creation");
        Check(owner.Choice == null && !game.Resolution.HasSuspendedEffect && owner.Hero.Weapon.AttackDamage == 3
            && owner.Hero.Weapon.Durability == 8, label + " completes engine resolution and equips Blood Fury");
        int depth = 0;
        foreach (var row in history)
        {
            if (row.HasPowerStart) depth++;
            if (row.HasPowerEnd) Check(--depth >= 0, label + " has no unmatched block end");
        }
        Check(depth == 0, label + " closes all blocks");
        if (dump) DumpHeroReplacement(label, source.Id, 0, 0, history);
        capture.SerializeAll();
        Console.WriteLine("PASS hero replacement " + label + ": sibling trigger, paired creations, dependency order and balanced history");
    }

    private static void CheckJaraxxusSpawnZones(List<PowerHistoryData> body, int controller)
    {
        // SetupCards uses GetZone(), not GetRealTimeZone(), before executing any queued TAG_CHANGE.
        // A null weapon zone leaves no actor; ActivateActorSpell then returns without its finished callback.
        foreach (string cardId in new[] { "EX1_tk33", "EX1_323w" })
        {
            var full = body.Single(row => row.HasFullEntity && row.FullEntity.Name == cardId);
            Check(SnapshotTag(full, GameTag.ZONE) == (int)SabberStoneCore.Enums.Zone.PLAY
                && SnapshotTag(full, GameTag.CONTROLLER) == controller,
                "Jaraxxus SetupCards can resolve " + cardId + " PLAY zone from initial FULL_ENTITY before task execution");
        }
    }

    private static void CheckJaraxxusTaskList(List<PowerHistoryData> history, int power, string label)
    {
        int depth = 0, taskList = 0, infernoList = -1, weaponList = -2;
        for (int at = power + 1; at < history.Count; at++)
        {
            var row = history[at];
            if (row.HasPowerStart || row.HasSubSpellStart) { depth++; taskList++; }
            if (row.HasPowerEnd || row.HasSubSpellEnd)
            {
                if (depth == 0) break;
                depth--; taskList++;
            }
            if (depth != 0 || !row.HasFullEntity) continue;
            if (row.FullEntity.Name == "EX1_tk33") infernoList = taskList;
            if (row.FullEntity.Name == "EX1_323w") weaponList = taskList;
        }
        Check(infernoList >= 0 && infernoList == weaponList,
            label + " has both FULL_ENTITYs in one uninterrupted POWER task list (no BLOCK_START between them)");
    }

    private static void JaraxxusPowerCreationScopes()
    {
        PowerHistoryData Start(HistoryBlock.Type type, int source) => new PowerHistoryData {
            PowerStart = new PowerHistoryStart { Type = type, Source = source } };
        PowerHistoryData End() => new PowerHistoryData { PowerEnd = new PowerHistoryEnd() };
        PowerHistoryData Full(int id) => new PowerHistoryData {
            FullEntity = new PegasusGame.PowerHistoryEntity { Entity = id, Name = "EX1_tk33" } };
        PowerHistoryData Tag(int value, int id = 70) => new PowerHistoryData {
            TagChange = new PegasusGame.PowerHistoryTagChange { Entity = id, Tag = (int)GameTag.COST, Value = value } };
        var weapon = new PowerHistoryData { FullEntity = new PegasusGame.PowerHistoryEntity { Entity = 71, Name = "EX1_323w" } };
        void AssertMove(string label, PowerHistoryData[] before, PowerHistoryData[] after, bool paired = false, int warnings = 0)
        {
            var packet = new PowerHistory { List = before.Concat(new[] { weapon }).ToList() };
            var logged = new List<string>();
            typeof(NativeMatch).GetMethod("MoveJaraxxusPowerCreation", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { packet, (Action<string>)logged.Add });
            var expected = after.Concat(new[] { weapon }).ToArray();
            Check(packet.List.Count == expected.Length && packet.List.Zip(expected, (actual, wanted) => ReferenceEquals(actual, wanted)).All(equal => equal),
                "Jaraxxus relocation " + label + " preserves exact packet objects/order");
            Check(logged.Count == warnings && logged.All(line => line.StartsWith("WARN ") && !line.Contains('\n')),
                "Jaraxxus relocation " + label + " logs one warning line per unsafe move");
            if (paired) CheckJaraxxusTaskList(packet.List,
                packet.List.FindIndex(row => row.HasPowerStart && row.PowerStart.Type == HistoryBlock.Type.POWER && row.PowerStart.Source == 69), label);
            Console.WriteLine("PASS Jaraxxus relocation " + label);
        }

        var play = Start(HistoryBlock.Type.PLAY, 69);
        var power = Start(HistoryBlock.Type.POWER, 69);
        var full = Full(70);
        var tag1 = Tag(1); var tag2 = Tag(2);
        var trigger = Start(HistoryBlock.Type.TRIGGER, 80);
        var nested = Start(HistoryBlock.Type.POWER, 81);
        var decoy = Full(82);
        var end1 = End(); var end2 = End();
        AssertMove("nested siblings and separated dependent tags",
            new[] { play, full, tag1, trigger, nested, decoy, end1, end2, tag2, power },
            new[] { play, trigger, nested, decoy, end1, end2, power, full, tag1, tag2 }, paired: true);
        var priorPlay = Start(HistoryBlock.Type.PLAY, 68);
        var previous = new[] { priorPlay, full, end1, play, power };
        AssertMove("cannot cross previous PLAY", previous, previous);
        var sibling = new[] { play, trigger, full, end1, power };
        AssertMove("cannot take sibling creation", sibling, sibling);
        var enclosing = new[] { play, full, trigger, power };
        AssertMove("requires directly enclosing PLAY", enclosing, enclosing);
        var meta = new PowerHistoryData { MetaData = new PegasusGame.PowerHistoryMetaData { Info = new List<int> { 70 } } };
        var dependent = new[] { play, full, trigger, nested, tag1, meta, end1, end2, tag2, power };
        AssertMove("dependent sibling leaves stream unchanged", dependent, dependent, warnings: 1);
        var sub = new PowerHistoryData { SubSpellStart = new PegasusGame.PowerHistorySubSpellStart {
            SpellPrefabGuid = "scope-test", SourceEntityId = 80, TargetEntityIds = new List<int>() } };
        var subEnd = new PowerHistoryData { SubSpellEnd = new PegasusGame.PowerHistorySubSpellEnd() };
        AssertMove("completed subspell decoy stays wrapped",
            new[] { play, full, sub, decoy, subEnd, power },
            new[] { play, sub, decoy, subEnd, power, full }, paired: true);
        var onlyDecoy = new[] { play, sub, decoy, subEnd, power };
        AssertMove("cannot take subspell creation", onlyDecoy, onlyDecoy);
        var openSubspell = new[] { play, full, sub, power };
        AssertMove("requires PLAY rather than open subspell", openSubspell, openSubspell);
        var other1 = Tag(1, 90); var other2 = Tag(2, 90);
        var mixed = new[] { play, full, trigger, tag1, other1, end1, other2, power };
        AssertMove("mixed-entity sibling cannot reverse unrelated updates", mixed, mixed, warnings: 1);
        AssertMove("independent updates keep order across relocation",
            new[] { play, full, tag1, trigger, other1, end1, other2, tag2, power },
            new[] { play, trigger, other1, end1, other2, power, full, tag1, tag2 }, paired: true);
        var mixedMeta = new PowerHistoryData { MetaData = new PegasusGame.PowerHistoryMetaData { Info = new List<int> { 70, 90 } } };
        var mixedRow = new[] { play, full, mixedMeta, other2, power };
        AssertMove("mixed-entity loose row cannot move", mixedRow, mixedRow, warnings: 1);
        AssertMove("single-entity loose metadata moves with creation",
            new[] { play, full, meta, trigger, other1, end1, power },
            new[] { play, trigger, other1, end1, power, full, meta }, paired: true);
        var prefix = new[] { play, full, power, tag1 };
        AssertMove("cannot cross dependent POWER prefix", prefix, prefix, warnings: 1);
        AssertMove("insert immediately before weapon after independent POWER sibling",
            new[] { play, full, power, trigger, other1, end1 },
            new[] { play, power, trigger, other1, end1, full }, paired: true);
        var target = Start(HistoryBlock.Type.TRIGGER, 80);
        target.PowerStart.Target = 70;
        var targeted = new[] { play, full, target, end1, power };
        AssertMove("dependent block target cannot move", targeted, targeted, warnings: 1);
        var subTarget = new PowerHistoryData { SubSpellStart = new PegasusGame.PowerHistorySubSpellStart {
            SpellPrefabGuid = "scope-test", SourceEntityId = 80, TargetEntityIds = new List<int> { 70 } } };
        var subDependent = new[] { play, full, subTarget, subEnd, power };
        AssertMove("dependent subspell target cannot move", subDependent, subDependent, warnings: 1);
    }

    private static void DumpHeroReplacement(string cardId, int source, int oldHero, int oldPower, List<PowerHistoryData> history)
    {
        Console.WriteLine("CAPTURE " + cardId + " source=" + source + " oldHero=" + oldHero + " oldPower=" + oldPower);
        foreach (var row in history)
        {
            if (row.HasPowerStart) Console.WriteLine("BLOCK " + row.PowerStart.Type + " source=" + row.PowerStart.Source);
            else if (row.HasPowerEnd) Console.WriteLine("END");
            else if (row.HasTagChange) Console.WriteLine("TAG " + row.TagChange.Entity + " " + (GameTag)row.TagChange.Tag + "=" + row.TagChange.Value);
            else if (row.HasFullEntity || row.HasShowEntity || row.HasChangeEntity)
            {
                var e = row.HasFullEntity ? row.FullEntity : row.HasShowEntity ? row.ShowEntity : row.ChangeEntity;
                Console.WriteLine((row.HasFullEntity ? "FULL " : row.HasShowEntity ? "SHOW " : "CHANGE ") + e.Entity + " " + e.Name + " "
                    + string.Join(" ", e.Tags.Select(t => (GameTag)t.Name + "=" + t.Value)));
            }
        }
    }
}
