using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Core2026
{
    internal static class SI7Agent
    {
        internal const int DbfId = 69621;
        internal static void Configure(Card card)
        {
            card.Power = new Power { ComboTask = new DamageTask(3, EntityType.TARGET) };
        }
    }
}
