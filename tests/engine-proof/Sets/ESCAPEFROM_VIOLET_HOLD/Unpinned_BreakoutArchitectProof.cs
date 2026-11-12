using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_BreakoutArchitectProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_123", (g,p,id) => {

        p.Hero.Card=Cards.FromId("HERO_08");
        var source=(Minion)Play(g,p,id);
        Core2026Proof.Check(p.Choice!=null && p.Choice.Choices.Count>=3,"Architect has admitted Discover pool");
        int chosen=p.Choice.Choices[0]; PlayerDecisionProof.Answer(g,chosen);
        var generated=p.HandZone.Single();
        Core2026Proof.Check(generated.Card.Cost>=5 && generated.AppliedEnchantments.Any(e=>e.Card.Id=="JAIL_123e"),"Unpinned_ArchitectMark: selected spell alone marked");
        Kill(g,source); End(g); End(g);
        Core2026Proof.Check(generated.Zone==p.HandZone && generated.AppliedEnchantments.Any(e=>e.Card.Id=="JAIL_123e"),"Unpinned_ArchitectMark: mark outlives Architect across turns");
    });
}
