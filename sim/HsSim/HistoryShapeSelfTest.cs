#if SOURCE_ENGINE
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;

namespace HsSim;

// `goldens history-selftest <file>`: proves each HistoryShape rule catches the break it is for. It replays the first game of the
// file that has every kind of entry the breaks need, checks its history passes, then breaks a copy of it once per case and
// checks exactly the expected rule fires, at the expected entry. Run by the goldens suite, next to `goldens check`.
public static class HistoryShapeSelfTest
{
    public static int Run(IEnumerable<GameRecord> games, Func<GameRecord, Game> replay)
    {
        foreach (GameRecord record in games)
        {
            List<IPowerHistoryEntry> history = replay(record).PowerHistory.Full;
            if (!Usable(history)) continue;
            int failures = 0;
            var clean = HistoryShape.Check(history);
            if (clean.Count > 0)
            {
                Console.Error.WriteLine($"history-selftest {record.Id}: the unbroken history fails: {string.Join("; ", clean.Take(5))}");
                return 1;
            }
            var cases = Cases(history).ToList();
            foreach (var (name, rule, at, broken) in cases)
            {
                var violations = HistoryShape.Check(broken);
                string[] fired = violations.Select(violation => violation.Rule).Distinct().Order(StringComparer.Ordinal).ToArray();
                bool ok = rule == null ? fired.Length == 0 : fired.Length == 1 && fired[0] == rule && violations[0].Index == at;
                if (!ok) failures++;
                Console.Error.WriteLine($"history-selftest {(ok ? "ok" : "FAIL")} {name}: expected {(rule == null ? "nothing" : $"{rule} at entry {at}")}; "
                    + $"fired {(fired.Length == 0 ? "nothing" : string.Join("; ", violations.Take(3)))}");
            }
            string[] untested = HistoryShape.Rules.Except(cases.Select(test => test.Rule)).ToArray();
            if (untested.Length > 0)
            {
                Console.Error.WriteLine("history-selftest: no case for " + string.Join(",", untested));
                failures++;
            }
            Console.Error.WriteLine($"history-selftest {record.Id}: {cases.Count - failures}/{cases.Count} cases ok");
            return failures == 0 ? 0 : 1;
        }
        Console.Error.WriteLine("history-selftest: no game in the file has a sub-spell inside a block, a metadata entity and a FULL_ENTITY");
        return 1;
    }

    private static bool Usable(List<IPowerHistoryEntry> history) =>
        history.Count > 0 && history[0] is PowerHistoryCreateGame && history.OfType<PowerHistorySubSpellEnd>().Any()
        && history.OfType<PowerHistoryMetaData>().Any(meta => meta.Info?.Count > 0) && history.OfType<PowerHistoryFullEntity>().Any()
        && history.OfType<PowerHistoryBlockEnd>().Any() && SubSpellInBlock(history);

    private static bool SubSpellInBlock(List<IPowerHistoryEntry> history)
    {
        int depth = 0;
        foreach (var entry in history)
            if (entry is PowerHistoryBlockStart) depth++;
            else if (entry is PowerHistoryBlockEnd) depth--;
            else if (entry is PowerHistorySubSpellEnd && depth > 0) return true;
        return false;
    }

