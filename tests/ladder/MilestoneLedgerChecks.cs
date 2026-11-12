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
                if (File.Exists(path)) File.Copy(path, path + ".prev", true);
                crossing = Play(p, "Won", p.History.Count);
                p.AcknowledgeRewardPresentation(crossing.Id);
                if (p.Stars < 15) {
                    var loss = Play(p, "Lost", p.History.Count);
                    p.AcknowledgeRewardPresentation(loss.Id);
                }
            } while (p.Stars < 15);
            File.Copy(path + ".prev", path + ".first-floor", true); // the save just before the lone Bronze 5 crossing
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
        Branches(root, path + ".first-floor", Path.Combine(root, "milestone-climb.xml.silver"), Path.Combine(root, "milestone-climb.xml.149"));
        Console.WriteLine("PASS milestone ledger: grants, climb, multi-floor, replay, restart");
    }

    static void Climb(string root)
    {
        string path = Path.Combine(root, "milestone-climb.xml");
        LocalMatchRecord legend;
        using (var p = new LocalProfile(path)) {
            p.SaveDeck("Winning Mage", "HERO_08", LocalDecks.Get("mage").CardIds);
            bool multiFloor = false;
            // Copies of the save taken before real wins: the branch cases below replay one match from them.
            string prev = path + ".prev", silver = path + ".silver", at149 = path + ".149";
            do {
                File.Copy(path, prev, true);
                if (p.Stars == 149) File.Copy(path, at149, true);
                string deck = p.Stars == 149 ? "custom:" + p.CustomDeck.Id : "mage";
                legend = Play(p, "Won", p.History.Count, deck);
                multiFloor |= legend.TierRewards.Count > 1;
                if (legend.TierRewards.Any(r => r.MilestoneId == "floor:silver-10") && !File.Exists(silver)) File.Copy(prev, silver);
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

    // One real match replayed from a copy of the climb save. Each case is a path every save reaches.
    static string Branch(string root, string from, string name)
    {
        string path = Path.Combine(root, "milestone-branch-" + name + ".xml");
        File.Copy(from, path, true);
        return path;
    }

    static void Branches(string root, string firstFloor, string silver, string at149)
    {
        // The recipe from an NPC opponent's deck is granted with the floor and survives a restart.
        string path = Branch(root, silver, "npc");
        using (var p = new LocalProfile(path)) {
            var match = Play(p, "Won", p.History.Count, opponent: "npc-pure-paladin");
            var reward = match.TierRewards.Single(r => r.MilestoneId == "floor:silver-10");
            Check(reward.OpponentNpcId == "npc-pure-paladin" && reward.OpponentName == "FatherUther"
                && reward.Grants.Any(g => g.Kind == LocalTierGrantKind.Recipe && g.Ref == "pure-paladin" && g.Count == 1)
                && Read(path).OwnedRecipes.Contains("pure-paladin"), "NPC opponent name and recipe survive milestone settlement");
        }
        using (var p = new LocalProfile(path))
            Check(p.History.Last().TierRewards.Single(r => r.MilestoneId == "floor:silver-10").OpponentName == "FatherUther"
                && Read(path).OwnedRecipes.Contains("pure-paladin"), "NPC milestone reward survives restart");

        // A commit failure while a floor is crossed leaves bytes and memory untouched; the retry pays once.
        path = Branch(root, firstFloor, "atomic");
        using (var p = new LocalProfile(path)) {
            int packs = p.UnopenedPacks;
            var match = p.Begin("mage", "priest", p.History.Count);
            foreach (string stage in new[] { "write", "flush", "replace" }) {
                byte[] bytes = File.ReadAllBytes(path); string memory = Memory(p);
                p.CommitFailure = point => { if (point == stage) throw new IOException("injected " + stage); };
                try { p.Settle(match.Id, "Won", 10, false); throw new Exception("Expected failure: milestone commit " + stage); }
                catch (IOException) { }
                Check(bytes.SequenceEqual(File.ReadAllBytes(path)) && Memory(p) == memory && !File.Exists(path + ".tmp"),
                    "milestone failure must leave bytes and all memory unchanged: " + stage);
                p.CommitFailure = null;
            }
            var receipt = p.Settle(match.Id, "Won", 10, false);
            Check(receipt.TierRewards.Single().MilestoneId == "floor:bronze-5" && p.UnopenedPacks == packs + 2,
                "failed settlement retries exactly once");
        }

        // An ambiguous "custom" deck on a pending match survives a reload; it never claims a Legend deck.
        foreach (bool recover in new[] { false, true }) {
            path = Branch(root, firstFloor, "ambiguous-" + recover);
            string matchId; int packs;
            using (var p = new LocalProfile(path)) {
                packs = p.UnopenedPacks;
                p.SaveDeck("First Mage", "HERO_08", LocalDecks.Get("mage").CardIds);
                matchId = p.Begin("custom", "priest", 100).Id;
                // Begin requires one complete deck; the library can grow while that match is pending.
                p.CreateDraft("Second Mage", "HERO_08");
            }
            using (var p = new LocalProfile(path)) {
                Check(p.CustomDecks.Count == 2 && p.Pending.Id == matchId && p.Pending.Deck == "custom",
                    "ambiguous custom pending match survives reload");
                if (recover) {
                    p.RecoverInterrupted();
                    var receipt = p.History.Last();
                    Check(p.Pending == null && receipt.Id == matchId && receipt.Result == "Abandoned"
                        && receipt.TierRewards.Count == 0 && p.UnopenedPacks == packs,
                        "ambiguous custom recovery succeeds without milestone grants");
                    byte[] bytes = File.ReadAllBytes(path); p.RecoverInterrupted();
                    Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "ambiguous custom recovery pays only once");
                } else {
                    var receipt = p.Settle(matchId, "Won", 10, false);
                    var reward = receipt.TierRewards.Single(); var grant = reward.Grants.Single();
                    Check(p.Pending == null && receipt.Result == "Won" && reward.MilestoneId == "floor:bronze-5"
                        && grant.Kind == LocalTierGrantKind.Pack && grant.Ref == "STANDARD" && grant.Count == 2 && p.UnopenedPacks == packs + 2,
                        "ambiguous custom non-Legend settlement grants Bronze 5 normally");
                }
                Check(Read(path).LegendDeck == null, "non-Legend custom match never claims a Legend deck");
            }
            byte[] saved = File.ReadAllBytes(path);
            using (var p = new LocalProfile(path))
                Check(p.Pending == null && saved.SequenceEqual(File.ReadAllBytes(path)), "ambiguous custom result validates on reload");
        }

        // Legend reached with a deck that cannot be resolved: an empty Legacy claim plus every Legend grant.
        foreach (string deck in new[] { "custom", "Unknown winning deck" }) {
            path = Branch(root, at149, "legend-" + deck.Replace(' ', '-'));
            string matchId;
            using (var p = new LocalProfile(path)) {
                p.SaveDeck("First Mage", "HERO_08", LocalDecks.Get("mage").CardIds);
                matchId = p.Begin(deck, "priest", 100).Id;
                p.CreateDraft("Second Mage", "HERO_08");
                var receipt = p.Settle(matchId, "Won", 10, false);
                var reward = receipt.TierRewards.Single();
                Check(p.Stars == 150 && receipt.Ladder.LegendReached && reward.MilestoneId == "legend"
                    && reward.Grants.Select(g => g.Kind + ":" + g.Ref + ":" + g.Count).SequenceEqual(new[] {
                        "CardBack:" + LocalShop.LadderExclusiveCardBacks["Legend"] + ":1", "Token:LEGENDARY:1", "Trophy:trophy:legend:1", "LegendDeck:Legacy:1" }),
                    "unresolved winning deck retains every available Legend grant: " + deck);
                var saved = Read(path);
                Check(saved.LegendDeck != null && saved.LegendDeck.Legacy && saved.LegendDeck.MatchId == null
                    && saved.LegendDeck.DeckName == null && saved.LegendDeck.Hero == null
                    && saved.LegendDeck.Cards.Count == 0 && saved.LegendDeck.Sideboards.Count == 0,
                    "unresolved winning deck saves an empty Legacy claim: " + deck);
                Check(saved.CardTokens.Legendary == 4 && saved.OwnedCardBacks.Contains(LocalShop.LadderExclusiveCardBacks["Legend"])
                    && saved.Trophies.Single().Id == "trophy:legend" && saved.Trophies[0].MatchId == matchId
                    && saved.LadderState.FloorsRewarded.Count == 10 && saved.LadderState.FloorsRewarded.Last() == "legend" && p.UnopenedPacks == 26,
                    "unresolved Legend settlement persists tokens, back, trophy and earned floor: " + deck);
            }
            byte[] bytes = File.ReadAllBytes(path);
            using (var p = new LocalProfile(path))
                Check(p.Stars == 150 && p.PendingRewardPresentation.Id == matchId
                    && Read(path).LegendDeck.Legacy && bytes.SequenceEqual(File.ReadAllBytes(path)),
                    "validator accepts unresolved Legend claim without granting twice: " + deck);
        }
    }
}
