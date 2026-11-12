using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_EfflorescenceProof
{
    internal static void Run() => StandardSetsProof.Case("BE_801", (g,p,id) => {

        foreach(var x in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(x));
        p.DeckZone.Add(Entity.FromCard(p,Cards.FromId("EDR_060")));
        p.DeckZone.Add(Entity.FromCard(p,Cards.FromId(id)));
        Play(g,p,id);
        var drawn=p.HandZone.Single(x=>x.Card.Id=="EDR_060");
        Core2026Proof.Check(p.Hero.Armor==0 && drawn.AppliedEnchantments.Any(e=>e.Card.Id=="BE_801e"),
            "Unpinned_EfflorescenceMark: Nature draw enters hand marked, never auto-cast");
        End(g); End(g); PlayEntity(g,p,drawn);
        Core2026Proof.Check(p.Hero.Armor==10,"Unpinned_EfflorescenceMark: retained specific card casts twice across turns");
    });
}
