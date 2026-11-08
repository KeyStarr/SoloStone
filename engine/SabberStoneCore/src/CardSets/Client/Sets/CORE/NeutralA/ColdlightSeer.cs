using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ColdlightSeer
    {
        internal const int DbfId = 69666;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.Tags[(GameTag)858] = 453;
            card.Power = new Power
            {
                PowerTask = ComplexTask.Create(
                    new IncludeTask(EntityType.MINIONS_NOSOURCE),
                    new FilterStackTask(SelfCondition.IsRace(Race.MURLOC)),
                    new AddEnchantmentTask("EX1_103e", EntityType.STACK))
            };
        }
    }
}
