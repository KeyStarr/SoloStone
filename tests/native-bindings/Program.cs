using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Reflection.Emit;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;

// Inspect metadata without loading Unity or invoking the client.
string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
if (!Directory.Exists(Path.Combine(root, "references/game"))) root = Directory.GetCurrentDirectory();
bool runtimeGuardOnly = args.Contains("--runtime-guard-only");
foreach (string arg in args)
    if (arg.StartsWith("--", StringComparison.Ordinal) && arg != "--runtime-guard-only") throw new ArgumentException("unknown argument: " + arg);
string candidatePlugin = args.Where(arg => arg != "--runtime-guard-only").SingleOrDefault();
#if DEBUG
const string configuration = "Debug";
#else
const string configuration = "Release";
#endif
string artifactPlugin = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../OfflineProbe", configuration.ToLowerInvariant(), "HSNativeUIProbe.dll"));
string pluginPath = candidatePlugin ?? (File.Exists(artifactPlugin) ? artifactPlugin : Path.Combine(root, "plugin/bin", configuration, "net48/HSNativeUIProbe.dll"));
using var plugin = new PEReader(File.OpenRead(pluginPath));
var pluginMetadata = plugin.GetMetadataReader();
UnityRuntimeGuard.Check(root, pluginMetadata);
if (runtimeGuardOnly) return;
using var game = new PEReader(File.OpenRead(Path.Combine(root, "references/game/Assembly-CSharp.dll")));
using var engine = new PEReader(File.OpenRead(Path.Combine(Path.GetDirectoryName(pluginPath), "SabberStoneCore.dll")));
var gameMetadata = game.GetMetadataReader();
var engineMetadata = engine.GetMetadataReader();
var signatures = new TypeNames();
if (!Field(gameMetadata, "GameState", "m_queuedChoices").DecodeSignature(signatures, (object)null).StartsWith("Queue"))
    throw new Exception("Choice diagnostics queue contract changed");
MethodSignature(gameMetadata, "MulliganManager", "IsCoinCard", new[] { "Card" }, "Boolean", signatures);
foreach (var contract in new[] {
    ("CollectionCardVisual", "OnDrag", Array.Empty<string>(), "Void"),
    ("DeckTrayCosmeticCoinContent", "WidgetEventListener", new[] { "String" }, "Void"),
    ("CollectionInputMgr", "DropCard", new[] { "Boolean" }, "Void"),
    ("CollectionInputMgr", "CanGrabItem", new[] { "Actor" }, "Boolean"),
    ("InputMgr", "set_MouseIsOverDeck", new[] { "Boolean" }, "Void")
}) MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures);
foreach (var contract in new[] { ("m_currentDeck", "CollectionDeck"), ("m_cosmeticCoinActor", "Actor"), ("m_animData", "AnimatedCosmeticCoin") })
    if (Field(gameMetadata, "DeckTrayCosmeticCoinContent", contract.Item1).DecodeSignature(signatures, (object)null) != contract.Item2)
        throw new Exception("Coin drag field contract changed: " + contract.Item1);
foreach (var contract in new[] {
    ("InputManager", "Update", Array.Empty<string>(), "Void"),
    ("InputManager", "get_LeftMouseButtonDown", Array.Empty<string>(), "Boolean"),
    ("InputManager", "SetDragging", new[] { "Boolean" }, "Void"),
    ("InputCollection", "GetMousePosition", Array.Empty<string>(), "Vector3"),
    ("InputCollection", "GetMouseButtonDown", new[] { "Int32" }, "Boolean"),
    ("InputCollection", "GetMouseButtonUp", new[] { "Int32" }, "Boolean"),
    ("InputCollection", "GetMouseButton", new[] { "Int32" }, "Boolean"),
    ("Spell", "ChangeStateImpl", new[] { "SpellStateType" }, "Void")
}) MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures);
foreach (string field in new[] { "m_leftMouseButtonIsDown", "m_touchDraggingCard", "m_targettingHeroPower" })
    if (Field(gameMetadata, "InputManager", field).DecodeSignature(signatures, (object)null) != "Boolean")
        throw new Exception("Drag cleanup field contract changed: " + field);
if (Field(gameMetadata, "InputManager", "m_lastObjectMousedDown").DecodeSignature(signatures, (object)null) != "GameObject")
    throw new Exception("Drag cleanup pressed-object contract changed");
Console.WriteLine("PASS gameplay drag native Update/pointer/cleanup and spell ChangeStateImpl contracts");
MethodSignature(gameMetadata, "Actor", "GetPremium", Array.Empty<string>(), "TAG_PREMIUM", signatures);
Console.WriteLine("PASS Actor.GetPremium(): TAG_PREMIUM pack reveal contract");
MethodSignature(gameMetadata, "GameMgr", "ShowTransitionPopup", new[] { "String", "Int32" }, "Void", signatures);
MethodSignature(gameMetadata, "GameMgr", "LoadTransitionPopup", new[] { "String" }, "Void", signatures);
var hooks = new[] {
    ("GameMenu", "Awake", "NativeReportUI", "AddRow"),
    ("GameMenu", "GetButtons", "NativeReportUI", "ListRow"),
    ("GameMenu", "Show", "NativeReportUI", "PrepareMenu"),
    ("GameMenu", "LayoutMenu", "NativeReportUI", "FitMenu"),
    ("BnetBar", "HandleEscapeKey", "NativeReportUI", "BeforeEscape"),
    ("BnetBar", "HandleEscapeKey", "NativeProfileSelector", "BeforeEscape"),
    ("MulliganManager", "IsCoinCard", "ClientBridge", "RecognizeStartingCoin"),
    ("CosmeticCoinManager", "RequestSetFavoriteCosmeticCoin", "NativeUIProbe", "SelectLocalCoin"),
    ("TransitionPopup", "EnableCancelButtonIfPossible", "NativeMatchmaking", "AllowCancel"),
    ("MatchingPopupDisplay", "OnNavigateBack", "NativeMatchmaking", "BlockBack"),
    ("MatchingPopupDisplay", "OnCancelButtonReleased", "NativeMatchmaking", "AllowCancelClick"),
    ("MatchingPopupDisplay", "Hide", "NativeMatchmaking", "AllowHide"),
    ("MatchingPopupDisplay", "OnGameplaySceneLoaded", "NativeMatchmaking", "GameplayLoaded"),
    ("Box", "OnButtonMouseOver", "NativeUIProbe", "HomeModeHover"),
    ("Box", "OnButtonMouseOut", "NativeUIProbe", "HomeModeOut"),
    ("BoxScrollButton", "TriggerOver", "NativeUIProbe", "HomeScrollHover"),
    ("BoxScrollButton", "TriggerOut", "NativeUIProbe", "HomeScrollOut"),
    ("UnopenedPack", "CanOpenPack", "NativeUIProbe", "OpenLocalPack"),
    ("PackOpening", "OpenBooster", "NativeUIProbe", "BeforeOpen"),
    ("PackOpening", "IsMassPackOpenable", "NativeUIProbe", "MassOpenableOnly"),
    ("PackOpeningDirector", "FinishMassPackOpen", "NativeUIProbe", "BeforeMassDone"),
    ("Gameplay", "OnBoardLoaded", "ClientBridge", "RetryBoardLoad"),
    ("ThinkEmoteManager", "Update", "ClientBridge", "AllowIdleThinkEmotes"),
    ("GameEntity", "NotifyOfMulliganInitialized", "NativeNpcPresence", "LoadEnemyEmoteHandler"),
    ("RemoteActionHandler", "CanReceiveEnemyEmote", "NativeNpcPresence", "CanReceiveEnemyEmote"),
    ("Player", "UpdateName", "NativeNpcPresence", "UpdateOpponentName"),
    ("DeckRule_CountCopiesOfEachCard", "GetCopiesInDeck", "NativeUIProbe", "LocalDeckCopyCount"),
    ("BnetBarMenuButton", "UpdateHighlight", "NativeUIProbe", "HiddenMenuHighlight"),
    ("Options", "GetBoolImpl", "NativeUIProbe", "OnboardingOption"),
    ("TutorialDeathKnightDeckBuilding", "HasSeenTutorial", "NativeUIProbe", "DeathKnightTipSeen"),
    ("TutorialDeathKnightDeckBuilding", "SetTutorialSeen", "NativeUIProbe", "MarkDeathKnightTipSeen"),
    ("Options", "GetBoolImpl", "LaneMute", "MuteBool"),
    ("Options", "GetFloatImpl", "LaneMute", "MuteFloat"),
    ("BoxDressingManager", "GetCurrentBoxDressing", "HubTheme", "CurrentDressing"),
    ("Box", "ShowEventBoxDressing", "HubTheme", "BeforeShow"),
    ("Box", "ShowEventBoxDressing", "HubTheme", "AfterShow"),
    ("CollectionManager", "SendCreateDeck", "NativeUIProbe", "CreateLocalDraft"),
    ("CollectionManager", "OnDeckDeletedWhileOffline", "NativeUIProbe", "DeleteLocalDraft"),
    ("CollectionDeckTray", "DeleteEditingDeck", "NativeUIProbe", "PrepareEditingDeckDelete"),
    ("DeckTrayDeckListContent", "DeleteDeck", "NativeUIProbe", "DeleteLocalDraftBeforeNative"),
    ("CollectionDeckBoxVisual", "OnDeleteButtonConfirmationResponse", "NativeUIProbe", "ConfirmListDeckDelete"),
    ("DeckOptionsMenu", "OnDeleteButtonConfirmationResponse", "NativeUIProbe", "ConfirmEditingDeckDelete"),
    ("CollectionDeckTray", "OnBackOutOfDeckContentsImpl", "NativeUIProbe", "PrepareDiscardDeck"),
    ("CollectionDeck", "SendChanges", "NativeUIProbe", "SaveLocalDraft"),
    ("DeckTrayReorderableContent", "StopDragToReorder", "NativeUIProbe", "ReorderLocalDrafts"),
    ("CollectionManager", "AddPendingDeckRename", "NativeUIProbe", "ReadOnly"),
    ("CollectionManager", "AddPendingDeckEdit", "NativeUIProbe", "ReadOnly"),
    ("CollectionManagerDisplay", "CreateDeckFromClipboard", "NativeUIProbe", "BlockDeckImport"),
    ("CollectionInputMgr", "PasteDeckInEditModeFromShareableDeck", "NativeUIProbe", "BlockDeckImport"),
    ("DeckTrayDeckListContent", "ShouldDeckBoxesBeLocked", "NativeUIProbe", "UnlockLocalDeckBoxSlots"),
    ("CollectionDeckBoxVisual", "SetDeckID", "NativeUIProbe", "LockLoanerBox"),
    ("CollectionDeckBoxVisual", "ShowDeleteButton", "NativeUIProbe", "HideLoanerDeleteButton"),
    ("DeckOptionsMenu", "ShowConvertButton", "NativeUIProbe", "HidePersonalDeckConvert"),
    ("CollectionManager", "ShouldShowWildToStandardTutorial", "NativeUIProbe", "HidePersonalDeckTutorial"),
    ("DeckHelper", "HasChoicesToOffer", "NativeUIProbe", "HidePersonalDeckHelperChoices"),
    ("DeckTrayCardListContent", "ShowDeckHelper", "NativeUIProbe", "BlockPersonalDeckHelper"),
    ("CollectionDeckTileActor", "UpdateAllComponents", "NativeUIProbe", "RestorePersonalDeckGhost"),
    ("CollectionDeckTray", "CompleteMyDeckButtonPress", "NativeUIProbe", "BlockPersonalDeckCompletion"),
    ("RewardBoxesDisplay", "CreateRewardInstance", "NativeRewardUI", "ObserveRewardActor"),
    ("RewardBoxesDisplay", "OpenReward", "NativeRewardUI", "ObserveRewardOpened"),
    ("RewardBoxesDisplay", "OnDoneButtonShown", "NativeRewardUI", "ObserveRewardDoneReady"),
    ("RewardBoxesDisplay", "OnNavigateBack", "NativeRewardUI", "BeforeRewardNavigateBack"),
    ("RewardBoxesDisplay", "OnDestroy", "NativeRewardUI", "BeforeRewardDestroy"),
    ("BnetBar", "HandleEscapeKey", "NativeRewardUI", "BeforeRewardEscape"),
    ("NarrativeManager", "OnArenaRewardsShown", "NativeRewardUI", "SuppressOwnedArenaNarrative"),
    ("PopupDisplayManager", "CanShowRankedIntroForNewPlayer", "NativeRewardUI", "SuppressLocalRankedIntro"),
    ("DialogManager", "AddToQueue", "NativeRewardUI", "ObserveRankedIntro"),
    ("Network", "CraftingTransaction", "NativeCrafting", "CommitBatch"),
    ("Network", "MassDisenchant", "NativeCrafting", "CommitMass"),
    ("Network", "GetMassDisenchantResponse", "NativeCrafting", "MassResponse"),
    ("CollectionManager", "OnMassDisenchantResponse", "NativeCrafting", "MassCompleted"),
    ("MassDisenchant", "UpdateContents", "NativeCrafting", "PreviewMass"),
    ("CraftingManager", "GetCardValue", "NativeCrafting", "Price"),
    ("CraftingManager", "CanCraftCardRightNow", "NativeCrafting", "CanCraft"),
    ("CraftingUI", "DoCreate", "NativeCrafting", "BeforeCreate"),
    ("CraftingUI", "DoDisenchant", "NativeCrafting", "BeforeDisenchant"),
    ("UpgradeToGoldenPopup", "HandleEvent", "NativeCrafting", "BeforeUpgradeEvent"),
    ("CraftingUI", "UpdateCraftingButtonsAndSoulboundText", "NativeCrafting", "ProtectPremiumButton"),
    ("FixedRewardsMgr", "CanCraftCard", "NativeCrafting", "Permission"),
    ("CraftingUI", "IsCraftingEventForCardActive", "NativeCrafting", "CraftingEvent"),
    ("CollectionManager", "RequestDeckContentsForDecksWithoutContentsLoaded", "NativeCrafting", "DeckContentsReady"),
    ("CollectionDeck", "SendChanges", "NativeCrafting", "PreserveRecipe"),
    ("Network", "ValidateAchieve", "NativeCrafting", "LocalOnly"),
    ("Network", "SetClientOptions", "NativeCrafting", "LocalOnly"),
    ("Network", "SendDeckData", "NativeCrafting", "LocalOnly"),
    ("Network", "RequestDeckContents", "NativeCrafting", "LocalOnly"),
    ("CraftingManager", "TellServerAboutWhatUserDid", "NativeCrafting", "BeforeClose"),
    ("CraftingManager", "TellServerAboutWhatUserDid", "NativeCrafting", "AfterClose"),
    ("MassDisenchant", "BlockUI", "NativeCrafting", "MassUnblocked"),
    ("Network", "AckCardSeenBefore", "NativeCollectionStatus", "AckSeen"),
    ("Options", "SetFormatType", "NativeFormatLock", "StandardFormat"),
    ("DeckPickerTrayDisplay", "SwitchFormatTypeAndRankedPlayMode", "NativeFormatLock", "StandardMode"),
    ("Options", "SetInRankedPlayMode", "NativeFormatLock", "RankedPlayMode"),
    ("DeckPickerTrayDisplay", "ShowFormatTypePickerPopup", "NativeFormatLock", "PickerShown"),
    // Test lanes must not persist or re-apply window geometry in the options file they share with lane 0.
    ("GraphicsManager", "Shutdown", "LaneWindow", "KeepSharedWindowOptions"),
    ("GraphicsManager", "InitializeScreen", "LaneWindow", "KeepSharedWindowOptions"),
    ("OptionsMenu", "Awake", "NativeOptionsPanels", "AddButtons"),
    ("BnetBar", "HandleEscapeKey", "NativeOptionsPanels", "BeforeEscape"),
    ("GameMenu", "GetButtons", "NativeGameMenuTidy", "Tidy"),
    ("AlertPopup", "UpdateLayout", "NativeHelpUI", "WidenHelpText"),
    ("AlertPopup", "UpdateLayout", "NativeHelpUI", "FitHelpPopup"),
    ("UnityTouchScreenService", "GetBatteryMode", "NativePowerSource", "AlwaysMains"),
    ("GraphicsManager", "UpdateFramerateSettings", "NativePowerSource", "RememberRate"),
    ("GraphicsManager", "UpdateFramerateSettings", "NativePowerSource", "KeepRate"),
    ("MulliganManager", "BeginDealNewCards", "DeckLayout", "BeforeNativeTransition"),
    ("MulliganManager", "SkipMulligan", "DeckLayout", "BeforeNativeTransition"),
    ("MulliganManager", "OnDestroy", "DeckLayout", "BeforeNativeTransition"),
    ("MulliganManager", "ForceMulliganActive", "DeckLayout", "BeforeActiveChange"),
    // Feats: native Journal popup fed from local achievements (NativeAchievements).
    ("Hearthstone.Progression.AchievementManager", "get_TotalPointsExcludeRetired", "NativeAchievements", "TotalPoints"),
    ("Hearthstone.Progression.AchievementManager", "LoadReward", "NativeAchievements", "SkipLocalReward"),
    ("Hearthstone.Progression.AchievementManager", "GetClaimedDate", "NativeAchievements", "LocalClaimedDate"),
    ("Hearthstone.Progression.AchievementManager", "GetAchievementDataModel", "NativeAchievements", "LocalAchievementModel"),
    ("Hearthstone.Progression.AchievementManager", "ClaimAchievementReward", "NativeAchievements", "RefuseLocalClaim"),
    ("Hearthstone.Progression.Section", "LoadAchievements", "NativeAchievements", "KeepLocalSection"),
    ("Hearthstone.Progression.JournalPopup", "HandleEvent", "NativeAchievements", "PopupEvent"),
    ("Hearthstone.Progression.JournalPopup", "OnDestroy", "NativeAchievements", "PopupDestroyed"),
    ("Hearthstone.Progression.JournalTrayDisplay", "HandleTabSelected", "NativeAchievements", "TabSelected"),
    ("Hearthstone.Progression.JournalButton", "HandleEvent", "NativeAchievements", "JournalButtonEvent"),
    ("Hearthstone.Progression.JournalButton", "ShowJournal", "NativeAchievements", "JournalButtonShow"),
    ("Hearthstone.Progression.AchievementInspectionPopup", "UpdateAchievement", "NativeAchievements", "InspectionDetail")
};
var failures = new List<string>();
if (pluginMetadata.TypeDefinitions.Select(pluginMetadata.GetTypeDefinition).SelectMany(type => type.GetMethods())
    .Select(pluginMetadata.GetMethodDefinition).Any(method => pluginMetadata.GetString(method.Name) == "BlockPersonalSuggestionCraft"))
    failures.Add("DeckHelper missing-card preview must retain native crafting; obsolete blocker still present");
