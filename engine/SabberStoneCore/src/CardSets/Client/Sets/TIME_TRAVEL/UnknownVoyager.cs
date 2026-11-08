using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class UnknownVoyager
    {
        internal const int DbfId = 120958;
        internal static OfflinePoolFilter Filter() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 7, maxCost: 7);
        [OfflinePool("TIME_055", DbfId)]
        internal static PoolRequirement[] Pools() => new[] { new PoolRequirement("7-cost-minion", Filter(), new[] { "NEUTRAL" }, destination: PoolDestination.Transform) };
        [StandardCard("TIME_055", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TAKE_DAMAGE) {
                TriggerSource = TriggerSource.SELF, Condition = SelfCondition.IsNotDead,
                SingleTask = new CustomTask((g,c,s,t,stack) => {
                    if (s is not Minion m || m.Zone != c.BoardZone || m.ToBeDestroyed) return;
                    var pick = new EffectContext(g,c.Id,s.Id).PickRandomCard(Filter(), recipeDbfId: DbfId);
                    if (pick != null) Generic.ChangeEntityBlock(c,m,pick,true);
                })
            } };
        }
    }
}
