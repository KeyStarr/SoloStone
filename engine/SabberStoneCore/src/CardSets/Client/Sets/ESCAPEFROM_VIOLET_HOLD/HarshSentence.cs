using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HarshSentence
    {
        internal const int DbfId = 127058;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                new AddCardTo("CAP_400t2t", EntityType.OP_DECK, 2).Process(g, c, s, t, stack);
                new Trigger(TriggerType.TURN_START) {
                    EitherTurn = true,
                    Condition = SelfCondition.IsOpTurn,
                    RemoveAfterTriggered = true,
                    SingleTask = new AddEnchantmentTask("CAP_404e", EntityType.CONTROLLER)
                }.Activate((IPlayable)s, asAncillary: ((IPlayable)s).ActivatedTrigger != null);
            }) };
        }

        internal static void ConfigureTax(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Aura = new Aura(AuraType.OP_HAND, Effects.AddCost(2)) {
                Condition = SelfCondition.IsMinion,
                RemoveTrigger = (TriggerType.TURN_END, SelfCondition.IsOpTurn)
            } };
        }
    }
}
