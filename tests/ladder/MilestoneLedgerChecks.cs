using System.Xml.Serialization;

internal static class MilestoneLedgerChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static LocalProfileData Read(string path) { using var s = File.OpenRead(path); return (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(s); }
    static void Write(string path, LocalProfileData p) { using var s = File.Create(path); new XmlSerializer(typeof(LocalProfileData)).Serialize(s, p); }
    static LocalMatchRecord Play(LocalProfile p, string result, int seed, string deck = "mage", string opponent = "priest")
    {
        var m = p.Begin(deck, opponent, seed);
        return p.Settle(m.Id, result, 10, false);
    }

    static void Reject(Action action, string message)
    {
        try { action(); }
        catch (Exception e) when (e is IOException || e is InvalidOperationException) { return; }
        throw new Exception("Expected rejection: " + message);
    }
    static string Xml<T>(T p) { using var w = new StringWriter(); new XmlSerializer(typeof(T)).Serialize(w, p); return w.ToString(); }
    static string Memory(LocalProfile p) => Xml((LocalProfileData)typeof(LocalProfile).GetField("data",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(p));
    static void Ack(LocalProfile p, LocalMatchRecord m) => p.AcknowledgeRewardPresentation(m.Id);

    internal static void Run(string root)
    {
        string path = Path.Combine(root, "milestone-ledger.xml");
        using (var p = new LocalProfile(path))
        {
            LocalMatchRecord crossing;
            do {
                crossing = Play(p, "Won", p.History.Count);
                p.AcknowledgeRewardPresentation(crossing.Id);
                if (p.Stars < 15) {
                    var loss = Play(p, "Lost", p.History.Count);
                    p.AcknowledgeRewardPresentation(loss.Id);
                }
            } while (p.Stars < 15);
            Check(crossing.TierRewards.Count == 1 && crossing.TierRewards[0].MilestoneId == "floor:bronze-5"
                && crossing.TierRewards[0].Grants.Single().Kind == LocalTierGrantKind.Pack
                && crossing.TierRewards[0].Grants[0].Ref == "STANDARD" && crossing.TierRewards[0].Grants[0].Count == 2,
                "Bronze 5 must record two Standard milestone packs");
            Check(p.PackInventory.Single(x => x.PackType == "STANDARD").Count == 2
                && Read(path).LadderState.FloorsRewarded.SequenceEqual(new[] { "floor:bronze-5" }), "Bronze milestone ownership");
            byte[] bytes = File.ReadAllBytes(path);
            string memory = Memory(p);
            var replay = p.Settle(crossing.Id, "Won", 10, false);
            Check(Xml(Read(path)) == memory && bytes.SequenceEqual(File.ReadAllBytes(path))
                && replay.TierRewards[0].Grants[0].Count == 2, "settle replay must not grant twice");
            replay.TierRewards.Clear();
            Check(p.History.Last().TierRewards.Count == 1, "milestone receipt copy");
            foreach (string result in new[] { "Lost", "Tied" }) {
                var m = Play(p, result, p.History.Count); Ack(p, m);
                Check(m.TierRewards.Count == 0 && p.PackInventory.Single(x => x.PackType == "STANDARD").Count == 2,
                    "non-win must not grant milestones");
            }
            p.Begin("mage", "priest", p.History.Count);
            p.RecoverInterrupted();
            Check(p.History.Last().Result == "Abandoned" && p.History.Last().TierRewards.Count == 0, "abandon must not grant milestones");
            bytes = File.ReadAllBytes(path); p.RecoverInterrupted();
            Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "abandon recovery replay");
        }
        using (var p = new LocalProfile(path))
            Check(p.PackInventory.Single(x => x.PackType == "STANDARD").Count == 2 && p.PendingMilestoneCatchUp == null,
                "restart preserves grants without catch-up");
        Climb(root);
        Console.WriteLine("PASS milestone ledger: grants, climb, multi-floor, replay, restart");
    }

