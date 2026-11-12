using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class CorruptConstableProof
{
    // STILL-UNPINNED: tie-break-cards/w02-constable-imp-choice.yaml (D text, owner assumption):
    // uniform existing Imp entity, stable topdeck without shuffle, then +2/+2.
    internal static void Run() => StandardSetsProof.Case("CAP_401", Unpinned_CorruptConstable);

    private static void Unpinned_CorruptConstable(Game fixture, Controller owner, string id)
    {
        foreach (bool reverse in new[] { false, true })
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1; var deck = p.Opponent.DeckZone;
            foreach (var card in deck.ToArray()) p.Opponent.SetasideZone.Add(deck.Remove(card));
            var imps = new List<Minion>();
            foreach (int marker in reverse ? new[] { 3, 2, 1 } : new[] { 1, 2, 3 })
            {
                deck.Add(Entity.FromCard(p.Opponent, Cards.FromId("CS2_029")));
                var imp = (Minion)Entity.FromCard(p.Opponent, Cards.FromId("CAP_400t2t"));
                imp.AttackDamage = marker + 3; deck.Add(imp); imps.Add(imp);
            }
            var expected = imps[g.Random.Clone().Next(imps.Count)];
            var before = deck.Where(c => c != expected).Select(c => c.Id).ToArray();
            int attack = expected.AttackDamage; var rng = g.Random.DrawCount;
            var source = Play(g, p, id);
            Core2026Proof.Check(deck.TopCard == expected && deck.Where(c => c != expected).Select(c => c.Id).SequenceEqual(before)
                && expected.AttackDamage == attack + 2 && expected.BaseHealth == 5
                && expected.AppliedEnchantments.Any(e => e.Card.Id == "CAP_401e")
                && imps.Where(m => m != expected).All(m => m.BaseHealth == 3)
                && g.Random.DrawCount == rng + 1, "seeded entity moved to top; other order preserved; exactly one buff");
            PoolPilotProof.Resume(g, branch => {
                Kill(branch, (Minion)branch.IdEntityDic[source.Id]);
                Generic.Draw(branch.Player2); branch.DeathProcessingAndAuraUpdate();
                var summoned = branch.Player1.BoardZone.Single(m => m.Card.Id == "CAP_400t2t" && m.BaseHealth == 5);
                Core2026Proof.Check(summoned.AttackDamage == attack + 2 && summoned.BaseHealth == 5,
                    "drawn exact Imp summons for caster with persistent deck buff after source dies, clone/load future");
            });
        }
        {
            var g = fixture.CloneExact(history: true); var p = g.Player1;
            var order = p.Opponent.DeckZone.Select(c => c.Id).ToArray(); var rng = g.Random.DrawCount;
            Play(g, p, id);
            Core2026Proof.Check(order.SequenceEqual(p.Opponent.DeckZone.Select(c => c.Id)) && g.Random.DrawCount == rng,
                "no enemy Imp: no movement, buff or RNG");
            foreach (var card in p.Opponent.DeckZone.ToArray()) p.Opponent.SetasideZone.Add(p.Opponent.DeckZone.Remove(card));
            Play(g, p, id);
            Core2026Proof.Check(p.Opponent.DeckZone.Count == 0 && g.Random.DrawCount == rng, "empty deck fizzles");
        }
    }
}
