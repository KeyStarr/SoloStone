using SabberStoneCore.Enums;
using SabberStoneCore.Model;

internal static class RushSilenceChecks
{
    internal static void Run(string root)
    {
#if ENGINE_SOURCE
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        var deck = LocalDecks.Get(LocalDecks.TestRushSilenceId);
        var cards = deck.Build();
        var ids = deck.CardIds.ToArray();
        Check(cards.Count == 30 && cards.All(card => card.Implemented), "Rush/silence deck scripted and complete");
        Check(deck.Hero == "HERO_09" && deck.Name == "TEST: Rush/Charge silence", "Priest test label");
        Check(cards.All(card => card.Class == CardClass.NEUTRAL || card.Class == CardClass.PRIEST)
            && cards.GroupBy(card => card.Id).All(group => group.Count() == 2 && group.First().Rarity != Rarity.LEGENDARY), "class and pair limits");
        Check(new[] { "EX1_332", "CS2_173", "CS2_124", "TIME_051", "EDR_492", "DINO_419", "CS2_172", "BE_228" }
            .All(id => ids.Count(value => value == id) == 2), "scenario cards present twice");
        Check(LocalDecks.OpponentFor(deck.Id, Array.Empty<string>()).Id == LocalDecks.TestStandardBatch1OpponentId
            && !LocalDecks.All.Contains(deck), "fixed Hunter and test-only rotation");
        NativeUIProjection.ValidateSelection(deck.Id, deck.Hero, ids, new Dictionary<string, int>());
        long nativeId = NativeUIProjection.DeckId(deck.Id);
        Check(nativeId == 900000022L && NativeUIProjection.IsLoanerId(nativeId) && !NativeUIProjection.IsPersonalId(nativeId)
            && NativeUIProjection.IsExactLoaner(nativeId, deck.Hero, ids), "reserved immutable loaner");
        var changed = ids.ToArray(); changed[0] = "CS2_179";
        bool rejected = false;
        try { NativeUIProjection.ValidateSelection(deck.Id, deck.Hero, changed, new Dictionary<string, int>()); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "altered loaner rejected");
        foreach (var card in cards)
        {
            bool standard = SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains(card.Id);
            Check(NativeUIProjection.SupportsPreview(card.Id)
                && NativeUIProjection.ForcesTestPlayability(true, card.Id) == standard
                && !NativeUIProjection.ForcesTestPlayability(false, card.Id), "only Standard cards force test playability");
            foreach (var tag in new[] { GameTag.COST, GameTag.ATK, GameTag.HEALTH, GameTag.CHARGE, GameTag.RUSH })
                Check(NativeUIProjection.TryGetDefinitionTag(true, true, true, card.Id, (int)tag, out int value)
                    && value == card[tag], "pinned printed stats/keywords");
        }
        string path = Path.Combine(root, "rush-silence-loaner.xml");
        using (var profile = new LocalProfile(path))
        {
            var before = profile.Collection.ToDictionary(pair => pair.Key, pair => pair.Value);
            var match = profile.Begin(deck.Id, LocalDecks.TestStandardBatch1OpponentId, 17);
            Check(match.DeckHero == deck.Hero && match.Opponent == LocalDecks.TestStandardBatch1OpponentId, "Priest pending match");
            Check(before.Count == profile.Collection.Count && before.All(pair => profile.Collection[pair.Key] == pair.Value), "loaner grants no collection cards");
        }
        using (var profile = new LocalProfile(path))
            Check(profile.Pending?.Opponent == LocalDecks.TestStandardBatch1OpponentId, "pending match survives reload");
        Console.WriteLine("PASS Rush/Charge silence loaner checks");
#endif
    }
}
