using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

// Options > Show Plain Cards: a match prepared with it sends every card of both sides Normal, owned, NPC, derived and engine-authored
// premium alike, and changes nothing else on the wire or in play. The golden hero is in GoldenHeroHistory.
internal static partial class Program
{
    private static bool PremiumWireTag(int tag) => tag == (int)GameTag.PREMIUM || tag == DiamondQuality || tag == SignatureQuality;

    private static void AssertNoPremiumOnWire(IEnumerable<PowerHistoryData> history, string label)
    {
        foreach (var entry in history)
        {
            var snapshot = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
            if (snapshot != null)
                Check(snapshot.Tags.All(tag => !PremiumWireTag(tag.Name) || tag.Value == 0),
                    label + ": snapshot of " + snapshot.Entity + " (" + snapshot.Name + ") carries premium");
            if (entry.HasTagChange && PremiumWireTag(entry.TagChange.Tag))
                Check(entry.TagChange.Value == 0, label + ": tag change " + entry.TagChange.Tag + "=" + entry.TagChange.Value + " on " + entry.TagChange.Entity);
        }
    }

    private static void PlainCardsHistory()
    {
        const int seed = 4242;
        LocalDeck source = LocalDecks.Get("mage");
        LocalDeck deck = LocalDecks.Custom("Plain", source.Hero, source.CardIds);
        // Every human copy premium (Golden, Diamond, Signature in turn), and a Legend NPC whose copies carry their best finish.
        var entries = deck.CardIds.Select((id, at) => new LocalDeckCard { Id = id, Premium = 1 + at % 3 }).ToList();
        var seat = new NpcPremiumSeat("npc.n.premium-test", "warrior", LadderRules.LegendIndex, null);

        NativeMatch Run(Capture capture, bool plain)
        {
            var match = new NativeMatch(capture.Send, deck, "warrior", seed, 0, entries) { OpponentPremiumSeat = seat, PlainCards = plain };
            match.Start();
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            DriveToTerminal(match, capture);
            return match;
        }
        var premiumRun = new Capture();
        var premium = Run(premiumRun, false);
        var plainRun = new Capture();
        var plain = Run(plainRun, true);
        Check(NativeHistory(premiumRun).Any(entry => SnapshotTag(entry, GameTag.PREMIUM) != 0)
            && NativeHistory(premiumRun).Any(entry => SnapshotTag(entry, (GameTag)DiamondQuality) != 0 || SnapshotTag(entry, (GameTag)SignatureQuality) != 0),
            "plain fixture's premium run shows no Golden and no Diamond/Signature card");
        AssertNoPremiumOnWire(NativeHistory(plainRun), "plain terminal run");
        Check(plain.Game.State == State.COMPLETE && premium.Game.State == State.COMPLETE && plain.Game.Turn == premium.Game.Turn
            && plain.Game.Player1.PlayState == premium.Game.Player1.PlayState
            && plain.Game.Player1.Hero.Health == premium.Game.Player1.Hero.Health
            && plain.Game.Player2.Hero.Health == premium.Game.Player2.Hero.Health, "Show Plain Cards changed the match outcome");
        Check(HistorySignature(NativeHistory(premiumRun)).SequenceEqual(HistorySignature(NativeHistory(plainRun))),
            "Show Plain Cards changed non-premium native history");
        plainRun.SerializeAll();

        // Engine-authored premium (Zola's Golden copy), a printed Signature flag and a late owned premium all read Normal.
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, deck, "warrior", seed, 0, entries) { PlainCards = true };
        match.Start();
        var game = match.Game;
        var owned = OwnedPremiums(match);
        Check(owned.Count > 0, "plain match dropped the owned premium bookkeeping");
        int cursor = capture.Packets.Count;
        var generated = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId(game.Player1.HandZone.First().Card.Id), zone: game.Player1.HandZone);
        FlushHistory(match);
        generated[GameTag.PREMIUM] = 1;
        FlushHistory(match);
        var signedCard = Cards.All.First(card => card.Collectible && card.Implemented && card.Type == CardType.MINION && card[(GameTag)SignatureQuality] == 1);
        var signed = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, signedCard, zone: game.Player1.HandZone);
        owned[signed.Id] = 3;
        FlushHistory(match);
        var late = NativeHistory(capture, cursor);
        Check(late.Any(entry => entry.HasFullEntity && entry.FullEntity.Entity == signed.Id), "plain fixture never sent the Signature card");
        AssertNoPremiumOnWire(late, "plain match injections");
        foreach (int id in new[] { generated.Id, signed.Id })
        {
            var tags = ReplayNativeEntityTags(NativeHistory(capture), id);
            Check(tags.Where(tag => PremiumWireTag(tag.Key)).All(tag => tag.Value == 0), "plain card " + id + " merged premium tags are not Normal");
            Check(match.SnapshotVisibleTransportTags(game.IdEntityDic[id]).Where(tag => PremiumWireTag(tag.Key)).All(tag => tag.Value == 0),
                "plain card " + id + " snapshot audit expects premium");
        }
        Console.WriteLine("PASS Show Plain Cards sends every card Normal without changing play or other history");
    }
}
