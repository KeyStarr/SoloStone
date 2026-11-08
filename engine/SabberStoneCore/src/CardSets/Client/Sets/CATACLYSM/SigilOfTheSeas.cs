using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SigilOfTheSeas
    {
        internal const int DbfId = 122773;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // CastSpell.cs:59 retains a non-secret spell trigger after the spell goes to the graveyard.
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                RemoveAfterTriggered = true,
                SingleTask = new SummonTask("CATA_528t", SummonSide.DEFAULT)
            } };
        }

    }
}