if (candidatePlugin == null)
{
    foreach (var handle in engineMetadata.TypeReferences)
    {
        var type = engineMetadata.GetTypeReference(handle);
        string space = engineMetadata.GetString(type.Namespace);
        string name = engineMetadata.GetString(type.Name);
        if (space == "System.Runtime.Serialization.Json" || space == "System.Text.Json" || space.StartsWith("System.Reflection.Emit")
            || name == "DataContractAttribute" || name == "DataMemberAttribute" || name == "DataContractSerializer")
            failures.Add("Unity engine references unavailable serializer/emit API: " + space + "." + name);
    }
    foreach (string api in new[] { "CloneExact", "CloneForSimulation", "TryAnswer" })
        if (!pluginMetadata.MemberReferences.Select(pluginMetadata.GetMemberReference)
            .Any(member => pluginMetadata.GetString(member.Name) == api)) failures.Add("Source plugin does not use " + api);
}
var gameplayEvent = MethodSignature(gameMetadata, "GameUtils", "IsCardGameplayEventActive",
    new[] { "String" }, "Boolean", signatures);
if ((gameplayEvent.Attributes & (MethodAttributes.Public | MethodAttributes.Static))
    != (MethodAttributes.Public | MethodAttributes.Static))
    failures.Add("GameUtils::IsCardGameplayEventActive(String) must be public static Boolean");
var proofPrefixes = pluginMetadata.TypeDefinitions.Select(pluginMetadata.GetTypeDefinition)
    .Single(type => pluginMetadata.GetString(type.Name) == "NativeUIProbe").GetMethods()
    .Select(pluginMetadata.GetMethodDefinition)
    .Where(method => pluginMetadata.GetString(method.Name) == "ProofCardPlayable").ToArray();
if (candidatePlugin == null)
{
    if (proofPrefixes.Length != 0) failures.Add("normal plugin contains candidate playability override");
}
else if (proofPrefixes.Length != 1)
    failures.Add("candidate plugin needs exact per-card playability prefix");
else
{
    var prefix = proofPrefixes[0];
    var signature = prefix.DecodeSignature(signatures, (object)null);
    if ((prefix.Attributes & MethodAttributes.Static) == 0 || signature.ReturnType != "Boolean"
        || !signature.ParameterTypes.SequenceEqual(new[] { "String", "Boolean&" })
        || !Parameters(pluginMetadata, prefix).Select(parameter => pluginMetadata.GetString(parameter.Name))
            .SequenceEqual(new[] { "__0", "__result" }))
        failures.Add("ProofCardPlayable: invalid exact string-overload Harmony prefix signature");
}
foreach (var (typeName, methodName, patchType, patchName) in hooks)
{
    var original = typeName == "CraftingManager" && methodName == "GetCardValue"
        ? MethodSignature(gameMetadata, typeName, methodName,
            new[] { "String", "TAG_PREMIUM", "CardValueDbfRecord&" }, "CardValue", signatures)
        : typeName == "UnopenedPack" && methodName == "CanOpenPack"
            ? MethodSignature(gameMetadata, typeName, methodName, new[] { "String&" }, "Boolean", signatures)
            : Method(gameMetadata, typeName, methodName);
    var patch = Method(pluginMetadata, patchType, patchName);
    var arguments = Parameters(gameMetadata, original);
    if (typeName == "UnopenedPack"
        && ((original.Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public
            || (arguments.Single().Attributes & ParameterAttributes.Out) == 0))
        failures.Add("UnopenedPack::CanOpenPack must be public Boolean with out String");
    var originalTypes = original.DecodeSignature(signatures, (object)null).ParameterTypes;
    var patchSignature = patch.DecodeSignature(signatures, (object)null);
    Console.WriteLine(typeName + "::" + methodName + "(" + string.Join(", ", arguments.Select(p => originalTypes[p.SequenceNumber - 1] + " " + gameMetadata.GetString(p.Name))) + ") <- " + patchName);
    foreach (var parameter in Parameters(pluginMetadata, patch))
    {
        string name = pluginMetadata.GetString(parameter.Name);
        string expected;
        if (name == "__instance")
        {
            if ((original.Attributes & MethodAttributes.Static) != 0) throw new Exception("Static target has no instance: " + methodName);
            expected = typeName.Substring(typeName.LastIndexOf('.') + 1);
        }
        else if (name == "__state")
        {
            expected = patchName == "BeforeOpen" ? "Int32&" : "Boolean&";
            if ((parameter.Attributes & ParameterAttributes.Out) == 0)
                failures.Add(patchName + ": __state must be an out parameter owned by this invocation");
        }
        else if (name == "__result")
        {
            expected = original.DecodeSignature(signatures, (object)null).ReturnType + "&";
        }
        else if (name == "__runOriginal")
        {
            expected = "Boolean";   // Harmony fills it: false when an earlier prefix already skipped the original
        }
        else if (name.StartsWith("___", StringComparison.Ordinal))
        {
            var field = Field(gameMetadata, typeName, name.Substring(3));
            expected = field.DecodeSignature(signatures, (object)null) + "&";
        }
        else
        {
            int index;
            if (!name.StartsWith("__", StringComparison.Ordinal) || !int.TryParse(name.Substring(2), out index))
                index = Array.FindIndex(arguments, p => gameMetadata.GetString(p.Name) == name);
            if (index < 0 || index >= originalTypes.Length)
            {
                failures.Add(patchName + ": parameter " + name + " not found in " + typeName + "::" + methodName);
                continue;
            }
            expected = originalTypes[index];
        }
        string actual = patchSignature.ParameterTypes[parameter.SequenceNumber - 1];
        bool typeMatches = actual == expected
            || (!expected.EndsWith("&", StringComparison.Ordinal) && actual == expected + "&");
        if (!typeMatches) failures.Add(patchName + ": " + name + " type " + actual + " != " + expected);
    }
    if (patchSignature.ReturnType != "Boolean" && patchSignature.ReturnType != "Void")
        failures.Add(patchName + ": invalid patch return type " + patchSignature.ReturnType);
}
// LaneWindow keeps background lanes rendering: Application.runInBackground must stay settable on the pinned Unity.
using (var unityCore = new PEReader(File.OpenRead(Path.Combine(root, "references/game/UnityEngine.CoreModule.dll"))))
{
    var unity = unityCore.GetMetadataReader();
    foreach (var contract in new[] {
        ("Application", "get_productName", Array.Empty<string>(), "String"),
        ("SystemInfo", "get_operatingSystem", Array.Empty<string>(), "String")
    }) MethodSignature(unity, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures);
    var application = unity.TypeDefinitions.Select(unity.GetTypeDefinition)
        .Where(t => unity.GetString(t.Name) == "Application" && unity.GetString(t.Namespace) == "UnityEngine").ToArray();
    if (application.Length != 1 || !application[0].GetMethods().Select(unity.GetMethodDefinition)
            .Any(m => unity.GetString(m.Name) == "set_runInBackground"))
        failures.Add("UnityEngine.Application::set_runInBackground missing (lane background rendering)");
}
using (var imguiFile = new PEReader(File.OpenRead(Path.Combine(root, "references/game/UnityEngine.IMGUIModule.dll"))))
    MethodSignature(imguiFile.GetMetadataReader(), "GUIUtility", "set_systemCopyBuffer", new[] { "String" }, "Void", signatures);
foreach (var contract in new[] {
    ("GameMenu", "GameMenuHide", Array.Empty<string>(), "Void"),
    ("ButtonListMenu", "Show", new[] { "Boolean" }, "Void"),
    ("ButtonListMenu", "Hide", Array.Empty<string>(), "Void"),
    ("ButtonListMenu", "OnBlockerRelease", new[] { "UIEvent" }, "Void")
}) MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures);
if (Field(gameMetadata, "ButtonListMenu", "m_allButtons").DecodeSignature(signatures, (object)null) != "List`1<UIBButton>")
    failures.Add("Report form ButtonListMenu.m_allButtons type changed");
if (Field(gameMetadata, "GameMenu", "m_quitButton").DecodeSignature(signatures, (object)null) != "UIBButton")
    failures.Add("Report row GameMenu.m_quitButton type changed");
if (Field(gameMetadata, "ButtonListMenu", "NORMAL_SCALE").DecodeSignature(signatures, (object)null) != "Vector3")
    failures.Add("Report row GameMenu animation scale contract changed");
if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "OfflineProbe", "Update")).Contains(("NativeReportUI", "Update")))
    failures.Add("Report completion is not polled from main Update");
// NativeNpcPresence reflection targets (enemy emote handler placement callback, opponent name field).
Method(gameMetadata, "GameEntity", "EnemyEmoteHandlerDoneLoadingCallback");
if (!gameMetadata.TypeDefinitions.Select(gameMetadata.GetTypeDefinition).Single(t => gameMetadata.GetString(t.Name) == "Player"
        && t.Namespace.IsNil && t.GetDeclaringType().IsNil).GetFields().Select(gameMetadata.GetFieldDefinition)
    .Any(f => gameMetadata.GetString(f.Name) == "m_name"))
    failures.Add("Player.m_name missing (native opponent name)");
Method(gameMetadata, "EnemyEmoteHandler", "IsSquelched");
Method(gameMetadata, "EmoteHandler", "IsValidEmoteTypeForOpponent");
foreach (string option in new[] { "HAS_SEEN_MULLIGAN", "HAS_SEEN_THE_COIN", "HAS_SEEN_STEALTH_TAUNTER" })
    foreach (string owner in new[] { "Option", "ServerOptionFlag" })
        if (TryEnumValue(gameMetadata, owner, option) == null)
            failures.Add(owner + "::" + option + " match onboarding flag missing");
var finalizer = Method(pluginMetadata, "NativeUIProbe", "FinishLocalReorder");
var finalizerSignature = finalizer.DecodeSignature(signatures, (object)null);
var finalizerParameters = Parameters(pluginMetadata, finalizer);
if (finalizerSignature.ReturnType != "Exception" || finalizerParameters.Length != 3
    || pluginMetadata.GetString(finalizerParameters[0].Name) != "__instance"
    || finalizerSignature.ParameterTypes[0] != "DeckTrayReorderableContent"
    || pluginMetadata.GetString(finalizerParameters[1].Name) != "__exception"
    || finalizerSignature.ParameterTypes[1] != "Exception"
    || pluginMetadata.GetString(finalizerParameters[2].Name) != "__state"
    || finalizerSignature.ParameterTypes[2] != "Boolean")
    failures.Add("FinishLocalReorder: invalid Harmony finalizer signature");
// #37: the picker switch's finalizer clears the single-use switch flag even when the native body throws.
var modeDone = Method(pluginMetadata, "NativeFormatLock", "StandardModeDone");
var modeDoneSignature = modeDone.DecodeSignature(signatures, (object)null);
var modeDoneParameters = Parameters(pluginMetadata, modeDone);
if (modeDoneSignature.ReturnType != "Exception" || modeDoneParameters.Length != 2
    || pluginMetadata.GetString(modeDoneParameters[0].Name) != "__exception" || modeDoneSignature.ParameterTypes[0] != "Exception"
    || pluginMetadata.GetString(modeDoneParameters[1].Name) != "__0" || modeDoneSignature.ParameterTypes[1] != "VisualsFormatType"
    || !File.ReadAllText(Path.Combine(root, "plugin/NativeFormatLock.cs")).Contains("finalizer: new HarmonyMethod(typeof(NativeFormatLock), nameof(StandardModeDone))"))
    failures.Add("NativeFormatLock.StandardModeDone: invalid Harmony finalizer signature or registration");
var pendingSend = Method(gameMetadata, "CollectionDeck", "SendChangesIfPending");
var pendingSignature = pendingSend.DecodeSignature(signatures, (object)null);
var pendingSource = Field(gameMetadata, "CollectionDeck", "m_pendingChangeSource");
if (pendingSignature.ReturnType != "Void" || Parameters(gameMetadata, pendingSend).Length != 0
    || pendingSource.DecodeSignature(signatures, (object)null) != "ChangeSource")
    failures.Add("CollectionDeck::SendChangesIfPending: pending ChangeSource contract changed");
