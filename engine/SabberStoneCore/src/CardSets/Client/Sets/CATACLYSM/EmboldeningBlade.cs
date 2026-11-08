using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EmboldeningBlade
    {
        internal const int DbfId = 123623;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new AddEnchantmentTask("MEND_803e", EntityType.CONTROLLER) };
        }

        internal static void ConfigureGameBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Aura = new Aura(AuraType.HAND_AND_BOARD, "MEND_803e2") {
                Condition = new SelfCondition(p => p is Minion && (p.Card.Id == "CS2_101t" || p.Card[(GameTag)3444] == 1))
            } };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) };
        }
    }
}
