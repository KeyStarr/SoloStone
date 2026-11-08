using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WidowsBite
    {
        internal const int DbfId = 126180;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(new AddEnchantmentTask("JAIL_436e", EntityType.HERO), new ArmorTask(1), new AddCardTo("JAIL_436t", EntityType.HAND)) };
        }

        internal static void ConfigureFeast(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(new AddEnchantmentTask("JAIL_436e2", EntityType.HERO), new ArmorTask(2), new AddCardTo("JAIL_436t2", EntityType.HAND)) };
        }
        internal static void ConfigureBanquet(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(new AddEnchantmentTask("JAIL_436e3", EntityType.HERO), new ArmorTask(4)) };
        }
        internal static void ConfigureBuff(Card card) => Buff(card, 1);
        internal static void ConfigureFeastBuff(Card card) => Buff(card, 2);
        internal static void ConfigureBanquetBuff(Card card) => Buff(card, 4);
        private static void Buff(Card card, int attack)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(attack)) { IsOneTurnEffect = true } };
        }
    }
}
