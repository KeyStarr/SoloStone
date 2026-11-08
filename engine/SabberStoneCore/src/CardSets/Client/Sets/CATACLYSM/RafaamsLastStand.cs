using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RafaamsLastStand
    {
        internal const int DbfId = 122829;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = ComplexTask.Scheme(ComplexTask.Create(
                    new RandomTask(2, EntityType.OP_MINIONS), new DamageNumberTask(EntityType.STACK, true))),
                Trigger = TriggerLibrary.UpgradeEachTurn
            };
        }
    }
}
