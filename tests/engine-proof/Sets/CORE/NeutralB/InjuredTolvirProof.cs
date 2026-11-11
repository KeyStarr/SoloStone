using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class InjuredTolvirProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_ULD_271", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasTaunt && source.AttackDamage == 2 && source.BaseHealth == 6 && source.Damage == 3 && source.Health == 3,
            "Taunt 2/6 deals exactly three self damage on play");
        var summoned = Summon(p, id);
        Core2026Proof.Check(summoned.HasTaunt && summoned.Health == 6 && summoned.Damage == 0, "summon skips self-damage battlecry");
        source.Silence(); Core2026Proof.Check(!source.HasTaunt && source.Damage == 3 && source.Health == 3, "Silence removes Taunt without healing self damage");
    });
}
