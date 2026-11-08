using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.EffectSystem;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RoamFree
    {
        internal const int DbfId = 123513;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/MEND_307/v1",
                new CustomTask((g, c, s, t, stack) => CompanionRules.Upgrade(c, s, 2, DbfId)),
                DiscoverThenContinueTask.CompanionRoster(),
                new CustomTask((g, c, s, t, stack) => {
                    if (stack.Playables.Count > 0)
                        new EffectContext(g, c.Id, s.Id, stack: stack).Summon(stack.Playables[0].Card.Id);
                })) };
        }
    }
}
