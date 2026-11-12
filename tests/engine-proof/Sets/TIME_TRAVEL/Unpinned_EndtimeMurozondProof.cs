using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class Unpinned_EndtimeMurozondProof
{
    internal static void Run() => StandardSetsProof.Case("END_037",(g,p,id) => {
        Summon(p,"LOE_077"); p.Hero.Damage=12;
        Play(g,p,id);
        Core2026Proof.Check(p.BoardZone.Count==7 && p.BoardZone.Where(m=>m.Card.Id!="LOE_077" && m.Card.Id!=id).All(m=>m.IsRace(Race.DRAGON)) && p.Hero.Damage==0,
            "Murozond fill/heal: board="+string.Join(",",p.BoardZone.Select(m=>m.Card.Id))+" damage="+p.Hero.Damage);
        foreach(int expected in new[]{p.Opponent.Id,p.Opponent.Id,p.Id,p.Opponent.Id,p.Opponent.Id,p.Id})
        {
            End(g);
            Core2026Proof.Check(g.CurrentPlayer.Id==expected,"Unpinned_MurozondRepeatedQueue: A-B-B-A-B-B-A");
        }
    });
}
