using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class WildFxHistoryProof
{
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var proof in new (string Name, Action Run)[] {
            ("Inspire keyword", Inspire), ("Seeker self target", Seeker),
            ("Coliseum survivor targets", Coliseum), ("Confuse targets", Confuse),
            ("unimplemented hero power rejection", HeroPowerRegistration) })
        {
            try { proof.Run(); Console.WriteLine("PASS " + proof.Name); }
            catch (Exception error) { failures.Add(proof.Name + ": " + error.Message); Console.WriteLine("FAIL " + failures[^1]); }
        }
        Check(failures.Count == 0, string.Join("; ", failures));
    }

    private static Game Fixture()
    {
        var game = new Game(new GameConfig { RandomSeed = 9, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            Player1HeroClass = CardClass.HUNTER, Player2HeroClass = CardClass.MAGE, History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), FormatType = FormatType.FT_WILD });
        game.StartGame();
        game.Player1.BaseMana = 10;
        return game;
    }

    private static Minion Summon(Controller owner, string id) => (Minion)Entity.FromCard(owner, Cards.FromId(id), zone: owner.BoardZone);

    private static IPlayable Play(Game game, string id)
    {
        var card = Entity.FromCard(game.Player1, Cards.FromId(id), zone: game.Player1.HandZone);
        Check(game.Process(PlayCardTask.Any(game.Player1, card)), "play " + id);
        return card;
    }

    private static List<IPowerHistoryEntry> Power(Game game, int source)
    {
        var result = new List<IPowerHistoryEntry>();
        int depth = 0;
        foreach (var entry in game.PowerHistory.Full)
        {
            if (depth == 0 && !(entry is PowerHistoryBlockStart start && start.BlockType == BlockType.POWER && start.Source == source)) continue;
            result.Add(entry);
            if (entry is PowerHistoryBlockStart) depth++;
            if (entry is PowerHistoryBlockEnd && --depth == 0) return result;
        }
        throw new Exception("missing complete POWER block for " + source);
    }

    private static void TargetsBeforeEffects(Game game, int source, params int[] targets)
    {
        var power = Power(game, source);
        var ranges = power.OfType<PowerHistoryMetaData>().Where(meta => meta.Type == MetaDataType.TARGET).ToArray();
        Check(ranges.Length == 1 && ranges[0].Info.SequenceEqual(targets), "expected one TARGET range [" + string.Join(",", targets) + "]");
        Check(power.IndexOf(ranges[0]) < power.FindIndex(entry => entry is PowerHistoryFullEntity || entry is PowerHistoryTagChange),
            "TARGET must precede enchantment/stat/destroy changes");
    }

    private static void Inspire()
    {
        Check(!Cards.AllStandard.Any(card => card[GameTag.INSPIRE] != 0), "no Standard card has INSPIRE");
        var game = Fixture(); var squire = Summon(game.Player1, "AT_082");
        Check(game.Process(HeroPowerTask.Any(game.Player1)), "use hero power");
        var trigger = game.PowerHistory.Full.OfType<PowerHistoryBlockStart>()
            .Single(entry => entry.BlockType == BlockType.TRIGGER && entry.Source == squire.Id);
        Check(trigger.TriggerKeyword == 403, "AT_082 TRIGGER keyword expected 403, got " + trigger.TriggerKeyword);
        Check(squire.AttackDamage == 2, "Inspire still buffs Attack");
        Check(!game.PowerHistory.Full.OfType<PowerHistoryBlockStart>().Any(entry => entry.Source != squire.Id && entry.TriggerKeyword == 403),
            "unrelated blocks have no INSPIRE keyword");
    }

    private static void Seeker()
    {
        var game = Fixture();
        for (int i = 0; i < 6; i++) Summon(game.Player1, "CS2_182");
        var seeker = (Minion)Play(game, "LOE_116");
        TargetsBeforeEffects(game, seeker.Id, seeker.Id);
        Check(seeker.AttackDamage == 5 && seeker.Health == 5, "Seeker still gains +4/+4");
        var empty = Fixture(); var unbuffed = (Minion)Play(empty, "LOE_116");
        Check(!Power(empty, unbuffed.Id).OfType<PowerHistoryMetaData>().Any(meta => meta.Type == MetaDataType.TARGET), "failed condition has no TARGET");
    }

    private static void Coliseum()
    {
        var game = Fixture(); var keep = Summon(game.Player1, "CS2_182"); var dead = Summon(game.Player1, "CS2_120");
        var enemyKeep = Summon(game.Player2, "CS2_182"); var enemyDead = Summon(game.Player2, "CS2_120");
        var spell = Play(game, "AT_078");
        TargetsBeforeEffects(game, spell.Id, keep.Id, dead.Id, enemyKeep.Id, enemyDead.Id);
        Check(keep.Zone.Type == Zone.PLAY && enemyKeep.Zone.Type == Zone.PLAY && dead.Zone.Type == Zone.GRAVEYARD && enemyDead.Zone.Type == Zone.GRAVEYARD,
            "Coliseum keeps highest Attack minion on each side");
        var empty = Fixture(); var fizzled = Play(empty, "AT_078");
        Check(!Power(empty, fizzled.Id).OfType<PowerHistoryMetaData>().Any(meta => meta.Type == MetaDataType.TARGET), "empty board has no TARGET");
    }

    private static void Confuse()
    {
        var game = Fixture(); var friendly = Summon(game.Player1, "CS2_182"); var enemy = Summon(game.Player2, "CS2_120");
        var spell = Play(game, "AT_016");
        TargetsBeforeEffects(game, spell.Id, friendly.Id, enemy.Id);
        Check(friendly.AttackDamage == 5 && friendly.Health == 4 && enemy.AttackDamage == 3 && enemy.Health == 2, "Confuse still swaps both sides");
        var empty = Fixture(); var fizzled = Play(empty, "AT_016");
        Check(!Power(empty, fizzled.Id).OfType<PowerHistoryMetaData>().Any(meta => meta.Type == MetaDataType.TARGET), "empty board has no TARGET");
    }

    private static void HeroPowerRegistration()
    {
        foreach (string id in new[] { "AT_132_DRUIDa", "AT_132_HUNTER_H1", "AT_132_ROGUE_H1", "AT_132_WARLOCKa" })
        {
            try { DebugScenario.Parse("humanHeroPower=" + id); }
            catch (ArgumentException error)
            {
                Check(error.Message == "humanHeroPower: " + id + " has no engine behaviour registration.", "rejection message: " + error.Message);
                continue;
            }
            throw new Exception("accepted unimplemented active hero power " + id);
        }
        Check(DebugScenario.Parse("humanHeroPower=AT_132_DRUID").HumanHeroPower == "AT_132_DRUID", "implemented upgraded power accepted");
        Check(DebugScenario.Parse("humanHeroPower=EDR_851p").HumanHeroPower == "EDR_851p", "implemented passive power accepted");
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
