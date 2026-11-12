using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ChronicleKeeperProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_062", (g, p, id) => {
        var none = (Minion)Play(g, p, id); Core2026Proof.Check(!none.HasTaunt && !none.HasDivineShield, "no held Dragon means no keywords");
        Summon(p, "TIME_056"); var boardOnly = (Minion)Play(g, p, id);
        Core2026Proof.Check(!boardOnly.HasTaunt && !boardOnly.HasDivineShield, "Dragon on board does not count");
        Add(p, "TIME_056"); var yes = (Minion)Play(g, p, id);
        Core2026Proof.Check(yes.HasTaunt && yes.HasDivineShield && yes.AttackDamage == 3 && yes.Health == 6, "held Dragon grants both keywords");
        Play(g, p, "BE_228", yes); Core2026Proof.Check(!yes.HasTaunt && !yes.HasDivineShield, "silence removes granted keywords");
    });
}
