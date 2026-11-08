using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.Actions
{
    public static class PastColossalCombat
    {
        // Preserve legacy freezer replays; the new Wings opt in to Silence-aware Freeze.
        public static bool SilencedWing(Minion minion) => minion.IsSilenced && (minion.Card.Id == "NX2_037t" || minion.Card.Id == "NX2_037t2");

        public static void Force(Minion attacker, ICharacter target)
        {
            var g = attacker.Game;
            var previous = g.CurrentEventData;
            try { Generic.AttackBlock(attacker.Controller, attacker, target, true, false); }
            finally { g.CurrentEventData = previous; }
        }
        internal static bool Replace(ICharacter source, bool forced)
        {
            if (source.Card.Id != "TID_712" || !source.Controller.BoardZone.Any(m => m.Card.Id == "TID_712" && !m.IsSilenced && !m.IsDead && !m.IsDormant)) return false;
            var hands = source.Controller.BoardZone.Where(m => (m.Card.Id == "TID_712t" || m.Card.Id == "TID_712t2") && !m.IsDead && !m.IsDormant).ToArray();
            if (hands.Length == 0) return false;
            var target = (ICharacter)source.Game.CurrentEventData.EventTarget;
            source.IsAttacking = false; target.IsDefending = false;
            if (!forced) {
                source.NumAttacksThisTurn++;
                if (source.NumAttacksThisTurn >= CombatRules.AttackLimit(source)) source.IsExhausted = true;
            }
            foreach (var hand in hands) {
                if (target.IsDead || target.Zone?.Type != Zone.PLAY || source.Game.State == State.COMPLETE) break;
                if (hand.Zone == source.Controller.BoardZone && !hand.IsDead && !hand.IsDormant) Force(hand, target);
            }
            return true;
        }
    }
}
