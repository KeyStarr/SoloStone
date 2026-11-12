using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B16TimersMiscProof
{
    [GroupProof("b16-timers-misc")]
    internal static void Run()
    {
        var g = HeraldProof.New(CardClass.SHAMAN); var p = g.Player1;
        Play(g,p,"FIR_927"); Answer(g);
        p.BaseMana=3; p.UsedMana=0;
        foreach(var next in new[]{g,g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())})
        {
            End(next);
            Core2026Proof.Check(next.Player1.TemporaryMana==0,"Unpinned_Whelp no opponent grant");
            End(next);
            Core2026Proof.Check(next.Player1.BaseMana==4 && next.Player1.RemainingMana==5,"Unpinned_Whelp next owner start grants usable temporary crystal");
            End(next);
            Core2026Proof.Check(next.Player1.BaseMana==4 && next.Player1.TemporaryMana==0,"Unpinned_Whelp expiry preserves permanent capacity");
        }
        B16TimersBoundaryProof.Run();
        Console.WriteLine("PASS B16 timers facility clone/snapshot");
    }
    internal static void Answer(Game g)
    {
        while(g.CurrentPlayer.Choice!=null)
        {
            var d=g.GetPendingDecisions().Single();
            Core2026Proof.Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(d.Id,d.ControllerId,
                new[]{g.CurrentPlayer.Choice.Choices[0]}),out _),"timer Discover answer accepted");
        }
    }
}
