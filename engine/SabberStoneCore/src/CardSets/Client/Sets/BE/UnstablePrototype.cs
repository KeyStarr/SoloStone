using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class UnstablePrototype
    {
        internal const int DbfId = 131236;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Game.ResolveReborns creates the fresh one-Health entity after deathrattle resolution.
            card.Power = new Power { DeathrattleTask = ComplexTask.Create(
                new IncludeTask(EntityType.HAND),
                new FilterStackTask(SelfCondition.IsMinion),
                new AddEnchantmentTask("BE_207e", EntityType.STACK)) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) };
        }
    }
}
