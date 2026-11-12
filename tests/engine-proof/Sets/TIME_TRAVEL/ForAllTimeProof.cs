using SabberStoneCore.Enums;
using static Core2026Proof;

internal static class ForAllTimeProof
{
    internal static void Run() => StandardSetsProof.Case("END_028", (g, p, id) => {
        var low = Summon(p, "CS2_171"); var boundary = Summon(p, "CS2_182"); var high = Summon(p, "CS2_200");
        var enemyLow = Summon(p.Opponent, "CS2_182"); var enemyHigh = Summon(p.Opponent, "CS2_200");
        var five = Summon(p, "CS2_213"); var enemyFive = Summon(p.Opponent, "CS2_213");
        Core2026Proof.Check(five.AttackDamage == 5 && enemyFive.AttackDamage == 5, "five-Attack boundary on both sides");
        Play(g, p, id);
        Core2026Proof.Check(low.Zone == p.GraveyardZone && boundary.Zone == p.GraveyardZone && enemyLow.Zone == p.Opponent.GraveyardZone && high.Zone == p.BoardZone && enemyHigh.Zone == p.Opponent.BoardZone, "destroys both sides at or below four; six survives");
        Core2026Proof.Check(five.Zone == p.BoardZone && enemyFive.Zone == p.Opponent.BoardZone, "five-Attack minions on both sides survive");
        Core2026Proof.Check(p.OverloadOwed == 2 && p.OverloadThisGame == 2, "Overload two tracked");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 2 && p.Opponent.BoardZone.Count == 2 && p.OverloadOwed == 4, "no low targets still casts and overloads");
        End(g); End(g); Core2026Proof.Check(p.OverloadLocked == 4 && p.RemainingMana == 6, "next own turn locks crystals");
    });
}
