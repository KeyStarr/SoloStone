using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static partial class B16TimersMiscCardsProof
{
    private static void Unpinned_Chains(Game g,Controller p,string id)
    {
        var victim=Summon(p.Opponent,"CS2_182");
        Play(g,p,id,victim);
        Core2026Proof.Check(victim.Controller==p && victim.CantAttack,"Chains steals and blocks attack");
        End(g);Core2026Proof.Check(victim.Controller==p && !victim.CantAttack,"attack ban expires independently");
        End(g);Core2026Proof.Check(victim.Controller==p.Opponent,"control returns at enemy end");
        Play(g,p,id,victim);victim.Silence();
        Core2026Proof.Check(victim.Controller==p.Opponent && !victim.CantAttack,"Silence returns and clears only lease");
    }
}
