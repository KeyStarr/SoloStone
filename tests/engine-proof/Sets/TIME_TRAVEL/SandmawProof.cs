using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class SandmawProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_053", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 7 && source.Health == 2 && source.IsRace(Race.ELEMENTAL) && source.IsRace(Race.BEAST) && !source.IsRace(Race.MURLOC), "printed stats and both types");
        Play(g, p, "DS1_070", source); Core2026Proof.Check(source.AttackDamage == 9 && source.Health == 4 && source.HasTaunt, "Beast-targeting buff accepts dual type");
        source.Silence(); Core2026Proof.Check(source.AttackDamage == 7 && source.Health == 2 && !source.HasTaunt && source.IsRace(Race.BEAST) && source.IsRace(Race.ELEMENTAL), "Silence restores stats, retains both races");
    });
}
