using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static ReplyTests;

// DeckPlanEvaluator.ClearExposure: the overextension term priced from the fair class prior only.
public static class ClearExposureTests
{
    private static double Exposure(Game g, double scale = 1)
    {
        double old = GoldenAblation.ClearRisk;
        try {
            GoldenAblation.ClearRisk = scale;
            var f = View(g).Facts;
            return new DeckPlanEvaluator(DeckPlans.DragonWarrior).ClearExposure(f.Own, f.Opponent);
        }
        finally { GoldenAblation.ClearRisk = old; }
    }
    private static Game Mage(int crocs, int enemyBaseMana = 9)
    {
        var g = Fixture(); g.Turn = 15; g.Player2.BaseMana = enemyBaseMana;
        for (int i = 0; i < 3; i++) Entity.FromCard(g.Player2, Cards.FromId("CS2_182"), zone: g.Player2.HandZone);
        for (int i = 0; i < crocs; i++) { var m = Board(g, true, "CS2_120", true); m.Damage = 1; }
        return g;
    }
    public static void TestClearDamageReadsPrintedAmountOrDestroy()
    {
        Check(CardFrequencies.ClearDamage("CS2_032") >= 4, "Flamestrike prints its area damage");
        Check(CardFrequencies.ClearDamage("EX1_407") == int.MaxValue, "Brawl destroys regardless of health");
        Check(CardFrequencies.Clears(CardClass.MAGE, 10).Count > 0, "the Mage prior holds castable clears at 10 mana");
    }
    public static void TestExtraVulnerableMinionsRaiseExposure()
    {
        double needed = Exposure(Mage(2)), three = Exposure(Mage(3)), five = Exposure(Mage(5));
        Check(needed == 0, "the two minions needed to contest are never penalized");
        Check(three > 0 && five > three, "each extra clear-vulnerable minion adds expected clear loss");
        Check(Exposure(Mage(5), 2) > five * 1.99 && Exposure(Mage(5), 0) == 0, "the weight scales the term; 0 turns it off");
    }
    public static void TestClearProofBodiesAddNoExposure()
    {
        var g = Mage(3); double before = Exposure(g);
        Board(g, true, "CS1_069", true); Board(g, true, "CS1_069", true);
        Check(Math.Abs(Exposure(g) - before) < 1e-9, "3/6 bodies survive the prior's clears and must not raise exposure");
    }
    public static void TestNoCastableClearNoExposure()
    {
        Check(Exposure(Mage(5, 1)) == 0, "a 2-mana Mage holds no castable clear in the prior");
    }
    public static void TestExposureIgnoresHiddenHandIdentities()
    {
        var g = Mage(5); var hidden = Variant(g);
        Check(Exposure(g) == Exposure(hidden), "exposure depends on public hand count only, never hidden identities");
    }
}
