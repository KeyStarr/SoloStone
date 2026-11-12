using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier A: Blizzard, 2017-08-10, https://hearthstone.blizzard.com/en-us/news/20944003 (Lifesteal heals damage dealt).
// Tier D: data/carddb/37.0.0.254477/cards.json.gz; retain existing current-text power.
internal static class CoreDrainSoulProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_ICC_055", (g, p, id) => {
        var enemy=Summon(p.Opponent,"CS2_182"); var friend=Summon(p,"CS2_182");
        TargetRules(g,p,id,enemy,friend,enemyOnly:false); PoolPilotProof.EmptyHand(p);
        p.Hero.Damage=15; Play(g,p,id,enemy);
        Core2026Proof.Check(enemy.Damage==3 && p.Hero.Damage==12 && p.Opponent.Hero.Damage==0,"three damage and three Lifesteal to own hero");
        var shield=Summon(p.Opponent,"EX1_008"); Play(g,p,id,shield);
        Core2026Proof.Check(!shield.HasDivineShield && shield.Damage==0 && p.Hero.Damage==12,"prevented damage gives no Lifesteal");
        var small=Summon(p.Opponent,"EX1_506a"); Play(g,p,id,small);
        Core2026Proof.Check(small.Zone.Type==Zone.GRAVEYARD && p.Hero.Damage==9,"overkill still lifesteals full three");
        Summon(p,"CS2_142"); Play(g,p,id,friend);
        Core2026Proof.Check(friend.Damage==4 && p.Hero.Damage==5,"Spell Damage scales damage and Lifesteal on friendly target");
        p.Hero.Damage=1; enemy=Summon(p.Opponent,"CS2_182"); Play(g,p,id,enemy);
        Core2026Proof.Check(p.Hero.Health==30 && enemy.Damage==4,"Lifesteal cannot overheal");
    }, legacyPoolMember: true);
}
