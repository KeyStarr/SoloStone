namespace NativeArenaBoundary
{
using PegasusShared;
using PegasusUtil;

// The production Arena transport (plugin/arena/NativeArena.cs, extracted) driven over the REAL ArenaService and a temp LocalProfile.
// The stub-service run lives in native-bindings-logic; this one proves the contract numbering and the money/state rules end to end.
internal static partial class NativeArena
{
    internal static void RunRealServiceTests(string root)
    {
        log = new ManualLogSource();
        string path = Path.Combine(root, "arena-transport.xml");
        using var profile = new LocalProfile(path);
        profile.CommitChange(next => { next.GoldBalance = 1000; return true; });
        bool opponentsAvailable = false;
        var service = new ArenaService(profile, () => opponentsAvailable);
        ArenaServiceRegistry.Current = service;
        NativeUIProbe.Active = true; disabled = false;
        var received = new List<(int Id, object Payload)>();
        Network.Receive = id => {
            object value = null;
            switch (id)
            {
                case 246: Network.BeginDraft begin = null; Check(!BeginReply(ref begin), "begin accessor"); value = begin; break;
                case 248: Network.DraftChoicesAndContents contents = null; Check(!ContentsReply(ref contents), "contents accessor"); value = contents; break;
                case 249: Network.DraftChosen chosen = null; Check(!ChoiceReply(ref chosen), "chosen accessor"); value = chosen; break;
                case 247: Network.DraftRetired retired = null; Check(!RetireReply(ref retired), "retire accessor"); value = retired; break;
                case 288: long deck = 0; Check(!AckReply(ref deck), "ack accessor"); value = deck; break;
                case 251: DraftError error = null; Check(!ErrorReply(ref error), "error accessor"); value = error; break;
                default: throw new Exception("unexpected reply " + id);
            }
            received.Add((id, value));
        };
        void Tick() { Time.frameCount++; Update(); }
        // Season reward levels come from the real rules table at the save's pace (what the native rewards list shows).
        var table = service.View().RewardTable;
        var levels = BuildSession(service.View()).CurrentSeason.NormalRewards;
        Check(table.Count == 13 && levels.Count == 13 && levels[12].Rewards.SelectMany(slot => slot.SpecificBoosterRewards).Single().MinBoosterCount == 5
            && levels[12].Rewards.SelectMany(slot => slot.CurrencyRewards).Single(c => c.CurrencyType == ArenaRewardCurrencyType.GOLD).MinAmount == table[12].Gold
            && table[12].Gold > 0 && table[0].Gold == 0 && table[0].PackTypes.Count == 1, "real season reward levels follow the rules table");
        void Refused(Action send, string message)
        {
            int errors = errorSeq, count = received.Count; var before = service.View();
            send(); Tick();
            Check(errorSeq == errors + 1 && lastError != null, message + ": counted as an error");
            Check(received.Skip(count).All(row => row.Id == 251 || row.Id == 248), message + ": only error/contents replies");
            var after = service.View();
            Check(after.Phase == before.Phase && after.Slot == before.Slot && after.Deck.Count == before.Deck.Count && after.GoldBalance == before.GoldBalance, message + ": state unchanged");
        }

        Refused(() => RetireRequest(0, 0, 0, false), "retire without a run");
        var original = File.ReadAllBytes(path);
        Refused(() => BeginRequest(false), "entry with no Arena opponents");
        Check(lastError == "Arena opponents are unavailable." && profile.Gold == 1000
            && File.ReadAllBytes(path).SequenceEqual(original), "unavailable opponents leave the save and wallet unchanged");
        opponentsAvailable = true;
        BeginRequest(false); Tick();
        var begin = (Network.BeginDraft)received.Last(row => row.Id == 246).Payload;
        var view = service.View();
        Check(view.Phase == ArenaPhase.ChoosingHero && view.Slot == 0 && profile.Gold == 850 && begin.DeckID == DeckId && DeckId != 0
            && begin.MaxSlot == view.MaxSlot && begin.Heroes.Select(card => card.Name).SequenceEqual(view.HeroOffers), "begin pays once, hero offers come from the service");
        int begins = received.Count(row => row.Id == 246);
        Refused(() => BeginRequest(false), "begin during a run");
        Check(profile.Gold == 850 && received.Count(row => row.Id == 246) == begins, "refused begin charges nothing");
        Refused(() => ChoiceRequest(DeckId, 5, 1, 1, false, false, new List<int>()), "stale hero slot");
        Refused(() => ChoiceRequest(DeckId, 0, 4, 1, false, false, new List<int>()), "hero index out of range");

        string hero = view.HeroOffers[1];
        ChoiceRequest(DeckId, NativeSlot(view), 2, 1, false, false, new List<int>());
        ChoiceRequest(DeckId, NativeSlot(view), 2, 1, false, false, new List<int>()); // double click while pending
        Tick();
        view = service.View();
        Check(view.Phase == ArenaPhase.Drafting && view.HeroCardId == hero && view.Slot == 1 && view.Offers.Count == 3, "hero choice maps to the service slot numbering");
        var drafted = new List<string>();
        for (int i = 0; i < 30; i++)
        {
            view = service.View();
            Check(view.Phase == ArenaPhase.Drafting && view.Slot == i + 1 && NativeSlot(view) == i + 1, "slot " + (i + 1) + " numbering");
            drafted.Add(view.Offers[i % 3]);
            ChoiceRequest(DeckId, NativeSlot(view), i % 3 + 1, 1, false, false, new List<int>()); Tick();
            var reply = (Network.DraftChosen)received.Last(row => row.Id == 249).Payload;
            Check(reply.ChosenCard.CardDef.Name == drafted.Last() && reply.NextChoices.Count == (i == 29 ? 0 : 3), "pick " + (i + 1) + " reply");
        }
        view = service.View();
        Check(view.Phase == ArenaPhase.Ready && view.Slot == 30 && view.Deck.SequenceEqual(drafted), "ready after hero plus thirty picks, Slot == 30");
        Refused(() => ChoiceRequest(DeckId, NativeSlot(view), 1, 1, false, false, new List<int>()), "pick after the deck is complete");
        opponentsAvailable = false;
        ContentsRequest(false); Tick();
        var ready = (Network.DraftChoicesAndContents)received.Last().Payload;
        Check(ready.Slot == 31 && ready.MaxSlot == view.MaxSlot && ready.MaxWins == view.MaxWins && ready.MaxLosses == view.MaxLosses
            && ready.DeckInfo.Cards.Sum(card => card.Count) == 30 && ready.Hero.Name == hero, "ready contents come from the service view");

        int gold = profile.Gold;
        Refused(() => RetireRequest(DeckId + 1, 31, 1, false), "retire for another run");
        RetireRequest(DeckId, 31, 1, false); Tick();
        var chest = ((Network.DraftRetired)received.Last(row => row.Id == 247).Payload).Chest;
        view = service.View();
        Check(view.Phase == ArenaPhase.RewardPending && view.Reward != null && profile.Gold == gold + view.Reward.Gold
            && chest.Rewards.OfType<GoldRewardData>().Sum(row => row.Amount) == view.Reward.Gold, "retire pays once and the chest shows the paid reward");
        gold = profile.Gold; long dust = profile.DustBalance;
        RetireRequest(DeckId, 31, 1, false); Tick();
        Check(profile.Gold == gold && profile.DustBalance == dust, "repeated retire cannot pay twice");
        Refused(() => BeginRequest(false), "begin while the reward is unclaimed");
        AckRequest(DeckId, 31, false); Tick();
        Check(service.View().Phase == ArenaPhase.None && profile.Gold == gold && received.Last().Id == 288, "ack closes the run without paying again");
        Refused(() => AckRequest(DeckId, 31, false), "ack with no run");
        Check(DeckId == 0 && profile.Gold == gold, "no run, no deck id");
        opponentsAvailable = true;
        BeginRequest(false); Tick();
        Check(service.View().Phase == ArenaPhase.ChoosingHero && profile.Gold == gold - service.View().EntryCost, "a new run can begin after the claim");

        ArenaServiceRegistry.Current = null; NativeUIProbe.Active = false;
        Console.WriteLine("PASS Arena transport over the real service: begin, hero, 30 picks, retire, ack, refusals");
    }
}
}
