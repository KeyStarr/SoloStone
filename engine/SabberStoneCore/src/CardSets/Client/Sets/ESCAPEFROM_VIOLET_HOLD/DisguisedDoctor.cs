using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DisguisedDoctor
    {
        [StandardCard("JAIL_442", 126255)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g,c,s,t,stack) => {
                using var shuffle = g.Facts.Shuffle(c.Id);
                for (int i = 0; i < 4 && !c.DeckZone.IsFull; i++)
                    c.DeckZone.AddGenerated(Cards.FromId("JAIL_443t"), s, shuffle: true);
            }) };
        }
        [StandardCard("JAIL_442a", 126336)]
        [StandardCard("JAIL_442b", 126337)]
        internal static void Side(Card card)
        { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.Power = new Power(); }
    }
}
