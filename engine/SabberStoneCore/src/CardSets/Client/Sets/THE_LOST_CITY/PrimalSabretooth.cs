using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PrimalSabretooth
    {
        internal const int DbfId = 118235;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.SELF,
                Condition = new SelfCondition(p => p.Game.CurrentEventData?.EventTarget is Minion victim && victim.ToBeDestroyed),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (g.CurrentEventData?.EventTarget is Minion victim)
                        new CopyTask(EntityType.TARGET, Zone.HAND).Process(g, c, s, victim, stack);
                })
            } };
        }
    }
}
