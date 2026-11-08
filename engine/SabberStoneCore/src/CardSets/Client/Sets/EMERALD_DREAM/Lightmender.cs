using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Lightmender
    {
        internal const int DbfId = 116667;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
        }

        internal static void ConfigureAttack(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(
                new AddEnchantmentTask("EDR_257ae", EntityType.SOURCE),
                new SetGameTagTask(GameTag.DIVINE_SHIELD, 1, EntityType.SOURCE)) };
        }

        internal static void ConfigureHealth(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(
                new AddEnchantmentTask("EDR_257be", EntityType.SOURCE),
                new SetGameTagTask(GameTag.LIFESTEAL, 1, EntityType.SOURCE)) };
        }

        internal static void ConfigureAttackBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(3)) };
        }

        internal static void ConfigureHealthBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Health_N(3)) };
        }
    }
}
