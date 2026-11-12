using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class HalazzitheLynxProof
{
    internal static void Run() => Case("CORE_TRL_900", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source[GameTag.RUSH] == 1 && source.AttackDamage == 4 && source.Health == 2, "Halazzi current Rush and stats");
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.All(c => c.Card.Id == "TRL_348t" && c[GameTag.RUSH] == 1), "fill hand with Rush Lynxes");
    });
}
