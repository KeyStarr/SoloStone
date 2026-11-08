using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FollowTheFuse
    {
        private static ISimpleTask Payload() => new CustomTask((g, c, s, t, stack) => {
            var source = (IPlayable)s;
            var enemies = IncludeTask.GetEntities(EntityType.ENEMIES, c, s, t, stack?.Playables);
            if (enemies.Count != 0)
                Generic.DamageCharFunc(source, (ICharacter)enemies[g.Random.Next(enemies.Count)], SpellDamageRules.Amount(source, 2), false);
            g.DeathProcessingAndAuraUpdate();
            FollowRules.Handoff(source, "CAP_101e", pirate: true);
        });

        [StandardCard("CAP_101", 127013)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = Payload() };
        }

        [StandardCard("CAP_101e", 127035)]
        internal static void ConfigureGrant(Card card) => FollowRules.ConfigureGrant(card,
            StateTaskList.Chain(FollowRules.Consume, Payload()));
    }
}