// The hook uses erased asset/delegate types, so check the native overload contract directly.
try
{
    MethodSignature(gameMetadata, "PrefabInstanceLoadTracker", "InstantiatePrefab",
        new[] { "Context", "AssetReference", "PrefabCallback`1<GameObject>", "Object", "AssetLoadingOptions" },
        "Boolean", signatures);
}
catch { failures.Add("PrefabInstanceLoadTracker::InstantiatePrefab five-parameter Boolean contract changed"); }
foreach (var contract in new[] {
    ("SoundManager", "Get", Array.Empty<string>(), "SoundManager"),
    ("SoundManager", "IsInitialized", Array.Empty<string>(), "Boolean"),
    ("DeckTrayDeckListContent", "CreateNewDeckFromUserSelection",
        new[] { "TAG_CLASS", "String", "String", "DeckSourceType", "String" }, "Void"),
    ("CollectionManager", "OnDeckCreated", new[] { "DeckHeader", "Nullable`1<Int32>" }, "Void"),
    ("DeckOptionsMenu", "ShowConvertButton", Array.Empty<string>(), "Boolean"),
    ("CollectionManager", "ShouldShowWildToStandardTutorial", new[] { "Boolean" }, "Boolean"),
    ("DeckHelper", "HasChoicesToOffer", new[] { "CollectionDeck" }, "Boolean"),
    ("DeckTrayCardListContent", "ShowDeckHelper", new[] { "CollectionDeckSlot", "Boolean" }, "Void"),
    ("CollectionDeckTray", "CompleteMyDeckButtonPress", Array.Empty<string>(), "Void"),
    ("CollectionDeckTray", "GetCurrentDeckContext", Array.Empty<string>(), "CollectionDeck"),
    ("CollectionDeckBoxVisual", "ShowDeleteButton", new[] { "Boolean" }, "Void")
})
    try { MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures); }
    catch { failures.Add(contract.Item1 + "::" + contract.Item2 + " native deck contract changed"); }

// One owned retail toast, built from local saved receipts. Pin the complete lifecycle rather than stock ShowFake.
foreach (var contract in new[] {
    ("AchievementToast", "Initialize", new[] { "AchievementDataModel" }, "Void"),
    ("AchievementToast", "Show", Array.Empty<string>(), "Void"),
    ("AchievementToast", "Hide", Array.Empty<string>(), "Void"),
    ("Widget", "get_IsReady", Array.Empty<string>(), "Boolean"),
    ("Widget", "RegisterReadyListenerDoOnce", new[] { "Action`1<Object>", "Object", "Boolean" }, "Void"),
    ("Widget", "RemoveReadyListener", new[] { "Action`1<Object>" }, "Void"),
    ("WidgetInstance", "Create", new[] { "String", "Boolean", "LoadingPolicy" }, "WidgetInstance"),
    ("WidgetInstance", "add_WidgetLoadFailed", new[] { "Action" }, "Void"),
    ("WidgetInstance", "remove_WidgetLoadFailed", new[] { "Action" }, "Void"),
    ("WidgetInstance", "OnDestroy", Array.Empty<string>(), "Void"),
    ("WidgetInstance", "Deinitialize", Array.Empty<string>(), "Void"),
    ("WidgetInstance", "Unload", Array.Empty<string>(), "Void"),
    ("AchievementDataModel", "set_Name", new[] { "String" }, "Void"),
    ("AchievementDataModel", "set_Progress", new[] { "Int32" }, "Void"),
    ("AchievementDataModel", "set_Quota", new[] { "Int32" }, "Void"),
    ("AchievementDataModel", "set_Status", new[] { "AchievementStatus" }, "Void"),
    ("AchievementDataModel", "set_NextTierID", new[] { "Int32" }, "Void"),
    ("AchievementDataModel", "set_Tier", new[] { "Int32" }, "Void"),
    ("AchievementDataModel", "set_DisplayMode", new[] { "AchievementDisplayMode" }, "Void"),
    ("AchievementDataModel", "set_IsLocked", new[] { "Boolean" }, "Void"),
    ("SpellStateAudioSource", "Play", new[] { "SpellState" }, "Void"),
    ("SpellStateAudioSource", "PlayNow", Array.Empty<string>(), "Void")
})
    try { MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures); }
    catch { failures.Add(contract.Item1 + "::" + contract.Item2 + " achievement toast contract changed"); }
if (Field(gameMetadata, "BnetBar", "m_socialToastBone").DecodeSignature(signatures, (object)null) != "GameObject")
    failures.Add("BnetBar.m_socialToastBone achievement toast host changed");
foreach (int row in new[] { 0x1474E, 0x1476A })
{
    var method = gameMetadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row));
    string owner = gameMetadata.GetString(gameMetadata.GetTypeDefinition(method.GetDeclaringType()).Name);
    if (owner != "SoundManager" || gameMetadata.GetString(method.Name) != (row == 0x1474E ? "Play" : "PlayClip"))
        failures.Add("SoundManager achievement audio observation token changed: 0x" + row.ToString("X"));
}
MethodSignature(pluginMetadata, "NativeAchievementToast", "BeforeHide", new[] { "AchievementToast" }, "Void", signatures);
MethodSignature(pluginMetadata, "NativeAchievementToast", "BeforeSpellAudio", new[] { "Object", "Boolean&" }, "Void", signatures);
MethodSignature(pluginMetadata, "NativeAchievementToast", "AfterSpellAudio", new[] { "Exception", "Boolean" }, "Exception", signatures);
MethodSignature(pluginMetadata, "NativeAchievementToast", "ObserveSoundCall", new[] { "MethodBase", "Object[]" }, "Void", signatures);
MethodSignature(pluginMetadata, "NativeAchievementToast", "BeforeStateSound", new[] { "Object", "MethodBase", "Boolean&" }, "Void", signatures);
MethodSignature(pluginMetadata, "NativeAchievementToast", "AfterStateSound", new[] { "Exception", "Boolean" }, "Exception", signatures);
MethodSignature(pluginMetadata, "NativeAchievementToastSentinel", "OnDestroy", Array.Empty<string>(), "Void", signatures);
MethodSignature(pluginMetadata, "NativeAchievementStep", "CancelForContext", Array.Empty<string>(), "Void", signatures);
foreach (var contract in new[] {
    ("PlaySoundClipStateAction", "Run", new[] { "Boolean" }, "Void"),
    ("PlaySoundClipStateAction", "HandleReady", new[] { "Object" }, "Void"),
    ("SoundManager", "LoadAndPlayClip", new[] { "AssetReference", "SoundPlayClipArgs" }, "Boolean")
})
    try { MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures); }
    catch { failures.Add(contract.Item1 + "::" + contract.Item2 + " achievement sound diagnostic contract changed"); }
foreach (var (owner, field, type) in new[] {
    ("StateActionImplementation", "<StateAction>k__BackingField", "StateAction"),
    ("VisualController", "m_stateCollection", "WidgetBehaviorStateCollection")
})
    try { if (Field(gameMetadata, owner, field).DecodeSignature(signatures, (object)null) != type) failures.Add(owner + "." + field + " sound diagnostic field type changed"); }
    catch { failures.Add(owner + "." + field + " achievement sound diagnostic field missing"); }
if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeUIProbe", "Initialize"))
        .Contains(("NativeAchievementToast", "Initialize"))
    || !Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeUIProbe", "Update"))
        .Contains(("NativeAchievementToast", "Update")))
    failures.Add("native achievement toast is not wired into the probe lifecycle");
foreach (string soundField in new[] { "m_isMasterEnabled", "m_isMusicEnabled" })
    if (Field(gameMetadata, "SoundManager", soundField).DecodeSignature(signatures, (object)null) != "Boolean")
        failures.Add("SoundManager::" + soundField + " must stay a Boolean field (lane mute resets it)");
// Lane mute forces exactly these native option names; a rename would silently un-mute test copies.
foreach (string optionName in new[] { "SOUND", "MUSIC", "SOUND_VOLUME", "MUSIC_VOLUME", "AMBIENCE_VOLUME", "DIALOG_VOLUME", "SOUND_EFFECT_VOLUME" })
{
    // Other nested types are also called "Option": require the top-level enum.
    var optionType = gameMetadata.TypeDefinitions.Select(gameMetadata.GetTypeDefinition)
        .Single(t => gameMetadata.GetString(t.Name) == "Option" && t.GetDeclaringType().IsNil);
    if (!optionType.GetFields().Select(gameMetadata.GetFieldDefinition).Any(f => gameMetadata.GetString(f.Name) == optionName))
        failures.Add("Option::" + optionName + " missing (lane mute)");
}
var directTagMappings = new[] {
    ("COLLECTIBLE", "COLLECTIBLE", "COLLECTIBLE"), ("CARD_SET", "CARD_SET", "CARD_SET"),
    ("CLASS", "CLASS", "CLASS"), ("CARDTYPE", "CARDTYPE", "CARDTYPE"), ("RARITY", "RARITY", "RARITY"),
    ("CARDRACE", "CARDRACE", "CARDRACE"), ("ELITE", "ELITE", "ELITE"), ("FACTION", "FACTION", "FACTION"),
    ("COST", "COST", "COST"), ("ATK", "ATK", "ATK"), ("HEALTH", "HEALTH", "HEALTH"),
    ("DURABILITY", "HEALTH", "DURABILITY"), ("SPELLPOWER", "SPELLPOWER", "SPELLPOWER"),
    ("AFFECTED_BY_SPELL_POWER", "AFFECTED_BY_SPELL_POWER", "AFFECTED_BY_SPELL_POWER"),
    ("BATTLECRY", "BATTLECRY", "BATTLECRY"), ("DEATHRATTLE", "DEATHRATTLE", "DEATHRATTLE"),
    ("SECRET", "SECRET", "SECRET"), ("TAUNT", "TAUNT", "TAUNT"), ("CHARGE", "CHARGE", "CHARGE"),
    ("RUSH", "RUSH", "RUSH"), ("DIVINE_SHIELD", "DIVINE_SHIELD", "DIVINE_SHIELD"),
    ("STEALTH", "STEALTH", "STEALTH"), ("WINDFURY", "WINDFURY", "WINDFURY"),
    ("POISONOUS", "POISONOUS", "POISONOUS"), ("FREEZE", "FREEZE", "FREEZE"),
    ("CANT_ATTACK", "CANT_ATTACK", "CANT_ATTACK"),
    ("CANT_BE_TARGETED_BY_SPELLS", "CANT_BE_TARGETED_BY_SPELLS", "CANT_BE_TARGETED_BY_SPELLS"),
    ("CANT_BE_TARGETED_BY_HERO_POWERS", "CANT_BE_TARGETED_BY_HERO_POWERS", "CANT_BE_TARGETED_BY_HERO_POWERS"),
    ("AURA", "AURA", "AURA"), ("ADJACENT_BUFF", "ADJACENT_BUFF", "ADJACENT_BUFF"),
    ("ENRAGED", "ENRAGED", "ENRAGED"), ("SILENCE", "SILENCE", "SILENCE")
};
foreach (var mapping in directTagMappings)
{
    int nativeValue = EnumValue(gameMetadata, "GAME_TAG", mapping.Item2);
    int engineValue = EnumValue(engineMetadata, "GameTag", mapping.Item3);
    if (mapping.Item1 == "DURABILITY")
    {
        if (nativeValue != EnumValue(gameMetadata, "GAME_TAG", "HEALTH")
            || engineValue != EnumValue(engineMetadata, "GameTag", "DURABILITY"))
            failures.Add("DURABILITY must map weapon-only native HEALTH to engine DURABILITY");
    }
    else if (nativeValue != engineValue) failures.Add(mapping.Item1 + ": native/engine tag values differ");
}
foreach (string tag in new[] { "CHARGE", "COUNTER", "DIVINE_SHIELD", "FREEZE", "SECRET", "SILENCE", "SPELLPOWER", "TAUNT", "WINDFURY" })
    if (EnumValue(gameMetadata, "GAME_TAG", tag) != EnumValue(engineMetadata, "GameTag", tag))
        failures.Add(tag + ": referenced native/engine tag values differ");
foreach (string tag in new[] { "CARD_SET", "FACTION", "TRIGGER_VISUAL", "TECH_LEVEL", "IS_BACON_POOL_MINION" })
    if (EnumValue(gameMetadata, "GAME_TAG", tag) != EnumValue(engineMetadata, "GameTag", tag))
        failures.Add(tag + ": metadata native/engine tag values differ");
if (TryEnumValue(gameMetadata, "GAME_TAG", "DISCARD_CARDS") != null
    || EnumValue(engineMetadata, "GameTag", "DISCARD_CARDS") != 890)
    failures.Add("DISCARD_CARDS availability contract changed");
foreach (string tag in new[] { "AttackVisualType", "DevState" })
    if (TryEnumValue(gameMetadata, "GAME_TAG", tag) != null || TryEnumValue(engineMetadata, "GameTag", tag) == null)
        failures.Add(tag + ": metadata availability contract changed");
if (EnumValue(gameMetadata, "GAME_TAG", "BACON_TRIPLE_UPGRADE_MINION_ID") != 1429
    || TryEnumValue(engineMetadata, "GameTag", "BACON_TRIPLE_UPGRADE_MINION_ID") != null)
    failures.Add("raw metadata tag 1429 availability contract changed");
foreach (var contract in new[] {
    ("GameUtils", "GetCardTagValue", new[] { "String", "GAME_TAG" }, "Int32"),
    ("EntityBase", "GetTag", new[] { "GAME_TAG" }, "Int32"),
    ("EntityBase", "GetTags", Array.Empty<string>(), "TagMap"),
    ("TagMap", "GetMap", Array.Empty<string>(), "Dictionary`2<Int32,Int32>"),
    ("EntityBase", "GetReferencedTag", new[] { "GAME_TAG" }, "Int32"),
    ("DefLoader", "HasLoadedEntityDefs", Array.Empty<string>(), "Boolean"),
    ("DefLoader", "GetEntityDef", new[] { "String" }, "EntityDef"),
    ("CardTextBuilder", "GetDefaultCardName", new[] { "EntityDef" }, "String"),
    ("CardTextBuilder", "GetRawCardName", new[] { "EntityDef" }, "String")
})
{
    var method = MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures);
    if (method.DecodeSignature(signatures, (object)null).ReturnType != contract.Item4)
        failures.Add(contract.Item1 + "::" + contract.Item2 + " return contract changed");
}
try
{
    MethodSignature(engineMetadata, "PowerHistoryBuilder", "FullEntity",
        new[] { "IPlayable", "Boolean" }, "PowerHistoryFullEntity", signatures);
    MethodSignature(pluginMetadata, "NativeMatch", "SnapshotVisibleTransportTags",
        new[] { "IPlayable" }, "IReadOnlyDictionary`2<Int32,Int32>", signatures);
    MethodSignature(pluginMetadata, "NativeMatch", "ValidateHistoryEntityIdentity",
        new[] { "PowerHistoryEntity" }, "Void", signatures);
    MethodSignature(pluginMetadata, "NativeUIProbe", "Navigate",
        new[] { "Mode" }, "Void", signatures);
    MethodSignature(pluginMetadata, "NativeUIProbe", "get_RewardRetryContextReady",
        Array.Empty<string>(), "Boolean", signatures);
    MethodSignature(pluginMetadata, "NativeUIProjection", "TryGetAuditDefinitionTag",
        new[] { "Boolean", "String", "Int32", "Int32&" }, "Boolean", signatures);
    MethodSignature(pluginMetadata, "ClientBridge", "PatchCopyTask",
        new[] { "Harmony", "Type", "String", "String" }, "Void", signatures);
    MethodSignature(pluginMetadata, "ClientBridge", "PatchAuraRemoval",
        new[] { "Harmony", "Type" }, "Void", signatures);
    foreach (string method in new[] {
        "RequiredProcess", "BeforeSummonCopy", "AfterSummonCopy",
        "BeforeTransformCopy", "AfterTransformCopy"
    }) Method(pluginMetadata, "CopyTransportBridge", method);
    foreach (string method in new[] { "RequiredRemoveInternal", "BeforeRemove", "AfterRemove" })
        Method(pluginMetadata, "AuraTransportBridge", method);
}
catch { failures.Add("compiled transport, copy/aura hook, reward retry, audit definition, or HUB navigation API missing"); }
foreach (string methodName in new[] { "NativeAuditMappings", "NativeMetadataMappings" })
    try { Method(pluginMetadata, "NativeUIProbe", methodName); }
    catch { failures.Add("NativeUIProbe::" + methodName + " compiled mapping missing"); }
