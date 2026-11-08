using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Morchie
    {
        internal const int DbfId = 121202;
        internal static bool KeepsBoth(Controller controller) =>
            controller.AppliedEnchantments?.Any(e => e.Card.Id == "END_036e") == true;

        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                Aura = new Aura(AuraType.CONTROLLER, "END_036e"),
                PowerTask = new DecisionBattlecryTask("client/END_036/v1",
                    new DiscoverThenContinueTask(TimeMachine.Filter, recipeDbfId: DbfId),
                    new CustomTask((g, c, s, t, stack) => {
                        var chosen = stack.Playables.SingleOrDefault();
                        if (chosen == null) return;
                        Generic.RemoveFromZone(c, chosen);
                        Generic.AddHandPhase(c, chosen);
                    }))
            };
        }

        internal static void ConfigureEnchantment(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant() };
        }
    }
}
