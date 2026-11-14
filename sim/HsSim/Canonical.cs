using System.Collections;
using Type = System.Type;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Kettle;
using SabberStoneCore.Tasks.PlayerTasks;

namespace HsSim;
using Game = SabberStoneCore.Model.Game;

public static class Canonical
{
    private sealed record StateEntity(int Id, string CardId, int Zone, int Controller, Dictionary<GameTag, int> Tags);
    private static readonly Dictionary<Type, MemberInfo[]> Members = new();
    private sealed class Decisions
    {
        public readonly StringBuilder Text = new();
        public int Options, Choices;
    }
    private static readonly ConditionalWeakTable<Game, Decisions> DecisionTraces = new();

    // Serialize every public data member, including nested history entities; never engine Print()/Hash().
    public static string Value(object value, int? packetIndex = null)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, value, packetIndex);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Write(Utf8JsonWriter writer, object value, int? packetIndex = null)
    {
        if (value == null) { writer.WriteNullValue(); return; }
        if (value is string text) { writer.WriteStringValue(text); return; }
        if (value is Enum) { writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture)); return; }
        if (value is int number) { writer.WriteNumberValue(number); return; }
        if (value is bool flag) { writer.WriteBooleanValue(flag); return; }
        if (value is sbyte or byte or short or ushort or uint or long or ulong or float or double or decimal)
        {
            JsonSerializer.Serialize(writer, value, value.GetType());
            return;
        }
        if (value is IDictionary dictionary)
        {
            writer.WriteStartObject();
            foreach (object key in dictionary.Keys.Cast<object>().OrderBy(k => Convert.ToInt64(k, CultureInfo.InvariantCulture)))
            {
                writer.WritePropertyName(Convert.ToInt64(key, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                Write(writer, dictionary[key]);
            }
            writer.WriteEndObject();
            return;
        }
        if (value is IEnumerable sequence)
        {
            writer.WriteStartArray();
            foreach (object item in sequence) Write(writer, item);
            writer.WriteEndArray();
            return;
        }
        Type type = value.GetType();
        if (!Members.TryGetValue(type, out MemberInfo[] members))
        {
            members = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.CanRead && property.GetIndexParameters().Length == 0).Cast<MemberInfo>()
                .Concat(type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                .OrderBy(member => member.Name, StringComparer.Ordinal).ToArray();
            Members.Add(type, members);
        }
        writer.WriteStartObject();
        writer.WriteString("type", type.Name);
        foreach (MemberInfo member in members)
        {
            // PlayerTaskList is the engine's resolution cache, not part of the client packet.
            if (value is PowerAllOptions && member.Name == nameof(PowerAllOptions.PlayerTaskList)) continue;
            writer.WritePropertyName(member.Name);
            if (packetIndex.HasValue && member.Name == "Index" && value is PowerAllOptions or PowerEntityChoices)
                writer.WriteNumberValue(packetIndex.Value);
            else Write(writer, member is PropertyInfo property ? property.GetValue(value) : ((FieldInfo)member).GetValue(value));
        }
        writer.WriteEndObject();
    }

    public static string History(Game game) => string.Concat(game.PowerHistory.Full.Select(entry => Value(entry) + "\n"))
        + "--- DECISIONS ---\n" + DecisionTrace(game);

    // The option and choice packets offered at every step, the part of the history a golden still pins.
    public static string DecisionTrace(Game game) => DecisionTraces.TryGetValue(game, out var decisions) ? decisions.Text.ToString() : "";

    public static void CaptureDecision(Game game, int step, Controller controller, List<PlayerTask> tasks, string legalSha)
    {
        Decisions decisions = DecisionTraces.GetValue(game, _ => new Decisions());
        string options = "null", choices = "null";
        if (controller.Choice != null)
            choices = Value(PowerChoicesBuilder.EntityChoices(game, controller.Choice), ++decisions.Choices);
        else
            options = Value(Options(game, tasks), ++decisions.Options);
        decisions.Text.Append("{\"step\":").Append(step).Append(",\"controller\":").Append(controller.Id)
            .Append(",\"legalSha\":\"").Append(legalSha).Append("\",\"options\":").Append(options)
            .Append(",\"choices\":").Append(choices).Append("}\n");
    }

    public static PowerAllOptions Options(Game game, List<PlayerTask> tasks)
    {
        PowerAllOptions options = PowerOptionsBuilder.AllOptions(game, tasks);
        // Mirror NativeMatch.SendHumanOptions (STANDARD_PROOF), including the pinned builder's target repair.
        foreach (var option in options.PowerOptionList.Where(option => option.MainOption != null))
            option.MainOption.Targets = tasks.Where(task =>
                    (task.Source?.Id ?? (task.PlayerTaskType == PlayerTaskType.HERO_ATTACK ? task.Controller.Hero.Id
                        : task.PlayerTaskType == PlayerTaskType.HERO_POWER ? task.Controller.Hero.HeroPower.Id : 0))
                    == option.MainOption.EntityId && task.ChooseOne <= 0 &&
#if SOURCE_ENGINE
                    task.ActionTarget != null
#else
                    task.Target != null
#endif
#if SOURCE_ENGINE
                    && option.MainOption.PowerKeyword == 0 && task.PlayerTaskType != PlayerTaskType.TRADE_CARD
#endif
                )
#if SOURCE_ENGINE
                .Select(task => task.ActionTarget.Id).Distinct().ToList();
#else
                .Select(task => task.Target.Id).Distinct().ToList();
#endif
        return options;
    }

    // Keys supported by ControllerAuraEffects and AuraEffects; type guards avoid their overlapping array slots.
    private static IEnumerable<GameTag> AuraKeys(IEntity entity)
    {
        if (entity is Controller) return new[] { GameTag.TIMEOUT, GameTag.SPELLPOWER_DOUBLE, GameTag.SPELL_HEALING_DOUBLE,
            GameTag.HERO_POWER_DOUBLE, GameTag.HEALING_DOES_DAMAGE, GameTag.CHOOSE_BOTH, GameTag.SPELLS_COST_HEALTH,
            GameTag.EXTRA_BATTLECRIES_BASE, GameTag.EXTRA_END_TURN_EFFECT, GameTag.HERO_POWER_DISABLED,
            GameTag.ALL_HEALING_DOUBLE, GameTag.EXTRA_MINION_BATTLECRIES_BASE, GameTag.SPELLPOWER };
        if (entity.AuraEffects == null) return Array.Empty<GameTag>();
        var common = new[] { GameTag.CARD_COSTS_HEALTH, GameTag.ECHO };
        return common.Concat(entity.Card.Type switch
        {
            CardType.MINION => new[] { GameTag.ATK, GameTag.HEALTH, GameTag.CHARGE, GameTag.TAUNT, GameTag.LIFESTEAL,
                GameTag.RUSH, GameTag.CANT_ATTACK, GameTag.IMMUNE_WHILE_ATTACKING, GameTag.WINDFURY, GameTag.CANT_BE_TARGETED_BY_SPELLS, GameTag.CANT_BE_TARGETED_BY_HERO_POWERS },
            CardType.HERO => new[] { GameTag.ATK, GameTag.IMMUNE, GameTag.WINDFURY, GameTag.IMMUNE_WHILE_ATTACKING, GameTag.CANNOT_ATTACK_HEROES, GameTag.HEROPOWER_DAMAGE,
                GameTag.CANT_BE_TARGETED_BY_SPELLS, GameTag.CANT_BE_TARGETED_BY_HERO_POWERS },
            CardType.WEAPON => new[] { GameTag.IMMUNE },
            _ => Array.Empty<GameTag>()
        });
    }

    public static string State(Game game)
    {
        var text = new StringBuilder();
        IEnumerable<IEntity> entities = new IEntity[] { game, game.Player1, game.Player2 }
            .Concat(game.IdEntityDic.Select(pair => (IEntity)pair.Value));
        foreach (IEntity entity in entities.OrderBy(entity => entity.Id))
        {
            // Enchantment.GetEnumerator is unimplemented; merge keys directly, read effective values for auras.
            // Missing tags read as zero; explicit zero storage is not a behavioral difference.
            var tags = entity.Card.Tags.Keys.Concat(entity.NativeTags.Keys).Concat(AuraKeys(entity)).Distinct().OrderBy(tag => (int)tag)
                .Select(tag => new KeyValuePair<GameTag, int>(tag, entity[tag])).Where(pair => pair.Value != 0)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            text.Append(Value(new StateEntity(entity.Id, entity.Card?.Id,
                entity[GameTag.ZONE], entity[GameTag.CONTROLLER], tags)));
#if SOURCE_ENGINE
            if (entity is Controller controller && controller.PendingQuestRewards.Count != 0)
                text.Append("|quest-rewards:").Append(Value(controller.PendingQuestRewards));
            if (entity is BoardObject location)
                text.Append("|location:").Append(location.Location.UsesCompleted).Append(':').Append(location.Location.Stage)
                    .Append(':').Append(location.Location.ConditionLocked ? 1 : 0).Append(':').Append(location.Durability);
#endif
            text.Append('\n');
        }
#if SOURCE_ENGINE
        if (game.HistoryFacts.HasConsumers)
            text.Append("|history-facts-v1:").Append(game.HistoryFacts.CanonicalText()).Append('\n');
#endif
        return text.ToString();
    }

    public static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static string Trace(Game game) => History(game) + "--- FINAL STATE ---\n" + State(game);
}

// SplitMix64, rejection sampling: policy randomness independent of engine/System.Random/runtime hashing.
public sealed class PolicyRandom(ulong seed)
{
    private ulong state = seed;
    public ulong Next()
    {
        ulong z = unchecked(state += 0x9E3779B97F4A7C15UL);
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        return z ^ (z >> 31);
    }
    public int Index(int count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        ulong bound = (ulong)count, threshold = unchecked(0UL - bound) % bound, value;
        do value = Next(); while (value < threshold);
        return (int)(value % bound);
    }
}
