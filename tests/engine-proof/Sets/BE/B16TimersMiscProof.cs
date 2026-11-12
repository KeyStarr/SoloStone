using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static partial class B16TimersMiscCardsProof
{
    [GroupProof("b16-timers-misc-cards",ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("BE_327",Unpinned_Tome);
        StandardSetsProof.Case("CATA_496",Unpinned_Chains);
        StandardSetsProof.Case("FIR_927",Unpinned_Whelp);
        StandardSetsProof.Case("JAIL_860",Unpinned_Chef);
        StandardSetsProof.Case("TIME_706",Unpinned_Fins);
    }
    private static void Unpinned_Tome(Game g,Controller p,string id)
    {
        var tome=Add(p,id);
        Core2026Proof.Check(tome[GameTag.TAG_SCRIPT_DATA_NUM_1]==2,"Tome enters with two owner starts");
        End(g);Core2026Proof.Check(tome.Card.Id==id,"opponent start cannot decipher");
        End(g);Core2026Proof.Check(tome.Card.Id==id && tome[GameTag.TAG_SCRIPT_DATA_NUM_1]==1,"first owner start");
        foreach(var next in new[]{g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot()),g})
        {
            End(next);End(next);
            Core2026Proof.Check(next.IdEntityDic[tome.Id].Card.Id.StartsWith("BE_327t"),"Unpinned_Tome second start reveals saved form");
        }
    }

}