try
{
    var guard = MethodSignature(pluginMetadata, "NativeUIProbe", "IsCanonicalDefinition",
        new[] { "EntityDef" }, "Boolean", signatures);
    if (guard.DecodeSignature(signatures, (object)null).ReturnType != "Boolean")
        failures.Add("NativeUIProbe::IsCanonicalDefinition return contract changed");
}
catch { failures.Add("NativeUIProbe::IsCanonicalDefinition compiled guard missing"); }
var localName = Method(pluginMetadata, "ClientBridge", "LocalCardName");
var localNameSignature = localName.DecodeSignature(signatures, (object)null);
if (localNameSignature.ReturnType != "Boolean"
    || !localNameSignature.ParameterTypes.SequenceEqual(new[] { "EntityDef", "String&" })
    || !Parameters(pluginMetadata, localName).Select(parameter => pluginMetadata.GetString(parameter.Name))
        .SequenceEqual(new[] { "__0", "__result" }))
    failures.Add("ClientBridge::LocalCardName: invalid Harmony name prefix signature");
// Held Herald card text: the pinned builder's private instance GetHeraldColossalName(Entity) reads the controller's tag 4534;
// ClientBridge prefixes it to read the card's own tag first (NativeHeraldText).
{
    var original = MethodSignature(gameMetadata, "HeraldCardTextBuilder", "GetHeraldColossalName", new[] { "Entity" }, "String", signatures);
    if ((original.Attributes & MethodAttributes.Static) != 0
        || (original.Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Private)
        failures.Add("HeraldCardTextBuilder::GetHeraldColossalName(Entity) must be a private instance method");
    var herald = Method(pluginMetadata, "ClientBridge", "HeraldColossalName");
    var heraldSignature = herald.DecodeSignature(signatures, (object)null);
    if (heraldSignature.ReturnType != "Boolean"
        || !heraldSignature.ParameterTypes.SequenceEqual(new[] { "Entity", "String&" })
        || !Parameters(pluginMetadata, herald).Select(parameter => pluginMetadata.GetString(parameter.Name)).SequenceEqual(new[] { "__0", "__result" }))
        failures.Add("ClientBridge::HeraldColossalName: invalid Harmony name prefix signature");
    // Source-level: the install line patches exactly that overload with this prefix.
    string bridge = File.ReadAllText(Path.Combine(root, "plugin/ClientBridge.cs"));
    if (!bridge.Contains("typeof(HeraldCardTextBuilder), \"GetHeraldColossalName\", nameof(HeraldColossalName), new[] { typeof(Entity) }"))
        failures.Add("ClientBridge no longer registers the HeraldCardTextBuilder card-tag prefix");
    foreach (var contract in new[] { ("EntityBase", "GetTag", new[] { "GAME_TAG" }, "Int32"), ("GameStrings", "Get", new[] { "String" }, "String") })
        MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures);
    // The Herald keyword tooltip is the second reader of the controller's tag 4534 (Cecil scan of every method: only
    // GetHeraldColossalName, this one, and the OnTagChange comparison). The transpiler needs exactly one `ldc.i4 4534; callvirt` here.
    var tooltip = MethodSignature(gameMetadata, "TooltipPanelManager", "SetupKeywordPanelIfNecessary",
        new[] { "KeywordPanelEntityInfo", "GAME_TAG" }, "Boolean", signatures);
    var tooltipIl = game.GetMethodBody(tooltip.RelativeVirtualAddress).GetILBytes();
    int reads = 0;
    for (int i = 0; i + 5 < tooltipIl.Length; i++)
        if (tooltipIl[i] == 0x20 && tooltipIl[i + 1] == 0xB6 && tooltipIl[i + 2] == 0x11 && tooltipIl[i + 3] == 0 && tooltipIl[i + 4] == 0 && tooltipIl[i + 5] == 0x6F) reads++;
    if (reads != 1) failures.Add("TooltipPanelManager::SetupKeywordPanelIfNecessary: expected exactly one ldc.i4 4534 + callvirt read, found " + reads);
    var tooltipTag = Method(pluginMetadata, "ClientBridge", "HeraldTooltipTag");
    var tooltipTagSignature = tooltipTag.DecodeSignature(signatures, (object)null);
    if (tooltipTagSignature.ReturnType != "Int32" || !tooltipTagSignature.ParameterTypes.SequenceEqual(new[] { "Player", "EntityBase" }))
        failures.Add("ClientBridge::HeraldTooltipTag: invalid replacement signature (Player, EntityBase) -> Int32");
    Method(pluginMetadata, "ClientBridge", "HeraldTooltipTranspiler");
    // A throwing Harmony.Patch must leave the applied flag cleared (the transpiler sets it before Patch returns), or the live check lies.
    if (!bridge.Contains("catch (Exception error) { HeraldTooltipApplied = false; HeraldTooltipStatus = \"patch failed: \""))
        failures.Add("ClientBridge: a failed Herald tooltip patch must clear HeraldTooltipApplied");
    if (!bridge.Contains("typeof(TooltipPanelManager), \"SetupKeywordPanelIfNecessary\"") || !bridge.Contains("nameof(HeraldTooltipTranspiler)"))
        failures.Add("ClientBridge no longer registers the Herald tooltip transpiler");
    Console.WriteLine("PASS HeraldCardTextBuilder card-tag prefix contract");
}
foreach (var contract in new[] {
    ("AssetLoader", "LoadGameObject", new[] { "AssetReference", "GameObjectCallback", "Object", "Boolean", "Boolean" }, "Boolean"),
    ("AssetLoader", "IsRuntimeAssetVariantAvailable", new[] { "AssetReference", "AssetLoadingOptions" }, "Boolean"),
    ("RewardBoxesDisplay", "SetLayer", new[] { "GameLayer" }, "Void"),
    ("RewardBoxesDisplay", "SetRewards", new[] { "List`1<RewardData>", "List`1<RewardData>" }, "Void"),
    ("RewardBoxesDisplay", "AnimateRewards", Array.Empty<string>(), "Void"),
    ("RewardBoxesDisplay", "RegisterDoneCallback", new[] { "Action" }, "Void"),
    // Post-match DETAILED RESULTS: a Done clone on the same PegUI release path, shown like RewardBoxesDisplay.AllDone.
    ("RewardBoxesDisplay", "OnDoneButtonPressed", new[] { "UIEvent" }, "Void"),
    ("NormalButton", "SetText", new[] { "String" }, "Void"),
    ("PegUIElement", "AddEventListener", new[] { "UIEventType", "Handler" }, "Boolean"),
    ("PegUIElement", "ClearEventListeners", Array.Empty<string>(), "Void"),
    ("PegUIElement", "SetEnabled", new[] { "Boolean", "Boolean" }, "Void"),
    ("RenderUtils", "EnableColliders", new[] { "GameObject", "Boolean" }, "Void"),
    ("Spell", "ActivateState", new[] { "SpellStateType" }, "Void"),
    ("GoldRewardData", ".ctor", new[] { "Int64" }, "Void"),
    // GAMEPLAY reward boxes are framed for the end-game layer-19 camera; HUB Box buttons are gated under them.
    ("CameraUtils", "FindFirstByLayerMask", new[] { "LayerMask" }, "Camera"),
    ("Box", "OnButtonPressed", new[] { "UIEvent" }, "Void"),
    // Off-screen/stranded reward boxes fall back to the saved-rewards popup: framing camera input, back-stack cleanup.
    ("PegUI", "AddInputCamera", new[] { "Camera" }, "Void"),
    ("PegUI", "RemoveInputCamera", new[] { "Camera" }, "Void"),
    ("Navigation", "RemoveHandler", new[] { "NavigateBackHandler" }, "Boolean"),
    ("RewardBoxesDisplay", "OnNavigateBack", Array.Empty<string>(), "Boolean"),
    ("RewardBoxesDisplay", "GetPackageData", new[] { "Int32" }, "List`1<RewardPackageData>"),
    // Reward V3 match dust: an Arcane Dust reward box beside gold and the pack.
    ("ArcaneDustRewardData", ".ctor", new[] { "Int32" }, "Void"),
    ("BoosterPackRewardData", ".ctor", new[] { "Int32", "Int32" }, "Void"),
    // Reward watchdog hit test: PegUI's own pick at a moved mouse position, mouse mode only (NativeRewardUI.Probe).
    ("PegUI", "FindHitElement", Array.Empty<string>(), "PegUIElement"),
    ("UniversalInputManager", "IsTouchMode", Array.Empty<string>(), "Boolean"),
    ("EndGameScreen", "Get", Array.Empty<string>(), "EndGameScreen")
})
    try { MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures); }
    catch
    {
        var owner = gameMetadata.TypeDefinitions.Select(gameMetadata.GetTypeDefinition)
            .Single(typeDef => gameMetadata.GetString(typeDef.Name) == contract.Item1);
        string actual = string.Join(" | ", owner.GetMethods().Select(gameMetadata.GetMethodDefinition)
            .Where(method => gameMetadata.GetString(method.Name) == contract.Item2)
            .Select(method => { var signature = method.DecodeSignature(signatures, (object)null);
                return signature.ReturnType + "(" + string.Join(",", signature.ParameterTypes) + ")"; }));
        failures.Add(contract.Item1 + "::" + contract.Item2 + " native reward contract changed: " + actual);
    }
foreach (var contract in new[] {
    ("GameObjectCallback", "Invoke", new[] { "AssetReference", "GameObject", "Object" }, "Void"),
    ("AssetReference", "op_Implicit", new[] { "String" }, "AssetReference"),
    // DETAILED RESULTS label fit (NativeRewardUI.FitLabel): UberText lives outside Assembly-CSharp; pinned by its call sites.
    ("UberText", "UpdateNow", new[] { "Boolean" }, "Void"),
    ("UberText", "set_ResizeToFit", new[] { "Boolean" }, "Void"),
    ("UberText", "set_WordWrap", new[] { "Boolean" }, "Void")
})
    try { MemberReferenceSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures); }
    catch { failures.Add(contract.Item1 + "::" + contract.Item2 + " external native reward contract changed"); }
try
{
    if (Field(pluginMetadata, "LocalProfileData", "PendingRewardPresentationId").DecodeSignature(signatures, (object)null) != "String")
        failures.Add("LocalProfileData::PendingRewardPresentationId compiled type changed");
    MethodSignature(pluginMetadata, "LocalProfile", "get_PendingRewardPresentation", Array.Empty<string>(), "LocalMatchRecord", signatures);
    MethodSignature(pluginMetadata, "LocalProfile", "AcknowledgeRewardPresentation", new[] { "String" }, "Boolean", signatures);
    MethodSignature(pluginMetadata, "NativeRewardUI", "Update", Array.Empty<string>(), "Void", signatures);
}
catch { failures.Add("compiled durable reward presentation API missing"); }
foreach (var contract in new[] {
    ("CollectionDeckBoxVisual", "OnDeleteButtonConfirmationResponse", new[] { "Response", "Object" }, "Void"),
    ("DeckOptionsMenu", "OnDeleteButtonConfirmationResponse", new[] { "Response", "Object" }, "Void"),
    ("CollectionDeckTray", "OnBackOutOfDeckContentsImpl", new[] { "Boolean" }, "Boolean"),
    ("DialogManager", "add_OnDialogHidden", new[] { "Action" }, "Void"),
    ("DialogManager", "remove_OnDialogHidden", new[] { "Action" }, "Void"),
    ("DialogBase", "AddHideListener", new[] { "HideCallback" }, "Void"),
    ("AlertPopup", "GetInfo", Array.Empty<string>(), "PopupInfo"),
    ("CacheCollection", "get_DeckReordering", Array.Empty<string>(), "Boolean"),
    ("CacheCollection", "set_DeckReordering", new[] { "Boolean" }, "Void"),
    ("DeckTrayDeckListContent", "get_CanDragToReorderDecks", Array.Empty<string>(), "Boolean")
})
    try { MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures); }
    catch { failures.Add(contract.Item1 + "::" + contract.Item2 + " delete/reorder contract changed"); }
// Flip All (NativePackFlip) and mass opening: hidden cards, Done spot, async widgets and full-batch feed.
foreach (var contract in new[] {
    ("PackOpening", "OnFakeBoosterOpened", Array.Empty<string>(), "IEnumerator"),
    ("PackOpeningDirector", "FinishMassPackOpen", Array.Empty<string>(), "Void"),
    ("PackOpening", "IsMassPackOpenable", new[] { "UnopenedPack" }, "Boolean"),
    ("PackOpeningDirector", "SetNumPacksOpened", new[] { "Int32" }, "Void"),
    ("PackOpeningCard", "ForceReveal", new[] { "Boolean" }, "Void"), ("PackOpeningCard", "IsReady", Array.Empty<string>(), "Boolean"),
    ("PackOpeningCard", "IsRevealEnabled", Array.Empty<string>(), "Boolean"), ("PackOpeningCard", "IsInputEnabled", Array.Empty<string>(), "Boolean"),
    ("PackOpeningCard", "IsRevealed", Array.Empty<string>(), "Boolean")
})
    try { MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures); }
    catch { failures.Add(contract.Item1 + "::" + contract.Item2 + " flip-all contract changed"); }
foreach (var contract in new[] {
    ("PackOpeningDirector", "m_hiddenCards", "HiddenCards"), ("HiddenCards", "m_cards", "List`1<PackOpeningCard>"),
    ("PackOpeningDirector", "m_massPackOpeningSummary", "MassPackOpeningSummary"),
    ("PackOpeningDirector", "m_massPackOpeningHighlights", "MassPackOpeningHighlights"),
    ("MassPackOpeningSummary", "m_cards", "List`1<BoosterCard>"), ("MassPackOpeningSummary", "m_numPacksOpened", "Int32"),
    ("PackOpening", "m_centerPack", "GameObject"), ("PackOpening", "m_lastOpenedBoosterId", "Int32"),
    ("PackOpening", "m_packOpeningId", "Int32"), ("PackOpening", "m_autoOpenPending", "Boolean"),
    ("PackOpening", "m_fakeBoosterCards", "List`1<BoosterCard>"),
    ("PackOpening", "m_BackButton", "UIBButton"), ("PackOpening", "m_Bones", "PackOpeningBones"), ("PackOpeningBones", "m_DoneButton", "Transform")
})
    try { if (Field(gameMetadata, contract.Item1, contract.Item2).DecodeSignature(signatures, (object)null) != contract.Item3) failures.Add(contract.Item1 + "::" + contract.Item2 + " flip-all field changed"); }
    catch { failures.Add(contract.Item1 + "::" + contract.Item2 + " flip-all field missing"); }
foreach (var contract in new[] {
    ("RewardBoxesDisplay", "m_DoneButton", "NormalButton"), ("NormalButton", "m_button", "GameObject"),
    ("RewardBoxesDisplay", "m_RewardSet", "RewardSet"), ("RewardSet", "m_RewardDust", "GameObject"),
    ("DialogManager", "m_currentDialog", "DialogBase"), ("DeckOptionsMenu", "m_deck", "CollectionDeck"),
    // Reward watchdog and HUB gate: box instances, revealed reward actors, queued presentation popups, input cameras.
    ("RewardBoxesDisplay", "m_rewardPackageInstances", "List`1<GameObject>"), ("RewardBoxesDisplay", "m_RewardObjects", "GameObject[]"),
    ("DialogManager", "m_dialogRequests", "Queue`1<DialogRequest>"), ("PegUI", "m_UICams", "List`1<Camera>"),
    ("DeckTrayDeckListContent", "m_traySections", "List`1<TraySection>"),
    ("DeckTrayDeckListContent", "m_deckOptionsMenu", "DeckOptionsMenu"),
    // GAMEPLAY reward boxes: the end-game continue hitbox and prompt are switched off under them; the watchdog
    // hit test moves the input manager's mouse; the compact card's cancel icon is hidden.
    ("EndGameScreen", "m_hitbox", "PegUIElement"), ("EndGameScreen", "m_continueText", "UberText"),
    ("UniversalInputManager", "m_mousePosition", "Vector3"),
    ("AlertPopup", "m_cancelButton", "UIBButton"),
    // Finding Opponent Cancel plate: retail disabled look (face-down flip) and the test-control report.
    ("TransitionPopup", "m_cancelButton", "UIBButton"), ("UIBButton", "m_RootObject", "GameObject"),
    ("UIBButton", "m_DisabledRotation", "Vector3"),
    ("AlertPopup", "m_buttonIconsSet1", "List`1<GameObject>"), ("AlertPopup", "m_buttonIconsSet2", "List`1<GameObject>")
})
{
    var field = Field(gameMetadata, contract.Item1, contract.Item2);
    if (field.DecodeSignature(signatures, (object)null) != contract.Item3)
        failures.Add(contract.Item1 + "::" + contract.Item2 + " owner field changed: " + field.DecodeSignature(signatures, (object)null));
}
// Per-deck cosmetics (card back, hero skin, random-hero toggle) saved with personal decks.
foreach (var contract in new[] {
    ("CollectionDeck", "CardBackID", "Nullable`1<Int32>"), ("CollectionDeck", "HeroOverridden", "Boolean"),
    ("CollectionDeck", "RandomHeroUseFavorite", "Boolean")
})
    if (Field(gameMetadata, contract.Item1, contract.Item2).DecodeSignature(signatures, (object)null) != contract.Item3)
        failures.Add(contract.Item1 + "::" + contract.Item2 + " deck cosmetics field changed");
