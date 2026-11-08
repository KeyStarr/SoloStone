using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enums;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FollowTheEvidence
    {
        private static ISimpleTask Payload() => new CustomTask((g, c, s, t, stack) => {
            if (!c.Opponent.DeckZone.IsFull) {
                var imp = Entity.FromCard(c.Opponent, Cards.FromId("CAP_400t2t"));
                imp[GameTag.DISPLAYED_CREATOR] = s.Id;
                Generic.ShuffleIntoDeck(c.Opponent, s, imp);
            }
            FollowRules.Handoff((IPlayable)s, "CAP_402e");
        });

        [StandardCard("CAP_402", 127051)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = Payload() };
        }

        [StandardCard("CAP_402e", 127052)]
        internal static void ConfigureGrant(Card card) => FollowRules.ConfigureGrant(card,
            StateTaskList.Chain(FollowRules.Consume, Payload()));
    }
}
