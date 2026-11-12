using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_TyrandeProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_464", (g,p,id) => {

        p.Opponent.Hero[GameTag.HEALTH]=1000;
        var source=(Minion)Play(g,p,id); Kill(g,source);
        for(int i=0;i<4;i++) Play(g,p,"CS2_029",p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage==42 && p.AppliedEnchantments.All(e=>e.Card.Id!="EDR_464e2"),
            "Unpinned_TyrandeCharges: three hand plays doubled after source death; repeat consumes no extra charge");
        Play(g,p,id);
        var counter=(Spell)Entity.FromCard(p.Opponent,Cards.FromId("EX1_287"));
        Generic.CastSpell(p.Opponent,g,counter,null,0); counter.IsExhausted=false;
        int damage=p.Opponent.Hero.Damage;
        Play(g,p,"CS2_029",p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage==damage && p.AppliedEnchantments.Single(e=>e.Card.Id=="EDR_464e2")[GameTag.TAG_SCRIPT_DATA_NUM_1]==2,
            "Unpinned_TyrandeCountered: counter consumes one charge and suppresses both bodies");
        var automatic=(Spell)Entity.FromCard(p,Cards.FromId("CS2_029"));
        Generic.CastSpell(p,g,automatic,p.Opponent.Hero,0);
        Core2026Proof.Check(p.Opponent.Hero.Damage==damage+6 && p.AppliedEnchantments.Single(e=>e.Card.Id=="EDR_464e2")[GameTag.TAG_SCRIPT_DATA_NUM_1]==2,
            "Unpinned_TyrandeEffectCast: generated cast neither repeats nor consumes player charge");
        Play(g,p,"EX1_287");
        Core2026Proof.Check(p.SecretZone.Count==1 && p.AppliedEnchantments.Single(e=>e.Card.Id=="EDR_464e2")[GameTag.TAG_SCRIPT_DATA_NUM_1]==1,
            "Unpinned_TyrandeSecret: duplicate repeat fizzles with one charge consumed");
    });
}
