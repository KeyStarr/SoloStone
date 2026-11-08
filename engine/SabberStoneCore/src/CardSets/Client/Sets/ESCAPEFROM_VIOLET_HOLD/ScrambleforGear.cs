using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ScrambleforGear
    {
        internal const int DbfId = 125893;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(new ArmorTask(2), new AddCardTo("JAIL_386t", EntityType.DECK, 5)) };
        }

        internal static void ConfigureGear(Card card)
        {
            card.Implemented = true;
            var effect = new ArmorTask(2);
            // DrawBlock invokes TopdeckTask only after a successful hand insertion.
            card.Power = new Power { PowerTask = effect, TopdeckTask = ComplexTask.Create(effect, new DrawTask()) };
        }
    }
}
