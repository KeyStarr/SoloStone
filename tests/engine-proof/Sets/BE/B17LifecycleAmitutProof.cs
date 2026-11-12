using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B17LifecycleAmitutProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("BE_143", Unpinned_AmitutSnapshotAndDrawGrant);
    }
    internal static void Unpinned_AmitutSnapshotAndDrawGrant(Game g, Controller p, string id)
    {
        PoolPilotProof.ClassDeck(p);
        var holy=Entity.FromCard(p,Cards.FromId("CORE_CS2_093"),zone:p.DeckZone);
        Entity.FromCard(p,Cards.FromId("CS2_029"),zone:p.DeckZone);
        Play(g,p,id);
        var copies=p.DeckZone.Where(c=>c.Card.Id==holy.Card.Id && c.Id!=holy.Id).ToArray();
        Core2026Proof.Check(copies.Length==1 && p.DeckZone.Count==3 && holy[GameTag.CASTSWHENDRAWN]==0,"Amitut snapshots only Holy originals");
        var copy=copies[0];
        Core2026Proof.Check(copy[GameTag.CASTSWHENDRAWN]==1,"granted keyword visible on entity");
        var again=Generic.Copy(p,p.Hero,copy,Zone.DECK);
        Core2026Proof.Check(again[GameTag.CASTSWHENDRAWN]==1,"further deck copy retains grant");
        PoolPilotProof.Resume(g.CloneExact(history: true),next=>{
            var owner=next.Player1; int enemy=owner.Opponent.Hero.Health;
            owner.SetasideZone.Add(owner.DeckZone.Remove(next.IdEntityDic[again.Id]));
            Generic.Draw(owner,next.IdEntityDic[copy.Id]);
            Core2026Proof.Check(next.IdEntityDic[copy.Id].Zone==owner.GraveyardZone && owner.Opponent.Hero.Health==enemy-2,
                "actual tutor draw casts granted spell");
        });
        p.HandZone.Add(p.DeckZone.Remove(copy));
        Core2026Proof.Check(copy.Zone==p.HandZone && copy[GameTag.CASTSWHENDRAWN]==1 && p.Opponent.Hero.Health==30,"put into hand retains grant without casting");
        PoolPilotProof.FillHand(p); Generic.Draw(p,again);
        Core2026Proof.Check(again.Zone==p.GraveyardZone && p.Opponent.Hero.Health==30,"burn never casts granted spell");
    }
}
