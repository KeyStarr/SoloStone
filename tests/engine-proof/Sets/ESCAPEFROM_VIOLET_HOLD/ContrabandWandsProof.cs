using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ContrabandWandsProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_312", (g, p, id) => {
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 3 && p.HandZone.All(c => c.Card.Id == "EX1_277" && c.Cost == 1 && c.Card.Implemented), "three exact Arcane Missiles");
        int hp = p.Opponent.Hero.Health; PlayEntity(g, p, p.HandZone.First());
        Core2026Proof.Check(p.Opponent.Hero.Health == hp - 3, "generated Missiles deal three damage");
        while (p.HandZone.Count < 9) Add(p, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == 10, "generation respects hand limit");
    });
}
