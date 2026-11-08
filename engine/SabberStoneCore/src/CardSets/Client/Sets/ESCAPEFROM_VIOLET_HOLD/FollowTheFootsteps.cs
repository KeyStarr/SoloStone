using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FollowTheFootsteps
    {
        internal const int DbfId = 127041;
        private static OfflinePoolFilter Filter() => OfflinePoolFilter.Discover(type: CardType.MINION, keyword: GameTag.STEALTH);
        [OfflinePool("CAP_002", DbfId)]
        internal static PoolRequirement[] DeclarePools() => new[] {
            new PoolRequirement("follow-footsteps-stealth", Filter(), new[] { "ROGUE" })
        };

        private static ISimpleTask[] Steps() => new ISimpleTask[] {
            new CustomTask((g, c, s, t, stack) => { if (s is Enchantment actor) actor.Remove(); }),
            new DiscoverThenContinueTask(Filter(), recipeDbfId: DbfId),
            new CustomTask((g, c, s, t, stack) => {
                var chosen = stack.Playables.LastOrDefault();
                if (chosen == null || !Generic.RemoveFromZone(c, chosen)) return;
                Generic.AddHandPhase(c, chosen);
                if (chosen.Zone == c.HandZone) FollowRules.Grant((IPlayable)s, chosen, "CAP_002e");
            })
        };

        [StandardCard("CAP_002", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/CAP_002/v1", Steps()) };
        }

        [StandardCard("CAP_002e", 127043)]
        internal static void ConfigureGrant(Card card) => FollowRules.ConfigureGrant(card,
            StateTaskList.Suspendable("client/CAP_002e/v1", Steps()));
    }
}
