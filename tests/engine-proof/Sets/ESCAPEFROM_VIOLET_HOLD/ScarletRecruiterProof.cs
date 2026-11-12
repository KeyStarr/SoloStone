using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static SilenceKeywordsProof;

internal static class ScarletRecruiterProof
{
    private static IPlayable[] Deck(Controller p, params string[] ids)
    {
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        return ids.Select(id => Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone)).ToArray();
    }

    private static Minion[] Recruited(Controller p, IPlayable source) => p.BoardZone.Where(m =>
        m.AppliedEnchantments?.Any(e => e.Card.Id == "JAIL_516e" && e.Creator?.Id == source.Id) == true).ToArray();

    internal static void Run() => StandardSetsProof.Case("JAIL_516", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_120");
        var opponentDeck = p.Opponent.DeckZone.Count;
        var fresh = g.CloneExact(history: true);

        // [location, left, Recruiter, outputs..., right]; eligible: current Cost <= 2 minions only.
        var deck = Deck(p, "CS2_231", "CS2_120", "CS2_182", "CS2_025", "CS2_172", "CS2_124");
        deck[2].Cost = 2; deck[4].Cost = 3;
        var eligible = new[] { deck[0].Id, deck[1].Id, deck[2].Id };
        var expected = new Dictionary<int, (string CardId, int Attack, int Health)> {
            [deck[0].Id] = ("CS2_231", 1, 1), [deck[1].Id] = ("CS2_120", 2, 3), [deck[2].Id] = ("CS2_182", 4, 5) };
        var location = LocationProofs.Location(p);
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var exact = g.CloneExact(history: true);
        var source = StandardSetsProof.PlayAt(g, p, id, 2);
        StandardSetsProof.PlayAt(exact, exact.Player1, id, 2);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(exact), "deterministic picks on an exact clone");
        var outputs = new[] { p.BoardZone.GetSlot(3), p.BoardZone.GetSlot(4) }.Cast<Minion>().ToArray();
        Core2026Proof.Check(p.BoardZone.Slots.Select(e => e.Id).SequenceEqual(new[] { location.Id, left.Id, source.Id, outputs[0].Id, outputs[1].Id, right.Id }),
            "two outputs right of the Recruiter in creation order, counting the location slot");
        Core2026Proof.Check(outputs.All(m => eligible.Contains(m.Id)) && outputs[0].Id != outputs[1].Id && p.DeckZone.Count == 4
            && outputs.All(m => m.Zone == p.BoardZone && m.Controller == p), "two distinct own deck minions with current Cost <= 2 left the deck");
        Core2026Proof.Check(p.DeckZone.Any(c => c.Id == deck[3].Id) && p.DeckZone.Any(c => c.Id == deck[4].Id) && p.DeckZone.Any(c => c.Id == deck[5].Id)
            && p.Opponent.DeckZone.Count == opponentDeck, "spell, Cost-3 minion and opponent deck untouched");
        Core2026Proof.Check(Recruited(p, source).Length == 2 && outputs.All(m => m.IsRush && CanAttack(m, enemy) && !CanAttack(m, p.Opponent.Hero)),
            "both get pinned JAIL_516e Rush: minions only on entry turn");
        Core2026Proof.Check(!((Minion)source).IsRush && left.AppliedEnchantments?.Any(e => e.Card.Id == "JAIL_516e") != true, "Recruiter and neighbours not given Rush");
        Core2026Proof.Check(outputs.All(m => (m.Card.Id, m.AttackDamage, m.Health) == expected[m.Id]), "recruited identities and printed stats preserved");
        AttackOptionsProof.Lifecycle(g, outputs[0], enemy, rush: true);
        Kill(g, (Minion)source);
        End(g); End(g);
        Core2026Proof.Check(outputs.All(m => m.IsRush && m.AppliedEnchantments.Any(e => e.Card.Id == "JAIL_516e")),
            "Rush persists after Recruiter dies and turns end");

        // One eligible: one summon, no random event needed; none eligible: nothing.
        foreach (int count in new[] { 0, 1 })
        {
            var branch = fresh.CloneExact(history: true); var bp = branch.Player1;
            var branchDeck = Deck(bp, count == 1 ? new[] { "CS2_120", "CS2_182" } : new[] { "CS2_182", "CS2_025" });
            var played = StandardSetsProof.PlayAt(branch, bp, id, 0);
            Core2026Proof.Check(Recruited(bp, played).Length == count && bp.BoardZone.Count == 1 + count && bp.DeckZone.Count == 2 - count,
                $"{count} eligible: {count} summoned");
            if (count == 1) Core2026Proof.Check(bp.BoardZone.GetSlot(1).Id == branchDeck[0].Id, "single output right of the Recruiter");
        }

        // Board capacity: one free slot after the Recruiter summons one; a full board summons none.
        foreach (int fill in new[] { 5, 6 })
        {
            var branch = fresh.CloneExact(history: true); var bp = branch.Player1;
            Deck(bp, "CS2_231", "CS2_120", "CS2_172");
            LocationProofs.Location(bp);
            for (int i = 1; i < fill; i++) Summon(bp, "CS2_182");
            var played = StandardSetsProof.PlayAt(branch, bp, id, 0);
            int count = 6 - fill;
            Core2026Proof.Check(Recruited(bp, played).Length == count && bp.BoardZone.SlotCount == 1 + fill + count
                && bp.BoardZone.SlotCount <= 7 && bp.DeckZone.Count == 3 - count, $"board fill {fill}: {count} summoned");
        }

        // Summoned (not played) Recruiter has no Battlecry; enemy Recruiter recruits from the enemy deck.
        var quiet = fresh.CloneExact(history: true);
        Deck(quiet.Player1, "CS2_231", "CS2_120");
        var summoned = Summon(quiet.Player1, id);
        Core2026Proof.Check(quiet.Player1.DeckZone.Count == 2 && Recruited(quiet.Player1, summoned).Length == 0, "summon is not a play");
    });
}
