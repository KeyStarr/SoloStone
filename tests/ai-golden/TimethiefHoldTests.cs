using System;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static ReplyTests;

// DeckPlanEvaluator.EarlyTimethief: a Timethief Rafaam played this turn before Ready is a thrown win condition.
public static class TimethiefHoldTests
{
    private static double Penalty(Game g) => DeckPlanEvaluator.EarlyTimethief(View(g).Facts.Own);
    public static void TestBoardTimethiefIsPenalizedHandIsNot()
    {
        var g = Fixture(CardClass.WARLOCK);
        Entity.FromCard(g.Player1, Cards.FromId("TIME_005"), zone: g.Player1.HandZone);
        Check(Penalty(g) == 0, "a held Timethief costs nothing, so drawing or Discovering it is never rewarded");
        Board(g, true, "TIME_005");
        Check(Penalty(g) == GoldenAblation.DefaultFabledHold, "a Timethief played this turn costs the shipped hold");
    }
    public static void TestOnlyThisTurnsPlayCounts()
    {
        var g = Fixture(CardClass.WARLOCK);
        var t = Board(g, true, "TIME_005");
        t[GameTag.NUM_TURNS_IN_PLAY] = 1;
        Check(Penalty(g) == GoldenAblation.DefaultFabledHold, "the reply turn's leaves still see this turn's slam");
        t[GameTag.NUM_TURNS_IN_PLAY] = 2;
        Check(Penalty(g) == 0, "an earlier slam is sunk: removing it (own Annihilation, suicide attack) earns nothing");
    }
    public static void TestOnlyOwnBoardCountsAndZeroTurnsItOff()
    {
        var g = Fixture(CardClass.WARLOCK, CardClass.WARLOCK);
        Board(g, false, "TIME_005");
        Check(Penalty(g) == 0, "the opponent's Timethief is not the bot's win condition");
        Board(g, true, "TIME_005");
        double old = GoldenAblation.FabledHold;
        try { GoldenAblation.FabledHold = 0; Check(Penalty(g) == 0, "weight 0 reproduces the pre-rule brain"); }
        finally { GoldenAblation.FabledHold = old; }
    }
}
