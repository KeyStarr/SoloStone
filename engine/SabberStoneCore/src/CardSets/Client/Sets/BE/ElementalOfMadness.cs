using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ElementalOfMadness
    {
        internal const int DbfId = 131008;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/BE_870/v1",
                new CustomTask((g, c, s, t, stack) => stack.Number = ((Minion)s).AttackDamage),
                new DiscoverThenContinueTask(BECostPools.MadnessSpell(), PoolCostBinding.StackNumber, recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = (Playable)stack.Playables.SingleOrDefault();
                    if (chosen == null) return;
                    Generic.RemoveFromZone(c, chosen);
                    var actor = Enchantment.GetInstance(c, (IPlayable)s, chosen, Cards.FromId("BE_870e"));
                    CostRules.AddModifier(chosen, EffectOperator.SET, 0, g.Turn, actor.Id);
                    Generic.AddHandPhase(c, chosen);
                    stack.Playables.Clear();
                })) };
        }
    }
}
