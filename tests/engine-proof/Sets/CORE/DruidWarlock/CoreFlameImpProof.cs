using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz; retain existing current-text power.
internal static class CoreFlameImpProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_319", (g, p, id) => {
        p.Hero.Armor=2; var source=(Minion)Play(g,p,id);
        Core2026Proof.Check(source.AttackDamage==3 && source.Health==2 && source.IsRace(Race.DEMON) && p.Hero.Armor==0 && p.Hero.Damage==1 && p.Opponent.Hero.Damage==0,"three damage to own hero, Armor absorbs two");
        Summon(p,id); Core2026Proof.Check(p.Hero.Damage==1,"summon skips battlecry");
        Play(g,p,id); Core2026Proof.Check(p.Hero.Damage==4 && p.Opponent.Hero.Damage==0,"unarmored battlecry deals three only to owner");
    }, legacyPoolMember: true);
}
