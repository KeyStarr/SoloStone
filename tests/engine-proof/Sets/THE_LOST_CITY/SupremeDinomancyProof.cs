using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SupremeDinomancyProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_828", (g, p, id) => {
        var beast = Summon(p, "CS2_171"); var dual = Summon(p, "CORE_LOE_039"); var none = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "CS2_171"); var hand = (Minion)Add(p, "CS2_171");
        var deck = (Minion)Entity.FromCard(p, Cards.FromId("CORE_LOE_039"), zone: p.DeckZone);
        var dead = Summon(p, "CS2_171"); Kill(g, dead); beast.Damage = 0;
        Play(g, p, id);
        Core2026Proof.Check(beast.AttackDamage == 3 && beast.Health == 3 && dual.AttackDamage == 5 && dual.Health == 6 && hand.AttackDamage == 3 && hand.Health == 3 && deck.AttackDamage == 5 && deck.Health == 6, "buff Beasts in all three zones including secondary type exactly once");
        Core2026Proof.Check(none.AttackDamage == 4 && enemy.AttackDamage == 1 && dead.AttackDamage == 1, "exclude typeless, opponents and graveyard");
        PlayEntity(g, p, hand); Core2026Proof.Check(hand.AttackDamage == 3 && hand.Health == 3, "hand buff persists onto board");
        dual.Silence(); Core2026Proof.Check(dual.AttackDamage == 3 && dual.Health == 4, "Silence removes buff; printed types remain");
        Core2026Proof.Check(beast.AppliedEnchantments.Any(e => e.Card.Id == "TLC_828e"), "pinned enchantment");
        Play(g, p, id); Core2026Proof.Check(beast.AttackDamage == 5 && beast.Health == 5, "repeated cast stacks");
    });
}
