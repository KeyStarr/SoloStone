using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HeirofHereafter
    {
        internal const int DbfId = 119780;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int count = c.BoardZone.Count(m => m.Damage > 0) + c.Opponent.BoardZone.Count(m => m.Damage > 0);
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                for (int i = 0; i < count; i++) ctx.GiveBuff(s.Id, "TIME_871e");
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(2)) };
        }
    }
}
