using System.Xml.Serialization;

internal static class V10ReservationTests
{
    internal static void Run(string root)
    {
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        LocalProfileData Read(string path) { using var file = File.OpenRead(path); return (LocalProfileData)serializer.Deserialize(file)!; }
        void Write(string path, LocalProfileData data) { using var file = File.Create(path); serializer.Serialize(file, data); }
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        void Reject(Action action) { try { action(); } catch (InvalidOperationException) { return; } catch (IOException) { return; } throw new Exception("Malformed reservation accepted"); }
        string path = Path.Combine(root, "reservations.xml");
        using (var p = new LocalProfile(path)) {
            while (p.Stars < 60) {
                var match = p.Begin("mage", "priest", p.History.Count);
                p.Settle(match.Id, "Won", 10, false);
                p.AcknowledgeRewardPresentation(match.Id);
            }
        }
        var data = Read(path);
        Check(data.Version == 15 && data.DeckKits.Single().Id == "kit:floor:gold-10"
            && data.DeckKits[0].GoldCap == 3200 && data.MilestoneCatchUp == null,
            "current profile earns a receipt-backed deck kit");
        data.GoldBalance = 10000;
        WelcomeGiftTests.OwnGrantOnly(data);
        data.CraftSequence = 1;
        data.LastCraft = new LocalCraftReceipt { Sequence = 1, Op = LocalCraftOp.TokenCraft, TokenRarity = LocalTokenRarity.Common, TokenDelta = -1,
            Cards = new List<LocalCraftCard> { new LocalCraftCard { CardId = "CS2_029", CountDelta = 1 } }, Undoable = true };
        Write(path, data);
        using (var p = new LocalProfile(path)) p.SelectCardBack(0); // Snapshot must preserve every reservation.
        var saved = Read(path);
        string Xml(LocalProfileData value) { using var text = new StringWriter(); serializer.Serialize(text, value); return text.ToString(); }
        Check(Xml(saved) == Xml(data), "snapshot and reload preserve every reserved field");
        Check(saved.DeckKits.Single().GoldCap == 3200 && saved.MilestoneCatchUp == null
            && saved.LastCraft.TokenDelta == -1 && saved.LastCraft.TokenRarity == LocalTokenRarity.Common, "reservation round trip");
        foreach (string xml in new[] { File.ReadAllText(path).Replace("<Op>TokenCraft</Op>", "<Op>Unknown</Op>"),
            File.ReadAllText(path).Replace("<TokenRarity>Common</TokenRarity>", "<TokenRarity>Unknown</TokenRarity>"),
            File.ReadAllText(path).Replace("<Kind>Pack</Kind>", "<Kind>Unknown</Kind>") }) {
            string badPath = Path.Combine(root, "bad-enum.xml"); File.WriteAllText(badPath, xml);
            Reject(() => { using var p = new LocalProfile(badPath); });
        }
        var corruptions = new Action<LocalProfileData>[] {
            p => p.CardTokens.Common = -1, p => p.CardTokens.Rare = -1, p => p.CardTokens.Epic = -1, p => p.CardTokens.Legendary = -1,
            p => p.DeckKits[0].GoldCap = -1, p => p.DeckKits[0].Id = "", p => p.DeckKits[0].MilestoneId = "",
            p => p.DeckKits[0].RedeemedPurchaseSequence = 1, p => p.DeckKits.Add(LocalProfile.Clone(p.DeckKits[0])),
            p => p.LadderState.FloorsRewarded.Add(p.LadderState.FloorsRewarded[0]), p => p.LadderState.FloorsRewarded[0] = "",
            p => p.LastCraft.TokenDelta = 1, p => p.LastCraft.TokenRarity = null
        };
        foreach (var corrupt in corruptions) {
            var bad = LocalProfile.Clone(saved); corrupt(bad); string badPath = Path.Combine(root, "bad-reservation.xml"); Write(badPath, bad);
            try { Reject(() => { using var p = new LocalProfile(badPath); }); } catch (Exception e) { throw new Exception("corruption " + Array.IndexOf(corruptions, corrupt), e); }
        }
        foreach (var op in Enum.GetValues<LocalCraftOp>()) {
            var operation = LocalProfile.Clone(saved);
            if (op != LocalCraftOp.TokenCraft) {
                operation.LastCraft.Op = op; operation.LastCraft.TokenRarity = null; operation.LastCraft.TokenDelta = 0;
                operation.LastCraft.DustDelta = op == LocalCraftOp.Craft ? -40 : 40;
                operation.LastCraft.Cards[0].CountDelta = op == LocalCraftOp.Craft ? 1 : -1;
                operation.LastCraft.Undoable = op != LocalCraftOp.MassDisenchant && op != LocalCraftOp.Undo;
            }
            if (op == LocalCraftOp.Upgrade) {
                operation.LastCraft.DustDelta = -360; operation.LastCraft.Undoable = false;
                operation.LastCraft.Cards = new List<LocalCraftCard> {
                    new LocalCraftCard { CardId = "CATA_452", Premium = 0, CountDelta = -1 },
                    new LocalCraftCard { CardId = "CATA_452", Premium = 1, CountDelta = 1 }
                };
            }
            string operationPath = Path.Combine(root, "craft-" + op + ".xml"); Write(operationPath, operation);
            using (var p = new LocalProfile(operationPath)) p.SelectCardBack(0);
            Check(Xml(Read(operationPath)) == Xml(operation), "craft reservation round trip " + op);
        }
        foreach (int cap in new[] { 0, 500, 100000 }) {
            string kitPath = Path.Combine(root, "kit-" + cap + ".xml");
            var kitData = LocalProfile.Clone(saved); kitData.DeckKits[0].GoldCap = cap;
            string kitId = kitData.DeckKits[0].Id;
            int startingGold = cap == 100000 ? 0 : 10000; kitData.GoldBalance = startingGold; Write(kitPath, kitData);
            int price;
            using (var p = new LocalProfile(kitPath)) {
                price = p.PurchasePrice("deck-manastorm-mage");
                Reject(() => p.Purchase("pack-standard", 1, 100, "wrong", kitId));
                Reject(() => p.Purchase("deck-manastorm-mage", 1, price, "unknown", "absent"));
                Reject(() => p.Purchase("deck-manastorm-mage", 1, price + 1, "stale", kitId));
                foreach (string stage in new[] { "write", "flush", "replace" }) {
                    var bytes = File.ReadAllBytes(kitPath); p.CommitFailure = point => { if (point == stage) throw new IOException("injected"); };
                    Reject(() => p.Purchase("deck-manastorm-mage", 1, price, "buy", kitId));
                    Check(bytes.SequenceEqual(File.ReadAllBytes(kitPath)) && p.Gold == startingGold, "kit failure atomic"); p.CommitFailure = null;
                }
                var receipt = p.Purchase("deck-manastorm-mage", 1, price, "buy", kitId);
                Check(receipt.KitId == kitId && receipt.PriceBeforeKit == price && receipt.GoldPaid == Math.Max(0, price - cap), "kit allowance");
                Reject(() => p.Purchase("deck-infinity-paladin", 1, p.PurchasePrice("deck-infinity-paladin"), "reuse", kitId));
            }
            using (var p = new LocalProfile(kitPath)) {
                var bytes = File.ReadAllBytes(kitPath); p.Purchase("deck-manastorm-mage", 1, price, "buy", kitId);
                Check(bytes.SequenceEqual(File.ReadAllBytes(kitPath)) && Read(kitPath).DeckKits[0].RedeemedPurchaseSequence == 1, "kit replay");
                Reject(() => p.Purchase("deck-manastorm-mage", 1, price, "buy"));
            }
        }
        string recipePath = Path.Combine(root, "recipe-save.xml"); long deckId;
        using (var p = new LocalProfile(recipePath)) p.SelectCardBack(0);
        var recipeData = Read(recipePath); recipeData.GoldBalance = 100; Write(recipePath, recipeData);
        var recipe = LocalShop.Default.Recipe("manastorm-mage");
        var library = RecipeLibrary.LoadText(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "recipes-v1.json")));
        var build = library.BuildDeck(recipe, RecipeLibrary.OwnedCount(new Dictionary<string, int>()));
        var draft = new LocalCustomDeck { Name = recipe.Name, Hero = recipe.Hero, Cards = build.CardIds.ToList(),
            Sideboards = build.Sideboards.Select(board => new LocalSideboard { Source = board.Key, Cards = board.Value.ToList() }).ToList() };
        using (var p = new LocalProfile(recipePath)) {
            Reject(() => p.SaveDeckFromRecipe(recipe.Id, draft));
            p.Purchase("recipe-manastorm-mage", 1, 100, "recipe");
            deckId = p.SaveDeckFromRecipe(recipe.Id, draft).Id;
            Check(!p.IsDeckComplete(deckId) && p.GetDraft(deckId).SourceRecipeId == recipe.Id, "incomplete recipe saved");
            Reject(() => p.Begin("custom:" + deckId, "priest", 1));
        }
        using (var p = new LocalProfile(recipePath)) {
            var d = p.GetDraft(deckId); p.SaveDraft(d.Id, "Renamed", d.Hero, d.Cards, d.SortOrder);
        }
        using (var p = new LocalProfile(recipePath)) {
            var d = p.GetDraft(deckId); Check(d.Cards.SequenceEqual(draft.Cards) && d.SourceRecipeId == recipe.Id && d.Name == "Renamed", "ordinary save retains recipe and missing slots");
            Reject(() => p.Begin("custom:" + deckId, "priest", 1));
        }
        string matchPath = Path.Combine(root, "receipt.xml"); string matchId;
        using (var p = new LocalProfile(matchPath)) {
            matchId = p.Begin("mage", "priest", 1).Id;
            foreach (string stage in new[] { "write", "flush", "replace" }) {
                var bytes = File.ReadAllBytes(matchPath); p.CommitFailure = point => { if (point == stage) throw new IOException("injected"); };
                Reject(() => p.Settle(matchId, "Won", 10, false));
                Check(bytes.SequenceEqual(File.ReadAllBytes(matchPath)) && p.History.Count == 0 && p.DustBalance == 0, "settlement atomic"); p.CommitFailure = null;
            }
            var r = p.Settle(matchId, "Won", 10, false);
            int firstWin = LadderRules.Fresh().Multiplier; // ladder rules: the first win pays the placement multiplier
            int rolledDust = GrindTiers.Find(r.GrindTier).Dust(LocalEconomy.RewardV6("Won", 10, false, LocalEconomy.RewardRollSeed(matchId, 1)).Dust);
            Check(r.RewardVersion == 6 && r.DustReward == rolledDust && p.DustBalance == rolledDust && r.Ladder.StarsBefore == 0 && r.Ladder.StarsAfter == firstWin && r.Ladder.StarsGained == firstWin
                && r.Ladder.MultiplierBefore == firstWin && r.Ladder.StreakBefore == 0 && r.Ladder.StreakAfter == 1
                && r.Ladder.RatingBefore == LadderRules.StartRating && r.Ladder.RatingAfter > LadderRules.StartRating && !r.Ladder.LegendReached
                && r.Ladder.FloorsReached.Count == 0 && r.Ladder.LadderRuleVersion == LadderRules.RuleVersion
                && r.ClassXpBefore == 0 && r.ClassXpAfter > 0 && r.ProgressionRuleVersion > 0 && r.TierRewards.Count == 0, "settled receipt");
            r.Ladder.StarsAfter = 99; r.UnlockedCardIds.Add("BAD");
        }
        using (var p = new LocalProfile(matchPath)) {
            var bytes = File.ReadAllBytes(matchPath); var r = p.Settle(matchId, "Won", 10, false);
            Check(bytes.SequenceEqual(File.ReadAllBytes(matchPath)) && r.Ladder.StarsAfter == LadderRules.Fresh().Multiplier && !r.UnlockedCardIds.Contains("BAD") && p.DustBalance == r.DustReward, "settlement replay preserves receipt");
            p.AcknowledgeRewardPresentation(matchId); p.Begin("mage", "priest", 2);
        }
        using (var p = new LocalProfile(matchPath)) {
            foreach (string stage in new[] { "write", "flush", "replace" }) {
                var original = File.ReadAllBytes(matchPath); p.CommitFailure = point => { if (point == stage) throw new IOException("injected"); };
                Reject(p.RecoverInterrupted); Check(original.SequenceEqual(File.ReadAllBytes(matchPath)) && p.Pending != null, "recover atomic"); p.CommitFailure = null;
            }
            p.RecoverInterrupted(); var r = p.History.Last();
            int stars = LadderRules.Fresh().Multiplier; // Bronze: an abandon-loss keeps stars, resets the streak
            Check(r.Result == "Abandoned" && r.Ladder.StarsBefore == stars && r.Ladder.StarsAfter == stars && r.Ladder.StreakAfter == 0 && r.ClassXpBefore == r.ClassXpAfter && r.TierRewards.Count == 0, "recover receipt");
            var bytes = File.ReadAllBytes(matchPath); p.RecoverInterrupted(); Check(bytes.SequenceEqual(File.ReadAllBytes(matchPath)), "recover idempotent");
        }
        var receipts = Read(matchPath);
        Check(receipts.LadderState.StarIndex == LadderRules.Fresh().Multiplier && receipts.LadderState.BestStarIndex == receipts.LadderState.StarIndex
            && receipts.LadderRuleVersion == LadderRules.RuleVersion, "ladder state synchronized");
        foreach (var corrupt in new Action<LocalProfileData>[] {
            p => p.Matches[0].DustReward = -1, p => p.Matches[0].Ladder.StarsAfter = -1,
            p => p.Matches[0].Ladder.RatingBefore = double.NaN, p => p.Matches[0].Ladder.MultiplierAfter = -1,
            p => p.Matches[0].Ladder.StarsGained = 99, p => p.Matches[0].ClassXpAfter = -1,
            p => p.Matches[0].UnlockedCardIds.Add("BAD"), p => p.Matches[0].ProgressionRuleVersion = -1
        }) {
            var bad = LocalProfile.Clone(receipts); corrupt(bad); string badPath = Path.Combine(root, "bad-receipt.xml"); Write(badPath, bad);
            Reject(() => { using var p = new LocalProfile(badPath); });
        }
        Console.WriteLine("PASS V10 reservations: round trips, malformed values, kit atomicity/replay, recipe drafts, settle/recover receipts");
    }
}
