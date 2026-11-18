using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

/// <summary>#141 interim: the frozen legacy Cost/N minion buckets are regrouped by each card's CURRENT cost (Cards.cs), so a Wild
/// identity row that changes a cost (Mechwarper 2 to 4) never makes a random-by-cost effect return a card of another cost. Standard and
/// Wild format buckets both. Replaced by the format-aware accessor of #141.</summary>
internal static class CostBucketProof
{
    internal static void Run()
    {
        foreach (var format in new[] { FormatType.FT_STANDARD, FormatType.FT_WILD })
        {
            var buckets = Cards.CostMinionCards(format);
            // The frozen Standard lists hold only the 2021 Standard minions; the Wild lists hold the listed Wild cards too.
            if (format == FormatType.FT_WILD)
            {
                Core2026Proof.Check(buckets[4].Any(c => c.Id == "GVG_006") && buckets[2].All(c => c.Id != "GVG_006"), "Mechwarper (4 Mana) is in Cost/4, not Cost/2: " + format);
                Core2026Proof.Check(buckets[3].Any(c => c.Id == "GVG_109") && buckets[4].All(c => c.Id != "GVG_109"), "Mini-Mage (3 Mana) moved to Cost/3: " + format);
            }
            var identity = SabberStoneCore.Loader.CardFacts.Embedded.Identity.Select(e => e.Id).ToHashSet();
            foreach (var (cost, list) in buckets)
                Core2026Proof.Check(list.Where(c => identity.Contains(c.Id)).All(c => c.Cost == cost), "every listed (identity) card sits in its current Cost/" + cost + " bucket: " + format
                    + " " + string.Join(",", list.Where(c => identity.Contains(c.Id) && c.Cost != cost).Select(c => c.Id)));
            Core2026Proof.Check(buckets.Values.SelectMany(l => l).GroupBy(c => c.Id).All(g => g.Count() == 1), "no card sits in two buckets: " + format);
        }
        // Silvermoon Portal (SummonRandomMinion(COST, 2)): only 2-cost minions, over many seeds, in both formats. Live matches run FT_WILD
        // (plugin/NativeMatch.cs), where the frozen buckets actually move: Mechwarper (GVG_006, 4 Mana now) must never appear.
        int seen = 0;
        foreach (var format in new[] { FormatType.FT_STANDARD, FormatType.FT_WILD })
            for (int seed = 1; seed <= 1000; seed++)
            {
                var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                    Player1HeroClass = CardClass.ROGUE, Player2HeroClass = CardClass.MAGE, History = true, Logging = false,
                    Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                    Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), FormatType = format });
                game.StartGame();
                var p = game.Player1;
                foreach (var opening in p.HandZone.ToArray()) { p.HandZone.Remove(opening); p.SetasideZone.Add(opening); }
                p.BaseMana = 10;
                var target = Summon(p, "CS2_182");
                Play(game, p, "KAR_077", target);
                var summoned = p.BoardZone.Where(m => m.Id != target.Id).ToArray();
                Core2026Proof.Check(summoned.Length == 1 && summoned[0].Card.Cost == 2 && summoned[0].Card.Type == CardType.MINION && summoned[0].Card.Id != "GVG_006",
                    "Silvermoon Portal summons one 2-cost minion, never Mechwarper: " + format + " " + string.Join(",", summoned.Select(m => m.Card.Id + ":" + m.Card.Cost)));
                seen++;
            }
        Console.WriteLine($"PASS cost buckets regrouped by current Cost (Standard and Wild); Silvermoon Portal {seen} games (Standard and Wild) only 2-cost minions (#141)");
    }
}
