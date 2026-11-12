using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Tier C: https://hearthstone.wiki.gg/wiki/Advanced_rulebook#Health, H1/H2 (maximum Health changes).
// Existing legacy generation and rebalanced powers retained (owner decision, 2026-10-21).
internal static class PowerWordShieldProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_004", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, friend, false);
        friend.Damage = 3; int deck = p.DeckZone.Count;
        Play(g, p, id, friend);
        Core2026Proof.Check(friend.AttackDamage == 4 && friend.Health == 4 && friend.Damage == 3 && p.HandZone.Count == 2 && p.DeckZone.Count == deck - 1, "+2 maximum/current Health without healing; draw one");
        Core2026Proof.Check(friend.AppliedEnchantments.Single().Card.Id == "CS2_004e", "exact Shield enchantment");
        friend.Silence(); Core2026Proof.Check(friend.AttackDamage == 4 && friend.BaseHealth == 5 && friend.Health == 4, "Silence removes maximum Health buff while preserving current Health below new maximum");
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.Health == 7 && enemy.AttackDamage == 4, "enemy minion can receive buff");
        PoolPilotProof.EmptyHand(p); var source = Add(p, id); PoolPilotProof.FillHand(p);
        deck = p.DeckZone.Count; PlayEntity(g, p, source, enemy);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1 && enemy.Health == 9, "draw fills last slot while buffs stack");
        deck = p.DeckZone.Count;
        PoolPilotProof.Resolve(g, source, enemy);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1, "already-full hand burns draw while effect still resolves");
        PoolPilotProof.EmptyHand(p); PoolPilotProof.ClassDeck(p); Play(g, p, id, enemy);
        Core2026Proof.Check(p.Hero.Damage == 1 && enemy.Health == 13, "buff resolves before empty-deck fatigue");
    }, legacyPoolMember: true);
}
