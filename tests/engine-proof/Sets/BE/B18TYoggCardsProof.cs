using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B18TYoggCardsProof
{
    [GroupProof("b18-t-yogg-cards", ProofKind.Cards)]
    internal static void Run() => StandardSetsProof.Case("BE_009", Pinned_Yogg);
    private static void Pinned_Yogg(Game g, Controller p, string id)
    {
        var god = Summon(p,id);
        B16OldGodProof.Use(g,god,"BE_009t1");
        PoolPilotProof.Resume(g,copy => Play(copy,copy.ControllerById(p.Id),"CS2_029",copy.ControllerById(p.Id).Opponent.Hero));
        Core2026Proof.Check(p.Opponent.Hero.Damage == 12,"Brain casts next spell twice");
        Play(g,p,"CS2_029",p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 18,"Brain consumed once");
    }
}
