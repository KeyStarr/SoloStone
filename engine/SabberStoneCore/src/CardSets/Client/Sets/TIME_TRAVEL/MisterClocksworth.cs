using System;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MisterClocksworth
    {
        internal const int DbfId = 120646;
        internal static readonly OfflinePoolFilter Filter = new OfflinePoolFilter(PoolScope.Standard,
            PoolClassMode.Any, type: CardType.MINION, rarity: Rarity.LEGENDARY);
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var context = new EffectContext(g, c.Id, s.Id, stack: stack);
                for (int i = 0; i < 2 && !c.BoardZone.IsFull; i++)
                {
                    var pick = context.PickRandomCard(Filter, recipeDbfId: DbfId);
                    if (pick != null) context.Summon(pick.Id, Math.Min(((IPlayable)s).ZonePosition + 1 + i, c.BoardZone.SlotCount));
                }
            }) };
        }
    }
}
