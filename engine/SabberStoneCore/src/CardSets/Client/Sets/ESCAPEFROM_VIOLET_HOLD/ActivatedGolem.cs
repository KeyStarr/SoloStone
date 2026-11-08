using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ActivatedGolem
    {
        internal const int DbfId = 126472;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                EitherTurn = true,
                SingleTask = ComplexTask.Create(new SetGameTagTask(GameTag.REBORN, 1, EntityType.SOURCE),
                    new SetGameTagTask(GameTag.HAS_BEEN_REBORN, 0, EntityType.SOURCE))
            } };
        }
    }
}
