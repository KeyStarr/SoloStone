using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EverburningPhoenix
    {
        internal const int DbfId = 115646;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                // PlayCard.cs increments after payment; Game.cs clears the tag at turn cleanup.
                Aura = new AdaptiveCostEffect(p => p.Controller.NumCardsPlayedThisTurn),
                DeathrattleTask = new AddEnchantmentTask("FIR_919e", EntityType.CONTROLLER)
            };
        }

        internal static void ConfigureReturn(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                EitherTurn = true,
                RemoveAfterTriggered = true,
                SingleTask = ComplexTask.Create(new AddCardTo("FIR_919", EntityType.HAND), RemoveEnchantmentTask.Task)
            } };
        }
    }
}
