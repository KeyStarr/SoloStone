using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static partial class B16TimersMiscCardsProof
{
    private static void Unpinned_Whelp(Game g,Controller p,string id)
    {
        Play(g,p,id);
        Core2026Proof.Check(p.Choice!=null && p.Choice.Choices.Count==3 && p.Choice.Choices.Select(e=>g.IdEntityDic[e].Card.Id).Distinct().Count()==3,"Whelp offers distinct five-cost cards");
        Core2026Proof.Check(p.Choice.Choices.All(e=>g.IdEntityDic[e].Card.Cost==5),"Whelp exact cost");
        B16TimersMiscProof.Answer(g);p.BaseMana=3;
        End(g);End(g);Core2026Proof.Check(p.BaseMana==4 && p.TemporaryMana==1,"Whelp temporary ramp");
    }
}
