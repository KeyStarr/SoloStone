using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using SabberStoneCore.Config;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.Model.Snapshots
{
    // A bounded, reference-preserving field codec for the closed legacy schema. Materialization
    // uses only collection/empty-manager constructors, never gameplay tasks or aura activations.
    // Collections store logical contents (including order), never CLR hash tables/capacities.
    internal sealed class SnapshotCodec
    {
        private const int MaxItems = 1000000;
        internal const int MaxBytes = 64 * 1024 * 1024;
        private readonly Dictionary<object, int> _written = new Dictionary<object, int>(ReferenceEquality.Instance);
        private readonly List<object> _read = new List<object>();
        private readonly ContinuationRegistry _continuations = ContinuationRegistry.Instance.Value;
        // 0: historical; 1: batch-13; 2: Health cost; 3: displays; 4: held forms/pools; 5: targeting; 6: listener trigger predicates; 7: Discover fallback.
        private byte _definitionVersion = 7;
        private bool _definitionNeedsUpgrade;
        private bool _definitionNeedsTargetingUpgrade;
        private bool _definitionNeedsTriggerUpgrade;
        private bool _definitionNeedsDiscoverUpgrade;
        private BinaryWriter _writer;
        private BinaryReader _reader;
        private Game _restoreRoot;
        private readonly Dictionary<Type, object> _runtimeServices;
        private readonly bool? _runtimeLogging;
        private enum Kind : byte { Null, Scalar, Reference, Object, Collection, Callback, CardDefinition, TaskRecipe, Bytes }

        internal SnapshotCodec(Game rewindRuntime = null)
        {
            if (rewindRuntime == null) return;
            _runtimeLogging = rewindRuntime.Logging;
            _runtimeServices = new Dictionary<Type, object> {
                [typeof(PowerHistory)] = rewindRuntime.PowerHistory,
                [typeof(Util.DeepCloneableRandom)] = rewindRuntime.Random,
                [typeof(Queue<LogEntry>)] = rewindRuntime.Logs
            };
        }

        internal static string EncodeDefinition(Card card, bool detached = false)
        {
            byte version = card.HasBoardHistoryDisplays ? (byte)3 : card.HealthCostCondition != null ? (byte)2 :
                card.Dormant == null && card.Shatter == null && card.HandProgress == null
                && !card.CopyHandTrigger && !card.ExpiresOnHandExit && card.Power?.LocationActivation?.TargetCondition == null ? (byte)0 : (byte)1;
            return EncodeDefinition(detached ? card.Clone() : card, version);
        }

        private static string EncodeDefinition(Card card, byte version)
        {
            var codec = new SnapshotCodec { _definitionVersion = version };
            using (var stream = new MemoryStream())
            using (codec._writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                // Preserve both published layouts. FF FF <version> cannot be an old root record kind.
                if (codec._definitionVersion > 0) codec._writer.Write((byte)255);
                if (codec._definitionVersion > 1) { codec._writer.Write((byte)255); codec._writer.Write(codec._definitionVersion); }
                codec.Write(card, 0);
                // Detect extensions throughout nested recipes; retain old bytes when all new fields are default.
                if (codec._definitionNeedsDiscoverUpgrade) return EncodeDefinition(card, (byte)7);
                if (codec._definitionNeedsTriggerUpgrade) return EncodeDefinition(card, (byte)6);
                if (codec._definitionNeedsTargetingUpgrade) return EncodeDefinition(card, (byte)5);
                if (codec._definitionNeedsUpgrade) return EncodeDefinition(card, (byte)4);
                return Convert.ToBase64String(stream.ToArray());
            }
        }
        internal static Card DecodeDefinition(string data)
        {
            var bytes = Convert.FromBase64String(data);
            if (bytes.Length > MaxBytes) throw new SerializationException("History definition exceeds size limit.");
            var codec = new SnapshotCodec { _definitionVersion = bytes.Length > 0 && bytes[0] == 255 ? (byte)1 : (byte)0 };
            using (var stream = new MemoryStream(bytes, false))
            using (codec._reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                if (codec._definitionVersion > 0)
                {
                    codec._reader.ReadByte();
                    if (bytes.Length > 1 && bytes[1] == 255)
                    {
                        codec._reader.ReadByte();
                        if (bytes.Length < 3 || (codec._definitionVersion = codec._reader.ReadByte()) < 2 || codec._definitionVersion > 7)
                            throw new SerializationException("Unsupported history definition version.");
                    }
                }
                var card = codec.Read(0) as Card;
                if (card == null || stream.Position != stream.Length) throw new SerializationException("Invalid history definition.");
                return card;
            }
        }

        internal byte[] Save(Game game)
        {
            using (var stream = new MemoryStream())
            using (_writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                _writer.Write("SabberStone/decision-snapshot");
                var reveals = game.PowerHistory?.SnapshotHandReveals();
                bool hasReveals = reveals != null && reveals.Count != 0;
                _writer.Write(hasReveals ? 4 : game.EnableCorpses ? 3 : 2);
                _writer.Write(SnapshotSchema.Fingerprint);
                _writer.Write(typeof(Game).Module.ModuleVersionId.ToString("N"));
                if (hasReveals || game.EnableCorpses) _writer.Write(game.EnableCorpses);
                var manifest = game.OfflinePools ?? throw new SerializationException("Missing snapshot offline pool manifest.");
                _writer.Write(manifest.PolicyVersion);
                _writer.Write(manifest.Hash);
                Write(game, 0);
                if (hasReveals) Write(reveals, 0);
                if (stream.Length > MaxBytes) throw new NotSupportedException("Snapshot exceeds size limit.");
                return stream.ToArray();
            }
        }

        internal Game Load(byte[] data, Game restoreRoot = null, OfflinePoolManifest pools = null)
        {
            _restoreRoot = restoreRoot;
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length > MaxBytes) throw new SerializationException("Snapshot exceeds size limit.");
            try
            {
                using (var stream = new MemoryStream(data, false))
                using (_reader = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    if (_reader.ReadString() != "SabberStone/decision-snapshot")
                        throw new SerializationException("Unsupported snapshot format.");
                    int version = _reader.ReadInt32();
                    if ((version != 2 && version != 3 && version != 4) || _reader.ReadString() != SnapshotSchema.Fingerprint ||
                        _reader.ReadString() != typeof(Game).Module.ModuleVersionId.ToString("N"))
                        throw new SerializationException("Unsupported snapshot format or engine schema.");
                    byte corpseFlag = version >= 3 ? _reader.ReadByte() : (byte)0;
                    if (corpseFlag > 1) throw new SerializationException("Invalid snapshot Corpse configuration.");
                    string policyVersion = _reader.ReadString();
                    string manifestHash = _reader.ReadString();
                    var manifest = pools ?? restoreRoot?.OfflinePools ?? OfflinePoolManifest.ValidateInstalled(policyVersion, manifestHash);
                    if (manifest.PolicyVersion != policyVersion || manifest.Hash != manifestHash)
                        throw new SerializationException("Snapshot offline pool policy/hash missing or mismatched.");
                    var game = Read(0) as Game ?? throw new SerializationException("Snapshot root must be a game.");
                    if (version == 4) {
                        var reveals = Read(0) as Dictionary<int, PowerHistoryEntity>
                            ?? throw new SerializationException("Missing public hand observations.");
                        if (_runtimeServices == null) game.PowerHistory.RestoreHandReveals(reveals);
                    }
                    if (stream.Position != stream.Length) throw new SerializationException("Trailing snapshot data.");
                    game.RestoreSnapshotCorpses(corpseFlag == 1);
                    game.RestoreOfflinePools(manifest);
                    return game;
                }
            }
            catch (Exception e) when (e is EndOfStreamException || e is ArgumentException || e is InvalidCastException || e is TargetInvocationException)
            {
                throw new SerializationException("Invalid snapshot data.", e);
            }
        }

        private void WriteType(Type type)
        {
            if (type.IsArray) { _writer.Write(-1); WriteType(type.GetElementType()); return; }
            _writer.Write(SnapshotSchema.Id(type.IsGenericType ? type.GetGenericTypeDefinition() : type));
            if (type.IsGenericType)
                foreach (Type argument in type.GetGenericArguments()) WriteType(argument);
        }

        private Type ReadType(int depth = 0)
        {
            if (depth > 20) throw new SerializationException("Snapshot type nesting limit.");
            int id = _reader.ReadInt32();
            if (id == -1) return ReadType(depth + 1).MakeArrayType();
            Type type = SnapshotSchema.Type(id);
            return type.IsGenericTypeDefinition
                ? type.MakeGenericType(type.GetGenericArguments().Select(_ => ReadType(depth + 1)).ToArray()) : type;
        }

        private static bool Scalar(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string) ||
            type == typeof(DateTime) || type == typeof(TimeSpan);

        private static bool Collection(Type type) => type.IsArray || type.IsGenericType && new[] {
            typeof(List<>), typeof(Dictionary<,>), typeof(Queue<>), typeof(Stack<>), typeof(HashSet<>),
            typeof(System.Collections.ObjectModel.ReadOnlyCollection<>), typeof(System.Collections.ObjectModel.ReadOnlyDictionary<,>) }.Contains(type.GetGenericTypeDefinition());

        private void Write(object value, int depth)
        {
            if (depth > 256 || _written.Count > MaxItems) throw new NotSupportedException("Snapshot state exceeds graph limits.");
            if (value == null) { _writer.Write((byte)Kind.Null); return; }
            Type type = value.GetType();
            // Legacy card scripts use reference equality (notably Kazakus exclusion sampling).
            // Only generated cards are materialized; catalogue identities must stay canonical.
            if (value is Card card && (ReferenceEquals(card, Card.CardGame) || ReferenceEquals(card, Card.CardPlayer) ||
                ReferenceEquals(card, Cards.FromId(card.Id))))
            {
                _writer.Write((byte)Kind.CardDefinition);
                _writer.Write(ReferenceEquals(card, Card.CardGame) ? "$game" : ReferenceEquals(card, Card.CardPlayer) ? "$player" : card.Id);
                return;
            }
            if (Scalar(type))
            {
                _writer.Write((byte)Kind.Scalar);
                WriteType(type);
                if (type == typeof(string)) _writer.Write((string)value);
                else if (type == typeof(bool)) _writer.Write((bool)value);
                else if (type == typeof(float)) _writer.Write((float)value);
                else if (type == typeof(double)) _writer.Write((double)value);
                else if (type == typeof(ulong)) _writer.Write((ulong)value);
                else if (type == typeof(DateTime)) _writer.Write(((DateTime)value).ToBinary());
                else if (type == typeof(TimeSpan)) _writer.Write(((TimeSpan)value).Ticks);
                else _writer.Write(Convert.ToInt64(value));
                return;
            }
            if (value is Delegate callbacks)
            {
                _writer.Write((byte)Kind.Callback);
                Delegate[] list = _continuations.IsDefinition(callbacks) ? new[] { callbacks } : callbacks.GetInvocationList();
                _writer.Write(list.Length);
                foreach (Delegate callback in list)
                {
                    string key = _continuations.Key(callback);
                    _writer.Write(key);
                    Write(_continuations.IsDefinition(key) ? null : callback.Target, depth + 1);
                }
                return;
            }
            if (_written.TryGetValue(value, out int id))
            {
                _writer.Write((byte)Kind.Reference);
                _writer.Write(id);
                return;
            }
            _written.Add(value, _written.Count);
            if (value is byte[] bytes)
            {
                _writer.Write((byte)Kind.Bytes);
                _writer.Write(bytes.Length);
                _writer.Write(bytes);
                return;
            }
            _writer.Write((byte)(value is ISimpleTask ? Kind.TaskRecipe : Collection(type) ? Kind.Collection : Kind.Object));
            if (value is ISimpleTask) _writer.Write(_continuations.TaskKey(type));
            else WriteType(type);
            if (Collection(type))
            {
                object[] items = ((IEnumerable)value).Cast<object>().ToArray();
                // Stack enumeration is top-first; loading pushes bottom-first.
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Stack<>)) Array.Reverse(items);
                _writer.Write(items.Length);
                foreach (object item in items) Write(item, depth + 1);
            }
            else
            {
                if (_definitionVersion < 7 && SnapshotSchema.Fields(type).Where(DiscoverDefinitionField)
                    .Any(field => Equals(field.GetValue(value), true))) _definitionNeedsDiscoverUpgrade = true;
                if (_definitionVersion < 6 && SnapshotSchema.Fields(type).Where(TriggerDefinitionField)
                    .Any(field => field.GetValue(value) != null)) _definitionNeedsTriggerUpgrade = true;
                if (_definitionVersion < 5 && SnapshotSchema.Fields(type).Where(Batch17DefinitionField)
                    .Any(field => field.GetValue(value) != null)) _definitionNeedsTargetingUpgrade = true;
                if (_definitionVersion < 4)
                    foreach (FieldInfo field in SnapshotSchema.Fields(type).Where(Batch15DefinitionField))
                    {
                        object extension = field.GetValue(value);
                        if (extension != null && !Equals(extension, field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null))
                            _definitionNeedsUpgrade = true;
                    }
                foreach (FieldInfo field in Fields(type)) Write(field.GetValue(value), depth + 1);
            }
        }

        private static bool Batch15DefinitionField(FieldInfo field) =>
            field.DeclaringType == typeof(Enchants.Power) &&
                (field.Name == "<HandEntryTask>k__BackingField" || field.Name == "<RetainInHeldForm>k__BackingField") ||
            field.DeclaringType == typeof(OfflinePoolFilter) &&
                (field.Name == "<ExactAttack>k__BackingField" || field.Name == "<AttackParity>k__BackingField" ||
                 field.Name == "<CostParity>k__BackingField" || field.Name == "_cardIds" || field.Name == "_anyOf") ||
            field.DeclaringType == typeof(Tasks.SimpleTasks.MoveToGraveYard) && field.Name == "_destroy";

        private static bool Batch17DefinitionField(FieldInfo field) =>
            field.DeclaringType == typeof(Card) && field.Name == "<SourceTargetingPredicate>k__BackingField" ||
            field.DeclaringType == typeof(DormantDefinition) && field.Name == "<AwakenCondition>k__BackingField";

        private static bool TriggerDefinitionField(FieldInfo field) =>
            field.DeclaringType == typeof(Triggers.Trigger) && field.Name == nameof(Triggers.Trigger.OwnerCondition);

        private static bool DiscoverDefinitionField(FieldInfo field) =>
            field.DeclaringType == typeof(OfflinePoolFilter) && field.Name == "<DiscoverFallback>k__BackingField";

        private IEnumerable<FieldInfo> Fields(Type type) => SnapshotSchema.Fields(type).Where(field =>
            !(_definitionVersion < 7 && DiscoverDefinitionField(field)) &&
            !(_definitionVersion < 6 && TriggerDefinitionField(field)) &&
            !(_definitionVersion < 5 && Batch17DefinitionField(field)) &&
            !(_definitionVersion < 4 && Batch15DefinitionField(field)) &&
            !(_definitionVersion < 3 && field.DeclaringType == typeof(Card) && field.Name == "_boardHistoryDisplays") &&
            !(_definitionVersion < 2 && field.DeclaringType == typeof(Card) && field.Name == "<HealthCostCondition>k__BackingField") &&
            (_definitionVersion > 0 || !(field.DeclaringType == typeof(Card) &&
                (field.FieldType == typeof(DormantDefinition) || field.FieldType == typeof(Actions.ShatterDefinition)
                    || field.FieldType == typeof(Actions.HandProgressDefinition)
                    || field.Name == "<CopyHandTrigger>k__BackingField" || field.Name == "<ExpiresOnHandExit>k__BackingField")
                || field.DeclaringType == typeof(SabberStoneCore.Enchants.LocationActivation) && field.Name == "TargetCondition")));

        private int Count()
        {
            int count = _reader.ReadInt32();
            if (count < 0 || count > MaxItems || count > _reader.BaseStream.Length - _reader.BaseStream.Position)
                throw new SerializationException("Invalid snapshot count.");
            return count;
        }

        private object Read(int depth)
        {
            if (depth > 256 || _read.Count > MaxItems) throw new SerializationException("Snapshot graph limit.");
            Kind kind = (Kind)_reader.ReadByte();
            if (kind == Kind.Null) return null;
            if (kind == Kind.CardDefinition)
            {
                string id = _reader.ReadString();
                return id == "$game" ? Card.CardGame : id == "$player" ? Card.CardPlayer :
                    Cards.FromId(id) ?? throw new SerializationException("Unknown snapshot card definition: " + id);
            }
            if (kind == Kind.Reference)
            {
                int id = _reader.ReadInt32();
                if (id < 0 || id >= _read.Count) throw new SerializationException("Invalid snapshot reference.");
                return _read[id];
            }
            if (kind == Kind.Bytes)
            {
                int length = _reader.ReadInt32();
                if (length < 0 || length > MaxBytes || length > _reader.BaseStream.Length - _reader.BaseStream.Position)
                    throw new SerializationException("Invalid snapshot byte blob length.");
                byte[] bytes = _reader.ReadBytes(length);
                _read.Add(bytes);
                return bytes;
            }
            if (kind == Kind.Callback)
            {
                int count = Count();
                if (count == 0) throw new SerializationException("Empty snapshot subscription.");
                Delegate result = null;
                for (int i = 0; i < count; i++)
                {
                    string key = _reader.ReadString();
                    result = Delegate.Combine(result, _continuations.Resolve(key, Read(depth + 1)));
                }
                return result;
            }
            if (kind != Kind.Scalar && kind != Kind.Object && kind != Kind.Collection && kind != Kind.TaskRecipe)
                throw new SerializationException("Invalid snapshot record kind.");
            Type type = kind == Kind.TaskRecipe ? _continuations.TaskType(_reader.ReadString()) : ReadType();
            if (kind == Kind.Scalar)
            {
                if (!Scalar(type)) throw new SerializationException("Invalid snapshot scalar.");
                if (type == typeof(string)) return _reader.ReadString();
                if (type == typeof(bool)) return _reader.ReadBoolean();
                if (type == typeof(float)) return _reader.ReadSingle();
                if (type == typeof(double)) return _reader.ReadDouble();
                if (type == typeof(ulong)) return _reader.ReadUInt64();
                if (type == typeof(DateTime)) return DateTime.FromBinary(_reader.ReadInt64());
                if (type == typeof(TimeSpan)) return TimeSpan.FromTicks(_reader.ReadInt64());
                long value = _reader.ReadInt64();
                return type.IsEnum ? Enum.ToObject(type, value) : Convert.ChangeType(value, type, System.Globalization.CultureInfo.InvariantCulture);
            }
            if ((kind == Kind.Collection) != Collection(type) || Scalar(type) || type.IsAbstract || type.IsInterface)
                throw new SerializationException("Invalid snapshot object recipe.");
            if (kind == Kind.Collection)
            {
                int count = Count();
                Type element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                Type generic = type.IsArray ? null : type.GetGenericTypeDefinition();
                bool readOnlyDictionary = generic == typeof(System.Collections.ObjectModel.ReadOnlyDictionary<,>);
                bool readOnly = readOnlyDictionary || generic == typeof(System.Collections.ObjectModel.ReadOnlyCollection<>);
                Type mutableType = readOnlyDictionary ? typeof(Dictionary<,>).MakeGenericType(type.GetGenericArguments()) :
                    readOnly ? typeof(List<>).MakeGenericType(element) : type;
                object collection = type.IsArray ? Array.CreateInstance(element, count) :
                    Activator.CreateInstance(mutableType);
                object result = readOnly ? Activator.CreateInstance(type, collection) : collection;
                // Decode into scratch storage; every alias/callback sees the carried service instead.
                object bound = _runtimeServices != null && _runtimeServices.TryGetValue(type, out var live) ? live : result;
                _read.Add(bound);
                MethodInfo add = type.IsArray ? null : collection.GetType().GetMethod(
                    generic == typeof(Queue<>) ? "Enqueue" : generic == typeof(Stack<>) ? "Push" : "Add");
                for (int i = 0; i < count; i++)
                {
                    object item = Read(depth + 1);
                    if (collection is Array array) array.SetValue(item, i);
                    else if (readOnlyDictionary || generic == typeof(Dictionary<,>))
                        add.Invoke(collection, new[] { item.GetType().GetProperty("Key").GetValue(item), item.GetType().GetProperty("Value").GetValue(item) });
                    else add.Invoke(collection, new[] { item });
                }
                return bound;
            }
#pragma warning disable SYSLIB0050 // Closed, fingerprinted field schema; no runtime type resolution or serialization callbacks.
            object instance;
            if (depth == 0 && _restoreRoot != null)
            {
                if (type != typeof(Game)) throw new SerializationException("Snapshot root must be a game.");
                instance = _restoreRoot;
                // Fresh Load allocates an uninitialized root; discard the same runtime-only state here.
                for (Type owner = type; owner != null; owner = owner.BaseType)
                    foreach (var field in owner.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                        .Where(f => f.IsDefined(typeof(NonSerializedAttribute))))
                        field.SetValue(instance, field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null);
            }
            else instance = FormatterServices.GetUninitializedObject(type);
#pragma warning restore SYSLIB0050
            object binding = _runtimeServices != null && _runtimeServices.TryGetValue(type, out var runtime) ? runtime : instance;
            _read.Add(binding);
            foreach (FieldInfo field in Fields(type))
            {
                object value = Read(depth + 1);
                if (_runtimeLogging.HasValue)
                {
                    if (field.Name == "_logging" && (instance is Entity || instance is Enchantment)) value = _runtimeLogging.Value;
                    // A baseline may have no log queue even when its receiving game enables logging.
                    if (instance is Game && field.FieldType == typeof(Queue<LogEntry>)) value = _runtimeServices[typeof(Queue<LogEntry>)];
                }
                field.SetValue(instance, value);
            }
            if (instance is GameConfig config && _runtimeLogging.HasValue) config.Logging = _runtimeLogging.Value;
            return binding;
        }
    }
}
