using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class SindragosasTriumphProof
{
    // STILL-UNPINNED: cost-rules/triumph-prevention-excess-contract.yaml;
    // E Triumph observation + C Explosive Runes analogy; owner assumption: outgoing amount minus current Health.
    internal static void Run() => StandardSetsProof.Case("CATA_978", Unpinned_SindragosasTriumph);

    private static void Unpinned_SindragosasTriumph(Game fixture, Controller owner, string id)
    {
        foreach (bool reverse in new[] { false, true })
        foreach (int prevention in new[] { 0, 1, 2 })
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            var target = Summon(prevention == 2 ? p : p.Opponent, "CS2_200"); target.Damage = 4;
            if (prevention == 1) target.HasDivineShield = true;
            if (prevention == 2) target.IsImmune = true;
            foreach (string card in reverse ? new[] { "CS2_200", "CS2_029", "CS2_182" } : new[] { "CS2_182", "CS2_029", "CS2_200" }) Add(p, card).Cost = 9;
            var hand = p.HandZone.ToArray(); var expected = hand[g.Random.Clone().Next(hand.Length)];
            var source = Play(g, p, id, target);
            foreach (var card in hand) Core2026Proof.Check(card.Cost == (card == expected ? 4 : 9)
                && card[GameTag.COST] == card.Cost, "seeded reordered hand pick; five excess independent of prevention/multiplier");
            Core2026Proof.Check(prevention == 1 ? target.Zone == target.Controller.BoardZone && !target.HasDivineShield && target.Damage == 4
                : prevention == 2 ? target.Zone == target.Controller.BoardZone && target.Damage == 4
                : target.Zone == target.Controller.GraveyardZone, "damage still obeys target prevention");
            var clone = g.CloneExact(history: true);
            Core2026Proof.Check(clone.IdEntityDic[expected.Id].Cost == 4, "permanent discount survives clone");
            var copy = Generic.Copy(p, source, expected, Zone.HAND);
            Core2026Proof.Check(copy.Cost == 4 && copy.AppliedEnchantments.Any(e => e.Card.Id == "CATA_978e"),
                "hand copy retains permanent discount and source actor without applying it twice");
            End(g); End(g); Core2026Proof.Check(expected.Cost == 4, "permanent discount survives turns/source exit");
        }
        foreach (int health in new[] { 8, 12 })
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            var target = Summon(p.Opponent, "CS2_200"); target[GameTag.HEALTH] = health;
            var card = Add(p, "CS2_029"); var rng = g.Random.DrawCount;
            Play(g, p, id, target);
            Core2026Proof.Check(card.Cost == card.Card.Cost && g.Random.DrawCount == rng, "zero excess: no discount or random choice");
        }
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            var target = Summon(p.Opponent, "CS2_231"); var rng = g.Random.DrawCount;
            Play(g, p, id, target);
            Core2026Proof.Check(p.HandZone.Count == 0 && g.Random.DrawCount == rng, "empty hand still deals damage without random choice");
            target = Summon(p.Opponent, "CS2_200"); target.Damage = 4; Summon(p, "EX1_012");
            var card = Add(p, "CS2_029"); Play(g, p, id, target);
            Core2026Proof.Check(card.Cost == 0, "Spell Damage included in excess; Cost floored at zero");
        }
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            var protectedMinion = Summon(p.Opponent, "EDR_598");
            var held = Add(p, "CS2_029");
            var spell = (Spell)Entity.FromCard(p, Cards.FromId(id)); var rng = g.Random.DrawCount;
            Generic.CastSpell(p, g, spell, protectedMinion, 0); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(protectedMinion.Damage == 0 && held.Cost == held.Card.Cost && g.Random.DrawCount == rng,
                "automatic cast with no legal target fizzles without damage, discount or RNG");
            var legal = Summon(p.Opponent, "CS2_200"); legal.Damage = 4;
            spell = (Spell)Entity.FromCard(p, Cards.FromId(id));
            Generic.CastSpell(p, g, spell, protectedMinion, 0); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(legal.Zone == p.Opponent.GraveyardZone && protectedMinion.Damage == 0 && held.Cost == 0,
                "automatic cast selects legal target instead of Elusive and applies ordinary excess");
        }
    }
}
