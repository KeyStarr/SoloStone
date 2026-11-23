using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Logging;
using HarmonyLib;
using PegasusShared;
using PegasusUtil;
using UnityEngine;

// Transport only. The service owns entry, drafting, settlement and durable rewards.
internal static partial class NativeArena
{
    internal static long DeckId => DeckIdFor(Read());
    private static long DeckIdFor(ArenaView view)
    {
        if (string.IsNullOrEmpty(view.RunId)) return 0;
        using (var hash = SHA256.Create())
            return Math.Max(1, BitConverter.ToInt64(hash.ComputeHash(Encoding.UTF8.GetBytes(view.RunId)), 0) & long.MaxValue);
    }
    // Native slots count the hero as choice zero; the service's own slot remains opaque to the transport.
    private static int NativeSlot(ArenaView view) => view.Phase == ArenaPhase.None || view.Phase == ArenaPhase.ChoosingHero ? 0 : view.Deck.Count + 1;
    private sealed class Reply
    {
        internal int Id, Frame;
        internal float Born;
        internal bool Consumed;
        internal string Accessor;
        internal object Payload;
        internal IArenaService Service;
        internal bool Display, Mutating;
    }
    private static readonly Queue<Reply> replies = new Queue<Reply>();
    private static readonly List<int> lastReplies = new List<int>();
    private static Reply delivering;
    private static ManualLogSource log;
    private static DraftManager loggedIn;
    // Packet ids come from the client's own enums so a client update cannot silently desync them.
    private const int SessionResponseId = (int)ArenaSessionResponse.PacketID.ID, BeginId = (int)DraftBeginning.PacketID.ID,
        RetiredId = (int)DraftRetired.PacketID.ID, ContentsId = (int)DraftChoicesAndContents.PacketID.ID,
        ChosenId = (int)DraftChosen.PacketID.ID, ErrorId = (int)DraftError.PacketID.ID, AckId = (int)DraftRewardsAcked.PacketID.ID,
        PremiumsId = (int)DraftRemovePremiumsResponse.PacketID.ID, SaveId = (int)SaveArenaDeckResponse.PacketID.ID,
        RatingId = (int)ArenaRatingInfoResponse.PacketID.ID;
    private const float ReplyTimeoutSeconds = 30f;
    // lastError is sticky until a mutation succeeds; errorSeq counts every Fail so a driver can tell its own failure from an old one.
    // Informational notices (Underground, Disclaimer, no store, match not started) go to lastNotice and never touch the error state.
    private static string lastError, lastNotice;
    private static int errorSeq;
    private static bool mutationPending, startingMatch, disabled, homePatched;
    // The Home Arena button is offered only when its prefix is installed and Arena is not disabled (else retail's online path runs).
    internal static bool HomeEntryAvailable => homePatched && !disabled;
    // Leaving DRAFT drops replies queued for that screen: a later visit is filled from the saved run, never from a stale reply.
    internal static void LeftDraft() { while (replies.Count > 0) { if (replies.Dequeue().Mutating) mutationPending = false; } }
    // Set when the DRAFT scene never finished loading (NativeArenaLoad): entry is refused for the session, but every offline
    // patch stays active so the scene that is being left never reaches retail code.
    private static string loadFailure;
    // The match's own record says whether it is an Arena match (NativeArenaMatch.OwnsArenaMatch): no flag to set or forget.
    internal static bool MatchActive => OwnsArenaMatch();
    // Offline = every Arena patch is armed (the offline UI is active), also after Arena disabled itself: a DRAFT scene that is still
    // open must never fall through to retail code (SpendTicket, matchmaking, MakeDraftChoice, store). Live = Arena actually works.
    private static bool Offline => NativeUIProbe.Active;
    private static bool Live => NativeUIProbe.Active && !disabled;
    private static bool Inert(bool popup = true) { Notice("Arena is unavailable in this session.", popup); return false; }
    private static bool EntryBlocked => disabled || loadFailure != null;
    // A failure anywhere in Arena disables Arena only. Every patch is gated on Offline, so the rest of the game is untouched.
    internal static void Disable(string reason, Exception error = null)
    {
        if (disabled) return;
        disabled = true;
        log?.LogError("NATIVE_ARENA disabled: " + reason + (error == null ? "" : " " + error.GetBaseException().Message));
        // A DRAFT scene open right now would keep showing Arena that no longer answers: leave it through the client's own navigation.
        try { if (InDraft) log?.LogError("NATIVE_ARENA_DISABLED " + (NativeUIProbe.LeaveStuckScene(SceneMgr.Mode.DRAFT, SceneMgr.Mode.HUB) ?? "returning to HUB")); }
        catch (Exception) { }
    }
    internal static void Unavailable() => Notice("Arena is unavailable in this session.");
    private static bool InDraft => Offline && SceneMgr.IsInitialized() && SceneMgr.Get().GetMode() == SceneMgr.Mode.DRAFT;

