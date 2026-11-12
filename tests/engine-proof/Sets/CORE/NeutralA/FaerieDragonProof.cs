using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FaerieDragonProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_NEW1_023", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 3 && source.Health == 2 && source.IsRace(Race.DRAGON), "printed Dragon 3/2");
        Elusive(g, p, source);
    });
}
