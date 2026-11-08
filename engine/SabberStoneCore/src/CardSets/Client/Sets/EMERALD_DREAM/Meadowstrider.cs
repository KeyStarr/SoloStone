using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Meadowstrider
    {
        internal const int DbfId = 117511;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                if (c.DeckZone.IsFull) return;
                var fresh = Entity.FromCard(c, Cards.FromId("EDR_978"),
                    new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                Effects.SetCost(1).ApplyTo(fresh);
                // Deck draws from its last index; index zero is the bottom.
                c.DeckZone.Add(fresh, 0);
            }) };
        }
    }
}
