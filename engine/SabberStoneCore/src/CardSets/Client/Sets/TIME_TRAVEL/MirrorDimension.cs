using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MirrorDimension
    {
        internal const int DbfId = 119541;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(TimeTravelHandConditions.HoldingAnotherDragon);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int count = TimeTravelHandConditions.HoldingAnotherDragon((IPlayable)s) ? 2 : 1;
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int position = s is Minion source && source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount;
                for (int i = 0; i < count; i++) ctx.Summon("TIME_006t1", position + i);
            }) };
        }
    }
}