    internal static void Initialize(Harmony harmony, ManualLogSource logger)
    {
        log = logger;
        // Home entries first: if a later patch fails, Disable runs and these already refuse instead of reaching retail's reconnect flow.
        Patch(harmony, typeof(Box), "OnArenaButtonPressed", nameof(HomeButton));
        Patch(harmony, typeof(Hub), "HandleAreanaButtonPressed", nameof(HomeArena));
        homePatched = true;
        foreach (var pair in new[] {
            ("SendArenaSessionRequest", nameof(SessionRequest)), ("RequestDraftChoicesAndContents", nameof(ContentsRequest)),
            ("DraftBegin", nameof(BeginRequest)), ("MakeDraftChoice", nameof(ChoiceRequest)),
            ("DraftRetire", nameof(RetireRequest)), ("AckDraftRewards", nameof(AckRequest)),
            ("DraftRequestDisablePremiums", nameof(PremiumsRequest)), ("RequestArenaRatingInfo", nameof(RatingRequest)),
            ("SaveArenaDeck", nameof(SaveRequest)), ("RedraftBegin", nameof(RedraftRequest)),
            ("GetArenaSessionResponse", nameof(SessionReply)), ("GetDraftChoicesAndContents", nameof(ContentsReply)),
            ("GetBeginDraft", nameof(BeginReply)), ("GetDraftChosen", nameof(ChoiceReply)),
            ("GetRetiredDraft", nameof(RetireReply)), ("GetRewardsAckDraftID", nameof(AckReply)),
            ("GetDraftRemovePremiumsResponse", nameof(PremiumsReply)), ("GetArenaRatingInfoResponse", nameof(RatingReply)),
            ("GetSaveArenaDeckResponse", nameof(SaveReply)), ("GetDraftError", nameof(ErrorReply)) })
            Patch(harmony, typeof(Network), pair.Item1, pair.Item2);
        Patch(harmony, typeof(ArenaLandingPageManager), "Awake", nameof(BeforeScene));
        harmony.Patch(Required(typeof(ArenaLandingPageManager), "Awake"), postfix: new HarmonyMethod(typeof(NativeArena), nameof(LandingModels)));
        Patch(harmony, typeof(DraftDisplay), "Awake", nameof(BeforeScene));
        Patch(harmony, typeof(ArenaLandingPageManager), "SpendTicket", nameof(Enter));
        Patch(harmony, typeof(ArenaLandingPageManager), "ToggleUndergroundState", nameof(Underground));
        Patch(harmony, typeof(ArenaLandingPageManager), "ShowArenaDisclaimer", nameof(Disclaimer));
        Patch(harmony, typeof(ArenaLandingPageManager), "CheckForFTUXPopup", nameof(NoFtue));
        Patch(harmony, typeof(ArenaLandingPageManager), "UpdateArenaRatingValues", nameof(NoRating));
        Patch(harmony, typeof(ArenaLandingPageManager), "StartMatchmaking", nameof(FindGame));
        Patch(harmony, typeof(DraftManager), "FindGame", nameof(FindGame));
        Patch(harmony, typeof(DraftDisplay), "PlayButtonPress", nameof(FindGame));
        Patch(harmony, typeof(DraftManager), "SetUndergroundState", nameof(NormalMode));
        Patch(harmony, typeof(DraftManager), "GetDraftPremium", nameof(NormalPremium));
        Patch(harmony, typeof(DraftManager), "HasAlreadySeenPhaseMessage", nameof(PhaseSeen));
        Patch(harmony, typeof(CollectionManager), "GetHeroPremium", nameof(DraftHeroPremium), typeof(TAG_CLASS));
        Patch(harmony, typeof(DraftManager), "GetNumTicketsOwned", nameof(Tickets));
        harmony.Patch(Required(typeof(CollectionDeck), "SendChanges"),
            prefix: new HarmonyMethod(typeof(NativeArena), nameof(ArenaDeckChanges)) { priority = Priority.First });
        Patch(harmony, typeof(DraftManager), "GetMaxWins", nameof(MaxWins), typeof(bool));
        Patch(harmony, typeof(DraftManager), "GetMaxWins", nameof(MaxWins), Type.EmptyTypes);
        Patch(harmony, typeof(DraftManager), "GetMaxLosses", nameof(MaxLosses), typeof(bool));
        Patch(harmony, typeof(DraftManager), "GetMaxLosses", nameof(MaxLosses), Type.EmptyTypes);
        Patch(harmony, typeof(ArenaModeToggleButton), "DetermineStartingState", nameof(ToggleStartingState));
        InstallRetailGuards(harmony);
        // These calls have no offline backend. Keep the scope to this scene and our Arena match.
        Patch(harmony, typeof(BnetPresenceMgr), "SetGameFieldBlob", nameof(NoPresence));
        Patch(harmony, typeof(PresenceMgr), "SetStatus", nameof(NoPresence));
        Patch(harmony, typeof(PresenceMgr), "SetPrevStatus", nameof(NoPresence));
        Patch(harmony, typeof(AchieveManager), "IsReady", nameof(AchievementsReady));
        Patch(harmony, typeof(TavernTicketDisplay), "OpenTavernTicketProductPage", nameof(NoStore));
        harmony.Patch(Required(typeof(DraftManager), "OnLoggedIn"), postfix: new HarmonyMethod(typeof(NativeArena), nameof(LoggedIn)));
        harmony.Patch(Required(typeof(GameMgr), "GetPostGameSceneMode"), postfix: new HarmonyMethod(typeof(NativeArena), nameof(ReturnToDraft)));
        InstallLoadWatch(harmony);
    }

