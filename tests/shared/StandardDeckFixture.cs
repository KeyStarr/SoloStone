using System;
using System.Collections.Generic;
using System.Linq;
// Thirty-card Standard decks a profile can play: the hero's owned class cards (two copies, one for legendaries), then
// owned neutral Standard cards. Classic cards are Wild since #87, so test decks that must start a match use these.
internal static class StandardDeckFixture
{
    internal static List<string> Deck(LocalProfile profile, string hero)
    {
        var owned = profile.Collection;
        var ids = LocalCoreProgression.Unlocks.Where(card => card.Hero == hero).Select(card => card.Id)
            .Concat(owned.Keys.Where(id => LocalCardCatalog.IsStandardId(id) && SabberStoneCore.Model.Cards.FromId(id).Class == SabberStoneCore.Enums.CardClass.NEUTRAL))
            .Where(id => owned.TryGetValue(id, out int n) && n > 0).Distinct().ToList();
        var deck = new List<string>();
        foreach (var id in ids)
            for (int i = 0; i < Math.Min(2, owned[id]) && deck.Count < 30; i++) deck.Add(id);
        if (deck.Count != 30) throw new Exception("Standard deck fixture needs 30 owned cards for " + hero + ", got " + deck.Count);
        return deck;
    }
}
