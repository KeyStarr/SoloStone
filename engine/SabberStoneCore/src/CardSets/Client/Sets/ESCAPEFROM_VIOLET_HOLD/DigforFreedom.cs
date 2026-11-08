using System.Collections.Generic;
using System;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DigforFreedom
    {
        internal const int DbfId = 126294;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 } });
            card.Power = new Power { PowerTask = new AddEnchantmentTask("JAIL_876e", EntityType.TARGET) };
        }

        internal static void ConfigureDeathrattle(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var source = s as Minion ?? (s as Enchantment)?.Target as Minion;
                int position = source == null ? c.BoardZone.SlotCount : System.Math.Clamp(
                    source.Zone == c.BoardZone ? source.ZonePosition + 1 : source.LastBoardPosition, 0, c.BoardZone.SlotCount);
                for (int i = 0; i < 2; i++)
                {
                    var chosen = ctx.PickRandomCard(EscapeFromVioletHoldPools.FourCostMinion(), recipeDbfId: DbfId);
                    if (chosen != null && ctx.Summon(chosen.Id, Math.Min(position, c.BoardZone.SlotCount)) != 0) position++;
                }
            }) };
        }
    }
}
