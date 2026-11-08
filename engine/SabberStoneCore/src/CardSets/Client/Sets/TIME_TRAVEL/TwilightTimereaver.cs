using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TwilightTimereaver
    {
        internal const int DbfId = 121144;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }

        internal static void ConfigureAttackOption(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = new AddEnchantmentTask("END_010ae", EntityType.ALLMINIONS_NOSOURCE) };
        }

        internal static void ConfigureHealthOption(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = new AddEnchantmentTask("END_010be", EntityType.ALLMINIONS_NOSOURCE) };
        }

        internal static void ConfigureAttack(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(1)) };
        }

        internal static void ConfigureHealth(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetMaxHealth(1)) };
        }
    }
}
