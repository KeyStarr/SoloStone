using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CommanderGeddon
    {
        internal const int DbfId = 123321;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (c.AppliedEnchantments?.Any(e => e.Card.Id == "CATA_591e") != true)
                    Enchantment.GetInstance(c, (IPlayable)s, c, Cards.FromId("CATA_591e"));
            }) };
        }
        internal static void ConfigureReplacement(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = StateTaskList.Suspendable("client/CATA_591e/v1",
                new DiscoverThenContinueTask(DiscoverEntitySource.Deck, DiscoverEntityAction.Draw,
                    RejectedOfferAction.Destroy, discount: "CATA_591e2")) };
        }
        internal static void ConfigureDiscount(Card card)
        {
            card.Implemented = true; card.Power = new Power { Enchant = new Enchant(Effects.ReduceCost(3)) };
        }
    }
}