foreach (var contract in new[] {
    ("CosmeticCoinManager", "RequestSetFavoriteCosmeticCoin", new[] { "Int32", "Boolean" }, "Void"),
    ("NetCache", "ProcessNewFavoriteCoin", new[] { "Int32", "Boolean" }, "Void"),
    ("CosmeticCoinManager", "GetCoinIdFromCoinCard", new[] { "String" }, "Int32"),
    ("CardBackManager", "get_TheRandomCardBackID", Array.Empty<string>(), "Int32"),
    ("CollectionDeck", "get_HeroCardID", Array.Empty<string>(), "String"),
    ("CollectionDeck", "set_HeroCardID", new[] { "String" }, "Void"),
    ("DeckHeader", "get_CardBack", Array.Empty<string>(), "Nullable`1<Int32>"),
    ("DeckHeader", "set_HeroOverridden", new[] { "Boolean" }, "Void"),
    ("DeckHeader", "set_RandomHeroUseFavorite", new[] { "Boolean" }, "Void")
})
    try { MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures); }
    catch { failures.Add(contract.Item1 + "::" + contract.Item2 + " deck cosmetics contract changed"); }
foreach (string owner in new[] { "CollectionDeckBoxVisual", "DeckOptionsMenu" })
    if ((Method(gameMetadata, owner, "OnDeleteButtonConfirmationResponse").Attributes & MethodAttributes.MemberAccessMask)
        != MethodAttributes.Private) failures.Add(owner + " delete response is no longer private");
foreach (var contract in new[] {
    ("Network", "CraftingTransaction", new[] { "CraftingPendingTransaction", "Int32", "Int32", "Int32", "Int32", "Int32" }, "Void"),
    ("CraftingManager", "TellServerAboutWhatUserDid", Array.Empty<string>(), "Void"),
    ("CollectionManager", "OnMassDisenchantResponse", Array.Empty<string>(), "Void"),
    ("MassDisenchant", "BlockUI", new[] { "Boolean" }, "Void"),
    ("CollectionDeck", "MarkNetworkContentsLoaded", Array.Empty<string>(), "Void"),
    ("CollectionDeck", "InsertSlotWithCard", new[] { "String", "TAG_PREMIUM", "Boolean", "Int32" }, "CollectionDeckSlot"),
    ("CollectionDeck", "GetAllSideboards", Array.Empty<string>(), "Dictionary`2<String,SideboardDeck>"),
    ("CollectionDeckTray", "GetDecksContent", Array.Empty<string>(), "DeckTrayDeckListContent"),
    ("DeckTrayDeckListContent", "RefreshMissingCardIndicators", Array.Empty<string>(), "Void"),
    ("CraftingManager", "ResetUnCommitedArcaneDustChanges", Array.Empty<string>(), "Void"),
    // Collection seen state and dust frame (NativeCollectionStatus): native marks seen, then acknowledges once.
    ("CollectionManager", "MarkAllInstancesAsSeen", new[] { "String", "TAG_PREMIUM" }, "Void"),
    ("Network", "AckCardSeenBefore", new[] { "Int32", "TAG_PREMIUM" }, "Void"),
    ("GameUtils", "TranslateDbIdToCardId", new[] { "Int32", "Boolean" }, "String"),
    ("BnetBar", "ToggleActive", new[] { "Boolean" }, "Void"),
    ("BnetBar", "RefreshCurrency", Array.Empty<string>(), "Void"),
    ("BnetBar", "TryGetRelevantCurrencyFrame", new[] { "CurrencyType", "CurrencyFrame&" }, "Boolean"),
    // HUB / Shop gold and Collection dust frames read CurrencyManager caches the plugin fills (NativeShop.LocalCurrency).
    ("CurrencyFrame", "GetVisibleCurrencies", Array.Empty<string>(), "IEnumerable`1<CurrencyType>"),
    ("CurrencyManager", "GetCurrencyCache", new[] { "CurrencyType" }, "CurrencyCache"),
    ("CurrencyManager", "UpdateWallet", new[] { "CurrencyType" }, "Void"),
    ("CurrencyCache", "UpdateBalance", new[] { "Int64" }, "Void"),
    ("CurrencyCache", "IsCached", Array.Empty<string>(), "Boolean"),
    ("WalletDataModel", "get_AllowRealMoneyPurchases", Array.Empty<string>(), "Boolean"),
    ("WalletDataModel", "set_AllowRealMoneyPurchases", new[] { "Boolean" }, "Void")
})
    MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures);
if (Field(gameMetadata, "BnetBar", "m_isLoggedIn").DecodeSignature(signatures, (object)null) != "Boolean")
    failures.Add("BnetBar login layout flag changed");
if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeUIProbe", "Update")).Contains(("NativeCollectionStatus", "Update")))
    failures.Add("Bottom currency bar lost its per-frame scene check");
if (Field(gameMetadata, "CurrencyManager", "m_walletDataModel").DecodeSignature(signatures, (object)null) != "WalletDataModel")
    failures.Add("CurrencyManager wallet model field changed");
// Retail picks the bar currencies per scene (HUB gold, Collection dust) unless an open store chooses them.
if (!Calls(game, gameMetadata, Method(gameMetadata, "CurrencyFrame", "GetVisibleCurrencies")).Contains(("IStore", "GetVisibleCurrencies")))
    failures.Add("CurrencyFrame::GetVisibleCurrencies no longer asks the open store");
if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeCollectionStatus", "Tick")).Contains(("NativeShop", "RefreshGold")))
    failures.Add("Currency bar no longer publishes gold when it appears");
foreach (string refresh in new[] { "RefreshDust", "RefreshGold" })
    if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeShop", "WalletUpdate")).Contains(("NativeShop", refresh)))
        failures.Add("NativeShop wallet update lost its per-frame " + refresh);
if (!Calls(game, gameMetadata, Method(gameMetadata, "CollectionManager", "MarkAllInstancesAsSeen")).Contains(("Network", "AckCardSeenBefore")))
    failures.Add("CollectionManager::MarkAllInstancesAsSeen no longer acknowledges through Network::AckCardSeenBefore");
foreach (string field in new[] { "m_pendingClientTransaction", "m_pendingServerTransaction" })
    if (Field(gameMetadata, "CraftingManager", field).DecodeSignature(signatures, (object)null) != "CraftingPendingTransaction")
        failures.Add("Crafting pending-state field changed: " + field);
foreach (string field in new[] { "NormalCreateCount", "NormalDisenchantCount", "GoldenCreateCount", "GoldenDisenchantCount",
    "GoldenUpgradeFromNormalCount", "GoldenUpgradeFromNothingCount", "SignatureDisenchantCount", "DiamondDisenchantCount" })
    if (Field(gameMetadata, "CraftingPendingTransaction", field).DecodeSignature(signatures, (object)null) != "Int32")
        failures.Add("Crafting batch counter changed: " + field);
// TryReadBatch maps the premium counters to TAG_PREMIUM by index, and BeforeClose writes Premium on the pending transaction.
foreach (var premium in new[] { ("NORMAL", 0), ("GOLDEN", 1), ("DIAMOND", 2), ("SIGNATURE", 3) })
    if (EnumValue(gameMetadata, "TAG_PREMIUM", premium.Item1) != premium.Item2)
        failures.Add("TAG_PREMIUM." + premium.Item1 + " is no longer " + premium.Item2);
if (Field(gameMetadata, "CraftingPendingTransaction", "Premium").DecodeSignature(signatures, (object)null) != "TAG_PREMIUM")
    failures.Add("CraftingPendingTransaction premium field changed");
MethodSignature(pluginMetadata, "NativeCrafting", "CloseFailed", new[] { "CraftingManager", "Exception" }, "Exception", signatures);
MethodSignature(pluginMetadata, "LocalProfile", "Settle", new[] { "String", "String", "Int32", "Boolean", "LocalRewardFacts", "Nullable`1<Double>", "Nullable`1<Boolean>", "LocalAchievementMatchEvidence", "LocalPetXpCounts" }, "LocalMatchRecord", signatures);
MethodSignature(pluginMetadata, "NativeCrafting", "SettlementDust", new[] { "LocalProfile" }, "Void", signatures);
MethodSignature(pluginMetadata, "NativeCrafting", "CraftingLogin", new[] { "IEnumerable`1<CodeInstruction>", "MethodBase" },
    "IEnumerable`1<CodeInstruction>", signatures);
foreach (var target in new[] { ("CreateButton", "OnRelease", 1), ("DisenchantButton", "OnRelease", 1),
    ("CraftingUI", "UpdateCraftingButtonsAndSoulboundText", 3) })
{
    var method = MethodSignature(gameMetadata, target.Item1, target.Item2, Array.Empty<string>(), "Void", signatures);
    int count = Calls(game, gameMetadata, method).Count(call => call == ("Network", "IsLoggedIn"));
    if (count != target.Item3) failures.Add(target.Item1 + "::" + target.Item2 + " crafting login call sites changed: " + count);
}
// MissingCardSet replaces exactly these native reads of a CardSet record that is null offline for timing-only sets.
MethodSignature(pluginMetadata, "NativeCrafting", "MissingCardSet", new[] { "IEnumerable`1<CodeInstruction>" },
    "IEnumerable`1<CodeInstruction>", signatures);
MethodSignature(pluginMetadata, "NativeCrafting", "FeaturedCardSet", new[] { "CardSetDbfRecord" }, "Boolean", signatures);
MethodSignature(pluginMetadata, "NativeCrafting", "CoreCardSet", new[] { "CardSetDbfRecord" }, "Boolean", signatures);
{
    var soulbound = Calls(game, gameMetadata, MethodSignature(gameMetadata, "CraftingUI", "UpdateCraftingButtonsAndSoulboundText",
        Array.Empty<string>(), "Void", signatures)).ToList();
    foreach (var call in new[] { ("GameDbfIndex", "GetCardSet"), ("CardSetDbfRecord", "get_IsFeaturedCardSet"), ("CardSetDbfRecord", "get_IsCoreCardSet") })
        if (soulbound.Count(found => found == call) != 1)
            failures.Add("CraftingUI soulbound card-set call sites changed: " + call.Item1 + "::" + call.Item2);
    MethodSignature(gameMetadata, "CraftingManager", "GetShownCardInfo", new[] { "EntityDef&", "TAG_PREMIUM&" }, "Boolean", signatures);
}
if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeUIProbe", "SaveLocalDraft"))
    .Contains(("NativeUIProbe", "IsUnrestoredCraftingRecipe")))
    failures.Add("Saved-deck hook lost its crafting reconciliation guard.");
// Check every policy path against the pinned client, without loading Unity.
MethodSignature(gameMetadata, "PackOpeningButton", "SetPackCount", new[] { "Int32" }, "Void", signatures);
MethodSignature(gameMetadata, "BoxMenuButton", "SetText", new[] { "String" }, "Void", signatures);
MethodSignature(gameMetadata, "BoxMenuButton", "GetText", Array.Empty<string>(), "String", signatures);
MethodSignature(gameMetadata, "TooltipZone", "ShowBoxTooltip", new[] { "String", "String", "Int32" }, "Void", signatures);
MethodSignature(gameMetadata, "BoxDrawer", "GetDrawerMesh", Array.Empty<string>(), "Renderer", signatures);
foreach (string caller in new[] { "ConfigureHomeButtons", "PublishBoosters" })
    if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeUIProbe", caller)).Contains(("NativeHomeUI", "RefreshDrawer")))
        failures.Add(caller + ": home badge refresh missing");
if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeHiddenEntries", "Apply")).Contains(("NativeHomeUI", "DisableMode")))
    failures.Add("Home policy no longer restores disabled native mode faces");
if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeHiddenEntries", "Apply")).Contains(("NativeHomeUI", "ApplyModePolicy")))
    failures.Add("Home policy no longer relabels/reorders the disabled mode faces");
if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeHiddenEntries", "Apply")).Contains(("NativeHomeUI", "HideJournalLabels")))
    failures.Add("Home refresh policy no longer hides detached Journal labels");
if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeUIProbe", "Update")).Contains(("NativeHomeUI", "UpdateJournal")))
    failures.Add("Home update lost its late-bound Journal enforcement and diagnostics");
if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeUIProbe", "LocalHubStart")).Contains(("NativeHomeUI", "BeginHub")))
    failures.Add("HUB load no longer resets Journal diagnostics");
// Standard-only presentation: LocalDeckFormat headers/medals/results and the NativeFormatLock picker lock.
foreach (var contract in new[] {
    ("DeckPickerTrayDisplay", "SwitchFormatTypeAndRankedPlayMode", new[] { "VisualsFormatType" }, "Void"),
    ("DeckPickerTrayDisplay", "ShowFormatTypePickerPopup", Array.Empty<string>(), "Void"),
    ("DeckPickerTrayDisplay", "get_IsModeSwitchShowing", Array.Empty<string>(), "Boolean"),
    ("GameUtils", "GetStandardSets", Array.Empty<string>(), "TAG_CARD_SET[]"),
    ("GameUtils", "GetAllWildPlayableSets", Array.Empty<string>(), "TAG_CARD_SET[]"),
    ("GameUtils", "GetCardSetFormat", new[] { "TAG_CARD_SET" }, "FormatType"),
    ("GameUtils", "IsFormatPlayable", new[] { "VisualsFormatType" }, "Boolean"),
    ("CacheGames", "set_RankedStandard", new[] { "Boolean" }, "Void"),
    ("CacheGames", "set_RankedWild", new[] { "Boolean" }, "Void"),
    ("CacheGames", "set_Casual", new[] { "Boolean" }, "Void"),
    ("MedalInfoTranslator", "GetCurrentMedal", new[] { "FormatType" }, "TranslatedMedalInfo"),
    ("MedalInfoTranslator", "GetChangeType", new[] { "FormatType" }, "RankChangeType"),
    ("RankChangeTwoScoop_NEW", "Initialize", new[] { "MedalInfoTranslator", "FormatType", "Action" }, "Void"),
    ("PegUIElement", "SetEnabled", new[] { "UIEventType", "Boolean" }, "Void"),
    ("PegUIElement", "SetEnabled", new[] { "Boolean", "Boolean" }, "Void"),
    ("PegUIElement", "IsEnabled", Array.Empty<string>(), "Boolean"),
    ("UIBButton", "Flip", new[] { "Boolean", "Boolean" }, "Void"),
    ("Clickable", "get_Active", Array.Empty<string>(), "Boolean"),
    ("Clickable", "set_Active", new[] { "Boolean" }, "Void"),
    ("PegUIElement", "AddEventListener", new[] { "UIEventType", "Handler" }, "Boolean"),
    ("NotificationManager", "CreatePopupText",
        new[] { "UserAttentionBlocker", "Vector3", "Vector3", "String", "Boolean", "PopupTextType" }, "Notification"),
    ("NotificationManager", "DestroyNotificationNowWithNoAnim", new[] { "Notification" }, "Void"),
    ("Notification", "ShowPopUpArrow", new[] { "PopUpArrowDirection" }, "Void"),
    ("OverlayUI", "GetRelativePosition", new[] { "Vector3", "Camera", "Transform", "Single" }, "Vector3"),
    ("TutorialEntity", "GetTextScale", Array.Empty<string>(), "Vector3"),
    ("WidgetBehaviorStateCollection", "get_States", Array.Empty<string>(), "IList`1<WidgetBehaviorState>"),
    ("WidgetBehaviorState", "get_Actions", Array.Empty<string>(), "IEnumerable`1<StateAction>"),
    ("StateAction", "get_Type", Array.Empty<string>(), "ActionType"),
    ("StateAction", "GetString", new[] { "Int32" }, "String")
})
    try { MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures); }
    catch { failures.Add(contract.Item1 + "::" + contract.Item2 + " Standard format contract changed"); }
