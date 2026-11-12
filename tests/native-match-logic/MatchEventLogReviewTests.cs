using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

// Independent review of the match event log (2026-11-06): the log must never state a false fact. Each test below
// reproduces one review finding against a real engine run; they ran red on 9ed4983ea before the fixes.
internal static partial class Program
{
    // F6: events beyond the writer's queue must not close the journal and cost replay rows.
    private sealed class GatedWriter : StringWriter
    {
        internal readonly ManualResetEventSlim Gate = new ManualResetEventSlim(false);
        public override void WriteLine(string value) { Gate.Wait(10000); base.WriteLine(value); }
        public override void Write(string value) { Gate.Wait(10000); base.Write(value); }
        public override void Flush() { Gate.Wait(10000); base.Flush(); }
    }
    private static void EventsCannotCloseJournal()
    {
        var writer = new GatedWriter();
        using var record = new PlaytestRecord(writer, new Dictionary<string, object> { { "type", "header" }, { "schema", 1 }, { "matchId", "gate0001" } }, queueCapacity: 4);
        for (int i = 0; i < 40; i++) record.OnEvents(new[] { "m=gate0001 t=1 n=" + i + " s=1 TURN turn=1" });
        Check(record.Enabled, "an event flood on a stalled disk must leave the journal open");
        record.OnResult("Won", 3);
        writer.Gate.Set();
        record.FlushAndWait(5000);
        Check(writer.ToString().Contains("\"type\":\"result\""), "the replay row still reached the journal:\n" + writer);
    }

    private static string ChoiceRows(bool withEvents, long budget, out int rows, out string text)
    {
        var writer = new StringWriter();
        var header = new Dictionary<string, object> { { "type", "header" }, { "schema", 1 }, { "matchId", "budget01" } };
        using (var record = new PlaytestRecord(writer, header, maximumBytes: 6000, eventBudgetBytes: budget))
        {
            for (int i = 0; i < 40; i++)
            {
                if (withEvents) record.OnEvents(new[] { "m=budget01 t=1 n=" + i + " s=1 TURN turn=1 " + new string('x', 100) });
                record.OnChoiceOpen(new Dictionary<string, object> { { "choiceId", i }, { "pad", new string('y', 120) } });
            }
            record.FlushAndWait(5000);
        }
        text = writer.ToString();
        rows = Regex.Matches(text, "\"type\":\"choice-open\"").Count;
        return text;
    }
    // Replay-class rows (choice-open stands in: same non-fx path, same size limit) are written identically with and without
    // events: events have their own byte budget, so they can never take replay rows' room in the journal.
    private static void EventBudgetSeparate()
    {
        Check(PlaytestRecord.MaximumEventBytes == 3L * 1024 * 1024, "event budget is 3 MiB");
        ChoiceRows(false, 1500, out int plain, out string plainText);
        ChoiceRows(true, 1500, out int mixed, out string mixedText);
        Check(plain > 5 && plain == mixed, "same replay rows with and without events: " + plain + " vs " + mixed);
        Check(plainText.Contains("size-limit") == mixedText.Contains("size-limit"), "events do not move the replay size limit");
        var eventRows = Regex.Matches(mixedText, "\"type\":\"events\"").Count;
        Check(eventRows > 0 && Regex.Matches(mixedText, "\"truncated\":\"event rows stop at 1500 bytes").Count == 1, "events stop at their own budget with one marker (" + eventRows + " rows)");
        long written = Regex.Matches(mixedText, "(?m)^\\{\"type\":\"events\".*$").Cast<Match>().Sum(m => (long)m.Length + 1);
        Check(written <= 1500 + 400, "event bytes stay inside the budget: " + written);
    }
}
