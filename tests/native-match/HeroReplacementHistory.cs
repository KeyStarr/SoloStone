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

    // Lord Jaraxxus: the client's JaraxxusHeroSpell only attaches if both new entities (the INFERNO! hero power and Blood
    // Fury) are FULL_ENTITY rows inside the card's own POWER task list, each created in PLAY (its SetupCards reads the
    // snapshot zone and never runs queued tag changes). Before, INFERNO! sat in the parent PLAY block, the spell never
    // attached and the client's task list never finished. Sibling on-play triggers (Inspector Murloc Holmes' watch) between
    // the two must not stop the rows being moved.
    private static void HeroReplacementHistory(bool dump = false)
    {
        HeroSlotAcrossRoutes();
        // The 37.0 client defines EX1_323 as a hero card; this legacy minion is only a ReplaceHeroTask packet-shape fixture, not client-playable.
        foreach (var (cardId, generated) in new[] { ("CORE_EX1_323", true), ("CORE_EX1_323", false),
            ("EX1_323", false), ("TLC_513t", false), ("CATA_190h", false), ("ICC_829", false), ("ICC_834", false) })
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
                CheckJaraxxusSpawnZones(body, owner.PlayerId);
                if (cardId == "EX1_323") CheckReplacementHeroFull(history, hero.Id, oldHero, owner.PlayerId, cardId);
                int weaponCreate = body.FindIndex(e => e.HasFullEntity && e.FullEntity.Entity == hero.Weapon.Id);
                int weaponPlay = body.FindIndex(e => IsZone(e, hero.Weapon.Id, SabberStoneCore.Enums.Zone.PLAY));
                Check(weaponCreate >= 0 && weaponPlay > weaponCreate && hero.Weapon.AttackDamage == 3 && hero.Weapon.Durability == 8,
                    cardId + " creates and equips 3/8 Blood Fury in order");
                Check(hero.HeroPower.Card.Id == "EX1_tk33", cardId + " installs INFERNO!");
            }
            if (cardId == "ICC_829" || cardId == "ICC_834")
            {
                var full = history.Single(row => row.HasFullEntity && row.FullEntity.Entity == hero.Weapon.Id);
                Check(SnapshotTag(full, GameTag.ZONE) == (int)SabberStoneCore.Enums.Zone.PLAY
                    && SnapshotTag(full, GameTag.CONTROLLER) == owner.PlayerId, cardId + " generic hero Battlecry weapon spawns in PLAY");
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
        LegacyJaraxxusNpcHistory(dump);
        HeroReplacementWithWeaponHistory(dump);
        MajordomoReplacementHistory(dump);
        OrdinaryWeaponSpawnZones();
        HeroReplacementSpawnZoneScopes();
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

    private static void CheckReplacementHeroFull(List<PowerHistoryData> history, int hero, int oldHero, int controller, string label)
    {
        int created = history.FindIndex(row => row.HasFullEntity && row.FullEntity.Entity == hero);
        Check(created >= 0, label + " creates replacement hero");
        var full = history[created];
        Check(SnapshotTag(full, GameTag.ZONE) == (int)SabberStoneCore.Enums.Zone.PLAY
            && SnapshotTag(full, GameTag.CONTROLLER) == controller && SnapshotTag(full, GameTag.ENTITY_ID) == hero,
            label + " FULL owns PLAY zone, controller and entity id before queued changes");
        int start = history.FindLastIndex(created, row => row.HasPowerStart || row.HasPowerEnd || row.HasSubSpellStart || row.HasSubSpellEnd);
        int end = history.FindIndex(created + 1, row => row.HasPowerStart || row.HasPowerEnd || row.HasSubSpellStart || row.HasSubSpellEnd);
        var body = history.Skip(start + 1).Take((end < 0 ? history.Count : end) - start - 1).ToList();
        Check(body.First(row => row.HasFullEntity).FullEntity.Entity == hero,
            label + " hero is first FULL in its task list");
        Check(body.Any(row => IsZone(row, oldHero, SabberStoneCore.Enums.Zone.SETASIDE)),
            label + " retires old hero in replacement task list");
    }

    private static void CheckReplacementBlocks(List<PowerHistoryData> history, string label)
    {
        int depth = 0;
        foreach (var row in history)
        {
            if (row.HasPowerStart) depth++;
            if (row.HasPowerEnd) Check(--depth >= 0, label + " no unmatched block end");
        }
        Check(depth == 0, label + " balanced blocks");
    }

    private static void LegacyJaraxxusNpcHistory(bool dump)
    {
        var (match, capture) = AnimationMatch(botTurn: true, deck: NpcCatalog.All[0].DeckId);
        var owner = match.Game.Player2;
        var source = Generic.DrawCard(owner, Cards.FromId("EX1_323"));
        int oldHero = owner.Hero.Id, oldPower = owner.Hero.HeroPower.Id;
        FlushFieldHistory(match);
        int cursor = capture.Packets.Count;
        var play = owner.Options().First(task => task.PlayerTaskType == PlayerTaskType.PLAY_CARD && task.Source?.Id == source.Id);
        match.ActNpc(new GoldenDecision(BotAction.From(play), 0, 1, 1, "Exhausted", null, null));
        var history = History(capture, cursor);
        Check(owner.Hero.Card.Id == "EX1_323h", "NPC legacy Jaraxxus replaces hero");
        CheckReplacementHeroFull(history, owner.Hero.Id, oldHero, owner.PlayerId, "NPC legacy Jaraxxus");
        CheckJaraxxusSpawnZones(history, owner.PlayerId);
        CheckReplacementBlocks(history, "NPC legacy Jaraxxus");
        if (dump) DumpHeroReplacement("NPC EX1_323", source.Id, oldHero, oldPower, history);
        capture.SerializeAll();
        Console.WriteLine("PASS hero replacement NPC legacy Jaraxxus: FULL snapshots and balanced blocks");
    }

    private static void HeroReplacementWithWeaponHistory(bool dump)
    {
        foreach (bool npc in new[] { false, true })
        foreach (string cardId in new[] { "CORE_EX1_323", "EX1_323", "ICC_834" })
        {
            var (match, capture) = AnimationMatch(botTurn: npc, deck: NpcCatalog.All[0].DeckId);
            var owner = match.Game.CurrentPlayer;
            var axe = Generic.DrawCard(owner, Cards.FromId("CS2_106"));
            InvokeProcess(match, owner.Options().Single(task => task.PlayerTaskType == PlayerTaskType.PLAY_CARD && task.Source?.Id == axe.Id));
            Check(owner.Hero.Weapon == axe, "fixture equips Fiery War Axe through the engine");
            owner.UsedMana = 0;
            var source = Generic.DrawCard(owner, Cards.FromId(cardId));
            int oldHero = owner.Hero.Id, oldPower = owner.Hero.HeroPower.Id;
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            if (npc)
            {
                var task = owner.Options().Single(option => option.PlayerTaskType == PlayerTaskType.PLAY_CARD && option.Source?.Id == source.Id);
                match.ActNpc(new GoldenDecision(BotAction.From(task), 0, 1, 1, "Exhausted", null, null));
            }
            else
            {
                typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
                var options = capture.Last<AllOptions>(14);
                int play = options.Options.FindIndex(option => option.MainOption?.Id == source.Id && option.MainOption.PowerKeyword == 0);
                Check(play >= 0, cardId + " native play option with weapon equipped");
                match.SendOption(options.Id, play, 0, -1, source.Card.Type == CardType.MINION ? 1 : 0);
            }
            var history = History(capture, cursor);
            string label = cardId + " with equipped weapon, seat " + owner.PlayerId;
            var weapon = owner.Hero.Weapon;
            var full = history.Single(row => row.HasFullEntity && row.FullEntity.Entity == weapon.Id);
            Check(SnapshotTag(full, GameTag.ZONE) == (int)SabberStoneCore.Enums.Zone.PLAY
                && SnapshotTag(full, GameTag.CONTROLLER) == owner.PlayerId, label + " weapon FULL owns PLAY and CONTROLLER");
            Check(owner.GraveyardZone.Contains(axe) && history.Any(row => IsZone(row, axe.Id, SabberStoneCore.Enums.Zone.GRAVEYARD)),
                label + " retires old weapon");
            Check(owner.Choice == null && !match.Game.Resolution.HasSuspendedEffect, label + " completes resolution");
            if (cardId != "ICC_834")
            {
                int power = history.FindIndex(row => row.HasPowerStart && row.PowerStart.Type == HistoryBlock.Type.POWER && row.PowerStart.Source == source.Id);
                Check(power >= 0, label + " opens weapon POWER");
                CheckJaraxxusSpawnZones(history, owner.PlayerId);
                CheckJaraxxusTaskList(history, power, label);
                Check(weapon.Card.Id == "EX1_323w" && owner.Hero.HeroPower.Card.Id == "EX1_tk33", label + " equips Blood Fury and INFERNO!");
                if (cardId == "EX1_323") CheckReplacementHeroFull(history, owner.Hero.Id, oldHero, owner.PlayerId, label);
            }
            else Check(weapon.Card.Id == "ICC_834w", label + " equips Shadowmourne");
            CheckReplacementBlocks(history, label);
            if (dump) DumpHeroReplacement(label, source.Id, oldHero, oldPower, history);
            capture.SerializeAll();
            Console.WriteLine("PASS hero replacement " + label + ": spawn zones, POWER tasks, old weapon retirement and balanced blocks");
        }
    }

    private static void MajordomoReplacementHistory(bool dump)
    {
        foreach (bool npcOwner in new[] { false, true })
        foreach (string scenario in new[] { "owner Hellfire", "opponent Hellfire", "combat", "silenced" })
        {
            bool ownerActs = scenario == "owner Hellfire";
            var (match, capture) = AnimationMatch(botTurn: ownerActs ? npcOwner : !npcOwner);
            var game = match.Game;
            var owner = npcOwner ? game.Player2 : game.Player1;
            var actor = game.CurrentPlayer;
            foreach (var player in new[] { game.Player1, game.Player2 })
                foreach (var held in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(held));
            var source = (SabberStoneCore.Model.Entities.Minion)SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("BRM_027"), zone: owner.BoardZone);
            int oldHero = owner.Hero.Id, oldPower = owner.Hero.HeroPower.Id;
            if (scenario == "silenced") source.Silence();
            source.Damage = source.Health - 3;
            SabberStoneCore.Model.Entities.IPlayable action;
            if (scenario == "combat")
            {
                action = SabberStoneCore.Model.Entities.Entity.FromCard(actor, Cards.FromId("CS2_200"), zone: actor.BoardZone);
                ((SabberStoneCore.Model.Entities.Minion)action).IsExhausted = false;
            }
            else action = Generic.DrawCard(actor, Cards.FromId("CS2_062"));
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            var task = actor.Options().First(option => option.Source?.Id == action.Id
                && (scenario == "combat" ? option.PlayerTaskType == PlayerTaskType.MINION_ATTACK && option.Target?.Id == source.Id
                    : option.PlayerTaskType == PlayerTaskType.PLAY_CARD));
            InvokeProcess(match, task);
            FlushFieldHistory(match);
            var history = History(capture, cursor);
            string label = "Majordomo " + (npcOwner ? "NPC " : "human ") + scenario;
            Check(owner.GraveyardZone.Contains(source), label + " source remains dead in graveyard");
            if (scenario == "silenced")
            {
                Check(owner.Hero.Id == oldHero && !history.Any(row => row.HasFullEntity && row.FullEntity.Name == "BRM_027h"), label + " does not replace hero");
            }
            else
            {
                Check(owner.Hero.Card.Id == "BRM_027h", label + " Deathrattle replaces hero");
                CheckReplacementHeroFull(history, owner.Hero.Id, oldHero, owner.PlayerId, label);
            }
            CheckReplacementBlocks(history, label);
            if (dump) DumpHeroReplacement(label, source.Id, oldHero, oldPower, history);
            capture.SerializeAll();
            Console.WriteLine("PASS hero replacement " + label + ": lifecycle, first FULL and balanced blocks");
        }
    }

    private static void OrdinaryWeaponSpawnZones()
    {
        foreach (bool npc in new[] { false, true })
        foreach (string card in new[] { "CS2_106", "EX1_398" })
        {
            var (match, capture) = AnimationMatch(botTurn: npc);
            var owner = match.Game.CurrentPlayer;
            var source = SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId(card), zone: owner.HandZone);
            FlushFieldHistory(match);
            var task = owner.Options().First(option => option.PlayerTaskType == PlayerTaskType.PLAY_CARD && option.Source?.Id == source.Id);
            InvokeProcess(match, task);
            FlushFieldHistory(match);
            var history = History(capture, 0);
            var weapon = owner.Hero.Weapon;
            var raw = match.Game.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistoryFullEntity>().Last(row => row.Entity.Id == weapon.Id);
            var full = history.Single(row => row.HasFullEntity && row.FullEntity.Entity == weapon.Id);
            Check(SnapshotTag(full, GameTag.ZONE) == raw.Entity.Tags[GameTag.ZONE], card + " ordinary weapon FULL zone unchanged, seat " + owner.PlayerId);
            if (card == "EX1_398") Check(SnapshotTag(full, GameTag.ZONE) == 0, "ordinary Battlecry weapon retains INVALID spawn zone");
            capture.SerializeAll();
            Console.WriteLine("PASS ordinary weapon FULL unchanged: " + card + " seat " + owner.PlayerId);
        }
    }

    private static void HeroReplacementSpawnZoneScopes()
    {
        var (match, capture) = AnimationMatch();
        var opening = capture.HistorySnapshots().First().List;
        foreach (var seat in new[] { match.Game.Player1, match.Game.Player2 })
        {
            var engine = match.Game.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistoryFullEntity>()
                .Single(row => row.Entity.Id == seat.Hero.Id);
            var full = opening.Single(row => row.HasFullEntity && row.FullEntity.Entity == seat.Hero.Id);
            Check(engine.Entity.Tags.TryGetValue(GameTag.ZONE, out int zone) && zone == 0
                && full.FullEntity.Tags.Single(tag => tag.Name == (int)GameTag.ZONE).Value == zone,
                "first history packet preserves starting hero FULL ZONE for seat " + seat.PlayerId);
        }
        Console.WriteLine("PASS starting hero FULL zones: both seats match engine in first history packet");
        var method = typeof(NativeMatch).GetMethod("FoldHeroReplacementSpawnZone", BindingFlags.Instance | BindingFlags.NonPublic);
        var convert = typeof(NativeMatch).GetMethod("ToNativeHistory", BindingFlags.Instance | BindingFlags.NonPublic);
        SabberStoneCore.Kettle.IPowerHistoryEntry Start() => new SabberStoneCore.Kettle.PowerHistoryBlockStart { BlockType = BlockType.POWER };
        SabberStoneCore.Kettle.IPowerHistoryEntry End() => new SabberStoneCore.Kettle.PowerHistoryBlockEnd();
        SabberStoneCore.Kettle.IPowerHistoryEntry Hero(int player) => new SabberStoneCore.Kettle.PowerHistoryTagChange {
            EntityId = player, Tag = GameTag.HERO_ENTITY, Value = 70 };
        void Case(string label, int type, SabberStoneCore.Kettle.IPowerHistoryEntry[] prefix, bool expected,
            int zone = 0, int destination = 1, int moving = 72, bool continuation = false,
            SabberStoneCore.Kettle.IPowerHistoryEntry[] between = null)
        {
            int depth = 0;
            foreach (var entry in prefix)
            {
                convert.Invoke(match, new object[] { entry });
                if (entry is SabberStoneCore.Kettle.PowerHistoryBlockStart) depth++;
                if (entry is SabberStoneCore.Kettle.PowerHistoryBlockEnd) depth--;
            }
            var row = new PowerHistoryData { FullEntity = new PegasusGame.PowerHistoryEntity { Entity = 72, Name = "arbitrary",
                Tags = new List<PegasusGame.Tag> { new() { Name = (int)GameTag.CARDTYPE, Value = type },
                    new() { Name = (int)GameTag.ZONE, Value = zone }, new() { Name = (int)GameTag.CONTROLLER, Value = match.Game.Player1.PlayerId } } } };
            var entries = prefix.Concat(new SabberStoneCore.Kettle.IPowerHistoryEntry[] { new SabberStoneCore.Kettle.PowerHistoryFullEntity() })
                .Concat(between ?? Array.Empty<SabberStoneCore.Kettle.IPowerHistoryEntry>())
                .Concat(new SabberStoneCore.Kettle.IPowerHistoryEntry[] { new SabberStoneCore.Kettle.PowerHistoryTagChange {
                    EntityId = moving, Tag = GameTag.ZONE, Value = destination } }).ToList();
            if (continuation) entries.RemoveRange(0, prefix.Length);
            bool folded = (bool)method.Invoke(match, new object[] { row, entries, continuation ? 0 : prefix.Length });
            for (int at = 0; at < depth; at++) convert.Invoke(match, new object[] { End() });
            Check(folded == expected && SnapshotTag(row, GameTag.ZONE) == (expected ? 1 : zone), "spawn-zone scope " + label);
        }
        int owner = match.Game.Player1.Id, opponent = match.Game.Player2.Id;
        Case("hero independent of card id", 3, new[] { Start() }, true);
        Case("hero outside any block", 3, Array.Empty<SabberStoneCore.Kettle.IPowerHistoryEntry>(), false);
        Case("weapon in the same block as the swap", 7, new[] { Start(), Hero(owner) }, true);
        Case("weapon in nested POWER", 7, new[] { Start(), Hero(owner), Start() }, true);
        var unrelated = new SabberStoneCore.Kettle.PowerHistoryTagChange { EntityId = 73, Tag = GameTag.ZONE, Value = 4 };
        var sameEntity = new SabberStoneCore.Kettle.PowerHistoryTagChange { EntityId = 72, Tag = GameTag.ATK, Value = 3 };
        Case("weapon skips unrelated retirement row", 7, new[] { Start(), Hero(owner) }, true, between: new[] { unrelated });
        Case("weapon's first reference must be ZONE PLAY", 7, new[] { Start(), Hero(owner) }, false, between: new[] { sameEntity });
        Case("weapon cannot cross block end", 7, new[] { Start(), Hero(owner) }, false, between: new[] { End() });
        Case("weapon cannot cross block start", 7, new[] { Start(), Hero(owner) }, false, between: new[] { Start() });
        Case("weapon cannot cross metadata naming it", 7, new[] { Start(), Hero(owner) }, false,
            between: new[] { new SabberStoneCore.Kettle.PowerHistoryMetaData { Info = new List<int> { 72 } } });
        Case("weapon cannot cross snapshot naming it", 7, new[] { Start(), Hero(owner) }, false,
            between: new[] { new SabberStoneCore.Kettle.PowerHistoryShowEntity { Entity = new SabberStoneCore.Kettle.PowerHistoryEntity { Id = 72 } } });
        Case("hero still requires adjacent move", 3, new[] { Start() }, false, between: new[] { unrelated });
        Case("weapon after completed sibling", 7, new[] { Start(), Start(), Hero(owner), End(), Start() }, false);
        Case("weapon in a sibling block after the swap block ended, same outer block", 7,
            new[] { Start(), Start(), Hero(owner), End(), Start(), Start() }, false);
        Case("weapon after descendant ends, swap block still open", 7, new[] { Start(), Hero(owner), Start(), End() }, true);
        Case("replacement in earlier packet", 7, new[] { Start(), Hero(owner), Start() }, true, continuation: true);
        Case("completed swap block in earlier packet", 7, new[] { Start(), Start(), Hero(owner), End(), Start() }, false, continuation: true);
        Case("ordinary weapon", 7, new[] { Start() }, false);
        Case("other controller replacement", 7, new[] { Start(), Hero(opponent) }, false);
        Case("previous completed outer block", 7, new[] { Start(), Hero(owner), End(), Start() }, false);
        Case("no open block", 7, new[] { Start(), Hero(owner), End() }, false);
        Case("already in PLAY", 3, new[] { Start() }, false, zone: 1);
        Case("not moving to PLAY", 3, new[] { Start() }, false, destination: 6);
        Case("different entity moves", 3, new[] { Start() }, false, moving: 73);
        Case("minion", 4, new[] { Start(), Hero(owner) }, false);
        Console.WriteLine("PASS hero replacement spawn-zone scopes: open hero block, swap subtree across packets, controller and adjacent move guards");
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
