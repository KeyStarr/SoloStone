using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RavenousFlock
    {
        internal const int DbfId = 117650;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new AddEnchantmentTask("TLC_232e", EntityType.CONTROLLER) };
        }

        internal static void ConfigureDelayed(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                TriggerSource = TriggerSource.FRIENDLY,
                RemoveAfterTriggered = true,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    new SummonTask("TLC_237t", 3).Process(g, c, s, t, stack);
                    // SummonTask stops on a full board; the delayed effect must still be consumed.
                    RemoveEnchantmentTask.Task.Process(g, c, s, t, stack);
                })
            } };
        }
    }
}
