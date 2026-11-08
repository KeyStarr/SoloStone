using System.Collections.Generic;
using SabberStoneCore.Enums;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NascentBolt
    {
        internal const int DbfId = 120448;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion minion)) return;
                var ctx = new EffectContext(g, c.Id, s.Id, t.Id, stack);
                ctx.Damage(t.Id, 5, spellDamage: true);
                if (!minion.IsDead) ctx.Draw(2);
            }) };
        }
    }
}
