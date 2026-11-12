using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static partial class B16DiscoverExtCardsProof
{
    private static void Unpinned_SkeletonKey(Game g, Controller p, string id)
    {
        int bucket = OfflineCardPools.Candidates(g, CardClass.MAGE, OfflinePoolFilter.Discover(type: CardType.SPELL), 126116).Count;
        Console.WriteLine("Skeleton Key MAGE spell bucket=" + bucket);
        Play(g, p, id);
        var offers = DiscoverRealProof.Offers(g);
        Core2026Proof.Check(offers.Length == 4 && offers.Last().Card.Id == "JAIL_319t", "three spells plus refresh");
        var lethal = g.CloneExact(history: true); lethal.Player1.Hero.Damage = lethal.Player1.Hero.BaseHealth - 5;
        for (int seed = 0; ; seed++) {
            lethal.SetRandomSeed(seed);
            if (lethal.Random.Clone().Next(5) == 0) break;
        }
        var immune = lethal.CloneExact(history: true); immune.Player1.Hero.IsImmune = true;
        DiscoverRealProof.Answer(immune, offers.Last().Id);
        Core2026Proof.Check(immune.Player1.Hero.Health == 5 && immune.Player1.Choice != null, "prevented trap still refreshes");
        DiscoverRealProof.Answer(lethal, offers.Last().Id);
        Core2026Proof.Check(lethal.State == State.COMPLETE && lethal.Player1.Choice == null && lethal.Player1.HandZone.Count == 0,
            "lethal refresh ends without grant or next prompt");
        var repeated = g.CloneExact(history: true); repeated.Player1.Hero.IsImmune = true;
        var seen = offers.Take(3).Select(c => c.Card.Id).ToHashSet(); bool recurrence = false;
        for (int i = 0; i < 12; i++) {
            DiscoverRealProof.Answer(repeated, DiscoverRealProof.Offers(repeated).Last().Id);
            var fresh = DiscoverRealProof.Offers(repeated).Take(3).ToArray();
            Core2026Proof.Check(fresh.Select(c => c.Card.Id).Distinct().Count() == 3, "refresh offers distinct within each trio");
            recurrence |= fresh.Any(c => seen.Contains(c.Card.Id));
            foreach (var c in fresh) seen.Add(c.Card.Id);
        }
        Core2026Proof.Check(recurrence, "prior trios do not blacklist definitions");
        var random = g.Random.Clone(); bool trap = random.Next(5) == 0;
        int health = p.Hero.Health;
        DiscoverRealProof.Saved(g, next => {
            DiscoverRealProof.Answer(next, offers.Last().Id);
            Core2026Proof.Check(next.Player1.Hero.Health == health - (trap ? 5 : 0), "independent seeded twenty-percent trap");
            Core2026Proof.Check(next.HistoryFacts.Query.Discoveries().Count() == 0, "refresh is not Discover completion");
            Core2026Proof.Check(DiscoverRealProof.Offers(next).Length == 4, "refresh reopens options");
        });
        var full = g.CloneExact(history: true);
        while (!full.Player1.HandZone.IsFull) Add(full.Player1, "CS2_231");
        DiscoverRealProof.Answer(full, DiscoverRealProof.Offers(full)[0].Id);
        Core2026Proof.Check(full.Player1.HandZone.Count == 10 && full.Player1.Choice == null, "full hand burns selected spell without stall");
        int chosen = DiscoverRealProof.Offers(g)[0].Id;
        DiscoverRealProof.Answer(g, chosen);
        Core2026Proof.Check(p.Choice == null && p.HandZone.Single().Id == chosen && g.HistoryFacts.Query.Discoveries().Count() == 1,
            "one real choice grants once and completes once");
    }

}
