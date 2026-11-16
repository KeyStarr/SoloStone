using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Config;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

// Retail publishes all of one activation's summons in its own block, with no SHOW_ENTITY for a minion created straight into
// play, so the client spawns them together. Summons queued by EnqueueTask / EnqueueNumberTask used to get one sibling POWER
// block each, and each created minion a SHOW_ENTITY; either made the client play one spawn at a time.
// A trigger of a spawn (Knife Juggler) stays nested between the spawns, as in retail.
internal static class QueuedSummonHistoryProof
{
    [GroupProof("queued-summon-history")]
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var (name, run) in new (string, Action)[] { ("EX1_538", Hounds), ("CORE_GVG_061", Muster), ("FP1_002", Creeper) })
        {
            try { run(); Console.WriteLine("QUEUED-SUMMON-HISTORY " + name + " PASS"); }
            catch (Exception error) { failures.Add(name + ": " + error.Message); Console.WriteLine("QUEUED-SUMMON-HISTORY " + name + " FAIL " + error.Message); }
        }
        Core2026Proof.Check(failures.Count == 0, string.Join("\n", failures));
    }

    private static Game NewGame()
    {
        var game = new Game(new GameConfig { RandomSeed = 42, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, FormatType = FormatType.FT_WILD,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        foreach (var player in new[] { game.Player1, game.Player2 })
            foreach (var card in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(card));
        return game;
    }

    private static void Hounds()
    {
        var g = NewGame(); var p = g.Player1;
        var juggler = Summon(p, "NEW1_019");
        for (int i = 0; i < 3; i++) Summon(p.Opponent, "CS2_182").BaseHealth = 100;
        int cursor = g.PowerHistory.Full.Count;
        var spell = Play(g, p, "EX1_538");
        var shape = Shape(g, cursor);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "EX1_538t") == 3, "three Hounds summoned");
        OneBlock(shape, spell.Id, BlockType.POWER, "EX1_538t", 3);
        var power = shape.Blocks.Single(b => b.Source == spell.Id && b.BlockType == BlockType.POWER);
        var throws = shape.Blocks.Where(b => b.Source == juggler.Id && b.BlockType == BlockType.TRIGGER).ToList();
        Core2026Proof.Check(throws.Count == 3 && throws.All(b => ReferenceEquals(shape.Parent[b], power)), "each Knife Juggler throw nests inside the spell's block");
    }

    private static void Muster()
    {
        var g = NewGame(); var p = g.Player1;
        int cursor = g.PowerHistory.Full.Count;
        var spell = Play(g, p, "CORE_GVG_061");
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "CS2_101t") == 3 && p.Hero.Weapon != null, "three Recruits and a weapon");
        OneBlock(Shape(g, cursor), spell.Id, BlockType.POWER, "CS2_101t", 3);
    }

    private static void Creeper()
    {
        var g = NewGame(); var p = g.Player1;
        var creeper = Summon(p, "FP1_002");
        int cursor = g.PowerHistory.Full.Count;
        Kill(g, creeper);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "FP1_002t") == 2, "two Spectral Spiders summoned");
        OneBlock(Shape(g, cursor), creeper.Id, BlockType.TRIGGER, "FP1_002t", 2);
    }

    private static void OneBlock(HistoryShape shape, int source, BlockType type, string summoned, int count)
    {
        var own = shape.Blocks.Where(b => b.Source == source && b.BlockType != BlockType.PLAY).ToList();
        Core2026Proof.Check(own.Count == 1 && own[0].BlockType == type, $"one {type} block from the source, found " + string.Join(",", own.Select(b => b.BlockType)));
        var spawns = shape.Creations.Where(c => c.Card == summoned).ToList();
        Core2026Proof.Check(spawns.Count == count && spawns.All(c => ReferenceEquals(c.Block, own[0])), $"all {count} {summoned} created directly in that block");
        // A SHOW_ENTITY of a minion its FULL_ENTITY already put in play reads to the client as a reveal the spawn waits for.
        Core2026Proof.Check(!shape.Shown.Overlaps(spawns.Select(c => c.Id)), $"no SHOW_ENTITY for a created {summoned}");
    }

    private sealed class HistoryShape
    {
        internal readonly List<PowerHistoryBlockStart> Blocks = new();
        internal readonly Dictionary<PowerHistoryBlockStart, PowerHistoryBlockStart> Parent = new();
        internal readonly List<(int Id, string Card, PowerHistoryBlockStart Block)> Creations = new();
        internal readonly HashSet<int> Shown = new();
    }

    private static HistoryShape Shape(Game game, int cursor)
    {
        var shape = new HistoryShape();
        var open = new Stack<PowerHistoryBlockStart>();
        foreach (var entry in game.PowerHistory.Full.Skip(cursor))
        {
            if (entry is PowerHistoryBlockStart start)
            {
                shape.Blocks.Add(start);
                shape.Parent[start] = open.Count > 0 ? open.Peek() : null;
                open.Push(start);
            }
            else if (entry is PowerHistoryBlockEnd) open.Pop();
            else if (entry is PowerHistoryFullEntity full && open.Count > 0)
                shape.Creations.Add((full.Entity.Id, game.IdEntityDic[full.Entity.Id].Card.Id, open.Peek()));
            else if (entry is PowerHistoryShowEntity show) shape.Shown.Add(show.Entity.Id);
        }
        Core2026Proof.Check(open.Count == 0, "every block closed");
        return shape;
    }
}
