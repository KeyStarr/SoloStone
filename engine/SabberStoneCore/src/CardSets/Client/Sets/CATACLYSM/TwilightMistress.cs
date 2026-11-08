using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Linq;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TwilightMistress
    {
        internal const int DbfId = 123072;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var minion in c.Opponent.BoardZone.GetAll().OrderBy(m => m.OrderOfPlay))
                    if (minion.Controller.HandZone.IsFull) minion.Destroy();
                    else Generic.ReturnToHandBlock(minion.Controller, minion);
            }) };
        }

    }
}
