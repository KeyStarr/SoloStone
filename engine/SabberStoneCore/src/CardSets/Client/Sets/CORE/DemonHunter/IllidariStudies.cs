using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class IllidariStudies
    {
        internal const int DbfId = 97377;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/CORE_YOP_001/v1",
                new DiscoverThenContinueTask(CoreCostPools.Outcast(), recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    foreach (var chosen in stack.Playables)
                    {
                        Generic.RemoveFromZone(c, chosen);
                        Generic.AddHandPhase(c, chosen);
                    }
                    stack.Playables.Clear();
                }),
                new AddEnchantmentTask("YOP_001e", EntityType.CONTROLLER)) };
        }

        internal static void ConfigureDiscount(Card card)
        {
            card.Implemented = true;
            card.Power = new Power {
                Aura = new SabberStoneCore.Auras.Aura(SabberStoneCore.Auras.AuraType.HAND, Effects.ReduceCost(1)) {
                    Condition = new SabberStoneCore.Conditions.SelfCondition(p => p.Card[GameTag.OUTCAST] > 0)
                },
                Trigger = new SabberStoneCore.Triggers.Trigger(TriggerType.PLAY_CARD) {
                    TriggerSource = TriggerSource.FRIENDLY,
                    Condition = new SabberStoneCore.Conditions.SelfCondition(p => p.Card[GameTag.OUTCAST] > 0),
                    SingleTask = RemoveEnchantmentTask.Task
                }
            };
        }
    }
}
