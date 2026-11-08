using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Collections.Generic;
using SabberStoneCore.Enums;
using System.Linq;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BitterEnd
    {
        internal const int DbfId = 121176;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion target) || target.Zone != target.Controller.BoardZone) return;
                var ctx = new EffectContext(g, c.Id, s.Id, target.Id, stack);
                var affected = new[] { target }.Concat(target.GetAdjacentMinions()).ToArray();
                foreach (var minion in affected) ctx.RunLegacy(ComplexTask.Freeze(EntityType.TARGET), minion.Id);
                foreach (var minion in affected.Where(m => m.Damage > 0)) ctx.Destroy(minion.Id);
            }) };
        }
    }
}
