using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GetawayHogdriver
    {
        internal const int DbfId = 128053;
        // Rule: both draws must put a minion into hand. Overdraw is not a draw (wiki.gg/wiki/Draw, tier C) and fatigue draws
        // nothing, so either fails the condition (independent retail report cited in its proof). The export has no Charge enchantment: Charge is
        // the entity's own CHARGE tag, which Silence clears with the keyword-readiness rule.
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var first = ctx.DrawEntity();
                var second = ctx.DrawEntity();
                if (first is Minion && second is Minion && s is Minion self && self.Zone == c.BoardZone)
                    self.HasCharge = true;
            }) };
        }
    }
}
