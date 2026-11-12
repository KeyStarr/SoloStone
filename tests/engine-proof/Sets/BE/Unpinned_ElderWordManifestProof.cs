using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_ElderWordManifestProof
{
    internal static void Run() => StandardSetsProof.Case("BE_222", (g,p,id) => {

        Play(g,p,id); var token=p.BoardZone.Single();
        Core2026Proof.Check(token.Card.Id=="BE_222t" && token.AttackDamage==5 && token.Health==5 && token.HasLifeSteal,"Manifest exact Voidwing token");
        Play(g,p,"GAME_005");
        Core2026Proof.Check(p.AppliedEnchantments.Count(e=>e.Card.Id=="BE_222e")==1,"non-Shadow spell preserves payload");
        Play(g,p,"BE_223");
        Core2026Proof.Check(p.BoardZone.Count(x=>x.Card.Id=="BE_222t")==2 && p.AppliedEnchantments.All(e=>e.Card.Id!="BE_222e"),"Unpinned_ElderNextShadow: consume once after next Shadow native effect");
    });
}
