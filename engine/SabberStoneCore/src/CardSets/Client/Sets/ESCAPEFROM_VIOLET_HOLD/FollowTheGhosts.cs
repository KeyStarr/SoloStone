using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FollowTheGhosts
    {
        private static ISimpleTask Payload() => new CustomTask((g, c, s, t, stack) => {
            new SummonTask("CAP_802t", SummonSide.SPELL).Process(g, c, s, t, stack);
            FollowRules.Handoff((IPlayable)s, "CAP_802e1");
        });

        [StandardCard("CAP_802", 127062)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = Payload() };
        }

        [StandardCard("CAP_802e1", 132351)]
        internal static void ConfigureGrant(Card card) => FollowRules.ConfigureGrant(card,
            StateTaskList.Chain(FollowRules.Consume, Payload()));

        [StandardCard("CAP_802t", 127063)]
        internal static void ConfigureGhost(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
    }
}
