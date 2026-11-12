using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_ElderWordFlayProof
{
    internal static void Run() => StandardSetsProof.Case("BE_223", (g,p,id) => {

        p.Opponent.Hero[GameTag.HEALTH]=1000;
        Play(g,p,id); Play(g,p,id);
        Core2026Proof.Check(p.Opponent.Hero.Damage==9 && p.AppliedEnchantments.Count(e=>e.Card.Id=="BE_223e")==1,
            "Unpinned_ElderStacksOrder: second native Flay plus inherited payload once; grant stays one-hop");
        RepeatRules.GrantPayload(p,p.Hero,"BE_223e"); RepeatRules.GrantPayload(p,p.Hero,"BE_223e");
        Play(g,p,id);
        Core2026Proof.Check(p.Opponent.Hero.Damage==21 && p.AppliedEnchantments.Count(e=>e.Card.Id=="BE_223e")==1,
            "Unpinned_ElderStacksOrder: all independent pre-play entries consumed once, newly created native grant stays pending");
    });
}
