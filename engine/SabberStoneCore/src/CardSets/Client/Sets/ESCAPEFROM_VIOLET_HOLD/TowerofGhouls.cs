using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TowerofGhouls
    {
        internal const int DbfId = 126252;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TAKE_DAMAGE) {
                TriggerSource = TriggerSource.SELF,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                    int position = s is Minion source && source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount;
                    for (int i = 0; i < 2; i++) ctx.Summon("HERO_11bpt", position + i);
                })
            } };
        }

        internal static void ConfigureGhoul(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new DestroyTask(EntityType.SOURCE)
            } };
        }
    }
}
