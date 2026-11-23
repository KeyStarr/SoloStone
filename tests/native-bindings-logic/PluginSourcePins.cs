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
        // #119: Nefarian's Creation draws the first-picked dragon's art; the hook must stay installed and read the part tag.
        string creationPath = Path.Combine(root, "plugin/NativeCreationPortrait.cs");
        string creation = File.Exists(creationPath) ? File.ReadAllText(creationPath) : "";
        foreach (string required in new[] { "\"CATA_470t1\"", "FirstPartTag = 471", "\"UpdatePortraitTexture\"", "SetPortraitTexture(texture)", "\"HistoryCard\"", "\"LoadTile\"", "\"m_portraitTexture\"",
            "\"m_usingInitialPortraitMaterial\"", "\"GetPortraitMaterial\"", "SetPortraitMaterial(material, -1)",
            "TAG_PREMIUM.DIAMOND ? TAG_PREMIUM.GOLDEN",
            "UseFirstDragonPremiumMaterial(__instance, entity, texture)", "material.mainTexture == null", "material.mainTexture = partTexture" })
            if (!creation.Contains(required)) failures.Add("Creation portrait hook lost: " + required);
        if (!File.ReadAllText(Path.Combine(root, "plugin/ClientBridge.cs")).Contains("NativeCreationPortrait.Install(harmony, log)"))
            failures.Add("Creation portrait hook is not installed by ClientBridge");
        DeckImportPins(root, failures);
        if (failures.Count != 0) throw new Exception(string.Join("\n", failures));
        Console.WriteLine("PASS plugin source-text pins (achievement toast, reward UI, format lock, shop open, creation portrait, deck-code import)");
    }
    // Deck-code import (#140): the clipboard import is served, never granting and never touching the server or the format.
    private static void DeckImportPins(string root, List<string> failures)
    {
        string probe = File.ReadAllText(Path.Combine(root, "plugin/NativeUIProbe.cs"));
        string editor = File.ReadAllText(Path.Combine(root, "plugin/NativeDeckEditor.cs"));
        string hooks = File.ReadAllText(Path.Combine(root, "plugin/NativeDeckCodeHooks.cs"));
        string profile = File.ReadAllText(Path.Combine(root, "plugin/NativeDeckCodeProfile.cs"));
        string plan = File.ReadAllText(Path.Combine(root, "plugin/NativeDeckCodeImport.cs"));
        if (probe.Contains("BlockDeckImport", StringComparison.Ordinal) || editor.Contains("BlockDeckImport", StringComparison.Ordinal)
            || hooks.Contains("BlockDeckImport", StringComparison.Ordinal))
            failures.Add("the clipboard create must not be blocked any more (BlockDeckImport is gone)");
        foreach (string row in new[] {
            "Hook(harmony, typeof(CollectionManagerDisplay), \"CreateDeckFromClipboard\", nameof(PlanDeckCodeImport))",
            "Hook(harmony, typeof(CollectionManagerDisplay), \"OnDeckCreatedFromClipboard\", nameof(ProjectImportedDeck))",
            "Hook(harmony, typeof(CollectionInputMgr), \"PasteDeckInEditModeFromShareableDeck\", nameof(BlockDeckEditPaste))" })
            if (!probe.Contains(row, StringComparison.Ordinal)) failures.Add("deck-code import hook row missing: " + row);
        int createStart = editor.IndexOf("private static bool CreateLocalDraft(", StringComparison.Ordinal);
        int createEnd = editor.IndexOf("// Live 2026-10-25", createStart, StringComparison.Ordinal);
        string create = createStart < 0 || createEnd < 0 ? "" : editor.Substring(createStart, createEnd - createStart);
        if (create.Length == 0) failures.Add("CreateLocalDraft not found for its deck-code pins (renamed?)");
        foreach (string required in new[] { "Profile.CreateDraftFromCode(", "NativeUIProjection.OwnsCreateSource(", "TakePendingImport()" })
            if (!create.Contains(required, StringComparison.Ordinal)) failures.Add("CreateLocalDraft must serve a planned pasted deck: " + required);
        foreach (string forbidden in new[] { "Network.", "SetFormatType", "OfflineDataCache", "Grant" })
            if (create.Contains(forbidden, StringComparison.Ordinal) || hooks.Contains(forbidden, StringComparison.Ordinal)
                || profile.Contains(forbidden, StringComparison.Ordinal) || plan.Contains(forbidden, StringComparison.Ordinal))
                failures.Add("deck-code import must never touch the server, the format or grants: " + forbidden);
        if (System.Text.RegularExpressions.Regex.Matches(profile, @"\bCommit\(").Count != 1 || profile.Contains("CreateDraft(", StringComparison.Ordinal))
            failures.Add("CreateDraftFromCode must write through exactly one commit, with no empty draft first");
        if (!probe.Contains("nameof(TidyRefusedImport)", StringComparison.Ordinal) || !probe.Contains("nameof(LeaveCollectionImportState)", StringComparison.Ordinal)
            || !editor.Contains("importCreated = true;", StringComparison.Ordinal))
            failures.Add("a refused or failed import must restore the client's callbacks (postfix), and leaving the Collection must drop the fill skip");
        foreach (string required in new[] { "IsInEditMode() == true) return true;", "nativeFillSkip.TryConsume(", "nativeFillSkip.Arm(__0);", "RestoreDeckCreatedCallbacks(", "RemoveDeckCreatedListener(", "Invoke(__instance, \"RejectDeckFromClipboard\")" })
            if (!hooks.Contains(required, StringComparison.Ordinal)) failures.Add("deck-code hooks lost a review fix: " + required);
        if (plan.Contains("KeepsWildOnlyCards", StringComparison.Ordinal) || !plan.Contains("Cards.FromId(id).Collectible", StringComparison.Ordinal))
            failures.Add("Usable must require collectible cards and carry no Wild switch (the profile ledger decides, PR #136)");
        // Sharing a deck (#220): the client's own Copy stays (observed by a finalizer that rethrows), and the pasted-text
        // reader only takes over when the client's own rule would pick a line that is not a code.
        foreach (string row in new[] {
            "Hook(harmony, typeof(ShareableDeck), \"ParseDataFromDeckString\", nameof(FindPastedDeckCode))",
            "Required(typeof(DeckOptionsMenu), \"OnCopyButtonReleased\")", "finalizer: new HarmonyMethod(typeof(NativeUIProbe), nameof(ObserveDeckCopy))" })
            if (!probe.Contains(row, StringComparison.Ordinal)) failures.Add("deck share hook row missing: " + row);
        if (!hooks.Contains("if (!rescued) return true;", StringComparison.Ordinal))
            failures.Add("FindPastedDeckCode must leave text the client reads to the client");
        if (!hooks.Contains("string code = data == null ? \"\" : found;", StringComparison.Ordinal))
            failures.Add("the copy observer must keep and log only a deck code, never other clipboard text");
        foreach (string forbidden in new[] { "systemCopyBuffer =", "CopyToClipboard" })
            if (hooks.Contains(forbidden, StringComparison.Ordinal)) failures.Add("the share observer must never write the clipboard: " + forbidden);
    }
}