    private static MethodInfo Required(Type owner, string name, Type[] args = null) =>
        AccessTools.Method(owner, name, args) ?? throw new MissingMethodException(owner.FullName, name);
    private static void Patch(Harmony harmony, Type owner, string name, string prefix, params Type[] args) =>
        harmony.Patch(Required(owner, name, args.Length == 0 && name != "GetMaxWins" && name != "GetMaxLosses" ? null : args), prefix: new HarmonyMethod(typeof(NativeArena), prefix));

    // Called from the home bootstrap: never throws. A missing DraftManager is not an error here (it is not waited for);
    // entering Arena bootstraps again and disables Arena if it is still missing.
    internal static void TryBootstrap()
    {
        try { if (disabled || DraftManager.Get() == null) return; Bootstrap(); }
        catch (Exception error) { Disable("bootstrap failed", error); }
    }
    internal static void Bootstrap()
    {
        var manager = DraftManager.Get() ?? throw new InvalidOperationException("DraftManager service missing.");
        if (!ReferenceEquals(loggedIn, manager)) manager.OnLoggedIn();
        SeedSession(manager);
    }
    private static void LoggedIn(DraftManager __instance) { loggedIn = __instance; }
    // False when Arena cannot open: the caller must not navigate (a native notice is already shown).
    internal static bool PrepareScene()
    {
        if (EntryBlocked) { Unavailable(); return false; }
        try { Bootstrap(); }
        catch (Exception error) { Disable("scene bootstrap failed", error); Unavailable(); return false; }
        return true;
    }
    private static void BeforeScene()
    {
        if (!Offline) return;
        try { Bootstrap(); }
        catch (Exception error) { Disable("scene bootstrap failed", error); }
    }
    private static void SeedSession(DraftManager manager)
    {
        manager.SetUndergroundState(false);
        manager.OnArenaSessionResponsePacket(BuildSession(Read()));
    }
    private static ArenaView Read() => ArenaServiceRegistry.Current?.View() ?? new ArenaView { EntryCost = 150 };
    private static IArenaService Service() => ArenaServiceRegistry.Current ?? throw new InvalidOperationException("Arena service unavailable.");
    private static bool HomeButton(Box __instance)
    {
        if (!NativeUIProbe.Active) return true;
        if (EntryBlocked) { Unavailable(); return false; }
        if (!NativeRewardUI.BlocksHome && !NativeAchievements.Active && !NativeProfileSelector.BlocksHome)
            __instance.FireButtonPressEvent(Box.ButtonType.ARENA);
        return false;
    }
    private static bool HomeArena()
    {
        if (!NativeUIProbe.Active) return true;
        if (EntryBlocked) { Unavailable(); return false; }
        NativeUIProbe.Navigate(SceneMgr.Mode.DRAFT);
        return false;
    }

