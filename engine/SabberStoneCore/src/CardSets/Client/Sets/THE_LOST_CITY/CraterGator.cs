using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CraterGator
    {
        [StandardCard("TLC_250", 118247)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) =>
                Generic.AddEnchantmentBlock(g,Cards.FromId("TLC_250e"),(IPlayable)s,c)) };
        }
        [StandardCard("TLC_250e", 118248)]
        internal static void ConfigureEnchantment(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                TriggerSource = TriggerSource.FRIENDLY, RemoveAfterTriggered = true,
                SingleTask = RemoveEnchantmentTask.Task
            } };
        }
    }
}
