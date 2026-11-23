using System;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using Hearthstone.Core;

// Co-op boss modes (Nefarian Rises! first, data/coop/): two people at one client against a boss, started from the home
// screen's fourth mode slot, which reads Tavern Brawl while a co-op mode ships (this client's home screen has no Tavern Brawl
// widget of its own: retail moved brawls into Modes). The match is an encounter like a run fight (both decks, both heroes and the boss come from
// the definition; no ladder record, never settled), with the engine's co-op rules and hot-seat seats: at every handover a
// pass-the-device popup asks the next player to take the screen before their hand is shown.
internal static class NativeCoopMode
{
    private static ManualLogSource log;
    private static readonly Random random = new Random();

    // The co-op mode the Tavern Brawl button starts (the first shipped one until there is a chooser).
    internal static CoopDefinition Current => CoopCatalog.All.FirstOrDefault();

    // Developer and test-hook builds show it; the public build once every card of the mode plays in this engine and the
    // client knows it.
    internal static bool Enabled
    {
        get
        {
            if (Current == null) return false;
#if SOLOSTONE_RELEASE
            if (ready == null)
            {
                if (!(DefLoader.Get()?.GetAllEntityDefs()?.Count > 0)) return false;
                ready = Admit(Current) == null;
            }
            return ready.Value;
#else
            return true;
#endif
        }
    }
#if SOLOSTONE_RELEASE
    private static bool? ready;
#endif

    // Null when every card of the mode is implemented and known to the client; else the first problem.
    internal static string Admit(CoopDefinition definition)
    {
        foreach (string id in definition.Seats.SelectMany(seat => seat.Deck.Concat(new[] { seat.Hero }))
            .Concat(definition.Seats.Where(seat => seat.Skin != null).Select(seat => seat.Skin)).Append(definition.Boss).Distinct())
        {
            SabberStoneCore.Model.Card card;
            try { card = SabberStoneCore.Model.Cards.FromId(id); }
            catch (Exception) { card = null; }
            if (card == null) return id + ": not in the engine";
            if (!card.Implemented && card.Type != SabberStoneCore.Enums.CardType.HERO) return id + ": not implemented";
            if (DefLoader.Get()?.GetEntityDef(id) == null) return id + ": the client has no definition";
        }
        return null;
    }

