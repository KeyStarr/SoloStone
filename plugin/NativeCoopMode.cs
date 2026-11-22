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
                + " fight Nefarian together. If either hero falls, you both lose.\n\nThe game hands the screen over at every turn.",
            m_responseDisplay = AlertPopup.ResponseDisplay.CONFIRM_CANCEL,
            m_confirmText = "PLAY", m_cancelText = NativeStrings.Get("GLOBAL_CANCEL", "Cancel").ToUpperInvariant(),
            m_responseCallback = (response, data) => { if (response == AlertPopup.Response.CONFIRM) Start(definition); },
        };
        DialogManager.Get()?.ShowUniquePopup(info);
    }

    // Starts a match: the two seats are dealt at random to the engine's Player 1 (who starts) and Player 2 (Nefarian's side).
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
            match.HandoverPrompt = Handover;
            match.SeatSwapped = () => Processor.ScheduleCallback(0.6f, false, _ => RefreshBanners());
            string id = "coop:" + definition.Id + ":" + Guid.NewGuid().ToString("N");
            log?.LogInfo("COOP start " + definition.Id + " id=" + id + " seed=" + seed + " player1=" + first.Name + " player2=" + second.Name);
            ended = false;
            ClientBridge.StartRunMatch(match, id);
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
            m_responseDisplay = AlertPopup.ResponseDisplay.OK, m_okText = "CONTINUE",
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
    private static void RefreshBanners()
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
        catch (Exception error) { log?.LogWarning("COOP name banners not refreshed: " + error.GetBaseException().Message); }
    }

    private static bool InCoopMatch => ClientBridge.ActiveMatch?.HotSeat == true && ClientBridge.ActiveMatch.ActiveEncounter?.Coop == true;

    // The client's end screen never registers its continue offline: a click goes back to the home screen.
    private static void EndScreenShown(EndGameScreen __instance)
    {
        if (!InCoopMatch || __instance?.m_hitbox == null) return;
        ended = true; leaving = false;
        __instance.m_hitbox.AddEventListener(UIEventType.RELEASE, _ => EndScreenContinue());
        log?.LogInfo("COOP end screen shown");
    }

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
