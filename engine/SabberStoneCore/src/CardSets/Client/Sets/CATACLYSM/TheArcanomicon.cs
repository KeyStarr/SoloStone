using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TheArcanomicon
    {
        internal const int DbfId = 123514;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/MEND_505/v1",
                // Lean: coordinator D4 fixes printed family order, including partial-hand survivors.
                EffectContext.Task(ctx => {
                    foreach (var leyline in LeylineRules.Family()) ctx.AddToHand(leyline.Id);
                    return SabberStoneCore.Tasks.TaskState.COMPLETE;
                }),
                new DiscoverThenContinueTask("MEND_505t", "MEND_505t2", "MEND_505t3"),
                new CustomTask((g, c, s, t, stack) => {
                    switch (stack?.Playables?.LastOrDefault()?.Card.Id)
                    {
                        case "MEND_505t": LeylineRules.Upgrade(c, (IPlayable)s, extraTriggers: 1); break;
                        case "MEND_505t2": LeylineRules.Upgrade(c, (IPlayable)s, cost: 2); break;
                        case "MEND_505t3": LeylineRules.Upgrade(c, (IPlayable)s, effect: 2); break;
                    }
                })) };
        }
        internal static void ConfigureOption(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                switch (s.Card.Id)
                {
                    case "MEND_505t": LeylineRules.Upgrade(c, (IPlayable)s, extraTriggers: 1); break;
                    case "MEND_505t2": LeylineRules.Upgrade(c, (IPlayable)s, cost: 2); break;
                    case "MEND_505t3": LeylineRules.Upgrade(c, (IPlayable)s, effect: 2); break;
                }
            }) };
        }
    }
}
