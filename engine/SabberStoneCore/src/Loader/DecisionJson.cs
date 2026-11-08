using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace SabberStoneCore.Model.Decisions
{
    // The v1 envelopes contain only fixed member names, integers and arrays/objects.
    // Keep that closed wire schema without depending on Unity's stripped serializers.
    internal static class DecisionJson
    {
        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
        private static string Array(IEnumerable<string> values) => "[" + string.Join(",", values) + "]";
        private static string Object(params object[] pairs) => "{" + string.Join(",", Enumerable.Range(0, pairs.Length / 2)
            .Select(i => "\"" + pairs[2 * i] + "\":" + pairs[2 * i + 1])) + "}";
        internal static string Write<T>(T value)
        {
            if (value is DecisionAnswer answer)
                return Object("SchemaVersion", Number(answer.SchemaVersion), "DecisionId", Number(answer.DecisionId),
                    "ControllerId", Number(answer.ControllerId), "SelectedOptionIds", Array(answer.SelectedOptionIds.Select(i => Number(i))));
            var view = (PendingDecision)(object)value;
            return Object("SchemaVersion", Number(view.SchemaVersion), "Id", Number(view.Id), "ControllerId", Number(view.ControllerId),
                "SourceId", Number(view.SourceId), "Kind", Number((int)view.Kind), "MinSelections", Number(view.MinSelections),
                "MaxSelections", Number(view.MaxSelections), "Options", Array(view.Options.Select(o => Object(
                    "OptionId", Number(o.OptionId), "Kind", Number((int)o.Kind), "EntityId", Number(o.EntityId),
                    "TargetId", Number(o.TargetId), "BranchIndex", Number(o.BranchIndex), "ZonePosition", Number(o.ZonePosition),
                    "TaskType", Number((int)o.TaskType)))));
        }

        internal static T Read<T>(string json) where T : class
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            try
            {
                var parser = new Parser(json);
                var obj = (Dictionary<string, object>)parser.Read();
                parser.End();
                if (Int(obj, "SchemaVersion") != 1) throw new SerializationException("Unsupported decision schema.");
                if (typeof(T) == typeof(DecisionAnswer))
                    return (T)(object)new DecisionAnswer((long)obj["DecisionId"], Int(obj, "ControllerId"),
                        ((List<object>)obj["SelectedOptionIds"]).Select(v => checked((int)(long)v)));
                return (T)(object)new PendingDecision((long)obj["Id"], Int(obj, "ControllerId"), Int(obj, "SourceId"),
                    (DecisionKind)Int(obj, "Kind"), Int(obj, "MinSelections"), Int(obj, "MaxSelections"),
                    ((List<object>)obj["Options"]).Select(value => {
                        var option = (Dictionary<string, object>)value;
                        return new DecisionOption(Int(option, "OptionId"), (DecisionKind)Int(option, "Kind"),
                            Int(option, "EntityId"), Int(option, "TargetId"), Int(option, "BranchIndex"), Int(option, "ZonePosition"),
                            (Tasks.PlayerTasks.PlayerTaskType)Int(option, "TaskType"));
                    }));
            }
            catch (Exception error) when (error is InvalidCastException || error is KeyNotFoundException ||
                error is OverflowException || error is FormatException || error is ArgumentException)
            { throw new SerializationException("Invalid decision envelope.", error); }
        }

        private static int Int(Dictionary<string, object> obj, string key) => checked((int)(long)obj[key]);

        private sealed class Parser
        {
            private static readonly Regex Token = new Regex(@"\G\s*(""[A-Za-z][A-Za-z0-9]*""|-?(?:0|[1-9][0-9]*)|[{}\[\]:,])", RegexOptions.CultureInvariant);
            private readonly string text;
            private int offset;
            private string next;
            internal Parser(string text)
            {
                if (text.Length > 1024 * 1024) throw new SerializationException("Decision envelope exceeds size limit.");
                this.text = text;
                Advance();
            }
            private void Advance()
            {
                var match = Token.Match(text, offset);
                if (!match.Success) { next = null; return; }
                offset = match.Index + match.Length;
                next = match.Groups[1].Value;
            }
            private bool Take(string token)
            {
                if (next != token) return false;
                Advance(); return true;
            }
            private void Require(string token)
            {
                if (!Take(token)) throw new SerializationException("Invalid decision JSON punctuation.");
            }
            internal object Read(int depth = 0)
            {
                if (depth > 4) throw new SerializationException("Decision JSON nesting limit.");
                if (Take("{"))
                {
                    var obj = new Dictionary<string, object>(StringComparer.Ordinal);
                    if (Take("}")) return obj;
                    do
                    {
                        if (next == null || next[0] != '"') throw new SerializationException("Expected decision member name.");
                        string key = next.Substring(1, next.Length - 2);
                        Advance(); Require(":"); obj.Add(key, Read(depth + 1));
                    } while (Take(","));
                    Require("}"); return obj;
                }
                if (Take("["))
                {
                    var array = new List<object>();
                    if (Take("]")) return array;
                    do { array.Add(Read(depth + 1)); } while (Take(","));
                    Require("]"); return array;
                }
                if (next == null || !long.TryParse(next, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long number))
                    throw new SerializationException("Expected decision integer.");
                Advance(); return number;
            }
            internal void End()
            {
                if (next != null || !string.IsNullOrWhiteSpace(text.Substring(offset)))
                    throw new SerializationException("Trailing decision JSON data.");
            }
        }
    }
}
