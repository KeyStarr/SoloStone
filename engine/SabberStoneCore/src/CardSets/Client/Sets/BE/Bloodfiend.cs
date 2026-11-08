using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Bloodfiend
    {
        internal const int DbfId = 130395;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CanSpendCorpse);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!CanSpendCorpse((IPlayable)s)) return;
                int spent = CorpseRules.SpendUpTo(c, 4);
                // All waves share the Battlecry death phase, including hits on mortally wounded minions.
                for (int i = 0; i < spent; i++)
                    new DamageTask(1, EntityType.ALLMINIONS_NOSOURCE).Process(g, c, s, null, stack);
            }) };
        }

        private static bool CanSpendCorpse(IPlayable source) =>
            source.Game.EnableCorpses && CorpseRules.Available(source.Controller) >= 1;
    }
}
