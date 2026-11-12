using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using static Core2026Proof;

internal static class TurnQueueProof
{
    internal static void Run()
    {
        var game=HeraldProof.New(CardClass.MAGE);
        // Queue B-B-A through the rules API: raw slot tags carry no turn provenance, which strict snapshot load rejects.
        TurnQueueRules.Prepend(game,game.Player2,game.Player2,game.Player1);
        Core2026Proof.Check(game[(GameTag)32200]==3 && game[(GameTag)40000]==game.Player2.Id && game[(GameTag)40001]==game.Player2.Id
            && game[(GameTag)40002]==game.Player1.Id,"TurnQueue: Prepend writes the B-B-A slots");
        for(int i=0;i<4;i++)
        {
            var clone=game.CloneExact(history:true); var loaded=Game.LoadSnapshot(game.SaveSnapshot());
            foreach(var next in new[]{game,clone,loaded}) End(next);
            Core2026Proof.Check(game.CurrentPlayer.Id==(i<2 || i==3 ? game.Player2.Id:game.Player1.Id),"TurnQueue: ordered B-B-A segment before normal alternation");
            Core2026Proof.Check(Canonical.State(game)==Canonical.State(clone) && Canonical.State(game)==Canonical.State(loaded),"TurnQueue clone/snapshot parity");
        }
        foreach(bool warpFirst in new[]{true,false})
        {
            var g=HeraldProof.New(CardClass.MAGE); var p=g.Player1;
            if(warpFirst) new ExtraTurnEffectTask().Process(g,p,p.Hero,null);
            TurnQueueRules.Prepend(g,p.Opponent,p.Opponent,p);
            if(!warpFirst) new ExtraTurnEffectTask().Process(g,p,p.Hero,null);
            foreach(int expected in warpFirst ? new[]{p.Opponent.Id,p.Opponent.Id,p.Id,p.Id}:new[]{p.Id,p.Opponent.Id,p.Opponent.Id,p.Id})
            {
                End(g); Core2026Proof.Check(g.CurrentPlayer.Id==expected,"Unpinned_QueueWarpOrder: insertion preserves each segment and prior extra turn");
            }
        }
        System.Console.WriteLine("PASS TurnQueue Unpinned_OrderedFutureTurns");
    }
}
