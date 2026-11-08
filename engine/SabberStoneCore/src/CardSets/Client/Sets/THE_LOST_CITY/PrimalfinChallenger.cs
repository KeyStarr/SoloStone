using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PrimalfinChallenger
    {
        [StandardCard("TLC_251", 118251)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) =>
                Enchantment.GetInstance(c, (IPlayable)s, c, Cards.FromId("TLC_251e"))) };
        }

        [StandardCard("TLC_251e", 121022)]
        internal static void ConfigureEnchantment(Card card)
        {
            card.Implemented = true;
            card.Power = new Power();
        }
    }
}
