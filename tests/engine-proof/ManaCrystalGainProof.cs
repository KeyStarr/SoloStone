using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Issue #178, the mana-cap rule decided 2026-11-19 (hearthstone.wiki.gg Nourish note: played at 10 Mana, you are left with
// 7): a FILLED gain of N raises available Mana by N in total. Max rises by min(N, cap - max); the part over the cap refills
// spent crystals. It never unlocks Overload and never makes temporary Mana. An EMPTY gain raises max only.
internal static class ManaCrystalGainProof
{
    private static void Verify(bool condition, string message) => Core2026Proof.Check(condition, message);

    [GroupProof("mana-crystal-gain")]
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var test in new Action[] { FilledAtCap, FilledOverflowBelowCap, FilledExactlyToCap, OverflowNeverExceedsSpent,
            OverloadStaysLocked, NoTemporaryMana, RaisedCap, EmptyGainUnchanged, BiologyProjectBothPlayers, Nourish,
            YseraEndToEnd, AstralCommunionOnRaisedCap, AstralCommunionExcessManaOnlyAtCap })
            try { test(); } catch (Exception e) { failures.Add(test.Method.Name + ": " + e.Message); }
        Verify(failures.Count == 0, string.Join("; ", failures));
        Console.WriteLine("PASS mana crystal gain: filled gain refills spent crystals past the cap; Overload locked; no temporary Mana; empty gain unchanged; raised cap; Biology Project, Nourish, Ysera, Astral Communion");
    }

    private static (Game, Controller) Start(int max, int used, int cap = 10)
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        p[GameTag.MAXRESOURCES] = cap; p.BaseMana = max; p.UsedMana = used; p.TemporaryMana = 0; p.OverloadLocked = 0;
        return (g, p);
    }
    private static string State(Controller p) => $"max {p.BaseMana} used {p.UsedMana} locked {p.OverloadLocked} temp {p.TemporaryMana} available {p.RemainingMana}";
    private static void Expect(Controller p, int max, int used, string what)
        => Verify(p.BaseMana == max && p.UsedMana == used && p.TemporaryMana == 0, $"{what}: expected max {max} used {used}, got {State(p)}");

    private static void FilledAtCap()
    {
        var (_, p) = Start(10, 4);
        Generic.ChangeManaCrystal(p, 2, true);
        Expect(p, 10, 2, "10/10 with 4 spent, filled +2");
        Verify(p.RemainingMana == 8, "available 8, got " + p.RemainingMana);
    }
    private static void FilledOverflowBelowCap()
    {
        var (_, p) = Start(9, 3);
        Generic.ChangeManaCrystal(p, 2, true);
        Expect(p, 10, 2, "9 max with 3 spent (6 available), filled +2");
        Verify(p.RemainingMana == 8, "available 8, got " + p.RemainingMana);
    }
    private static void FilledExactlyToCap()
    {
        var (_, p) = Start(8, 3);
        Generic.ChangeManaCrystal(p, 2, true);
        Expect(p, 10, 3, "8 max, filled +2 reaches the cap with no overflow");
    }
    private static void OverflowNeverExceedsSpent()
    {
        var (_, p) = Start(10, 1);
        Generic.ChangeManaCrystal(p, 3, true);
        Expect(p, 10, 0, "overflow larger than the spent crystals stops at fully refilled");
        Verify(p.RemainingMana == 10, "available 10, got " + p.RemainingMana);
    }
    private static void OverloadStaysLocked()
    {
        var (_, p) = Start(10, 4);
        p.OverloadLocked = 3;
        Generic.ChangeManaCrystal(p, 2, true);
        Verify(p.OverloadLocked == 3 && p.UsedMana == 2 && p.RemainingMana == 5, "Overload stays locked, got " + State(p));
        var (_, q) = Start(10, 0);
        q.OverloadLocked = 3;
        Generic.ChangeManaCrystal(q, 2, true);
        Verify(q.OverloadLocked == 3 && q.UsedMana == 0 && q.RemainingMana == 7, "refill never reaches locked crystals, got " + State(q));
    }
    private static void NoTemporaryMana()
    {
        var (_, p) = Start(10, 0);
        Generic.ChangeManaCrystal(p, 2, true);
        Verify(p.TemporaryMana == 0 && p.RemainingMana == 10, "no spent crystal means no gain, no temporary Mana, got " + State(p));
        var (_, q) = Start(9, 0);
        Generic.ChangeManaCrystal(q, 2, true);
        Verify(q.TemporaryMana == 0 && q.RemainingMana == 10, "9/10 unspent: 10/10, got " + State(q));
    }
    private static void RaisedCap()
    {
        var (_, p) = Start(14, 5, cap: 15);
        Generic.ChangeManaCrystal(p, 3, true);
        Expect(p, 15, 3, "14/15 cap with 5 spent, filled +3 (report 3 in the issue: it gave +1)");
        Verify(p.RemainingMana == 12, "available 12 (9 + 3), got " + p.RemainingMana);
        var (_, q) = Start(15, 5, cap: 15);
        Generic.ChangeManaCrystal(q, 3, true);
        Expect(q, 15, 2, "15/15 with 5 spent, filled +3");
    }
    private static void EmptyGainUnchanged()
    {
        var (_, p) = Start(10, 4);
        Generic.ChangeManaCrystal(p, 1, false);
        Expect(p, 10, 4, "empty gain at the cap changes nothing");
        var (_, q) = Start(9, 2);
        Generic.ChangeManaCrystal(q, 2, false);
        Expect(q, 10, 3, "empty +2 from 9/10: one crystal, the rest dropped, the new crystal empty");
        var (_, r) = Start(5, 2);
        Generic.ChangeManaCrystal(r, 2, false);
        Expect(r, 7, 4, "empty gain below the cap adds empty crystals");
    }

    private static void Play(Game g, Controller p, string id, int chooseOne = 0)
    {
        var card = Add(p, id);
        Verify(g.Process(PlayCardTask.Any(p, card, chooseOne: chooseOne)), "play " + id);
    }
    private static void BiologyProjectBothPlayers()
    {
        int cost = Cards.FromId("BOT_054").Cost;
        var (g, p) = Start(10, 4 - cost);
        var o = p.Opponent; o.BaseMana = 10; o.UsedMana = 4; o.TemporaryMana = 0; o.OverloadLocked = 0;
        Play(g, p, "BOT_054"); // after the cost 4 are spent, then +2 filled
        Expect(p, 10, 2, "Biology Project at 10/10, player");
        Expect(o, 10, 2, "Biology Project at 10/10, opponent with 4 spent");
        var (g2, q) = Start(9, 4 - cost);
        Play(g2, q, "BOT_054");
        Expect(q, 10, 3, "Biology Project from 9/10 after 4 are spent");
    }
    private static void Nourish()
    {
        var (g, p) = Start(10, 0);
        Play(g, p, "EX1_164", chooseOne: 1); // cost 5, gain 2: the wiki note says 7 Mana left
        Verify(p.RemainingMana == 7 && p.BaseMana == 10, "Nourish at 10 Mana leaves 7, got " + State(p));
    }
    private static void YseraEndToEnd()
    {
        var deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 29).Append(Cards.FromId("EDR_000")).ToList();
        var g = new Game(new GameConfig { RandomSeed = 73, StartPlayer = 1, Shuffle = false, SkipMulligan = true, History = true,
            Logging = false, FormatType = FormatType.FT_WILD, Player1HeroClass = CardClass.DRUID, Player2HeroClass = CardClass.DRUID,
            Player1Deck = deck.ToList(), Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        g.StartGame();
        foreach (var owner in new[] { g.Player1, g.Player2 })
            Verify(owner[GameTag.MAXRESOURCES] == 15, "Start of Game raises both players' cap to 15, got " + owner[GameTag.MAXRESOURCES]);
        var p = g.Player1;
        foreach (var c in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(c));
        p.BaseMana = 14; p.UsedMana = 0; p.TemporaryMana = 0;
        int cost = Cards.FromId("EDR_000").Cost;
        Play(g, p, "EDR_000");
        // paid cost, then Gain 3 filled: max 15, available 14 - cost + 3
        Verify(p.BaseMana == 15 && p.RemainingMana == 14 - cost + 3 && p.TemporaryMana == 0,
            $"Ysera at 14/15 gives 15 max and +3 available, got {State(p)}");
        var q = g.Player2;
        Verify(q[GameTag.MAXRESOURCES] == 15, "opponent cap stays raised");
    }
    private static void AstralCommunionOnRaisedCap()
    {
        int cost = Cards.FromId("AT_043").Cost;
        var (g, p) = Start(12, 7 - cost, cap: 15); // Seven Mana is spent after paying for the spell.
        Play(g, p, "AT_043");
        Verify(p.BaseMana == 15 && p.RemainingMana == 15, "Astral Communion (Gain 10) from 12 under cap 15 never lowers max, got " + State(p));
    }
    private static void AstralCommunionExcessManaOnlyAtCap()
    {
        // Excess Mana comes only when max is already at the cap (the player's MAXRESOURCES), not at a hard-coded 10.
        var (g, p) = Start(10, 0, cap: 15);
        Play(g, p, "AT_043");
        Verify(p.BaseMana == 15 && p.RemainingMana == 15 && !p.HandZone.Any(c => c.Card.Id == "CS2_013t"),
            "Astral Communion at 10 under cap 15 gains 5 and gives no Excess Mana, got " + State(p));
        var (g2, q) = Start(15, 0, cap: 15);
        Play(g2, q, "AT_043");
        Verify(q.BaseMana == 15 && q.HandZone.Any(c => c.Card.Id == "CS2_013t"),
            "Astral Communion at the raised cap 15 gives Excess Mana, got " + State(q));
    }
}
