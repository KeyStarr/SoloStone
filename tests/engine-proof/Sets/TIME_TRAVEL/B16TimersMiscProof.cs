using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static partial class B16TimersMiscCardsProof
{
    private static void Unpinned_Fins(Game g,Controller p,string id)
    {
        g=B16TimersBoundaryProof.FinsGame(g.Random.Next());p=g.Player1;
        var saved=Add(p,"CS2_029");
        var fins=(Minion)Play(g,p,id);
        Core2026Proof.Check(!p.HandZone.Contains(saved) && p.HandZone.Count==3,"Fins replaces hand with opening three");
        var added=Add(p,"CS2_024");Kill(g,fins);
        End(g);
        Core2026Proof.Check(p.HandZone.Select(c=>c.Id).SequenceEqual(new[]{added.Id,saved.Id}),"Unpinned_Fins retains new card and restores original after source death");
    }
}
