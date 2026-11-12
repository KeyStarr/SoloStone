using System.Xml.Serialization;

// NPC world v1 milestone seams (spec S9): Legend numbers from the NPC field, Challengers from the band's notables,
// opponent names from NpcId. The plugin wires the seams to NpcWorldRuntime; here they are fakes.
internal static class NpcMilestoneChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static LocalProfileData Read(string path) { using var s = File.OpenRead(path); return (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(s); }
    static void Write(string path, LocalProfileData p) { using var s = File.Create(path); new XmlSerializer(typeof(LocalProfileData)).Serialize(s, p); }

    // Drive a current V14 profile to the last valid position before a target milestone.
    static void SeedBelow(string path, int target)
    {
        using var p = new LocalProfile(path);
        while (LadderRules.Apply(p.Ladder, LadderOutcome.Win, p.Ladder.Rating).After.StarIndex < target)
            Play(p, "Won", "npc.seed", p.Ladder.Rating);
        Check(p.Stars < target, "seed crossed target " + target);
    }

    static LocalMatchRecord Play(LocalProfile p, string result, string npcId, double rating)
    {
        var m = p.Begin("mage", "priest", p.History.Count, npcId, "priest", rating);
        var settled = p.Settle(m.Id, result, 10, false);
        p.AcknowledgeRewardPresentation(m.Id);
        return settled;
    }

    static void Reset() { LocalProfile.NpcLegendNumber = null; LocalProfile.NpcChallengers = null; LocalProfile.NpcDisplayName = null; }

    internal static void Run(string root)
    {
        try {
            Challengers(root);
            LegendNumbers(root);
            BrokenSeams(root);
            AbandonPaysNoLegendNumber(root);
        }
        finally { Reset(); }
        Console.WriteLine("PASS NPC milestones: band Challengers, Legend top-100 and #1 from the field, NPC names, seams never block settlement, abandon pays no Legend-number milestone");
    }

    static void Challengers(string root)
    {
        string path = Path.Combine(root, "npc-challengers.xml");
        SeedBelow(path, LadderRules.SilverTenIndex);
        var asked = new List<string>();
        LocalProfile.NpcChallengers = id => { asked.Add(id); return new[] { "npc.n.silver-ace", "npc.n.silver-ace", "npc.n.queue-regular", "bad\nid" }; };
        LocalProfile.NpcDisplayName = id => id == "npc.n.dragonyyyy" ? "Dragonyyyy" : null;
        using (var p = new LocalProfile(path)) {
            var m = Play(p, "Won", "npc.n.dragonyyyy", 1100);
            var reward = m.TierRewards.Single(r => r.MilestoneId == "floor:silver-10");
            Check(asked.SequenceEqual(new[] { "floor:silver-10" }), "Challengers asked for the reached band only");
            Check(reward.Grants.Where(g => g.Kind == LocalTierGrantKind.Challengers).Select(g => g.Ref)
                .SequenceEqual(new[] { "npc.n.silver-ace", "npc.n.queue-regular" }), "band notables granted once each, invalid ids dropped");
            Check(reward.OpponentNpcId == "npc.n.dragonyyyy" && reward.OpponentName == "Dragonyyyy", "milestone opponent named from NpcId");
        }
        using (var p = new LocalProfile(path))
            Check(p.History.Last().TierRewards.Single(r => r.MilestoneId == "floor:silver-10")
                    .Grants.Count(g => g.Kind == LocalTierGrantKind.Challengers) == 2,
                "Challengers receipt survives restart");
    }

