using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Convalescence
    {
        [StandardCard("MEND_802", 123622)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                for (int i = 0; i < 2 && !c.BoardZone.IsFull; i++)
                {
                    var recruit = (Minion)Entity.FromCard(c, Cards.FromId("CS2_101t"),
                        new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id, [GameTag.DIVINE_SHIELD] = 1 });
                    Generic.SummonBlock(g, recruit, -1, s);
                }
            }) };
        }
    }
}
