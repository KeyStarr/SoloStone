using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Reload
    {
        internal const int DbfId = 129823;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/BE_555/v1",
                new DiscoverThenContinueTask(BEPools.ReloadSpell(), recipeDbfId: DbfId, reductionBudget: 8, discount: "BE_555e")) };
        }
        internal static void ConfigureDiscount(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.ReduceCost(0)) { UseScriptTag = true } };
        }
    }
}
