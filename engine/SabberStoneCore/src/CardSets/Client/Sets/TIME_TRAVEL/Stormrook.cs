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
    internal static class Stormrook
    {
        internal const int DbfId = 120177;
        internal static OfflinePoolFilter FiveCostMinion() =>
            OfflinePoolFilter.Random(type: CardType.MINION, minCost: 5, maxCost: 5);

        [OfflinePool("TIME_217", DbfId)]
        internal static PoolRequirement[] DeclarePools() => new[] {
            new PoolRequirement("five-cost-minion", FiveCostMinion(), new[] { "SHAMAN" }, destination: PoolDestination.Summon)
        };

        [StandardCard("TIME_217", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.PREDAMAGE) {
                TriggerSource = TriggerSource.SELF,
                FastExecution = true,
                Condition = new SelfCondition(me => {
                    var data = me.Game.CurrentEventData;
                    return me is Character character && !character.IsImmune
                        && data != null && data.EventNumber > 0 && data.EventSource.Controller == me.Controller
                        && PrimordialOverseer.IsNatureSpell(data.EventSource.Card);
                }),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    // Character.TakeDamage rereads this amount after PREDAMAGE tasks.
                    g.CurrentEventData.EventNumber = 0;
                    var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                    var chosen = ctx.PickRandomCard(FiveCostMinion(), recipeDbfId: DbfId);
                    if (chosen != null)
                        ctx.Summon(chosen.Id, s.Zone == c.BoardZone ? ((IPlayable)s).ZonePosition + 1 : c.BoardZone.SlotCount);
                })
            } };
        }
    }
}
