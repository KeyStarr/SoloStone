using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BloodTap
    {
        internal const int DbfId = 111321;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CanSpendCorpses);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var minion in c.HandZone.OfType<Minion>())
                    Generic.AddEnchantmentBlock(g, Cards.FromId("RLK_712e"), (IPlayable)s, minion);
                if (CanSpendCorpses((IPlayable)s) && CorpseRules.TrySpend(c, 2))
                    foreach (var minion in c.HandZone.OfType<Minion>())
                        Generic.AddEnchantmentBlock(g, Cards.FromId("RLK_712e"), (IPlayable)s, minion);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) };
        }
        private static bool CanSpendCorpses(IPlayable source) =>
            source.Game.EnableCorpses && CorpseRules.Available(source.Controller) >= 2;

    }
}
