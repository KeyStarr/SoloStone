using SabberStoneCore.Model.Zones;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static LostCityPoolProof;

internal static class MerchantOfLegendProof
{
    private static bool Matches(Card c) => c.Type == CardType.MINION && c.Rarity == Rarity.LEGENDARY;
    private static void Verify(Game g, int[] offers, int chosen, int source, int deck)
    {
        var p = g.Player1; var kept = g.IdEntityDic[chosen];
        Core2026Proof.Check(kept.Zone == p.HandZone && p.DeckZone.Count == deck + offers.Length - 1,
            "chosen entity enters hand; every rejected offer enters deck");
        foreach (int id in offers)
        {
            var made = g.IdEntityDic[id]; var m = (Minion)made;
            Core2026Proof.Check(made.Controller == p && made[GameTag.DISPLAYED_CREATOR] == source && Matches(made.Card)
                && m.AttackDamage == m.Card.ATK && m.Health == m.Card.Health
                && (id == chosen ? made.Zone == p.HandZone : made.Zone == p.DeckZone), "exact offered identities, printed stats, owner/creator and destinations");
        }
    }
    internal static void Run() => StandardSetsProof.Case("TLC_514", (g, p, id) => {
        ShuffleOperations(g.Seed, id);
        Core2026Proof.Check(OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), Cards.FromId(id).AssetId).Count >= 3, "legendary controller/neutral pool meets threshold");
        var source = Start(g, p, id); var offers = Offers(g, p, source, Matches); int deck = p.DeckZone.Count;
        foreach (int chosen in offers)
        {
            var clone = g.CloneExact(history: true); Answer(clone, chosen); Verify(clone, offers, chosen, source.Id, deck);
        }
        PoolPilotProof.Resume(g, next => { Answer(next, offers[0]); Verify(next, offers, offers[0], source.Id, deck); });
        var replay = g.CloneExact(history: true); int hand = replay.Player1.HandZone.Count;
        var shudder = Play(replay, replay.Player1, "GIL_820");
        Core2026Proof.Check(replay.Player1.Choice == null && !replay.Resolution.HasSuspendedEffect
            && replay.Player1.HandZone.Count == hand + 1 && replay.Player1.DeckZone.Count == deck + 4
            && replay.Player1.HandZone.Any(c => c[GameTag.DISPLAYED_CREATOR] == shudder.Id && Matches(c.Card)),
            "Shudderwock replay uses Merchant declaration and automatically resolves exactly one Discover");
        ParkBoard(p); PoolPilotProof.EmptyHand(p);
        foreach (int count in new[] { 0, 1, 2 })
        {
            PoolPilotProof.Pool(g, id, count, () => {
                int before = p.DeckZone.Count; var random = g.Random.CaptureState(); source = Play(g, p, id);
                if (count == 0) Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 0 && p.DeckZone.Count == before
                    && random.Equals(g.Random.CaptureState()), "empty pool has no output/choice/RNG");
                else { offers = Offers(g, p, source, Matches); Answer(g, offers[0]); Verify(g, offers, offers[0], source.Id, before); }
            });
            ParkBoard(p); PoolPilotProof.EmptyHand(p);
        }
        Summon(p, "LOE_077"); source = Play(g, p, id); offers = Offers(g, p, source, Matches); deck = p.DeckZone.Count;
        Answer(g, offers[0]); Core2026Proof.Check(p.Choice != null && p.DeckZone.Count == deck + 2, "Brann first Discover completes before second opens");
        var second = Offers(g, p, source, Matches); Answer(g, second[0]);
        Core2026Proof.Check(p.HandZone.Count == 2 && p.DeckZone.Count == deck + 4 && p.Choice == null, "Brann gives two rewards and shuffles four rejected offers");
        ParkBoard(p); PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p, 9);
        var location = LocationProofs.Location(p); while (p.BoardZone.FreeSpace > 1) Summon(p, "CS2_182");
        source = Play(g, p, id); offers = Offers(g, p, source, Matches); deck = p.DeckZone.Count; PoolPilotProof.FillHand(p); Answer(g, offers[0]);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.IdEntityDic[offers[0]].Zone == p.GraveyardZone
            && offers.Skip(1).All(i => g.IdEntityDic[i].Zone == p.DeckZone) && p.DeckZone.Count == deck + 2
            && p.BoardZone.SlotCount == 7 && p.BoardZone.GetLocations().Contains(location), "full hand burns chosen reward but shuffles rejected offers; mixed board has seven slots");
    });

    private static void ShuffleOperations(long seed, string id)
    {
        var g = GiftedDiscoverProof.New(seed, CardClass.ROGUE); var p = g.Player1;
        var knockback = Add(p, "TLC_517"); var tracker = Add(p, "TLC_520");
        // Tier C: wiki.gg/Knockback (history-facts R4): one improvement per shuffle operation, not per inserted card.
        var source = Play(g, p, id); var offers = Offers(g, p, source, Matches);
        PoolPilotProof.Resume(g, next => {
            Answer(next, offers[0]);
            var facts = next.HistoryFacts.Query.Events<ShuffleOperationFact>().ToArray();
            Core2026Proof.Check(facts.Length == 2 && facts.Select(f => f.OperationId).Distinct().Count() == 1
                && facts.Count(f => f.IsFirst) == 1 && facts.All(f => f.ControllerId == next.Player1.Id && f.DestinationControllerId == next.Player1.Id)
                && facts.Select(f => f.EntityId).OrderBy(i => i).SequenceEqual(offers.Skip(1).OrderBy(i => i)),
                "Merchant records both rejected entities as one shuffle operation per Battlecry");
            Core2026Proof.Check(next.IdEntityDic[knockback.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 2
                && next.IdEntityDic[tracker.Id].Cost == 5, "one Merchant improves Knockback and Tracker once");
        });
        Summon(p, "LOE_077"); source = Play(g, p, id); offers = Offers(g, p, source, Matches);
        Answer(g, offers[0]);
        Core2026Proof.Check(g.HistoryFacts.Query.Shuffles().Count() == 2 && p.Choice != null, "Brann first Battlecry records one additional operation");
        offers = Offers(g, p, source, Matches); Answer(g, offers[0]);
        Core2026Proof.Check(g.HistoryFacts.Query.Shuffles().Count() == 3 && g.HistoryFacts.Query.Events<ShuffleOperationFact>().Count() == 6
            && knockback[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4 && tracker.Cost == 3,
            "Brann records two distinct operations, each improving history consumers once");
        ParkBoard(p);
        while (p.DeckZone.Count < (DeckZone.DeckMaximumCapcity - 1)) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_182")));
        foreach (int inserted in new[] { 1, 0 })
        {
            int operations = g.HistoryFacts.Query.Shuffles().Count(), members = g.HistoryFacts.Query.Events<ShuffleOperationFact>().Count();
            source = Play(g, p, id); offers = Offers(g, p, source, Matches); Answer(g, offers[0]);
            Core2026Proof.Check(p.DeckZone.Count == DeckZone.DeckMaximumCapcity && g.HistoryFacts.Query.Shuffles().Count() == operations + inserted
                && g.HistoryFacts.Query.Events<ShuffleOperationFact>().Count() == members + inserted,
                "partially full deck records one successful operation; full deck records none");
        }
    }
}
