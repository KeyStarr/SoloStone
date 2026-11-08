using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MicroMachine
    {
        internal const int DbfId = 130823;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            // "Each turn" includes the opponent; replace this printing's power without mutating the legacy prototype.
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                EitherTurn = true,
                SingleTask = new AddEnchantmentTask("GVG_076a", EntityType.SOURCE)
            } };
        }
    }
}
