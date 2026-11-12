using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ScorchingObserverProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_486", (g, p, id) => {
        p.Hero.Damage = 15; var source = (Minion)Play(g, p, id);
        var enemy = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == source.Id && t.Target?.Id == enemy.Id), "Rush attacks minions immediately");
        Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == source.Id && t.Target is Hero), "Rush cannot hit hero on play turn");
        AttackOptionsProof.Lifecycle(g, source, enemy, rush: true);
        Attack(g, source, enemy); Core2026Proof.Check(p.Hero.Damage == 8, "Lifesteal heals attack damage");
    });
}
