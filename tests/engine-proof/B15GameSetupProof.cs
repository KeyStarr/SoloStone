using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B15GameSetupProof
{
    [GroupProof("b15-game-setup")]
    internal static void Run()
    {
        var g = new Game(new GameConfig { StartPlayer = 1, RandomSeed = 17, FillDecks = false,
            Shuffle = false, SkipMulligan = true, History = true, Logging = false });
        g.StartGame();
        var p = g.Player1;
        for (int i = 0; i < 99; i++) Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        Core2026Proof.Check(p.DeckZone.Count == 99 && p.DeckZone.IsFull, "retail deck grows to 99");
        foreach (var next in new[] {g.CloneExact(history:true), Game.LoadSnapshot(g.SaveSnapshot())})
            Core2026Proof.Check(next.Player1.DeckZone.Count == 99 && next.Player1.DeckZone.IsFull, "99-cap clone/snapshot");
        LegacyOpening(); Setup(); B15GameSetupExtraProof.Run();
        Console.WriteLine("PASS B15 setup capacity and pre-mulligan");
    }
    private static void LegacyOpening()
    {
        foreach (bool history in new[] {false, true})
        {
            var g = new Game(new GameConfig { StartPlayer = 1, RandomSeed = 17, Shuffle = false,
                SkipMulligan = false, History = history, Logging = false, FillDecks = true,
                FillDecksPredictably = true, Player1HeroClass = CardClass.MAGE,
                Player1Deck = new() { Cards.FromId("KAR_096") } });
            g.StartGame();
            Core2026Proof.Check(g.Player1.DeckZone.Count == 27, "legacy Malchezaar remains queued until after mulligan");
        }
    }
    internal static Game Opening(string[] first, string[] second = null, int seed = 7)
    {
        var g = new Game(new GameConfig { StartPlayer=1, RandomSeed=seed, Shuffle=false, SkipMulligan=false,
            History=true, Logging=false, Player1HeroClass=CardClass.DRUID,
            Player1Deck=first.Select(Cards.FromId).ToList(),
            Player2Deck=(second??Enumerable.Repeat("CS2_182",12).ToArray()).Select(Cards.FromId).ToList() });
        g.StartGame(); return g;
    }
    /// <summary>Both players keep, then the host's post-mulligan join resolves Start of Game.</summary>
    internal static Game Kept(Game g)
    {
        foreach (var p in new[]{g.Player1,g.Player2})
            Core2026Proof.Check(g.Process(SabberStoneCore.Tasks.PlayerTasks.ChooseTask.Mulligan(p,p.Choice.Choices.ToList())),"keep opening");
        g.MainBegin(); return g;
    }
    internal static Game Kept(string[] first, string[] second = null, int seed = 7) => Kept(Opening(first, second, seed));
    /// <summary>Both players keep; Start of Game resolves without entering turn one (no turn draw).</summary>
    internal static Game Resolved(string[] first, int seed = 7)
    {
        var g = Opening(first, null, seed);
        foreach (var p in new[]{g.Player1,g.Player2})
            Core2026Proof.Check(g.Process(SabberStoneCore.Tasks.PlayerTasks.ChooseTask.Mulligan(p,p.Choice.Choices.ToList())),"keep opening");
        SabberStoneCore.Actions.StartOfGameRules.Resolve(g); return g;
    }
    private static void Setup()
    {
        var g=Opening(new[]{"EDR_845","CS2_029","EDR_000","JAIL_384","CS2_182","CS2_182","CS2_182"});
        // Retail lands Start of Game gameplay after both mulligans (start-of-game/sog-timing-generic.yaml).
        Core2026Proof.Check(g.Player1[GameTag.MAXRESOURCES]!=15 && g.Player2[GameTag.MAXRESOURCES]!=15,"Ysera leaves mana caps alone during mulligan");
        Core2026Proof.Check(g.Player1.DeckZone.Count(e=>e.Card.Id=="EDR_000")==0,"Hogger copies are not mulligan replacements");
        Core2026Proof.Check(g.Player1.Choice!=null && g.Player2.Choice!=null,"both mulligans remain answerable");
        Kept(g);
        Core2026Proof.Check(g.Player1[GameTag.IMBUE]==0,"Hamuul sees non-Nature original spell even if drawn");
        Core2026Proof.Check(g.Player1[GameTag.MAXRESOURCES]==15 && g.Player2[GameTag.MAXRESOURCES]==15,"Ysera raises both mana caps after mulligan");
        Core2026Proof.Check(g.Player1.DeckZone.Concat(g.Player1.HandZone).Count(e=>e.Card.Id=="EDR_000")==2,"Hogger duplicates kept Legendary without moving original");
    }
}
