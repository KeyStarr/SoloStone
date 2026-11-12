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

    // Real historical receipts, accepted by the V9 validator; migration must stay separate from catch-up.
    static void Legacy(string path, int wins, bool pendingPresentation = false)
    {
        using (var p = new LocalProfile(path)) p.SelectCardBack(0);
        var profile = Read(path);
        profile.Version = 9; profile.LadderRuleVersion = 0; profile.LadderState = null;
        profile.Matches = Enumerable.Range(0, wins).Select(i => new LocalMatchRecord {
            Id = Guid.NewGuid().ToString(), Deck = "mage", DeckHero = "HERO_08", Opponent = "priest", Seed = i,
            StartedUtc = "2026-10-24T00:00:00.0000000Z", Result = "Won", Turns = 10, RewardVersion = 2,
            RewardFacts = new LocalRewardFacts(), Gold = LocalEconomy.Reward("Won", 10, false, new LocalRewardFacts()).Gold
        }).ToList();
        profile.GoldBalance = profile.Matches.Sum(m => m.Gold);
        profile.PendingRewardPresentationId = pendingPresentation ? profile.Matches.Last().Id : null;
        Write(path, profile);
    }

    internal static void Run(string root)
    {
        string nullPath = Path.Combine(root, "milestone-null-reservation.xml");
        using (var p = new LocalProfile(nullPath)) p.SelectCardBack(0);
        var nullReservation = Read(nullPath); nullReservation.LadderState = null; Write(nullPath, nullReservation);
        using (var p = new LocalProfile(nullPath)) Check(p.PendingMilestoneCatchUp == null, "null V10 reservation uses effective ladder");

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
        NpcOpponent(root);
        AtomicSettlement(root);
        AmbiguousCustomDeck(root);
        UnresolvedLegendDeck(root);
        Climb(root);
        CatchUp(root);
        PartialCatchUp(root);
        Console.WriteLine("PASS milestone ledger: grants, climb, multi-floor, atomic settlement/catch-up, replay, restart, acknowledgement, ambiguous custom decks, Legacy Legend, partial catch-up and continued play");
    }

    static void NpcOpponent(string root)
    {
        string path = Path.Combine(root, "milestone-npc-opponent.xml");
        Legacy(path, 29);
        using (var p = new LocalProfile(path)) {
            p.AcknowledgeMilestoneCatchUp("catchup:v1");
            var match = Play(p, "Won", 100, opponent: "npc-pure-paladin");
            var reward = match.TierRewards.Single(r => r.MilestoneId == "floor:silver-10");
            Check(match.Ladder.StarsBefore == 29 && match.Ladder.StarsAfter >= 30
                && reward.OpponentNpcId == "npc-pure-paladin" && reward.OpponentName == "FatherUther"
                && reward.Grants.Any(g => g.Kind == LocalTierGrantKind.Recipe && g.Ref == "pure-paladin" && g.Count == 1)
                && Read(path).OwnedRecipes.Contains("pure-paladin"), "NPC opponent name and recipe survive milestone settlement");
        }
        using (var p = new LocalProfile(path))
            Check(p.History.Last().TierRewards.Single().OpponentName == "FatherUther"
                && Read(path).OwnedRecipes.Contains("pure-paladin"), "NPC milestone reward survives restart");
    }

    static void AmbiguousCustomDeck(string root)
    {
        foreach (bool recover in new[] { false, true }) {
            string path = Path.Combine(root, "milestone-ambiguous-custom-" + recover + ".xml");
            Legacy(path, 14);
            string matchId;
            using (var p = new LocalProfile(path)) {
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
                        && receipt.TierRewards.Count == 0 && p.Stars == 14 && p.UnopenedPacks == 0,
                        "ambiguous custom recovery succeeds without milestone grants");
                    byte[] bytes = File.ReadAllBytes(path); p.RecoverInterrupted();
                    Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "ambiguous custom recovery pays only once");
                } else {
                    var receipt = p.Settle(matchId, "Won", 10, false);
                    var reward = receipt.TierRewards.Single(); var grant = reward.Grants.Single();
                    Check(p.Pending == null && receipt.Result == "Won" && receipt.Ladder.StarsBefore == 14
                        && receipt.Ladder.StarsAfter >= 15 && receipt.Ladder.StarsAfter < 150
                        && reward.MilestoneId == "floor:bronze-5" && grant.Kind == LocalTierGrantKind.Pack
                        && grant.Ref == "STANDARD" && grant.Count == 2 && p.UnopenedPacks == 2,
                        "ambiguous custom non-Legend settlement grants Bronze 5 normally");
                }
                Check(Read(path).LegendDeck == null, "non-Legend custom match never claims a Legend deck");
            }
            byte[] saved = File.ReadAllBytes(path);
            using (var p = new LocalProfile(path))
                Check(p.Pending == null && saved.SequenceEqual(File.ReadAllBytes(path)), "ambiguous custom result validates on reload");
        }
    }

    static void UnresolvedLegendDeck(string root)
    {
        foreach (string deck in new[] { "custom", "Unknown winning deck" }) {
            string path = Path.Combine(root, "milestone-unresolved-legend-" + deck + ".xml");
            Legacy(path, 149);
            string matchId;
            using (var p = new LocalProfile(path)) {
                p.SaveDeck("First Mage", "HERO_08", LocalDecks.Get("mage").CardIds);
                matchId = p.Begin(deck, "priest", 100).Id;
                p.CreateDraft("Second Mage", "HERO_08");
                var receipt = p.Settle(matchId, "Won", 10, false);
                var reward = receipt.TierRewards.Single();
                Check(p.Stars == 150 && receipt.Ladder.LegendReached && reward.MilestoneId == "legend"
                    && reward.FirstTime && reward.OpponentNpcId == "priest" && reward.OpponentName == "Mira, the Patient"
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
                    && saved.Trophies[0].Utc == receipt.StartedUtc && saved.LadderState.FloorsRewarded.Count == 10
                    && saved.LadderState.FloorsRewarded.Last() == "legend" && p.UnopenedPacks == 26,
                    "unresolved Legend settlement persists tokens, back, trophy and earned floor: " + deck);
            }
            byte[] bytes = File.ReadAllBytes(path);
            using (var p = new LocalProfile(path))
                Check(p.Stars == 150 && p.PendingRewardPresentation.Id == matchId
                    && Read(path).LegendDeck.Legacy && bytes.SequenceEqual(File.ReadAllBytes(path)),
                    "validator accepts unresolved Legend claim without granting twice: " + deck);
        }
    }

    static void PartialCatchUp(string root)
    {
        string path = Path.Combine(root, "milestone-partial-catchup.xml");
        Legacy(path, 75);
        using (var p = new LocalProfile(path)) p.AcknowledgeMilestoneCatchUp("catchup:v1");
        var partial = Read(path);
        Check(partial.MilestoneCatchUp.Id == "catchup:v1" && partial.MilestoneCatchUp.Acknowledged,
            "partial catch-up starts with an acknowledged receipt");
        partial.LadderState.FloorsRewarded.Remove("floor:silver-5");
        partial.MilestoneCatchUp.TierRewards.RemoveAll(r => r.MilestoneId == "floor:silver-5");
        partial.UnopenedPacks.Single(x => x.PackType == "STANDARD").Count -= 2;
        Write(path, partial);
        string oldEntries = Xml(partial.MilestoneCatchUp.TierRewards.ToArray());
        using (var p = new LocalProfile(path)) {
            var receipt = p.PendingMilestoneCatchUp;
            Check(receipt != null && receipt.Id == "catchup:v1" && !receipt.Acknowledged && receipt.TierRewards.Count == 5
                && Xml(receipt.TierRewards.Take(4).ToArray()) == oldEntries,
                "partial catch-up preserves old receipt entries and resets acknowledgement");
            var appended = receipt.TierRewards.Last(); var grant = appended.Grants.Single();
            Check(appended.MilestoneId == "floor:silver-5" && appended.FirstTime
                && appended.OpponentNpcId == null && appended.OpponentName == null
                && grant.Kind == LocalTierGrantKind.Pack && grant.Ref == "STANDARD" && grant.Count == 2
                && p.UnopenedPacks == 13, "partial catch-up appends only the missing floor and pays its two packs");
            partial.UnopenedPacks.Single(x => x.PackType == "STANDARD").Count += 2;
            partial.LadderState.FloorsRewarded.Add("floor:silver-5");
            partial.MilestoneCatchUp.TierRewards.Add(new LocalTierReward { MilestoneId = "floor:silver-5", FirstTime = true,
                Grants = new List<LocalTierGrant> { new LocalTierGrant { Kind = LocalTierGrantKind.Pack, Ref = "STANDARD", Count = 2 } } });
            partial.MilestoneCatchUp.Acknowledged = false;
            Check(Xml(Read(path)) == Xml(partial), "partial catch-up changes no other saved grants or state");
        }
        byte[] bytes = File.ReadAllBytes(path);
        using (var p = new LocalProfile(path))
            Check(p.PendingMilestoneCatchUp.TierRewards.Count == 5 && p.UnopenedPacks == 13
                && bytes.SequenceEqual(File.ReadAllBytes(path)), "second partial catch-up load is byte-identical");
        ContinueAfterCatchUp(path);
    }

    static void ContinueAfterCatchUp(string path)
    {
        using (var p = new LocalProfile(path)) {
            p.AcknowledgeMilestoneCatchUp("catchup:v1");
            var paid = new List<string>(); var floors = new List<string>();
            while (p.Stars < 105) {
                var match = Play(p, "Won", p.History.Count); Ack(p, match);
                Check(match.Ladder.LadderRuleVersion >= 2, "continued play uses floor cross-check rules");
                paid.AddRange(match.TierRewards.Select(r => r.MilestoneId));
                floors.AddRange(match.Ladder.FloorsReached);
                Check(match.TierRewards.Count == match.Ladder.FloorsReached.Count,
                    "continued play pays every newly reached floor in the same match");
                byte[] before = File.ReadAllBytes(path);
                p.Settle(match.Id, "Won", 10, false);
                Check(before.SequenceEqual(File.ReadAllBytes(path)), "continued settlement replay cannot repay a floor");
            }
            Check(paid.SequenceEqual(new[] { "floor:platinum-10", "floor:platinum-5" })
                && floors.SequenceEqual(new[] { "90", "105" }), "continued play crosses and pays the next two floors exactly once");
            var loss = Play(p, "Lost", p.History.Count); Ack(p, loss);
            Check(loss.TierRewards.Count == 0 && loss.Ladder.FloorsReached.Count == 0,
                "continued loss does not repay catch-up or new floors");
            var saved = Read(path);
            Check(p.UnopenedPacks == 20 && saved.CardTokens.Epic == 1 && saved.CardTokens.Legendary == 1
                && saved.OwnedCardBacks.Contains(LocalShop.LadderExclusiveCardBacks["Platinum"]) && saved.LadderState.FloorsRewarded.Count == 7
                && saved.LadderState.FloorsRewarded.Distinct().Count() == 7
                && saved.MilestoneCatchUp.TierRewards.Count == 5 && saved.MilestoneCatchUp.Acknowledged,
                "continued play preserves catch-up and adds only the next floor grants");
        }
        byte[] bytes = File.ReadAllBytes(path);
        using (var p = new LocalProfile(path))
            Check(p.PendingMilestoneCatchUp == null && p.UnopenedPacks == 20 && bytes.SequenceEqual(File.ReadAllBytes(path)),
                "continued play validates on reload without another catch-up");

    }

    static void AtomicSettlement(string root)
    {
        string path = Path.Combine(root, "milestone-atomic.xml");
        Legacy(path, 14);
        using var p = new LocalProfile(path);
        var match = p.Begin("mage", "priest", 100);
        foreach (string stage in new[] { "write", "flush", "replace" }) {
            byte[] bytes = File.ReadAllBytes(path); string memory = Memory(p);
            p.CommitFailure = point => { if (point == stage) throw new IOException("injected " + stage); };
            Reject(() => p.Settle(match.Id, "Won", 10, false), "milestone commit " + stage);
            Check(bytes.SequenceEqual(File.ReadAllBytes(path)) && Memory(p) == memory && !File.Exists(path + ".tmp"),
                "milestone failure must leave bytes and all memory unchanged: " + stage);
            p.CommitFailure = null;
        }
        var receipt = p.Settle(match.Id, "Won", 10, false);
        Check(receipt.TierRewards.Single().MilestoneId == "floor:bronze-5" && p.UnopenedPacks == 2,
            "failed settlement retries exactly once");
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

    static void CatchUp(string root)
    {
        string path = Path.Combine(root, "milestone-catchup.xml");
        Legacy(path, 75, true);
        string pending = Read(path).PendingRewardPresentationId;
        using (var p = new LocalProfile(path)) {
            var saved = Read(path); var receipt = p.PendingMilestoneCatchUp;
            Check(saved.Version == 12 && receipt != null && receipt.Id == "catchup:v1" && !receipt.Acknowledged
                && receipt.TierRewards.Select(r => r.MilestoneId).SequenceEqual(new[] {
                    "floor:bronze-5", "floor:silver-10", "floor:silver-5", "floor:gold-10", "floor:gold-5" }), "Gold 5 catch-up receipt");
            Check(p.UnopenedPacks == 13 && saved.CardTokens.Epic == 1 && saved.DeckKits.Single().GoldCap == 3200
                && saved.OwnedCardBacks.Order().SequenceEqual(new[] { 0, LocalShop.LadderExclusiveCardBacks["Silver"], LocalShop.LadderExclusiveCardBacks["Gold"] }.Order())
                && receipt.TierRewards.SelectMany(r => r.Grants).All(g => g.Kind != LocalTierGrantKind.Recipe && g.Kind != LocalTierGrantKind.Challengers),
                "Gold 5 catch-up exact grants");
            Check(p.PendingRewardPresentation.Id == pending, "catch-up preserves pending match presentation");
            var trigger = new LocalTierRewardCatchUp();
            int queued = 0, errors = 0;
            Action presented = null;
            void Enqueue(LocalResultsReceipt r, Action done) {
                queued++; presented = done;
                Check(r.MatchId == "catchup:v1" && r.TierRewards.Count == 5 && !r.HasRank && !r.HasItems,
                    "catch-up queue carries only its saved tier grants");
            }
            trigger.Update(p, false, Enqueue, _ => errors++);
            Check(queued == 0, "catch-up waits for idle menu");
            trigger.Update(p, true, Enqueue, _ => errors++);
            trigger.Update(p, true, Enqueue, _ => errors++);
            Check(queued == 1 && p.PendingMilestoneCatchUp != null, "catch-up queues once, without premature acknowledgement");
            p.CommitFailure = _ => throw new IOException("injected catch-up presentation ack failure");
            presented();
            p.CommitFailure = null;
            Check(errors == 1 && p.PendingMilestoneCatchUp != null && p.PendingRewardPresentation.Id == pending,
                "presentation ack failure logs and preserves pending catch-up and match");
            presented();
            trigger.Update(p, true, Enqueue, _ => errors++);
            Check(queued == 1 && p.PendingMilestoneCatchUp != null, "duplicate completion cannot retry a failed ack this session");
            receipt.TierRewards.Clear(); receipt.Acknowledged = true;
            Check(p.PendingMilestoneCatchUp.TierRewards.Count == 5, "catch-up copy isolation");
        }
        byte[] original = File.ReadAllBytes(path);
        using (var p = new LocalProfile(path)) {
            Check(original.SequenceEqual(File.ReadAllBytes(path)), "second load does not grant or commit");
            Reject(() => p.AcknowledgeMilestoneCatchUp("unknown"), "unknown catch-up id");
            foreach (string stage in new[] { "write", "flush", "replace" }) {
                string memory = Memory(p);
                p.CommitFailure = point => { if (point == stage) throw new IOException("injected"); };
                Reject(() => p.AcknowledgeMilestoneCatchUp("catchup:v1"), "ack failure " + stage);
                Check(original.SequenceEqual(File.ReadAllBytes(path)) && memory == Memory(p), "ack atomicity " + stage);
                p.CommitFailure = null;
            }
            Action continued = null;
            new LocalTierRewardCatchUp().Update(p, true, (r, done) => continued = done, error => throw error);
            Check(continued != null && p.PendingMilestoneCatchUp != null, "new session can requeue failed catch-up acknowledgement");
            continued(); continued();
            Check(p.PendingMilestoneCatchUp == null && p.PendingRewardPresentation.Id == pending, "catch-up presentation acknowledgement");
            original = File.ReadAllBytes(path);
            Check(!p.AcknowledgeMilestoneCatchUp("catchup:v1") && original.SequenceEqual(File.ReadAllBytes(path)), "ack replay no commit");
        }
        using (var p = new LocalProfile(path)) Check(p.PendingMilestoneCatchUp == null && p.UnopenedPacks == 13, "ack survives restart");

        // A validated V10 reservation lets each injected failure target catch-up, not the preceding schema migration.
        string failurePath = Path.Combine(root, "milestone-catchup-failure.xml");
        Legacy(failurePath, 75);
        var reservation = Read(failurePath); reservation.Version = 12; reservation.SelectedHubTheme = "CLASSIC";
        reservation.LadderState = new LocalLadderState { StarIndex = 75, BestStarIndex = 75 };
        Write(failurePath, reservation);
        original = File.ReadAllBytes(failurePath);
        var snapshotConstructor = typeof(LocalProfile).GetConstructor(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, new[] { typeof(string), typeof(bool) }, null);
        using (var snapshot = (LocalProfile)snapshotConstructor.Invoke(new object[] { failurePath, true }))
            Check(snapshot.PendingMilestoneCatchUp == null && snapshot.UnopenedPacks == 0
                && original.SequenceEqual(File.ReadAllBytes(failurePath)), "read-only snapshot must not catch up or commit");
        foreach (string stage in new[] { "write", "flush", "replace" }) {
            Reject(() => { using var p = new LocalProfile(failurePath, point => { if (point == stage) throw new IOException("injected"); }); }, "catch-up failure " + stage);
            Check(original.SequenceEqual(File.ReadAllBytes(failurePath)) && !File.Exists(failurePath + ".tmp"), "catch-up failure preserves saved profile " + stage);
        }
        using (var p = new LocalProfile(failurePath)) Check(p.UnopenedPacks == 13 && p.PendingMilestoneCatchUp != null, "failed catch-up retries once");
        using (var p = new LocalProfile(failurePath)) Check(p.UnopenedPacks == 13, "catch-up retry restart");

        string revealPath = Path.Combine(root, "milestone-catchup-reveal.xml");
        using (var p = new LocalProfile(revealPath)) p.SelectCardBack(0);
        var reveal = Read(revealPath); reveal.GoldBalance = 100; Write(revealPath, reveal);
        using (var p = new LocalProfile(revealPath)) {
            p.BuyPack(); p.BeginNativePack("STANDARD");
        }
        reveal = Read(revealPath);
        reveal.Matches = reservation.Matches;
        reveal.LadderState = new LocalLadderState { StarIndex = 75, BestStarIndex = 75 };
        reveal.PendingRewardPresentationId = reveal.Matches.Last().Id;
        Write(revealPath, reveal);
        using (var p = new LocalProfile(revealPath)) {
            var saved = Read(revealPath);
            Check(p.PendingMilestoneCatchUp != null && p.PackRevealPending && p.PackSequence == reveal.PackSequence
                && saved.LastPack.Cards.Select(c => c.Id).SequenceEqual(reveal.LastPack.Cards.Select(c => c.Id))
                && saved.Collection.Select(c => c.Id + ":" + c.Count + ":" + c.Acquired).SequenceEqual(reveal.Collection.Select(c => c.Id + ":" + c.Count + ":" + c.Acquired))
                && p.PendingRewardPresentation.Id == reveal.PendingRewardPresentationId, "catch-up preserves pack reveal, receipt, collection and match presentation");
            p.AcknowledgeMilestoneCatchUp("catchup:v1");
            Check(p.PackRevealPending && p.PendingRewardPresentation.Id == reveal.PendingRewardPresentationId, "catch-up acknowledgement preserves other presentations");
        }

        string legendPath = Path.Combine(root, "milestone-legacy-legend.xml");
        Legacy(legendPath, 150);
        using (var p = new LocalProfile(legendPath)) {
            var saved = Read(legendPath);
            Check(saved.LegendDeck.Legacy && saved.LegendDeck.MatchId == null && saved.LegendDeck.Cards.Count == 0
                && p.UnopenedPacks == 26 && saved.CardTokens.Legendary == 4, "pre-V10 Legend gets Legacy snapshot and full rewards");
            Check(saved.Trophies.Single().MatchId == "catchup:v1" && saved.Trophies[0].Utc == "1970-01-01T00:00:00.0000000Z", "catch-up trophy fixed marker");
        }
        original = File.ReadAllBytes(legendPath);
        using (var p = new LocalProfile(legendPath)) Check(original.SequenceEqual(File.ReadAllBytes(legendPath)), "Legacy Legend catch-up idempotent");
    }
}
