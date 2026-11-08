using System;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Morchok
    {
        internal const int DbfId = 123006;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                // The authored draw impact needs a typed source before deck-card SHOW.
                if (g.History) g.PowerHistory.Add(new PowerHistoryMetaData {
                    Type = MetaDataType.TARGET, Info = new List<int> { s.Id }
                });
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int remaining = 10;
                while (remaining > 0 && !c.DeckZone.IsEmpty)
                {
                    // Discount the actual draw before Shatter replaces its hand entity.
                    using var handEntry = c.HandZone.DeferCardStateEntry();
                    var drawn = ctx.DrawEntity() as Playable;
                    if (drawn == null || drawn.Zone != c.HandZone) continue;
                    g.AuraUpdate(); // New hand entries must receive cost auras before sampling the budget debit.
                    int cost = Math.Max(0, drawn.Cost);
                    CostRules.AddModifier(drawn, EffectOperator.SUB, Math.Min(cost, remaining));
                    remaining -= cost - Math.Max(0, drawn.Cost);
                }
            }) };
        }
    }
}
