using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz; retain existing current-text power.
internal static class CoreVulgarHomunculusProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_LOOT_013", (g, p, id) => {
        p.Hero.Armor=1; var source=(Minion)Play(g,p,id);
        Core2026Proof.Check(source.HasTaunt && source.AttackDamage==2 && source.Health==4 && source.IsRace(Race.DEMON) && p.Hero.Armor==0 && p.Hero.Damage==1 && p.Opponent.Hero.Damage==0,"2/4 Taunt Demon deals two to owner through Armor");
        source.Silence(); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(!source.HasTaunt,"Silence removes Taunt");
        Summon(p,id); Core2026Proof.Check(p.Hero.Damage==1,"summon skips battlecry");
        Play(g,p,id); Core2026Proof.Check(p.Hero.Damage==3 && p.Opponent.Hero.Damage==0,"unarmored battlecry deals two only to owner");
    }, legacyPoolMember: true);
}
