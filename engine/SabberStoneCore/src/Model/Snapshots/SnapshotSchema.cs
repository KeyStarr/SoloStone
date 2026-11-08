using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using SabberStoneCore.Model.Events;

namespace SabberStoneCore.Model.Snapshots
{
    // Closed engine-owned schema. Input cannot name/load CLR types, assemblies or methods.
    // Field-layout fingerprints reject incompatible engine revisions instead of losing new state.
    internal static class SnapshotSchema
    {
        // Append extensions after the original sorted table: historical definition bytes use these IDs.
        private static readonly Type[] Extensions = new[] { typeof(Rewind.RewindTransaction), typeof(Rewind.RewindStage),
            typeof(Kettle.PowerHistoryResetGame) }
            // Published Kettle options retain their executable task cache, including abstract list element types.
            .Concat(typeof(Game).Assembly.GetTypes().Where(t => t.Namespace == "SabberStoneCore.Tasks.PlayerTasks" && t != typeof(Tasks.PlayerTasks.ActivateOldGodTask))
                .OrderBy(t => t.FullName, StringComparer.Ordinal))
            .Concat(new[] { typeof(Tasks.SimpleTasks.LeylineRepeatTask) })
            // Card-state extensions also stay outside the historical sorted type IDs.
            .Concat(new[] { typeof(DormantDefinition), typeof(Actions.ShatterDefinition),
                typeof(ShatterEvent), typeof(Actions.HandProgressDefinition), typeof(HandProgressMode),
                typeof(Zones.HandZone).GetNestedType("CardStateScope", BindingFlags.NonPublic) })
            .Concat(new[] { typeof(Tasks.SimpleTasks.MoonGrantTask) })
            // Main's history extensions must not renumber historical card-definition type IDs.
            .Concat(new[] { typeof(PoweredUp), typeof(Kettle.PowerHistorySubSpellStart), typeof(Kettle.PowerHistorySubSpellEnd),
                typeof(Kettle.PowerHistory).GetNestedType("BlockFrame", BindingFlags.NonPublic),
                typeof(Kettle.PowerHistory).GetNestedType("EffectSourceScope", BindingFlags.NonPublic),
                typeof(Kettle.PowerHistory).GetNestedType("HandBuffScope", BindingFlags.NonPublic) })
            // The legacy glow helper must not shift historical definition type IDs.
            .Concat(new[] { typeof(LegacyPoweredUpRules) })
            // Nor the split-hit helper (Tasks.SimpleTasks is namespace-discovered).
            .Concat(new[] { typeof(Tasks.SimpleTasks.SplitHits) })
            .Concat(new[] { typeof(Kettle.PowerHistory).GetNestedType("VisualScope", BindingFlags.NonPublic) })
            .Concat(new[] { typeof(Tasks.SimpleTasks.SummonCompanionTask) })
            .Concat(new[] { typeof(CardDiscardedFact), typeof(CardPlayCompletedFact), typeof(MinionRebornFact), typeof(CardSets.Client.Sets.DukeOfBelow.DiscardScalingAura) })
            .Concat(new[] { typeof(Tasks.PlayerTasks.ActivateOldGodTask) })
            // Query-only metadata must not renumber historical card-definition types.
            .Concat(new[] { typeof(PoolDestination), typeof(Kettle.PowerHistoryShuffleDeck) })
            // Achievement evidence is additive; retain every published type id before it.
            .Concat(new[] { typeof(HistoryDeathCause),typeof(HistoryDamageCause),typeof(HistoryBurnReason),typeof(HistoryKeyword),typeof(HistoryBoardLeaveReason),
                typeof(ControlChangedFact),typeof(CardBurnedFact),typeof(KeywordAppliedFact),typeof(CorpseSpentFact),typeof(DeathrattleResolvedFact),
                typeof(SpellCounteredFact),typeof(ExtraTurnGrantedFact),typeof(ExtraTurnStartedFact),typeof(ComboActivatedFact),typeof(MaxManaChangedFact),
                typeof(HeroFatalFact),typeof(SpellBodyStartedFact),typeof(MinionLeftPlayFact),typeof(MinionFormChangedFact),typeof(MinionAwakenedFact),
                typeof(CardTransferredFact),typeof(MatchTerminalFact),typeof(HistoryPlayerActionKind),typeof(PlayerActionFact),typeof(CombatStartedFact) })
            // Recorder scopes also live in the discovered Model namespace: append them rather than shift published IDs.
            .Concat(new[] { typeof(HistoryRecorder.PlayerActionScope),
                typeof(HistoryRecorder).GetNestedType("TransferScope", BindingFlags.NonPublic) })
            // History-only effect plays: append, never renumber published definition type IDs.
            .Concat(new[] { typeof(Kettle.PowerHistoryEffectPlayStart) })
            .Concat(new[] { typeof(Kettle.PowerHistoryRetainedOverdraw) }).ToArray();
        private static readonly string[] Namespaces = {
            "SabberStoneCore.Model", "SabberStoneCore.Model.Entities", "SabberStoneCore.Model.Zones",
            "SabberStoneCore.Config", "SabberStoneCore.Enums", "SabberStoneCore.Enchants",
            "SabberStoneCore.Auras", "SabberStoneCore.Triggers", "SabberStoneCore.Conditions",
            "SabberStoneCore.Tasks", "SabberStoneCore.Tasks.SimpleTasks", "SabberStoneCore.Kettle", "SabberStoneCore.Model.Effects"
        };
        private static readonly ConcurrentDictionary<Type, FieldInfo[]> FieldCache = new ConcurrentDictionary<Type, FieldInfo[]>();
        private static readonly Type[] Types = typeof(Game).Assembly.GetTypes()
            // Installed meta metadata is not game state; preserve historical definition type IDs.
            .Where(t => t != typeof(OfflinePoolPolicy.Meta) && !Extensions.Contains(t) && Namespaces.Contains(t.Namespace) && !t.IsDefined(typeof(CompilerGeneratedAttribute), false) &&
                !t.Name.Contains("<") && !typeof(Delegate).IsAssignableFrom(t))
            .Concat(RegisteredDefinitionTypes())
            // Deliberately admitted cost state, outside the namespace-discovered schema.
            .Concat(new[] { typeof(Costs.CostRuleState), typeof(Costs.EntityCostState), typeof(Costs.CostModifier), typeof(Costs.CostPaymentOverride),
                typeof(Costs.CostOneUseRule), typeof(Costs.CostPaymentKind) })
            .Concat(new[] { typeof(GameEventBus), typeof(GameEventSubscription), typeof(ResolutionScheduler), typeof(bool), typeof(byte), typeof(int), typeof(long), typeof(ulong), typeof(float),
                typeof(double), typeof(string), typeof(object), typeof(DateTime), typeof(TimeSpan),
                typeof(List<>), typeof(Dictionary<,>), typeof(Queue<>), typeof(Stack<>), typeof(HashSet<>),
                typeof(KeyValuePair<,>), typeof(ValueTuple<,>), typeof(ValueTuple<,,>), typeof(Nullable<>),
                typeof(System.Collections.ObjectModel.ReadOnlyCollection<>), typeof(System.Collections.ObjectModel.ReadOnlyDictionary<,>) })
            .Except(Extensions).Distinct().OrderBy(t => t.FullName, StringComparer.Ordinal).Concat(Extensions).ToArray();
        private static readonly Dictionary<Type, int> TypeIds = Types.Select((t, i) => (t, i)).ToDictionary(p => p.t, p => p.i);
        internal static readonly string Fingerprint = Hash(string.Join("\n", Types.Select(t => t.FullName + ":" +
            string.Join(",", Fields(t).Select(f => f.DeclaringType.FullName + "." + f.Name + ":" + f.FieldType)))));

