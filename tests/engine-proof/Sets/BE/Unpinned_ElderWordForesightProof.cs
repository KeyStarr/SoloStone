using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_ElderWordForesightProof
{
    internal static void Run() => StandardSetsProof.Case("BE_221", (g,p,id) => {

        int deck=p.DeckZone.Count; Play(g,p,id);
        var drawn=p.HandZone.Single();
        Core2026Proof.Check(p.DeckZone.Count==deck-1 && drawn.Cost==Math.Max(0,drawn.Card.Cost-2),"Foresight discounts its own newly drawn card");
        Play(g,p,"BE_222");
        Core2026Proof.Check(p.DeckZone.Count==deck-2 && p.BoardZone.Count(x=>x.Card.Id=="BE_222t")==1,"Unpinned_ElderNextShadow: Manifest inherits only Foresight payload");
        Play(g,p,"BE_223");
        Core2026Proof.Check(p.DeckZone.Count==deck-2 && p.BoardZone.Count(x=>x.Card.Id=="BE_222t")==2 && p.Opponent.Hero.Damage==3,
            "Unpinned_ElderNextShadow: Flay inherits Manifest without Foresight propagation");
    });
}
