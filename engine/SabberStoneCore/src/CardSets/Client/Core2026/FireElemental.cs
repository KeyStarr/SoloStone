using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Core2026
{
    internal static class FireElemental
    {
        internal const int DbfId = 69516;
        internal static void Configure(Card card)
        {
            card.Power = new Power { PowerTask = new DamageTask(4, EntityType.TARGET) };
        }
    }
}
