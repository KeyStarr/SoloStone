using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TachyonBarrage
    {
        internal const int DbfId = 120485;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int missiles = 6 + SabberStoneCore.Actions.SpellDamageRules.Bonus((SabberStoneCore.Model.Entities.IPlayable)s);
                new EnqueueTask(1, new AddCardTo("TIME_025t", EntityType.DECK, 2)).Process(g, c, s, t, stack);
                SplitHits.Inline(g, c, s, missiles,
                    ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1), deathProcessing: false);
            }) };
        }
    }
}
