using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GraveStrength
    {
        internal const int DbfId = 80645;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CanSpendCorpses);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                string enchantment = CanSpendCorpses((IPlayable)s) && CorpseRules.TrySpend(c, 5) ? "RLK_707e2" : "RLK_707e";
                new AddEnchantmentTask(enchantment, EntityType.MINIONS).Process(g, c, s, t, stack);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1)) };
        }

        internal static void ConfigureGreaterBuff(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(3)) };
        }
        private static bool CanSpendCorpses(IPlayable source) =>
            source.Game.EnableCorpses && CorpseRules.Available(source.Controller) >= 5;

    }
}
