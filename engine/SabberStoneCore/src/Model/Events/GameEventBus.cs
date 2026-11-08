using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Enums;

namespace SabberStoneCore.Model.Events
{
    /// <summary>A registration identity. Owner is the callback target, including detached trigger/aura instances.</summary>
    public sealed class GameEventSubscription
    {
        public long Ordinal { get; }
        public TriggerManager.TriggerHandler Handler { get; }
        public object Owner => Handler.Target;
        internal Model.Entities.IPlayable EntityOwner { get; }

        internal GameEventSubscription(long ordinal, TriggerManager.TriggerHandler handler)
        {
            Ordinal = ordinal;
            Handler = handler;
            EntityOwner = ResolutionScheduler.OwnerOf(handler.Target);
        }
    }

    /// <summary>Registration-ordered typed transport; no task drains, sorting or resolution phases.</summary>
    public sealed class GameEventBus
    {
        private readonly Dictionary<string, GameEventSubscription[]> _subscriptions = new Dictionary<string, GameEventSubscription[]>();
        private long _nextOrdinal;

        /// <summary>
        /// Diagnostic callback immediately before each subscriber, with its zero-based position
        /// in this invocation snapshot (not its registration ordinal). Not inherited by clones/snapshots.
        /// Observers must not mutate the game.
        /// </summary>
        public Action<IGameEvent, TriggerManager.TriggerHandler, int> Trace { get; set; }

        /// <summary>Immutable registration snapshot in invocation order. Ordinals are local to this bus.</summary>
        public IReadOnlyList<GameEventSubscription> GetSubscriptions(string channel) => Array.AsReadOnly(Snapshot(channel));

        private GameEventSubscription[] Snapshot(string channel) =>
            _subscriptions.TryGetValue(channel, out var entries) ? entries : Array.Empty<GameEventSubscription>();

        internal Dictionary<string, GameEventSubscription[]> Subscriptions => _subscriptions;
        internal bool Contains(string channel, GameEventSubscription entry) => Array.IndexOf(Snapshot(channel), entry) >= 0;

        internal bool HasSubscribers<T>() where T : struct, IGameEvent => Snapshot(default(T).Channel).Length != 0;

        internal void Subscribe<T>(TriggerManager.TriggerHandler handler) where T : struct, IGameEvent
        {
            if (handler == null) return;
            string channel = default(T).Channel;
            var current = Snapshot(channel);
            var added = handler.GetInvocationList();
            var next = new GameEventSubscription[current.Length + added.Length];
            Array.Copy(current, next, current.Length);
            for (int i = 0; i < added.Length; i++)
                next[current.Length + i] = new GameEventSubscription(checked(_nextOrdinal++), (TriggerManager.TriggerHandler)added[i]);
            _subscriptions[channel] = next;
        }

        internal void Unsubscribe<T>(TriggerManager.TriggerHandler handler) where T : struct, IGameEvent
        {
            if (handler == null) return;
            string channel = default(T).Channel;
            var current = Snapshot(channel);
            var removed = handler.GetInvocationList();
            // Delegate.Remove removes the LAST matching contiguous invocation subsequence.
            for (int start = current.Length - removed.Length; start >= 0; start--)
            {
                int i = 0;
                while (i < removed.Length && current[start + i].Handler.Equals(removed[i])) i++;
                if (i != removed.Length) continue;
                var next = new GameEventSubscription[current.Length - removed.Length];
                Array.Copy(current, next, start);
                Array.Copy(current, start + removed.Length, next, start, current.Length - start - removed.Length);
                _subscriptions[channel] = next;
                return;
            }
        }

        internal void Raise<T>(T value, Model.Entities.IEntity sender = null) where T : struct, IGameEvent
        {
            sender = sender ?? value.Sender;
            // Copy-on-write arrays freeze outer invocation; a nested raise reads the current array.
            var snapshot = Snapshot(value.Channel);
            bool heldPhases = value is TurnStartEvent && snapshot.Any(e =>
                e.EntityOwner?.Card.Id == "TIME_876" && e.EntityOwner.Zone == e.EntityOwner.Controller.HandZone ||
                e.EntityOwner is Enchantment a && a.Card.Id == "TIME_876e" && a.Target.Zone == a.Controller.HandZone);
            int Phase(GameEventSubscription entry) => entry.EntityOwner is Enchantment ? 0 :
                entry.EntityOwner?.Zone?.Type == Zone.HAND ? 1 : 0;
            if (heldPhases) snapshot = snapshot.OrderBy(Phase).ThenBy(e => e.EntityOwner?.OrderOfPlay ?? 0).ThenBy(e => e.Ordinal).ToArray();
            int handPhaseStart = heldPhases ? Array.FindIndex(snapshot, e => Phase(e) == 1) : -1;
            var trace = Trace;
            var scheduler = sender?.Game?.Resolution;
            if (scheduler == null)
                foreach (var entry in snapshot)
                    if (entry.EntityOwner != null) { scheduler = entry.EntityOwner.Game.Resolution; break; }
            using var frame = scheduler?.Enter(ResolutionKind.Event, sender?.Id ?? 0) ?? default;
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (i > 0 && i == handPhaseStart) sender.Game.ProcessTasks();
                var handler = snapshot[i].Handler;
                trace?.Invoke(value, handler, i);
                if (scheduler == null) { handler(sender); continue; }
                var previous = scheduler.BeginCallback(this, snapshot[i], value.Channel, sender);
                bool completed = false;
                try { handler(sender); completed = true; }
                finally { scheduler.EndCallback(previous, completed); }
            }
        }

        internal void RestoreCloneSubscriptions(GameEventBus source, Dictionary<object, object> copies, Game game)
        {
            _subscriptions.Clear();
            foreach (var channel in source._subscriptions)
            {
                var entries = new List<GameEventSubscription>(channel.Value.Length);
                foreach (var entry in channel.Value)
                {
                    var handler = TriggerManager.CloneHandlers(entry.Handler, copies, game);
                    if (handler != null) entries.Add(new GameEventSubscription(entry.Ordinal, handler));
                }
                _subscriptions.Add(channel.Key, entries.ToArray());
            }
            // Removed registrations and excluded diagnostics still consumed ordinal identities.
            _nextOrdinal = source._nextOrdinal;
        }
    }
}