        // Only installed definition graphs may extend the closed schema, never input type names.
        private static IEnumerable<Type> RegisteredDefinitionTypes()
        {
            var types = new HashSet<Type>();
            var seen = new HashSet<object>(ReferenceEquality.Instance);
            void Visit(object value)
            {
                if (value == null || value is string || value is Delegate || !seen.Add(value)) return;
                if (value is IDictionary dictionary)
                {
                    foreach (DictionaryEntry entry in dictionary) { Visit(entry.Key); Visit(entry.Value); }
                    return;
                }
                if (value is IEnumerable items && (value.GetType().IsArray || value.GetType().Assembly != typeof(Game).Assembly))
                {
                    foreach (object item in items) Visit(item);
                    return;
                }
                var type = value.GetType();
                if (!DefinitionObject(type) || type.IsEnum) return;
                if (type.Assembly == typeof(Game).Assembly) types.Add(type);
                foreach (var field in Fields(type)) Visit(field.GetValue(value));
            }
            foreach (var card in Cards.All) Visit(card);
            return types;
        }

        internal static bool DefinitionObject(Type type) => type.Assembly == typeof(Game).Assembly
            || type.IsGenericType && new[] { typeof(ValueTuple<,>), typeof(ValueTuple<,,>),
                typeof(KeyValuePair<,>), typeof(Nullable<>) }.Contains(type.GetGenericTypeDefinition());

        internal static FieldInfo[] Fields(Type type) => FieldCache.GetOrAdd(type, t => {
            var fields = new List<FieldInfo>();
            for (; t != null && t != typeof(object); t = t.BaseType)
                fields.AddRange(t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(f => !f.IsDefined(typeof(NonSerializedAttribute)) && !(f.DeclaringType == typeof(GameEventBus) && f.Name == "<Trace>k__BackingField")));
            return fields.OrderBy(f => f.DeclaringType.FullName, StringComparer.Ordinal).ThenBy(f => f.Name, StringComparer.Ordinal).ToArray();
        });

        internal static int Id(Type type)
        {
            if (TypeIds.TryGetValue(type, out int id)) return id;
            throw new NotSupportedException("Unsupported snapshot state/continuation recipe: " + type.FullName);
        }

        internal static Type Type(int id)
        {
            if (id < 0 || id >= Types.Length) throw new SerializationException("Unknown snapshot type key.");
            return Types[id];
        }

        private static string Hash(string value)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }
    }
}
