using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EmberrootDestroyer
    {
        internal const int DbfId = 115754;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TAKE_DAMAGE) {
                // Global damage events include Armor damage and survive hero replacement.
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = new SelfCondition(p => p == p.Controller.Hero && p.Controller == p.Game.CurrentPlayer),
                SingleTask = ComplexTask.Create(new RandomTask(1, EntityType.OP_MINIONS), new DamageTask(3, EntityType.STACK))
            } };
        }
    }
}
