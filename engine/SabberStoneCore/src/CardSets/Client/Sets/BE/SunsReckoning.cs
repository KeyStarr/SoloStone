using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SunsReckoning
    {
        internal const int DbfId = 129137;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(
                new DamageTask(1, EntityType.OP_MINIONS, true),
                new CustomTask((g, c, s, t, stack) => Generic.ShuffleIntoDeck(c, s,
                    Entity.FromCard(c, Cards.FromId("BE_153t")), revealGenerated: true))) };
        }

        internal static void ConfigureFinal(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = new DamageTask(3, EntityType.OP_MINIONS, true) };
        }
    }
}
