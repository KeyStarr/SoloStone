using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DreamboundDisciple
    {
        internal const int DbfId = 114912;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            var grant = new CustomTask((g, c, s, t, stack) => {
                var actor = Enchantment.GetInstance(c, (IPlayable)s, c, Cards.FromId("EDR_847e"));
                CostRules.AddNextHeroPowerRule(c, setCost: 0, enchantmentId: actor.Id);
            });
            card.Power = new Power { PowerTask = grant, DeathrattleTask = grant };
        }
    }
}
