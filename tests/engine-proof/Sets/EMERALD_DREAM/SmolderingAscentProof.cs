using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class SmolderingAscentProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_916", (g, p, id) => SmolderingStrengthProof.Scenario(g, p, id));
}
