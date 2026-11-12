using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, current text/tags; unchanged legacy power.
internal static class SouthseaCaptainProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_NEW1_027", (g, p, id) => {
        var pirate = Summon(p, "NEW1_018"); var ordinary = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "NEW1_018");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(Cards.FromId("NEW1_027e").AssetId == 1852 && Cards.FromId("NEW1_027e").Text == "+1/+1.",
            "exact pinned Captain enchantment identity and text");
        Core2026Proof.Check(source.AttackDamage == 3 && source.Health == 3 && pirate.AttackDamage == 3 && pirate.Health == 4
            && ordinary.AttackDamage == 4 && ordinary.Health == 5 && enemy.AttackDamage == 2 && enemy.Health == 3, "only other friendly Pirates gain +1/+1");
        var later = Summon(p, "NEW1_018");
        Core2026Proof.Check(later.AttackDamage == 3 && later.Health == 4, "later Pirate gains aura");
        PoolPilotProof.Resume(g, next => {
            ((Minion)next.IdEntityDic[source.Id]).Silence(); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(((Minion)next.IdEntityDic[pirate.Id]).AttackDamage == 2 && ((Minion)next.IdEntityDic[later.Id]).Health == 3, "Silence removes aura in original/clone/snapshot");
        });
        source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(pirate.AttackDamage == 2 && pirate.Health == 3 && later.AttackDamage == 2 && later.Health == 3, "source death removes aura");
    }, legacyPoolMember: true);
}
