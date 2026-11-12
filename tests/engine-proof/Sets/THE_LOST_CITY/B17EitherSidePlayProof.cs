using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
internal static class B17EitherSidePlayTHE_LOST_CITYProof
{
    [GroupProof("b17-either-side-play-the-lost-city-cards", ProofKind.Cards)]
    internal static void Run() => StandardSetsProof.Case("TLC_522", Unpinned_OpuBrannAndDeathrattle);
    private static void Unpinned_OpuBrannAndDeathrattle(Game g,Controller p,string id)
    {
        Summon(p,"LOE_077"); p.IsComboActive=true;
        var enemy=Summon(p.Opponent,"CS2_200");
        var card=(Minion)Play(g,p,id);
        Core2026Proof.Check(card.HasStealth && enemy.Damage==3 && p.HandZone.Count==3,
            "Brann repeats Battlecry twice then single independent Combo Fan");
        foreach(var next in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g})
        {
            Kill(next,(Minion)next.IdEntityDic[card.Id]);
            Core2026Proof.Check(((Minion)next.IdEntityDic[enemy.Id]).Damage==4 && next.Player1.HandZone.Count==4,
                "Opu Deathrattle casts once after clone/snapshot");
        }
    }
}
