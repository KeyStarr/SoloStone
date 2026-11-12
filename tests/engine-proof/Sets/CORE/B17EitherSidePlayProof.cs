using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
internal static class B17EitherSidePlayCOREProof
{
    [GroupProof("b17-either-side-play-core-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("CORE_DAL_575", (g,p,id) => {
            Play(g,p,id); Play(g,p,"CS2_027");
            Core2026Proof.Check(p.BoardZone.Count(m=>m.Card.Id=="CS2_mirror")==4,"Khadgar duplicates each Mirror Image");
        });
    }
}
