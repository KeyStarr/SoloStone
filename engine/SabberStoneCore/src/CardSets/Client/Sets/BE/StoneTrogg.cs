using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Collections.Generic;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class StoneTrogg
    {
        internal const int DbfId = 131454;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion target)) return;
                var ctx = new EffectContext(g, c.Id, s.Id, target.Id, stack);
                ctx.Damage(target.Id, 6);
                if (target.IsDead) return;
                ctx.GiveBuff(s.Id, "BE_994e");
                ((Minion)s).HasTaunt = true;
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Health_N(6)) };
        }
    }
}