    internal static void Update()
    {
        if (!Live) return;
        try { UpdatePresentation(); }
        catch (Exception error) { Disable("presentation failed", error); return; }
        int count = replies.Count;
        for (int i = 0; i < count; i++)
        {
            Reply reply = replies.Dequeue();
            if (!ReferenceEquals(reply.Service, ArenaServiceRegistry.Current)) { if (reply.Mutating) mutationPending = false; continue; }
            if (reply.Frame >= Time.frameCount || reply.Display && DraftDisplay.Get() == null)
            {
                if (Time.realtimeSinceStartup - reply.Born < ReplyTimeoutSeconds) replies.Enqueue(reply);
                else { if (reply.Mutating) mutationPending = false; Fail("Arena screen did not register its reply handlers.", InDraft); }
                continue;
            }
            try
            {
                delivering = reply;
                Network.Get().FakeHandleType(reply.Id);
                // FakeHandleType reports success even with no handler; a handler that took the reply read its accessor.
                if (!reply.Consumed) log.LogWarning("NATIVE_ARENA reply=" + reply.Id + " was not read by any handler");
                lastReplies.Add(reply.Id);
                if (lastReplies.Count > 20) lastReplies.RemoveAt(0);
                log.LogInfo("NATIVE_ARENA reply=" + reply.Id + " phase=" + Read().Phase);
            }
            catch (Exception error) { Fail("Arena display failed: " + error.GetBaseException().Message); }
            finally { delivering = null; if (reply.Mutating) mutationPending = false; }
        }
    }
    private static void Queue(int id, string accessor, object payload, bool display = true)
    {
        replies.Enqueue(new Reply { Id = id, Accessor = accessor, Payload = payload, Frame = Time.frameCount, Born = Time.realtimeSinceStartup,
            Service = ArenaServiceRegistry.Current, Display = display,
            Mutating = mutationPending && (id == BeginId || id == ChosenId || id == RetiredId || id == AckId) });
    }
    private static bool Result<T>(string accessor, ref T result)
    {
        if (!Offline || delivering?.Accessor != accessor) return true;
        result = (T)delivering.Payload;
        delivering.Consumed = true;
        return false;
    }
    private static bool SessionReply(ref ArenaSessionResponse __result) => Result("GetArenaSessionResponse", ref __result);
    private static bool ContentsReply(ref Network.DraftChoicesAndContents __result) => Result("GetDraftChoicesAndContents", ref __result);
    private static bool BeginReply(ref Network.BeginDraft __result) => Result("GetBeginDraft", ref __result);
    private static bool ChoiceReply(ref Network.DraftChosen __result) => Result("GetDraftChosen", ref __result);
    private static bool RetireReply(ref Network.DraftRetired __result) => Result("GetRetiredDraft", ref __result);
    private static bool AckReply(ref long __result) => Result("GetRewardsAckDraftID", ref __result);
    private static bool PremiumsReply(ref Network.DraftChoicesAndContents __result) => Result("GetDraftRemovePremiumsResponse", ref __result);
    private static bool RatingReply(ref ArenaRatingInfoResponse __result) => Result("GetArenaRatingInfoResponse", ref __result);
    private static bool SaveReply(ref SaveArenaDeckResponse __result) => Result("GetSaveArenaDeckResponse", ref __result);
    private static bool ErrorReply(ref DraftError __result) => Result("GetDraftError", ref __result);

