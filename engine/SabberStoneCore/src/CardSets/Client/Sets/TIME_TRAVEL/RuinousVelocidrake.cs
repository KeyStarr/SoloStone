using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RuinousVelocidrake
    {
        internal const int DbfId = 120488;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!TwilightTimehopper.CastShred(g, c, s)) return;
                var side = s.Zone == c.BoardZone ? SummonSide.RIGHT : SummonSide.DEFAULT;
                new EffectContext(g, c.Id, s.Id, stack: stack).RunLegacy(new SummonCopyTask(EntityType.SOURCE, side: side));
            }) };
        }
    }
}