// Several types are named Options: resolve the global one (as the hook table does) before checking signatures.
foreach (var contract in new[] {
    ("SetFormatType", new[] { "FormatType" }, "Void"), ("GetFormatType", Array.Empty<string>(), "FormatType"),
    ("SetInRankedPlayMode", new[] { "Boolean" }, "Void"), ("GetInRankedPlayMode", Array.Empty<string>(), "Boolean")
})
    try
    {
        var signature = Method(gameMetadata, "Options", contract.Item1).DecodeSignature(signatures, (object)null);
        if (!signature.ParameterTypes.SequenceEqual(contract.Item2) || signature.ReturnType != contract.Item3)
            failures.Add("Options::" + contract.Item1 + " Standard format contract changed");
    }
    catch { failures.Add("Options::" + contract.Item1 + " Standard format contract missing"); }
foreach (var contract in new[] {
    ("DeckPickerTrayDisplay", "m_formatTypePickerWidget", "Widget"), ("DeckPickerTrayDisplay", "m_switchFormatButton", "SwitchFormatButton"),
    ("Clickable", "m_stateCollection", "WidgetBehaviorStateCollection"), ("GameMgr", "m_nextFormatType", "FormatType"),
    ("Clickable", "m_boundsCollider", "Collider"), ("Clickable", "m_geometryColliders", "List`1<Collider>")
})
    try
    {
        var field = Field(gameMetadata, contract.Item1, contract.Item2);
        if (field.DecodeSignature(signatures, (object)null) != contract.Item3)
            failures.Add(contract.Item1 + "::" + contract.Item2 + " Standard format field changed: " + field.DecodeSignature(signatures, (object)null));
    }
    catch { failures.Add(contract.Item1 + "::" + contract.Item2 + " Standard format field missing"); }
if (TryEnumValue(gameMetadata, "FormatType", "FT_STANDARD") != 2 || TryEnumValue(gameMetadata, "FormatType", "FT_WILD") != 1
    || TryEnumValue(gameMetadata, "Option", "IN_RANKED_PLAY_MODE") == null || TryEnumValue(gameMetadata, "VisualsFormatType", "VFT_STANDARD") == null
    || TryEnumValue(gameMetadata, "VisualsFormatType", "VFT_CASUAL") == null)
    failures.Add("Standard format enum contract changed (FT_STANDARD/FT_WILD/IN_RANKED_PLAY_MODE/VFT_STANDARD)");
// Live 2026-10-25: the material-only grey was invisible; locked picker buttons use the native inactive Clickable state.
if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeFormatLock", "Lock")).Contains(("NativeFormatLock", "Grey")))
    failures.Add("Format picker lock no longer greys the locked button art");
if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeUIProbe", "Update")).Contains(("NativeFormatLock", "Update")))
    failures.Add("Probe update no longer runs the format picker lock");
// Live 2026-10-25 (B1): new-deck cosmetic presses; every tray write path binds the tray to the edited deck first.
foreach (var contract in new[] {
    ("DeckTrayCardBackContent", "AnimateInCardBack", new[] { "Int32", "GameObject" }, "Boolean"),
    ("DeckTrayCardBackContent", "UpdateCardBack", new[] { "Int32", "Boolean", "GameObject" }, "Void"),
    ("DeckTrayCardBackContent", "ToggleFavoritesOnly", new[] { "Boolean" }, "Void"),
    ("DeckTrayCardBackContent", "RemoveCardBack", Array.Empty<string>(), "Void"),
    ("DeckTrayCardBackContent", "GrabCardBack", Array.Empty<string>(), "Void"),
    ("DeckTrayCardBackContent", "PreAnimateContentEntrance", Array.Empty<string>(), "Boolean"),
    ("DeckTrayCosmeticCoinContent", "AnimateInCosmeticCoin", new[] { "Actor" }, "Void"),
    ("DeckTrayCosmeticCoinContent", "UpdateCosmeticCoin", new[] { "Int32", "Boolean", "Actor" }, "Void"),
    ("DeckTrayCosmeticCoinContent", "ToggleFavoritesOnly", new[] { "Boolean" }, "Void"),
    ("DeckTrayCosmeticCoinContent", "RemoveCosmeticCoin", Array.Empty<string>(), "Void"),
    ("DeckTrayCosmeticCoinContent", "GrabCosmeticCoin", Array.Empty<string>(), "Void"),
    ("CollectionDeckTray", "AnimateInCosmeticCoin", new[] { "Actor" }, "Void"),
    ("DeckTrayHeroSkinContent", "AnimateInHeroSkin", new[] { "Actor" }, "Void"),
    ("DeckTrayHeroSkinContent", "UpdateHeroSkin", new[] { "String", "TAG_PREMIUM", "Boolean", "Actor" }, "Void"),
    ("DeckTrayHeroSkinContent", "ToggleFavoritesOnly", new[] { "Boolean" }, "Void"),
    ("DeckTrayHeroSkinContent", "RemoveHeroSkin", Array.Empty<string>(), "Void"),
    ("DeckTrayHeroSkinContent", "GrabHeroSkin", Array.Empty<string>(), "Void"),
    ("DeckTrayHeroSkinContent", "PreAnimateContentEntrance", Array.Empty<string>(), "Boolean"),
    ("CollectionManager", "IsInEditMode", Array.Empty<string>(), "Boolean"),
    ("CollectionManager", "GetEditedDeck", Array.Empty<string>(), "CollectionDeck")
})
    try { MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures); }
    catch { failures.Add(contract.Item1 + "::" + contract.Item2 + " cosmetic tray binding contract changed"); }
foreach (string tray in new[] { "DeckTrayCardBackContent", "DeckTrayHeroSkinContent", "DeckTrayCosmeticCoinContent" })
    try
    {
        if (Field(gameMetadata, tray, "m_currentDeck").DecodeSignature(signatures, (object)null) != "CollectionDeck")
            failures.Add(tray + "::m_currentDeck type changed");
    }
    catch { failures.Add(tray + "::m_currentDeck missing"); }
if (!Calls(game, gameMetadata, Method(gameMetadata, "CollectionCardVisual", "OnRelease"))
    .Contains(("CollectionDeckTray", "AnimateInCosmeticCoin")))
    failures.Add("Collection coin left-click no longer reaches native tray assignment");
foreach (var contract in new[] {
    ("CosmeticCoinDbfRecord", "get_CardRecord", Array.Empty<string>(), "CardDbfRecord"),
    ("CardDataModel", "set_CardId", new[] { "String" }, "Void"),
    ("CoinPreview", "EnterPreviewWhenReady", new[] { "CardDataModel", "Int32", "Transform" }, "Void")
})
    try { MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures); }
    catch { failures.Add(contract.Item1 + "::" + contract.Item2 + " coin preview binding changed"); }
// Live 2026-10-25 (B2): test control maps the human's cosmetic hero skin through these NativeMatch fields by name.
foreach (string field in new[] { "cosmeticHero", "cosmeticBaseHero", "npcProfile", "opponentDeck" })
    try { Field(pluginMetadata, "NativeMatch", field); }
    catch { failures.Add("Test-control presentation field missing: NativeMatch::" + field); }
if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeUIProbe", "Initialize")).Contains(("NativeUIProbe", "InitializeCosmeticTrayBinding")))
    failures.Add("Probe initialization no longer binds the cosmetic trays");
if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeUIProbe", "BindCosmeticTray")).Contains(("NativeUIProbe", "BindTrayDeck")))
    failures.Add("Cosmetic tray binding lost its shared rule");
if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeUIProbe", "Initialize")).Contains(("NativeFormatLock", "Initialize")))
    failures.Add("Probe initialization no longer installs the Standard format lock");
// HubTheme (main-menu themes): dressing state, re-dress calls and the per-call timings gate.
if (Field(gameMetadata, "Box", "m_eventBoxDressingActive").DecodeSignature(signatures, (object)null) != "Boolean")
    failures.Add("Box dressing state field changed");
MethodSignature(gameMetadata, "Box", "UpdateEventBoxDressingWithConfig", Array.Empty<string>(), "Void", signatures);
MethodSignature(gameMetadata, "Box", "HideEventBoxDressing", Array.Empty<string>(), "Void", signatures);
MethodSignature(gameMetadata, "Box", "ShowEventBoxDressing", Array.Empty<string>(), "Void", signatures);
MethodSignature(gameMetadata, "BoxDressingManager", "GetCurrentBoxDressing", Array.Empty<string>(), "BoxDressingDbfRecord", signatures);
MethodSignature(gameMetadata, "EventTimingManager", "get_HasReceivedEventTimingsFromServer", Array.Empty<string>(), "Boolean", signatures);
MethodSignature(gameMetadata, "EventTimingManager", "set_HasReceivedEventTimingsFromServer", new[] { "Boolean" }, "Void", signatures);
// Theme/board shop tiles: a DRAFT bundle item becomes an ARENA_TICKET reward (pinned RewardFactory); tiles are renamed in place.
MethodSignature(gameMetadata, "ProductDataModel", "set_Name", new[] { "String" }, "Void", signatures);
if (TryEnumValue(gameMetadata, "RewardItemType", "ARENA_TICKET") != 6) failures.Add("RewardItemType.ARENA_TICKET changed");
MethodSignature(gameMetadata, "Box", "GetOldJournalButton", Array.Empty<string>(), "JournalButton", signatures);
MethodSignature(gameMetadata, "WidgetInstance", "get_Widget", Array.Empty<string>(), "WidgetTemplate", signatures);
if (Field(gameMetadata, "Box", "m_journalButtonWidget").DecodeSignature(signatures, (object)null) != "Widget")
    failures.Add("Journal widget host type changed");
foreach (string field in new[] { "m_storeText", "m_storeClosedText" })
    if (Field(gameMetadata, "StoreButton", field).DecodeSignature(signatures, (object)null) != "UberText")
        failures.Add("Native Store text mesh type changed: " + field);
// NativeTierRewardCaption gates centring, and NativeTierRewardStep the player wait, on these private display fields.
foreach (var (field, type) in new[] { ("m_isAnimating", "Boolean"), ("m_isHidePending", "Boolean"), ("m_numAnimationsRemaining", "Int32") })
    if (Field(gameMetadata, "RankedRewardDisplay", field).DecodeSignature(signatures, (object)null) != type)
        failures.Add("RankedRewardDisplay state field changed: " + field);
string hiddenPolicy = File.ReadAllText(Path.Combine(root, "plugin/NativeHiddenEntries.cs"));
var hiddenRows = Regex.Matches(hiddenPolicy, "new Entry\\(\"[^\"]+\", \"([^\"]+)\", \"[^\"]+\", \"[^\"]+\", \"([^\"]+)\"");
foreach (Match row in hiddenRows)
{
    foreach (string target in row.Groups[1].Value.Split('|'))
    {
        try
        {
            string[] parts = target.Split('.');
            int member = parts.Length - 1;
            TypeDefinition owner = default;
            for (; member > 0; member--)
            {
                string name = string.Join(".", parts.Take(member));
                var candidates = gameMetadata.TypeDefinitions.Select(gameMetadata.GetTypeDefinition)
                    .Where(type => (gameMetadata.GetString(type.Namespace) + "." + gameMetadata.GetString(type.Name)).TrimStart('.') == name).ToArray();
                if (candidates.Length == 1) { owner = candidates[0]; break; }
            }
            if (member == 0) throw new Exception("owner missing");
            foreach (string name in parts.Skip(member).Where(name => name != "$self"))
            {
                var field = owner.GetFields().Select(gameMetadata.GetFieldDefinition)
                    .Single(field => gameMetadata.GetString(field.Name) == name);
                string fieldType = field.DecodeSignature(signatures, (object)null);
                if (name != parts.Last()) owner = gameMetadata.TypeDefinitions.Select(gameMetadata.GetTypeDefinition)
                    .Single(type => gameMetadata.GetString(type.Name) == fieldType);
            }
        }
        catch { failures.Add(target + ": hidden-entry target changed"); }
    }
    foreach (string handler in row.Groups[2].Value.Split('|')) CheckHiddenMethod(handler);
}
string refreshes = hiddenPolicy.Split("string[] RefreshHooks = {")[1].Split("};")[0];
foreach (Match hook in Regex.Matches(refreshes, "\"([^\"]+)\"")) CheckHiddenMethod(hook.Groups[1].Value);
foreach (string hook in new[] { "Box.FireButtonPressEvent", "Hub.OnBoxButtonPressed", "GameMenu.GetButtons" }) CheckHiddenMethod(hook);
foreach (string hook in new[] { "Apply", "Block", "GuardBoxRoute", "FilterMenuButtons" })
    try { Method(pluginMetadata, "NativeHiddenEntries", hook); }
    catch { failures.Add("NativeHiddenEntries::" + hook + " compiled hook missing"); }
foreach (var contract in new[] {
    ("Box", "FireButtonPressEvent", new[] { "ButtonType", "Boolean" }, "Void"),
    ("Hub", "OnBoxButtonPressed", new[] { "ButtonType", "Boolean", "Object" }, "Void"),
    ("GameMenu", "GetButtons", Array.Empty<string>(), "List`1<UIBButton>"),
    ("CurrencyFrame", "AllowRecharge", new[] { "CurrencyType" }, "Boolean")
})
    try { MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures); }
    catch { failures.Add(contract.Item1 + "::" + contract.Item2 + " hidden-entry hook signature changed"); }
