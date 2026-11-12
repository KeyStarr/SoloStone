using SabberStoneCore.Actions;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class FloatingLessonsProof
{
    // STILL-UNPINNED: cost-sampling/floating-lessons-{additive,cost}.yaml (D export, E example);
    // owner assumption: additive, preserve damage, committed modified Cost; partial effect without hand minions.
    internal static void Run() => StandardSetsProof.Case("BE_094", Unpinned_FloatingLessons);

    private static void Unpinned_FloatingLessons(Game fixture, Controller owner, string id)
    {
        foreach (int cost in new[] { 0, 1, 4 })
        foreach (bool reverse in new[] { false, true })
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            var target = Summon(p.Opponent, "CS2_200"); target.Damage = 2;
            foreach (string card in reverse ? new[] { "CS2_182", "CS2_200", "CS2_231" } : new[] { "CS2_231", "CS2_200", "CS2_182" }) Add(p, card);
            var minions = p.HandZone.OfType<Minion>().ToArray();
            var spell = Add(p, id); spell.Cost = cost;
            var expected = minions[g.Random.Clone().Next(minions.Length)];
            PlayEntity(g, p, spell, target);
            int amount = 2 * cost;
            Core2026Proof.Check(target.AttackDamage == target.Card.ATK + amount && target.BaseHealth == target.Card.Health + amount
                && target.Damage == 2, "additive board buff preserves damage and samples committed Cost");
            foreach (var minion in minions)
                Core2026Proof.Check(minion.AttackDamage == minion.Card.ATK + (minion == expected ? amount : 0)
                    && minion.BaseHealth == minion.Card.Health + (minion == expected ? amount : 0),
                    "seeded physical hand entity selected, including reordered hand");
            var clone = g.CloneExact(history: true); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
            Core2026Proof.Check(HsSim.Canonical.State(g) == HsSim.Canonical.State(clone)
                && HsSim.Canonical.State(g) == HsSim.Canonical.State(loaded), "board/hand buff state survives exact clone and snapshot");
            target.Silence();
            Core2026Proof.Check(target.AttackDamage == target.Card.ATK && target.BaseHealth == target.Card.Health,
                "Silence removes board grant");
            var copy = (Minion)Generic.Copy(p, spell, expected, Zone.HAND);
            Core2026Proof.Check(copy.AttackDamage == expected.AttackDamage && copy.BaseHealth == expected.BaseHealth,
                "hand copy preserves granted stats");
        }
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            var target = Summon(p, "CS2_200"); Add(p, "CS2_029");
            var rng = g.Random.DrawCount; Play(g, p, id, target);
            Core2026Proof.Check(target.AttackDamage == 8 && target.BaseHealth == 9 && g.Random.DrawCount == rng,
                "no hand minion: board still gains +2/+2, no random pick");
            var spell = Add(p, id);
            CostRules.AddNextCardRule(p, cardType: CardType.SPELL, setCost: 0);
            PlayEntity(g, p, spell, target);
            Core2026Proof.Check(target.AttackDamage == 8, "consumed zero-Cost rule is retained, no fallback to printed one");
        }
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            var target = Summon(p, "CS2_200");
            var spell = (Spell)Entity.FromCard(p, Cards.FromId(id)); spell.Cost = 4;
            Generic.CastSpell(p, g, spell, target, 0); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(target.AttackDamage == 14 && target.BaseHealth == 15, "automatic cast uses current modified Cost");
            spell = (Spell)Add(p, id); spell.Cost = 4;
            CostRules.AddPaymentOverride(spell, CostPaymentKind.Health);
            int health = p.Hero.Health;
            var task = p.Options().Single(t => t.Source?.Id == spell.Id && t.Target?.Id == target.Id);
            Core2026Proof.Check(g.Process(task) && p.Hero.Health == health - 4 && target.AttackDamage == 22,
                "Health payment retains numeric four-Cost buff");
        }
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            var held = (Minion)Add(p, "CS2_200");
            var spell = (Spell)Entity.FromCard(p, Cards.FromId(id)); var rng = g.Random.DrawCount;
            Generic.CastSpell(p, g, spell, null, 0); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(held.AttackDamage == held.Card.ATK && g.Random.DrawCount == rng,
                "automatic cast with no legal board target fizzles before independent hand buff");
            var protectedMinion = Summon(p.Opponent, "EDR_598");
            var legal = Summon(p.Opponent, "CS2_200");
            spell = (Spell)Entity.FromCard(p, Cards.FromId(id));
            Generic.CastSpell(p, g, spell, protectedMinion, 0); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(legal.AttackDamage == 8 && protectedMinion.AttackDamage == protectedMinion.Card.ATK
                && held.AttackDamage == 8, "automatic invalid target replaced by legal minion, then normal hand pick");
        }
    }
}
