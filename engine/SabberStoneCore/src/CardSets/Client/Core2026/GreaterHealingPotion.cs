using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Core2026
{
    internal static class GreaterHealingPotion
    {
        internal const int DbfId = 120459;
        internal static void Configure(Card card)
        {
            card.Power = new Power { PowerTask = ComplexTask.Create(new HealTask(12, EntityType.TARGET), new DrawTask()) };
        }
    }
}
