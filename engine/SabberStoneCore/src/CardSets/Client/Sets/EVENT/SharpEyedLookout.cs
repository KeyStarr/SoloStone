using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SharpEyedLookout
    {
        [StandardCard("EDR_950", 122547)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_6, Preview);
            card.HistoryDisplay((GameTag)955, PreviewText);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var drawn = new EffectContext(g, c.Id, s.Id).DrawEntity() as Playable;
                if (drawn == null || drawn.Zone != c.HandZone) return;
                var actor = Enchantment.GetInstance(c, (IPlayable)s, drawn, Cards.FromId("EDR_950e"));
                CostRules.AddModifier(drawn, EffectOperator.SUB, 1, g.Turn, actor.Id);
            }) };
        }
        private static int Preview(IPlayable source) => source.Controller.DeckZone.LastOrDefault()?.Card.AssetId ?? 0;
        private static int PreviewText(IPlayable source) => Preview(source) == 0 ? 0 : 1;
        [StandardCard("EDR_950e", 122548)]
        internal static void Discount(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.Power = new Power();
        }
    }
}
