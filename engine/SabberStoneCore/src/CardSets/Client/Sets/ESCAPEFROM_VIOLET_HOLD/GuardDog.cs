using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GuardDog
    {
        internal const int DbfId = 126299;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var chosen = ctx.PickRandomCard(EscapeFromVioletHoldPools.OneCostDeathrattle(), recipeDbfId: DbfId);
                if (chosen == null) return;
                var source = s as Minion ?? (s as Enchantment)?.Target as Minion;
                int position = source == null ? c.BoardZone.SlotCount : System.Math.Clamp(
                    source.Zone == c.BoardZone ? source.ZonePosition + 1 : source.LastBoardPosition, 0, c.BoardZone.SlotCount);
                ctx.Summon(chosen.Id, position);
            }) };
        }
    }
}
