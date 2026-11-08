using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ManifestedTimeways
    {
        private static bool HasAura(IPlayable source) => AuraSpellRules.HasActive(source.Controller);
        [StandardCard("TIME_019", 120011)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(HasAura);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (HasAura((IPlayable)s)) new DamageTask(3, EntityType.ENEMIES).Process(g, c, s, t, stack);
            }) };
        }
    }
}
