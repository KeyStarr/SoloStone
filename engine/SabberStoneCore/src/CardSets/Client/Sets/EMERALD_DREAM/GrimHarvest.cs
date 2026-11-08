using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GrimHarvest
    {
        internal const int DbfId = 114654;
        internal static void Configure(Card card)
        {
            Dreadseeds.ConfigureGenerator(card);
            card.Power = new Power { PowerTask = ComplexTask.Create(new DrawTask(1), Dreadseeds.Summon()) };
        }
    }
}
