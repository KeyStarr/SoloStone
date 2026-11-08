using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.Actions
{
    public static class PlayLockRules
    {
        private const GameTag Restriction = (GameTag)32550, Remaining = (GameTag)32551,
            LastBoundary = (GameTag)32552;
        public static bool IsPrivateTag(GameTag tag) => (int)tag >= 32550 && (int)tag <= 32552;
        private static bool Has(IEntity target, GameTag tag) => target.AppliedEnchantments?.Any(e => e[Restriction] == (int)tag) == true;
        public static bool BlocksPlay(IPlayable target) => target.Zone == target.Controller.HandZone
            && (Has(target, GameTag.CANT_PLAY) || CardSets.Client.Sets.ArrivalRules.BlocksPlay(target));
        public static bool IsSleeping(IEntity target) => target.Zone == target.Controller.BoardZone && Has(target, GameTag.CANT_ATTACK);

        public static Enchantment LockHand(IPlayable target, IPlayable source, string enchantmentId, int ownerEnds = 1)
        {
            if (target == null || target.Zone != target.Controller.HandZone) return null;
            return Attach(target, source, enchantmentId, target.Controller, GameTag.CANT_PLAY, ownerEnds);
        }

        public static Enchantment Sleep(Minion target, IPlayable source, string enchantmentId)
        {
            if (target == null || target.Zone != target.Controller.BoardZone || target.IsDormant) return null;
            return Attach(target, source, enchantmentId, source.Controller, GameTag.CANT_ATTACK,
                target.Game.CurrentPlayer == source.Controller ? 2 : 1);
        }

        private static Enchantment Attach(IPlayable target, IPlayable source, string id, Controller anchor, GameTag kind, int ends)
        {
            if (source == null || source.Game != target.Game || ends < 0) throw new ArgumentException("Invalid play-lock source/duration.");
            if (kind == GameTag.CANT_PLAY) CardStateBoundary.EnrollAttachments(target);
            var actor = Enchantment.GetInstance(anchor, source, target, Cards.FromId(id));
            actor[Restriction] = (int)kind;
            actor[Remaining] = ends;
            actor.Power.Trigger?.Activate(actor);
            Publish(target, kind);
            return actor;
        }

        public static Trigger Expiry() => new Trigger(TriggerType.TURN_END) {
            SingleTask = new CustomTask((g, c, s, t, stack) => {
                var actor = (Enchantment)s;
                // Extra end-turn effects must not age a restriction twice at the same boundary.
                if (actor[Restriction] == 0 || actor[LastBoundary] == g.Turn) return;
                actor[LastBoundary] = g.Turn;
                if (actor[Remaining] > 0 && --actor[Remaining] == 0) actor.Remove();
            })
        };

        internal static void CardPlayed(Controller controller)
        {
            // Zero owner ends means the next committed play, including a countered spell.
            foreach (var card in controller.HandZone)
                foreach (var actor in card.AppliedEnchantments?.Where(e => e[Restriction] == (int)GameTag.CANT_PLAY && e[Remaining] == 0).ToArray()
                    ?? Array.Empty<Enchantment>()) actor.Remove();
        }

        internal static void Removing(Enchantment actor)
        {
            var kind = (GameTag)actor[Restriction];
            if (kind != GameTag.CANT_PLAY && kind != GameTag.CANT_ATTACK) return;
            actor[Restriction] = 0;
            Publish((IPlayable)actor.Target, kind);
        }

        internal static void HandExit(IPlayable target)
        {
            foreach (var actor in target.AppliedEnchantments?.Where(e => e[Restriction] == (int)GameTag.CANT_PLAY).ToArray()
                ?? Array.Empty<Enchantment>()) actor.Remove();
        }

        internal static void CopyState(Enchantment source, Enchantment copy)
        {
            if (source[Restriction] == 0) return;
            foreach (var tag in new[] { Restriction, Remaining, LastBoundary }) copy[tag] = source[tag];
            // Sleep's deadline belongs to the original caster, including copies controlled by the opponent.
            if (source[Restriction] == (int)GameTag.CANT_ATTACK) copy.RebindController(source.Controller);
            copy.Power.Trigger?.Activate(copy);
        }

        private static void Publish(IPlayable target, GameTag kind)
        {
            if (target.Game.History) target.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(target.Id, kind, target[kind]));
        }
    }
}