    // Informational: explains a feature that is not there. Never an error.
    private static void Notice(string message, bool popup = true)
    {
        lastNotice = message;
        Show(message, popup);
    }
    // A failed action: sticky error plus a sequence number.
    private static void Fail(string message, bool popup = true)
    {
        lastError = message; errorSeq++;
        Show(message, popup);
    }
    private static void Show(string message, bool popup)
    {
        log?.LogWarning("NATIVE_ARENA " + message);
        if (popup) DialogManager.Get()?.ShowPopup(new AlertPopup.PopupInfo { m_id = "solostone.arena.notice", m_headerText = "Arena",
            m_text = message, m_responseDisplay = AlertPopup.ResponseDisplay.OK });
    }
    private static void Refuse(string message)
    {
        Fail(message);
        // DE_NO_LICENSE only logs a warning in DraftManager.OnError; every other code logs an error or changes the screen.
        Queue(ErrorId, "GetDraftError", new DraftError { ErrorCode_ = DraftError.ErrorCode.DE_NO_LICENSE });
        QueueContents(Read());
    }
    private static ArenaView Mutate(Func<IArenaService, ArenaView> action)
    {
        if (mutationPending) return null;
        try
        {
            ArenaView view = action(Service());
            if (view == null || !string.IsNullOrEmpty(view.Error)) { Refuse(view?.Error ?? "Arena returned no state."); return null; }
            lastError = null;
            mutationPending = true;
            return view;
        }
        catch (Exception error) { Refuse(error.GetBaseException().Message); return null; }
    }
    private static bool SessionRequest()
    {
        if (!Offline) return true;
        if (disabled) return Inert(false);
        Queue(SessionResponseId, "GetArenaSessionResponse", BuildSession(Read()), false);
        return false;
    }
    private static void QueueContents(ArenaView view)
    {
        if (view.Phase == ArenaPhase.None)
            Queue(ErrorId, "GetDraftError", new DraftError { ErrorCode_ = DraftError.ErrorCode.DE_NOT_IN_DRAFT });
        else Queue(ContentsId, "GetDraftChoicesAndContents", BuildContents(view));
    }
    private static bool ContentsRequest(bool __0)
    {
        if (!Offline) return true;
        if (disabled) return Inert(false);
        if (__0) { Notice(UndergroundNotice); return false; }
        QueueContents(Read());
        return false;
    }
    private static ArenaView BeginRun()
    {
        var before = Read();
        if (mutationPending) return null;
        if (before.Phase != ArenaPhase.None) { Refuse("Finish the current Arena run first."); return null; }
        ArenaView view = Mutate(service => service.Begin(Guid.NewGuid().ToString("N")));
        if (view == null) return null;
        var begin = new Network.BeginDraft { DeckID = DeckIdFor(view), Wins = view.Wins, MaxSlot = view.MaxSlot,
            SlotType = DraftSlotType.DRAFT_SLOT_HERO, UniqueSlotTypesForDraft = SlotTypes(), Session = BuildRunSession(view) };
        begin.Heroes.AddRange(view.HeroOffers.Select(Card));
        Queue(BeginId, "GetBeginDraft", begin);
        return view;
    }
    private static bool BeginRequest(bool __0)
    {
        if (!Offline) return true;
        if (disabled) return Inert();
        if (__0) Notice(UndergroundNotice); else BeginRun();
        return false;
    }
    private static bool Enter(ArenaLandingPageManager __instance)
    {
        if (!Offline) return true;
        if (disabled) return Inert();
        if (BeginRun() != null)
        {
            __instance.GetWidget().TriggerEvent("MOVETO_DRAFTING");
            __instance.GetWidget().TriggerEvent("DRAFTING");
        }
        return false;
    }
    private static bool ChoiceRequest(long __0, int __1, int __2, int __3, bool __4, bool __5, List<int> __6)
    {
        if (!Offline) return true;
        if (disabled) return Inert();
        if (mutationPending) return false;
        ArenaView before = Read();
        var offers = before.Phase == ArenaPhase.ChoosingHero ? before.HeroOffers : before.Offers;
        if (__0 != DeckId || __4 || __5 || __1 != NativeSlot(before) || __2 < 1 || __2 > offers.Count)
        { Refuse("Arena choice is stale. Try the current offers."); return false; }
        string chosen = offers[__2 - 1];
        ArenaView view = Mutate(service => service.Choose(before.Slot, __2 - 1));
        if (view != null)
            Queue(ChosenId, "GetDraftChosen", new Network.DraftChosen { ChosenCard = new Network.CardChoice { CardDef = Card(chosen) },
                SlotType = SlotType(view), NextChoices = Choices(view) });
        return false;
    }
    private static bool RetireRequest(long __0, int __1, int __2, bool __3)
    {
        if (!Offline) return true;
        if (disabled) return Inert();
        if (__0 != DeckId || __3) { Refuse("Arena run is stale."); return false; }
        ArenaView view = Mutate(service => service.Retire());
        if (view != null) Queue(RetiredId, "GetRetiredDraft", new Network.DraftRetired { Deck = DeckId, Chest = Chest(view.Reward) });
        return false;
    }
    private static bool AckRequest(long __0, int __1, bool __2)
    {
        if (!Offline) return true;
        if (disabled) return Inert();
        if (__0 != DeckId || __2) { Refuse("Arena reward is stale."); return false; }
        ArenaView before = Read();
        ArenaView view = Mutate(service => service.AcknowledgeReward(before.RunId));
        if (view != null) Queue(AckId, "GetRewardsAckDraftID", DeckIdFor(before), false);
        return false;
    }
    private static bool PremiumsRequest()
    {
        if (!Offline) return true;
        if (disabled) return Inert(false);
        Queue(PremiumsId, "GetDraftRemovePremiumsResponse", BuildContents(Read()));
        return false;
    }
    private static bool RatingRequest()
    {
        if (!Offline) return true;
        if (disabled) return Inert(false);
        var view = Read();
        Queue(RatingId, "GetArenaRatingInfoResponse", new ArenaRatingInfoResponse { PlayerInfo = new ArenaPlayerInfo {
            NormalWins = view.Wins, NormalLosses = view.Losses, NormalMaxWins = view.MaxWins, NormalMaxLosses = view.MaxLosses,
            UndergroundMaxWins = view.MaxWins, UndergroundMaxLosses = view.MaxLosses } }, false);
        return false;
    }
    private static bool ArenaDeckChanges(CollectionDeck __instance, CollectionDeck.ChangeSource __0)
    {
        if (!Offline || __instance.Type != DeckType.DRAFT_DECK) return true;
        if (disabled) return Inert(false);
        // Arena autosaves never enter the constructed editor, its ownership validation, or the login-gated sender.
        if (__0 == CollectionDeck.ChangeSource.Arena)
            SaveRequest(__instance.ID, null, 0, 0, null, false);
        return false;
    }
    private static bool SaveRequest(long __0, List<Network.CardUserData> __1, long __2, int __3, RuneType[] __4, bool __5)
    {
        if (!Offline) return true;
        if (disabled) return Inert(false);
        // MVP decks are immutable: answer with the authoritative deck, never persist an editor's reconstruction.
        if (__0 != DeckId || __5) { Refuse("Arena deck is stale."); return false; }
        var deck = new PegasusUtil.DeckContents { DeckId = DeckId };
        foreach (var group in Read().Deck.GroupBy(id => id))
            deck.Cards.Add(new DeckCardData { Def = new PegasusShared.CardDef { Asset = GameUtils.TranslateCardIdToDbId(group.Key), Premium = 0 }, Qty = group.Count() });
        Queue(SaveId, "GetSaveArenaDeckResponse", new SaveArenaDeckResponse { Decks = deck });
        return false;
    }
    private static bool RedraftRequest() { if (!Offline) return true; if (disabled) return Inert(); Refuse("Redraft is unavailable in this Arena mode."); return false; }

