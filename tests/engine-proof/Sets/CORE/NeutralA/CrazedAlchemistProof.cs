using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
internal static class CrazedAlchemistProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_059", (g, p, id) => {
        Play(g, p, id); ParkBoard(p);
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, friend, false); PoolPilotProof.EmptyHand(p);
        Play(g, p, "CS2_189", friend); Core2026Proof.Check(friend.Health == 4, "damaged target fixture");
        Play(g, p, id, friend); Core2026Proof.Check(friend.AttackDamage == 4 && friend.Health == 4, "swap uses current Health, not maximum");
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.AttackDamage == 5 && enemy.Health == 4, "enemy current Attack and Health swapped");
        Core2026Proof.Check(enemy.AppliedEnchantments.Any(e => e.Card.Id == "EX1_059e"), "exact swap enchantment");
        var zero = Summon(p.Opponent, "EX1_100"); Play(g, p, id, zero);
        Core2026Proof.Check(zero.Zone.Type == Zone.GRAVEYARD, "zero Attack becomes zero Health and dies");
        PoolPilotProof.Resume(g, next => { ((Minion)next.IdEntityDic[enemy.Id]).Silence(); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(((Minion)next.IdEntityDic[enemy.Id]).AttackDamage == 4 && ((Minion)next.IdEntityDic[enemy.Id]).Health == 5, "Silence restores base stats"); });
    }, legacyPoolMember: true);
}
