using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class IKnowAGuy
    {
        internal const int DbfId = 120468;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/CORE_WON_350/v1",
                new DiscoverThenContinueTask(CoreCostPools.TauntMinion(), recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen == null) return;
                    Generic.AddEnchantmentBlock(g, Cards.FromId("CFM_940e"), (IPlayable)s, chosen);
                    Generic.RemoveFromZone(c, chosen);
                    Generic.AddHandPhase(c, chosen);
                    stack.Playables.Clear();
                })) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1), Effects.Health_N(2)) };
        }
    }
}
