using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class SmolderingTimerProof
{
    [GroupProof("smoldering-cards", ProofKind.Cards)]
    internal static void CardScenarios()
    {
        SmolderingStrengthProof.Run();
        SmolderingGroveProof.Run();
        SmolderingAscentProof.Run();
    }

    [GroupProof("smoldering-timers")]
    internal static void Run()
    {
        foreach (string id in new[] { "FIR_911", "FIR_914", "FIR_916" })
        {
            foreach (int first in new[] { 1, 2 })
            foreach (bool replace in new[] { false, true })
            foreach (int seat in new[] { 1, 2 }) Opening(id, first, seat, replace);
            DrawAndReenter(id, false);
            DrawAndReenter(id, true);
        }
        Console.WriteLine("PASS Smoldering timers: all three cards; both opening seats/first-player assignments, keep/replace, Coin, mid-game draw, own/enemy reentry, cast/display/expiry, clone/snapshot");
    }

    private static void Display(IPlayable card, int magnitude, string where)
    {
        Core2026Proof.Check(card.Zone == card.Controller.HandZone
            && card[GameTag.TAG_SCRIPT_DATA_NUM_1] == magnitude
            && card[GameTag.TAG_SCRIPT_DATA_NUM_2] == 4 - magnitude,
            $"{card.Card.Id} {where}: expected magnitude/left {magnitude}/{4 - magnitude}, got {card[GameTag.TAG_SCRIPT_DATA_NUM_1]}/{card[GameTag.TAG_SCRIPT_DATA_NUM_2]}");
    }

    private static void Cast(Game g, IPlayable card, int magnitude)
    {
        // Resolution must use the closed residence, not a possibly stale display counter.
        var branch = g.CloneExact(history: true);
        SmolderingStrengthProof.Resolve(branch, branch.IdEntityDic[card.Id].Controller,
            (IPlayable)branch.IdEntityDic[card.Id], magnitude);
    }

    private static void Lifetime(Game g, IPlayable card, string where)
    {
        var p = card.Controller;
        for (int magnitude = 1; magnitude <= 3; magnitude++)
        {
            Display(card, magnitude, where + " owner turn " + magnitude);
            Cast(g, card, magnitude);
            End(g);
            if (magnitude == 3)
                Core2026Proof.Check(card.Zone == p.GraveyardZone && p.DiscardedEntities.Contains(card.Id), where + " discards at third owner end");
            else Display(card, magnitude + 1, where + " after owner end");
            End(g);
            if (magnitude < 3) Display(card, magnitude + 1, where + " after enemy end");
        }
    }

    private static void Opening(string id, int first, int seat, bool replace)
    {
        // Enough copies at the top to exercise both kept and replacement opening cards through real mulligans.
        var deck = Enumerable.Repeat(Cards.FromId(id), 30).ToList();
        var g = new Game(new GameConfig { RandomSeed = 73, StartPlayer = first, Shuffle = false,
            SkipMulligan = false, History = true, Logging = false, FormatType = FormatType.FT_WILD,
            Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE,
            Player1Deck = deck.ToList(), Player2Deck = deck.ToList() });
        g.StartGame();
        foreach (var owner in new[] { g.Player1, g.Player2 })
            Core2026Proof.Check(g.Process(ChooseTask.Mulligan(owner, replace ? new List<int>() : owner.Choice.Choices.ToList())), "Smoldering mulligan accepted");
        g.MainBegin();
        var p = seat == 1 ? g.Player1 : g.Player2;
        var card = p.HandZone.First(c => c.Card.Id == id);
        Core2026Proof.Check(p.HandZone.Any(c => c.Card.Id == "GAME_005") == (p != g.FirstPlayer), "Coin belongs to second player");
        foreach (var owner in new[] { g.Player1, g.Player2 })
            foreach (var other in owner.HandZone.Where(c => c.Id != card.Id && c.Card.Id != "GAME_005").ToArray())
                owner.SetasideZone.Add(owner.HandZone.Remove(other));
        string where = $"opening first={first} seat={seat} replace={replace}";
        Display(card, 1, where + " before first owner turn");
        if (g.CurrentPlayer != p) { End(g); Display(card, 1, where + " enemy opening end"); }
        foreach (var branch in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
            Lifetime(branch, (IPlayable)branch.IdEntityDic[card.Id], where);
    }

    private static void DrawAndReenter(string id, bool enemyEntry)
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        End(g); End(g); // A previous owner start exists before this hand residence.
        if (enemyEntry) End(g);
        var card = Entity.FromCard(p, Cards.FromId(id)); p.DeckZone.Add(card);
        Generic.Draw(p, card);
        g.AuraUpdate();
        Display(card, 1, "mid-game draw");
        if (enemyEntry) { End(g); Display(card, 1, "draw on enemy turn does not age at enemy end"); }
        End(g); Display(card, 2, "draw upgrades at first owner end"); End(g);
        p.SetasideZone.Add(p.HandZone.Remove(card));
        if (enemyEntry) End(g);
        p.HandZone.Add(p.SetasideZone.Remove(card)); g.AuraUpdate();
        Display(card, 1, "reentry resets previous upgrade");
        if (enemyEntry) { End(g); Display(card, 1, "reentry on enemy turn has no retroactive owner end"); }
        Lifetime(g, card, "reentry enemy=" + enemyEntry);
    }
}
