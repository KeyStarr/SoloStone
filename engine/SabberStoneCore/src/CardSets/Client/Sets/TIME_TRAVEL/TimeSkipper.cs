using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TimeSkipper
    {
        internal const int DbfId = 120955;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                EitherTurn = true,
                SingleTask = new CustomTask((g, c, s, t, stack) =>
                    new AddCardTo("GAME_005", EntityType.HAND).Process(g, g.CurrentPlayer, s, null, stack))
            } };
        }
    }
}
