using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz; retain existing current-text power.
internal static class CoreMarkOfTheWildProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_009", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, friend, enemyOnly: false); PoolPilotProof.EmptyHand(p);
        Play(g, p, id, friend);
        Core2026Proof.Check(friend.AttackDamage == 6 && friend.Health == 8 && friend.HasTaunt && enemy.AttackDamage == 4, "friendly +2/+3 and Taunt only");
        Core2026Proof.Check(friend.AppliedEnchantments.Any(e => e.Card.Id == "CS2_009e"), "exact Mark enchantment");
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.AttackDamage == 6 && enemy.Health == 8 && enemy.HasTaunt, "enemy target also receives buff");
        friend.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(friend.AttackDamage == 4 && friend.Health == 5 && !friend.HasTaunt, "Silence removes all granted stats and Taunt");
        ParkBoard(p); ParkBoard(p.Opponent); var spell = Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id), "cannot target heroes or play without minions");
    }, legacyPoolMember: true);
}
