using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Warloc
    {
        internal const int DbfId = 123675;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var actor = Enchantment.GetInstance(c, (IPlayable)s, c, Cards.FromId("CATA_180e"));
                CostRules.AddNextCardRule(c, CostPaymentKind.Health, CardType.MINION, Race.MURLOC,
                    maxEffectiveCost: 3, enchantmentId: actor.Id);
            }) };
        }
    }
}