    // Each case: the break, the one rule expected to fire (null: none may fire), and the entry index its first violation must name.
    private static IEnumerable<(string Name, string Rule, int At, List<IPowerHistoryEntry> Broken)> Cases(List<IPowerHistoryEntry> history)
    {
        // Most breaks are inserted right after CREATE_GAME, where only the game and the players exist.
        int gameId = ((PowerHistoryCreateGame)history[0]).Game.Id;
        int lastFullId = history.OfType<PowerHistoryFullEntity>().Last().Entity.Id;
        int firstFull = history.FindIndex(entry => entry is PowerHistoryFullEntity);
        List<IPowerHistoryEntry> Insert(int at, params IPowerHistoryEntry[] entries) { var copy = history.ToList(); copy.InsertRange(at, entries); return copy; }
        List<IPowerHistoryEntry> Remove(int at) { var copy = history.ToList(); copy.RemoveAt(at); return copy; }

        int lastBlockEnd = history.FindLastIndex(entry => entry is PowerHistoryBlockEnd);
        yield return ("last BLOCK_END missing", "BLOCK_BALANCE", history.Count - 1, Remove(lastBlockEnd));
        yield return ("BLOCK_END with no open block", "BLOCK_BALANCE", 1, Insert(1, new PowerHistoryBlockEnd()));

        // A sub-spell that ends inside a block, and the BLOCK_END of that block.
        int subEnd = -1, blockEnd = -1;
        for (int i = 0, depth = 0, closing = 0; i < history.Count && blockEnd < 0; i++)
        {
            if (history[i] is PowerHistoryBlockStart) depth++;
            else if (history[i] is PowerHistorySubSpellEnd && depth > 0 && subEnd < 0) (subEnd, closing) = (i, depth);
            else if (history[i] is PowerHistoryBlockEnd && depth-- == closing && subEnd >= 0) blockEnd = i;
        }
        // Both fire at that block's BLOCK_END, one entry earlier once the SUB_SPELL_END is out of its place.
        yield return ("SUB_SPELL_END in a block missing", "SUB_SPELL_BALANCE", blockEnd - 1, Remove(subEnd));
        var crossed = history.ToList();
        crossed.Insert(blockEnd + 1, crossed[subEnd]);
        crossed.RemoveAt(subEnd);
        yield return ("SUB_SPELL_END moved past its block's BLOCK_END", "SUB_SPELL_BALANCE", blockEnd - 1, crossed);
        yield return ("SUB_SPELL_END with no open sub-spell", "SUB_SPELL_BALANCE", 1, Insert(1, new PowerHistorySubSpellEnd()));

        yield return ("TAG_CHANGE before the entity's FULL_ENTITY", "ENTITY_REF", 1,
            Insert(1, PowerHistoryBuilder.TagChange(lastFullId, SabberStoneCore.Enums.GameTag.DAMAGE, 1)));
        yield return ("SHOW_ENTITY of an entity never created", "ENTITY_REF", 1,
            Insert(1, new PowerHistoryShowEntity { Entity = new PowerHistoryEntity { Id = 1_000_000 } }));
        yield return ("HIDE_ENTITY before the entity's FULL_ENTITY", "ENTITY_REF", 1,
            Insert(1, new PowerHistoryHideEntity { EntityID = lastFullId, Zone = SabberStoneCore.Enums.Zone.HAND }));
        yield return ("CHANGE_ENTITY before the entity's FULL_ENTITY", "ENTITY_REF", 1,
            Insert(1, new PowerHistoryChangeEntity { Entity = new PowerHistoryEntity { Id = lastFullId } }));
        yield return ("BLOCK_START sourced by an entity not yet created", "ENTITY_REF", 1,
            Insert(1, new PowerHistoryBlockStart { BlockType = SabberStoneCore.Enums.BlockType.POWER, Source = lastFullId }, new PowerHistoryBlockEnd()));
        yield return ("BLOCK_START targeting an entity not yet created", "ENTITY_REF", 1,
            Insert(1, new PowerHistoryBlockStart { BlockType = SabberStoneCore.Enums.BlockType.POWER, Source = gameId, Target = lastFullId },
                new PowerHistoryBlockEnd()));
        yield return ("SUB_SPELL_START sourced by an entity not yet created", "ENTITY_REF", 1,
            Insert(1, new PowerHistorySubSpellStart { Source = lastFullId }, new PowerHistorySubSpellEnd()));
        yield return ("SUB_SPELL_START targeting an entity not yet created", "ENTITY_REF", 1,
            Insert(1, new PowerHistorySubSpellStart { Targets = new List<int> { lastFullId } }, new PowerHistorySubSpellEnd()));

        yield return ("META_DATA naming an entity before its FULL_ENTITY", "META_REF", 1,
            Insert(1, new PowerHistoryMetaData { Type = SabberStoneCore.Enums.MetaDataType.TARGET, Info = new List<int> { lastFullId } }));

        yield return ("FULL_ENTITY repeated", "FULL_ENTITY_DUPLICATE", firstFull + 1, Insert(firstFull + 1, history[firstFull]));

        // A rewind forgets every entity: one it does not create again no longer exists, and one it creates again is no duplicate.
        var reset = new PowerHistoryResetGame { CreateGame = (PowerHistoryCreateGame)history[0] };
        yield return ("TAG_CHANGE after a rewind of an entity it did not create again", "ENTITY_REF", history.Count + 1,
            Insert(history.Count, reset, PowerHistoryBuilder.TagChange(history.OfType<PowerHistoryFullEntity>().First().Entity.Id,
                SabberStoneCore.Enums.GameTag.DAMAGE, 1)));
        yield return ("FULL_ENTITY again after a rewind", null, -1, Insert(history.Count, reset, history[firstFull]));
    }
}
#endif
