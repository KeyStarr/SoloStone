using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
internal static class B18ONativeGuardProof
{
    internal static void Run()=>StandardSetsProof.Case("JAIL_703",(g,p,id)=>B18ONativeProof.Guard());
    internal static void Unpinned_Edges()
    {
        foreach(bool enemy in new[]{false,true})
        {
            var g=LocationProofs.Game();var p=enemy?g.Player2:g.Player1;
            var muted=Summon(p,"JAIL_703");muted.Silence();Kill(g,muted);
            Core2026Proof.Check(p[NativeTailRules.SorryEnabled]==0,"silenced Guard cannot unlock");
            while(!p.HandZone.IsFull)Add(p,"CS2_182");while(!p.BoardZone.IsFull)Summon(p,"JAIL_703");
            Kill(g,p.BoardZone.First());Kill(g,p.BoardZone.First());End(g);
            foreach(var next in B16AutoplayProof.Futures(g))
            {
                var c=next.ControllerById(p.Id);Core2026Proof.Check(c[NativeTailRules.SorryEnabled]==1 && c.Opponent[NativeTailRules.SorryEnabled]==0,"idempotent per-controller unlock and capacity independence");
                End(next);End(next);Core2026Proof.Check(c[NativeTailRules.SorryEnabled]==1,"unlock persists across turns");
                var hero=(Hero)Entity.FromCard(c,Cards.FromId("ICC_828"),zone:c.SetasideZone);AutoPlayRules.Play(hero,c.Hero);
                Core2026Proof.Check(c[NativeTailRules.SorryEnabled]==1,"hero replacement keeps controller unlock");
            }
        }
        Core2026Proof.Check(LocationProofs.Game().Player1[NativeTailRules.SorryEnabled]==0,"new match clears Sorry");
    }
}
