using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ImpGangStooge
    {
        internal const int DbfId = 125917;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                for (int i = 0; i < 2 && !c.DeckZone.IsFull; i++)
                    Entity.FromCard(c, Cards.FromId("JAIL_399t1"), new EntityData { { GameTag.DISPLAYED_CREATOR, s.Id } }, c.DeckZone, zonePos: 0, creator: s);
            }) };
        }
    }
}
