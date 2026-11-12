using System;
using System.Linq;
using System.Collections.Generic;

internal static class ResultsTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected invalid results input"); }
    internal static void Run()
    {
        TierRewardPlanTests.Run();
        ResultsRegressionTests.Run();
        RewardPlacementTests.Run();
        SavedReceipts();
        PremiumScroll();
        PremiumRewardNames();
        TierRewardsSummary();
        TierOnlyOrdering();
        foreach (var item in new[] { (0,1,0,40), (40,2,0,50), (100,3,10,70), (190,4,30,90), (1000,10,0,300), (1300,11,0,310), (25189,59,669,670), (25190,60,1,1), (int.MaxValue,60,1,1) })
        {
            var xp = LocalResultsXp.Project(item.Item1);
            Check(xp.Level == item.Item2 && xp.XP == item.Item3 && xp.MaxXP == item.Item4 && xp.MaxLevel == 60, "within-level XP");
        }
        foreach (string result in new[] { "Won", "Lost", "Tied" })
        {
            var match = Match(result, result, 30, result == "Won" ? 34 : 30);
            var receipt = LocalResultsReceipt.FromHistory(new[] { match }, match);
            var flow = new LocalResultsFlow(); int gen = flow.Begin(receipt, false);
            Check(flow.BlocksEscape, "Escape cannot open Game Menu over native results");
            Check(!flow.Acknowledge(result, gen), "no acknowledgement before boxes");
            foreach (var phase in new[] { LocalResultsPhase.AwaitNativeScreen, LocalResultsPhase.Celebration, LocalResultsPhase.HidingScoop })
                Check(flow.Complete(result, gen, phase), "happy path " + phase);
            Check(flow.Phase == LocalResultsPhase.Rank, "rank after hide");
            flow.Complete(result, gen, LocalResultsPhase.Rank);
            Check(flow.Phase == LocalResultsPhase.RouteHome && flow.RouteReleaseCount == 1, "tier stub releases once");
            Check(!flow.Complete(result, gen, LocalResultsPhase.Rank) && flow.RouteReleaseCount == 1, "duplicate rank callback inert");
            flow.Complete(result, gen, LocalResultsPhase.RouteHome);
            Check(!flow.BlocksEscape, "boxes retain their existing Escape policy");
            Check(!flow.Acknowledge(result, gen), "boxes must complete first");
            flow.Complete(result, gen, LocalResultsPhase.Boxes);
            Check(flow.Acknowledge(result, gen) && flow.Phase == LocalResultsPhase.Acknowledged, "ack only after boxes");
            flow.Complete(result, gen, LocalResultsPhase.Acknowledged);
            Check(flow.Phase == LocalResultsPhase.RouteChoice, "route after durable ack");
            int next = flow.Begin(receipt, true);
            Check(flow.Phase == LocalResultsPhase.Boxes && !flow.CanAcknowledge
                && !flow.Fail(result, gen, LocalResultsPhase.AwaitNativeScreen) && next != gen, "restart skips gameplay XP; stale callback inert");
        }
        var concede = Match("c", "Lost", 135, 135); concede.Conceded = true; concede.Gold = 0;
        concede.ClassXpBefore = concede.ClassXpAfter = 190;
        var c = LocalResultsReceipt.FromHistory(new[] { concede }, concede);
        Check(c.ClassXpBefore == 190 && c.ClassXpAfter == 190 && !c.HasItems && !c.HasXpBar, "concede has no items or XP animation");
        var failed = new LocalResultsFlow(); int token = failed.Begin(c, false);
        failed.Fail("c", token, LocalResultsPhase.AwaitNativeScreen);
        Check(failed.Phase == LocalResultsPhase.RouteHome && failed.RouteReleaseCount == 1, "native failure releases route");
        var rankFailure = new LocalResultsFlow(); int rankToken = rankFailure.Begin(c,false);
        rankFailure.Complete("c",rankToken,LocalResultsPhase.AwaitNativeScreen);
        rankFailure.Complete("c",rankToken,LocalResultsPhase.Celebration);
        rankFailure.Complete("c",rankToken,LocalResultsPhase.HidingScoop);
        Check(rankFailure.Fail("c",rankToken,LocalResultsPhase.Rank) && rankFailure.Phase == LocalResultsPhase.RouteHome
            && !rankFailure.Acknowledge("c",rankToken) && rankFailure.RouteReleaseCount == 1, "rank asset failure skips safely without ack");
        Check(!LocalRewardPresentation.ShouldRouteResultHome(true,true,"Result",true,false)
            && LocalRewardPresentation.ShouldRouteResultHome(true,true,"Result",true,true), "rank holds route gate");
        var heldTier = new HeldTier(); var guarded = new LocalResultsFlow(heldTier); int old = guarded.Begin(c,false);
        guarded.Fail("c",old,LocalResultsPhase.AwaitNativeScreen);
        Check(guarded.Phase == LocalResultsPhase.TierReward, "tier owns completion");
        var oldDone = heldTier.Done;
        guarded.Begin(c,true); oldDone();
        Check(guarded.Phase == LocalResultsPhase.TierReward && guarded.RouteReleaseCount == 0, "old tier callback inert");
        Check(heldTier.Retires == 1 && heldTier.Calls == 2, "new receipt retires the old presenter so its retry cannot re-present");
        heldTier.Done();
        Check(guarded.Phase == LocalResultsPhase.Boxes && heldTier.Retires == 1, "presenter that finished by itself is not retired");
        var timedOut = new HeldTier(); float now = 0;
        var attempt = new LocalTierRewardAttempt(() => now, null); int fellBack = 0;
        attempt.Start(timedOut, new LocalResultsReceipt { MatchId = "t" }, () => throw new Exception("timeout is not success"), () => fellBack++);
        now = 121; attempt.Update();
        Check(fellBack == 1 && timedOut.Retires == 1, "attempt timeout retires the presenter before its fallback");
        Check(LocalResultsFlow.CanBeginPending(true, false) && LocalResultsFlow.CanBeginPending(false, true)
            && !LocalResultsFlow.CanBeginPending(false, false), "restart receipt waits for a settled HUB; owned gameplay starts at once");
        Check(LocalResultsFlow.TierControlId == "results.tier", "tier chest control id matches tools/hslive.py reward_step");
        Console.WriteLine("PASS results receipt, medals, XP and guarded phases");
    }

    private static void PremiumScroll()
    {
        var saved = Match("premium", "Won", 30, 34);
        saved.ClassRewardGrants = new List<LocalClassRewardGrant> {
            new LocalClassRewardGrant { CardId = "CORE_CS2_029", Premium = 1, Copies = 1 },
            new LocalClassRewardGrant { Dust = 400 }
        };
        saved.MasteryGrants = new List<LocalMasteryGrant> {
            new LocalMasteryGrant { CardId = "CORE_CS2_029", Premium = 1, Copies = 1 },
            new LocalMasteryGrant { CardId = "CATA_213", Premium = 3, Copies = 1 },
            new LocalMasteryGrant { CardId = "CATA_213", Premium = 2, Copies = 1 },
            new LocalMasteryGrant { CardId = "EX1_298", Premium = 1, Copies = 0 }
        };
        var receipt = LocalResultsReceipt.FromHistory(new[] { saved }, saved);
        saved.MasteryGrants[1].Copies = 99;
        Check(receipt.PremiumUnlocks.Length == 3 && receipt.PremiumUnlocks[0].Count == 2
            && receipt.PremiumUnlocks[1].Premium == 3 && receipt.PremiumUnlocks[1].Count == 1
            && receipt.PremiumUnlocks[2].Premium == 2, "premium receipt groups sources and preserves versions/counts without aliasing");
        var large = new LocalResultsReceipt { MasteryGrants = Enumerable.Range(0, 31)
            .Select(i => new LocalMasteryGrant { CardId = "card" + i, Premium = 1, Copies = 2 }).ToArray() };
        Check(large.PremiumUnlockPages.Length == 8 && large.PremiumUnlockPages.Last().Length == 3
            && large.PremiumUnlockPages.All(page => page.Length <= 4)
            && large.PremiumUnlockPages.SelectMany(page => page).Select(row => row.Id).SequenceEqual(large.PremiumUnlocks.Select(row => row.Id)),
            "large mastery payout must retain every card within native four-item layout");
        var tier = new HeldTier(); var premium = new HeldTier();
        float now = 0;
        var flow = new LocalResultsFlow(tier, () => now, premium: premium);
        int generation = flow.Begin(receipt, true);
        Check(tier.Calls == 1 && premium.Calls == 0, "premium must follow tier");
        tier.Done();
        Check(flow.Phase == LocalResultsPhase.PremiumReward && flow.BlocksEscape && !flow.CanAcknowledge && premium.Calls == 1,
            "premium scroll must block early acknowledgement");
        var stale = premium.Done;
        flow.Begin(receipt, true); stale();
        Check(flow.Phase == LocalResultsPhase.TierReward && premium.Retires == 1, "stale premium callback advanced restart");
        tier.Done(); premium.Done();
        Check(flow.Phase == LocalResultsPhase.Boxes && !flow.CanAcknowledge && flow.RouteReleaseCount == 1, "dismissal acknowledges neither receipt nor boxes");
        premium.Done();
        Check(flow.Phase == LocalResultsPhase.Boxes && flow.RouteReleaseCount == 1, "duplicate premium completion advanced flow");
        var empty = new LocalResultsReceipt { MatchId = "empty" };
        flow.Begin(empty, true); tier.Done();
        Check(flow.Phase == LocalResultsPhase.Boxes && premium.Calls == 2 && empty.PremiumUnlocks.Length == 0,
            "empty unlocks must not invoke or delay native presentation");
        generation = flow.Begin(receipt, true); tier.Done();
        now = 121; flow.Update();
        Check(flow.Phase == LocalResultsPhase.Boxes && !flow.CanAcknowledge && premium.Retires == 2,
            "stalled premium load must retire without acknowledgement");
    }
    private static void PremiumRewardNames()
    {
        var match = Match("premium-receipt", "Won", 30, 31);
        match.ClassRewardGrants = new() {
            new() { Hero = "HERO_08", Level = 39, CardId = "CORE_CS2_032", Premium = 1, Copies = 2, ScheduleVersion = 1 },
            new() { Hero = "HERO_08", Level = 40, CardId = "CORE_DAL_575", Premium = 3, Copies = 1, ScheduleVersion = 1 },
            new() { Hero = "HERO_08", Level = 60, CardId = "TLC_452", Premium = 2, Copies = 1, ScheduleVersion = 1 } };
        match.PortraitGrants = new() {
            new() { Hero = "HERO_08", Kind = "golden", CardId = "HERO_08" },
            new() { Hero = "HERO_08", Kind = "honored", CardId = "HERO_08c" } };
        var receipt = LocalResultsReceipt.FromHistory(new[] { match }, match);
        string text = receipt.ProgressionText;
        Check(text.Contains("Golden Flamestrike x2") && text.Contains("Signature Khadgar")
            && text.Contains("Diamond"), "results name receipt premium rewards");
        Check(text.Contains("Golden Jaina Proudmoore") && text.Contains("Honored")
            && !text.Contains("HERO_08c"), "results name both portrait unlocks");
        match.ClassRewardGrants[0].Copies = 99;
        match.PortraitGrants[0].CardId = "mutated";
        Check(receipt.ProgressionText == text, "results retain copied receipt grants");
        var historical = Match("historical", "Won", 30, 31);
        Check(!LocalResultsReceipt.FromHistory(new[] { historical }, historical).ProgressionText.Contains("Golden"),
            "historical receipts do not invent premium grants from current schedule");
    }
    internal static LocalMatchRecord Match(string id, string result, int before, int after)
    {
        return new LocalMatchRecord { Id = id, Result = result, DeckHero = "HERO_08", Gold = 25,
            ClassXpBefore = 0, ClassXpAfter = 100, UnlockedCardIds = new List<string> { "CORE_CS2_024" },
            RewardFacts = new LocalRewardFacts(), TierRewards = new List<LocalTierReward>(),
            Ladder = new LocalLadderReceipt {
                StarsBefore = before, StarsAfter = after, BestBefore = before, BestAfter = Math.Max(before, after),
                StarsGained = after - before, MultiplierBefore = before >= 135 ? 1 : 2, MultiplierAfter = after >= 135 ? 1 : 2,
                StreakBefore = 2, StreakAfter = result == "Won" ? 3 : result == "Tied" ? 2 : 0,
                PlacementBefore = 10, PlacementAfter = 10, RatingBefore = 1500, RatingAfter = 1500,
                StreakBonus = result == "Won" && before < 135, LegendReached = before < 150 && after == 150,
                LadderRuleVersion = LadderRules.RuleVersion, Outcome = result
            } };
    }

    private static void SavedReceipts()
    {
        var rows = Enumerable.Range(0, 51).Select(i => new LadderRankRow {
            StarLevel = 100 + i * 2, Stars = i == 50 ? 0 : 3, Legend = i == 50 }).Reverse().ToArray();
        foreach (var item in new[] {
            ("Won", 44, 48, 128, 2, 130, 3, 128, 130),
            ("Lost", 135, 135, 190, 0, 190, 0, 192, 192),
            ("Tied", 44, 44, 128, 2, 128, 2, 128, 128),
            ("Won", 149, 150, 198, 2, 200, 0, 198, 200) })
        {
            var match = Match("saved", item.Item1, item.Item2, item.Item3);
            match.DustReward = 17;
            if (item.Item1 == "Won" && item.Item2 == 44) match.Ladder.MultiplierAfter = 1;
            if (item.Item1 == "Lost") match.Ladder.BestBefore = match.Ladder.BestAfter = 140;
            // These other matches deliberately cannot explain the saved ladder or XP numbers.
            var history = new[] { new LocalMatchRecord { Id = "prior", Result = "Won" }, match,
                new LocalMatchRecord { Id = "future", Result = "Won", DeckHero = "HERO_08" } };
            var receipt = LocalResultsReceipt.FromHistory(history, match);
            Check(receipt.HasRank && receipt.StarsBefore == item.Item2 && receipt.StarsAfter == item.Item3
                && receipt.ClassXpBefore == 0 && receipt.ClassXpAfter == 100 && receipt.HasXpBar
                && receipt.Dust == 17 && receipt.UnlockedCardIds.SequenceEqual(match.UnlockedCardIds), "saved V10 receipt only");
            var before = LadderRules.Medal(receipt.Position(false), 7, rows, receipt.ArrivedByWinBefore);
            var after = LadderRules.Medal(receipt.Position(true), 7, rows, receipt.ArrivedByWinAfter);
            Check(before.StarLevel == item.Item4 && before.Stars == item.Item5
                && after.StarLevel == item.Item6 && after.Stars == item.Item7
                && before.BestStarLevel == item.Item8 && after.BestStarLevel == item.Item9
                && before.StarsPerWin == match.Ladder.MultiplierBefore && after.StarsPerWin == match.Ladder.MultiplierAfter
                && before.Streak == (item.Item2 < 135 ? match.Ladder.StreakBefore : 0)
                && after.Streak == (item.Item3 < 135 ? match.Ladder.StreakAfter : 0)
                && receipt.SeasonGamesBefore == 1 && receipt.SeasonGamesAfter == 2
                && receipt.SeasonWinsBefore == 1 && receipt.SeasonWinsAfter == (item.Item1 == "Won" ? 2 : 1),
                "LadderRules medal from saved positions, season games differ exactly one even on tie");
            if (item.Item1 == "Tied") Check(before.StarLevel == after.StarLevel && before.Stars == after.Stars,
                "tie uses native RANK_SAME zero delta; native PlayStarChange enables click immediately");
            if (item.Item3 == 150) Check(after.IsLegend && after.Stars == 0 && receipt.Ladder.LegendReached, "Legend receipt");
            if (item.Item1 == "Won" && item.Item2 < 135) Check(receipt.RankText.Contains("(+4 stars)")
                && receipt.RankText.Contains("bonus stars x2") && receipt.RankText.Contains("win streak"), "multiplier and streak text");
        }
        var legacy = new LocalMatchRecord { Id = "legacy", Result = "Won", DeckHero = "HERO_08" };
        var old = LocalResultsReceipt.FromHistory(new[] { legacy }, legacy);
        Check(!old.HasRank && !old.HasXpBar && old.RankText == "" && old.ProgressionText == "", "legacy null receipts omit rank and XP");
        var folded = new LocalMatchRecord { Id = "folded", Result = "Won", Ladder = new LocalLadderReceipt { StarsBefore = 3, StarsAfter = 4, LadderRuleVersion = 1 } };
        Check(!LocalResultsReceipt.FromHistory(new[] { folded }, folded).HasRank, "rule-version-1 ladder receipts are legacy");
        foreach (bool beforeMissing in new[] { true, false })
        {
            legacy.ClassXpBefore = beforeMissing ? (int?)null : 0;
            legacy.ClassXpAfter = beforeMissing ? 100 : (int?)null;
            var partialXp = LocalResultsReceipt.FromHistory(new[] { legacy }, legacy);
            Check(!partialXp.HasXpBar && partialXp.ProgressionText == "", "either missing XP endpoint omits bar and inferred XP");
        }
        var flow = new LocalResultsFlow(); int generation = flow.Begin(old, false);
        foreach (var phase in new[] { LocalResultsPhase.AwaitNativeScreen, LocalResultsPhase.Celebration, LocalResultsPhase.HidingScoop })
            flow.Complete(old.MatchId, generation, phase);
        Check(flow.Phase == LocalResultsPhase.RouteHome, "legacy skips rank phase");
        flow.Complete(old.MatchId, generation, LocalResultsPhase.RouteHome);
        Check(flow.Phase == LocalResultsPhase.Boxes, "legacy skips XP phase");
        Check(flow.Begin(old, true) != generation && flow.Phase == LocalResultsPhase.Boxes, "legacy restart skips unavailable scoops and XP");
        Reject(() => LocalResultsReceipt.FromHistory(new[] { legacy }, new LocalMatchRecord { Id = "missing" }));
        Reject(() => LocalResultsReceipt.FromHistory(new[] { legacy, legacy }, legacy));
    }

    private static void TierRewardsSummary()
    {
        var match = Match("tier", "Won", 29, 33);
        var recipe = LocalShop.Default.Recipes.First();
        match.DustReward = 23; match.PackReward = true;
        match.TierRewards.Add(new LocalTierReward { MilestoneId = "floor:silver-10", OpponentName = "The Innkeeper", Grants = new List<LocalTierGrant> {
            new LocalTierGrant { Kind = LocalTierGrantKind.Pack, Ref = "standard", Count = 2 },
            new LocalTierGrant { Kind = LocalTierGrantKind.Recipe, Ref = recipe.Id, Count = 1 },
            new LocalTierGrant { Kind = LocalTierGrantKind.Challengers, Ref = "npc-mage", Count = 1 },
            new LocalTierGrant { Kind = LocalTierGrantKind.Token, Ref = "Rare", Count = 3 },
            new LocalTierGrant { Kind = LocalTierGrantKind.Challengers, Ref = "npc-priest", Count = 1 }
        } });
        match.TierRewards.Add(new LocalTierReward { MilestoneId = "floor:gold-10", OpponentName = "Gold Opponent", Grants = new List<LocalTierGrant> {
            new LocalTierGrant { Kind = LocalTierGrantKind.Recipe, Ref = "unknown-recipe", Count = 1 },
            new LocalTierGrant { Kind = LocalTierGrantKind.DeckKit, Ref = "kit", Count = 1 },
            new LocalTierGrant { Kind = LocalTierGrantKind.CardBack, Ref = "back", Count = 1 },
            new LocalTierGrant { Kind = LocalTierGrantKind.Trophy, Ref = "cup", Count = 1 },
            new LocalTierGrant { Kind = LocalTierGrantKind.LegendDeck, Ref = "deck", Count = 1 }
        } });
        var receipt = LocalResultsReceipt.FromHistory(new[] { match }, match);
        var summary = LocalRewardPresentation.Summary(match);
        string text = receipt.SummaryText(summary);
        Check(receipt.TierRewards.Count == 2 && text.Contains("You beat The Innkeeper to reach Silver")
            && text.Contains("You beat Gold Opponent to reach Gold") && text.Contains("2 pack(s)")
            && text.Contains("Recipe: " + recipe.Name) && text.Contains("Recipe: unknown-recipe")
            && text.Contains("New challengers: npc-mage, npc-priest") && !text.Contains("Challengers:")
            && text.Contains("Token: Rare x3") && text.Contains("DeckKit: kit x1") && text.Contains("CardBack: back x1")
            && text.Contains("Trophy: cup x1") && text.Contains("LegendDeck: deck x1"), "every tier grant in summary; challengers grouped, recipe names resolved");
        Check(text.Contains("Rank: ") && text.Contains("(+4 stars)") && text.Contains("bonus stars x2")
            && text.Contains("win streak") && text.Contains("Completion +25") && text.Contains("Total: 25 gold")
            && text.Contains("1 pack(s)") && text.Contains("Dust +23") && text.Contains("class progress +100 XP")
            && text.Contains("Level up: 1 to 3") && text.Contains("Core unlocked:"), "restart/fallback full receipt text with synthetic nonzero dust");
        Check(summary.Items.Any(i => i.Kind == LocalRewardKind.Dust && i.Amount == 23), "dust box input uses committed amount");
        var tier = new HeldTier(); var flow = new LocalResultsFlow(tier);
        int generation = flow.Begin(receipt, true);
        Check(flow.Phase == LocalResultsPhase.TierReward && tier.Receipt.TierRewards.Count == 2
            && !flow.CanAcknowledge && !flow.Acknowledge(receipt.MatchId, generation), "restart tier step holds acknowledgement");
        tier.Done(); tier.Done();
        Check(flow.Phase == LocalResultsPhase.Boxes && flow.RouteReleaseCount == 1 && !flow.CanAcknowledge, "tier completion once precedes boxes");
        flow.Complete(receipt.MatchId, generation, LocalResultsPhase.Boxes);
        Check(flow.Acknowledge(receipt.MatchId, generation), "acknowledgement allowed only after held tier and boxes complete");
        match.Gold = 0; match.PackReward = false;
        var dustOnly = LocalRewardPresentation.Summary(match);
        Check(dustOnly.Items.Count == 1 && dustOnly.Items[0].Kind == LocalRewardKind.Dust
            && dustOnly.Body.Contains("Dust +23") && !dustOnly.Body.Contains("No rewards"), "synthetic dust without gold still gets summary and native box input");
    }

    private static void TierOnlyOrdering()
    {
        var queue = new LocalTierRewardsOnly(); var tier = new HeldTier(); int firstDone = 0, secondDone = 0;
        var first = new LocalResultsReceipt { MatchId = "catch-up-one" };
        var second = new LocalResultsReceipt { MatchId = "catch-up-two" };
        queue.Enqueue(first, () => firstDone++);
        queue.Update(true, false, tier);
        Check(tier.Calls == 0, "tier-only waits for pending/active match results");
        queue.Update(false, true, tier);
        Check(tier.Calls == 0, "tier-only waits for pending pack reveal");
        queue.Update(false, false, tier);
        var stale = tier.Done;
        Check(queue.Active && tier.Calls == 1 && tier.Receipt == first, "tier-only starts independently when clear");
        queue.Enqueue(second, () => secondDone++);
        queue.Update(false, false, tier);
        Check(tier.Calls == 1 && tier.Receipt == first, "queued request never overwrites active tier step");
        stale(); stale();
        Check(!queue.Active && firstDone == 1 && secondDone == 0, "tier-only done exactly once");
        queue.Update(true, false, tier);
        Check(tier.Calls == 1, "second request still waits for new pending match");
        queue.Update(false, false, tier);
        stale();
        Check(queue.Active && tier.Receipt == second && firstDone == 1 && secondDone == 0, "stale generation cannot finish next presentation");
        tier.Done(); tier.Done();
        queue.Update(false, false, tier);
        Check(!queue.Active && firstDone == 1 && secondDone == 1 && tier.Calls == 2, "FIFO tier-only completion once per request");
        var immediate = new LocalTierRewardsOnly(); int complete = 0;
        immediate.Enqueue(first, () => complete++);
        immediate.Update(false, false, new LocalTierRewardStub());
        Check(!immediate.Active && complete == 1, "default tier stub completes immediately");
    }

    sealed class HeldTier : ILocalTierRewardStep
    {
        internal Action Done;
        internal LocalResultsReceipt Receipt;
        internal int Calls, Retires;
        public bool AwaitingPlayer => false;
        public void Present(LocalResultsReceipt receipt, Action done) { Receipt = receipt; Done = done; Calls++; }
        public void Retire() => Retires++;
    }
}
