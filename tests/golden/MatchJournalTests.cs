using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

public static class MatchJournalTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static Game Opening()
    {
        var deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList();
        var game = new Game(new GameConfig { Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.DEATHKNIGHT,
            Player1Deck = deck, Player2Deck = deck.ToList(), StartPlayer = 2, RandomSeed = 27,
            SkipMulligan = true, Shuffle = false, History = true, Logging = false });
        game.StartGame(); return game;
    }
    public static void TestDecisionSearchProvenance()
    {
        var output = new StringWriter();
        using (var record = new PlaytestRecord(output, new Dictionary<string, object> { { "type", "header" } })) {
            var action = BotAction.From(Opening().CurrentPlayer.Options().First());
            var decision = new GoldenDecision(action, 1, 32, 2, "Nodes", null, null) {
                SearchInfo = new DecisionSearchInfo { PlanNodeCap = 1926, SearchBudgetMilliseconds = 5000,
                    Interrupted = true, ValidationNodeCap = 7000, ValidationNodes = 2145, UnscheduledNodes = 123, ValidationCompared = true,
                    ValidationDeadline = true, ValidationFallback = true, Mistake = "TradeTarget" } };
            record.OnDecision(new GoldenContext(2, 1, 71, NpcCatalog.All[0]), decision, "incomplete", true, 32, 5, 2);
            record.OnPostAction(1, "hash"); Check(record.FlushAndWait(5000), "provenance flush");
        }
        using (var row = JsonDocument.Parse(output.ToString().Split('\n')[1])) {
            var fields = row.RootElement;
            Check(fields.GetProperty("planNodeCap").GetInt32() == 1926
                && fields.GetProperty("searchBudgetMs").GetDouble() == 5000
                && fields.GetProperty("searchInterrupted").GetBoolean() && fields.GetProperty("reuse").GetBoolean()
                && fields.GetProperty("validationNodeCap").GetInt32() == 7000
                && fields.GetProperty("validationNodes").GetInt32() == 2145
                && fields.GetProperty("unscheduledNodes").GetInt32() == 123
                && fields.GetProperty("validationCompared").GetBoolean()
                && fields.GetProperty("validationDeadline").GetBoolean()
                && fields.GetProperty("validationFallback").GetBoolean()
                && fields.GetProperty("mistakeKind").GetString() == "TradeTarget", "missing decision search provenance");
        }
    }
    public static void TestSnapshotHiddenCardsOrderKeywordsAndNoMutation()
    {
        var game = Opening();
        Entity.FromCard(game.Player1, Cards.FromId("CS2_029"), zone: game.Player1.HandZone);
        Entity.FromCard(game.Player1, Cards.FromId("EX1_287"), zone: game.Player1.SecretZone);
        Entity.FromCard(game.Player2, Cards.FromId("CS2_032"), zone: game.Player2.HandZone);
        var ghoul = (Minion)Entity.FromCard(game.Player2, Cards.FromId("HERO_11bpt"), zone: game.Player2.BoardZone);
        ghoul.IsExhausted = false;
        var guard = (Minion)Entity.FromCard(game.Player2, Cards.FromId("CS2_179"), zone: game.Player2.BoardZone);
        guard.HasDivineShield = true; guard.IsFrozen = true;
        guard[GameTag.STEALTH] = 1; guard[GameTag.POISONOUS] = 1; guard[GameTag.TAG_ONE_TURN_EFFECT] = 1;
        game.Player2.UsedMana = 2; game.Player2.BaseMana = 4;
        game.Player2.OverloadLocked = 1; game.Player2.OverloadOwed = 2;
        game.Player2.Hero.Armor = 3;
        var axe = Entity.FromCard(game.Player2, Cards.FromId("CS2_106"), zone: game.Player2.HandZone); axe.Cost = 0;
        Check(game.Process(PlayCardTask.Any(game.Player2, axe)), "weapon fixture failed");
        var location = Entity.FromCard(game.Player2, Cards.FromId("CORE_REV_990"), zone: game.Player2.BoardZone);
        string before = MatchRecorder.Fingerprint(game), rng = MatchRecorder.RandomState(game);
        string human = MatchSnapshot.Side(game.Player1, false), npc = MatchSnapshot.Side(game.Player2, true);
        Check(human.Contains(",CS2_029] deck=") && human.Contains("secrets=1[EX1_287]"), "human hand/Secret unreadable: " + human);
        Check(npc.Contains("CS2_032") && npc.Contains("mana=2/4") && npc.Contains("overload=1/2")
            && npc.Contains("+3 weapon=CS2_106:3/2"), "hand/hero/weapon/mana missing: " + npc);
        Check(npc.IndexOf("HERO_11bpt#", StringComparison.Ordinal) < npc.IndexOf("CS2_179#", StringComparison.Ordinal)
            && npc.Contains("charge|dies-at-end-of-turn|can-attack"), "charge ghoul state/order missing: " + npc);
        foreach (string keyword in new[] { "taunt", "divine-shield", "frozen", "stealth", "poisonous", "temporary", "cannot-attack", "durability=" })
            Check(npc.Contains(keyword), "missing keyword " + keyword);
        Check(npc.Contains(location.Card.Id + "#" + location.Id), "location omitted from mixed board");
        Check(before == MatchRecorder.Fingerprint(game) && rng == MatchRecorder.RandomState(game), "snapshot mutated engine/RNG");
        ghoul.IsSilenced = true;
        Check(!MatchSnapshot.Side(game.Player2, true).Contains("dies-at-end-of-turn"), "silenced ghoul still reports death trigger");
    }
    public static void TestRotationKeepsNewestFiftyAndOtherFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), "journal-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            for (int i = 0; i < 55; i++)
            {
                string path = Path.Combine(directory, "match-" + i + ".jsonl");
                File.WriteAllText(path, "existing-" + i);
                File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 1).AddMinutes(i));
            }
            File.WriteAllText(Path.Combine(directory, "profile.xml"), "preserve");
            using (var record = PlaytestRecord.Open(directory, "current", () => new Dictionary<string, object> { { "type", "header" } }))
            {
                Check(record.FlushAndWait(5000) && record.Enabled, "rotation disabled recorder");
                record.OnImplicitAction("main-begin"); record.OnPostAction(1, "hash");
                Check(record.FlushAndWait(5000), "journal flush timed out");
                Check(Directory.GetFiles(directory, "*.jsonl").Length == 50, "not bounded to fifty");
                Check(!File.Exists(Path.Combine(directory, "match-5.jsonl")) && File.Exists(Path.Combine(directory, "match-6.jsonl")), "wrong files rotated");
                Check(File.ReadAllText(Path.Combine(directory, "match-54.jsonl")) == "existing-54", "recent journal overwritten");
            }
            Check(File.ReadAllText(Path.Combine(directory, "profile.xml")) == "preserve", "rotation touched save");
        }
        finally { Directory.Delete(directory, true); }
    }
    public static void TestSizeCapUtf8AndRecorderFailureIsolation()
    {
        var lines = new StringWriter();
        var record = new PlaytestRecord(lines, new Dictionary<string, object> { { "type", "header" } },
            message => { throw new Exception("broken logger"); }, 1024);
        var wrapper = new MatchRecorder(message => { throw new Exception("broken outer logger"); }) { Inner = record };
        for (int i = 0; i < 100; i++)
            wrapper.OnEmote(2, new string('\u754c', 50), i);
        Check(record.FlushAndWait(5000), "size-limit completion timed out");
        Check(!record.Enabled && Encoding.UTF8.GetByteCount(lines.ToString()) <= 1024, "byte cap exceeded");
        Check(lines.ToString().Contains("recording-stopped") && lines.ToString().Contains("size-limit"), "truncation not marked");
        foreach (string line in lines.ToString().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            using (JsonDocument.Parse(line)) { }
        record.Dispose();
        using (var active = new PlaytestRecord(new StringWriter(), new Dictionary<string, object> { { "type", "header" } }))
            new MatchRecorder(message => { throw new Exception("broken outer logger"); }) {
                Inner = active, ReplayHash = game => { throw new IOException("hash unavailable"); }
            }.PostAction(Opening());
    }
    public static void TestTotalBudgetReservesActiveAllowanceOldestFirst()
    {
        string directory = Path.Combine(Path.GetTempPath(), "journal-budget-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            for (int i = 0; i < 8; i++)
            {
                string path = Path.Combine(directory, "old-" + i + ".jsonl");
                using (var file = File.Create(path)) file.SetLength(PlaytestRecord.MaximumBytes);
                File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 1).AddMinutes(i));
            }
            File.WriteAllText(Path.Combine(directory, "unrelated.tsv"), "preserve");
            using (var record = PlaytestRecord.Open(directory, "active", () => new Dictionary<string, object> { { "type", "header" } }))
            {
                Check(record.FlushAndWait(5000) && record.Enabled, "budget rotation failed");
                var old = new DirectoryInfo(directory).GetFiles("old-*.jsonl");
                Check(old.Length == 5 && old.Sum(f => f.Length) + PlaytestRecord.MaximumBytes <= PlaytestRecord.TotalMaximumBytes,
                    "active journal growth can exceed total cap");
                Check(!File.Exists(Path.Combine(directory, "old-2.jsonl")) && File.Exists(Path.Combine(directory, "old-3.jsonl")), "not oldest-first");
            }
            Check(File.ReadAllText(Path.Combine(directory, "unrelated.tsv")) == "preserve", "budget touched unrelated reports");
        }
        finally { Directory.Delete(directory, true); }
    }
    public static void TestSnapshotDeltasReconstructExactly()
    {
        var game = Opening(); var lines = new StringWriter();
        var expected = new List<string[]>();
        using (var record = new PlaytestRecord(lines, new Dictionary<string, object> { { "type", "header" } }))
            for (int i = 0; i < 4; i++)
            {
                game.Player1.Hero.Health -= i;
                expected.Add(new[] { MatchSnapshot.Side(game.Player1, false), MatchSnapshot.Side(game.Player2, true) });
                record.CaptureState(game); record.OnPostAction(i + 1, "hash");
            }
        var rows = lines.ToString().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string[] previous = new string[2]; bool sawDelta = false;
        for (int i = 1; i < rows.Length; i++)
            using (var doc = JsonDocument.Parse(rows[i]))
                for (int side = 0; side < 2; side++)
                {
                    var value = doc.RootElement.GetProperty(side == 0 ? "human" : "npc");
                    if (value.ValueKind == JsonValueKind.Array)
                    {
                        sawDelta = true;
                        int prefix = value[0].GetInt32(), suffix = value[1].GetInt32();
                        previous[side] = previous[side].Substring(0, prefix) + value[2].GetString()
                            + previous[side].Substring(previous[side].Length - suffix);
                    }
                    else previous[side] = value.GetString();
                    Check(previous[side] == expected[i - 1][side], "snapshot reconstruction differs");
                }
        Check(sawDelta, "unchanged board repeated in full");
    }
    public static void TestFxBatchesActionTagsTerminalTailAndSizeFailure()
    {
        var lines = new StringWriter();
        using (var record = new PlaytestRecord(lines, new Dictionary<string, object> { { "type", "header" } }))
        {
            record.OnFx(new Dictionary<string, object> { { "event", "opening" } });

            record.OnPostAction(1, "hash");
            record.OnFx(new Dictionary<string, object> { { "event", "attach" }, { "taskList", 42 } });
            Check(record.FlushAndWait(5000), "FX flush timed out");
            for (int i = 0; i < 31; i++) record.OnFx(new Dictionary<string, object> { { "event", "batched" } });

            record.OnFx(new Dictionary<string, object> { { "event", "batch-boundary" } });
            Check(record.FlushAndWait(5000) && lines.ToString().Contains("batch-boundary"), "FX queue lost batch");
            record.OnResult("Won", 15);
            Check(record.Enabled, "result closed before terminal animations");
            record.OnFx(new Dictionary<string, object> { { "event", "finished" } });
        }
        var rows = lines.ToString().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        using (var before = JsonDocument.Parse(rows[1]))
            Check(before.RootElement.GetProperty("afterSequence").GetInt64() == 0, "opening FX action tag");
        using (var after = JsonDocument.Parse(rows[3]))
            Check(after.RootElement.GetProperty("afterSequence").GetInt64() == 1 && !after.RootElement.TryGetProperty("sequence", out _), "FX confused with replay action");
        Check(rows.Last().Contains("finished"), "dispose lost terminal FX batch");
        var capped = new StringWriter();
        using (var record = new PlaytestRecord(capped, new Dictionary<string, object> { { "type", "header" } }, maximumBytes: 1024))
        {
            for (int i = 0; i < 100; i++) record.OnFx(new Dictionary<string, object> { { "spellName", new string('\u754c', 40) } });
            Check(record.FlushAndWait(5000), "FX cap completion timed out");
            Check(!record.Enabled && Encoding.UTF8.GetByteCount(capped.ToString()) <= 1024 && capped.ToString().Contains("size-limit"), "FX bypasses journal cap");
        }
        var broken = new PlaytestRecord(new BrokenWriter(), new Dictionary<string, object> { { "type", "header" } }, _ => { throw new Exception(); });
        broken.OnFx(new Dictionary<string, object>()); broken.Flush(); broken.Dispose();
        Check(!broken.Enabled, "FX I/O failure not contained");
        var brokenFlush = new PlaytestRecord(new BrokenFlushWriter(), new Dictionary<string, object> { { "type", "header" } });
        brokenFlush.OnFx(new Dictionary<string, object>()); brokenFlush.Flush(); brokenFlush.Dispose();
        Check(!brokenFlush.Enabled, "FX batch flush failure not contained");
    }
    public static void TestNonBlockingEnqueueOverflowAndImmutableRows()
    {
        var output = new BlockingWriter();
        var header = new Dictionary<string, object> { { "type", "header" }, { "schema", 1 } };
        var record = new PlaytestRecord(output, header, queueCapacity: 3);
        try
        {
            record.OnEmote(2, "accepted", 0);
            Check(output.Entered.Wait(5000), "worker did not reach blocked storage");
            var row = new Dictionary<string, object> { { "event", "immutable" } };
            var enqueue = System.Threading.Tasks.Task.Run(() => record.OnFx(row));
            Check(enqueue.Wait(500), "enqueue waited for blocked disk writer");
            row["event"] = "mutated"; header["schema"] = 99;
            var overflow = System.Threading.Tasks.Task.Run(() => record.OnPostAction(1, "lost"));
            Check(overflow.Wait(500) && !record.Enabled, "overflow blocked or failed to stop recording immediately");
            var shutdown = System.Diagnostics.Stopwatch.StartNew();
            record.Dispose();
            Check(shutdown.ElapsedMilliseconds < 1000, "shutdown exceeded short bounded wait");
            shutdown.Restart(); record.Dispose();
            Check(shutdown.ElapsedMilliseconds < 100, "repeated bridge disposal waited on disk again");
            Check(output.ThreadId != System.Threading.Thread.CurrentThread.ManagedThreadId, "journal touched storage on caller thread");
            output.Release.Set();
            Check(record.FlushAndWait(5000), "overflow prefix did not drain");
            var rows = output.ToString().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Check(rows.Length == 4 && !output.ToString().Contains("mutated") && !output.ToString().Contains("lost"), "overflow lost accepted prefix or captured mutable row");
            using (var first = JsonDocument.Parse(rows[0])) Check(first.RootElement.GetProperty("schema").GetInt32() == 1, "header was not captured immutably");
            using (var last = JsonDocument.Parse(rows.Last()))
                Check(last.RootElement.GetProperty("type").GetString() == "recording-stopped"
                    && last.RootElement.GetProperty("reason").GetString() == "queue-overflow", "overflow not explicitly marked incomplete");
        }
        finally { output.Release.Set(); record.Dispose(); }
    }
    public static void TestRetentionSkipsReadOnlyJournalAndKeepsRecording()
    {
        string directory = Path.Combine(Path.GetTempPath(), "journal-locked-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        string locked = Path.Combine(directory, "old-0.jsonl");
        try
        {
            for (int i = 0; i < 51; i++)
            {
                string path = Path.Combine(directory, "old-" + i + ".jsonl");
                File.WriteAllText(path, "preserved-" + i);
                File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 1).AddMinutes(i));
            }
            File.SetAttributes(locked, FileAttributes.ReadOnly);
            var warnings = new List<string>();
            using (var record = PlaytestRecord.Open(directory, "current", () => new Dictionary<string, object> { { "type", "header" } }, warnings.Add))
            {
                record.OnPostAction(1, "hash");
                Check(record.FlushAndWait(5000) && record.Enabled, "cleanup failure disabled new journal");
                Check(File.ReadAllLines(Path.Combine(directory, "current.jsonl")).Length == 2, "new journal left empty");
                Check(Directory.GetFiles(directory, "*.jsonl").Length == 50 && File.ReadAllText(locked) == "preserved-0", "retention did not try other eligible files");
                Check(warnings.Any(w => w.Contains("old-0.jsonl")), "cleanup failure not reported");
            }
        }
        finally { File.SetAttributes(locked, FileAttributes.Normal); Directory.Delete(directory, true); }
    }
    public static void TestRetentionPreservesOtherOpenJournals()
    {
        string directory = Path.Combine(Path.GetTempPath(), "journal-active-" + Guid.NewGuid());
        try
        {
            using (var first = PlaytestRecord.Open(directory, "first", () => new Dictionary<string, object> { { "type", "header" } }))
            {
                first.OnPostAction(1, "first");
                Check(first.FlushAndWait(5000), "first active journal timed out");
                string path = Path.Combine(directory, "first.jsonl");
                File.SetLastWriteTimeUtc(path, new DateTime(2000, 1, 1));
                var bytes = File.ReadAllBytes(path);
                for (int i = 0; i < 49; i++)
                {
                    string old = Path.Combine(directory, "old-" + i + ".jsonl");
                    File.WriteAllText(old, "old");
                    File.SetLastWriteTimeUtc(old, new DateTime(2010, 1, 1).AddMinutes(i));
                }
                using (var second = PlaytestRecord.Open(directory, "second", () => new Dictionary<string, object> { { "type", "header" } }))
                {
                    second.OnPostAction(1, "second");
                    Check(second.FlushAndWait(5000) && second.Enabled, "second journal timed out");
                    Check(File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(bytes), "retention removed another active journal");
                    Check(Directory.GetFiles(directory, "*.jsonl").Length == 50, "active journals not counted in retention");
                    first.OnPostAction(2, "later");
                    Check(first.FlushAndWait(5000) && File.ReadAllLines(path).Length == 3, "active journal lost its later writes");
                }
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    private sealed class BlockingWriter : StringWriter
    {
        internal readonly System.Threading.ManualResetEventSlim Entered = new System.Threading.ManualResetEventSlim();
        internal readonly System.Threading.ManualResetEventSlim Release = new System.Threading.ManualResetEventSlim();
        internal int ThreadId;
        public override void WriteLine(string value)
        {
            ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            Entered.Set();
            if (!Release.Wait(5000)) throw new IOException("blocked writer test timed out");
            base.WriteLine(value);
        }
    }
    private sealed class BrokenWriter : StringWriter
    {
        public override void WriteLine(string value) { throw new IOException("disk failed"); }
    }
    private sealed class BrokenFlushWriter : StringWriter
    {
        public override void Flush() { throw new IOException("flush failed"); }
    }
    public static void TestLegacyActionsStateRngAndConcessionAreReplayable()
    {
        var game = Opening(); var lines = new StringWriter();
        var header = PlaytestRecord.Header("legacy", null, LocalDecks.All[0], LocalDecks.All[1], 27, "source", "pool", "build", null);
        using (var record = new PlaytestRecord(lines, header))
        {
            var wrapper = new MatchRecorder { Inner = record };
            record.OnNpcAction(game.Turn, BotAction.From(EndTurnTask.Any(game.Player2)).ToString());
            wrapper.PostAction(game);
            record.OnNpcAction(game.Turn, BotAction.From(EndTurnTask.Any(game.Player2)).ToString());
            record.OnNpcAction(game.Turn, BotAction.From(ConcedeTask.Any(game.Player2)).ToString());
            record.OnConcession(2, game.Turn); wrapper.PostAction(game);
        }
        var rows = lines.ToString().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        using (var row = JsonDocument.Parse(rows[1]))
        {
            Check(row.RootElement.GetProperty("seat").GetInt32() == 2 && row.RootElement.GetProperty("type").GetString() == "decision", "legacy action missing");
            Check(row.RootElement.GetProperty("fingerprint").GetString() == MatchRecorder.Fingerprint(game), "wrong state hash");
            Check(row.RootElement.GetProperty("rngState").GetString() == MatchRecorder.RandomState(game), "RNG not captured");
            Check(row.RootElement.GetProperty("npc").GetString().StartsWith("npc hero="), "snapshot not in action row");
        }
        using (var row = JsonDocument.Parse(rows[3]))
            Check(row.RootElement.GetProperty("conceded").GetBoolean() && row.RootElement.GetProperty("action").GetString().StartsWith("CONCEDE:"), "NPC concession recorded as end-turn");
    }
}
