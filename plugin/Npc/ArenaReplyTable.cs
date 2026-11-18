using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

// Arena (#6) reply prior. CardFrequencies predicts what an opponent may hold from constructed decks: at most 2 copies, weight .001 for
// every card no list plays. A drafted deck follows neither (no copy limit, cards no constructed list plays are common), so an Arena
// match samples the opponent's unknown hand from this table instead: the average copies per deck over the drafted Arena NPC decks
// (NpcWorldHooks.ArenaPopulation), per class.
//   weight(card, class) = (copies in the decks of that class + K * pooled rate) / (decks of that class + K) + floor
// The pooled rate is the card's copies over every deck whose class may hold it, so a neutral card is read from all 200 decks and a class
// card from its class decks alone; K shrinks a class's few decks (about 20) toward it. The floor keeps every legal card possible, as in
// CardFrequencies. Population knowledge only (the public draft rules and lists): no hand, deck or play of the actual opponent is an input.
// Used only by profiles whose BrainOptions carry ReplyPrior.Arena, which NpcWorldRuntime.Begin sets for an Arena encounter; Ranked,
// Casual and the golden decks never reach it. A class without Arena decks, or no installed population, falls back to the class prior.
public static class ArenaReplyTable
{
    internal const double Shrink = 10;
    internal const double Floor = .001;
    private static readonly object Gate = new object();
    private static Func<IReadOnlyList<PopulationDeck>> cachedSource;
    private static Dictionary<CardClass, IReadOnlyList<CardFrequency>> tables;

    public static IReadOnlyList<CardFrequency> For(CardClass heroClass)
    {
        lock (Gate) {
            var source = NpcWorldHooks.ArenaPopulation;
            if (source == null) return CardFrequencies.ForOrEmpty(heroClass);
            if (tables == null || source != cachedSource) {
                // A population the table cannot read (an unknown card id) leaves the class prior in place, once, instead of throwing in search.
                try { tables = Build(source); } catch (Exception) { tables = new Dictionary<CardClass, IReadOnlyList<CardFrequency>>(); }
                cachedSource = source;
            }
            IReadOnlyList<CardFrequency> table;
            return tables.TryGetValue(heroClass, out table) ? table : CardFrequencies.ForOrEmpty(heroClass);
        }
    }

    // Number of Arena decks the current table reads (0: none installed, the class prior applies); research and tests.
    internal static int DeckCount() { lock (Gate) return NpcWorldHooks.ArenaPopulation?.Invoke()?.Count ?? 0; }

    internal static Dictionary<CardClass, IReadOnlyList<CardFrequency>> Build(Func<IReadOnlyList<PopulationDeck>> source)
    {
        IReadOnlyList<PopulationDeck> decks = null;
        try { decks = source(); } catch (Exception) { decks = null; }
        var result = new Dictionary<CardClass, IReadOnlyList<CardFrequency>>();
        if (decks == null || decks.Count == 0) return result;
        var classes = decks.Select(d => d.Class).Distinct().OrderBy(c => (int)c).ToArray();
        var perClass = classes.ToDictionary(c => c, c => decks.Count(d => d.Class == c));
        var counts = new SortedDictionary<string, Dictionary<CardClass, int>>(StringComparer.Ordinal);
        var maxCopies = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var deck in decks)
            foreach (var group in deck.Cards.GroupBy(id => id, StringComparer.Ordinal)) {
                if (!counts.TryGetValue(group.Key, out var byClass)) counts[group.Key] = byClass = new Dictionary<CardClass, int>();
                byClass.TryGetValue(deck.Class, out int old); byClass[deck.Class] = old + group.Count();
                maxCopies.TryGetValue(group.Key, out int most); maxCopies[group.Key] = Math.Max(most, group.Count());
            }
        // Classes that may hold each card, once per card (rate denominators).
        var holders = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string id in counts.Keys) {
            var card = Cards.FromId(id);
            holders[id] = classes.Where(c => LocalDecks.IsAllowedInDeck(card, c)).Sum(c => perClass[c]);
        }
        foreach (var heroClass in classes) {
            var prior = CardFrequencies.ForOrEmpty(heroClass);
            var weights = new SortedDictionary<string, double>(StringComparer.Ordinal);
            foreach (var f in prior) weights[f.CardId] = 0;
            foreach (var pair in counts) {
                var card = Cards.FromId(pair.Key);
                if (!LocalDecks.IsAllowedInDeck(card, heroClass)) continue;
                int total = pair.Value.Values.Sum(), own;
                pair.Value.TryGetValue(heroClass, out own);
                double pooled = holders[pair.Key] == 0 ? 0 : (double)total / holders[pair.Key];
                weights[pair.Key] = (own + Shrink * pooled) / (perClass[heroClass] + Shrink);
            }
            var rows = new List<CardFrequency>();
            foreach (var pair in weights) {
                var card = Cards.FromId(pair.Key);
                int copies = Math.Max(card.Rarity == Rarity.LEGENDARY ? 1 : 2, maxCopies.TryGetValue(pair.Key, out int most) ? most : 0);
                rows.Add(new CardFrequency(pair.Key, pair.Value + Floor, copies));
            }
            result[heroClass] = GoldenData.Copy(rows);
        }
        return result;
    }
}
