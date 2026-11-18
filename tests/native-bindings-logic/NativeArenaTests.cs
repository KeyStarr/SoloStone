namespace NativeArenaBoundary
{
using PegasusShared;
using PegasusUtil;

internal static partial class NativeArena
{
    internal static void RunTests()
    {
        log = new ManualLogSource();
        var stub = new ArenaStubService(_ => new[] { "CARD_A", "CARD_B", "CARD_C" });
        ArenaServiceRegistry.Current = stub;
        NativeUIProbe.Active = false;
        Check(SessionRequest() && BeginRequest(false) && ContentsRequest(false) && RedraftRequest(), "inactive sends keep retail behavior");
        Check(stub.View().Phase == ArenaPhase.None, "inactive transport leaves service unchanged");
        NativeUIProbe.Active = true;
        var received = new List<(int Id, object Payload)>();
        Network.Receive = id => {
            object value = null;
            switch (id)
            {
                case 351: ArenaSessionResponse session = null; Check(!SessionReply(ref session), "session accessor"); value = session; break;
                case 246: Network.BeginDraft begin = null; Check(!BeginReply(ref begin), "begin accessor"); value = begin; break;
                case 248: Network.DraftChoicesAndContents contents = null; Check(!ContentsReply(ref contents), "contents accessor"); value = contents; break;
                case 249: Network.DraftChosen chosen = null; Check(!ChoiceReply(ref chosen), "chosen accessor"); value = chosen; break;
                case 247: Network.DraftRetired retired = null; Check(!RetireReply(ref retired), "retire accessor"); value = retired; break;
                case 288: long deck = 0; Check(!AckReply(ref deck), "ack accessor"); value = deck; break;
                case 355: Network.DraftChoicesAndContents premiums = null; Check(!PremiumsReply(ref premiums), "premium accessor"); value = premiums; break;
                case 719: ArenaRatingInfoResponse rating = null; Check(!RatingReply(ref rating), "rating accessor"); value = rating; break;
                case 716: SaveArenaDeckResponse save = null; Check(!SaveReply(ref save), "save accessor"); value = save; break;
                case 251: DraftError error = null; Check(!ErrorReply(ref error), "error accessor"); value = error; break;
                default: throw new Exception("unexpected reply " + id);
            }
            received.Add((id, value));
        };
        void Tick() { Time.frameCount++; Update(); }
        ContentsRequest(false); Update();
        Check(received.Count == 0, "same-frame replies must wait"); Tick();
        Check(((DraftError)received.Last().Payload).ErrorCode_ == DraftError.ErrorCode.DE_NOT_IN_DRAFT, "empty run opens landing, not active deck");
        SessionRequest(); Tick();
        var sessionReply = (ArenaSessionResponse)received.Last().Payload;
        Check(sessionReply.CurrentSeason.Season.GameContentSeason.EndSecondsFromNow > 0
            && sessionReply.Session.State == ArenaSessionState.NO_RUN && !sessionReply.UndergroundSession.IsActive, "season exists before scene and only one playable mode");
        var levels = sessionReply.CurrentSeason.NormalRewards;
        Check(levels.Count == 13 && levels.Select(level => level.Wins).SequenceEqual(Enumerable.Range(0, 13))
            && levels[12].Losses == 2 && levels[3].Losses == 3
            && levels[12].Rewards.SelectMany(slot => slot.CurrencyRewards).Single(c => c.CurrencyType == ArenaRewardCurrencyType.GOLD).MinAmount == 900
            && levels[12].Rewards.SelectMany(slot => slot.SpecificBoosterRewards).Single().MinBoosterCount == 5
            && levels[0].Rewards.SelectMany(slot => slot.CurrencyRewards).Count() == 0,
            "season carries one reward level per win count from the service table (retail rewards list data)");
        // Queue a session before Begin: its delivery must not release the mutation lock for the pending Begin.
        SessionRequest(); BeginRequest(false);
        Check(stub.View().GoldBalance == 850 && stub.View().HeroOffers.Count == 3 && mutationPending, "Begin pays once through service");
        BeginRequest(false); Check(replies.Count == 2, "double-click cannot begin or enqueue twice");
        DraftDisplay.Current = null; Tick();
        Check(mutationPending && replies.Count == 1, "session reply cannot unlock pending display mutation");
        DraftDisplay.Current = new DraftDisplay(); Tick();
        var beginReply = (Network.BeginDraft)received.Last().Payload;
        Check(beginReply.DeckID == DeckId && beginReply.MaxSlot == 30 && beginReply.Heroes.Select(card => card.Name)
            .SequenceEqual(new[] { "HERO_01", "HERO_08", "HERO_06" }), "native hero offers and deck id");
        var detached = stub.View(); detached.HeroOffers.Clear(); Check(stub.View().HeroOffers.Count == 3, "stub views are detached");
        ChoiceRequest(DeckId, 0, 2, 3, false, false, new List<int>()); Tick();
        var heroReply = (Network.DraftChosen)received.Last().Payload;
        Check(stub.View().HeroCardId == "HERO_08" && stub.View().Slot == 1 && heroReply.ChosenCard.CardDef.Premium == TAG_PREMIUM.NORMAL
            && heroReply.NextChoices.Count == 3 && heroReply.SlotType == DraftSlotType.DRAFT_SLOT_CARD, "one-based native hero index maps to zero-based service and normal premium");
        ChoiceRequest(DeckId, 0, 1, 0, false, false, new List<int>()); Tick();
        Check(stub.View().Deck.Count == 0 && lastError != null, "stale slot changes nothing and refreshes current offers");
        for (int i = 0; i < 30; i++)
        {
            ChoiceRequest(DeckId, stub.View().Slot, 1, 1, false, false, new List<int>());
            ChoiceRequest(DeckId, stub.View().Slot, 1, 1, false, false, new List<int>()); // pending duplicate
            Tick();
        }
        Check(stub.View().Phase == ArenaPhase.Ready && stub.View().Slot == 30 && stub.View().Deck.Count == 30, "hero plus exactly thirty card picks; Ready slot matches ArenaService");
        var translated = stub.View(); translated.Slot = 999;
        Check(BuildContents(translated).Slot == 31, "native slots do not depend on service slot numbering");
        long firstDeckId = DeckId;
        Check(firstDeckId != DeckIdFor(new ArenaView { RunId = "another-run" }), "deck ids distinguish successive runs");
        ContentsRequest(false); Tick();
        var ready = (Network.DraftChoicesAndContents)received.Last().Payload;
        Check(ready.Slot == 31 && ready.MaxSlot == 30 && ready.MaxWins == 12 && ready.MaxLosses == 3 && ready.Choices.Count == 0
            && ready.DeckInfo.Cards.Sum(card => card.Count) == 30 && ready.DeckInfo.Cards.Max(card => card.Count) == 30
            && ready.DeckInfo.Cards.All(card => card.Premium == TAG_PREMIUM.NORMAL), "ready deck keeps duplicates beyond constructed limits");
        RatingRequest(); Tick();
        var info = ((ArenaRatingInfoResponse)received.Last().Payload).PlayerInfo;
        Check(info.NormalMaxWins == 12 && info.NormalMaxLosses == 3 && info.Rating == 0, "record reply has no rating");
        PremiumsRequest(); Tick(); Check(received.Last().Id == 355, "disable premiums returns authoritative normal deck");
        SaveRequest(DeckId, new List<Network.CardUserData>(), 0, 0, Array.Empty<RuneType>(), false); Tick();
        Check(((SaveArenaDeckResponse)received.Last().Payload).Decks.Cards.Sum(card => card.Qty) == 30 && stub.View().Deck.Count == 30,
            "save cannot replace authoritative drafted deck with editor input");
        Check(ArenaDeckChanges(new CollectionDeck { Type = DeckType.NORMAL_DECK }, CollectionDeck.ChangeSource.Arena), "constructed saves retain existing path");
        Check(!ArenaDeckChanges(new CollectionDeck { ID = DeckId, Type = DeckType.DRAFT_DECK }, CollectionDeck.ChangeSource.Arena), "Arena editor save bypasses constructed validation and login gate");
        Tick(); Check(received.Last().Id == 716, "Arena editor save gets authoritative reply");
        RedraftRequest(); Tick(); Check(stub.View().Phase == ArenaPhase.Ready, "redraft refused");
        Underground(); Check(DialogManager.Last.m_text.Contains("coming in a later SoloStone update") && stub.View().Phase == ArenaPhase.Ready, "Underground explains and never switches");
        bool underground = true; NormalMode(ref underground); Check(!underground, "retail preference cannot enable Underground");
        int errorsBefore = errorSeq; FindGame();
        Check(lastNotice == "stub" && errorSeq == errorsBefore && !MatchActive, "stub never launches a match; the notice is not an error");
        RetireRequest(DeckId, 31, 1, false); Tick();
        var chest = ((Network.DraftRetired)received.Last().Payload).Chest;
        Check(chest.Rewards.OfType<GoldRewardData>().Single().Amount == 100 && chest.Rewards.OfType<ArcaneDustRewardData>().Single().Amount == 10
            && chest.Rewards.OfType<BoosterPackRewardData>().Single().Count == 1, "native chest uses service reward");
        Check(stub.View().GoldBalance == 950 && stub.Retire().GoldBalance == 950, "stub reward is paid before presentation and repeated retire cannot pay twice");
        AckRequest(DeckId, 31, false); Tick(); Check(stub.View().Phase == ArenaPhase.None && received.Last().Id == 288 && stub.View().GoldBalance == 950,
            "claim acknowledges service reward without paying again");
        // Old-profile replies cannot arrive in a newly bound service, even when the slot numbers happen to match.
        ContentsRequest(false); ArenaServiceRegistry.Current = new ArenaStubService(_ => new[] { "A", "B", "C" });
        int delivered = received.Count; Tick(); Check(received.Count == delivered, "replies are scoped to service identity");
        Network.BeginDraft outside = null; Check(BeginReply(ref outside), "stash is available only during dispatch");
        ArenaMatchOwned = true; var mode = SceneMgr.Mode.TOURNAMENT; ReturnToDraft(ref mode); Check(mode == SceneMgr.Mode.DRAFT, "Arena returns to draft with VS_AI transport");
        Check(NavigationTarget(SceneMgr.Mode.TOURNAMENT) == SceneMgr.Mode.DRAFT && NavigationTarget(SceneMgr.Mode.HUB) == SceneMgr.Mode.HUB,
            "post-reward picker navigation returns to Arena without changing other destinations");
        ArenaMatchOwned = false; mode = SceneMgr.Mode.TOURNAMENT; ReturnToDraft(ref mode); Check(mode == SceneMgr.Mode.TOURNAMENT, "other modes preserve route");
        // Informational notices never touch the error state; failures bump errorSeq.
        errorsBefore = errorSeq; Underground(); Disclaimer(); NoStore();
        Check(errorSeq == errorsBefore && lastNotice != null, "notices are not errors");
        Underground(); Check(lastNotice == "This Arena mode is coming in a later SoloStone update.", "Underground notice text");
        Disclaimer(); string rules = DialogManager.Last.m_text;
        Check(rules.Contains("12 wins or 3 losses") && rules.Contains("NPC opponents") && rules.Contains("150 gold") && rules.Contains("Rewards grow")
            && rules.Contains("retire at any time") && !rules.Contains("Underground") && GoldCostText(150) == "150 Gold", "rules popup text");
        ChoiceRequest(DeckId, 7, 1, 1, false, false, new List<int>()); Check(errorSeq == errorsBefore + 1, "a refusal bumps errorSeq");
        // A disabled Arena stays armed (a DRAFT scene still open must never reach retail code) and answers with a notice; it leaves DRAFT at once.
        int leftBefore = NativeUIProbe.Left; int queued = replies.Count;
        Disable("test");
        Check(NativeUIProbe.Left == leftBefore + 1, "disabling inside DRAFT leaves the scene");
        Check(Offline && !Live && !SessionRequest() && !BeginRequest(false) && !ContentsRequest(false) && !RedraftRequest() && !FindGame()
            && !ChoiceRequest(DeckId, 0, 1, 1, false, false, new List<int>()) && !RetireRequest(DeckId, 0, 0, false) && !AckRequest(DeckId, 0, false)
            && !PremiumsRequest() && !RatingRequest() && !SaveRequest(DeckId, null, 0, 0, null, false) && !Underground() && !Disclaimer(),
            "disabled Arena refuses every send instead of falling through to retail");
        Check(replies.Count == queued && !mutationPending, "disabled Arena queues and changes nothing");
        int tickets = 0; Check(!Tickets(ref tickets) && tickets == 1, "tickets are answered inside the DRAFT scene");
        errorsBefore = errorSeq; Unavailable();
        Check(errorSeq == errorsBefore && DialogManager.Last.m_text.Contains("unavailable"), "unavailable is a notice");
        disabled = false;
        ArenaServiceRegistry.Current = null; NativeUIProbe.Active = false;
        Console.WriteLine("PASS Arena transport: deferred/scoped replies, ten send paths, 30 picks, no copy cap, errors, chest, claim, return");
    }
}

}