void CheckHiddenMethod(string path)
{
    int split = path.LastIndexOf('.');
    string ownerName = path[..split];
    string methodName = path[(split + 1)..];
    try
    {
        var owner = gameMetadata.TypeDefinitions.Select(gameMetadata.GetTypeDefinition).Single(type =>
            (gameMetadata.GetString(type.Namespace) + "." + gameMetadata.GetString(type.Name)).TrimStart('.') == ownerName);
        var methods = owner.GetMethods().Select(gameMetadata.GetMethodDefinition)
            .Where(method => gameMetadata.GetString(method.Name) == methodName).ToArray();
        if (methods.Length != 1) throw new Exception("missing or ambiguous method");
    }
    catch { failures.Add(path + ": hidden-entry native hook changed"); }
}
// Raised deck limit: NativeDeckLimits transpiles exactly these retail tray literals (27 decks, 29 sections, 25/26 slots).
foreach (var site in new[] {
    ("DeckTrayDeckListContent", "CreateTraySections", new[] { 27, 29 }),
    ("DeckTrayDeckListContent", "RefreshTraySectionPositions", new[] { 29 }),
    ("DeckTrayDeckListContent", "GetLastUnusedTraySection", new[] { 27 }),
    ("DeckTrayDeckListContent", "GetLastUsedTraySection", new[] { 27 }),
    ("DeckTrayDeckListContent", "CanShowNewDeckButton", new[] { 27 }),
    ("<DeleteDeckAnimation>", "MoveNext", new[] { 25, 26, 26 }),
    ("CollectionDeckTray", "OnDeckCountUpdated", new[] { 27 })
})
{
    try
    {
        MethodDefinition method;
        if (site.Item1.StartsWith("<"))
        {
            var owner = gameMetadata.TypeDefinitions.Select(gameMetadata.GetTypeDefinition).Single(t => gameMetadata.GetString(t.Name) == "DeckTrayDeckListContent"
                && t.Namespace.IsNil && t.GetDeclaringType().IsNil);
            var nested = owner.GetNestedTypes().Select(gameMetadata.GetTypeDefinition).Single(t => gameMetadata.GetString(t.Name).StartsWith(site.Item1));
            method = nested.GetMethods().Select(gameMetadata.GetMethodDefinition).Single(m => gameMetadata.GetString(m.Name) == "MoveNext");
        }
        else method = Method(gameMetadata, site.Item1, site.Item2);
        var literals = Int32Literals(game, method).Where(value => value == 25 || value == 26 || value == 27 || value == 29).OrderBy(v => v);
        if (!literals.SequenceEqual(site.Item3.OrderBy(v => v)))
            failures.Add(site.Item1 + "::" + site.Item2 + " deck tray literals changed: " + string.Join(",", literals));
    }
    catch (Exception error) { failures.Add(site.Item1 + "::" + site.Item2 + " deck tray site missing: " + error.Message); }
}
// SoloStone Help (NativeHelpUI): a HUB button cloned from the Game Menu's own row template (no Game Menu row of its own),
// one AlertPopup. The Change Profile row (NativeProfileSelector) is the only plugin row the Game Menu still gets.
const string HelpButton = "HUB Help button", ProfileRow = "Game Menu Change Profile row";
foreach (var contract in new[] {
    ("ButtonListMenu", "CreateMenuButton", new[] { "String", "String", "Handler" }, "UIBButton", ProfileRow),
    ("GameMenu", "IsInGameMenu", Array.Empty<string>(), "Boolean", ProfileRow),
    ("BnetBar", "HideGameMenu", Array.Empty<string>(), "Void", ProfileRow),
    ("Box", "IsInStateWithButtons", Array.Empty<string>(), "Boolean", HelpButton),
    ("GameUtils", "InstantiateGameObject", new[] { "String", "GameObject", "Boolean" }, "Object", HelpButton),
    ("CameraUtils", "FindFirstByLayerMask", new[] { "LayerMask" }, "Camera", HelpButton),
    ("DialogManager", "ShowUniquePopup", new[] { "PopupInfo" }, "Boolean", HelpButton),
    ("UIBButton", "SetText", new[] { "String" }, "Void", HelpButton),
    ("PegUIElement", "ClearEventListeners", Array.Empty<string>(), "Void", HelpButton)
})
    try { MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures); }
    catch { failures.Add(contract.Item1 + "::" + contract.Item2 + " " + contract.Item5 + " method contract changed"); }
foreach (var (owner, field, type, user) in new[] { ("ButtonListMenu", "m_menu", "ButtonListMenuDef", HelpButton),
    ("ButtonListMenuDef", "m_headerText", "UberText", HelpButton), ("ButtonListMenuDef", "m_templateButton", "UIBButton", HelpButton),
    ("UIBButton", "m_ButtonText", "UberText", HelpButton), ("BnetBar", "m_currentTime", "UberText", HelpButton),
    ("BnetBar", "m_menuButton", "BnetBarMenuButton", HelpButton), ("GameMenu", "m_optionsButton", "UIBButton", ProfileRow),
    ("ButtonListMenu", "m_menuDefPrefab", "String", HelpButton) })
    try { if (Field(gameMetadata, owner, field).DecodeSignature(signatures, (object)null) != type) failures.Add(owner + "." + field + " " + user + " field type changed"); }
    catch { failures.Add(owner + "." + field + " " + user + " field missing"); }
try
{
    MemberReferenceSignature(gameMetadata, "UberText", "set_TextColor", new[] { "Color" }, "Void", signatures);
    MemberReferenceSignature(gameMetadata, "UberText", "get_Width", Array.Empty<string>(), "Single", signatures);
    MemberReferenceSignature(gameMetadata, "UberText", "set_Width", new[] { "Single" }, "Void", signatures);
}
catch { failures.Add("UberText achievement text color/width contract missing"); }
try
{
    var alertPopup = gameMetadata.TypeDefinitions.Select(gameMetadata.GetTypeDefinition).Single(t => gameMetadata.GetString(t.Name) == "AlertPopup" && t.Namespace.IsNil);
    var popupInfo = alertPopup.GetNestedTypes().Select(gameMetadata.GetTypeDefinition).Single(t => gameMetadata.GetString(t.Name) == "PopupInfo");
    var iconSet = popupInfo.GetNestedTypes().Select(gameMetadata.GetTypeDefinition).Single(t => gameMetadata.GetString(t.Name) == "IconSet");
    if (popupInfo.GetFields().Select(gameMetadata.GetFieldDefinition).Single(f => gameMetadata.GetString(f.Name) == "m_iconSet")
            .DecodeSignature(signatures, (object)null) != "IconSet"
        || !iconSet.GetFields().Select(gameMetadata.GetFieldDefinition).Any(f => gameMetadata.GetString(f.Name) == "None"))
        failures.Add("AlertPopup.PopupInfo.m_iconSet/IconSet.None help popup contract changed");
}
catch { failures.Add("AlertPopup.PopupInfo.IconSet help popup contract missing"); }
// The HUB button loads the menu definition by the client's own literal (ButtonListMenu.m_menuDefPrefab initializer).
string helpPrefab = Regex.Match(File.ReadAllText(Path.Combine(root, "plugin/NativeHelpUI.cs")), "MenuDefPrefab = \"([^\"]+)\"").Groups[1].Value;
if (helpPrefab.Length == 0 || !UserStrings(gameMetadata).Contains(helpPrefab))
    failures.Add("Help button menu-definition prefab literal not found in pinned client: " + helpPrefab);
if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeUIProbe", "Update")).Contains(("NativeHelpUI", "Update")))
    failures.Add("Probe update no longer drives the HUB help button");
if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeUIProbe", "Initialize")).Contains(("NativeHelpUI", "Initialize")))
    failures.Add("Probe initialization no longer installs the help popup layout hooks");
// Steam Deck foundation: exact layout/controls hooks and the native geometry contract.
foreach (var contract in new[] {
    ("MulliganManager", "GetStartingCards", Array.Empty<string>(), "List`1<Card>"),
    ("MulliganManager", "GetMulliganButton", Array.Empty<string>(), "GameObject"),
    ("MulliganManager", "GetMulliganBanner", Array.Empty<string>(), "GameObject"),
    ("MulliganManager", "IsMulliganActive", Array.Empty<string>(), "Boolean"),
    ("MulliganManager", "IsMulliganIntroActive", Array.Empty<string>(), "Boolean"),
    ("MulliganManager", "IsCustomIntroActive", Array.Empty<string>(), "Boolean"),
    ("ButtonListMenu", "Awake", Array.Empty<string>(), "Void"),
    ("ButtonListMenu", "LayoutMenu", Array.Empty<string>(), "Void"),
    ("OptionsMenu", "Hide", new[] { "Boolean" }, "Void"),
    ("OptionsMenu", "Show", Array.Empty<string>(), "Void"),
    ("OptionsMenu", "IsShown", Array.Empty<string>(), "Boolean"),
    ("SceneMgr", "IsModeRequested", new[] { "Mode" }, "Boolean"),
    ("DialogManager", "ShowingDialog", Array.Empty<string>(), "Boolean")
})
    try { MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures); }
    catch { failures.Add(contract.Item1 + "::" + contract.Item2 + " Deck contract changed"); }
foreach (var (owner, field, type) in new[] {
    ("UniversalInputManager", "m_gameDialogActiveCount", "Int32"),
    ("UniversalInputManager", "m_systemDialogActive", "Boolean"),
    ("UniversalInputManager", "m_notificationDialogActive", "Boolean"),
    ("DialogManager", "m_currentDialog", "DialogBase"),
    ("DialogManager", "m_loadingDialog", "Boolean"),
    ("MulliganManager", "m_waitingForUserInput", "Boolean"),
    ("MulliganManager", "mulliganButtonWidget", "MulliganButton"),
    ("OptionsMenu", "m_soundOptionsButton", "UIBButton"),
    ("OptionsMenu", "m_miscellaneousButton", "UIBButton"),
    ("OptionsMenu", "m_graphicsRes", "DropdownControl"),
    ("OptionsMenu", "m_graphicsQuality", "DropdownControl"),
    ("OptionsMenu", "m_graphicsFps", "DropdownControl"),
    ("OptionsMenu", "m_signatureTextDisplayDropdown", "DropdownControl"),
    ("OptionsMenu", "m_middleBottomRightPane", "MultiSliceElement"),
    ("ButtonListMenuDef", "m_headerText", "UberText")
})
    try { if (Field(gameMetadata, owner, field).DecodeSignature(signatures, (object)null) != type) failures.Add(owner + "." + field + " Deck field type changed"); }
    catch { failures.Add(owner + "." + field + " Deck field missing"); }
foreach (var (method, target) in new[] { ("InitializeProbe", "Initialize"), ("Update", "Tick"), ("OnDestroy", "Shutdown") })
    if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "OfflineProbe", method)).Contains(("DeckExperience", target)))
        failures.Add("OfflineProbe::" + method + " does not drive DeckExperience::" + target);
// Production menus bind exact native owners and never submit diagnostic or transaction shortcuts.
foreach(var (owner,field,type) in new[]{
    ("PegUI","m_mouseDownElement","PegUIElement"), ("PegUI","m_currentElement","PegUIElement"), ("PegUI","m_prevElement","PegUIElement"),
    ("PegUI","m_mouseDownTimer","Single"), ("PegUI","m_lastClickTimer","Single"),
    ("UniversalInputManager","m_inputOwner","GameObject"), ("UniversalInputManager","m_inputCanceledCallback","TextInputCanceledCallback"), ("HeroPickerDisplay","m_deckPickerTray","DeckPickerTrayDisplay"),
    ("AbsDeckPickerTrayDisplay","m_heroButtons","List`1<HeroPickerButton>"), ("AbsDeckPickerTrayDisplay","m_playButton","PlayButton"),
    ("DeckPickerTrayDisplay","m_currentPageIndex","Int32"), ("DeckPickerTrayDisplay","m_deckPickerMode","DeckPickerMode"),
    ("DeckPickerTrayDisplay","m_doingDeckShareTransition","Boolean"),
    ("DeckTemplatePicker","m_pickerButtons","List`1<DeckTemplatePickerButton>"), ("DeckTemplatePicker","m_currentSelectedClass","TAG_CLASS"),
    ("DeckTemplatePicker","m_currentSelectedDeck","TemplateDeck"), ("DeckTemplatePicker","m_customDeckButton","DeckTemplatePickerButton"),
    ("DeckTemplatePicker","m_chooseButton","PlayButton"), ("DeckTemplatePicker","m_phoneBackButton","UIBButton"),
    ("DropdownControl","m_items","List`1<DropdownMenuItem>"), ("DropdownMenuItem","m_value","Object"),
    ("CollectibleDisplay","m_setFilterTray","SetFilterTray"), ("CollectionManagerDisplay","m_manaTabManager","ManaFilterTabManager"),
    ("CollectibleDisplay","m_inputBlockers","Int32"), ("CollectionPageManager","m_classTabs","List`1<CollectionClassTab>"),
    ("ManaFilterTabManager","m_tabs","List`1<ManaFilterTab>"), ("SetFilterTray","m_items","List`1<SetFilterItem>"), ("SetFilterTray","m_isAnimating","Boolean"),
    ("DeckTrayDeckListContent","m_traySections","List`1<TraySection>"), ("DeckTrayDeckListContent","m_deckOptionsMenu","DeckOptionsMenu"),
    ("DeckTrayCardListContent","m_cardTiles","List`1<DeckTrayDeckTileVisual>"), ("DeckTrayCardListContent","m_sideboardDoneButton","UIBButton"),
    ("DeckOptionsMenu","m_renameButton","PegUIElement"), ("DeckOptionsMenu","m_deleteButton","PegUIElement"), ("DeckOptionsMenu","m_deckInfo","CollectionDeckInfo"), ("DeckOptionsMenu","m_deck","CollectionDeck"),
    ("DeckTrayDeckListContent","m_deckInfoTooltip","CollectionDeckInfo"),
    ("CollectionDeckTileActor","m_sideboardSectionWidget","DeckCardBarSideboardWidget"), ("DeckCardBarSideboardWidget","m_sideboardButton","UIBButton"),
    ("CraftingUI","m_isAnimating","Boolean"), ("CraftingUI","m_buttonCreate","CreateButton"), ("CraftingUI","m_buttonDisenchant","DisenchantButton"),
    ("CraftingTray","m_massDisenchantButton","PegUIElement"), ("CraftingTray","m_premiumOwnedCheckbox","CheckBox"), ("CraftingTray","m_premiumMissingCheckbox","CheckBox"),
    ("MassDisenchant","m_disenchantButton","NormalButton"), ("MassDisenchant","m_infoButton","UIBButton"),
    ("RelatedCardsTray","m_relatedCardList","DataModelList`1<CardTileDataModel>"), ("RelatedCardsTray","m_bundledCardList","DataModelList`1<CardTileDataModel>"),
    ("Clickable","m_stateCollection","WidgetBehaviorStateCollection"), ("Clickable","m_pegUiElement","PegUIElement"),
    ("PurchaseAuthWindow","m_purchaseAuth","StorePurchaseAuth"), ("ProductPageController","m_quantityPrompt","StoreQuantityPrompt"),
    ("PackOpening","m_draggedPack","UnopenedPack"), ("PackOpening","m_Socket","PackOpeningSocket"), ("PackOpening","m_director","PackOpeningDirector"),
    ("PackOpeningDirector","m_hiddenCards","HiddenCards"), ("PackOpeningDirector","m_doneButton","NormalButton"),
    ("HiddenCards","m_cards","List`1<PackOpeningCard>"), ("PackOpeningCard","m_revealButton","PegUIElement"), ("PackOpeningCard","m_cardNumber","Int32"), ("PackOpeningCard","m_boosterCard","BoosterCard"),
    ("RewardBoxesDisplay","m_rewardPackageInstances","List`1<GameObject>"),
    ("RewardPackage","m_RewardIndex","Int32"), ("RewardBoxesDisplay","m_doneButtonFinishedShown","Boolean")
})
    try {
        var nativeOwner=gameMetadata.TypeDefinitions.Select(gameMetadata.GetTypeDefinition).Single(t=>gameMetadata.GetString(t.Name)==owner
            && t.GetFields().Select(gameMetadata.GetFieldDefinition).Any(f=>gameMetadata.GetString(f.Name)==field));
        string actual=nativeOwner.GetFields().Select(gameMetadata.GetFieldDefinition).Single(f=>gameMetadata.GetString(f.Name)==field).DecodeSignature(signatures,(object)null);
        if(actual!=type) failures.Add(owner+"::"+field+" menu binding expected "+type+", got "+actual);
    } catch { failures.Add(owner+"::"+field+" menu field missing"); }
