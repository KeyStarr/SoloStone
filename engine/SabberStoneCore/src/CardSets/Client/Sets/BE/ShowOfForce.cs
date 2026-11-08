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
    internal static class ShowOfForce
    {
        internal const int DbfId = 129942;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.PoweredUpWhen(GainedArmorThisTurn);
            // Rule: any positive own Armor gain this global turn qualifies, even if lost; positive SET/replacement deltas follow R5.
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, t?.Id, stack);
                int? target = t is Minion minion && minion.Zone == minion.Controller.BoardZone
                    && ((Playable)s).IsValidPlayTarget(minion) ? t.Id : (int?)null;
                // Player casts retain a lost target; only automatic casts choose a replacement.
                if (!target.HasValue && ctx.History.Casts().Last(f => f.EntityId == s.Id
                    && f.Outcome == HistoryCastOutcome.Started).CasterKind != HistoryCastKind.Player)
                    target = ctx.RandomLegalTarget(s.Id);
                if (!target.HasValue) return;
                if (GainedArmorThisTurn((IPlayable)s))
                    ctx.Destroy(target.Value);
                else
                    ctx.Damage(target.Value, 3, spellDamage: true);
            }) };
        }

        private static bool GainedArmorThisTurn(IPlayable source)
        {
            var history = source.Game.HistoryFacts.Query;
            return history.ArmorGains(history.ThisTurn).Any(f => f.ControllerId == source.Controller.Id);
        }
    }
}
