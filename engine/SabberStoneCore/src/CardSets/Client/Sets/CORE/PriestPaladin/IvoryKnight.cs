using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class IvoryKnight
    {
        internal const int DbfId = 130821;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.Tags[(GameTag)858] = 39439;
            card.Tags[(GameTag)1413] = 39439;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/CORE_KAR_057/v1",
                new DiscoverThenContinueTask(CoreCostPools.Spell(), recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen == null) return;
                    int amount = chosen.Card.Cost;
                    if (Generic.RemoveFromZone(c, chosen)) Generic.AddHandPhase(c, chosen);
                    new HealTask(amount, EntityType.HERO).Process(g, c, s, t, stack);
                })) };
        }
    }
}