    internal static void Install(Harmony harmony, ManualLogSource logger)
    {
        log = logger;
        NativeHomeUI.CoopModesAvailable = () => Enabled;
        NativeHomeUI.ModeSlotEnabled = WireSlot;
        // The slot's scroll button presses only while its server feature (Battlegrounds) is active: offline it never is.
        harmony.Patch(AccessTools.Method(typeof(BoxScrollButton), "IsFeatureActive"),
            postfix: new HarmonyMethod(typeof(NativeCoopMode), nameof(SlotFeatureActive)));
        // Box.OnButtonPressed checks the server's feature flags before it reaches the slot's own handler: catch the press first.
        harmony.Patch(AccessTools.Method(typeof(Box), "OnButtonPressed", new[] { typeof(UIEvent) }),
            prefix: new HarmonyMethod(typeof(NativeCoopMode), nameof(BoxButtonPressed)));
        foreach (string handler in new[] { "ContinueButtonPress_PrevMode", "ContinueButtonPress_Common" })
            harmony.Patch(AccessTools.Method(typeof(EndGameScreen), handler), prefix: new HarmonyMethod(typeof(NativeCoopMode), nameof(EndScreenContinue)));
        harmony.Patch(AccessTools.Method(typeof(EndGameScreen), "Show"), postfix: new HarmonyMethod(typeof(NativeCoopMode), nameof(EndScreenShown)));
        // The victory or defeat banner waits on services that do not exist offline; a co-op match has no rewards to wait for.
        harmony.Patch(AccessTools.DeclaredMethod(typeof(EndGameScreen), "Awake"), postfix: new HarmonyMethod(typeof(NativeCoopMode), nameof(EndScreenAwake)));
        foreach (var method in new[] { AccessTools.DeclaredMethod(typeof(EndGameScreen), "ShouldMakeUtilRequests"),
            AccessTools.DeclaredMethod(typeof(EndGameScreen), "ShowBmecRewards"), AccessTools.DeclaredMethod(typeof(VictoryScreen), "JustEarnedHeroReward") })
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(NativeCoopMode), nameof(NoServiceResult)));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(EndGameScreen), "ShowJournalGameOnlyXpGains"), prefix: new HarmonyMethod(typeof(NativeCoopMode), nameof(NoServiceStep)));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(EndGameTwoScoop), "Show"), prefix: new HarmonyMethod(typeof(NativeCoopMode), nameof(BannerWithoutXp)));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(NameBanner), "ShouldShowGameIconBanner"), postfix: new HarmonyMethod(typeof(NativeCoopMode), nameof(NoModeIconBanner)));
        harmony.Patch(AccessTools.Method(typeof(PowerProcessor), "CreateSwapGameStateSpellController"),
            postfix: new HarmonyMethod(typeof(NativeCoopMode), nameof(SwapCoverFromRewind)));
        harmony.Patch(AccessTools.Method(typeof(ResetGameSpellController), "IsResetSourceRewind"),
            postfix: new HarmonyMethod(typeof(NativeCoopMode), nameof(SwapLooksLikeRewind)));
        var resetEffects = typeof(ResetGameSpellController).GetNestedTypes(AccessTools.all)
            .FirstOrDefault(type => type.Name.StartsWith("<DoEffectsWithTiming>", StringComparison.Ordinal));
        if (resetEffects == null) log?.LogWarning("COOP: the reset effects coroutine was not found; swaps keep the client's audio pause");
        else harmony.Patch(AccessTools.Method(resetEffects, "MoveNext"), prefix: new HarmonyMethod(typeof(NativeCoopMode), nameof(ResetStep)),
            postfix: new HarmonyMethod(typeof(NativeCoopMode), nameof(ResetStepDone)));
    }

    // A swap's reset controller (SwapGameStateSpellController) is built from no prefab, so its Rewind cover spells are empty
    // and it shows the board vanishing and coming back. In hot-seat it takes the screen wipe and loop of the client's own
    // RewindGameSpellController prefab (loaded once, kept inactive) and plays them as a Rewind does. Only the cover changes:
    // the swap stays a game-state swap and its rebuild runs as before. A load that fails leaves the old look.
    private const string RewindControllerPrefab = "RewindGameSpellController.prefab:9dd5e7376da08de4fb6b0baa314ff0ba";
    private static readonly string[] RewindCoverFields = { "m_RewindSpellScreenWipe", "m_RewindHideScreenLoop",
        "m_RewindSpellScreenWipeOpponent", "m_RewindHideScreenLoopOpponent", "m_screenWipeToLoopTransitionTimer" };
    private static RewindGameSpellController rewindTemplate;
    private static bool rewindTemplateFailed;

    private static void SwapCoverFromRewind(ResetGameSpellController __result)
    {
        if (__result == null || ClientBridge.ActiveMatch?.HotSeat != true) return;
        try
        {
            var template = RewindTemplate();
            if (template == null) return;
            foreach (string name in RewindCoverFields)
                AccessTools.Field(typeof(ResetGameSpellController), name).SetValue(__result, AccessTools.Field(typeof(RewindGameSpellController), name).GetValue(template));
        }
        catch (Exception error) { log?.LogWarning("COOP swap: Rewind cover not applied: " + error.GetBaseException().Message); }
    }

    private static RewindGameSpellController RewindTemplate()
    {
        if (rewindTemplate != null || rewindTemplateFailed) return rewindTemplate;
        try
        {
            object loader = typeof(AssetLoader).GetMethod("Get", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.Invoke(null, null)
                ?? throw new InvalidOperationException("AssetLoader unavailable");
            // InstantiatePrefab(AssetReference, AssetLoadingOptions), as PowerProcessor.CreateSpellController calls it (options 0).
            var instantiate = loader.GetType().GetMethods().First(m => m.Name == "InstantiatePrefab" && m.ReturnType == typeof(UnityEngine.GameObject)
                && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType.IsEnum);
            var parameters = instantiate.GetParameters();
            var reference = parameters[0].ParameterType.GetMethod("op_Implicit", new[] { typeof(string) })
                ?? throw new MissingMethodException(parameters[0].ParameterType.FullName, "op_Implicit");
            var root = instantiate.Invoke(loader, new[] { reference.Invoke(null, new object[] { RewindControllerPrefab }), Enum.ToObject(parameters[1].ParameterType, 0) })
                as UnityEngine.GameObject ?? throw new InvalidOperationException("prefab did not load");
            root.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(root);
            rewindTemplate = root.GetComponent<RewindGameSpellController>() ?? throw new InvalidOperationException("no RewindGameSpellController on the prefab");
            log?.LogInfo("COOP swap: Rewind cover loaded");
        }
        catch (Exception error) { rewindTemplateFailed = true; log?.LogWarning("COOP swap: Rewind cover unavailable: " + error.GetBaseException().Message); }
        return rewindTemplate;
    }

    private static readonly System.Reflection.FieldInfo RewindLoop = AccessTools.Field(typeof(ResetGameSpellController), "m_RewindHideScreenLoop");
    private static void SwapLooksLikeRewind(ResetGameSpellController __instance, ref bool __result)
    {
        if (!__result && __instance is SwapGameStateSpellController && ClientBridge.ActiveMatch?.HotSeat == true
            && RewindLoop?.GetValue(__instance) is UnityEngine.Object loop && loop != null) __result = true;
    }

    // A hot-seat swap is a GAME_RESET. While the client rebuilds the board for it, ResetGameSpellController pauses the whole
    // AudioListener (the rebuilt cards stay silent) and resumes it once the rebuild is done: a few seconds without music or
    // ambience after every handover. In hot-seat the sounds already playing when the reset starts (music, ambience) ignore
    // that pause and get their setting back after it; the rebuild itself stays silent. AudioModule is not a plugin reference.
    private static readonly Type AudioSourceType = Type.GetType("UnityEngine.AudioSource, UnityEngine.AudioModule");
    private static readonly Type AudioListenerType = Type.GetType("UnityEngine.AudioListener, UnityEngine.AudioModule");
    private static System.Collections.Generic.List<UnityEngine.Object> keptPlaying;
    private static bool resetPaused;

    private static void ResetStep()
    {
        try
        {
            if (keptPlaying != null || ClientBridge.ActiveMatch?.HotSeat != true || AudioSourceType == null || AudioListenerType == null) return;
            keptPlaying = new System.Collections.Generic.List<UnityEngine.Object>();
            resetPaused = false;
            var isPlaying = AudioSourceType.GetProperty("isPlaying");
            var ignorePause = AudioSourceType.GetProperty("ignoreListenerPause");
            foreach (var source in UnityEngine.Object.FindObjectsOfType(AudioSourceType))
                if ((bool)isPlaying.GetValue(source, null) && !(bool)ignorePause.GetValue(source, null))
                {
                    ignorePause.SetValue(source, true, null);
                    keptPlaying.Add(source);
                }
            log?.LogInfo("COOP swap: " + keptPlaying.Count + " playing sound(s) kept through the reset's audio pause");
        }
        catch (Exception error) { log?.LogWarning("COOP swap sound keep failed: " + error.GetBaseException().Message); }
    }

    private static void ResetStepDone(bool __result)
    {
        // The reset rebuilt the name banners with the old seat's names; the swap's own refresh can come before that.
        if (!__result && ClientBridge.ActiveMatch?.HotSeat == true && seats != null) Processor.ScheduleCallback(0.1f, false, _ => RefreshBanners());
        if (keptPlaying == null) return;
        try
        {
            if ((bool)AudioListenerType.GetProperty("pause").GetValue(null, null)) { resetPaused = true; return; }
            if (!resetPaused && __result) return;
            var ignorePause = AudioSourceType.GetProperty("ignoreListenerPause");
            foreach (var source in keptPlaying)
                if (source != null) ignorePause.SetValue(source, false, null);
        }
        catch (Exception error) { log?.LogWarning("COOP swap sound restore failed: " + error.GetBaseException().Message); }
        keptPlaying = null;
        resetPaused = false;
    }

    private static void SlotFeatureActive(BoxScrollButton __instance, ref bool __result)
    {
        if (!__result && Enabled && __instance != null && __instance == Box.Get()?.m_BattleGroundsButton) __result = true;
    }

    // The slot's own listener starts the brawl (NativeHomeUI.EnableMode); the box's generic route must not act on the press.
    private static bool BoxButtonPressed(Box __instance, UIEvent __0) =>
        !Enabled || __instance == null || __0?.GetElement() == null || __0.GetElement() != __instance.m_BattleGroundsButton;

    // Box.OnButtonPressed checks the server's feature flags before the slot's own handler: the slot gets its own listener (once
    // per button instance; a reloaded HUB builds a new box). Box buttons act on PRESS.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<BoxMenuButton, object> wiredSlots =
        new System.Runtime.CompilerServices.ConditionalWeakTable<BoxMenuButton, object>();
    private static void WireSlot(BoxMenuButton button)
    {
        if (button == null || wiredSlots.TryGetValue(button, out _)) return;
        wiredSlots.Add(button, new object());
        button.AddEventListener(UIEventType.PRESS, _ => {
            log?.LogInfo("COOP slot pressed");
            if (SceneMgr.IsInitialized() && SceneMgr.Get().GetMode() == SceneMgr.Mode.HUB) BrawlPressed();
        });
        log?.LogInfo("COOP fourth mode slot wired: " + button.name);
    }

    private static bool BrawlPressed()
    {
        if (!Enabled) return true;
        try { ShowStart(Current); }
        catch (Exception error) { log?.LogWarning("COOP start popup unavailable: " + error.Message); }
        return false;
    }

    private static void ShowStart(CoopDefinition definition)
    {
        var info = new AlertPopup.PopupInfo {
            m_id = "solostone-coop-start", m_headerText = definition.Name, m_showAlertIcon = false, m_keyboardEscIsCancel = true,
            m_text = "Two players, one screen: " + definition.Seats[0].Name + " and " + definition.Seats[1].Name
                + " fight " + BossName(definition) + " together. If either hero falls, you both lose.\n\n"
                + (definition.HandoverScreen ? "The game hands the screen over at every turn." : "The board turns around for the next player at every turn."),
            m_responseDisplay = AlertPopup.ResponseDisplay.CONFIRM_CANCEL,
            m_confirmText = "PLAY", m_cancelText = NativeStrings.Get("GLOBAL_CANCEL", "Cancel").ToUpperInvariant(),
            m_responseCallback = (response, data) => { if (response == AlertPopup.Response.CONFIRM) Start(definition); },
        };
        DialogManager.Get()?.ShowUniquePopup(info);
    }

    // The boss as the start popup names it: its engine card's name.
    private static string BossName(CoopDefinition definition) =>
        SabberStoneCore.Model.Cards.FromId(definition.Boss)?.Name is string name && name.Length > 0 ? name : "the boss";

    // Starts a match: the two seats are dealt at random to the engine's Player 1 (who starts) and Player 2 (the boss's side).
    internal static void Start(CoopDefinition definition)
    {
        try
        {
            bool swapped = random.Next(2) == 1;
            CoopSeat first = definition.Seats[swapped ? 1 : 0], second = definition.Seats[swapped ? 0 : 1];
            seats = new[] { first, second };
            var encounter = new EncounterSpec {
                PlayerHero = first.Hero, PlayerHeroSkin = first.Skin, PlayerDeck = first.Deck,
                NpcHero = second.Hero, NpcHeroSkin = second.Skin, NpcDeck = second.Deck, NpcName = second.Name,
                NpcBoard = new[] { definition.Boss },
                FirstPlayer = 1, MissionId = definition.Scenario, BoardId = definition.Board,
                Coop = true, HotSeat = true,
            };
            int seed = random.Next();
            var match = ClientBridge.PrepareRunMatch(encounter, seed);
            // Without the handover screen the seats swap as soon as the turn ends.
            match.HandoverPrompt = definition.HandoverScreen ? Handover : (Action<int, Action>)null;
            match.SeatSwapped = () => Processor.ScheduleCallback(0.6f, false, _ => RefreshBanners(early: true));
            string id = "coop:" + definition.Id + ":" + Guid.NewGuid().ToString("N");
            log?.LogInfo("COOP start " + definition.Id + " id=" + id + " seed=" + seed + " player1=" + first.Name + " player2=" + second.Name);
            ended = false;
            // Developer builds may stage the opening with the debug scenario file, as a run fight does (human = Player 1,
            // npc = Player 2); the brawl's encounter stays.
            DebugScenario scenario = null;
#if !SOLOSTONE_RELEASE
            scenario = DebugScenario.Read(null, message => log?.LogWarning(message), ClientBridge.ClientKnowsCard, DebugScenario.RunId, NativeTestControl.DiagnosticsEnabled);
#endif
            ClientBridge.StartRunMatch(match, id, scenario);
        }
        catch (Exception error) { log?.LogError("COOP start failed: " + error); }
    }

    // The seats of the match under way: [0] = Player 1, [1] = Player 2.
    private static CoopSeat[] seats;
    private static bool ended, leaving;

    private static string SeatName(int seat) => seats != null && seat >= 1 && seat <= 2 ? seats[seat - 1].Name : "Player " + seat;

    // The pass-the-device popup: nothing of the next player's hand is sent before Continue.
    private const string HandoverPopupId = "solostone-coop-handover";
    private static Action pendingProceed;

    // Test control's handover-continue: the popup's Continue, without a pointer. The popup loads asynchronously and may not be
    // on screen yet: it is closed now, and again for a few seconds in case it appears after the swap.
    internal static bool ContinueHandoverForTest()
    {
        var proceed = pendingProceed;
        if (proceed == null) return false;
        pendingProceed = null;
        DismissHandoverPopup(6);
        proceed();
        return true;
    }

    private static void DismissHandoverPopup(int retries)
    {
        try { AccessTools.Method(typeof(DialogManager), "DismissAlertOrRemoveFromQueue")?.Invoke(DialogManager.Get(), new object[] { HandoverPopupId }); }
        catch (Exception error) { log?.LogWarning("COOP handover popup not dismissed: " + error.GetBaseException().Message); }
        if (retries > 0 && pendingProceed == null) Processor.ScheduleCallback(0.5f, false, _ => { if (pendingProceed == null) DismissHandoverPopup(retries - 1); });
    }

    private static void Handover(int seat, Action proceed)
    {
        string name = SeatName(seat);
        // Each popup answers only its own handover: a stale one (a test-control continue that beat its loading) does nothing.
        Action once = () => { if (!ReferenceEquals(pendingProceed, proceed)) return; pendingProceed = null; proceed(); };
        DismissHandoverPopup(0);
        pendingProceed = proceed;
        var info = new AlertPopup.PopupInfo {
            m_id = HandoverPopupId, m_headerText = name + "'s turn", m_showAlertIcon = false, m_keyboardEscIsCancel = false,
            m_text = "Hand the screen to " + name + ".\n\n" + name + ", press Continue when you are ready.",
            m_responseDisplay = AlertPopup.ResponseDisplay.OK, m_okText = NativeStrings.Get("GLOBAL_CONTINUE", "CONTINUE"),
            m_responseCallback = (response, data) => {
                try { once(); }
                catch (Exception error) { log?.LogError("COOP handover failed: " + error); }
            },
        };
        log?.LogInfo("COOP handover to Player" + seat + " (" + name + ")");
        if (DialogManager.Get() == null) { once(); return; }
        DialogManager.Get().ShowUniquePopup(info);
    }

    // The client's name banners stay bound to the player id each side had at the start: after a swap each banner is rebound to
    // the player now on its side and shows that player's hero again.
    // The early refresh can land while the swap's reset is still rebuilding the board; the refresh after the reset repeats it.
    private static void RefreshBanners(bool early = false)
    {
        try
        {
            var gameplay = Gameplay.Get(); var state = GameState.Get();
            if (gameplay == null || state == null) return;
            var playerId = AccessTools.Field(typeof(NameBanner), "m_playerId");
            foreach (var side in new[] { Player.Side.FRIENDLY, Player.Side.OPPOSING })
            {
                var banner = gameplay.GetNameBannerForSide(side);
                var player = side == Player.Side.FRIENDLY ? state.GetFriendlySidePlayer() : state.GetOpposingSidePlayer();
                if (banner == null || player == null) continue;
                playerId?.SetValue(banner, player.GetPlayerId());
                AccessTools.Method(typeof(NameBanner), "UpdateHeroNameBanner")?.Invoke(banner, null);
                var subtext = AccessTools.Method(typeof(NameBanner), "UpdateSubtext");
                if (subtext != null && subtext.GetParameters().Length == 0) subtext.Invoke(banner, null);
            }
        }
        catch (Exception error) { if (!early) log?.LogWarning("COOP name banners not refreshed: " + error.GetBaseException().Message); }
    }

    private static bool InCoopMatch => ClientBridge.ActiveMatch?.HotSeat == true && ClientBridge.ActiveMatch.ActiveEncounter?.Coop == true;

    // In any mission the bottom name banner keeps room for a game-mode icon on its left, and this match shows none (the
    // client's Tavern Brawl icon needs its own brawl game type): the name sat further right on a longer banner than the top
    // one. Both seats take turns at the bottom, so both banners use the plain layout.
    private static void NoModeIconBanner(ref bool __result)
    {
        if (__result && InCoopMatch) __result = false;
    }

    private static bool OwnsEndScreen => InCoopMatch && !ClientBridge.OwnsLocalPostMatch;

    // The end screen shows its victory or defeat banner once net cache, achievements and rewards report ready; offline they
    // never do (it gives up after 5 s). A co-op match has none of them: they count as ready, and there is no rank, card back
    // or reward-track progress to show.
    private static void EndScreenAwake(EndGameScreen __instance)
    {
        if (!OwnsEndScreen || __instance == null) return;
        try
        {
            foreach (string ready in new[] { "m_netCacheReady", "m_achievesReady", "m_rewardsLoaded" })
                AccessTools.Field(typeof(EndGameScreen), ready).SetValue(__instance, true);
            foreach (string shown in new[] { "m_shouldShowRankChange", "m_shouldShowRankedCardBackProgress", "m_shouldShowRewardXpGains" })
                AccessTools.Field(typeof(EndGameScreen), shown).SetValue(__instance, false);
        }
        catch (Exception error) { log?.LogWarning("COOP end screen keeps its service waits: " + error.GetBaseException().Message); }
    }

    private static bool NoServiceResult(ref bool __result)
    {
        if (!OwnsEndScreen) return true;
        __result = false;
        return false;
    }

    private static bool NoServiceStep() => !OwnsEndScreen;

    private static void BannerWithoutXp(ref bool showXPBar)
    {
        if (OwnsEndScreen) showXPBar = false;
    }

    // The client's end screen never registers its continue offline: a click goes back to the home screen. It takes clicks
    // once the banner is up, as the client's own continue does (or after a while, should the banner never come).
    private static void EndScreenShown(EndGameScreen __instance)
    {
        if (!InCoopMatch || __instance?.m_hitbox == null) return;
        ended = true; leaving = false;
        float shownAt = UnityEngine.Time.realtimeSinceStartup;
        var bannerShown = AccessTools.Field(typeof(EndGameScreen), "m_haveShownTwoScoop");
        __instance.m_hitbox.AddEventListener(UIEventType.RELEASE, _ => {
            bool banner = bannerShown == null || (bool)bannerShown.GetValue(__instance);
            if (banner || UnityEngine.Time.realtimeSinceStartup - shownAt > EndScreenBannerWait) EndScreenContinue();
        });
        log?.LogInfo("COOP end screen shown");
    }

    private const float EndScreenBannerWait = 8f;

    private static bool EndScreenContinue()
    {
        if (!ended) return true;
        if (!leaving)
        {
            leaving = true; ended = false; seats = null;
            log?.LogInfo("COOP end screen continued: home");
            // No ladder result to present: the match is done (as a run fight's end, NativeRunModes).
            LocalLadder.RewardPresentationComplete();
            NativeUIProbe.Navigate(SceneMgr.Mode.HUB);
        }
        return false;
    }
}
