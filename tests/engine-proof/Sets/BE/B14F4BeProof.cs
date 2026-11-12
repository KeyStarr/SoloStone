using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class B14F4BeProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("BE_230", Scenario);
    }
    private static void Scenario(Game g, Controller p, string id)
    {
        var first = Summon(p, "CS2_182"); var body = (Minion)Play(g,p,id);
        var last = Summon(p, "CS2_182");
        Core2026Proof.Check(body.IsRush && !last.IsRush && last[GameTag.IMMUNE_WHILE_ATTACKING] == 1, "positional Rush and attacking immunity");
        body.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(!body.IsRush && last[GameTag.IMMUNE_WHILE_ATTACKING] == 0, "positional auras removed by Silence");

        ParkBoard(p);
        var target = Summon(p, "CS2_182");
        var provider = Summon(p, id); var remaining = Summon(p, id);
        Core2026Proof.Check(target.IsRush && target.CanAttack && target.AttackableByRush,
            "two Ravitz grant leftmost Rush and minion-only readiness");
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var copiedTarget = (Minion)next.IdEntityDic[target.Id];
            ((Minion)next.IdEntityDic[provider.Id]).Silence(); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(copiedTarget.IsRush && copiedTarget.CanAttack,
                "silencing one Ravitz preserves remaining Rush provider after clone/snapshot");
            Kill(next, (Minion)next.IdEntityDic[remaining.Id]);
            Core2026Proof.Check(!copiedTarget.IsRush, "last Ravitz leaving play removes Rush");
        }
    }
}
