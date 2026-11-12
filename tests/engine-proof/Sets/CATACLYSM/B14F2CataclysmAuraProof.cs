using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PaladinAurasProof;

internal static class B14F2CataclysmAuraProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("CATA_480", Unpinned_SandfuryMinionsOnly);
        StandardSetsProof.Case("MEND_802", Convalescence);
    }
    private static void Unpinned_SandfuryMinionsOnly(Game g, Controller p, string id)
    {
        var emperor = Summon(p, "BRM_028");
        Summon(p.Opponent, "BRM_028");
        var own = Add(p, "EX1_561"); var enemy = Add(p.Opponent, "EX1_561");
        Play(g, p, id); Install(p, "TIME_700");
        for (int turn = 1; turn <= 3; turn++)
        {
            End(g);
            Core2026Proof.Check(own.Cost == 9 - 2 * turn && enemy.Cost == 10 - turn,
                "friendly end minion doubles including final Aura turn");
            Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "TIME_700t") == turn, "Aura spell end effect never doubled");
            End(g);
            Core2026Proof.Check(enemy.Cost == 9 - turn, "opponent end minion never doubled");
        }
        End(g);
        Core2026Proof.Check(own.Cost == 2 && Active(p).Length == 0, "expired Sandfury stops doubling");
    }

    private static void Convalescence(Game g, Controller p, string id)
    {
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.All(m => m.Card.Id == "CS2_101t"
            && m.AttackDamage == 1 && m.Health == 1 && m.HasDivineShield), "two exported Recruits with shield");
        while (p.BoardZone.SlotCount < 6) Summon(p, "CS2_231");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "CS2_101t") == 3, "one free slot truncates summons");
    }
}
