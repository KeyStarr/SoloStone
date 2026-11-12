using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class JailhouseManastormProof
{
    // cost-sampling/manastorm-above-ten.yaml PINNED, A Blizzard 36.2 correction.
    // aftercast-paid-cost/effect-cast-qualification.yaml STILL-UNPINNED, C rulebook/Summoning Stone;
    // owner assumption: committed numeric Cost, player casts only, including Health payment.
    internal static void Run() => StandardSetsProof.Case("JAIL_122", Unpinned_JailhouseManastorm);

    private static void Unpinned_JailhouseManastorm(Game fixture, Controller owner, string id)
    {
        foreach (int cost in new[] { 0, 2, 5, 11, 12, 99 })
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            var source = (Minion)Play(g, p, id); source.Silence(); Kill(g, source);
            var controllerEffect = p.AppliedEnchantments.Single(e => e.Card.Id == "JAIL_122e5");
            var spell = (Spell)Add(p, "CS2_029"); spell.Cost = cost;
            if (cost >= 10 && cost < 99) CostRules.AddPaymentOverride(spell, CostPaymentKind.Health);
            var candidates = OfflineCardPools.Candidates(g, p.HeroClass,
                OfflinePoolFilter.Random(type: CardType.MINION, minCost: cost, maxCost: cost), controllerEffect.Card.AssetId);
            var expected = candidates.Count == 0 ? null : candidates[g.Random.Clone().Next(candidates.Count)];
            if (cost == 99)
            {
                Generic.CastSpell(p, g, spell, p.Opponent.Hero, 0); g.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(p.BoardZone.Count == 0, "automatic cast does not qualify, irrespective of source Cost");
                continue;
            }
            p.BaseMana = 10; p.UsedMana = 0;
            PoolPilotProof.Resume(g, branch => {
                var card = branch.IdEntityDic[spell.Id]; var recipient = branch.Player1;
                var action = recipient.Options().Single(t => t.Source?.Id == card.Id && t.Target?.Id == recipient.Opponent.Hero.Id);
                Core2026Proof.Check(branch.Process(action), "qualifying player spell accepted");
                if (expected == null) Core2026Proof.Check(recipient.BoardZone.Count == 0, "empty same-Cost bucket has no substitute");
                else
                {
                    var effect = recipient.AppliedEnchantments.Single(e => e.Card.Id == "JAIL_122e5");
                    var made = recipient.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == effect.Id);
                    Core2026Proof.Check(made.Card.Id == expected.Id && made.Card.Type == CardType.MINION
                        && made.Card.Cost == cost && made.Card.AssetId != effect.Card.AssetId
                        && made.AttackDamage == made.Card.ATK && made.BaseHealth == made.Card.Health,
                        $"seeded exact printed-Cost minion: expected={expected.Id}/{cost}, actual={made.Card.Id}/{made.Card.Cost}, attack={made.AttackDamage}/{made.Card.ATK}, health={made.BaseHealth}/{made.Card.Health}");
                }
            });
        }
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            var source = (Minion)Play(g, p, id); var spell = Add(p, "CS2_029");
            CostRules.AddNextCardRule(p, cardType: CardType.SPELL, setCost: 2);
            PlayEntity(g, p, spell, p.Opponent.Hero);
            Core2026Proof.Check(p.BoardZone.Single(m => m.Id != source.Id).Card.Cost == 2,
                "consumed discount uses committed two, not reset printed four");
            End(g); int board = p.BoardZone.Count;
            Play(g, p.Opponent, "CS2_029", p.Hero);
            Core2026Proof.Check(p.BoardZone.Count == board, "opponent casts do not summon");
            End(g); StandardSetsProof.ClearBoard(g, p);
            Summon(p, id); Play(g, p, "CS2_029", p.Opponent.Hero);
            Core2026Proof.Check(p.BoardZone.Count == 2, "summoning another Manastorm does not install another Battlecry effect");
        }
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1; Play(g, p, id);
            LostCityPoolProof.FullBoard(p); var rng = g.Random.DrawCount;
            Play(g, p, "CS2_029", p.Opponent.Hero);
            Core2026Proof.Check(p.BoardZone.SlotCount == 7 && g.Random.DrawCount == rng,
                "full mixed board suppresses summon without random sampling");
        }
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            Play(g, p, id); Play(g, p, id);
            var effect = p.AppliedEnchantments.Single(e => e.Card.Id == "JAIL_122e5");
            Core2026Proof.Check(effect.ScriptTag1 == 2, "two Battlecries retain two permanent effect stacks");
            Play(g, p, "CS2_029", p.Opponent.Hero);
            Core2026Proof.Check(p.BoardZone.Count(m => m[GameTag.DISPLAYED_CREATOR] == effect.Id) == 2,
                "each stack summons once for one player spell");
            End(g); Play(g, p.Opponent, "CORE_EX1_287"); End(g);
            int board = p.BoardZone.SlotCount; var rng = g.Random.DrawCount;
            Play(g, p, "CS2_029", p.Opponent.Hero);
            Core2026Proof.Check(p.BoardZone.SlotCount == board && p.Opponent.SecretZone.Count == 0 && g.Random.DrawCount == rng,
                "countered player spell produces no after-cast summon or random selection");
        }
    }
}