foreach(var contract in new[]{
    ("PegUI","MouseInputUpdate",Array.Empty<string>(),"Void"),
    ("PegUI","GetMouseDownElement",Array.Empty<string>(),"PegUIElement"),
    ("PegUIElement","TriggerReleaseAll",new[]{"Boolean"},"Void"),
    ("PackOpening","UpdateDraggedPack",Array.Empty<string>(),"Void"),
    ("UniversalInputManager","UserCancelTextInput",Array.Empty<string>(),"Void"),
    ("CollectiblePageManager","GetCurrentCollectiblePage",Array.Empty<string>(),"CollectiblePageDisplay"),
    ("DropdownControl","isMenuShown",Array.Empty<string>(),"Boolean"),
    ("CraftingButton","GetCraftingState",Array.Empty<string>(),"CraftingState"),
    ("CraftingManager","GetShownActor",Array.Empty<string>(),"Actor"),
    ("WidgetBehavior","GetDataModel",new[]{"Int32","IDataModel&"},"Boolean"),
    ("StateAction","GetString",new[]{"Int32"},"String")
})
    try {
        gameMetadata.TypeDefinitions.Select(gameMetadata.GetTypeDefinition).Where(t=>gameMetadata.GetString(t.Name)==contract.Item1)
            .SelectMany(t=>t.GetMethods().Select(gameMetadata.GetMethodDefinition)).Single(m=>gameMetadata.GetString(m.Name)==contract.Item2
                && m.DecodeSignature(signatures,(object)null).ParameterTypes.SequenceEqual(contract.Item3)
                && m.DecodeSignature(signatures,(object)null).ReturnType==contract.Item4);
    }
    catch {failures.Add(contract.Item1+"::"+contract.Item2+" menu method changed");}
foreach(var (owner,name) in new[]{("PegUI","MouseInputUpdate"),("PackOpening","UpdateDraggedPack"),("UniversalInputManager","UserCancelTextInput")})
    if((Method(gameMetadata,owner,name).Attributes & MethodAttributes.MemberAccessMask)!=MethodAttributes.Private)
        failures.Add(owner+"::"+name+" private menu hook changed");
if (!Calls(plugin, pluginMetadata, Method(pluginMetadata, "NativeUIProbe", "Initialize")).Contains(("NativeAchievements", "Initialize")))
    failures.Add("Probe initialization no longer installs the native achievement browser");
foreach (var contract in new[] {
    ("BnetBar", "ToggleGameMenu", Array.Empty<string>(), "Void"),
    ("MultiSliceElement", "UpdateSlices", Array.Empty<string>(), "Void"),
    ("GameMenu", "Show", new[] { "Boolean" }, "Void"),
    ("GameMenu", "Hide", Array.Empty<string>(), "Void"),
    ("GameMenu", "OnDestroy", Array.Empty<string>(), "Void"),
    ("BaseHeroSkinInfoManager", "EnterPreview", new[] { "CollectionCardVisual" }, "Void"),
    ("CardBackManager", "GetCollectionManagerCardBackPurchaseProductId", new[] { "Int32" }, "Int32"),
    ("HeroSkinUtils", "GetCollectionManagerHeroSkinPurchaseProductData", new[] { "String" }, "HeroSkinProductData"),
    ("CardBackInfoManager", "EnterPreview", new[] { "Int32", "CollectionCardVisual" }, "Void"),
    ("EntityDef", "GetFlavorText", Array.Empty<string>(), "String")
})
    try { MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures); }
    catch { failures.Add(contract.Item1 + "::" + contract.Item2 + " achievement browser contract changed"); }
foreach (var (owner, field, type) in new[] {
    ("BaseHeroSkinInfoManager", "m_currentEntityDef", "EntityDef"),
    ("BaseHeroSkinInfoManager", "m_vanillaHeroDescription", "UberText"),
    ("BaseHeroSkinInfoManager", "m_newHeroDescription", "UberText"),
    ("CollectionPageManager", "m_coinsTab", "CollectionClassTab"),
    ("CoinPreview", "m_favoriteButton", "UIBButton"),
    ("CoinPreview", "m_clickable", "Clickable"),
    ("CardBackInfoManager", "m_currentCardBackIdx", "Nullable`1<Int32>"),
    ("CardBackInfoManager", "m_description", "UberText")
})
    try { if (Field(gameMetadata, owner, field).DecodeSignature(signatures, (object)null) != type) failures.Add(owner + "." + field + " acquisition-copy field type changed"); }
    catch { failures.Add(owner + "." + field + " acquisition-copy field missing"); }
// NPC choice backs: ClientBridge.OpposingChoiceRevealed reads the opposing ChoiceCardMgr state to pace the NPC's pick.
foreach (var contract in new[] {
    ("ChoiceCardMgr", "Get", Array.Empty<string>(), "ChoiceCardMgr"),
    ("ChoiceCardMgr", "GetChoiceStateForPlayer", new[] { "Int32" }, "ChoiceState"),
    ("GameState", "GetOpposingSidePlayer", Array.Empty<string>(), "Player"),
    ("GameState", "GetOpponentEntityChoices", Array.Empty<string>(), "EntityChoices")
})
    try { MethodSignature(gameMetadata, contract.Item1, contract.Item2, contract.Item3, contract.Item4, signatures); }
    catch { failures.Add(contract.Item1 + "::" + contract.Item2 + " opposing choice reveal contract changed"); }
try
{
    var manager = gameMetadata.TypeDefinitions.Select(gameMetadata.GetTypeDefinition)
        .Single(t => gameMetadata.GetString(t.Name) == "ChoiceCardMgr" && t.GetDeclaringType().IsNil);
    var state = manager.GetNestedTypes().Select(gameMetadata.GetTypeDefinition).Single(t => gameMetadata.GetString(t.Name) == "ChoiceState");
    if ((state.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.NestedPublic) throw new Exception("ChoiceState not public");
    foreach (var (field, type) in new[] { ("m_choiceID", "Int32"), ("m_hasBeenRevealed", "Boolean"), ("m_hasBeenConcealed", "Boolean") })
    {
        var definition = state.GetFields().Select(gameMetadata.GetFieldDefinition).Single(f => gameMetadata.GetString(f.Name) == field);
        if ((definition.Attributes & FieldAttributes.Public) == 0 || definition.DecodeSignature(signatures, (object)null) != type)
            throw new Exception(field);
    }
    // ClientBridge's received probe reads Network.EntityChoices.ID of the opponent's processed choice.
    var network = gameMetadata.TypeDefinitions.Select(gameMetadata.GetTypeDefinition)
        .Single(t => gameMetadata.GetString(t.Name) == "Network" && t.Namespace.IsNil && t.GetDeclaringType().IsNil);
    var choices = network.GetNestedTypes().Select(gameMetadata.GetTypeDefinition).Single(t => gameMetadata.GetString(t.Name) == "EntityChoices");
    if (!choices.GetProperties().Select(gameMetadata.GetPropertyDefinition).Any(p => gameMetadata.GetString(p.Name) == "ID")
        && !choices.GetFields().Select(gameMetadata.GetFieldDefinition).Any(f => gameMetadata.GetString(f.Name) == "ID"))
        throw new Exception("Network.EntityChoices.ID");
    // Several metadata types are named Player; Method() selects the global gameplay one.
    if (Method(gameMetadata, "Player", "GetPlayerId").DecodeSignature(signatures, (object)null).ReturnType != "Int32") throw new Exception("Player.GetPlayerId");
    Method(pluginMetadata, "ClientBridge", "OpposingChoiceRevealed");
}
catch (Exception error) { failures.Add("ChoiceCardMgr.ChoiceState opposing reveal contract changed: " + error.Message); }
// Startup ordering: Mono runs a class's static initializers while JIT-compiling a method that touches its statics. The card
// catalogs (NativeUIProjection -> LocalEconomy -> engine Cards, ~3 s) must start after the isolation guard, in their own
// measured STARTUP_TIMING stage, so InitializeProbe never touches NativeUIProjection itself.
{
    var probeInit = Method(pluginMetadata, "OfflineProbe", "InitializeProbe");
    var loanerPath = Method(pluginMetadata, "OfflineProbe", "ConfigureLoanerGroupPath");
    if (StaticOwners(plugin, pluginMetadata, probeInit).Concat(Calls(plugin, pluginMetadata, probeInit).Select(call => call.Type)).Contains("NativeUIProjection"))
        failures.Add("OfflineProbe.InitializeProbe touches NativeUIProjection: its catalogs would initialize before the isolation guard");
    if (!Calls(plugin, pluginMetadata, probeInit).Contains(("OfflineProbe", "ConfigureLoanerGroupPath"))
        || (loanerPath.ImplAttributes & System.Reflection.MethodImplAttributes.NoInlining) == 0
        || !StaticOwners(plugin, pluginMetadata, loanerPath).Contains("NativeUIProjection"))
        failures.Add("OfflineProbe.ConfigureLoanerGroupPath no longer isolates the card catalog initialization");
}
PackHookIlChecks.Run(root, (ok, message) => { if (!ok) failures.Add(message); });
if (failures.Count != 0) throw new Exception(string.Join("\n", failures));
Console.WriteLine("PASS native pack, deck, reward, crafting and onboarding hooks, scoped login sites, card audit APIs and reconcile contract against pinned metadata");

static IEnumerable<(string Type, string Method)> Calls(PEReader pe, MetadataReader reader, MethodDefinition method)
{
    var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null))
        .ToDictionary(opcode => unchecked((ushort)opcode.Value));
    byte[] il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();
    for (int offset = 0; offset < il.Length;)
    {
        ushort code = il[offset++];
        if (code == 0xfe) code = (ushort)(0xfe00 | il[offset++]);
        OpCode opcode = opcodes[code];
        if (opcode == OpCodes.Call || opcode == OpCodes.Callvirt)
        {
            EntityHandle handle = MetadataTokens.EntityHandle(BitConverter.ToInt32(il, offset));
            if (handle.Kind == HandleKind.MethodDefinition)
            {
                var called = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                yield return (reader.GetString(reader.GetTypeDefinition(called.GetDeclaringType()).Name), reader.GetString(called.Name));
            }
        }
        offset += opcode.OperandType switch {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
            _ => 4
        };
    }
}

static IEnumerable<string> StaticOwners(PEReader pe, MetadataReader reader, MethodDefinition method)
{
    var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null))
        .ToDictionary(opcode => unchecked((ushort)opcode.Value));
    byte[] il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();
    for (int offset = 0; offset < il.Length;)
    {
        ushort code = il[offset++];
        if (code == 0xfe) code = (ushort)(0xfe00 | il[offset++]);
        OpCode opcode = opcodes[code];
        if (opcode == OpCodes.Ldsfld || opcode == OpCodes.Stsfld || opcode == OpCodes.Ldsflda)
        {
            EntityHandle handle = MetadataTokens.EntityHandle(BitConverter.ToInt32(il, offset));
            if (handle.Kind == HandleKind.FieldDefinition)
                yield return reader.GetString(reader.GetTypeDefinition(reader.GetFieldDefinition((FieldDefinitionHandle)handle).GetDeclaringType()).Name);
        }
        offset += opcode.OperandType switch {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
            _ => 4
        };
    }
}

static IEnumerable<int> Int32Literals(PEReader pe, MethodDefinition method)
{
    var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null))
        .ToDictionary(opcode => unchecked((ushort)opcode.Value));
    byte[] il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();
    for (int offset = 0; offset < il.Length;)
    {
        ushort code = il[offset++];
        if (code == 0xfe) code = (ushort)(0xfe00 | il[offset++]);
        OpCode opcode = opcodes[code];
        if (opcode == OpCodes.Ldc_I4_S) yield return (sbyte)il[offset];
        else if (opcode == OpCodes.Ldc_I4) yield return BitConverter.ToInt32(il, offset);
        offset += opcode.OperandType switch {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
            _ => 4
        };
    }
}

static MethodDefinition Method(MetadataReader reader, string typeName, string methodName)
{
    // "Namespace.Type" names a namespaced type; a bare name means the global namespace.
    int dot = typeName.LastIndexOf('.');
    string space = dot < 0 ? "" : typeName.Substring(0, dot), shortName = typeName.Substring(dot + 1);
    var type = reader.TypeDefinitions.Select(reader.GetTypeDefinition).Single(t => reader.GetString(t.Name) == shortName
        && reader.GetString(t.Namespace) == space && t.GetDeclaringType().IsNil);
    return type.GetMethods().Select(reader.GetMethodDefinition).Single(m => reader.GetString(m.Name) == methodName);
}
static MethodDefinition MethodSignature(MetadataReader reader, string typeName, string methodName, string[] parameterTypes, string returnType, TypeNames signatures)
{
    return reader.TypeDefinitions.Select(reader.GetTypeDefinition).Where(t => reader.GetString(t.Name) == typeName)
        .SelectMany(type => type.GetMethods()).Select(reader.GetMethodDefinition).Single(method => reader.GetString(method.Name) == methodName
        && method.DecodeSignature(signatures, (object)null).ParameterTypes.SequenceEqual(parameterTypes)
        && method.DecodeSignature(signatures, (object)null).ReturnType == returnType);
}
static MemberReference MemberReferenceSignature(MetadataReader reader, string typeName, string methodName,
    string[] parameterTypes, string returnType, TypeNames signatures)
{
    return reader.MemberReferences.Select(reader.GetMemberReference).Single(member =>
    {
        if (reader.GetString(member.Name) != methodName || member.Parent.Kind != HandleKind.TypeReference
            || reader.GetString(reader.GetTypeReference((TypeReferenceHandle)member.Parent).Name) != typeName)
            return false;
        var signature = member.DecodeMethodSignature(signatures, (object)null);
        return signature.ParameterTypes.SequenceEqual(parameterTypes) && signature.ReturnType == returnType;
    });
}
static int EnumValue(MetadataReader reader, string typeName, string fieldName) =>
    TryEnumValue(reader, typeName, fieldName) ?? throw new Exception(typeName + "." + fieldName + " missing from compiled metadata");
static int? TryEnumValue(MetadataReader reader, string typeName, string fieldName)
{
    var type = reader.TypeDefinitions.Select(reader.GetTypeDefinition)
        .Single(t => reader.GetString(t.Name) == typeName && t.GetDeclaringType().IsNil);
    var field = type.GetFields().Select(reader.GetFieldDefinition)
        .SingleOrDefault(candidate => reader.GetString(candidate.Name) == fieldName);
    if (field.Equals(default(FieldDefinition)) || field.GetDefaultValue().IsNil) return null;
    var constant = reader.GetConstant(field.GetDefaultValue());
    return reader.GetBlobReader(constant.Value).ReadInt32();
}
static FieldDefinition Field(MetadataReader reader, string typeName, string fieldName)
{
    return reader.TypeDefinitions.Select(reader.GetTypeDefinition).Where(t => reader.GetString(t.Name) == typeName)
        .SelectMany(type => type.GetFields()).Select(reader.GetFieldDefinition).Single(f => reader.GetString(f.Name) == fieldName);
}
static HashSet<string> UserStrings(MetadataReader reader)
{
    var strings = new HashSet<string>(StringComparer.Ordinal);
    for (var handle = MetadataTokens.UserStringHandle(1); !handle.IsNil; handle = reader.GetNextHandle(handle))
        strings.Add(reader.GetUserString(handle));
    return strings;
}
static Parameter[] Parameters(MetadataReader reader, MethodDefinition method) => method.GetParameters()
    .Select(reader.GetParameter).Where(p => p.SequenceNumber != 0).OrderBy(p => p.SequenceNumber).ToArray();

sealed class TypeNames : ISignatureTypeProvider<string, object>
{
    public string GetPrimitiveType(PrimitiveTypeCode code) => code.ToString();
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte kind) => reader.GetString(reader.GetTypeDefinition(handle).Name);
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte kind) => reader.GetString(reader.GetTypeReference(handle).Name);
    public string GetTypeFromSpecification(MetadataReader reader, object context, TypeSpecificationHandle handle, byte kind) => reader.GetTypeSpecification(handle).DecodeSignature(this, context);
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> arguments) => genericType + "<" + string.Join(",", arguments) + ">";
    public string GetSZArrayType(string type) => type + "[]";
    public string GetArrayType(string type, ArrayShape shape) => type + "[" + shape.Rank + "]";
    public string GetByReferenceType(string type) => type + "&";
    public string GetPointerType(string type) => type + "*";
    public string GetPinnedType(string type) => type;
    public string GetModifiedType(string modifier, string type, bool required) => type;
    public string GetGenericMethodParameter(object context, int index) => "!!" + index;
    public string GetGenericTypeParameter(object context, int index) => "!" + index;
    public string GetFunctionPointerType(MethodSignature<string> signature) => throw new NotSupportedException();
}
