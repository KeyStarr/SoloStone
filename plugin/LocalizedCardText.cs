using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// SoloStone's frozen card text in the client's language (facts/README.md, "Locale files"). A translation is used only while the
// engine still plays the English text it was made from (its Key); otherwise the card shows the engine's English. One file per non-English locale,
// embedded in the plugin; only the running locale's file is ever read, and only on first use. Pure (no Unity, no engine types),
// so the launcher tests run it. Keyed by engine card ID; LocalNativeText maps the client's native ID to it.
// A closed-schema reader for the same reason CardFacts is one: Unity's Mono has no usable JSON serializer.
internal static class LocalizedCardText
{
    internal const string Meta = "violet-hold-2026-09";
    internal const string English = "enUS";
    private static readonly object gate = new object();
    private static readonly Dictionary<string, Dictionary<string, Entry>> loaded = new Dictionary<string, Dictionary<string, Entry>>(StringComparer.Ordinal);

    // A translation and the key of the English text it translates (Key). Used only while the engine still plays that English text.
    internal struct Entry { internal string Key, Text; }

    internal enum Status { English, NoFile, NoEntry, Stale, Localized }

    // Opens a locale file by its resource name; null when there is none. The plugin reads its assembly, the tests read facts/.
    internal static Func<string, Stream> Open = name => typeof(LocalizedCardText).Assembly.GetManifestResourceStream(name);

    internal static string ResourceName(string locale) => "SoloStone.Facts." + Meta + "." + locale + ".json";

    // The locale the card text follows: the client's own, with enGB as enUS. Null for English and for anything that is not
    // one of the client's 14 locales, so nothing odd is ever used as a file name.
    internal static string Normalize(string clientLocale)
    {
        if (string.IsNullOrEmpty(clientLocale) || clientLocale == "enGB" || clientLocale == English) return null;
        return GameLanguage.Known(clientLocale) ? clientLocale : null;
    }

    // The English text a translation is keyed to, as the engine plays it: no " @spelldmg" suffix, no whitespace at either end.
    internal static string Canonical(string english)
    {
        if (english == null) return null;
        const string suffix = " @spelldmg";
        if (english.EndsWith(suffix, StringComparison.Ordinal)) english = english.Substring(0, english.Length - suffix.Length);
        return english.Trim(' ', '\t', '\r', '\n');
    }

    // FNV-1a 32 over the UTF-8 of Canonical(english), 8 lowercase hex digits. tools/gen_card_facts.py text_key writes the same.
    internal static string Key(string english)
    {
        uint hash = 2166136261;
        foreach (byte b in Encoding.UTF8.GetBytes(Canonical(english) ?? "")) hash = (hash ^ b) * 16777619;
        return hash.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
    }

    // The English with the client's layout marks undone: a leading [x], `_` (non-breaking space), `&` (optional line break) and
    // newlines. Some card modules set Card.Text in the client's raw form; the card file has the same words with the marks resolved.
    // tools/gen_card_facts.py without_layout does the same.
    internal static string WithoutLayout(string english)
    {
        string text = Canonical(english);
        if (text == null) return null;
        if (text.StartsWith("[x]", StringComparison.Ordinal)) text = text.Substring(3);
        text = text.Replace('_', '\u00a0').Replace("&", "").Replace('\n', ' ');
        while (text.Contains("  ")) text = text.Replace("  ", " ");
        return text.Trim(' ', '\t', '\r', '\n');
    }

    // True while the engine plays the English a translation was made from: the same text, or the same words in raw layout.
    internal static bool SameEnglish(string key, string engineText) => key == Key(engineText) || key == Key(WithoutLayout(engineText));

    // Which language the card text follows, given what the client reports and what Options > Language started it with.
    // `cacheable` is false while the two disagree about English: the client can report enUS before its language is set up.
    internal static string Resolve(string clientLocale, string runningLocale, out bool cacheable)
    {
        string client = Normalize(clientLocale), running = Normalize(runningLocale);
        bool known = client != null || clientLocale == English || clientLocale == "enGB";
        cacheable = known && (client != null || running == null);
        if (client != null) return client;
        return known ? null : running;
    }

    // The translation to use for a card whose engine text is `engineText`, and why not when there is none.
    internal static Status Lookup(string locale, string engineId, string engineText, out string raw)
    {
        raw = null;
        locale = Normalize(locale);
        if (locale == null) return Status.English;
        if (string.IsNullOrEmpty(engineId)) return Status.NoEntry;
        var texts = Load(locale);
        if (texts == null) return Status.NoFile;
        if (!texts.TryGetValue(engineId, out Entry entry) || string.IsNullOrEmpty(entry.Text)) return Status.NoEntry;
        // The engine rewrites some cards after load (Configure hooks); a translation of the old text would say something else.
        if (!SameEnglish(entry.Key, engineText)) return Status.Stale;
        raw = entry.Text;
        return Status.Localized;
    }

