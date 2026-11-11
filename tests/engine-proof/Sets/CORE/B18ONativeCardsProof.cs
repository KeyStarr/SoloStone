using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
internal static class B18ONativeCardsProof
{
    [GroupProof("b18-o-native-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("CS3_035", (g,p,id)=>B18ONativeProof.Nozdormu());
        StandardSetsProof.Case("CORE_WON_145", Unpinned_Avatar);
        B18ONativeIllusionProof.Run();
        B18ONativeGuardProof.Run();
    }
    private static void Unpinned_Avatar(Game g, Controller p, string id)
    {
        KnownMetaProofFixture.UseBlackEmpire(g);
        Play(g,p,id);
        Core2026Proof.Check(g.IdEntityDic.Values.Count(e=>e[SabberStoneCore.Enums.GameTag.DISPLAYED_CREATOR]!=0)>=5,"Avatar generates pack");
        Core2026Proof.Check(p.Choice==null && !g.Resolution.HasSuspendedEffect,"pack autoplay settles decisions");
    }
}
