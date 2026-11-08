using System;
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
    internal static class SindragosasTriumph
    {
        internal const int DbfId = 123847;
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
                var target = t as Minion;
                bool valid = target != null && target.Zone == target.Controller.BoardZone && ((Playable)s).IsValidPlayTarget(target);
                if (!valid && ctx.History.Casts().Last(f => f.EntityId == s.Id
                    && f.Outcome == HistoryCastOutcome.Started).CasterKind != HistoryCastKind.Player)
                {
                    int? selected = ctx.RandomLegalTarget(s.Id);
                    if (!selected.HasValue) return;
                    target = (Minion)g.IdEntityDic[selected.Value];
                    valid = true;
                }
                if (!valid) return;
                int outgoing = (8 + SpellDamageRules.Bonus((SabberStoneCore.Model.Entities.IPlayable)s))
                    * (int)Math.Pow(2, c.ControllerAuraEffects[GameTag.SPELLPOWER_DOUBLE]);
                int excess = Math.Max(0, outgoing - target.Health);
                ctx.Damage(target.Id, 8, spellDamage: true);
                if (excess == 0 || c.HandZone.IsEmpty) return;
                var chosen = (Playable)c.HandZone[g.Random.Next(c.HandZone.Count)];
                CostRules.AddModifier(chosen, EffectOperator.SUB, excess);
                ctx.GiveBuff(chosen.Id, "CATA_978e", excess);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            // CostRules owns the permanent scalar; this exported actor presents its source and amount.
            card.Power = new Power();
        }
    }
}
