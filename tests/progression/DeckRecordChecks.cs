using System.Text;

// Deck picker record (owner 2026-11-05): lifetime wins/losses of one deck across Ranked and Casual, folded from the settled ledger.
// Nothing is stored, so counting is exactly the settlements: exactly once, no double count on replay, restart, concede or rematch.
internal static class DeckRecordChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception("DeckRecord: " + message); }
    static string Rec(LocalDeckRecord r) => r.Wins + "-" + r.Losses + "-" + r.Ties + "|R" + r.RankedWins + "-" + r.RankedLosses + "|C" + r.CasualWins + "-" + r.CasualLosses;
    static string Text(string path) => Encoding.UTF8.GetString(File.ReadAllBytes(path));

    internal static void Run(string root)
    {
        Fold();
        TextFormat();
        Settlement(root);
        Console.WriteLine("PASS deck record: fold (W/L/concede/abandon/tie, Ranked+Casual split, other decks, legacy custom key, unsettled rows), text, "
            + "settlement counted once (win, loss, concede, tie, replay, restart, abandon, rematch, debug scenario), nothing stored in the save");
    }

    static LocalMatchRecord M(string deck, string result, string mode = null) => new LocalMatchRecord { Deck = deck, Result = result, Mode = mode };

    static void Fold()
    {
        var rows = new[] { M("mage", "Won"), M("mage", "Lost"), M("mage", "Abandoned"), M("mage", "Tied"), M("mage", "Won", CasualRules.Mode),
            M("mage", "Lost", CasualRules.Mode), M("mage", null), M("priest", "Won"), M("custom:910000010", "Won"), M("custom:910000010", "Lost"),
            M("custom:910000011", "Won"), M("custom", "Won"), null, M(null, "Won") };
        Check(Rec(LocalDeckRecord.Of(rows, "mage")) == "2-3-1|R1-2|C1-1", "starter fold " + Rec(LocalDeckRecord.Of(rows, "mage")));
        Check(Rec(LocalDeckRecord.Of(rows, "custom:910000010")) == "1-1-0|R1-1|C0-0", "custom deck fold");
        Check(Rec(LocalDeckRecord.Of(rows, "custom:910000011")) == "1-0-0|R1-0|C0-0", "another custom deck is separate");
        Check(Rec(LocalDeckRecord.Of(rows, "custom:" + LocalProfile.LegacyCustomDeckId)) == "1-0-0|R1-0|C0-0", "legacy 'custom' key is the legacy deck id");
        Check(Rec(LocalDeckRecord.Of(rows, "druid")) == "0-0-0|R0-0|C0-0" && LocalDeckRecord.Of(rows, "druid").Games == 0, "unplayed deck is 0-0");
        Check(LocalDeckRecord.Of(rows, null) == LocalDeckRecord.None && LocalDeckRecord.Of(null, "mage") == LocalDeckRecord.None
            && LocalDeckRecord.Of(rows, "") == LocalDeckRecord.None, "no key or ledger: none, never throws");
        // The key carries no name: a rename or edit (same id) keeps the record, a different id (copy, rebuild) starts empty.
        Check(LocalDeckRecord.SameDeck("custom:910000010", "custom:910000010") && !LocalDeckRecord.SameDeck("custom:910000010", "custom:91000001"),
            "id match is exact");
    }

    static void TextFormat()
    {
        Check(DeckRecordText.Format(new LocalDeckRecord(8, 4, 4, 3, 2)) == "12 W - 7 L", "format " + DeckRecordText.Format(new LocalDeckRecord(8, 4, 4, 3, 2)));
        Check(DeckRecordText.Format(LocalDeckRecord.None) == "0 W - 0 L" && DeckRecordText.Format(null) == "0 W - 0 L", "unplayed deck reads 0 W - 0 L");
        Check(DeckRecordText.Format(new LocalDeckRecord(0, 0, 0, 0, 9)) == "0 W - 0 L", "ties are not shown");
        foreach (var record in new[] { new LocalDeckRecord(3, 1, 0, 0, 0), new LocalDeckRecord(1, 3, 0, 0, 0), LocalDeckRecord.None, new LocalDeckRecord(500, 0, 0, 0, 0) })
            Check(System.Text.RegularExpressions.Regex.IsMatch(DeckRecordText.Format(record), @"^\d+ W - \d+ L$"), "text is only 'N W - N L': no percent, no streak");
        // The key the picker looks up is the key the start path writes: one function (KeyFor), used by both (source tie in test-control).
        Check(LocalDeckRecord.KeyFor(true, 910000010, "mage") == "custom:910000010" && LocalDeckRecord.KeyFor(false, 910000010, "mage") == "mage",
            "KeyFor: custom decks by id, starters by loaner id");
        Check(LocalDeckRecord.SameDeck("custom:" + 910000010, LocalDeckRecord.KeyFor(true, 910000010, null)), "a settled custom row is found by KeyFor");
    }

    static LocalMatchRecord Play(LocalProfile p, string deck, string result, int seed, bool conceded = false, string mode = null)
    {
        var m = p.Begin(deck, "npc-dragon-warrior", seed, null, null, null, mode);
        var settled = p.Settle(m.Id, result, 10, conceded);
        p.AcknowledgeRewardPresentation(m.Id);
        return settled;
    }

    static void Settlement(string root)
    {
        string path = Path.Combine(root, "deck-record.xml");
        string winId;
        using (var p = new LocalProfile(path))
        {
            Check(Rec(p.DeckRecord("mage")) == "0-0-0|R0-0|C0-0", "fresh save starts empty");
            winId = Play(p, "mage", "Won", 1).Id;
            Check(Rec(p.DeckRecord("mage")) == "1-0-0|R1-0|C0-0", "win " + Rec(p.DeckRecord("mage")));
            Play(p, "mage", "Lost", 2);
            Check(p.DeckRecord("mage").Losses == 1, "loss");
            Play(p, "mage", "Lost", 3, conceded: true);
            Check(Rec(p.DeckRecord("mage")) == "1-2-0|R1-2|C0-0", "concede is a loss " + Rec(p.DeckRecord("mage")));
            Play(p, "mage", "Tied", 4);
            Check(Rec(p.DeckRecord("mage")) == "1-2-1|R1-2|C0-0", "tie is not a win or a loss");
            Play(p, "mage", "Won", 5, mode: CasualRules.Mode);
            Check(Rec(p.DeckRecord("mage")) == "2-2-1|R1-2|C1-0", "Casual counts in the total, kept apart in the data");
            Check(p.DeckRecord("priest").Games == 0, "another deck untouched");
            // Replay of a settled match (the native result path retried): idempotent, no second count.
            p.Settle(winId, "Won", 10, false);
            Check(p.DeckRecord("mage").Wins == 2, "replayed settlement counts once");
            // Rematch is a new match on the same deck: one more row, never two.
            Play(p, "mage", "Won", 6);
            Check(Rec(p.DeckRecord("mage")) == "3-2-1|R2-2|C1-0", "rematch adds exactly one " + Rec(p.DeckRecord("mage")));
            // A pending (unsettled) match is not counted.
            p.Begin("mage", "npc-dragon-warrior", 7);
            Check(p.DeckRecord("mage").Games == 6, "pending excluded");
        }
        using (var p = new LocalProfile(path))
        {
            Check(p.DeckRecord("mage").Games == 6, "restart with a pending match: unchanged until recovery");
            p.RecoverInterrupted();
            Check(Rec(p.DeckRecord("mage")) == "3-3-1|R2-3|C1-0", "interrupted match counts as one loss " + Rec(p.DeckRecord("mage")));
            p.RecoverInterrupted();
            Check(p.DeckRecord("mage").Losses == 3, "recovery is not repeated");
            // Debug scenario matches never settle and never count.
            p.ArmDebugScenario();
            var scenario = p.Begin("mage", "npc-dragon-warrior", 8);
            p.DisarmDebugScenario();
            p.Settle(scenario.Id, "Won", 10, false);
            Check(p.DeckRecord("mage").Wins == 3, "debug scenario win is not counted");
        }
        using (var p = new LocalProfile(path))
        {
            Check(Rec(p.DeckRecord("mage")) == "3-3-1|R2-3|C1-0", "reload keeps the record " + Rec(p.DeckRecord("mage")));
            Play(p, "mage", "Won", 9);
            Check(p.DeckRecord("mage").Wins == 4, "keeps counting after restart");
        }
        // Derived, never stored: the save holds no record element, so there is no schema to migrate and old saves read as an empty record.
        Check(!Text(path).Contains("DeckRecord") && !Text(path).Contains("RankedWins"), "nothing new is written to the save");
    }
}
