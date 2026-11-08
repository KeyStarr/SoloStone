using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PterrordaxEgg
    {
        internal const int DbfId = 117674;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int position = EffectContext.DeathrattlePosition(c, s);
                int summoned = ctx.Summon("TLC_831t", position);
                if (summoned == 0) return;
                var others = c.BoardZone.GetAll().Concat(c.Opponent.BoardZone.GetAll()).Where(m => m.Id != summoned).ToArray();
                foreach (var minion in others) {
                    ctx.GiveBuff(minion.Id, "TLC_831e");
                    if (minion.IsDead) ctx.Destroy(minion.Id);
                    ctx.GiveBuff(summoned, "TLC_831e2");
                }
            }) };
        }

        internal static void ConfigureLoss(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Health_N(-1)) };
        }

        internal static void ConfigureGain(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Health_N(1)) };
        }
    }
}
