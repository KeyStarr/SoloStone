using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MythicalRunebear
    {
        internal const int DbfId = 114298;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(HasFourAttack);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (HasFourAttack((IPlayable)s))
                    new EffectContext(g, c.Id, s.Id, stack: stack).RunLegacy(new SummonCopyTask(EntityType.SOURCE, side: SummonSide.RIGHT));
            }) };
        }

        private static bool HasFourAttack(IPlayable source) => ((Minion)source).AttackDamage >= 4;
    }
}
