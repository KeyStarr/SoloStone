using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Spelunker
    {
        internal const int DbfId = 117890;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var actor = Enchantment.GetInstance(c, (IPlayable)s, c, Cards.FromId("TLC_450e"));
                CostRules.AddNextTemporaryRule(c, 2, actor.Id);
            }) };
        }
    }
}
