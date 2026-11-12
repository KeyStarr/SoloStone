using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
internal static partial class B14H4IdentityProof
{
    private static void TimeTravel()=>DeathKnightC1Proof.Case("END_005",Unpinned_BygoneEchoes);
    private static void Unpinned_BygoneEchoes(Game g,Controller p,string id)
    {
        Pools(g,id);
        foreach(int corpses in new[]{3,4}) foreach(bool outcast in new[]{false,true})
        {
            ParkBoard(p);EmptyHand(p);p[GameTag.CORPSES]=p[GameTag.CORPSES_SPENT_THIS_GAME]+corpses;
            Add(p,"CS2_182");var spell=Add(p,id);if(!outcast)Add(p,"CS2_182");
            Future(g,next=>{
                var held=next.IdEntityDic[spell.Id];
                Core2026Proof.Check(OutcastRules.AtEdge(held)==outcast,$"fixture edge={OutcastRules.AtEdge(held)} expected={outcast} slot={held.ZonePosition} count={next.Player1.HandZone.Count}");
                PlayEntity(next,next.Player1,held);
                Core2026Proof.Check(next.Player1.BoardZone.Count==1+(corpses==4?1:0)+(outcast?1:0) && next.Player1.BoardZone.All(m=>m.Card.Cost==4),$"Unpinned_BygoneEchoes: corpses={corpses} outcast={outcast} board={string.Join(",",next.Player1.BoardZone.Select(m=>m.Card.Id))} remaining={CorpseRules.Available(next.Player1)}");
                Core2026Proof.Check(CorpseRules.Available(next.Player1)==(corpses==4?0:3),"Unpinned_BygoneEchoes: no partial payment");
            });
        }
        ParkBoard(p);EmptyHand(p);for(int i=0;i<6;i++)Summon(p,"CS2_182");p[GameTag.CORPSES]=p[GameTag.CORPSES_SPENT_THIS_GAME]+4;
        Play(g,p,id);Core2026Proof.Check(p.BoardZone.Count==7 && CorpseRules.Available(p)==0,"Unpinned_BygoneEchoes: entire bonus spent with one slot");
    }
}
