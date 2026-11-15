using System.Text.RegularExpressions;

// Plugin source-text pins: what plugin/*.cs must (or must not) contain. They read source files only, so they need no client.
internal static class PluginSourcePins
{
    // The source a release build (SOLOSTONE_RELEASE defined) compiles: #if !SOLOSTONE_RELEASE blocks (and #if SOLOSTONE_RELEASE
    // #else branches) dropped; any other condition is kept whole, so the check errs toward finding lab code.
    internal static string ReleaseSource(string source)
    {
        var kept = new List<string>(); var stack = new Stack<(bool known, bool active)>(); bool on = true;
        foreach (string raw in source.Split('\n'))
        {
            string line = raw.Trim();
            if (line.StartsWith("#if ", StringComparison.Ordinal))
            {
                string condition = line.Substring(4).Trim();
                bool known = condition == "SOLOSTONE_RELEASE" || condition == "!SOLOSTONE_RELEASE";
                bool active = !known || condition == "SOLOSTONE_RELEASE";
                stack.Push((known, on)); on = on && active; continue;
            }
            // #elif: its condition is not evaluated; inside a SOLOSTONE_RELEASE test it is kept (errs toward finding lab code).
            if (line.StartsWith("#elif", StringComparison.Ordinal) && stack.Count > 0)
            { var top = stack.Peek(); if (top.known) on = top.active; continue; }
            if (line.StartsWith("#else", StringComparison.Ordinal) && stack.Count > 0)
            { var top = stack.Peek(); if (top.known) on = top.active && !on; continue; }
            if (line.StartsWith("#endif", StringComparison.Ordinal) && stack.Count > 0) { on = stack.Pop().active; continue; }
            if (on) kept.Add(raw.TrimEnd('\r'));
        }
        return string.Join("\n", kept);
    }
    internal static void Run(string root)
    {
        var failures = new List<string>();
        // Lab test-control helpers (gold-grant, shop-quantity-type, held press) must not exist in a public release build.
        var releaseText = string.Join("\n", Directory.GetFiles(Path.Combine(root, "plugin"), "*.cs", SearchOption.AllDirectories)
            .Select(file => ReleaseSource(File.ReadAllText(file))));
        foreach (string labOnly in new[] { "GrantGoldForTest", "MaximumTestGoldGrant", "TypeQuantityForTest", "HandleCraftingTest",
            "\"OnInputComplete\"", "HoldUntil", "AmountDiagnostics", "GrantCardTokensForTest", "DoublePress", "DoubleRelease" })
            if (releaseText.Contains(labOnly)) failures.Add("lab-only test-control code reaches the release build: " + labOnly);
        if (!ReleaseSource("#if !SOLOSTONE_RELEASE\nA\n#else\nB\n#endif\n#if SOLOSTONE_RELEASE\nC\n#endif\n#if X\nD\n#endif").Equals("B\nC\nD")
            || !ReleaseSource("#if !SOLOSTONE_RELEASE\nA\n#elif X\nE\n#endif").Equals("E"))
            failures.Add("release source filter is wrong");
        string achievementToastSource = File.ReadAllText(Path.Combine(root, "plugin/NativeAchievementToast.cs"));
        if (!achievementToastSource.Contains("AchievementToast.prefab:9fa72c338c657d54fb9f63de21c1e4f8")
            || achievementToastSource.Contains("ShowFake("))
            failures.Add("owned achievement toast must use the pinned prefab without stock ShowFake");
        if (!achievementToastSource.Contains("ReferenceEquals(profile, NativeUIProbe.RewardProfile)"))
            failures.Add("non-match notice acknowledgement is not scoped to the profile that started it");
        int contextMonitor = achievementToastSource.IndexOf("if (step.Active && !step.ContextReady)", StringComparison.Ordinal);
        int attemptUpdate = achievementToastSource.IndexOf("step.Update();", StringComparison.Ordinal);
        int contextOwner = achievementToastSource.IndexOf("ownerProfile = owner", StringComparison.Ordinal);
        int widgetCreate = achievementToastSource.IndexOf("WidgetInstance.Create(ToastPrefab)", StringComparison.Ordinal);
        if (contextMonitor < 0 || attemptUpdate < 0 || contextMonitor > attemptUpdate
            || contextOwner < 0 || widgetCreate < 0 || contextOwner > widgetCreate
            || !achievementToastSource.Contains("step.CancelForContext()")
            || !achievementToastSource.Contains("MatchAchievementToastContextReady(ownerProfile, ownerMatch)"))
            failures.Add("loading/shown match and non-match toasts do not retire before timeout/continuation when HUB safety ends");
        if (!achievementToastSource.Contains("Time.realtimeSinceStartup - queuedAt > AttemptTimeout")
            || achievementToastSource.Contains("AttemptTimeout && toast == null"))
            failures.Add("achievement toast timeout does not cover the whole automatic shown attempt");
        int readyContext = achievementToastSource.IndexOf("ContextFailure(\"ready\")", StringComparison.Ordinal);
        int initializeToast = achievementToastSource.IndexOf("toast.Initialize(", StringComparison.Ordinal);
        int hideContext = achievementToastSource.IndexOf("ContextFailure(\"hide\")", StringComparison.Ordinal);
        int normalHide = achievementToastSource.IndexOf("lifetime.ObserveNormalHide", StringComparison.Ordinal);
        int destroyContext = achievementToastSource.IndexOf("ContextFailure(\"destroy\")", StringComparison.Ordinal);
        int completeDestroy = achievementToastSource.IndexOf("lifetime.Destroyed", StringComparison.Ordinal);
        if (readyContext < 0 || initializeToast < 0 || readyContext > initializeToast
            || hideContext < 0 || normalHide < 0 || hideContext > normalHide
            || destroyContext < 0 || completeDestroy < 0 || destroyContext > completeDestroy)
            failures.Add("achievement toast callback boundaries can show or complete after safe HUB context is lost");
        string hideBoundary = hideContext >= 0 && normalHide > hideContext
            ? achievementToastSource.Substring(hideContext, normalHide - hideContext) : "";
        if (!hideBoundary.Contains("FailWithoutHiding(unsafeContext)") || hideBoundary.Contains("Fail(token"))
            failures.Add("achievement toast unsafe Hide boundary recursively enters the native Hide cleanup");
        string rewardUiSource = File.ReadAllText(Path.Combine(root, "plugin/NativeRewardUI.cs"));
        if (!rewardUiSource.Contains("NativeAchievementToast.Active || NativeResultsUI.BlocksEscape")
            || Regex.Matches(rewardUiSource, "NativeAchievementToast.Active \\|\\| NativeResultsUI.BlocksEscape").Count < 2)
            failures.Add("achievement toast and stable-HUB continuation do not own both Home and Escape input");
        if (Regex.IsMatch(achievementToastSource,
            @"AchievementManager\s*\.\s*Get|\bJournal\b|\bRewardTrack|\bQuestManager\b|\bNetCache\b|\bTelemetry"))
            failures.Add("achievement toast source references an online progression/service path");
        if (!achievementToastSource.Contains("NATIVE_ACHIEVEMENT_SOUND_SUMMARY", StringComparison.Ordinal)
            || !achievementToastSource.Contains("InspectVisualController", StringComparison.Ordinal)
            || !achievementToastSource.Contains("InspectPlayMaker", StringComparison.Ordinal)
            || !achievementToastSource.Contains("stateSoundDepth > 0", StringComparison.Ordinal)
            || achievementToastSource.Contains("SoundManager.Get().LoadAndPlayClip", StringComparison.Ordinal))
            failures.Add("achievement toast does not observe owned serialized sound paths without adding fallback playback");
        string formatLock = File.ReadAllText(Path.Combine(root, "plugin/NativeFormatLock.cs"));
        foreach (string required in new[] { "clickable.Active = false", "element.SetEnabled(true, true)", "renderer.SetPropertyBlock(greyBlock)" })
            if (!formatLock.Contains(required)) failures.Add("Format picker lock lost its native disabled state: " + required);
        string shopSource = File.ReadAllText(Path.Combine(root, "plugin/NativeShop.cs"));
        int openStart = shopSource.IndexOf("private static bool Open(Box __instance)", StringComparison.Ordinal);
        int openEnd = shopSource.IndexOf("private static Action<ShopType> BoxListener", openStart, StringComparison.Ordinal);
        string open = openStart < 0 || openEnd < 0 ? "" : shopSource.Substring(openStart, openEnd - openStart);
        if (open.Length == 0) failures.Add("Shop Open method not found for its pins (renamed?)");
        foreach (string required in new[] { "NativeShopOpenText.OpenBlocker(Profile.PackRevealPending)",
            "m_id = NativeShopOpenText.PackRevealPopupId", "ShowUniquePopup(new AlertPopup.PopupInfo",
            "Refuse(\"pack reveal pending\")" })
            if (!open.Contains(required)) failures.Add("Shop Open must explain the unfinished reveal: " + required);
        string uiTree = File.ReadAllText(Path.Combine(root, "plugin/NativeUiTree.cs"));
        if (!uiTree.Contains("NativeShopOpenText.DialogButtonId(popupId, \"okay\")"))
            failures.Add("Unfinished reveal OK must have its semantic control id");
        if (failures.Count != 0) throw new Exception(string.Join("\n", failures));
        Console.WriteLine("PASS plugin source-text pins (achievement toast, reward UI, format lock, shop open)");
    }
}
