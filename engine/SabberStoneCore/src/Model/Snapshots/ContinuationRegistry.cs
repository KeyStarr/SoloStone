using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using SabberStoneCore.Auras;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Triggers;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.Model.Snapshots
{
    // Definition paths are recipes, not serialized closures. A load resolves them against the
    // installed card definitions. Unknown runtime closures deliberately have no fallback.
    internal sealed class ContinuationRegistry
    {
        internal static readonly Lazy<ContinuationRegistry> Instance = new Lazy<ContinuationRegistry>(() => new ContinuationRegistry());
        private readonly Dictionary<Delegate, string> _keys = new Dictionary<Delegate, string>();
        private readonly Dictionary<string, Delegate> _definitions = new Dictionary<string, Delegate>(StringComparer.Ordinal);
        private readonly Dictionary<string, (Type type, MethodInfo method)> _handlers = new Dictionary<string, (Type, MethodInfo)>();
        private readonly Dictionary<string, Type> _recipes = new Dictionary<string, Type>(StringComparer.Ordinal);
        private readonly Dictionary<Type, string> _recipeKeys = new Dictionary<Type, string>();

        private ContinuationRegistry()
        {
            foreach (Type type in typeof(Game).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(ISimpleTask).IsAssignableFrom(t) &&
                (t.Namespace == "SabberStoneCore.Tasks" || t.Namespace == "SabberStoneCore.Tasks.SimpleTasks")))
            {
                string key = "legacy-task/" + type.Name + "/v1";
                _recipes.Add(key, type);
                _recipeKeys.Add(type, key);
            }
            var seen = new HashSet<object>(ReferenceEquality.Instance);
            foreach (Card card in Cards.All.OrderBy(c => c.Id, StringComparer.Ordinal))
                Visit(card, "card/" + card.Id + "/v1", seen);
            foreach (var recipe in Actions.AssembleRules.SummonRecipes)
            {
                _keys.Add(recipe.Run, recipe.Key);
                _definitions.Add(recipe.Key, recipe.Run);
            }
            // Bus entries persist their ordinal beside these recipes; callback targets are graph
            // references, so restored subscription owners and trigger/aura owners stay identical.
            Handler<TriggerManager.TriggerHandler>("trigger/process/v1", typeof(Trigger), "Process");
            Handler<TriggerManager.TriggerHandler>("trigger/validate/v1", typeof(Trigger), "Validate");
            Handler<TriggerManager.TriggerHandler>("aura/remove/v1", typeof(Aura), "TriggeredRemove");
            Handler<TriggerManager.TriggerHandler>("cost/update/v1", typeof(AdaptiveCostEffect), "Trigger");
            Handler<TriggerManager.TriggerHandler>("cost/remove/v1", typeof(AdaptiveCostEffect), "RemoveAtEnd");
            Handler<TriggerManager.TriggerHandler>("switch/on/v1", typeof(SwitchingAura), "TurnOn");
            Handler<TriggerManager.TriggerHandler>("switch/off/v1", typeof(SwitchingAura), "TurnOff");
            Handler<Action<IPowerHistoryEntry>>("history/add/v1", typeof(PowerHistory), "Add");
            Handler<EntityChangedEventHandler>("game/step/v1", typeof(GameEventManager), "EntityChangedEvent");
        }

        private void Handler<T>(string key, Type owner, string method) where T : Delegate
            => _handlers.Add(key, (typeof(T), owner.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing snapshot handler " + key)));

        private void Visit(object value, string path, HashSet<object> seen)
        {
            if (value == null || value is string || value.GetType().IsPrimitive || value.GetType().IsEnum || !seen.Add(value)) return;
            if (value is Delegate callback)
            {
                if (!_keys.ContainsKey(callback))
                {
                    _keys.Add(callback, path);
                    _definitions.Add(path, callback);
                }
                return;
            }
            if (value is IDictionary dictionary)
            {
                int i=0;
                foreach (DictionaryEntry entry in dictionary)
                {
                    Visit(entry.Key, path + "/key/" + i, seen);
                    Visit(entry.Value, path + "/value/" + i++, seen);
                }
                return;
            }
            if (value is IEnumerable items && (value.GetType().IsArray || value.GetType().Assembly != typeof(Game).Assembly))
            {
                int i = 0;
                foreach (object item in items) Visit(item, path + "/" + i++, seen);
                return;
            }
            if (!SnapshotSchema.DefinitionObject(value.GetType())) return;
            if (value is ISimpleTask && !_recipeKeys.ContainsKey(value.GetType()))
            {
                _recipeKeys.Add(value.GetType(), path);
                _recipes.Add(path, value.GetType());
            }
            foreach (FieldInfo field in SnapshotSchema.Fields(value.GetType()))
                Visit(field.GetValue(value), path + "/" + field.Name, seen);
        }

        internal string Key(Delegate callback)
        {
            if (_keys.TryGetValue(callback, out string key)) return key;
            foreach (var pair in _handlers)
                if (callback.GetType() == pair.Value.type && callback.Method == pair.Value.method)
                    return pair.Key;
            throw new NotSupportedException("Unsupported snapshot continuation: " + callback.Method.DeclaringType?.FullName + "." + callback.Method.Name);
        }

        internal bool IsDefinition(string key) => _definitions.ContainsKey(key);
        internal bool IsDefinition(Delegate callback) => _keys.ContainsKey(callback);

        internal string TaskKey(Type type) => _recipeKeys.TryGetValue(type, out string key) ? key :
            throw new NotSupportedException("Unregistered snapshot task recipe: " + type.FullName);

        internal Type TaskType(string key) => _recipes.TryGetValue(key, out Type type) ? type :
            throw new System.Runtime.Serialization.SerializationException("Unknown snapshot task recipe: " + key);

        internal Delegate Resolve(string key, object owner)
        {
            if (_definitions.TryGetValue(key, out Delegate definition))
            {
                if (owner != null) throw new System.Runtime.Serialization.SerializationException("Definition recipe cannot have a runtime owner.");
                return definition;
            }
            if (!_handlers.TryGetValue(key, out var handler) || owner == null || !handler.method.DeclaringType.IsInstanceOfType(owner))
                throw new System.Runtime.Serialization.SerializationException("Unknown snapshot continuation or invalid owner: " + key);
            return Delegate.CreateDelegate(handler.type, owner, handler.method);
        }
    }

    internal sealed class ReferenceEquality : IEqualityComparer<object>
    {
        internal static readonly ReferenceEquality Instance = new ReferenceEquality();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
