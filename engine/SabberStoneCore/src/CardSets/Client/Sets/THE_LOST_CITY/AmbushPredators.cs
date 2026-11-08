using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AmbushPredators
    {
        internal const int DbfId = 118580;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                bool kindred = KindredRules.Match((IPlayable)s);
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                ctx.Summon("TLC_519t");
                int repeats = kindred ? KindredRules.BeginMatchedClause((IPlayable)s) : 0;
                for (int i = 0; i < repeats; i++) ctx.Summon("TLC_519t");
            }) };
        }
    }
}
