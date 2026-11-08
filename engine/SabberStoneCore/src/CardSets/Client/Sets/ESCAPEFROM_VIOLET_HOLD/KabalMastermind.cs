using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class KabalMastermind
    {
        internal const int DbfId = 127090;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new AddEnchantmentTask("CAP_406e", EntityType.CONTROLLER) };
        }

        internal static void ConfigureControllerBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.SUMMON) {
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = new SelfCondition(p => p.Card.Id == "CAP_400t2t"),
                SingleTask = new AddEnchantmentTask("CAP_406e2", EntityType.TARGET)
            } };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2), Effects.Health_N(2)) };
        }
    }
}