    // Null when the locale has no usable file. A failure is remembered, so a broken file is not re-read on every card.
    internal static Dictionary<string, Entry> Load(string locale)
    {
        lock (gate)
        {
            if (loaded.TryGetValue(locale, out var cached)) return cached;
            Dictionary<string, Entry> texts = null;
            try
            {
                using (var stream = Open(ResourceName(locale)))
                    if (stream != null) texts = Read(stream, locale);
            }
            catch (Exception) { texts = null; }
            loaded[locale] = texts;
            return texts;
        }
    }

    internal static void Reset() { lock (gate) loaded.Clear(); }

    internal static Dictionary<string, Entry> Read(Stream stream, string locale)
    {
        string json;
        using (var reader = new StreamReader(stream, new UTF8Encoding(false), false)) json = reader.ReadToEnd();
        return new Reader(json).Document(locale);
    }

    private sealed class Reader
    {
        private readonly string json;
        private int at;

        internal Reader(string text) { json = text; }

        internal Dictionary<string, Entry> Document(string locale)
        {
            Dictionary<string, Entry> texts = null;
            int schema = 0;
            string meta = null, fileLocale = null;
            Expect('{');
            do
            {
                string key = Str();
                Expect(':');
                switch (key)
                {
                    case "schemaVersion": schema = Int(); break;
                    case "meta": meta = Str(); break;
                    case "locale": fileLocale = Str(); break;
                    case "text": texts = Texts(); break;
                    default: Skip(); break; // provenance and the exclusion lists, for the generator and the tests
                }
            } while (Next('}'));
            Space();
            if (at != json.Length) throw Bad("trailing data");
            if (schema != 2 || meta != Meta || fileLocale != locale || texts == null) throw Bad("not the " + locale + " file of " + Meta);
            return texts;
        }

        private Dictionary<string, Entry> Texts()
        {
            var texts = new Dictionary<string, Entry>(10000, StringComparer.Ordinal);
            Expect('{');
            Space();
            if (Peek() == '}') { at++; return texts; }
            do
            {
                string id = Str();
                Expect(':');
                Expect('[');
                string key = Str();
                Expect(',');
                string text = Str();
                Expect(']');
                texts.Add(id, new Entry { Key = key, Text = text });
            } while (Next('}'));
            return texts;
        }

        private void Skip()
        {
            Space();
            switch (Peek())
            {
                case '"': Str(); break;
                case '[': Container('[', ']'); break;
                case '{': Container('{', '}'); break;
                default:
                    while (at < json.Length && (char.IsLetterOrDigit(json[at]) || json[at] == '-' || json[at] == '+' || json[at] == '.')) at++;
                    break;
            }
        }

        private void Container(char open, char close)
        {
            Expect(open);
            Space();
            if (Peek() == close) { at++; return; }
            do
            {
                if (open == '{') { Str(); Expect(':'); }
                Skip();
            } while (Next(close));
        }

        private int Int()
        {
            Space();
            int start = at;
            while (at < json.Length && json[at] >= '0' && json[at] <= '9') at++;
            if (at == start || at - start > 6) throw Bad("bad number");
            return int.Parse(json.Substring(start, at - start), System.Globalization.CultureInfo.InvariantCulture);
        }

        private string Str()
        {
            Space();
            if (Peek() != '"') throw Bad("string expected");
            at++;
            var builder = new StringBuilder();
            while (true)
            {
                if (at >= json.Length) throw Bad("unterminated string");
                char c = json[at++];
                if (c == '"') return builder.ToString();
                if (c != '\\') { builder.Append(c); continue; }
                if (at >= json.Length) throw Bad("unterminated string");
                char e = json[at++];
                switch (e)
                {
                    case '"': case '\\': case '/': builder.Append(e); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (at + 4 > json.Length) throw Bad("bad escape");
                        int code = 0;
                        for (int i = 0; i < 4; i++)
                        {
                            int digit = "0123456789abcdef".IndexOf(char.ToLowerInvariant(json[at + i]));
                            if (digit < 0) throw Bad("bad escape");
                            code = code * 16 + digit;
                        }
                        builder.Append((char)code);
                        at += 4;
                        break;
                    default: throw Bad("bad escape");
                }
            }
        }

        private bool Next(char close)
        {
            Space();
            if (at >= json.Length) throw Bad("unexpected end");
            char c = json[at++];
            if (c == ',') return true;
            if (c == close) return false;
            throw Bad("expected , or " + close);
        }

        private void Expect(char c)
        {
            Space();
            if (at >= json.Length || json[at] != c) throw Bad("expected " + c);
            at++;
        }

        private char Peek() => at < json.Length ? json[at] : '\0';
        private void Space() { while (at < json.Length && (json[at] == ' ' || json[at] == '\n' || json[at] == '\r' || json[at] == '\t')) at++; }
        private InvalidDataException Bad(string what) => new InvalidDataException("locale file: " + what + " at " + at);
    }
}
