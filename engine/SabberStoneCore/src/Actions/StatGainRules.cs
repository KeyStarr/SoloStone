using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;

namespace SabberStoneCore.Actions
{
    public static class StatGainRules
    {
        private const GameTag OperationDepth = (GameTag)31880;
        // These are immutable card recipes, never game-owned listeners or state.
        private static readonly Dictionary<string, Action<Minion, StatGainedEvent>> Listeners = new Dictionary<string, Action<Minion, StatGainedEvent>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Action<Minion, Enchantment>> EntrySetters = new Dictionary<string, Action<Minion, Enchantment>>(StringComparer.Ordinal);

        public static void DeclareListener(Card card, Action<Minion, StatGainedEvent> recipe) => Listeners.Add(card.Id, recipe);
        public static void DeclareEntrySetter(Card card, Action<Minion, Enchantment> recipe) => EntrySetters.Add(card.Id, recipe);
        internal static bool RetainActor(Card card) => EntrySetters.ContainsKey(card.Id);
        public static bool IsInternalTag(GameTag tag) => tag == OperationDepth;

        public static Operation Begin(IEntity target, IEntity source = null, Card effect = null) =>
            target is Minion minion ? new Operation(minion, source, effect) : default;

        public readonly struct Operation : IDisposable
        {
            private readonly Minion _target;
            private readonly IEntity _source;
            private readonly Card _effect;
            private readonly int _attack, _health;
            private readonly Zone _zone;
            internal Operation(Minion target, IEntity source, Card effect)
            {
                _target = target;
                _source = source ?? target.Game.CurrentEventData?.EventSource;
                _effect = effect;
                _attack = target._modifiedATK ?? target.Card.ATK;
                _health = target._modifiedHealth ?? target.Card.Health;
                _zone = target.Zone?.Type ?? Zone.INVALID;
                target.NativeTags[OperationDepth] = target[OperationDepth] + 1;
            }
            public void Dispose()
            {
                if (_target == null) return;
                int depth = _target[OperationDepth] - 1;
                if (depth > 0) { _target.NativeTags[OperationDepth] = depth; return; }
                _target.NativeTags.Remove(OperationDepth);
                int attack = (int)Math.Min(int.MaxValue, Math.Max(0L, (long)(_target._modifiedATK ?? _target.Card.ATK) - _attack));
                int health = (int)Math.Min(int.MaxValue, Math.Max(0L, (long)(_target._modifiedHealth ?? _target.Card.Health) - _health));
                if ((attack > 0 || health > 0) && EligibleZone(_target) && _target.Zone.Type == _zone)
                    _target.Game.TriggerManager.OnStatGained(new StatGainedEvent(_target, _source, _effect, _zone, attack, health));
            }
        }

        private static bool EligibleZone(Minion m) =>
            m.Zone == m.Controller.BoardZone || m.Zone == m.Controller.HandZone || m.Zone == m.Controller.DeckZone;

        private static bool Active(Minion m) => !m.IsDead && EligibleZone(m);

        internal static void AfterGain(StatGainedEvent value)
        {
            var target = (Minion)value.Sender;
            var owners = target.Game.Player1.BoardZone.Concat(target.Game.Player2.BoardZone)
                .OrderBy(m => m.OrderOfPlay).ToList();
            if (target.Zone != target.Controller.BoardZone) owners.Add(target);
            foreach (var owner in owners)
                if (Active(owner) && !owner.IsSilenced && !owner.IsDormant && Listeners.TryGetValue(owner.Card.Id, out var recipe))
                    recipe(owner, value);
        }

        internal static void BeforeEntry(Minion minion)
        {
            if (minion.Controller.BoardZone.IsFull) return;
            foreach (var actor in minion.Controller.AppliedEnchantments?.ToArray() ?? Array.Empty<Enchantment>())
                if (EntrySetters.TryGetValue(actor.Card.Id, out var setter)) setter(minion, actor);
        }
    }
}
