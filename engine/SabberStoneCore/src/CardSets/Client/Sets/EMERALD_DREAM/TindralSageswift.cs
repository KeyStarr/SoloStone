using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TindralSageswift
    {
        internal const int DbfId = 115759;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                new EffectContext(g, c.Id, s.Id, stack: stack).RunLegacy(
                    new DamageTask(g.CurrentPlayer == c ? 1 : 4, EntityType.ENEMIES));
            }) };
        }
    }
}
