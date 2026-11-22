using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Hearthstone deck codes (deckstrings), client-free. Reads the format the client's own ShareableDeck writes (version 1:
// reserved 0 byte, version, format, heroes, cards by copy count, then an optional sideboard block) and finds the code in
// pasted text. Sharing a deck (#220) uses the client's own Copy in the deck options; this reads what it wrote, for the
// log, the tests and pasting text that went through Discord.
internal sealed class DeckCodeData
{
    internal int Format, Hero;
    internal List<(int dbId, int qty)> Cards = new List<(int, int)>();
    internal List<(int dbId, int qty, int owner)> Sideboards = new List<(int, int, int)>();
    internal int CardCount => Cards.Sum(card => card.qty);
}

internal static class DeckCodeText
{
    private const int MaximumEntries = 400; // a real code lists at most 40 main and a few sideboard cards
    private const int MaximumFormat = 4, MaximumCodeLength = 1024;

    // Every version-1 code starts with the bytes 0, 1 ("AAE" in base64); anything else, or a line too long to be one, is not
    // decoded at all, so a large clipboard costs no exception per line.
    private static bool LooksLikeCode(string code)
    {
        if (code == null) return false;
        string text = code.Trim();
        return text.Length >= 8 && text.Length <= MaximumCodeLength && text.Length % 4 == 0 && text.StartsWith("AAE", StringComparison.Ordinal)
            && text.All(ch => ch >= 'A' && ch <= 'Z' || ch >= 'a' && ch <= 'z' || ch >= '0' && ch <= '9' || ch == '+' || ch == '/' || ch == '=');
    }

    // Parses one code line exactly as the client's version-1 reader does (order and field layout); null when it is not one.
    internal static DeckCodeData TryDecode(string code)
    {
        if (!LooksLikeCode(code)) return null;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(code.Trim()); }
        catch (FormatException) { return null; }
        try
        {
            using (var stream = new MemoryStream(bytes))
            {
                if (stream.ReadByte() != 0) return null;
                if (ReadVarint(stream) != 1) return null;
                var deck = new DeckCodeData { Format = Small(ReadVarint(stream)) };
                if (deck.Format < 1 || deck.Format > MaximumFormat) return null; // FT_WILD..FT_TWIST; the client refuses 0 and unknown formats
                ulong heroes = ReadVarint(stream);
                if (heroes != 1) return null;
                deck.Hero = Small(ReadVarint(stream));
                for (int copies = 1; copies <= 2; copies++)
                {
                    int count = Count(stream);
                    for (int index = 0; index < count; index++) deck.Cards.Add((Small(ReadVarint(stream)), copies));
                }
                int many = Count(stream);
                for (int index = 0; index < many; index++) deck.Cards.Add((Small(ReadVarint(stream)), Small(ReadVarint(stream))));
                if (stream.ReadByte() == 1) // like the client, any other byte (or none) means no sideboards
                {
                    // Sideboards: one and two copies as (card, owner); more as (count, card, owner): the client's order, in both
                    // ShareableDeck.Serialize_WriteSideBoardCards and DeserializeFromVersion_1 (client 37.0.0), unlike the main deck's (card, count).
                    for (int copies = 1; copies <= 2; copies++)
                    {
                        int count = Count(stream);
                        for (int index = 0; index < count; index++) deck.Sideboards.Add((Small(ReadVarint(stream)), copies, Small(ReadVarint(stream))));
                    }
                    int more = Count(stream);
                    for (int index = 0; index < more; index++)
                    {
                        int qty = Small(ReadVarint(stream));
                        deck.Sideboards.Add((Small(ReadVarint(stream)), qty, Small(ReadVarint(stream))));
                    }
                }
                if (deck.Hero <= 0 || deck.Cards.Any(card => card.dbId <= 0 || card.qty <= 0)
                    || deck.Sideboards.Any(card => card.dbId <= 0 || card.qty <= 0 || card.owner <= 0)) return null;
                return deck;
            }
        }
        catch (EndOfStreamException) { return null; }
        catch (OverflowException) { return null; }
    }

    // The code in pasted text, and the deck name its "###" line gives (empty when none). First the client's own rule (the first
    // line that does not start with '#'), so a code it reads is read the same. When that line is not a code (text copied from a
    // Discord message drops the '#' of its heading lines; a code may sit in `backticks`), the first line that is one.
    internal static string FindCode(string text, out string name) => FindCode(text, out name, out _);

    // rescued: the client's own rule would have picked a line that is not a code, and another line is one.
    internal static string FindCode(string text, out string name, out bool rescued)
    {
        name = ""; rescued = false;
        if (string.IsNullOrEmpty(text)) return null;
        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        string title = lines.FirstOrDefault(line => line.Trim().StartsWith("###", StringComparison.Ordinal));
        if (title != null) name = title.Replace("###", "").Trim();
        string first = lines.FirstOrDefault(line => !line.Trim().StartsWith("#", StringComparison.Ordinal))?.Trim();
        if (first == null || TryDecode(first) != null) return first;
        string found = lines.Select(line => line.Trim().Trim('`').Trim()).FirstOrDefault(line => TryDecode(line) != null);
        rescued = found != null;
        return found ?? first;
    }

    private static int Count(Stream stream)
    {
        ulong count = ReadVarint(stream);
        if (count > MaximumEntries) throw new OverflowException("deck code lists too many cards");
        return (int)count;
    }

    private static int Small(ulong value) => value > int.MaxValue ? throw new OverflowException("deck code value out of range") : (int)value;

    private static ulong ReadVarint(Stream stream)
    {
        ulong value = 0;
        for (int shift = 0; shift < 64; shift += 7)
        {
            int next = stream.ReadByte();
            if (next < 0) throw new EndOfStreamException();
            value |= (ulong)(next & 0x7f) << shift;
            if ((next & 0x80) == 0) return value;
        }
        throw new OverflowException("deck code varint too long");
    }
}