    private static bool FindGame()
    {
        if (!Offline) return true;
        if (disabled) return Inert();
        if (startingMatch || mutationPending || DraftDisplay.Get()?.IsRewardsFlowPending == true) return false;
        startingMatch = true;
        try
        {
            if (!Service().StartMatch(out string error)) Notice(error ?? "Arena match could not start.");
        }
        catch (Exception error) { Fail(error.GetBaseException().Message); }
        finally { startingMatch = false; }
        return false;
    }
    // Keep GT_VS_AI transport/presentation. Only post-game routing changes; retail's OnGameOver must not count a second result.
    internal static SceneMgr.Mode NavigationTarget(SceneMgr.Mode mode) => Live && MatchActive && mode == SceneMgr.Mode.TOURNAMENT ? SceneMgr.Mode.DRAFT : mode;
    private static void ReturnToDraft(ref SceneMgr.Mode __result) { if (Live && MatchActive && !EntryBlocked) __result = SceneMgr.Mode.DRAFT; }
    private static bool Underground() { if (!Offline) return true; if (disabled) return Inert(); Notice(UndergroundNotice); return false; }
    private static bool Disclaimer() { if (!Offline) return true; if (disabled) return Inert(); Notice(RulesText(Read())); return false; }
    internal const string UndergroundNotice = "This Arena mode is coming in a later SoloStone update.";
    // The "?" button: our own rules, not retail's first-visit slides (5 wins, other players).
    internal static string RulesText(ArenaView view) =>
        "Draft a deck of " + 30 + " cards, then play NPC opponents who drafted decks of their own. "
        + "Your run ends at " + view.MaxWins + " wins or " + view.MaxLosses + " losses. "
        + "Entry costs " + view.EntryCost + " gold. Rewards grow with every win. "
        + "You can retire at any time and keep the reward you have earned so far.";
    internal static string GoldCostText(int cost) => cost + " Gold";
    private static void NormalMode(ref bool __0) { if (Offline) __0 = false; }
    // The phase header still shows; only the one-time phase popup (first visit) is treated as already seen, like the tutorial slides.
    private static bool PhaseSeen(ref bool __result) { if (!Offline) return true; __result = true; return false; }
    private static bool NormalPremium(ref TAG_PREMIUM __result) { if (!Offline) return true; __result = TAG_PREMIUM.NORMAL; return false; }
    private static bool DraftHeroPremium(ref TAG_PREMIUM __result) { if (!InDraft) return true; __result = TAG_PREMIUM.NORMAL; return false; }
    private static bool Tickets(ref int __result) { if (!InDraft) return true; __result = 1; return false; }
    private static bool MaxWins(ref int __result) { if (!Offline) return true; __result = Read().MaxWins; return false; }
    private static bool MaxLosses(ref int __result) { if (!Offline) return true; __result = Read().MaxLosses; return false; }
    private static bool NoPresence() => !Offline || !InDraft && !MatchActive;
    private static bool AchievementsReady(ref bool __result) { if (!InDraft) return true; __result = true; return false; }
    private static bool NoStore() { if (!InDraft) return true; Notice("Entry costs " + Read().EntryCost + " gold. Use the play button."); return false; }
    private static bool ToggleStartingState(ArenaModeToggleButton __instance)
    {
        if (!Offline) return true;
        __instance.GetComponent<Hearthstone.UI.Widget>()?.TriggerEvent("START_DEFAULT");
        return false;
    }

