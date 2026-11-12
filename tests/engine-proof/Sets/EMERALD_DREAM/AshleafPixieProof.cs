using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AshleafPixieProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_961", (g, p, id) => {
        var none = (Minion)Play(g, p, id); Core2026Proof.Check(!none.HasDivineShield && !none.HasLifeSteal, "empty hand grants no keywords");
        Add(p, "CS2_029"); Add(p, "CS2_226"); var below = (Minion)Play(g, p, id);
        Core2026Proof.Check(!below.HasDivineShield && !below.HasLifeSteal, "four-cost spell and expensive minion do not qualify");
        var spell = Add(p, "CS2_029"); spell.Cost = 5; var yes = (Minion)Play(g, p, id);
        Core2026Proof.Check(yes.HasDivineShield && yes.HasLifeSteal && yes.AttackDamage == 3 && yes.Health == 2, "current five-cost spell grants shield and lifesteal");
        yes.Silence(); Core2026Proof.Check(!yes.HasDivineShield && !yes.HasLifeSteal, "silence removes both granted keywords");
        spell.Cost = 4; var no = (Minion)Play(g, p, id); Core2026Proof.Check(!no.HasDivineShield && !no.HasLifeSteal, "current cost below five fails");
    });
}
