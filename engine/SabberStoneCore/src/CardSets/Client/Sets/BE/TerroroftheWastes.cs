using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TerroroftheWastes
    {
        internal const int DbfId = 130114;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(BEHighlightConditions.FriendlyMinionDiedThisTurn);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!BEHighlightConditions.FriendlyMinionDiedThisTurn((IPlayable)s)) return;
                var targets = new TaskStack();
                new IncludeTask(EntityType.ENEMIES).Process(g, c, s, null, targets);
                new FilterStackTask(SelfCondition.IsNotDead).Process(g, c, s, null, targets);
                if (targets.Playables.Count == 0) return;
                new RandomTask(1, EntityType.STACK).Process(g, c, s, null, targets);
                new AttackTask(EntityType.SOURCE, EntityType.STACK).Process(g, c, s, null, targets);
            }) };
        }
    }
}