    static void LegendNumbers(string root)
    {
        string path = Path.Combine(root, "npc-legend-numbers.xml");
        SeedBelow(path, LadderRules.LegendIndex);
        var calls = new List<(double Rating, int Index)>();
        int number = 42;
        LocalProfile.NpcLegendNumber = (rating, index) => { calls.Add((rating, index)); return number; };
        using (var p = new LocalProfile(path)) {
            // Below Legend the field is never consulted.
            var below = Play(p, "Lost", "npc.n.a", 1500);
            Check(below.Ladder.StarsAfter < 150 && calls.Count == 0, "no Legend number below Legend");
            LocalMatchRecord legend;
            do legend = Play(p, "Won", "npc.n.a", 1500); while (legend.Ladder.StarsAfter < 150);
            Check(calls.Count == 1 && calls[0].Rating == legend.Ladder.RatingAfter && calls[0].Index == p.History.Count,
                "Legend number asked with the rating after and the settled match count");
            Check(legend.TierRewards.Select(r => r.MilestoneId).SequenceEqual(new[] { "legend", "legend:top-100" }),
                "reaching Legend inside the field's top 100 pays legend and legend:top-100");
            var top = legend.TierRewards.Single(r => r.MilestoneId == "legend:top-100");
            Check(top.Grants.Single().Kind == LocalTierGrantKind.CardBack
                && top.Grants.Single().Ref == LocalShop.LadderExclusiveCardBacks["Elite"].ToString(), "top-100 elite card back");
            Check(Read(path).OwnedCardBacks.Contains(LocalShop.LadderExclusiveCardBacks["Elite"])
                && Read(path).LadderState.FloorsRewarded.SequenceEqual(new[] { "floor:bronze-5", "floor:silver-10", "floor:silver-5",
                    "floor:gold-10", "floor:gold-5", "floor:platinum-10", "floor:platinum-5", "floor:diamond-10", "floor:diamond-5",
                    "legend", "legend:top-100" }), "elite back owned and top-100 earned once");
            // Paid once; a later loss that leaves the player at #1 of the field still pays legend:1.
            var again = Play(p, "Won", "npc.n.a", 1700);
            Check(again.TierRewards.Count == 0, "top-100 is not paid twice");
            number = 1;
            var first = Play(p, "Lost", "npc.n.a", 1700);
            Check(first.TierRewards.Select(r => r.MilestoneId).SequenceEqual(new[] { "legend:1" })
                && first.TierRewards[0].Grants.Any(g => g.Kind == LocalTierGrantKind.Trophy && g.Ref == "trophy:legend-1"),
                "Legend #1 follows the field on any outcome");
        }
        using (var p = new LocalProfile(path))
            Check(p.Stars == 150 && Read(path).Trophies.Any(t => t.Id == "trophy:legend-1"), "Legend-number receipts validate after restart");
    }

    // Stubbed runtime (NotImplementedException), nonsense numbers and empty names: settlement proceeds as legacy.
    static void BrokenSeams(string root)
    {
        string path = Path.Combine(root, "npc-broken-seams.xml");
        SeedBelow(path, LadderRules.LegendIndex);
        LocalProfile.NpcLegendNumber = (rating, index) => throw new NotImplementedException();
        LocalProfile.NpcChallengers = id => throw new NotImplementedException();
        LocalProfile.NpcDisplayName = id => throw new NotImplementedException();
        using (var p = new LocalProfile(path)) {
            var legend = Play(p, "Won", "npc.n.a", 1500);
            var reward = legend.TierRewards.Single();
            Check(reward.MilestoneId == "legend" && reward.OpponentNpcId == "npc.n.a" && reward.OpponentName == null
                && reward.Grants.All(g => g.Kind != LocalTierGrantKind.Challengers), "throwing seams fall back to legacy rewards");
            LocalProfile.NpcLegendNumber = (rating, index) => 0;
            Check(Play(p, "Won", "npc.n.a", 1500).TierRewards.Count == 0, "non-positive Legend number ignored");
        }
    }

    // Quitting mid-match at Legend: RecoverInterrupted rates the loss against the stored NPC and pays no Legend-number
    // milestone (it would never be presented); the next real settlement pays them.
    static void AbandonPaysNoLegendNumber(string root)
    {
        string path = Path.Combine(root, "npc-abandon-legend.xml");
        SeedBelow(path, LadderRules.LegendIndex);
        LocalProfile.NpcLegendNumber = null; LocalProfile.NpcChallengers = null; LocalProfile.NpcDisplayName = null;
        using (var p = new LocalProfile(path)) {
            Check(Play(p, "Won", "npc.n.a", 1500).Ladder.StarsAfter == 150, "reached Legend");
            p.Begin("mage", "priest", 500, "npc.n.top", "priest", 1750);
        }
        LocalProfile.NpcLegendNumber = (rating, index) => 1;
        using (var p = new LocalProfile(path)) {
            p.RecoverInterrupted();
            var abandoned = p.History.Last();
            var saved = Read(path);
            Check(abandoned.Result == "Abandoned" && abandoned.Ladder.OpponentRating == 1750 && abandoned.TierRewards.Count == 0
                && !saved.LadderState.FloorsRewarded.Contains("legend:top-100") && !saved.LadderState.FloorsRewarded.Contains("legend:1")
                && !saved.OwnedCardBacks.Contains(LocalShop.LadderExclusiveCardBacks["Elite"]), "abandon pays no Legend-number milestone");
            var next = Play(p, "Lost", "npc.n.top", 1750);
            Check(next.TierRewards.Select(r => r.MilestoneId).SequenceEqual(new[] { "legend:top-100", "legend:1" }),
                "the next real settlement pays them");
        }
    }
}
