using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class InjuredAttendantProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_304", (g, p, id) => {
        p.Hero.Damage = 10; var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.Health == 4 && source.Damage == 4 && p.Hero.Damage == 6, "self damage 4 triggers Lifesteal");
        var summoned = Summon(p, id); Core2026Proof.Check(summoned.Health == 8 && summoned.Damage == 0, "summon skips battlecry");
    });
}
