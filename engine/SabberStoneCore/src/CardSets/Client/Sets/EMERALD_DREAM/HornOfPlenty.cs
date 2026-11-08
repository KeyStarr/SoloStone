using SabberStoneCore.Tasks;
using SabberStoneCore.Model.Entities;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HornOfPlenty
    {
        internal const int DbfId = 115080;
        [StandardCard("EDR_270", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = StateTaskList.Suspendable("client/EDR_270/h4-v1",
                new DiscoverThenContinueTask(B14H4EmeraldPools.Nature()),
                new CustomTask((g,c,s,t,stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen == null) return;
                    Generic.AddEnchantmentBlock(g,Cards.FromId("EDR_270e"),(IPlayable)s,chosen);
                    Generic.AddHandPhase(c,c.SetasideZone.Remove(chosen));
                })) };
        }
        [StandardCard("EDR_270e", 115081)]
        internal static void ConfigureDiscount(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.ReduceCost(2)) };
        }
    }
}
