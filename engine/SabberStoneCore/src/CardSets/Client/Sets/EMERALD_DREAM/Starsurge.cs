using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Starsurge
    {
        internal const int DbfId = 115866;
        private static int Damage(IPlayable source) => 1 + source.Game.HistoryFacts.Query.FriendlyDeathCount(source.Controller.Id);
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Rule: one plus every friendly board death this game, including before creation; enemy deaths and discards excluded (R7).
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, Damage);
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, t?.Id, stack);
                int? target = t is Minion minion && minion.Zone == minion.Controller.BoardZone
                    && ((Playable)s).IsValidPlayTarget(minion) ? t.Id : (int?)null;
                if (!target.HasValue && ctx.History.Casts().Last(f => f.EntityId == s.Id
                    && f.Outcome == HistoryCastOutcome.Started).CasterKind != HistoryCastKind.Player)
                    target = ctx.RandomLegalTarget(s.Id);
                if (target.HasValue) ctx.Damage(target.Value, Damage((IPlayable)s), spellDamage: true);
            }) };
        }
    }
}
