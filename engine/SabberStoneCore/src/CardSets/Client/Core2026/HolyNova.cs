using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Core2026
{
    internal static class HolyNova
    {
        internal const int DbfId = 69508;
        internal static void Configure(Card card)
        {
            card.Power = new Power { PowerTask = ComplexTask.Create(new DamageTask(2, EntityType.OP_MINIONS, true), new HealTask(2, EntityType.FRIENDS)) };
        }
    }
}
