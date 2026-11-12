using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PegasusGame;

internal static partial class Program
{
    private static void MatchJournalNative()
    {
        NativeMatch.ClockFreeBot = true;
        var capture = new Capture(); var trace = new List<string>(); var choiceTrace = new List<string>(); var eventTrace = new List<string>();
        var match = new NativeMatch(capture.Send, "mage", "warrior", 27) { MatchId = "journal-smoke", BotTrace = trace.Add, ChoiceTrace = choiceTrace.Add, EventTrace = eventTrace.Add };
        string directory = Path.Combine(Path.GetTempPath(), "hs-journal-native-" + Guid.NewGuid());
        try
        {
            using (var record = PlaytestRecord.Open(directory, match.MatchId, () => PlaytestRecord.Header(match.MatchId,
                null, LocalDecks.Get("mage"), LocalDecks.Get("warrior"), 27, PlaytestRecord.EngineSourceIdentity,
                match.Game.OfflinePools.Hash, "native-test", null)))
            {
                match.Recorder = record;
                match.Start();
                match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
                while (match.Game.State != SabberStoneCore.Enums.State.COMPLETE && match.Game.Turn < 15)
                {
                    var options = capture.Last<AllOptions>(14);
                    int end = options.Options.FindIndex(o => o.Type_ == PegasusGame.Option.Type.END_TURN);
                    Check(end >= 0, "fixture human end-turn unavailable");
                    int turn = match.Game.Turn;
                    match.SendOption(options.Id, end, 0, -1, 0);
                    Check(match.Game.State == SabberStoneCore.Enums.State.COMPLETE || match.Game.Turn > turn, "fixture did not advance turn");
                }
                if (match.Game.State != SabberStoneCore.Enums.State.COMPLETE) match.Concede();
                // Presentation records after result must not break exact replay or close the journal early.
                record.OnFx(new Dictionary<string, object> { { "fxSequence", 1 }, { "event", "fixture-terminal-fx" },
                    { "taskList", 42 }, { "blockType", "TRIGGER" }, { "spellName", "Synthetic_Deathrattle" } });
            }
            string path = Path.Combine(directory, match.MatchId + ".jsonl");
            var rows = File.ReadAllLines(path).Select(line => JsonDocument.Parse(line)).ToArray();
            try
            {
                var actions = rows.Select(row => row.RootElement).Where(row => row.TryGetProperty("sequence", out _)).ToArray();
                Check(actions.Count(row => row.GetProperty("type").GetString() == "mulligan") == 2, "both mulligans not recorded");
                Check(actions.Any(row => row.GetProperty("type").GetString() == "decision"), "legacy NPC actions missing");
                Check(actions.Any(row => row.GetProperty("type").GetString() == "main-begin"), "main begin missing");
                Check(actions.Select(row => row.GetProperty("sequence").GetInt64()).SequenceEqual(Enumerable.Range(1, actions.Length).Select(n => (long)n)), "action sequence gap");
                Check(actions.Last().GetProperty("fingerprint").GetString() == MatchRecorder.Fingerprint(match.Game), "terminal fingerprint differs");
                Check(actions.All(row => row.GetProperty("rngState").GetString().Split(':').Length == 4), "RNG state missing");
                var opened = rows.Select(row => row.RootElement).Where(row => row.GetProperty("type").GetString() == "choice-open").ToArray();
                Check(opened.Length >= 2 && opened.Any(row => row.GetProperty("seat").GetInt32() == 1)
                    && opened.Any(row => row.GetProperty("seat").GetInt32() == 2), "both choice offers recorded");
                foreach (var row in opened)
                {
                    Check(row.GetProperty("offers").GetArrayLength() > 0, "choice offers not empty");
                    foreach (var offer in row.GetProperty("offers").EnumerateArray())
                    {
                        int id = offer.GetProperty("entityId").GetInt32();
                        Check(offer.GetProperty("cardId").GetString() == match.Game.IdEntityDic[id].Card.Id, "journal offered card id");
                        Check(offer.GetProperty("projection").GetProperty("present").ValueKind == JsonValueKind.True, "offer projected to client");
                    }
                }
                Check(choiceTrace.Count == opened.Length && choiceTrace.All(line => line.Contains("\"cardId\"")), "choice-open log carries offered card ids");
                Check(rows.Any(row => row.RootElement.GetProperty("type").GetString() == "result"), "result missing");
                // Match event log: the journal carries exactly the lines the log sink saw, tied to the action sequence, both seats.
                var eventRows = rows.Select(row => row.RootElement).Where(row => row.GetProperty("type").GetString() == "events").ToArray();
                var journalEvents = eventRows.SelectMany(row => row.GetProperty("lines").EnumerateArray().Select(line => line.GetString())).ToArray();
                Check(eventRows.Length > 0 && journalEvents.SequenceEqual(eventTrace), "journal events rows carry the logged lines");
                Check(eventRows.Select(row => row.GetProperty("afterSequence").GetInt64()).Zip(eventRows.Skip(1).Select(row => row.GetProperty("afterSequence").GetInt64()), (a, b) => b >= a).All(x => x)
                    && eventRows.All(row => row.GetProperty("afterSequence").GetInt64() <= actions.Length), "events rows reference completed actions in order");
                Check(eventTrace.All(line => line.StartsWith("m=journal- t=")), "every event line carries the match id and turn");
                Check(eventTrace.Any(line => line.Contains(" s=1 ")) && eventTrace.Any(line => line.Contains(" s=2 ")), "both seats logged");
                Check(eventTrace.Any(line => line.Contains(" DRAW ")) && eventTrace.Any(line => line.Contains(" PLAY ") || line.Contains(" CAST ")), "draws and plays logged");
                long journalBytes = new FileInfo(path).Length, eventBytes = eventRows.Sum(row => (long)row.ToString().Length);
                Console.WriteLine("INFO event rows " + eventTrace.Count + " lines, " + eventBytes + " of " + journalBytes + " journal bytes");
                Check(eventBytes < PlaytestRecord.MaximumEventBytes, "event rows stay inside their journal budget");
                Check(rows.Last().RootElement.GetProperty("type").GetString() == "fx"
                    && rows.Last().RootElement.GetProperty("afterSequence").GetInt64() == actions.Length, "terminal FX/action reference missing");
                Check(trace.Any(line => line.Contains(" human hero=")) && trace.Any(line => line.Contains(" npc hero=")), "NPC board log absent");
                Check(trace.Any(line => line.Contains(" candidates=[")) && actions.Any(row => row.TryGetProperty("candidates", out _)), "legacy search candidates missing");
            }
            finally { foreach (var row in rows) row.Dispose(); }
            // Gate-local artifact for the independent AiHarness replay command; never committed or deployed.
            string replayPath = Path.Combine(AppContext.BaseDirectory, "match-journal-smoke.jsonl");
            File.Copy(path, replayPath, true);
            Console.WriteLine("PASS native match journal, mulligans, legacy NPC, human actions, result; replay=" + replayPath);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
