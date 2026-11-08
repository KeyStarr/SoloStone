using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Tichondrius
    {
        internal const int DbfId = 126887;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                Aura = new Aura(AuraType.HERO, Effects.Immune),
                PowerTask = new AddEnchantmentTask("CORE_CATA_001e", EntityType.CONTROLLER)
            };
        }

        internal static void ConfigureDiscount(Card card)
        {
            card.Implemented = true;
            card.Power = new Power {
                Aura = new Aura(AuraType.HAND, Effects.SetCost(0)) { Condition = SelfCondition.IsRace(Race.DEMON) },
                Trigger = new Trigger(TriggerType.PLAY_MINION) {
                    TriggerSource = TriggerSource.FRIENDLY,
                    Condition = SelfCondition.IsRace(Race.DEMON),
                    SingleTask = RemoveEnchantmentTask.Task
                }
            };
        }
    }
}
