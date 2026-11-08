using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Jailbird
    {
        internal const int DbfId = 126666;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.CARD_PREPARED) {
                TriggerActivation = TriggerActivation.HAND,
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = ComplexTask.Create(new GetEventNumberTask(), new AddEnchantmentTask("JAIL_453e", EntityType.SOURCE, true))
            } };
        }

        internal static void ConfigureDiscount(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.ReduceCost(0)) { UseScriptTag = true } };
        }
    }
}
