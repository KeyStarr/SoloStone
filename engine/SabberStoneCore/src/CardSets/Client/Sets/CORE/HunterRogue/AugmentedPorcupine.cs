using SabberStoneCore.Enchants;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AugmentedPorcupine
    {
        internal const int DbfId = 130286;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                int missiles = ((Minion)s).AttackDamage;
                var missile = ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1);
                // Finish this Deathrattle before the next already-queued Deathrattle can resolve.
                for (int i = 0; i < missiles; i++)
                    missile.Process(g, c, s, t, new TaskStack());
            }) };
        }
    }
}
