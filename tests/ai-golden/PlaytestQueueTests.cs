using System;
using System.IO;
using System.Linq;
using System.Text.Json;

public static class PlaytestQueueTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void TestQueueValidationAndProofIsolation()
    {
        foreach (var npc in NpcCatalog.All)
        {
            var q = PlaytestQueue.Parse("npc=" + npc.Id + "\nemotes=on\nhover=off\nskin=off\ncardback=12\nturnNodes=123\n");
            var presentation = q.Presentation();
            Check(presentation.Emotes && !presentation.Hover && !presentation.Skin && presentation.CardBack
                && presentation.CardBackOverride == 12, "presentation knobs not applied");
            Check(q.Profile == npc && q.TurnNodes == 123 && q.Emotes && q.CardBack == 12, "knobs lost");
            Check(q.Opponent(false).Id == npc.DeckId && q.Opponent(true) == null, "proof queue override");
        }
        // Absent and "npc" knobs show the NPC's own world presentation; explicit values keep their meaning.
        {
            var natural = PlaytestQueue.Parse("npc=npc.golden.1").Presentation();
            Check(natural.Emotes && natural.Hover, "absent knobs must default to the NPC's own emotes and hover");
            var profile = NpcCatalog.All[0];
            Check(natural.Skin == !string.IsNullOrEmpty(profile.HeroSkin) && natural.CardBack == (profile.CardBack > 0)
                && natural.CardBackOverride == null, "absent skin/cardback must follow the NPC profile");
            var explicitNpc = PlaytestQueue.Parse("npc=npc.golden.1\nemotes=npc\nhover=npc\nskin=npc\ncardback=npc").Presentation();
            Check(explicitNpc.Emotes == natural.Emotes && explicitNpc.Hover == natural.Hover && explicitNpc.Skin == natural.Skin
                && explicitNpc.CardBack == natural.CardBack, "npc must equal absent");
            var off = PlaytestQueue.Parse("npc=npc.golden.1\nemotes=off\nhover=off\nskin=off\ncardback=off").Presentation();
            Check(!off.Emotes && !off.Hover && !off.Skin && !off.CardBack, "explicit off must stay off");
            var forced = PlaytestQueue.Parse("npc=npc.golden.1\nskin=on\ncardback=77").Presentation(id => false, id => false);
            Check(forced.Skin && forced.CardBack && forced.CardBackOverride == 77, "explicit on/id must not depend on client lookups");
            var unknown = PlaytestQueue.Parse("npc=npc.golden.1\nskin=npc\ncardback=npc");
            var fallback = unknown.Presentation(id => false, id => false);
            Check(!fallback.Skin && !fallback.CardBack, "natural cosmetics must fall back when the client does not know them");
            Check((string)unknown.Knobs["emotes"] == "npc" && (string)unknown.Knobs["cardback"] == "npc", "knob echo");
        }
        var seeded = PlaytestQueue.Parse("npc=npc.golden.1\nseed=17");
        Check(seeded.Seed == 17 && seeded.SelectSeed(99) == 17 && (int)seeded.Knobs["seed"] == 17, "seed override and telemetry");
        Check(PlaytestQueue.Parse("npc=npc.golden.1").SelectSeed(99) == 99, "absent seed retains normal generation");
        foreach (var text in new[] { "seed=17", "npc=unknown\nseed=17", "npc=npc.golden.1\nseed=0", "npc=npc.golden.1\nseed=-1", "npc=npc.golden.1\nseed=2147483648", "npc=npc.golden.1\nseed=1\nseed=2", "", "npc=unknown", "npc=npc.golden.1\nemotes=maybe", "npc=npc.golden.1\nhover=true", "npc=npc.golden.1\nturnNodes=0",
            "npc=npc.golden.1\nturnNodes=2147483648", "npc=npc.golden.1\ncardback=-1", "npc=npc.golden.1\nx=y", "npc=npc.golden.1\nnpc=npc.golden.2" })
        {
            bool rejected = false;
            try { PlaytestQueue.Parse(text); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "accepted invalid queue: " + text);
        }
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".knobs");
        try {
            Check(PlaytestQueue.Read(path) == null, "missing queue");
            File.WriteAllText(path, "npc=unknown");
            string error = null;
            Check(PlaytestQueue.Read(path, s => error = s) == null && error != null, "invalid queue must log + ignore");
        } finally { File.Delete(path); }
    }
    public static void TestRecordFlushAndExistingFilePreservation()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var q = PlaytestQueue.Parse("npc=npc.golden.3");
        try {
            using (var recorder = PlaytestRecord.Open(directory, "match-1", () => PlaytestRecord.Header("match-1", q.Profile,
                LocalDecks.All[0], LocalDecks.Get(q.Profile.DeckId), 42, "source", "pool", "build", q)))
            {
                recorder.OnMulligan(2, new int[0]); recorder.OnPostAction(1, "abc");
                Check(recorder.FlushAndWait(5000), "journal flush timed out");
                string path = Path.Combine(directory, "match-1.jsonl");
                Check(File.ReadAllLines(path).Length == 2, "events not flushed while match still open");
                var bytes = File.ReadAllBytes(path);
                using (var duplicate = PlaytestRecord.Open(directory, "match-1", () => null))
                {
                    Check(duplicate.FlushAndWait(5000), "duplicate open did not complete");
                    Check(!duplicate.Enabled, "existing record overwritten");
                }
                Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "existing record bytes changed");
                recorder.OnResult("Won", 12);
                Check(recorder.Enabled && File.ReadAllLines(path).Length == 3, "result not flushed or terminal FX disabled");
                recorder.Dispose();
                Check(!recorder.Enabled, "bridge disposal did not close journal");
            }
            string escaped = PlaytestRecord.Serialize("a\"\\\n\t");
            using (var json = JsonDocument.Parse(escaped)) Check(json.RootElement.GetString() == "a\"\\\n\t", "JSON escaping");
        } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    public static void TestCorrectedDecisionAndImplicitActions()
    {
        var lines = new StringWriter();
        var game = new SabberStoneCore.Model.Game(new SabberStoneCore.Config.GameConfig {
            Player1HeroClass = SabberStoneCore.Enums.CardClass.MAGE, Player2HeroClass = SabberStoneCore.Enums.CardClass.MAGE });
        var wrong = BotAction.From(SabberStoneCore.Tasks.PlayerTasks.ConcedeTask.Any(game.Player2));
        var actual = BotAction.From(SabberStoneCore.Tasks.PlayerTasks.EndTurnTask.Any(game.Player2));
        using (var record = new PlaytestRecord(lines, new System.Collections.Generic.Dictionary<string, object> { { "type", "header" } }))
        {
            record.OnDecision(new GoldenContext(2, 1, 71, NpcCatalog.All[0]),
                new GoldenDecision(wrong, 0, 0, 0, "Forced", null, null), "none", false, 0, 0, 0);
            record.CorrectDecision(new GoldenDecision(actual, 0, 0, 0, "Forced", "resolve", null, "fallback"));
            record.OnPostAction(1, "hash");
            record.OnImplicitAction("main-begin"); record.OnPostAction(2, "hash2");
            record.OnImplicitAction("autonomous"); record.OnPostAction(3, "hash3");
        }
        var rows = lines.ToString().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        using (var decision = JsonDocument.Parse(rows[1]))
            Check(decision.RootElement.GetProperty("action").GetString() == actual.ToString()
                && decision.RootElement.GetProperty("fallback").GetString() == "resolve", "record retained unresolvable action");
        using (var begin = JsonDocument.Parse(rows[2]))
            Check(begin.RootElement.GetProperty("type").GetString() == "main-begin", "implicit MainBegin not replayable");
        using (var autonomous = JsonDocument.Parse(rows[3]))
            Check(autonomous.RootElement.GetProperty("type").GetString() == "autonomous", "implicit autonomous action not replayable");
    }

    public static void TestHumanTimingWithFakeClock()
    {
        var start = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
        double ms = 0;
        var lines = new StringWriter();
        using (var recorder = new PlaytestRecord(lines,
            new System.Collections.Generic.Dictionary<string, object> { { "type", "header" }, { "startedUtc", "deferred" } },
            maximumBytes: 4096, queueCapacity: 16, utcNow: () => start.AddMilliseconds(ms), elapsedMilliseconds: () => ms))
        {
            ms = 90000;
            recorder.OnMulligan(2, new int[0]); recorder.OnPostAction(1, "npc");
            recorder.OnMulligan(1, new int[0]);
            recorder.OnHumanAction(0, "choice", -1, new int[0], false); recorder.OnPostAction(2, "human");
            ms = 100000;
            recorder.OnHumanAction(1, "play", 0, new int[0], false); recorder.OnPostAction(3, "play");
            ms = 170000;
            // Legacy target emits the human-action hook before the mulligan hook.
            recorder.OnHumanAction(0, "choice", -1, new int[0], false);
            recorder.OnMulligan(1, new int[0]); recorder.OnPostAction(4, "legacy");
            ms = 300000;
            recorder.OnHumanAction(5, "concede", 0, new int[0], true);
            recorder.OnConcession(1, 5); recorder.OnPostAction(5, "concede");
            ms = 400000;
            recorder.OnConcession(2, 5); recorder.OnPostAction(6, "npc-concede");
            // Result time is recorded, but no trailing gap after the last human action is counted.
            recorder.OnResult("Lost", 5);
            Check(recorder.Enabled && recorder.LastSequence == 6, "timed result keeps journal open and last sequence");
            Check(lines.ToString().Contains("\"endedUtc\""), "timed result flushed before return");
        }
        var rows = lines.ToString().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => JsonDocument.Parse(s)).ToArray();
        try {
            var result = rows.Last().RootElement;
            Check(result.GetProperty("endedUtc").GetString() == start.AddMilliseconds(ms).ToString("o"), "result UTC end time");
            Check(result.GetProperty("humanActions").GetInt32() == 4, "human hooks count once, NPC ignored");
            Check(result.GetProperty("longestHumanGapMs").GetDouble() == 130000, "longest human gap");
            Check(result.GetProperty("humanIdleOver60sMs").GetDouble() == 110000, "sum excess over 60 seconds including initial gap");
            Check(rows[0].RootElement.GetProperty("startedUtc").GetString() == start.ToString("o"), "header uses recorder start");
            foreach (var row in rows.Take(rows.Length - 1))
                foreach (string field in new[] { "endedUtc", "humanActions", "longestHumanGapMs", "humanIdleOver60sMs" })
                    Check(!row.RootElement.TryGetProperty(field, out _), "timing fields only on result");
        } finally { foreach (var row in rows) row.Dispose(); }
        var empty = new StringWriter();
        using (var recorder = new PlaytestRecord(empty, new System.Collections.Generic.Dictionary<string, object> { { "type", "header" } },
            utcNow: () => start, elapsedMilliseconds: () => 0)) recorder.OnResult("Tied", 0);
        using (var result = JsonDocument.Parse(empty.ToString().Split('\n')[1]))
            Check(result.RootElement.GetProperty("humanActions").GetInt32() == 0
                && result.RootElement.GetProperty("longestHumanGapMs").GetDouble() == 0
                && result.RootElement.GetProperty("humanIdleOver60sMs").GetDouble() == 0, "no human actions yield zero gaps");
    }

    public static void TestRecordJsonAndFailureIsolation()
    {
        Check(!string.IsNullOrEmpty(PlaytestRecord.EngineSourceIdentity), "source identity missing");
        Check(MatchRecorder.PersonalityRoll(1, "test", 3, "pause-pre") != MatchRecorder.PersonalityRoll(1, "test", 3, "pause-act")
            && MatchRecorder.PersonalityRoll(1, "test", 3, "emote") != MatchRecorder.PersonalityRoll(1, "test", 3, "pause-act"), "personality streams collide");
        var lines = new StringWriter();
        var q = PlaytestQueue.Parse("npc=npc.golden.2");
        using (var recorder = new PlaytestRecord(lines, PlaytestRecord.Header("test", q.Profile,
            LocalDecks.All[0], LocalDecks.Get(q.Profile.DeckId), 42, PlaytestRecord.EngineSourceIdentity, "pool", "build", q)))
        {
            recorder.OnMulligan(1, new[] { 7, 8 });
            recorder.OnHumanAction(0, "choice", -1, new[] { 7, 8 }, false);
            recorder.OnPostAction(1, "hash");
            recorder.OnEmote(2, "WellPlayed", 1);
            recorder.OnResult("Won", 12);
        }
        var events = lines.ToString().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => JsonDocument.Parse(s)).ToArray();
        try {
            Check(events.Length == 4, "duplicate human mulligan or missing event");
            var header = events[0].RootElement;
            Check(header.GetProperty("schema").GetInt32() == 1 && header.GetProperty("npcId").GetString() == "npc.golden.2", "schema");
            Check(!string.IsNullOrEmpty(header.GetProperty("engineSourceIdentity").GetString()), "record source identity missing");
            Check(header.GetProperty("humanDeck").GetProperty("cards").GetArrayLength() == 30, "deck list");
            Check(events[1].RootElement.GetProperty("fingerprint").GetString() == "hash", "fingerprint association");
            Check(events[1].RootElement.GetProperty("type").GetString() == "mulligan", "mulligan association");
        } finally { foreach (var e in events) e.Dispose(); }
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        string profile = Path.Combine(directory, "profile"); File.WriteAllText(profile, "preserve");
        try {
            string error = null;
            using (var recorder = PlaytestRecord.Open(profile, "test", () => PlaytestRecord.Header("test", q.Profile,
                LocalDecks.All[0], LocalDecks.Get(q.Profile.DeckId), 42, "source", "pool", "build", q), s => error = s))
                recorder.OnMulligan(2, new int[0]);
            Check(File.ReadAllText(profile) == "preserve" && error != null, "record failure affected existing bytes");
        } finally { Directory.Delete(directory, true); }
    }
}
