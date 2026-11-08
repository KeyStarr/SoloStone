using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Core2026
{
    internal static class DrainSoul
    {
        internal const int DbfId = 69735;
        internal static void Configure(Card card)
        {
            card.Power = new Power { PowerTask = new DamageTask(3, EntityType.TARGET, true) };
        }
    }
}