    static void Climb(string root)
    {
        string path = Path.Combine(root, "milestone-climb.xml");
        LocalMatchRecord legend;
        using (var p = new LocalProfile(path)) {
            p.SaveDeck("Winning Mage", "HERO_08", LocalDecks.Get("mage").CardIds);
            bool multiFloor = false;
            do {
                string deck = p.Stars == 149 ? "custom:" + p.CustomDeck.Id : "mage";
                legend = Play(p, "Won", p.History.Count, deck);
                multiFloor |= legend.TierRewards.Count > 1;
                if (legend.Ladder.StarsAfter < 150) Ack(p, legend);
            } while (p.Stars < 150);
            Check(multiFloor, "scripted climb must exercise a real multi-floor jump");
            var saved = Read(path);
            Check(saved.UnopenedPacks.Single(x => x.PackType == "STANDARD").Count == 26
                && saved.CardTokens.Common == 0 && saved.CardTokens.Rare == 0 && saved.CardTokens.Epic == 1 && saved.CardTokens.Legendary == 4,
                "climb exact packs and tokens");
            Check(saved.DeckKits.Count == 1 && saved.DeckKits[0].Id == "kit:floor:gold-10" && saved.DeckKits[0].GoldCap == 3200
                && saved.DeckKits[0].RedeemedPurchaseSequence == null, "climb kit");
            Check(saved.OwnedCardBacks.Order().SequenceEqual(new[] { 0, LocalShop.LadderExclusiveCardBacks["Silver"], LocalShop.LadderExclusiveCardBacks["Gold"], LocalShop.LadderExclusiveCardBacks["Platinum"], LocalShop.LadderExclusiveCardBacks["Diamond"], LocalShop.LadderExclusiveCardBacks["Legend"] }.Order()), "climb five reserved backs");
            Check(saved.Trophies.Count == 1 && saved.Trophies[0].Id == "trophy:legend" && saved.Trophies[0].MatchId == legend.Id
                && saved.Trophies[0].Utc == legend.StartedUtc, "deterministic Legend trophy");
            Check(saved.LegendDeck != null && !saved.LegendDeck.Legacy && saved.LegendDeck.MatchId == legend.Id
                && saved.LegendDeck.DeckName == "Winning Mage" && saved.LegendDeck.Hero == "HERO_08"
                && saved.LegendDeck.Cards.SequenceEqual(p.CustomDeck.Cards) && saved.LegendDeck.Sideboards.Count == 0,
                "winning custom deck snapshot");
            Check(saved.LadderState.FloorsRewarded.SequenceEqual(new[] { "floor:bronze-5", "floor:silver-10", "floor:silver-5", "floor:gold-10",
                "floor:gold-5", "floor:platinum-10", "floor:platinum-5", "floor:diamond-10", "floor:diamond-5", "legend" }), "all ten ids");
            Check(saved.Matches.SelectMany(m => m.TierRewards).All(r => r.FirstTime && r.OpponentNpcId == "priest" && r.OpponentName == "Mira, the Patient")
                && saved.Matches.SelectMany(m => m.TierRewards).SelectMany(r => r.Grants).All(g => g.Kind != LocalTierGrantKind.Recipe
                    && g.Kind != LocalTierGrantKind.Challengers), "NPC names; no source recipe mapping or challengers yet");
            Check(saved.Matches.All(m => m.Packs.Count == (m.PackReward ? 1 : 0))
                && saved.Matches.SelectMany(m => m.TierRewards).SelectMany(r => r.Grants).Where(g => g.Kind == LocalTierGrantKind.Pack).Sum(g => g.Count) == 26,
                "milestone packs stay in tier receipts; bonus pack receipt remains distinct");
            foreach (var m in saved.Matches) {
                var floorIds = m.TierRewards.Where(r => r.MilestoneId != "legend").Select(r => r.MilestoneId).ToArray();
                Check(floorIds.Length == m.Ladder.FloorsReached.Count, "every crossed floor paid in same match");
            }
            byte[] bytes = File.ReadAllBytes(path); string memory = Memory(p);
            var replay = p.Settle(legend.Id, "Won", 10, false);
            Check(replay.TierRewards.Single().MilestoneId == "legend" && bytes.SequenceEqual(File.ReadAllBytes(path)) && Memory(p) == memory,
                "Legend replay cannot grant again");
        }
        byte[] before = File.ReadAllBytes(path);
        using (var p = new LocalProfile(path)) {
            Check(p.PendingRewardPresentation.Id == legend.Id && p.PendingMilestoneCatchUp == null
                && before.SequenceEqual(File.ReadAllBytes(path)), "restart before presentation preserves every grant");
            Ack(p, legend);
            var loss = Play(p, "Lost", 999); Ack(p, loss);
            Check(loss.TierRewards.Count == 0, "Legend number absent: no prestige reward on loss");
        }
    }
}
