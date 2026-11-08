using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Fumigate
    {
        internal const int DbfId = 117685;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion target)) return;
                var targets = c.BoardZone.GetAll().Concat(c.Opponent.BoardZone.GetAll())
                    .Where(m => m == target || m.Card.SharesRaceWith(target.Card)).Select(m => m.Id).ToArray();
                new EffectContext(g, c.Id, s.Id, stack: stack).DamageBatch(targets, 3, true);
            }) };
        }
    }
}
