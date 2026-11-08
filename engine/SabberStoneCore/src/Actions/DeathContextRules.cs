using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    /// <summary>Opt-in last-known death state and ordered outgoing-combat kill memory.</summary>
    public static class DeathContextRules
    {
        public const GameTag LastAttack = (GameTag)31400, Captured = (GameTag)31401,
            FatalSource = (GameTag)31402, Anchor = (GameTag)31403, DeathPosition = (GameTag)31404,
            TrackCombatKills = (GameTag)31405, PendingTracker = (GameTag)31406,
            FatalOrdinal = (GameTag)31407, KillClock = (GameTag)31408,
            LeftObserver = (GameTag)31409, CaptureDeath = (GameTag)31410,
            RightObserver = (GameTag)31411, ExcessCombat = (GameTag)31412,
            ShieldHits = (GameTag)31413, ShieldAura = (GameTag)31414,
            DeathDepth = (GameTag)31415, ObserveDeathAttack = (GameTag)31416,
            AttackObserver = (GameTag)31417, PositionAnchor = (GameTag)31418, CancelAttack = (GameTag)31419;
        public const int KillKey = 31405;
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 31400 && (int)tag <= 31419;
        public static bool Active(Minion m) => m.Zone == m.Controller.BoardZone && !m.ToBeDestroyed && !m.IsSilenced && !m.Untouchable;
        public static int AttackAtDeath(IPlayable source) => source[Captured] != 0 ? source[LastAttack] : (source as ICharacter)?.AttackDamage ?? source.Card.ATK;

        internal static void CaptureBatch(Game game)
        {
            if (game.DeadMinions.Count == 0) return;
            bool observer = game.Player1.BoardZone.Concat(game.Player2.BoardZone)
                .Any(m => m[ObserveDeathAttack] != 0 && Active(m));
            foreach (var minion in game.DeadMinions.ToArray())
            {
                if (minion[Captured] != 0) continue;
                if (observer || minion[CaptureDeath] != 0 || minion[PositionAnchor] != 0)
                {
                    minion[LastAttack] = minion.AttackDamage;
                    minion[Captured] = 1;
                }
                if (minion[PositionAnchor] != 0 && !minion.IsSilenced)
                {
                    game[PositionAnchor] = 1;
                    minion[Anchor] = minion.ZonePosition + 1;
                    minion[DeathPosition] = minion.ZonePosition + 1;
                }
            }
            // Capture before ClearWeapons: a final-Durability kill belongs to that weapon.
            foreach (var victim in game.DeadMinions.Where(m => m[PendingTracker] != 0).OrderBy(m => m[FatalOrdinal]).ToArray())
            {
                if (game.IdEntityDic.TryGetValue(victim[PendingTracker], out var tracker))
                    PayloadRules.StoreCard(tracker, KillKey, victim.Card);
                victim.NativeTags.Remove(PendingTracker);
            }
        }

        internal static void RecordFatal(Minion victim, IPlayable source, bool combat)
        {
            if (victim == null || source == null || !victim.ToBeDestroyed) return;
            if (victim[CaptureDeath] != 0) victim[FatalSource] = source.Id;
            IPlayable tracker = source is Hero hero ? hero.Weapon : source;
            if (!combat || !(source is ICharacter attacker) || (!(source is Hero) && !attacker.IsAttacking) || tracker == null
                || tracker[TrackCombatKills] == 0 || tracker[GameTag.SILENCED] != 0) return;
            victim[PendingTracker] = tracker.Id;
            victim[FatalOrdinal] = ++victim.Game[KillClock];
        }

        internal static void Destroying(Minion victim)
        {
            if (victim.ToBeDestroyed) return;
            var frame = victim.Game.Resolution.Current;
            bool direct = frame.Kind == Model.Events.ResolutionKind.TaskResolution
                || frame.Kind == Model.Events.ResolutionKind.TriggerResolution || frame.Kind == Model.Events.ResolutionKind.EffectResolution;
            var source=direct && victim.Game.IdEntityDic.TryGetValue(frame.SourceId,out var resolved)?resolved:null;
            victim.Game.Facts.CaptureDestroy(victim,source);
            if(victim[CaptureDeath]!=0 && !victim.NativeTags.ContainsKey(FatalSource)) RecordDestroy(victim,source);
        }

        internal static void CopyShield(IPlayable source, IPlayable copy)
        {
            if (source[ShieldHits] == 0 || !(source is Minion original) || !original.HasDivineShield || !(copy is Minion target)) return;
            target.HasDivineShield = true;
            target[ShieldHits] = source[ShieldHits];
        }

        public static void RecordDestroy(IPlayable victim, IEntity source)
        {
            if(!victim.ToBeDestroyed) victim.Game.Facts.CaptureDestroy(victim,source);
            if (victim is Minion minion && !minion.ToBeDestroyed && minion[CaptureDeath] != 0)
                minion[FatalSource] = source?.Id ?? 0;
        }

        public static Minion[] DeathNeighbours(IEntity source)
        {
            var board = source.Controller.BoardZone;
            int gap = source[Anchor] - 1;
            if (gap < 0) return Array.Empty<Minion>();
            return new[] { gap > 0 ? board.GetSlot(gap - 1) as Minion : null,
                gap < board.SlotCount ? board.GetSlot(gap) as Minion : null }
                .Where(m => m != null && !m.Untouchable && !m.ToBeDestroyed).ToArray();
        }

        internal static void Removing(IPlayable entity)
        {
            if (entity.Game[PositionAnchor] == 0) return;
            foreach (var anchor in Anchors(entity.Game).Where(a => a.Controller == entity.Controller))
                if (entity.Id != anchor.Id && entity.ZonePosition < anchor[Anchor] - 1) anchor[Anchor]--;
        }
        internal static void Summoned(Minion minion, IEntity summoner)
        {
            if (minion.Game[PositionAnchor] == 0) return;
            foreach (var anchor in Anchors(minion.Game).Where(a => a.Controller == minion.Controller))
            {
                int gap = anchor[Anchor] - 1;
                bool fromLeft = summoner is Minion dead && dead.Zone == dead.Controller.GraveyardZone
                    && dead.LastBoardPosition < anchor[DeathPosition] - 1;
                if (minion.ZonePosition < gap || minion.ZonePosition == gap && fromLeft) anchor[Anchor]++;
            }
        }
        private static IPlayable[] Anchors(Game game) => game.IdEntityDic.Values.Where(e => e[Anchor] > 0).ToArray();

        internal static DeathScope EnterDeaths(Game game)
        {
            game[DeathDepth]++;
            return new DeathScope(game);
        }
        internal readonly struct DeathScope : IDisposable
        {
            private readonly Game _game;
            internal DeathScope(Game game) { _game = game; }
            public void Dispose()
            {
                if (--_game[DeathDepth] != 0) return;
                if (_game[PositionAnchor] != 0)
                {
                    foreach (var anchor in Anchors(_game)) anchor.NativeTags.Remove(Anchor);
                    _game.NativeTags.Remove(PositionAnchor);
                }
                _game.NativeTags.Remove(DeathDepth);
            }
        }

        // Attack scopes are synchronous; immutable saved integers never outlive the invocation.
        internal static IDisposable EnterAttack(ICharacter source)
        {
            if (!(source is Minion minion) || minion.Zone != minion.Controller.BoardZone) return null;
            var adjacent = minion.GetAdjacentMinions();
            int[] witnesses = adjacent.Where(m => m[AttackObserver] != 0 && Active(m)).Select(m => m.Id).ToArray();
            if (witnesses.Length == 0 && source[LeftObserver] == 0 && source[RightObserver] == 0) return null;
            var scope = new AttackScope(source);
            source[LeftObserver] = witnesses.Length > 0 ? witnesses[0] : 0;
            source[RightObserver] = witnesses.Length > 1 ? witnesses[1] : 0;
            return scope;
        }
        private sealed class AttackScope : IDisposable
        {
            private readonly ICharacter _source;
            private readonly int _left, _right;
            internal AttackScope(ICharacter source) { _source = source; _left = source[LeftObserver]; _right = source[RightObserver]; }
            public void Dispose()
            {
                if (_left == 0) _source.NativeTags.Remove(LeftObserver); else _source[LeftObserver] = _left;
                if (_right == 0) _source.NativeTags.Remove(RightObserver); else _source[RightObserver] = _right;
            }
        }
        internal static bool ConsumeAttackCancellation(ICharacter source)
        {
            bool cancelled = source[CancelAttack] != 0;
            source.NativeTags.Remove(CancelAttack);
            return cancelled;
        }
        internal static int AllocateExcess(ICharacter source, ICharacter target, int attack) =>
            source[ExcessCombat] != 0 && target is Minion ? Math.Max(0, attack - target.Health) : 0;
        internal static void ApplyExcess(ICharacter source, int excess)
        {
            if (excess > 0) source.Controller.Opponent.Hero.TakeDamage(source, excess);
        }
        public static void ResetShield(IEntity target) => target.NativeTags.Remove(ShieldHits);
        internal static bool KeepShield(ICharacter target)
        {
            if (!target.Controller.BoardZone.Any(m => m[ShieldAura] != 0 && m.Zone == m.Controller.BoardZone && !m.IsSilenced && !m.Untouchable)) return false;
            return ++target[ShieldHits] < 3;
        }
        internal static void ClearTransient(Minion source)
        {
            foreach (var tag in source.NativeTags.Keys.Where(IsInternalTag).ToArray()) source.NativeTags.Remove(tag);
        }
    }
}
