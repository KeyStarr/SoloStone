using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Coilbearer
    {
        internal const int DbfId = 130441;
        [StandardCard("BE_150", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/BE_150/v1",
                new DiscoverThenContinueTask(B14K3BEPools.Spell(), recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen == null || c.DeckZone.IsFull) return;
                    Generic.RemoveFromZone(c, chosen);
                    CostRules.AddModifier((Playable)chosen, EffectOperator.SUB, 2);
                    c.DeckZone.Add(chosen);
                    stack.Playables.Clear();
                })) };
        }
    }
}
