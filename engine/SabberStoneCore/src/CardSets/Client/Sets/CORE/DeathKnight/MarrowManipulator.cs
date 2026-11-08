using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MarrowManipulator
    {
        internal const int DbfId = 111323;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CanSpendCorpse);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!CanSpendCorpse((IPlayable)s)) return;
                int spent = CorpseRules.SpendUpTo(c, 5);
                for (int i = 0; i < spent; i++)
                    ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 2).Process(g, c, s, null, new TaskStack());
            }) };
        }
        private static bool CanSpendCorpse(IPlayable source) =>
            source.Game.EnableCorpses && CorpseRules.Available(source.Controller) >= 1;

    }
}
