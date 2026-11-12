using System.Text.RegularExpressions;

// Plugin source-text pins: what plugin/*.cs must (or must not) contain. They read source files only, so they need no client.
internal static class PluginSourcePins
{
    internal static void Run(string root)
    {
        var failures = new List<string>();
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
        if (failures.Count != 0) throw new Exception(string.Join("\n", failures));
        Console.WriteLine("PASS plugin source-text pins (achievement toast, reward UI, format lock)");
    }
}
