using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static partial class Program
{
    // Regression for a frozen live match: the engine opened a choice (a Discover-style offer or a target pick) whose
    // source card and offered cards the client had never been sent, so the player saw no prompt and the match stuck.
    // Recovery restores the missing entities, registers the choice with the client one frame later, and only then
    // submits the pick. The "negative control" below shows why: with the old back-to-back send the client's
    // acknowledgement arrives before the choice is registered and the choice stays stuck. Every recorded choice-open
    // row must also say which source and offers the client lacked.
    private static void ChoiceToolsNative()
    {
        foreach (string card in new[] { "TLC_245", "CORE_DS1_184", "EX1_319" })
        {
            var definition = Cards.FromId(card);
            var originalPower = definition.Power;
            if (card == "EX1_319") definition.Power = new Power { PowerTask = new DecisionBattlecryTask(
                "proof/choice-recovery/target/v1", new SelectEntityThenContinueTask(), new ArmorTask(1)) };
            string Run(bool recover)
            {
                var (capture, match) = ChoiceRetirementMatch(CardClass.ROGUE);
                var writer = new StringWriter(); var logs = new List<string>();
                using (var record = new PlaytestRecord(writer, new Dictionary<string, object> { { "type", "header" }, { "schema", 1 } }))
                {
                    match.Recorder = record; match.ChoiceTrace = logs.Add;
                    match.NativeEntityDiagnostic = id => new Dictionary<string, object> { { "present", false }, { "hasActor", false } };
                    if (card == "EX1_319") Generic.DrawCard(match.Game.Player1, Cards.FromId("CS2_182"));
                    var source = Generic.DrawCard(match.Game.Player1, Cards.FromId(card));
                    const int sourceAlias = 1900000000;
                    var aliases = (Dictionary<int, int>)typeof(NativeMatch).GetField("privateReturnIds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match);
                    var originals = (Dictionary<int, int>)typeof(NativeMatch).GetField("privateReturnOriginals", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match);
                    aliases[source.Id] = sourceAlias; originals[sourceAlias] = source.Id;
                    FlushFieldHistory(match);
                    var choice = ChoiceRetirementPlay(match, capture, source, card == "CORE_DS1_184" ? 0 : 1);
                    int pick = match.Game.Player1.Choice.Choices[0];
                    string fingerprint = MatchRecorder.Fingerprint(match.Game), rng = MatchRecorder.RandomState(match.Game);
                    var diagnostic = match.DiagnosticEntity(pick);
                    Check((int)match.DiagnosticEntity(source.Id)["clientEntityId"] == sourceAlias, "engine diagnostics locate native return alias");
                    Check((bool)diagnostic["exists"] && (string)diagnostic["cardId"] == match.Game.IdEntityDic[pick].Card.Id
                        && diagnostic.ContainsKey("zone") && diagnostic.ContainsKey("controller") && diagnostic.ContainsKey("creator")
                        && diagnostic.ContainsKey("tags"), "engine diagnostics inspect entity absent from client");
                    Check(!(bool)match.DiagnosticEntity(int.MaxValue)["exists"], "unknown engine id explicit");
                    Check(match.ValidateRecoveryChoice(choice.Id, new[] { pick }).SequenceEqual(new[] { choice.Entities[0] }), "engine offer maps to client offer including deck proxy");
                    if (choice.Entities[0] == pick)
                    {
                        aliases[pick] = sourceAlias + 1; originals[sourceAlias + 1] = pick;
                        Check(match.ValidateRecoveryChoice(choice.Id, new[] { pick }).Single() == sourceAlias + 1
                            && (int)match.DiagnosticEntity(pick)["clientEntityId"] == sourceAlias + 1, "recovery selection/native diagnostics resolve private-return offer alias");
                        aliases.Remove(pick); originals.Remove(sourceAlias + 1);
                    }
                    foreach (var invalid in new[] { Array.Empty<int>(), new[] { pick, pick }, new[] { int.MaxValue } })
                    {
                        try { match.ValidateRecoveryChoice(choice.Id, invalid); throw new Exception("Accepted invalid recovery"); }
                        catch (InvalidOperationException) { }
                    }
                    try { match.ValidateRecoveryChoice(choice.Id + 1, new[] { pick }); throw new Exception("Accepted stale recovery"); }
                    catch (InvalidOperationException) { }
                    Check(fingerprint == MatchRecorder.Fingerprint(match.Game) && rng == MatchRecorder.RandomState(match.Game), "diagnostics/validation leave engine and RNG unchanged");
                    var client = new ClientChoiceQueue(requireEntities: recover);
                    client.LoadScene();
                    int openingPacket = capture.Packets.FindLastIndex(p => p.Id == 17);
                    int cursor = openingPacket;
                    cursor = client.Deliver(capture, cursor);
                    int before = capture.Packets.Count;
                    if (recover)
                    {
                        Check(client.Queued(choice.Id) && client.Mapped(1) == -1, "missing native source/offers block queued choice");
                        int submitted = 0; Exception failure = null;
                        double now = 0; match.NpcClock = () => now;
                        match.RecoverUnregisteredChoice(choice.Id, new[] { pick }, id => false,
                            id => client.Mapped(1) == id,
                            picks => { submitted++; match.SendChoices(choice.Id, picks); }, error => failure = error);
                        cursor = client.Deliver(capture, cursor);
                        Check(!client.Queued(choice.Id) && client.Mapped(1) == -1 && client.DeferredCount == 1,
                            "restoration drains native queue but defers choice registration one frame");
                        // Negative control: the old back-to-back send lets acknowledgement beat deferred registration.
                        var unordered = new ClientChoiceQueue(requireEntities: true); unordered.LoadScene();
                        unordered.Deliver(capture, openingPacket);
                        var earlyAck = new Capture();
                        earlyAck.Send(13, new EntitiesChosen { PlayerId = 1, ChoiceType = choice.ChoiceType,
                            ChooseEntities = new ChooseEntities { Id = choice.Id, Entities = new List<int> { choice.Entities[0] } } });
                        unordered.Deliver(earlyAck, 0); unordered.EndFrame();
                        Check(unordered.Mapped(1) == choice.Id && unordered.Log.IndexOf("chosen:1:" + choice.Id)
                            < unordered.Log.IndexOf("choices:1:" + choice.Id), "old recovery ordering reproduces stuck native choice: " + card);
                        match.TickNpc();
                        Check(submitted == 0 && failure == null && !capture.Packets.Skip(before).Any(p => p.Id == 13)
                            && fingerprint == MatchRecorder.Fingerprint(match.Game) && rng == MatchRecorder.RandomState(match.Game),
                            "no acknowledgement or engine mutation before deferred native registration");
                        client.EndFrame();
                        Check(client.Mapped(1) == choice.Id, "deferred native choice registers matching ID");
                        match.TickNpc();
                        Check(submitted == 1 && failure == null, "registered recovery submits once");
                        cursor = client.Deliver(capture, cursor); client.EndFrame();
                        now += NativeMatch.ChoiceRecoveryWaitMilliseconds + 1; match.TickNpc();
                        Check(submitted == 1 && failure == null && client.Mapped(1) == -1 && client.QueuedCount == 0 && client.DeferredCount == 0,
                            "native acknowledgement clears registration and queue without manual cleanup or duplicate submission");
                        Check(client.Log.IndexOf("choices:1:" + choice.Id) < client.Log.IndexOf("chosen:1:" + choice.Id),
                            "native choice registration precedes acknowledgement");
                        int acknowledgement = capture.Packets.FindIndex(before, p => p.Id == 13);
                        var restored = capture.Packets.Skip(before).Take(acknowledgement - before).Where(p => p.Id == 19)
                            .SelectMany(p => ((PowerHistory)p.Message).List).Where(e => e.HasFullEntity).Select(e => e.FullEntity).ToArray();
                        Check(choice.Entities.All(id => restored.Any(e => e.Entity == id && !string.IsNullOrEmpty(e.Name))), "missing native offers rehydrated before choice acknowledgement");
                        Check(restored.Any(e => e.Entity == sourceAlias && !string.IsNullOrEmpty(e.Name)), "missing native source rehydrated with native return alias before choice acknowledgement");
                        try { match.ValidateRecoveryChoice(choice.Id, new[] { pick }); throw new Exception("Accepted completed recovery"); }
                        catch (InvalidOperationException) { }
                    }
                    else
                    {
                        match.SendChoices(choice.Id, new List<int> { choice.Entities[0] });
                        cursor = client.Deliver(capture, cursor); client.EndFrame();
                        Check(client.Mapped(1) == -1 && client.QueuedCount == 0, "normal native choice drains");
                    }
                    Check(match.Game.Player1.Choice == null && match.HumanOptionsId >= 0, "choice recovery resumes authoritative input");
                    record.FlushAndWait(5000);
                    var rows = writer.ToString().Split('\n').Where(line => line.Length > 0).Select(line => JsonDocument.Parse(line)).ToArray();
                    try
                    {
                        var opened = rows.Select(row => row.RootElement).Single(row => row.GetProperty("type").GetString() == "choice-open");
                        Check(opened.GetProperty("choiceId").GetInt32() == choice.Id && opened.GetProperty("choiceType").GetString() == (card == "EX1_319" ? "TARGET" : "GENERAL")
                            && opened.GetProperty("source").GetProperty("cardId").GetString() == card, "choice-open source/type/id recorded");
                        Check(opened.GetProperty("offers").EnumerateArray().All(offer => !string.IsNullOrEmpty(offer.GetProperty("cardId").GetString())
                            && !offer.GetProperty("client").GetProperty("present").GetBoolean()), "journal offered card identities and missing-client check");
                        Check(!opened.GetProperty("source").TryGetProperty("tags", out _)
                            && opened.GetProperty("offers").EnumerateArray().All(offer => !offer.TryGetProperty("tags", out _)),
                            "choice opening omits explicit tags; entity inspection retains them");
                        Check(logs.Count == 1 && logs[0].Contains(match.Game.IdEntityDic[pick].Card.Id), "choice-open log contains actual offered card ID");
                        Check(logs[0] == opened.GetRawText(), "journal and logger share exact serialized choice row");
                    }
                    finally { foreach (var row in rows) row.Dispose(); }
                    capture.SerializeAll();
                    return MatchRecorder.Fingerprint(match.Game) + "/" + MatchRecorder.RandomState(match.Game);
                }
            }
            try { Check(Run(false) == Run(true), "diagnostic recovery has same engine/RNG outcome as normal choice: " + card); }
            finally { definition.Power = originalPower; }
        }
        ChoiceRecoveryFailures();
        Console.WriteLine("PASS choice diagnostics/journal and recovery: GENERAL + TARGET native queue, deck proxies, validation, no engine/RNG drift");
    }

    private static void ChoiceRecoveryFailures()
    {
        foreach (bool stale in new[] { false, true })
        {
            var (capture, match) = ChoiceRetirementMatch(CardClass.ROGUE);
            var source = Generic.DrawCard(match.Game.Player1, Cards.FromId("TLC_245"));
            FlushFieldHistory(match);
            var choice = ChoiceRetirementPlay(match, capture, source, 1);
            int pick = match.Game.Player1.Choice.Choices[0], submitted = 0, failed = 0;
            string error = null;
            double now = 0; match.NpcClock = () => now;
            match.RecoverUnregisteredChoice(choice.Id, new[] { pick }, id => true, id => false,
                picks => submitted++, failure => { failed++; error = failure.Message; });
            try
            {
                match.RecoverUnregisteredChoice(choice.Id, new[] { pick }, id => true, id => true,
                    picks => submitted++, failure => failed++);
                throw new Exception("Accepted duplicate pending recovery");
            }
            catch (InvalidOperationException) { }
            if (stale) match.SendChoices(choice.Id, new List<int> { choice.Entities[0] });
            string fingerprint = MatchRecorder.Fingerprint(match.Game), rng = MatchRecorder.RandomState(match.Game);
            int before = capture.Packets.Count;
            if (!stale) now = NativeMatch.ChoiceRecoveryWaitMilliseconds;
            match.TickNpc(); match.TickNpc();
            Check(submitted == 0 && failed == 1 && error.Contains(stale ? "stale or missing" : "timed out waiting for native registration"),
                "recovery fails once with clear stale/timeout reason");
            Check(fingerprint == MatchRecorder.Fingerprint(match.Game) && rng == MatchRecorder.RandomState(match.Game)
                && !capture.Packets.Skip(before).Any(p => p.Id == 13), "failed recovery never mutates engine or sends acknowledgement");
            if (!stale) Check(match.Game.Player1.Choice != null, "registration timeout preserves authoritative choice");
        }
    }
}
