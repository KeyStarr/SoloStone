using System;
using System.IO;
using System.Linq;

internal static class ResultsRegressionTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    internal static void Run()
    {
        SettlementFailureMissingScreen();
        NoItemsRestartPopupRejection(true);
        NoItemsRestartPopupRejection(false);
        OwnedTimeoutStaysLocal();
        ShowSurvivesSaveRetry();
        RetiredRankChildLoads();
        RankSummarySurvivesFallback();
        ThrowingTierPresenterAdvancesMatch();
        MatchTierTimeoutRetiresDone(false);
        MatchTierTimeoutRetiresDone(true);
        PlayerWaitIsNotATimeout();
        BoundaryFormFollowsLastMove();
        CatchUpTierFailureRequiresContinue(true);
        CatchUpTierFailureRequiresContinue(false);
        CatchUpGuardsReleaseAfterCompletion();
        ExplicitTierFailureRetiresDone();
        ScoopXpAndLocalRewardText();
        CachePublishBookkeepingIsNotARequest();
        RouteCompletionRetiresGeneration();
        HeroLevelsRestoreOnScoopRetirement();
        HeroLevelsRestoreAfterPublishFailure();
        FixedRewardsReadOnlyPollsAreExcluded();
        PostMatchRewardsStayInGameplay();
        AchievementReceiptCopiesSavedGrants();
        AchievementStaysInFinishedGameplay();
        PostMatchRoutesToPicker();
        PostMatchDetailedResultsIds();
        PetMomentOrderAndSkips();
        PetMomentExactlyOnce();
        PetMomentFailureAndStall();
        PetMomentEventMath();
        PetStepNaturalAndSkip();
        PetStepTimeoutRetireAndFailure();
    }

    private static void AchievementStaysInFinishedGameplay()
    {
        var achievement = new HeldAchievement();
        var flow = new LocalResultsFlow(achievement: achievement);
        var receipt = new LocalResultsReceipt { MatchId = "achievement-chain", Gold = 25,
            AchievementGrants = new[] { LocalAchievementGrantReceipt.FromCatalog("calculated") } };
        int token = flow.Begin(receipt, false);
        flow.Complete(receipt.MatchId, token, LocalResultsPhase.AwaitNativeScreen);
        flow.Complete(receipt.MatchId, token, LocalResultsPhase.Celebration);
        flow.Complete(receipt.MatchId, token, LocalResultsPhase.HidingScoop);
        Check(flow.Phase == LocalResultsPhase.Achievement && !flow.RoutingReleased && achievement.PresentCount == 1,
            "settled gameplay starts its native toast before routing or reward boxes");
        Action stale = achievement.Done;
        stale();
        Check(!LocalRewardPresentation.ShouldRouteResultHome(true, true, "Result", true,
            flow.RoutingReleased, flow.KeepsAchievementInGameplay), "late native toast completion must not route to HUB between frames");
        Check(flow.Phase == LocalResultsPhase.RouteHome && flow.RouteReleaseCount == 1 && flow.BlocksEscape,
            "normal achievement completion waits for the next safe presentation observation");
        Check(flow.EnterBoxes(receipt.MatchId, token, true) && flow.Phase == LocalResultsPhase.RouteHome,
            "gameplay toast continues through tier without a HUB detour");
        Check(flow.EnterBoxes(receipt.MatchId, token, true) && flow.Phase == LocalResultsPhase.Boxes && flow.BoxesInGameplay,
            "gameplay achievement reaches reward boxes in the same scene");
        int restarted = flow.Begin(receipt, true);
        Check(flow.Phase == LocalResultsPhase.RouteHome && achievement.PresentCount == 1 && achievement.RetireCount >= 2,
            "saved pending presentation parks while the restarted HUB host is unsafe");
        Check(flow.EnterBoxes(receipt.MatchId, restarted, false)
            && flow.Phase == LocalResultsPhase.Achievement && achievement.PresentCount == 2,
            "later stable restarted HUB presents its grouped toast exactly once");
        stale();
        Check(flow.Phase == LocalResultsPhase.Achievement && flow.Generation == restarted,
            "stale toast completion cannot advance a restarted receipt");
        achievement.Fail();
        Check(flow.Phase == LocalResultsPhase.RouteHome && flow.BlocksEscape,
            "toast failure releases results without acknowledging durable presentation");
        Check(flow.EnterBoxes(receipt.MatchId, restarted, false) && flow.Phase == LocalResultsPhase.Boxes
            && flow.RouteReleaseCount == 1,
            "toast failure fails open only after stable HUB without acknowledging");

        var cosmeticStep = new HeldAchievement();
        var cosmeticFlow = new LocalResultsFlow(achievement: cosmeticStep);
        var cosmetic = new LocalResultsReceipt { MatchId = "cosmetic-only",
            AchievementGrants = new[] { LocalAchievementGrantReceipt.FromCatalog("calculated") } };
        int cosmeticToken = cosmeticFlow.Begin(cosmetic, true);
        Check(cosmetic.HasAchievements && !cosmetic.HasItems && cosmeticFlow.Phase == LocalResultsPhase.RouteHome
            && cosmeticFlow.EnterBoxes(cosmetic.MatchId, cosmeticToken, false)
            && cosmeticFlow.Phase == LocalResultsPhase.Achievement,
            "cosmetic-only grant waits for safe HUB, then needs a toast but no native reward box");
        cosmeticStep.Done();
        Check(cosmeticFlow.EnterBoxes(cosmetic.MatchId, cosmeticToken, false),
            "cosmetic-only completion observes stable HUB before continuing");
        Action continueNoItems = null; int continued = 0;
        cosmeticFlow.PresentNoItems(done => { continueNoItems = done; return true; }, () => continued++);
        continueNoItems();
        Check(continued == 1 && cosmeticFlow.CanAcknowledge
            && cosmeticFlow.Acknowledge(cosmetic.MatchId, cosmeticToken),
            "cosmetic-only toast continues through no-items summary to one acknowledgement");

        foreach (string id in new[] { "death-knight-grave-expenditure", "demon-hunter-face-value" })
        {
            var heroStep = new HeldAchievement(); var heroFlow = new LocalResultsFlow(achievement: heroStep);
            var heroReceipt = new LocalResultsReceipt { MatchId = id,
                AchievementGrants = new[] { LocalAchievementGrantReceipt.FromCatalog(id) } };
            int heroToken = heroFlow.Begin(heroReceipt, false);
            heroFlow.Complete(id, heroToken, LocalResultsPhase.AwaitNativeScreen);
            heroFlow.Complete(id, heroToken, LocalResultsPhase.Celebration);
            heroFlow.Complete(id, heroToken, LocalResultsPhase.HidingScoop);
            Check(!heroReceipt.HasItems && heroFlow.Phase == LocalResultsPhase.Achievement && heroStep.PresentCount == 1,
                "hero-skin-only feat toasts in gameplay: " + id);
            heroStep.Fail();
            heroFlow.EnterBoxes(id, heroToken, true); heroFlow.EnterBoxes(id, heroToken, true);
            Check(heroFlow.Phase == LocalResultsPhase.Boxes && heroFlow.BoxesInGameplay,
                "failed cosmetic toast cannot strand gameplay rewards: " + id);
        }

        var zeroStep = new HeldAchievement();
        var zeroFlow = new LocalResultsFlow(achievement: zeroStep);
        zeroFlow.Begin(new LocalResultsReceipt { MatchId = "zero-achievements" }, true);
        Check(zeroStep.PresentCount == 0 && zeroFlow.Phase == LocalResultsPhase.Boxes,
            "zero grants preserve the existing HUB reward flow without an empty toast");
        Console.WriteLine("PASS AchievementStaysInFinishedGameplay");
    }

    private static void AchievementReceiptCopiesSavedGrants()
    {
        var saved = LocalAchievementGrantReceipt.FromCatalog("calculated");
        var match = new LocalMatchRecord { Id = "saved-achievement", AchievementGrants = new System.Collections.Generic.List<LocalAchievementGrantReceipt> { saved } };
        var receipt = LocalResultsReceipt.FromHistory(new[] { match }, match);
        saved.CardBackId = 0;
        match.AchievementGrants.Clear();
        Check(receipt.AchievementGrants.Count == 1 && receipt.AchievementGrants[0].AchievementId == "calculated"
            && receipt.AchievementGrants[0].CardBackId > 0,
            "results receipt defensively copies saved achievement grants");
        Console.WriteLine("PASS AchievementReceiptCopiesSavedGrants");
    }

    private sealed class HeldPet : ILocalPetStep
    {
        internal int PresentCount, RetireCount, ClickCount;
        internal bool Awaiting, ThrowOnPresent;
        internal Action Done, Fail;
        public void Present(LocalResultsReceipt receipt, Action done, Action failed)
        { PresentCount++; Done = done; Fail = failed; if (ThrowOnPresent) throw new InvalidOperationException("pet step"); }
        public void Retire() { RetireCount++; }
        public void ContinueClicked() { ClickCount++; }
        public bool AwaitingPlayer => Awaiting;
    }

    private sealed class FakePetHost : ILocalPetNative
    {
        internal int ClearCount, TerminateCount, ShownCount, Scrolls, Queued;
        internal bool HasBar = true, SyncTerminate = true, BarShowing, Ready, OnScreen, ThrowOnShow;
        internal readonly System.Collections.Generic.List<LocalPetXpEvent> Notified = new System.Collections.Generic.List<LocalPetXpEvent>();
        internal Action BarFinished, Hidden;
        public void ClearQueues() { ClearCount++; Scrolls = 0; BarShowing = false; }
        public void Notify(LocalPetXpEvent move) { Notified.Add(move); Scrolls = Queued; }
        public bool BeginBar(Action finished) { if (!HasBar) return false; BarFinished = finished; BarShowing = true; return true; }
        public bool BarReady => Ready;
        public void TerminateBar()
        {
            if (!BarShowing) return;
            BarShowing = false; TerminateCount++;
            if (SyncTerminate) BarFinished();
        }
        public bool ShowNextScroll(Action hidden)
        {
            if (ThrowOnShow) throw new InvalidOperationException("scroll widget");
            if (Scrolls == 0) return false;
            Scrolls--; ShownCount++; Hidden = hidden; OnScreen = true; return true;
        }
        public bool ScrollOnScreen => OnScreen;
        internal void HideScroll() { OnScreen = false; var hidden = Hidden; hidden(); }
    }

    private sealed class PetRig
    {
        internal readonly FakePetHost Host = new FakePetHost();
        internal readonly System.Collections.Generic.List<string> Warnings = new System.Collections.Generic.List<string>();
        internal float Now = 10f; internal int Done, Failed; internal Action ThrowIn;
        internal LocalPetMomentStep Step;
        internal PetRig(int scrolls = 0, bool hasBar = true, bool sync = true)
        {
            Host.Queued = scrolls; Host.HasBar = hasBar; Host.SyncTerminate = sync;
            Step = new LocalPetMomentStep(Host, () => Now, null, Warnings.Add);
            Step.Present(PetReceipt("rig"), () => { Done++; ThrowIn?.Invoke(); }, () => Failed++);
        }
        // Bar widget loaded and visible for long enough that a click may skip it.
        internal void ReadyBar() { Host.Ready = true; Step.Update(); Now += LocalPetMomentStep.SkipGrace + 0.1f; }
    }

    private static void PetStepNaturalAndSkip()
    {
        // The event carries the receipt's move and fires once.
        var rig = new PetRig(1);
        Check(rig.Host.Notified.Count == 1 && rig.Host.Notified[0].PreviousLevel == 1 && rig.Host.Notified[0].NewLevel == 2
            && rig.Host.ClearCount == 1 && rig.Done == 0, "step fires the single event after clearing stale queues");
        // Natural completion with a level-up scroll: scroll only after the bar, done only after the scroll is hidden.
        Check(rig.Host.ShownCount == 0, "no scroll while the bar plays");
        rig.Host.BarFinished();
        Check(rig.Host.ShownCount == 1 && rig.Done == 0, "bar completion shows the scroll");
        rig.Host.HideScroll();
        Check(rig.Done == 1 && rig.Failed == 0 && rig.Host.ClearCount == 1, "scroll dismissal completes without clearing queues");
        rig.Host.BarFinished();
        Check(rig.Done == 1 && rig.Host.ShownCount == 1, "late bar callback is inert");

        var none = new PetRig(0);
        none.Host.BarFinished();
        Check(none.Done == 1, "natural completion with no scroll");
        var noBar = new PetRig(1, hasBar: false);
        Check(noBar.Host.ShownCount == 1 && noBar.Done == 0, "no bar to show goes straight to the scrolls");
        noBar.Host.HideScroll();
        Check(noBar.Done == 1, "scroll-only moment completes");

        // Skip: TerminateEarly runs the bar callback synchronously (native), then the click handler continues. 0/1/2 scrolls.
        foreach (bool sync in new[] { true, false })
        foreach (int scrolls in new[] { 0, 1, 2 })
        {
            var skip = new PetRig(scrolls, sync: sync);
            skip.Step.ContinueClicked();
            Check(skip.Host.TerminateCount == 0 && !skip.Step.Skippable, "a click before the bar is ready is ignored");
            skip.ReadyBar();
            Check(skip.Step.Skippable && skip.Step.AwaitingPlayer, "a ready bar is skippable and counts as waiting for the player");
            skip.Step.ContinueClicked();
            string what = " (skip, " + scrolls + " scrolls, sync=" + sync + ")";
            Check(skip.Host.TerminateCount == 1, "terminated once" + what);
            if (scrolls == 0) { Check(skip.Done == 1 && skip.Host.ShownCount == 0, "completes immediately" + what); continue; }
            Check(skip.Host.ShownCount == 1 && skip.Done == 0, "exactly one scroll shown at a time" + what);
            skip.Step.ContinueClicked();
            Check(skip.Host.ShownCount == 1 && skip.Done == 0, "a second click cannot start another scroll" + what);
            skip.Host.HideScroll();
            if (scrolls == 2) Check(skip.Host.ShownCount == 2 && skip.Done == 0, "second scroll follows the first" + what);
            if (scrolls == 2) skip.Host.HideScroll();
            Check(skip.Done == 1 && skip.Failed == 0 && skip.Host.ShownCount == scrolls && skip.Host.ClearCount == 1,
                "finishes only after the last scroll, queues untouched" + what);
        }
        Console.WriteLine("PASS PetStepNaturalAndSkip");
    }

    private static void PetStepTimeoutRetireAndFailure()
    {
        // A bar whose widget never loads still ends and still shows the level-up scroll.
        var late = new PetRig(1, sync: false);
        late.Now += LocalPetMomentStep.BarReadyTimeout - 1; late.Step.Update();
        Check(late.Host.TerminateCount == 0 && late.Host.ShownCount == 0, "no timeout before the limit");
        late.Now += 2; late.Step.Update();
        Check(late.Host.TerminateCount == 1 && late.Host.ShownCount == 1 && late.Failed == 0 && late.Host.ClearCount == 1
            && late.Warnings.Exists(w => w.Contains("BAR_TIMEOUT")), "bar timeout terminates it and still shows the scroll");
        late.Host.HideScroll();
        Check(late.Done == 1, "timed-out bar moment completes after the scroll");

        // A loaded bar that never progresses (cap / early-returning handler) ends after the long stall limit and still shows scrolls.
        var stuck = new PetRig(1, sync: false);
        stuck.ReadyBar();
        stuck.Now += LocalPetMomentStep.BarStallTimeout - 5; stuck.Step.Update();
        Check(stuck.Host.TerminateCount == 0 && stuck.Step.AwaitingPlayer, "a loaded bar waits for the player before the stall limit");
        stuck.Now += 10; stuck.Step.Update();
        Check(stuck.Host.TerminateCount == 1 && stuck.Host.ShownCount == 1 && stuck.Failed == 0 && stuck.Host.ClearCount == 1
            && stuck.Warnings.Exists(w => w.Contains("BAR_STALL")), "a stalled loaded bar is terminated and the scrolls still show");
        stuck.Host.HideScroll();
        Check(stuck.Done == 1 && !stuck.Step.Skippable, "stalled bar moment completes after the scroll");
        var stuckNone = new PetRig(0, sync: true);
        stuckNone.ReadyBar(); stuckNone.Now += LocalPetMomentStep.BarStallTimeout + 1; stuckNone.Step.Update();
        Check(stuckNone.Done == 1 && stuckNone.Host.TerminateCount == 1, "a stalled bar with no scrolls simply ends");

        // Flow stall guard cannot cut a ready bar waiting at the cap, nor a visible scroll.
        float now = 50f; var host = new FakePetHost { Queued = 1, Ready = true };
        var step = new LocalPetMomentStep(host, () => now);
        var flow = new LocalResultsFlow(clock: () => now, pet: step);
        var receipt = PetReceipt("stall-ready"); int token = flow.Begin(receipt, false);
        ToPostRank(flow, receipt, token);
        step.Update(); now += 1000; flow.Update();
        Check(flow.Phase == LocalResultsPhase.PetMoment && host.ShownCount == 0, "a ready bar waiting for the player never stalls");
        host.BarFinished(); now += 1000; flow.Update();
        Check(flow.Phase == LocalResultsPhase.PetMoment && host.ShownCount == 1, "a visible scroll never stalls");
        host.HideScroll();
        Check(flow.Phase == LocalResultsPhase.RouteHome, "dismissing the scroll continues the flow");

        // Retire during load: queues cleared, late native callbacks inert, nothing completes.
        var retired = new PetRig(1, sync: false);
        int clears = retired.Host.ClearCount;
        retired.Step.Retire();
        Check(retired.Host.ClearCount == clears + 1 && retired.Done == 0 && retired.Failed == 0, "retire clears the native queues silently");
        retired.Host.BarFinished(); retired.Step.Update(); retired.Step.ContinueClicked();
        Check(retired.Host.ShownCount == 0 && retired.Done == 0 && !retired.Step.Skippable, "callbacks after retire are inert");
        var idle = new PetRig(0, hasBar: false);
        int idleClears = idle.Host.ClearCount; idle.Step.Retire(); idle.Step.Retire();
        Check(idle.Host.ClearCount == idleClears, "retiring an idle step touches nothing");

        // A true scroll failure clears the queues and fails open.
        var broken = new PetRig(1, sync: false);
        broken.Host.ThrowOnShow = true;
        broken.Host.BarFinished();
        Check(broken.Failed == 1 && broken.Done == 0 && broken.Host.ClearCount == 2 && broken.Warnings.Exists(w => w.Contains("FALLBACK")),
            "scroll failure clears both native queues and fails open");

        // The flow continuation is logged, not swallowed.
        var thrower = new PetRig(0);
        thrower.ThrowIn = () => throw new InvalidOperationException("tier boom");
        thrower.Host.BarFinished();
        Check(thrower.Done == 1 && thrower.Warnings.Exists(w => w.Contains("CONTINUE") && w.Contains("tier boom")),
            "an exception from the flow continuation is logged");
        Console.WriteLine("PASS PetStepTimeoutRetireAndFailure");
    }

    private static LocalResultsReceipt PetReceipt(string id, int before = 700, int after = 800, bool rank = false, bool achievement = false) =>
        new LocalResultsReceipt { MatchId = id, Gold = 10, Pet = "PET_3_1", PetXpBefore = before, PetXpAfter = after,
            Ladder = rank ? new LocalLadderReceipt() : null,
            AchievementGrants = achievement ? new[] { LocalAchievementGrantReceipt.FromCatalog("calculated") } : Array.Empty<LocalAchievementGrantReceipt>() };

    private static void ToPostRank(LocalResultsFlow flow, LocalResultsReceipt receipt, int token)
    {
        flow.Complete(receipt.MatchId, token, LocalResultsPhase.AwaitNativeScreen);
        flow.Complete(receipt.MatchId, token, LocalResultsPhase.Celebration);
        flow.Complete(receipt.MatchId, token, LocalResultsPhase.HidingScoop);
        if (receipt.HasRank) flow.Complete(receipt.MatchId, token, LocalResultsPhase.Rank);
    }

    // Retail order: class XP, rank, pet bar and scrolls, then (achievements/tier/)boxes, all in the finished scene.
    private static void PetMomentOrderAndSkips()
    {
        foreach (bool rank in new[] { false, true })
        {
            var pet = new HeldPet();
            var flow = new LocalResultsFlow(pet: pet);
            var receipt = PetReceipt("pet-order", rank: rank);
            int token = flow.Begin(receipt, false);
            flow.Complete(receipt.MatchId, token, LocalResultsPhase.AwaitNativeScreen);
            flow.Complete(receipt.MatchId, token, LocalResultsPhase.Celebration);
            flow.Complete(receipt.MatchId, token, LocalResultsPhase.HidingScoop);
            if (rank)
            {
                Check(flow.Phase == LocalResultsPhase.Rank && pet.PresentCount == 0, "rank precedes the pet moment");
                flow.Complete(receipt.MatchId, token, LocalResultsPhase.Rank);
            }
            Check(flow.Phase == LocalResultsPhase.PetMoment && pet.PresentCount == 1 && flow.BlocksEscape && flow.RouteReleaseCount == 0
                && !flow.RoutingReleased, "pet moment follows rank, holds Escape, and does not release the HUB route");
            pet.Done();
            Check(flow.Phase == LocalResultsPhase.RouteHome && flow.RouteReleaseCount == 1 && !flow.BlocksEscape,
                "pet completion continues to tier/route/boxes");
            Check(flow.EnterBoxes(receipt.MatchId, token, true) && flow.Phase == LocalResultsPhase.Boxes && flow.BoxesInGameplay,
                "boxes open in GAMEPLAY after the pet moment");
        }

        var chain = new HeldPet(); var achievement = new HeldAchievement();
        var withAchievement = new LocalResultsFlow(achievement: achievement, pet: chain);
        var saved = PetReceipt("pet-achievement", achievement: true);
        int chained = withAchievement.Begin(saved, false);
        ToPostRank(withAchievement, saved, chained);
        Check(withAchievement.Phase == LocalResultsPhase.PetMoment && chain.PresentCount == 1 && achievement.PresentCount == 0,
            "pet moment comes before the achievement toast");
        chain.Done();
        Check(withAchievement.Phase == LocalResultsPhase.Achievement && withAchievement.RouteReleaseCount == 0 && achievement.PresentCount == 1,
            "after the pet the saved achievements toast in the finished gameplay scene");

        var skipped = new (string Name, LocalResultsReceipt Receipt, bool Hub)[] {
            ("no pet", new LocalResultsReceipt { MatchId = "no-pet", Gold = 10 }, false),
            ("pet without XP snapshot", new LocalResultsReceipt { MatchId = "no-xp", Pet = "PET_3_1" }, false),
            ("zero gain", PetReceipt("zero-gain", 6750, 6750), false),
            ("HUB resume", PetReceipt("hub-resume"), true) };
        foreach (var row in skipped)
        {
            var step = new HeldPet(); var flow = new LocalResultsFlow(pet: step);
            int token = flow.Begin(row.Receipt, row.Hub);
            if (!row.Hub) ToPostRank(flow, row.Receipt, token);
            Check(step.PresentCount == 0 && flow.Phase != LocalResultsPhase.PetMoment, "no pet moment: " + row.Name);
        }
        var broken = new HeldPet(); var failedFlow = new LocalResultsFlow(pet: broken);
        var failedReceipt = PetReceipt("native-failed"); int failedToken = failedFlow.Begin(failedReceipt, false);
        failedFlow.Fail(failedReceipt.MatchId, failedToken, LocalResultsPhase.AwaitNativeScreen);
        Check(broken.PresentCount == 0 && failedFlow.Phase == LocalResultsPhase.RouteHome, "a failed native screen never starts the pet moment");

        var click = new HeldPet(); var clickFlow = new LocalResultsFlow(pet: click);
        var clickReceipt = PetReceipt("click"); int clickToken = clickFlow.Begin(clickReceipt, false);
        clickFlow.PetContinueClicked();
        ToPostRank(clickFlow, clickReceipt, clickToken);
        clickFlow.PetContinueClicked();
        click.Done();
        clickFlow.PetContinueClicked();
        Check(click.ClickCount == 1, "hitbox clicks reach the pet step only during the pet moment");
        Console.WriteLine("PASS PetMomentOrderAndSkips");
    }

    // The bar and scrolls show once per settled match: not on a replayed receipt, not after a restart.
    private static void PetMomentExactlyOnce()
    {
        var pet = new HeldPet(); var flow = new LocalResultsFlow(pet: pet);
        var receipt = PetReceipt("once");
        int token = flow.Begin(receipt, false);
        ToPostRank(flow, receipt, token);
        Check(pet.PresentCount == 1, "first pass presents");
        Action stale = pet.Done;
        pet.Done();
        stale();
        Check(flow.Phase == LocalResultsPhase.RouteHome && pet.PresentCount == 1, "a duplicate completion is inert");

        int replay = flow.Begin(receipt, false);
        ToPostRank(flow, receipt, replay);
        Check(pet.PresentCount == 1 && flow.Phase != LocalResultsPhase.PetMoment, "replaying the same receipt in this session never shows the pet again");
        stale();
        Check(flow.Generation == replay && flow.Phase != LocalResultsPhase.PetMoment, "stale callback of the old attempt stays inert");

        var restarted = new LocalResultsFlow(pet: pet);
        restarted.Begin(receipt, true);
        Check(pet.PresentCount == 1 && restarted.Phase == LocalResultsPhase.Boxes, "restart resumes in HUB without the pet moment");

        // A second settled match is its own moment.
        var next = PetReceipt("once-next", 800, 900);
        int nextToken = flow.Begin(next, false);
        ToPostRank(flow, next, nextToken);
        Check(pet.PresentCount == 2 && flow.Phase == LocalResultsPhase.PetMoment, "the next match presents its own moment");

        // A superseded attempt retires its step; a late completion cannot advance the new generation.
        Action superseded = pet.Done;
        int retired = pet.RetireCount;
        flow.Begin(next, true);
        superseded();
        Check(pet.RetireCount > retired && flow.Phase == LocalResultsPhase.Boxes, "re-Begin retires the step and ignores its late completion");
        Console.WriteLine("PASS PetMomentExactlyOnce");
    }

    private static void PetMomentFailureAndStall()
    {
        var pet = new HeldPet(); var flow = new LocalResultsFlow(pet: pet);
        var receipt = PetReceipt("fail");
        int token = flow.Begin(receipt, false);
        ToPostRank(flow, receipt, token);
        pet.Fail();
        Check(flow.Phase == LocalResultsPhase.RouteHome && flow.RouteReleaseCount == 1, "step failure continues the results flow");
        Check(!flow.Fail(receipt.MatchId, token, LocalResultsPhase.PetMoment), "failure outside the pet phase is inert");

        var thrower = new HeldPet { ThrowOnPresent = true }; var throwing = new LocalResultsFlow(pet: thrower);
        var thrown = PetReceipt("throws"); int thrownToken = throwing.Begin(thrown, false);
        ToPostRank(throwing, thrown, thrownToken);
        Check(throwing.Phase == LocalResultsPhase.RouteHome && thrower.RetireCount >= 2, "a throwing step is retired and the flow continues");

        float now = 100f;
        var stall = new HeldPet(); var timed = new LocalResultsFlow(clock: () => now, pet: stall);
        var slow = PetReceipt("stall"); int slowToken = timed.Begin(slow, false);
        ToPostRank(timed, slow, slowToken);
        now += LocalResultsFlow.PetStallSeconds - 1; timed.Update();
        Check(timed.Phase == LocalResultsPhase.PetMoment, "no failure before the stall limit");
        stall.Awaiting = true; now += 1000; timed.Update();
        Check(timed.Phase == LocalResultsPhase.PetMoment, "waiting for the player (scroll) never stalls");
        stall.Awaiting = false; now += LocalResultsFlow.PetStallSeconds - 1; timed.Update();
        Check(timed.Phase == LocalResultsPhase.PetMoment, "the stall timer restarts after the player wait");
        now += 2; timed.Update();
        Check(timed.Phase == LocalResultsPhase.RouteHome && stall.RetireCount >= 2, "a stalled bar fails open and retires the step");
        Console.WriteLine("PASS PetMomentFailureAndStall");
    }

    // Event args mirror native NetCachePets: 1-based level and XP remainder inside the level.
    private static void PetMomentEventMath()
    {
        LocalPetXpEvent move;
        Check(LocalPetXpEvent.TryFrom(PetReceipt("e1", 700, 800), out move) && move.PetId == 3 && move.PreviousLevel == 1 && move.NewLevel == 2
            && move.PreviousXp == 700 && move.NewXp == 50, "700 -> 800 crosses level 1 -> 2 with remainder 50");
        Check(LocalPetXpEvent.TryFrom(PetReceipt("e2", 100, 200), out move) && move.PreviousLevel == 1 && move.NewLevel == 1
            && move.PreviousXp == 100 && move.NewXp == 200, "a within-level gain keeps the level");
        Check(LocalPetXpEvent.TryFrom(PetReceipt("e3", 6700, 6750), out move) && move.PreviousLevel == 3 && move.NewLevel == 4
            && move.PreviousXp == 3950 && move.NewXp == 0, "reaching the cap shows the top level with remainder 0");
        Check(LocalPetXpEvent.TryFrom(PetReceipt("e4", 700, 2800), out move) && move.PreviousLevel == 1 && move.NewLevel == 3 && move.NewXp == 50,
            "multi-level gain reports both ends (one scroll per level crossed)");
        Check(!LocalPetXpEvent.TryFrom(PetReceipt("e5", 6750, 6750), out _), "a capped pet earns nothing, so no event");
        var form = PetReceipt("e6", 700, 800); form.Pet = "PET_3_2";
        Check(LocalPetXpEvent.TryFrom(form, out move) && move.PetId == 3, "any form id resolves to its pet family");
        var unknown = PetReceipt("e7", 700, 800); unknown.Pet = "NOT_A_PET";
        Check(!LocalPetXpEvent.TryFrom(unknown, out _) && !LocalPetXpEvent.TryFrom(null, out _), "unknown pet or missing receipt yields no event");

        var match = new LocalMatchRecord { Id = "pet-history", Result = "Won", Pet = "PET_3_1", PetXpBefore = 700, PetXpAfter = 800 };
        var fromHistory = LocalResultsReceipt.FromHistory(new[] { match }, match);
        Check(fromHistory.Pet == "PET_3_1" && fromHistory.PetXpBefore == 700 && fromHistory.PetXpAfter == 800 && fromHistory.HasPetXp,
            "receipt carries the settled pet XP move from history");
        var none = new LocalMatchRecord { Id = "no-pet-history", Result = "Won" };
        Check(!LocalResultsReceipt.FromHistory(new[] { none }, none).HasPetXp, "historical receipts without a pet have no pet moment");

        Check(LocalResultsScreenState.Limit(true) > LocalResultsScreenState.Limit(false) && LocalResultsScreenState.Limit(false) == 45f,
            "a pet match allows a longer no-Show window");
        var state = new LocalResultsScreenState(); state.ObserveTerminal("m", 0);
        Check(!state.CheckTimeout(60f, LocalResultsScreenState.Limit(true)) && state.CheckTimeout(100f, LocalResultsScreenState.Limit(true)),
            "the pet no-Show timeout fires only after its own limit");
        Console.WriteLine("PASS PetMomentEventMath");
    }

    private sealed class HeldAchievement : ILocalAchievementStep
    {
        internal int PresentCount, RetireCount;
        internal Action Done, Fail;
        public void Present(LocalResultsReceipt receipt, Action done, Action failed)
        { PresentCount++; Done = done; Fail = failed; }
        public void Retire() { RetireCount++; }
    }

    // Owner 2026-10-25: rewards right after rank/XP in the same scene, no HUB detour, no route popup.
    private static void PostMatchRewardsStayInGameplay()
    {
        var flow = new LocalResultsFlow();
        var receipt = new LocalResultsReceipt { MatchId = "chain", Gold = 50 };
        int token = flow.Begin(receipt, false);
        Check(!flow.EnterBoxes("chain", token, true) && !flow.BoxesInGameplay, "boxes wait for the celebration to finish");
        flow.Complete("chain", token, LocalResultsPhase.AwaitNativeScreen);
        flow.Complete("chain", token, LocalResultsPhase.Celebration);
        flow.Complete("chain", token, LocalResultsPhase.HidingScoop);
        Check(flow.Phase == LocalResultsPhase.RouteHome && flow.RoutingReleased, "tier step hands over once");
        Check(!flow.EnterBoxes("other", token, true) && !flow.EnterBoxes("chain", token - 1, true), "stale hand-over inert");
        Check(flow.EnterBoxes("chain", token, true) && flow.Phase == LocalResultsPhase.Boxes && flow.BoxesInGameplay
            && !flow.CanAcknowledge && !flow.BlocksEscape, "boxes open in GAMEPLAY without acknowledging");
        Check(!flow.EnterBoxes("chain", token, true), "second hand-over inert");
        // RoutingReleased stays 1, but the HUB route is suppressed while GAMEPLAY presents the boxes.
        Check(!LocalRewardPresentation.ShouldRouteResultHome(true, true, "Result", true, flow.RoutingReleased, flow.BoxesInGameplay)
            && LocalRewardPresentation.ShouldRouteResultHome(true, true, "Result", true, flow.RoutingReleased, false),
            "HUB route only as a safety net once GAMEPLAY ownership is gone");
        Check(LocalRewardPresentation.CanPresentInGameplay(true, "Result", true, true)
            && !LocalRewardPresentation.CanPresentInGameplay(false, "Result", true, true)
            && !LocalRewardPresentation.CanPresentInGameplay(true, "Playing", true, true)
            && !LocalRewardPresentation.CanPresentInGameplay(true, "Result", false, true)
            && !LocalRewardPresentation.CanPresentInGameplay(true, "Result", true, false), "gameplay context needs the owned finished match");
        flow.Complete("chain", token, LocalResultsPhase.Boxes);
        Check(flow.Acknowledge("chain", token), "one acknowledgement after Done");
        flow.Complete("chain", token, LocalResultsPhase.Acknowledged);
        flow.Complete("chain", token, LocalResultsPhase.RouteChoice);
        Check(flow.Phase == LocalResultsPhase.Idle && !flow.BoxesInGameplay && flow.Receipt == null, "route resets the gameplay flag");
        int restart = flow.Begin(receipt, true);
        Check(flow.Phase == LocalResultsPhase.Boxes && !flow.BoxesInGameplay && restart != token, "restart presents in HUB, never GAMEPLAY");
        var hub = new LocalResultsFlow();
        int hubToken = hub.Begin(new LocalResultsReceipt { MatchId = "hub" }, false);
        hub.Fail("hub", hubToken, LocalResultsPhase.AwaitNativeScreen);
        Check(hub.EnterBoxes("hub", hubToken, false) && !hub.BoxesInGameplay && hub.Phase == LocalResultsPhase.Boxes,
            "a lost GAMEPLAY scene still falls back to HUB boxes");
        Console.WriteLine("PASS PostMatchRewardsStayInGameplay");
    }

    private static void PostMatchRoutesToPicker()
    {
        Check(LocalRewardPresentation.ShouldRouteFinishedToPicker(true, false, false, "Menu", true), "acknowledged GAMEPLAY leaves for the picker");
        Check(!LocalRewardPresentation.ShouldRouteFinishedToPicker(true, true, false, "Menu", true)
            && !LocalRewardPresentation.ShouldRouteFinishedToPicker(true, false, true, "Menu", true)
            && !LocalRewardPresentation.ShouldRouteFinishedToPicker(true, false, false, "Result", true)
            && !LocalRewardPresentation.ShouldRouteFinishedToPicker(true, false, false, "Playing", true)
            && !LocalRewardPresentation.ShouldRouteFinishedToPicker(true, false, false, "Menu", false)
            && !LocalRewardPresentation.ShouldRouteFinishedToPicker(false, false, false, "Menu", true),
            "pending rewards, live matches and other scenes never auto-route");
        Check(LocalRewardPresentation.PostMatchEscape(LocalRewardEscapeAction.NavigateBack, true, false) == LocalRewardEscapeAction.Consume
            && LocalRewardPresentation.PostMatchEscape(LocalRewardEscapeAction.Passthrough, false, true) == LocalRewardEscapeAction.Consume
            && LocalRewardPresentation.PostMatchEscape(LocalRewardEscapeAction.Passthrough, false, false) == LocalRewardEscapeAction.Passthrough
            && LocalRewardPresentation.PostMatchEscape(LocalRewardEscapeAction.NavigateBack, false, true) == LocalRewardEscapeAction.NavigateBack,
            "Escape: details popup and GAMEPLAY boxes never reach the native game menu; Done-ready Escape still closes");
        Console.WriteLine("PASS PostMatchRoutesToPicker");
    }

    private static void PostMatchDetailedResultsIds()
    {
        string noItems = LocalRewardPresentation.NoItemsPopupPrefix + "m_1", details = LocalRewardPresentation.DetailsPopupPrefix + "m_1";
        // CONTINUE is the confirm (check) button on the compact card and the details card.
        Check(LocalRewardPresentation.DialogButtonId(noItems, "confirm") == "rewards.continue"
            && LocalRewardPresentation.DialogButtonId(noItems, "cancel") == "results.detailed"
            && LocalRewardPresentation.DialogButtonId(details, "okay") == "results.detailed.close"
            && LocalRewardPresentation.DialogButtonId(details, "confirm") == "rewards.continue"
            && LocalRewardPresentation.DialogButtonId(details, "cancel") == "dialog.cancel"
            && LocalRewardPresentation.DialogButtonId("LOCAL_REWARD_FALLBACK_m_1", "cancel") == "dialog.cancel"
            && LocalRewardPresentation.DialogButtonId(null, "okay") == "dialog.okay", "semantic post-match dialog ids");
        // Live 2026-10-25: the card's cancel slot auto-shrank "DETAILED RESULTS"; its label must fit like CONTINUE.
        Check(LocalRewardPresentation.DetailsCardLabel.Length <= "CONTINUE".Length, "compact card details label fits its button");
        var conceded = new LocalMatchRecord { Id = "c", Result = "Lost", Conceded = true, RewardFacts = new LocalRewardFacts(), ClassXpBefore = 0, ClassXpAfter = 0 };
        var summary = LocalRewardPresentation.Summary(conceded);
        Check(summary.Items.Count == 0 && LocalRewardPresentation.NoItemsText(summary) == "No rewards: the encounter was conceded."
            && LocalRewardPresentation.NoItemsText(new LocalRewardSummary("DEFEAT", "Only line", new LocalRewardItem[0])) == "Only line",
            "compact no-items card shows only the reason");
        Console.WriteLine("PASS PostMatchDetailedResultsIds");
    }

    private static void HeroLevelsRestoreOnScoopRetirement()
    {
        foreach (string exit in new[] { "hide", "fallback", "timeout", "scene", "restart" })
        {
            var baseline = new[] { 1, 4 };
            int[] cache = baseline;
            var published = new System.Collections.Generic.List<int[]>();
            bool tierRestored = true;
            var flow = new LocalResultsFlow(new CacheCheckingTier(() => tierRestored &= cache.SequenceEqual(baseline)));
            var receipt = new LocalResultsReceipt { MatchId = exit };
            int token = flow.Begin(receipt, false);
            flow.PublishHeroLevels(() => { cache = new[] { 7, 4 }; published.Add(cache); },
                () => { cache = baseline.ToArray(); published.Add(cache); });
            Check(cache[0] == 7 && published.Count == 1, "projection published before scoop show");
            flow.Complete(exit, token, LocalResultsPhase.AwaitNativeScreen);
            if (exit == "hide")
            {
                flow.Complete(exit, token, LocalResultsPhase.Celebration);
                Check(cache[0] == 7, "projection retained during hide animation");
                Check(!flow.Complete(exit, token - 1, LocalResultsPhase.HidingScoop) && cache[0] == 7, "stale hide cannot retire current projection");
                flow.Complete(exit, token, LocalResultsPhase.HidingScoop);
            }
            else if (exit == "fallback" || exit == "timeout")
            {
                if (exit == "timeout") flow.Complete(exit, token, LocalResultsPhase.Celebration);
                flow.Fail(exit, token, flow.Phase);
            }
            else if (exit == "scene") flow.RetireScoop();
            else flow.Begin(new LocalResultsReceipt { MatchId = "restarted" }, true);
            flow.RetireScoop(); flow.RetireScoop();
            Check(tierRestored && published.Count == 2 && cache.SequenceEqual(new[] { 1, 4 }) && !ReferenceEquals(cache, baseline)
                && published[0][0] == 7 && ReferenceEquals(cache, published[1]), "fresh baseline republished exactly once on " + exit);
        }
        Console.WriteLine("PASS HeroLevelsRestoreOnScoopRetirement");
    }

    private sealed class CacheCheckingTier : ILocalTierRewardStep
    {
        private readonly Action check;
        internal CacheCheckingTier(Action check) { this.check = check; }
        public bool AwaitingPlayer => false;
        public void Present(LocalResultsReceipt receipt, Action done) { check(); done(); }
        public void Retire() { }
    }

    private static void HeroLevelsRestoreAfterPublishFailure()
    {
        int[] cache = new[] { 1, 4 };
        var flow = new LocalResultsFlow();
        flow.Begin(new LocalResultsReceipt { MatchId = "publish-failed" }, false);
        int restores = 0;
        bool failed = false;
        try
        {
            flow.PublishHeroLevels(() => { cache = new[] { 7, 4 }; throw new InvalidOperationException("listener failed after cache write"); },
                () => { restores++; cache = new[] { 1, 4 }; });
        }
        catch (InvalidOperationException) { failed = true; }
        flow.RetireScoop();
        Check(failed && restores == 1 && cache.SequenceEqual(new[] { 1, 4 }), "partial publish failure immediately restores cache and preserves error");
        int attempts = 0;
        flow.PublishHeroLevels(() => cache = new[] { 8, 4 }, () => {
            if (++attempts == 1) throw new InvalidOperationException("restore listener failed");
            cache = new[] { 1, 4 };
        });
        try { flow.RetireScoop(); } catch (InvalidOperationException) { }
        flow.RetireScoop(); flow.RetireScoop();
        Check(attempts == 2 && cache.SequenceEqual(new[] { 1, 4 }), "failed restore retained for cleanup retry");
        Console.WriteLine("PASS HeroLevelsRestoreAfterPublishFailure");
    }

    private static void FixedRewardsReadOnlyPollsAreExcluded()
    {
        foreach (string method in new[] { "Get", "IsStartupFinished", "IsRewardReady", "HasReward", "get_Instance", "GetDependencies",
            "GetNumberOfRewardsToShow", "GetNextHeroLevelReward", "GetNextTotalLevelReward", "CanCraftCard" })
            Check(!LocalResultsFlow.CountFixedRewardsCall(method), "read-only FixedRewardsMgr poll excluded: " + method);
        foreach (string method in new[] { "ClaimReward", "AckReward", "ShowReward", "RequestRewards", "Startup", "Update", "set_Instance", "OnRewardsReceived", "GetEarnedMetaActionReward" })
            Check(LocalResultsFlow.CountFixedRewardsCall(method), "FixedRewardsMgr state/UI method counted: " + method);
        Console.WriteLine("PASS FixedRewardsReadOnlyPollsAreExcluded");
    }

    private static void ScoopXpAndLocalRewardText()
    {
        var receipt = new LocalResultsReceipt { HeroClass = "HERO_08", ClassXpBefore = 1580, ClassXpAfter = 1680 };
        Check(receipt.HasXpBar, "known class saved XP enables native scoop bar");
        var before = LocalResultsXp.Project(receipt.ClassXpBefore.Value);
        var after = LocalResultsXp.Project(receipt.ClassXpAfter.Value);
        Check(before.Level == 11 && before.XP == 280 && before.MaxXP == 310
            && after.Level == 12 && after.XP == 70 && after.MaxXP == 320, "live receipt within-level projection");
        Check(LocalResultsXp.NextCoreRewardText("HERO_08", 7) == "Next Core card unlock at level 8"
            && LocalResultsXp.NextCoreRewardText("HERO_08", 10) == ""
            && LocalResultsXp.NextCoreRewardText("unknown", 1) == "", "local schedule label, cap and unknown omit reward");
        Check(receipt.ProgressionText.Contains("Level up: 11 to 12") && !receipt.ProgressionText.Contains("Core"),
            "post-Core level up does not promise a Core reward");
        Check(LocalResultsXp.NextCoreRewardText("HERO_08", 11) == ""
            && LocalResultsXp.NextCoreRewardText("HERO_08", 60) == "", "Core reward hints stop at ten");
        receipt.ClassXpBefore = receipt.ClassXpAfter;
        Check(!receipt.HasXpBar && receipt.ProgressionText == "", "zero XP omits bar and class-progress line");
        receipt.ClassXpBefore = 1580; receipt.HeroClass = "unknown";
        Check(!receipt.HasXpBar, "unknown class never enables native bar");
        receipt.HeroClass = "HERO_08"; receipt.ClassXpAfter = null;
        Check(!receipt.HasXpBar, "legacy XP endpoint missing disables native bar");
        before = LocalResultsXp.Project(25190); after = LocalResultsXp.Project(25290);
        Check(before.Level == 60 && after.Level == 60 && before.MaxXP > 0 && after.MaxXP > 0, "cap denominator positive");
        Console.WriteLine("PASS ScoopXpAndLocalRewardText");
    }

    private static void CachePublishBookkeepingIsNotARequest()
    {
        foreach (string method in new[] { "UpdateRequestNeedState", "NetCacheCheckRequest" })
            Check(!LocalResultsFlow.CountNetCacheCall(method, true) && LocalResultsFlow.CountNetCacheCall(method, false),
                "only plugin publisher bookkeeping exempt: " + method);
        foreach (string method in new[] { "RegisterScreenEndOfGame", "RequestNetCacheObject", "NetCacheReload_Internal", "NetCacheMakeBatchRequest" })
            Check(LocalResultsFlow.CountNetCacheCall(method, true) && LocalResultsFlow.CountNetCacheCall(method, false),
                "native requests counted even inside publisher listeners: " + method);
        foreach (string method in new[] { "RegisterScreenEndOfGame", "RequestNetCacheObject", "NetCacheReload_Internal" })
            Check(LocalResultsFlow.BlockNetCacheRequest(method), "owned native request suppressed: " + method);
        Check(!LocalResultsFlow.BlockNetCacheRequest("UpdateRequestNeedState")
            && !LocalResultsFlow.BlockNetCacheRequest("NetCacheCheckRequest")
            && !LocalResultsFlow.BlockNetCacheRequest("NetCacheMakeBatchRequest"), "projection bookkeeping and existing menu batches retain callbacks");
        Console.WriteLine("PASS CachePublishBookkeepingIsNotARequest");
    }

    private static void RouteCompletionRetiresGeneration()
    {
        var flow = new LocalResultsFlow();
        int token = flow.Begin(new LocalResultsReceipt { MatchId = "route" }, true);
        Check(!flow.Complete("route", token, LocalResultsPhase.RouteChoice), "cannot route before acknowledgement");
        flow.Complete("route", token, LocalResultsPhase.Boxes);
        Check(flow.Acknowledge("route", token), "durable acknowledgement precedes route choice");
        flow.Complete("route", token, LocalResultsPhase.Acknowledged);
        Check(!flow.Complete("other", token, LocalResultsPhase.RouteChoice)
            && !flow.Complete("route", token - 1, LocalResultsPhase.RouteChoice), "wrong route identity inert");
        Check(flow.Complete("route", token, LocalResultsPhase.RouteChoice)
            && flow.Phase == LocalResultsPhase.Idle && flow.Receipt == null && flow.Generation != token
            && !flow.RoutingReleased && !flow.CanAcknowledge && !flow.BlocksEscape, "route retires receipt and callbacks into Idle");
        int next = flow.Begin(new LocalResultsReceipt { MatchId = "next-route" }, true);
        Check(!flow.Complete("route", token, LocalResultsPhase.RouteChoice) && flow.Generation == next
            && flow.Phase == LocalResultsPhase.Boxes && !flow.CanAcknowledge, "old route cannot reset next match");
        Console.WriteLine("PASS RouteCompletionRetiresGeneration");
    }

    private static void ThrowingTierPresenterAdvancesMatch()
    {
        var tier = new FailingTier { Throw = true };
        var receipt = new LocalResultsReceipt { MatchId = "throwing-tier", HeroClass = "HERO_08", ClassXpBefore = 0, ClassXpAfter = 100 };
        var flow = new LocalResultsFlow(tier);
        int generation = flow.Begin(receipt, true);
        Check(flow.Phase == LocalResultsPhase.Boxes && flow.RouteReleaseCount == 1 && !flow.CanAcknowledge,
            "throwing tier presenter falls through to boxes without acknowledging");
        tier.Done(); tier.Done();
        Check(flow.Phase == LocalResultsPhase.Boxes && flow.RouteReleaseCount == 1
            && !flow.Acknowledge(receipt.MatchId, generation), "late throwing presenter completion is inert");
        Console.WriteLine("PASS ThrowingTierPresenterAdvancesMatch");
    }

    private sealed class FailingTier : ILocalTierRewardStep
    {
        internal bool Throw, Awaiting;
        internal Action Done;
        public bool AwaitingPlayer => Awaiting;
        public void Retire() { }
        public void Present(LocalResultsReceipt receipt, Action done)
        {
            Done = done;
            if (Throw) throw new InvalidOperationException("tier presenter failed");
        }
    }

    private static void BoundaryFormFollowsLastMove()
    {
        var win = ResultsTests.Match("edge-win", "Won", 20, 21);
        var won = LocalResultsReceipt.FromHistory(new[] { win }, win);
        Check(!won.ArrivedByWinBefore && won.ArrivedByWinAfter && won.RankText.StartsWith("Rank: Bronze 4 (+1 star)"),
            "win onto a boundary stays at Bronze 4 3/3");
        CheckMedals(won, 112, 2, 112, 3);
        var next = ResultsTests.Match("edge-next", "Won", 21, 22);
        var promoted = LocalResultsReceipt.FromHistory(new[] { win, next }, next);
        Check(promoted.ArrivedByWinBefore && promoted.RankText.StartsWith("Rank: Bronze 4 → Bronze 3 (+1 star)"), "next win promotes");
        CheckMedals(promoted, 112, 3, 114, 1);
        var loss = ResultsTests.Match("edge-loss", "Lost", 22, 21);
        loss.Ladder.BestAfter = 21; // Bronze's display best follows its current index, including synthetic losses.
        var dropped = LocalResultsReceipt.FromHistory(new[] { win, next, loss }, loss);
        Check(!dropped.ArrivedByWinAfter && dropped.RankText.StartsWith("Rank: Bronze 3 (-1 star)"), "loss onto a boundary shows Bronze 3 0/3");
        CheckMedals(dropped, 114, 1, 114, 0);
        var tie = ResultsTests.Match("edge-tie", "Tied", 21, 21);
        var tied = LocalResultsReceipt.FromHistory(new[] { win, tie }, tie);
        Check(tied.ArrivedByWinBefore && tied.ArrivedByWinAfter && tied.RankText.StartsWith("Rank: Bronze 4") && !tied.RankText.Contains("→"), "tie keeps the boundary form");
        CheckMedals(tied, 112, 3, 112, 3);
        CheckMedals(LocalResultsReceipt.FromHistory(new[] { win, next, loss, tie }, tie), 114, 0, 114, 0);
        var floorWin = ResultsTests.Match("floor-win", "Won", 29, 30);
        var protectedLoss = ResultsTests.Match("protected-loss", "Lost", 30, 30);
        var held = LocalResultsReceipt.FromHistory(new[] { floorWin, protectedLoss }, protectedLoss);
        Check(held.ArrivedByWinBefore && held.ArrivedByWinAfter && held.RankText.StartsWith("Rank: Bronze 1 (no stars lost)"), "protected loss keeps winning boundary form");
        CheckMedals(held, 118, 3, 118, 3);
        var legend = ResultsTests.Match("legend", "Won", 149, 150);
        CheckMedals(LocalResultsReceipt.FromHistory(new[] { legend }, legend), 198, 2, 200, 0);
        foreach (int rule in new[] { 0, 1 })
        {
            var legacyLoss = ResultsTests.Match("legacy-loss", "Lost", 22, 21);
            legacyLoss.Ladder.LadderRuleVersion = rule;
            CheckMedals(LocalResultsReceipt.FromHistory(new[] { win, legacyLoss, tie }, tie), 112, 3, 112, 3);
            var legacyWin = ResultsTests.Match("legacy-win", "Won", 20, 21);
            legacyWin.Ladder.LadderRuleVersion = rule;
            CheckMedals(LocalResultsReceipt.FromHistory(new[] { loss, legacyWin, tie }, tie), 114, 0, 114, 0);
            CheckMedals(LocalResultsReceipt.FromHistory(new[] { legacyWin, tie }, tie), 114, 0, 114, 0);
        }
        Check(LocalResultsReceipt.DisplayRank(0, true) == LadderRules.RankName(0)
            && LocalResultsReceipt.DisplayRank(LadderRules.LegendIndex, true) == "Legend", "floor and Legend never shift");
        Console.WriteLine("PASS BoundaryFormFollowsLastMove");
    }

    private static void CheckMedals(LocalResultsReceipt receipt, int beforeRank, int beforeStars, int afterRank, int afterStars)
    {
        var rows = Enumerable.Range(0, 51).Select(i => new LadderRankRow {
            StarLevel = 100 + i * 2, Stars = i == 50 ? 0 : 3, Legend = i == 50 }).Reverse().ToArray();
        var before = LadderRules.Medal(receipt.Position(false), 7, rows, receipt.ArrivedByWinBefore);
        var after = LadderRules.Medal(receipt.Position(true), 7, rows, receipt.ArrivedByWinAfter);
        Check(before.StarLevel == beforeRank && before.Stars == beforeStars && after.StarLevel == afterRank && after.Stars == afterStars,
            "native before/after medal form for " + receipt.MatchId);
        Check(after.IsLegend == (afterRank == 200), "Legend never gets a full-star boundary form");
    }

    private static void PlayerWaitIsNotATimeout()
    {
        float now = 100; int failures = 0;
        var tier = new FailingTier();
        var match = ResultsTests.Match("tier-waits", "Won", 29, 33);
        var receipt = LocalResultsReceipt.FromHistory(new[] { match }, match);
        var flow = new LocalResultsFlow(tier, () => now, error => failures++);
        int generation = flow.Begin(receipt, true);
        tier.Awaiting = true;
        for (now = 100; now <= 700; now += 60) flow.Update();
        Check(flow.Phase == LocalResultsPhase.TierReward && failures == 0, "chest waiting on the player never times out");
        tier.Awaiting = false; now = 819.9f; flow.Update();
        Check(flow.Phase == LocalResultsPhase.TierReward && failures == 0, "stall timer restarts after the player wait");
        now = 820; flow.Update();
        Check(flow.Phase == LocalResultsPhase.Boxes && failures == 1, "stall after the player wait still times out");
        var waiting = new FailingTier { Awaiting = true }; now = 0; failures = 0;
        var ok = new LocalResultsFlow(waiting, () => now, error => failures++);
        ok.Begin(receipt, true); now = 5000; ok.Update(); waiting.Done(); ok.Update();
        Check(ok.Phase == LocalResultsPhase.Boxes && failures == 0, "player-paced chest completes through done");
        Console.WriteLine("PASS PlayerWaitIsNotATimeout");
    }

    private static void MatchTierTimeoutRetiresDone(bool inHub)
    {
        float now = 100;
        int failures = 0;
        var tier = new FailingTier();
        var match = ResultsTests.Match("tier-timeout", "Won", 29, 33);
        match.TierRewards.Add(new LocalTierReward { MilestoneId = "Silver", OpponentName = "Innkeeper",
            Grants = new System.Collections.Generic.List<LocalTierGrant> {
                new LocalTierGrant { Kind = LocalTierGrantKind.Pack, Ref = "standard", Count = 2 } } });
        var receipt = LocalResultsReceipt.FromHistory(new[] { match }, match);
        var flow = new LocalResultsFlow(tier, () => now, error => { Check(error is TimeoutException, "timeout logged"); failures++; });
        int generation = flow.Begin(receipt, inHub);
        if (!inHub) flow.Fail(receipt.MatchId, generation, LocalResultsPhase.AwaitNativeScreen);
        now = 219.9f; flow.Update();
        Check(flow.Phase == LocalResultsPhase.TierReward && flow.BlocksEscape && failures == 0, "retail pacing retained before tier timeout");
        now = 220; flow.Update();
        Check(flow.Phase == (inHub ? LocalResultsPhase.Boxes : LocalResultsPhase.RouteHome)
            && flow.RouteReleaseCount == 1 && failures == 1 && !flow.CanAcknowledge, "tier timeout releases route once, never acknowledges");
        tier.Done(); tier.Done(); now = 400; flow.Update();
        Check(flow.RouteReleaseCount == 1 && failures == 1, "late timed-out tier completion inert");
        if (!inHub) flow.Complete(receipt.MatchId, generation, LocalResultsPhase.RouteHome);
        Check(!flow.Acknowledge(receipt.MatchId, generation)
            && receipt.SummaryText(LocalRewardPresentation.Summary(match)).Contains("2 pack(s)"), "fallback keeps tier grants in final summary and waits for boxes");
        flow.Complete(receipt.MatchId, generation, LocalResultsPhase.Boxes);
        Check(flow.Acknowledge(receipt.MatchId, generation), "normal acknowledgement order survives timeout");
        Console.WriteLine("PASS MatchTierTimeoutRetiresDone inHub=" + inHub);
    }

    private static void ExplicitTierFailureRetiresDone()
    {
        var tier = new FailingTier(); var flow = new LocalResultsFlow(tier);
        var receipt = new LocalResultsReceipt { MatchId = "tier-explicit-failure" };
        int generation = flow.Begin(receipt, true);
        Check(flow.Fail(receipt.MatchId, generation, LocalResultsPhase.TierReward)
            && flow.Phase == LocalResultsPhase.Boxes && !flow.CanAcknowledge, "explicit tier failure releases to unacknowledged boxes");
        tier.Done();
        Check(!flow.CanAcknowledge && flow.RouteReleaseCount == 1, "late done cannot complete boxes");
        Console.WriteLine("PASS ExplicitTierFailureRetiresDone");
    }

    private static void CatchUpTierFailureRequiresContinue(bool throws)
    {
        float now = 100; int completed = 0, failures = 0, popupCalls = 0;
        var queue = new LocalTierRewardsOnly(() => now, error => failures++);
        var tier = new FailingTier { Throw = throws };
        var receipt = new LocalResultsReceipt { MatchId = "catch-up-failure" };
        Action rejected = null, continued = null;
        bool Show(LocalResultsReceipt value, Action done)
        {
            Check(value == receipt, "fallback gets exact tier receipt");
            popupCalls++;
            if (popupCalls == 1) { rejected = done; return false; }
            if (popupCalls == 2) throw new InvalidOperationException("popup unavailable");
            continued = done; return true;
        }
        queue.Enqueue(receipt, () => completed++);
        queue.Update(false, false, tier, Show);
        var late = tier.Done;
        if (!throws)
        {
            now = 219.9f; queue.Update(false, false, tier, Show);
            Check(queue.Active && popupCalls == 0 && failures == 0, "catch-up retains retail pacing");
            now = 220; queue.Update(false, false, tier, Show);
        }
        Check(queue.Active && completed == 0 && popupCalls == 1 && failures == 1, "failed tier cannot complete catch-up on rejected popup");
        late(); rejected();
        Check(queue.Active && completed == 0, "late presenter and rejected popup callbacks inert");
        queue.Update(true, true, tier, Show);
        Check(queue.Active && completed == 0 && popupCalls == 2 && failures == 2, "popup exception stays pending and logged");
        queue.Update(true, true, tier, Show);
        AssertCatchUpHeld(queue);
        queue.Update(false, false, tier, Show);
        rejected(); late();
        Check(queue.Active && completed == 0 && popupCalls == 3, "accepted fallback waits for CONTINUE; Update does not duplicate it");
        continued(); continued(); late();
        Check(!queue.Active && completed == 1 && queue.CanStart(false) && queue.CanNavigate(false, false)
            && !queue.BlocksEscape(false), "CONTINUE completes exactly once and releases all guards");
        queue.Enqueue(new LocalResultsReceipt { MatchId = "next-catch-up" }, () => completed++);
        tier.Throw = false; queue.Update(false, false, tier, Show);
        continued(); rejected(); late();
        Check(queue.Active && completed == 1, "stale fallback and presenter callbacks cannot finish next catch-up");
        tier.Done();
        Check(!queue.Active && completed == 2, "queue resumes after fallback continuation");
        Console.WriteLine("PASS CatchUpTierFailureRequiresContinue throws=" + throws);
    }

    private static void AssertCatchUpHeld(LocalTierRewardsOnly queue)
    {
        Check(queue.Active && queue.BlocksEscape(false) && !queue.CanNavigate(false, false)
            && !queue.CanNavigate(false, true) && !queue.CanStart(false),
            "held catch-up blocks Escape, navigation, pack opening and match start even without pending match");
    }

    private static void CatchUpGuardsReleaseAfterCompletion()
    {
        var queue = new LocalTierRewardsOnly(); var tier = new FailingTier();
        int done = 0;
        queue.Enqueue(new LocalResultsReceipt { MatchId = "held-catch-up" }, () => done++);
        queue.Update(false, false, tier);
        AssertCatchUpHeld(queue);
        queue.Update(true, true, tier);
        AssertCatchUpHeld(queue);
        tier.Done();
        Check(done == 1 && queue.CanNavigate(false, false) && queue.CanNavigate(false, true)
            && queue.CanStart(false) && !queue.BlocksEscape(false), "normal completion releases catch-up guards");
        Check(!queue.CanNavigate(true, false) && queue.CanNavigate(true, true)
            && !queue.CanStart(true) && queue.BlocksEscape(true), "pending match policy preserved after catch-up");
        Console.WriteLine("PASS CatchUpGuardsReleaseAfterCompletion");
    }

    private static void SettlementFailureMissingScreen()
    {
        string root = Path.Combine(Path.GetTempPath(), "hs-results-save-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "profile.xml");
        try
        {
            using var profile = new LocalProfile(path);
            var match = profile.Begin("mage", "priest", 51);
            byte[] saved = File.ReadAllBytes(path);
            File.Delete(path); Directory.CreateDirectory(path);
            bool failed = false;
            try { profile.Settle(match.Id, "Won", 8, false); }
            catch (IOException) { failed = true; }
            catch (UnauthorizedAccessException) { failed = true; }
            Check(failed && profile.PendingRewardPresentation == null, "settlement fault must leave no receipt");
            var screen = new LocalResultsScreenState();
            screen.ObserveTerminal(match.Id, 100);
            Check(!screen.CheckTimeout(144), "missing screen retains native pacing");
            Check(screen.CheckTimeout(146) && screen.Fallback, "terminal watchdog must work without receipt");
            Check(LocalRewardPresentation.CanServiceSettlementRetry(true, "Result", true)
                && !LocalRewardPresentation.ShouldRouteResultHome(true, false, "Result", true, false),
                "watchdog completion exposes retry while routing stays held");
            Check(!screen.CheckTimeout(147), "missing-screen timeout is handled once");
            Directory.Delete(path); File.WriteAllBytes(path, saved);
            profile.Settle(match.Id, "Won", 8, false);
            var receipt = LocalResultsReceipt.FromHistory(profile.History, profile.PendingRewardPresentation);
            Check(receipt.MatchId == match.Id && screen.Fallback && !screen.CanShow(match.Id),
                "retry keeps missing-screen fallback for this match");
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine("PASS SettlementFailureMissingScreen");
    }

    private static void NoItemsRestartPopupRejection(bool conceded)
    {
        string root = Path.Combine(Path.GetTempPath(), "hs-results-popup-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "profile.xml");
        try
        {
            string id;
            using (var profile = new LocalProfile(path))
            {
                id = profile.Begin("mage", "priest", 52).Id;
                profile.Settle(id, "Lost", 2, conceded);
            }
            using var restarted = new LocalProfile(path);
            var flow = new LocalResultsFlow();
            int generation = flow.Begin(LocalResultsReceipt.FromHistory(restarted.History, restarted.PendingRewardPresentation), true);
            byte[] pendingBytes = File.ReadAllBytes(path);
            int acknowledgements = 0, attempts = 0, routes = 0;
            Action rejectedResponse = null, response = null;
            Action acknowledge = () => {
                Check(flow.CanAcknowledge, "explicit continuation authorizes acknowledgement");
                try { Check(restarted.AcknowledgeRewardPresentation(id), "one durable acknowledgement"); }
                catch (IOException) { return; }
                catch (UnauthorizedAccessException) { return; }
                Check(flow.Acknowledge(id, generation), "flow acknowledgement");
                acknowledgements++;
                flow.Complete(id, generation, LocalResultsPhase.Acknowledged);
                routes++;
                flow.Complete(id, generation, LocalResultsPhase.RouteChoice);
            };
            flow.PresentNoItems(done => { attempts++; rejectedResponse = done; return false; }, acknowledge);
            Check(attempts == 1 && restarted.PendingRewardPresentation?.Id == id && !flow.CanAcknowledge,
                "popup rejection retains pending receipt on restart");
            flow.PresentNoItems(done => { attempts++; response = done; return true; }, acknowledge);
            rejectedResponse();
            flow.PresentNoItems(done => { attempts++; return true; }, acknowledge);
            Check(attempts == 2 && acknowledgements == 0 && restarted.PendingRewardPresentation?.Id == id
                && File.ReadAllBytes(path).SequenceEqual(pendingBytes),
                "Update retries rejected popup, accepted popup waits for explicit response");
            Directory.CreateDirectory(path + ".tmp");
            response(); response();
            Check(flow.CanAcknowledge && acknowledgements == 0 && routes == 0
                && restarted.PendingRewardPresentation?.Id == id && File.ReadAllBytes(path).SequenceEqual(pendingBytes),
                "failed callback acknowledgement keeps receipt pending and never routes");
            flow.PresentNoItems(done => { attempts++; return true; }, acknowledge);
            Check(attempts == 2, "save failure does not show another summary popup");
            Directory.Delete(path + ".tmp");
            acknowledge(); response();
            Check(acknowledgements == 1 && routes == 1 && restarted.PendingRewardPresentation == null
                && flow.Phase == LocalResultsPhase.Idle, "save retry acknowledges once, routes once and retires flow without another summary");
            generation = flow.Begin(new LocalResultsReceipt { MatchId = "next" }, true);
            response();
            Check(!flow.CanAcknowledge && acknowledgements == 1, "old popup response cannot acknowledge next generation");
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine("PASS NoItemsRestartPopupRejection conceded=" + conceded);
    }

    private static void OwnedTimeoutStaysLocal()
    {
        float began = 0; bool timedOut = false; int suppressed = 0;
        Check(LocalResultsFlow.WaitForNativeReady(100, ref began, ref timedOut, () => suppressed++), "native readiness initially waits");
        Check(LocalResultsFlow.WaitForNativeReady(104.99f, ref began, ref timedOut, () => suppressed++), "native five-second wait retained");
        Check(!LocalResultsFlow.WaitForNativeReady(105, ref began, ref timedOut, () => suppressed++) && timedOut && suppressed == 1,
            "timeout continues locally and counts suppressed telemetry");
        Check(!LocalResultsFlow.WaitForNativeReady(110, ref began, ref timedOut, () => suppressed++) && suppressed == 1,
            "native timeout is logged once");
        Console.WriteLine("PASS OwnedTimeoutStaysLocal");
    }

    private static void ShowSurvivesSaveRetry()
    {
        string root = Path.Combine(Path.GetTempPath(), "hs-results-show-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "profile.xml");
        try
        {
            using var profile = new LocalProfile(path);
            var match = profile.Begin("mage", "priest", 53);
            var screen = new LocalResultsScreenState();
            screen.ObserveTerminal(match.Id, 100);
            byte[] saved = File.ReadAllBytes(path);
            File.Delete(path); Directory.CreateDirectory(path);
            bool failed = false;
            try { profile.Settle(match.Id, "Won", 8, false); }
            catch (IOException) { failed = true; }
            catch (UnauthorizedAccessException) { failed = true; }
            Check(failed && profile.PendingRewardPresentation == null, "Show arrives while settlement has failed");
            screen.Show(match.Id);
            Check(screen.CanShow(match.Id) && !screen.CanShow("other"), "Show is scoped to matching screen match");
            screen.ObserveTerminal(match.Id, 110);
            Directory.Delete(path); File.WriteAllBytes(path, saved);
            profile.Settle(match.Id, "Won", 8, false);
            var flow = new LocalResultsFlow();
            flow.Begin(LocalResultsReceipt.FromHistory(profile.History, profile.PendingRewardPresentation), false);
            Check(screen.CanShow(flow.Receipt.MatchId) && flow.Phase == LocalResultsPhase.AwaitNativeScreen
                && !screen.CheckTimeout(146), "receipt creation after save retry retains Show signal");
            screen.ObserveTerminal("next", 200);
            screen.Show(match.Id);
            Check(!screen.CanShow("next"), "prior match Show cannot release new screen");
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine("PASS ShowSurvivesSaveRetry");
    }

    private static void RetiredRankChildLoads()
    {
        var flow = new LocalResultsFlow();
        int generation = flow.Begin(new LocalResultsReceipt { MatchId = "rank", Ladder = new LocalLadderReceipt() }, false);
        flow.Complete("rank", generation, LocalResultsPhase.AwaitNativeScreen);
        flow.Complete("rank", generation, LocalResultsPhase.Celebration);
        flow.Complete("rank", generation, LocalResultsPhase.HidingScoop);
        int destroyed = 0;
        Check(flow.AcceptRankChild("rank", generation, true, () => destroyed++) && destroyed == 0, "current rank owns loaded star");
        Check(!flow.AcceptRankChild("rank", generation, false, () => destroyed++) && destroyed == 1, "destroyed owner discards star");
        flow.Fail("rank", generation, LocalResultsPhase.Rank);
        Check(!flow.AcceptRankChild("rank", generation, true, () => destroyed++) && destroyed == 2,
            "delayed child after timeout is destroyed before native callback touches owner");
        flow.Begin(new LocalResultsReceipt { MatchId = "next-rank" }, false);
        Check(!flow.AcceptRankChild("rank", generation, true, () => destroyed++) && destroyed == 3, "old generation discards star");
        Console.WriteLine("PASS RetiredRankChildLoads");
    }

    private static void RankSummarySurvivesFallback()
    {
        foreach (var item in new[] {
            ("Won", 20, 21, "Rank: Bronze 4 (+1 star)"),
            ("Won", 18, 19, "Rank: Bronze 4 (+1 star)"),
            ("Lost", 19, 18, "Rank: Bronze 4 (-1 star)"),
            ("Lost", 21, 20, "Rank: Bronze 3 → Bronze 4 (-1 star)"),
            ("Lost", 135, 135, "Rank: Diamond 5 (no stars lost)"),
            ("Lost", 18, 18, "Rank: Bronze 4 (no stars lost)"),
            ("Tied", 18, 18, "Rank: Bronze 4"),
            ("Won", 149, 150, "Rank: Diamond 1 → Legend") })
        {
            var match = ResultsTests.Match("rank-summary", item.Item1, item.Item2, item.Item3);
            match.Ladder.MultiplierBefore = 1; match.Ladder.StreakBonus = false;
            var receipt = LocalResultsReceipt.FromHistory(new[] { match }, match);
            var flow = new LocalResultsFlow();
            flow.Begin(receipt, true);
            Check(flow.Phase == LocalResultsPhase.Boxes && receipt.RankText == item.Item4,
                "restart/fallback uses player-facing rank and star delta: " + receipt.RankText);
        }
        Console.WriteLine("PASS RankSummarySurvivesFallback");
    }
}
