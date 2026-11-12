using System.Reflection;
using UnityEngine;

// Run the production presenter; substitute Unity/native boundaries because the client cannot run on macOS.
static class Program
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    internal static void Invoke(object target, string method) => target.GetType().GetMethod(method,
        BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);

    static void Main()
    {
        int failed = 0;
        foreach (Action test in new Action[] { () => StartupInputAndCompletion(false), () => StartupInputAndCompletion(true),
            SkinPreservesAnimationAndRewards, CentresSingleItemUnderRibbon, CentresItemsEvenly, RibbonCarriesMilestoneTitle,
            LoadingIsNotPlayerWait, AnimatingIsNotPlayerWait, AchievementBlocksCatchup, ClickReadyFollowsNativeWait,
            SceneChangeRepresentsInSettledHub, SecondSceneLossShowsSavedPopup, RetiredAttemptNeverRepresents })
            try { test(); } catch (Exception error) { failed++; Console.WriteLine("FAIL " + error.GetBaseException().Message); }
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }

    static NativeTierRewardStep Present(Action done, LocalTierRewardPlan plan = null)
    {
        LocalTierRewardPlan.Next = plan;
        var step = new NativeTierRewardStep();
        step.Present(new LocalResultsReceipt { MatchId = "catchup:v1", TierRewards = new[] { new LocalTierReward() } }, done);
        return step;
    }

    static void StartupInputAndCompletion(bool existingCamera)
    {
        PegUI.Instance = new PegUI();
        if (existingCamera) PegUI.Get().AddInputCamera(CameraUtils.PopupCamera);
        int completed = 0;
        var step = Present(() => completed++);
        var display = Hearthstone.UI.WidgetInstance.Last.Display;
        Check(PegUI.Get().Registered(CameraUtils.PopupCamera), "startup registers popup input camera");
        Check(!step.AwaitingPlayer, "widget activation does not mark loading as clickable");
        foreach (var caption in display.gameObject.GetComponentsInChildren<NativeTierRewardCaption>(true)) Invoke(caption, "OnShown");
        Check(step.AwaitingPlayer, "native OnShown enables player wait");
        display.m_debugClickCatcher.Release();
        Check(completed == 1, "native click completes tier presentation exactly once");
        Check(PegUI.Get().Registered(CameraUtils.PopupCamera) == existingCamera, "cleanup preserves scene camera ownership");
        display.m_debugClickCatcher.Release();
        Check(completed == 1, "stale click cannot acknowledge twice");
    }

    static void SkinPreservesAnimationAndRewards()
    {
        var step = Present(() => { });
        var widget = Hearthstone.UI.WidgetInstance.Last;
        var display = widget.Display;
        var chests = display.gameObject.Child("Chests");
        var frame = chests.Child("Frame").AddComponent<Renderer>();
        var medal = chests.Child("Medal").AddComponent<RankedMedal>();
        var medalRenderer = medal.gameObject.AddComponent<Renderer>();
        var banner = display.gameObject.Child("GenericBlueRewardBanner").Child("GenericBlueRewardBanner");
        var source = banner.Child("SourceText").AddComponent<UberText>();
        source.Text = "GLUE_RANKED_REWARD_IN_GAME_FOOTER";
        var title = banner.Child("HeadlineText").AddComponent<UberText>();
        title.Text = "GLUE_RANKED_REWARD_IN_GAME_TITLE";
        var details = banner.Child("DetailsText").AddComponent<UberText>();
        details.Text = "asdgqasdadftasd";
        var pack = display.gameObject.Child("RewardItems").Child("Booster").AddComponent<Renderer>();
        var amount = pack.gameObject.Child("PackAmountText").AddComponent<UberText>();
        amount.Text = "2";
        var medalText = medal.gameObject.Child("MedalText").AddComponent<UberText>();
        medalText.Text = "GLUE_RANKED_MEDAL";
        var ribbon = banner.Child("Ribbon").AddComponent<Renderer>();
        var looseFrame = display.gameObject.Child("MedalFrame").AddComponent<Renderer>();
        var catcherMesh = display.m_debugClickCatcher.gameObject.AddComponent<Renderer>();
        var sparkles = display.gameObject.Child("Sparkles").AddComponent<ParticleSystemRenderer>();
        var chestSparkles = chests.Child("ChestSparkles").AddComponent<ParticleSystemRenderer>();
        var caption = widget.gameObject.GetComponentsInChildren<NativeTierRewardCaption>(true).Single();
        Invoke(caption, "LateUpdate");
        Check(chests.activeSelf && medal.gameObject.activeSelf, "chest and medal animation hierarchy remains active");
        Check(frame.forceRenderingOff && medalRenderer.forceRenderingOff && chestSparkles.forceRenderingOff, "chest frame and medal rendering suppressed");
        Check(looseFrame.forceRenderingOff && catcherMesh.forceRenderingOff && medalText.Text == "", "medal frame and texts outside items/ribbon suppressed");
        Check(!sparkles.forceRenderingOff && !ribbon.forceRenderingOff, "ribbon and non-chest native effects stay visible");
        Check(!pack.forceRenderingOff && pack.gameObject.activeSelf && amount.Text == "2", "pack x2 actor and count remain visible");
        Check(source.Text == "Click to continue" && title.Text == "Bronze 5!" && details.Text == "", "localized retail keys and placeholder replaced");
        Check(typeof(NativeTierRewardCaption).GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic) == null, "no IMGUI caption box");
        Time.realtimeSinceStartup += 5;
        source.Text = "Open your chest at the end of the season.";
        details.Text = "asdgqasdadftasd";
        var lateMesh = chests.Child("LateMesh").AddComponent<Renderer>();
        Invoke(caption, "LateUpdate");
        Check(lateMesh.forceRenderingOff && source.Text == "Click to continue" && details.Text == "", "late native bindings cannot restore retail visuals");
        display.m_debugClickCatcher.Release();
    }

    static (RankedRewardDisplay display, Transform items, NativeTierRewardCaption caption) Layout(float itemsX, params float[] slots)
    {
        Present(() => { });
        var widget = Hearthstone.UI.WidgetInstance.Last;
        var display = widget.Display;
        var banner = display.gameObject.Child("GenericBlueRewardBanner");
        banner.Child("Ribbon").AddComponent<Renderer>().Size = new Vector3(10, 2, 1);
        // Text meshes follow content length; they must not move the anchor.
        var text = banner.Child("SourceText");
        text.AddComponent<UberText>();
        text.AddComponent<Renderer>().transform.localPosition = new Vector3(20, 0, 0);
        var items = display.gameObject.Child("RewardItems").transform;
        items.localPosition = new Vector3(itemsX, 0, 0);
        var slot = items.gameObject.Child("x" + slots.Length);
        foreach (float x in slots)
        {
            var pack = slot.Child("RewardItemDisplay").AddComponent<Renderer>();
            pack.Size = new Vector3(2, 3, 1); pack.transform.localPosition = new Vector3(x, 0, 0);
        }
        var unused = items.gameObject.Child("x3").Child("RewardItemDisplay").AddComponent<Renderer>();
        unused.transform.localPosition = new Vector3(-50, 0, 0); unused.gameObject.SetActive(false);
        return (display, items, widget.gameObject.GetComponentsInChildren<NativeTierRewardCaption>(true).Single());
    }

    static void CentresSingleItemUnderRibbon()
    {
        var (display, items, caption) = Layout(-3, 0);
        var pack = items.GetComponentsInChildren<Renderer>().First(r => r.gameObject.activeInHierarchy);
        Invoke(caption, "LateUpdate");
        Check(Math.Abs(pack.bounds.center.x) < 1e-4f, "single reward item centred under the ribbon");
        Invoke(caption, "LateUpdate");
        Check(Math.Abs(items.localPosition.x) < 1e-4f, "centring is stable once aligned");
        display.m_isAnimating = true;
        items.localPosition = new Vector3(-3, 0, 0); // native animation rewrites the retail position
        Invoke(caption, "LateUpdate");
        Check(Math.Abs(pack.bounds.center.x) < 1e-4f, "offset survives native animation rewriting the group");
        display.m_isAnimating = false;
        display.m_debugClickCatcher.Release();
    }

    static void CentresItemsEvenly()
    {
        var (display, items, caption) = Layout(0, -4, -1);
        Invoke(caption, "LateUpdate");
        var packs = items.GetComponentsInChildren<Renderer>().Where(r => r.gameObject.activeInHierarchy).Select(r => r.bounds.center.x).ToArray();
        Check(Math.Abs(packs[0] + 1.5f) < 1e-4f && Math.Abs(packs[1] - 1.5f) < 1e-4f, "two reward items evenly spaced around the centre");
        display.m_debugClickCatcher.Release();
    }

    static void RibbonCarriesMilestoneTitle()
    {
        var plan = new LocalTierRewardPlan { Title = "Gold", Subtitle = "You beat Firebat", Header = "You beat Firebat to reach Gold" };
        plan.Captions.Add("Craft any Epic");
        Present(() => { }, plan);
        var display = Hearthstone.UI.WidgetInstance.Last.Display;
        var banner = display.gameObject.Child("GenericBlueRewardBanner").Child("GenericBlueRewardBanner");
        var title = banner.Child("HeadlineText").AddComponent<UberText>();
        var source = banner.Child("SourceText").AddComponent<UberText>();
        Invoke(display.GetComponentsInChildren<NativeTierRewardCaption>(true).Single(), "LateUpdate");
        Check(title.Text == "Gold!" && source.Text == "You beat Firebat\nCraft any Epic", "ribbon shows title, opponent and captions natively");
        Check(!source.Text.Contains("Gold"), "milestone title is not duplicated under the ribbon");
        display.m_debugClickCatcher.Release();
        LocalTierRewardPlan.Next = null;
    }

    static void LoadingIsNotPlayerWait()
    {
        var step = Present(() => throw new Exception("loading cannot acknowledge"));
        var caption = Hearthstone.UI.WidgetInstance.Last.gameObject.GetComponentsInChildren<NativeTierRewardCaption>(true).Single();
        Invoke(caption, "LateUpdate");
        Check(!step.AwaitingPlayer, "active component alone cannot disable stall timeout");
        Time.realtimeSinceStartup += 121;
        step.Update();
        Check(!step.AwaitingPlayer && Hearthstone.UI.WidgetInstance.Last.gameObject.Destroyed, "stalled load retires without acknowledgment");
    }

    static void AnimatingIsNotPlayerWait()
    {
        var step = Present(() => throw new Exception("stalled animation cannot acknowledge"));
        var display = Hearthstone.UI.WidgetInstance.Last.Display;
        var caption = display.GetComponentsInChildren<NativeTierRewardCaption>(true).Single();
        Invoke(caption, "OnShown");
        Check(step.AwaitingPlayer, "shown idle native display waits for click");
        display.m_isAnimating = true;
        Check(!step.AwaitingPlayer, "native reward animation retains stall timeout");
        display.m_isAnimating = false;
        display.m_isHidePending = true;
        Check(!step.AwaitingPlayer, "native death animation retains stall timeout");
        Time.realtimeSinceStartup += 121;
        step.Update();
        Check(Hearthstone.UI.WidgetInstance.Last.gameObject.Destroyed, "stalled close cleans up without acknowledgment");
    }

    static LocalResultsReceipt Receipt(string match) => new LocalResultsReceipt { MatchId = match, TierRewards = new[] { new LocalTierReward() } };

    static void ShowNative(Hearthstone.UI.WidgetInstance widget)
    {
        foreach (var caption in widget.Display.gameObject.GetComponentsInChildren<NativeTierRewardCaption>(true)) Invoke(caption, "OnShown");
    }

    // Test control lists the chest's own click control and enables it only while the native display waits for a click.
    static void ClickReadyFollowsNativeWait()
    {
        PegUI.Instance = new PegUI();
        int completed = 0;
        var step = Present(() => completed++);
        var widget = Hearthstone.UI.WidgetInstance.Last;
        var catcher = widget.Display.m_debugClickCatcher;
        Check(step.ClickTarget == catcher && !step.ClickReady, "startup chest lists its own catcher, not clickable while loading");
        ShowNative(widget);
        Check(step.ClickReady, "shown idle chest accepts the click");
        widget.Display.m_isAnimating = true;
        Check(!step.ClickReady, "reward item animation disables the click");
        widget.Display.m_isAnimating = false;
        widget.Display.m_isHidePending = true;
        Check(!step.ClickReady, "death animation disables the click");
        widget.Display.m_isHidePending = false;
        catcher.Release();
        Check(completed == 1 && step.ClickTarget == null && !step.ClickReady, "completed chest leaves no click control behind");
    }

    // Live 2026-10-28: a restart presented the chest during LOGIN->HUB, the scene change destroyed it and HUB stayed covered.
    static void SceneChangeRepresentsInSettledHub()
    {
        PegUI.Instance = new PegUI();
        NativeUIProbe.RewardContextReady = NativeUIProbe.GameplayRewardContextReady = false;
        NativeTierRewards.Initialize(null);   // production wiring: retry waits for a reward-ready scene
        var step = (NativeTierRewardStep)NativeResultsUI.TierRewardStep;
        int completed = 0;
        step.Present(Receipt("restart:1"), () => completed++);
        var first = Hearthstone.UI.WidgetInstance.Last;
        ShowNative(first);
        UnityEngine.Object.Destroy(first.gameObject);
        step.Update();
        Check(!step.AwaitingPlayer && NativeTierRewards.ClickTarget == null && !PegUI.Get().Registered(CameraUtils.PopupCamera),
            "destroyed chest releases its input camera and click control");
        step.Update();
        Check(ReferenceEquals(Hearthstone.UI.WidgetInstance.Last, first), "no second chest while the scene is still changing");
        Time.realtimeSinceStartup += 30;
        NativeUIProbe.RewardContextReady = true;
        step.Update();
        var second = Hearthstone.UI.WidgetInstance.Last;
        Check(!ReferenceEquals(second, first) && completed == 0, "chest presented again once HUB is settled");
        ShowNative(second);
        Check(NativeTierRewards.ClickReady && NativeTierRewards.ClickTarget == second.Display.m_debugClickCatcher, "re-presented chest is pressable");
        second.Display.m_debugClickCatcher.Release();
        Check(completed == 1, "re-presented chest completes the presentation once");
        NativeUIProbe.RewardContextReady = false;
    }

    static void SecondSceneLossShowsSavedPopup()
    {
        PegUI.Instance = new PegUI();
        DialogManager.Current = new DialogManager(); AlertPopup.Shown = null;
        int completed = 0;
        var step = new NativeTierRewardStep(null, () => true);
        step.Present(Receipt("restart:2"), () => completed++);
        UnityEngine.Object.Destroy(Hearthstone.UI.WidgetInstance.Last.gameObject);
        step.Update(); step.Update();
        var retry = Hearthstone.UI.WidgetInstance.Last;
        Check(!retry.gameObject.Destroyed, "first loss retries");
        UnityEngine.Object.Destroy(retry.gameObject);
        step.Update();
        Check(step.ClickTarget == null && completed == 0, "second loss stops presenting the chest");
        step.Update();
        Check(AlertPopup.Shown?.m_headerText == "REWARDS SAVED" && completed == 0, "second loss shows REWARDS SAVED instead of a silent wait");
        AlertPopup.Shown.m_responseCallback(AlertPopup.Response.CANCEL, null);
        Check(completed == 1, "saved popup completes the presentation once");
        DialogManager.Current = null;
    }

    // Review 2026-10-28: the GAMEPLAY scene unloads mid-chest, #51 fails the attempt and moves to boxes in HUB.
    static void RetiredAttemptNeverRepresents()
    {
        PegUI.Instance = new PegUI();
        bool ready = false; int completed = 0;
        var step = new NativeTierRewardStep(null, () => ready);
        step.Present(Receipt("retired:1"), () => completed++);
        var first = Hearthstone.UI.WidgetInstance.Last;
        UnityEngine.Object.Destroy(first.gameObject);
        step.Update();
        step.Retire();
        ready = true;
        step.Update();
        Check(ReferenceEquals(Hearthstone.UI.WidgetInstance.Last, first) && step.ClickTarget == null && completed == 0,
            "retired attempt drops its pending retry: no chest over the reward boxes");
        step.Present(Receipt("retired:2"), () => completed++);
        var shown = Hearthstone.UI.WidgetInstance.Last;
        step.Retire();
        Check(shown.gameObject.Destroyed && !step.AwaitingPlayer && completed == 0, "retiring a shown chest removes it without acknowledging");
    }

    static void AchievementBlocksCatchup()
    {
        NativeUIProbe.RewardProfile = new LocalProfile();
        NativeUIProbe.RewardContextReady = NativeResultsUI.CanStart = true;
        NativeResultsUI.BlocksEscape = NativeRewardUI.Active = ClientBridge.OwnsLocalPostMatch = false;
        NativeTierRewards.Initialize(null);
        NativeAchievementToast.Active = true;
        NativeTierRewards.Update();
        Check(!LocalTierRewardCatchUp.LastIdle, "achievement toast blocks tier catch-up presentation");
        NativeAchievementToast.Active = false;
        NativeTierRewards.Update();
        Check(LocalTierRewardCatchUp.LastIdle, "tier catch-up resumes after achievement toast retirement");
    }
}
