using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FloatingLessons
    {
        internal const int DbfId = 129805;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, t?.Id, stack);
                var cast = ctx.History.Casts().Last(f => f.EntityId == s.Id && f.Outcome == HistoryCastOutcome.Started);
                var target = t as Minion;
                bool valid = target != null && target.Zone == target.Controller.BoardZone && ((Playable)s).IsValidPlayTarget(target);
                if (!valid && cast.CasterKind != HistoryCastKind.Player)
                {
                    int? selected = ctx.RandomLegalTarget(s.Id);
                    // Lost repeat targets suppress only the board buff; automatic casts still need a target.
                    if (!selected.HasValue && !RepeatRules.IsRepeating(s)) return;
                    target = selected.HasValue ? (Minion)g.IdEntityDic[selected.Value] : null;
                    valid = target != null;
                }
                int amount = 2 * (cast.CasterKind == HistoryCastKind.Player
                    ? s[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] : ((IPlayable)s).Cost);
                if (valid)
                    ctx.GiveBuff(target.Id, "BE_094e", amount);
                var hand = c.HandZone.OfType<Minion>().ToArray();
                if (hand.Length != 0) ctx.GiveBuff(hand[g.Random.Next(hand.Length)].Id, "BE_094e", amount);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(0)) { UseScriptTag = true } };
        }
    }
}
