using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using static Core2026Proof;

internal static class B15CombatContextProof
{
    [GroupProof("b15-combat-context")]
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        {
            var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
            var source = Summon(p, "CS2_231");
            source[(GameTag)31410] = 1;
            new Enchant(Effects.Attack_N(5)).ActivateTo(source);
            var listener=Summon(p,"EDR_484");
            DeathEvent saved=default;
            g.TriggerManager.Events.Trace=(ev,handler,index)=> { if(ev is DeathEvent death && death.Sender.Id==source.Id) saved=death; };
            Kill(g, source);
            Core2026Proof.Check(saved.LastKnownAttack==6 && saved.BoardPosition==0,"immutable DeathEvent captures effective Attack and original slot");
            g.TriggerManager.Events.Trace=null;
            Core2026Proof.Check(source[(GameTag)31400] == 6, "death context retains effective Attack before cleanup");
            foreach (var copy in new[] { g.CloneExact(history: history), Game.LoadSnapshot(g.SaveSnapshot()) })
                Core2026Proof.Check(copy.IdEntityDic[source.Id][(GameTag)31400] == 6, "death context survives clone/snapshot");
        }
        B15CombatContextEdges.Run();
        Console.WriteLine("PASS b15 combat context effective Attack/clone/snapshot/edges");
    }
}