    internal static ArenaSessionResponse BuildSession(ArenaView view) => new ArenaSessionResponse {
        ErrorCode = ErrorCode.ERROR_OK, CurrentSeason = Season(view), Session = BuildRunSession(view),
        UndergroundSession = new ArenaSession { State = ArenaSessionState.NO_RUN, IsActive = false, IsUnderground = true, Info = Season(view) } };
    // Retail's season carries the reward levels the landing rewards list and chest icons bind to (ArenaLandingPageManager
    // .PopulateArenaRewardsDataModel). Offline: one level per win count from the service's own reward table; Underground none.
    private static ArenaSeasonInfo Season(ArenaView view)
    {
        var season = new ArenaSeasonInfo { CurrentSeasonDisplayId = 1, IsDualClass = false,
            Season = new ArenaSeasonSpec { GameContentSeason = new GameContentSeasonSpec { SeasonId = 1, EndSecondsFromNow = uint.MaxValue } } };
        foreach (var reward in view.RewardTable) season.NormalRewards.Add(RewardLevel(reward, view));
        return season;
    }
    private static ArenaRewardLevel RewardLevel(ArenaReward reward, ArenaView view)
    {
        // A run ends at MaxWins (any losses below the cap) or at MaxLosses: one row per win count, as the run can end there.
        var level = new ArenaRewardLevel { Wins = reward.Wins, Losses = reward.Wins == view.MaxWins ? view.MaxLosses - 1 : view.MaxLosses };
        if (reward.Gold > 0) level.Rewards.Add(Currency(ArenaRewardCurrencyType.GOLD, reward.Gold));
        if (reward.Dust > 0) level.Rewards.Add(Currency(ArenaRewardCurrencyType.DUST, reward.Dust));
        foreach (var pack in reward.PackTypes.GroupBy(type => type))
        {
            var slot = new ArenaRewardSlot();
            slot.SpecificBoosterRewards.Add(new ArenaRewardSpecificBoosterSlot { BoosterType = FairPackRules.NativeBoosterId(pack.Key),
                MinBoosterCount = pack.Count(), MaxBoosterCount = pack.Count() });
            level.Rewards.Add(slot);
        }
        return level;
    }
    private static ArenaRewardSlot Currency(ArenaRewardCurrencyType type, int amount)
    {
        var slot = new ArenaRewardSlot();
        slot.CurrencyRewards.Add(new ArenaRewardCurrencySlot { CurrencyType = type, MinAmount = amount, MaxAmount = amount });
        return slot;
    }
    private static ArenaSession BuildRunSession(ArenaView view) => new ArenaSession { Wins = view.Wins, Losses = view.Losses,
        IsActive = view.Phase != ArenaPhase.None, IsInRewards = view.Phase == ArenaPhase.RewardPending, IsUnderground = false,
        State = view.Phase == ArenaPhase.None ? ArenaSessionState.NO_RUN : view.Phase == ArenaPhase.Ready ? ArenaSessionState.MIDRUN
            : view.Phase == ArenaPhase.RewardPending ? ArenaSessionState.REWARDS : ArenaSessionState.DRAFTING, Info = Season(view) };
    private static DraftSlotType SlotType(ArenaView view) => view.Phase == ArenaPhase.ChoosingHero ? DraftSlotType.DRAFT_SLOT_HERO : DraftSlotType.DRAFT_SLOT_CARD;
    private static List<DraftSlotType> SlotTypes() => new List<DraftSlotType> { DraftSlotType.DRAFT_SLOT_HERO, DraftSlotType.DRAFT_SLOT_CARD };
    private static NetCache.CardDefinition Card(string id) => new NetCache.CardDefinition { Name = id ?? string.Empty, Premium = TAG_PREMIUM.NORMAL };
    private static List<Network.CardChoice> Choices(ArenaView view) => (view.Phase == ArenaPhase.ChoosingHero ? view.HeroOffers : view.Offers)
        .Select(id => new Network.CardChoice { CardDef = Card(id) }).ToList();
    internal static Network.DraftChoicesAndContents BuildContents(ArenaView view)
    {
        var contents = new Network.DraftChoicesAndContents { Slot = NativeSlot(view), RedraftSlot = -1, MaxSlot = view.MaxSlot,
            MaxWins = view.MaxWins, MaxLosses = view.MaxLosses, Wins = view.Wins, Losses = view.Losses,
            SlotType = SlotType(view), Session = BuildRunSession(view), Chest = Chest(view.Reward) };
        contents.DeckInfo.Deck = DeckIdFor(view);
        contents.Hero.Name = view.HeroCardId ?? string.Empty;
        contents.HeroPower.Name = string.IsNullOrEmpty(view.HeroCardId) ? string.Empty : GameUtils.GetHeroPowerCardIdFromHero(view.HeroCardId);
        contents.Choices.AddRange(Choices(view));
        contents.UniqueSlotTypesForDraft.AddRange(SlotTypes());
        foreach (var group in view.Deck.GroupBy(id => id)) contents.DeckInfo.Cards.Add(new Network.CardUserData {
            DbId = GameUtils.TranslateCardIdToDbId(group.Key), Count = group.Count(), Premium = TAG_PREMIUM.NORMAL });
        return contents;
    }
    private static Network.RewardChest Chest(ArenaReward reward)
    {
        if (reward == null) return null;
        var chest = new Network.RewardChest();
        if (reward.Gold > 0) chest.Rewards.Add(new GoldRewardData(reward.Gold));
        if (reward.Dust > 0) chest.Rewards.Add(new ArcaneDustRewardData(reward.Dust));
        foreach (var pack in reward.PackTypes.GroupBy(type => type)) chest.Rewards.Add(new BoosterPackRewardData(FairPackRules.NativeBoosterId(pack.Key), pack.Count()));
        return chest;
    }
}
