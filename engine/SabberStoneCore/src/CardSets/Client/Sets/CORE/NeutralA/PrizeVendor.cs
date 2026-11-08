using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PrizeVendor
    {
        internal const int DbfId = 120429;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            var draw = new CustomTask((g, c, s, t, stack) => {
                Generic.Draw(c);
                Generic.Draw(c.Opponent);
            });
            card.Power = new Power { PowerTask = draw, DeathrattleTask = draw };
        }
    }
}
