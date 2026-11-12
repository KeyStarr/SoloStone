using System;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using SabberStoneCore.Model;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    private static void NpcRopeClock()
    {
        var game = NativeTailGame();
        Check(game.Process(EndTurnTask.Any(game.Player1)), "opponent turn started");
        var capture = new Capture();
        bool sync = false;
        var match = new NativeMatch(capture.Send) {
            NativeNpcTurnSynchronized = turn => sync && turn == game.Turn,
            NativeHumanInputReady = () => true
        };
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        int turn = game.Turn;
        match.TickTurnTimer(0);
        match.TickTurnTimer(54999);
        Check(!capture.Packets.Any(p => p.Id == 9), "no warning before 20 seconds remain");
        match.TickTurnTimer(55000);
        Check(!capture.Packets.Any(p => p.Id == 9), "no warning for unsynchronized client");
        sync = true;
        match.TickTurnTimer(55500);
        var shown = capture.Last<PegasusGame.TurnTimer>(9);
        Check(shown.Show && shown.Turn == turn && shown.Seconds == 20, "native timer uses remaining deadline and whole-second rounding");
        game.Player1.Choice = new Choice(game.Player1);
        match.TickTurnTimer(56000);
        var hidden = capture.Last<PegasusGame.TurnTimer>(9);
        Check(!hidden.Show && hidden.Seconds > 0, "choice hides rope without clearing options");
        match.TickTurnTimer(90000);
        Check(game.Turn == turn, "human choice pauses ordinary opponent deadline");
        game.Player1.Choice = null;
        match.TickTurnTimer(91000);
        shown = capture.Last<PegasusGame.TurnTimer>(9);
        Check(shown.Show && shown.Turn == turn && shown.Seconds == 19, "resume retains elapsed time");
        match.TickTurnTimer(109999);
        Check(game.Turn == turn, "paused clock cannot end early");
        match.TickTurnTimer(110000);
        Check(game.Turn > turn, "real deadline ends opponent turn");
        match.TickTurnTimer(110001);
        Check(!capture.Last<PegasusGame.TurnTimer>(9).Show, "old rope hidden at turn transition");
        capture.SerializeAll();

        game = NativeTailGame();
        capture = new Capture();
        match = new NativeMatch(capture.Send) { NativeHumanInputReady = () => true };
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        turn = game.Turn;
        match.TickTurnTimer(long.MaxValue - 1);
        Check(game.Turn == turn, "ordinary human turn remains untimed");

        game = NativeTailGame();
        Check(game.Process(EndTurnTask.Any(game.Player1)), "abort fixture opponent turn");
        capture = new Capture();
        match = new NativeMatch(capture.Send) { NativeNpcTurnSynchronized = _ => true };
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        match.TickTurnTimer(0);
        match.TickTurnTimer(55000);
        match.StopNpc();
        Check(!capture.Last<PegasusGame.TurnTimer>(9).Show && capture.Last<PegasusGame.TurnTimer>(9).Seconds > 0, "abort hides rope without zero timeout");
        game = NativeTailGame();
        Check(game.Process(EndTurnTask.Any(game.Player1)), "early-end fixture opponent turn");
        capture = new Capture();
        match = new NativeMatch(capture.Send) { NativeNpcTurnSynchronized = _ => true };
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        match.TickTurnTimer(0);
        match.TickTurnTimer(55000);
        int oldTurn = game.Turn;
        Check(game.Process(EndTurnTask.Any(game.Player2)), "opponent ends early");
        match.TickTurnTimer(56000);
        hidden = capture.Last<PegasusGame.TurnTimer>(9);
        Check(!hidden.Show && hidden.Turn == oldTurn && hidden.Seconds > 0, "early end invalidates old turn without zero timeout");
        match.TickTurnTimer(200000);
        Check(game.CurrentPlayer == game.Player1, "old deadline cannot end human turn");

        game = NativeTailGame();
        Check(game.Process(EndTurnTask.Any(game.Player1)), "busy fixture opponent turn");
        capture = new Capture();
        match = new NativeMatch(capture.Send) { NativeNpcTurnSynchronized = _ => false };
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        turn = game.Turn;
        match.TickTurnTimer(0);
        match.TickTurnTimer(75000);
        Check(game.Turn > turn && !capture.Packets.Any(p => p.Id == 9), "native busy does not extend or send stale deadline");

        game = NativeTailGame(true);
        Check(game.Process(EndTurnTask.Any(game.Player1)), "short turn opponent fixture");
        capture = new Capture();
        match = new NativeMatch(capture.Send) { NativeNpcTurnSynchronized = _ => true, NativeHumanInputReady = () => false };
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        turn = game.Turn;
        match.TickTurnTimer(0);
        Check(capture.Last<PegasusGame.TurnTimer>(9).Seconds == 15, "Nozdormu uses actual 15 seconds");
        game.Player1.Choice = new Choice(game.Player1);
        match.TickTurnTimer(16000);
        Check(game.Turn == turn && !capture.Last<PegasusGame.TurnTimer>(9).Show, "shortened choice waits for readiness without showing stale rope");
        game.Player1.Choice = null;
        match.TickTurnTimer(16001);
        Check(game.Turn > turn, "Nozdormu deadline continues during choice");

        var choiceFixture = OpposingChoiceMatch();
        capture = choiceFixture.Item1; match = choiceFixture.Item2; game = match.Game;
        var bot = game.Player2;
        var discover = Generic.DrawCard(bot, Cards.FromId("UNG_941"));
        FlushFieldHistory(match);
        typeof(NativeMatch).GetField("timedTurn", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(match, -1);
        match.TickTurnTimer(0);
        InvokeProcess(match, bot.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == discover.Id));
        FlushFieldHistory(match);
        Check(bot.Choice != null, "legal NPC Discover pending");
        var offer = capture.Packets.Where(p => p.Id == 17).Select(p => (PegasusGame.EntityChoices)p.Message).Last();
        int selected = bot.Choice.Choices[0];
        turn = game.Turn;
        int before = capture.Packets.Count;
        var choiceReplay = game.CloneExact(history: true);
        var choiceJournal = new StringWriter();
        using var choiceRecord = new PlaytestRecord(choiceJournal, new Dictionary<string, object> { { "type", "header" } });
        match.Recorder = choiceRecord;
        match.TickTurnTimer(75000);
        Check(choiceRecord.FlushAndWait(5000), "legacy choice journal flush timed out");
        ReplayTimeoutJournal(choiceReplay, choiceJournal.ToString(), false);
        Check(bot.Choice == null && game.Turn == turn + 1, "timeout resolves real NPC choice and ends once");
        var chosen = capture.Packets.Skip(before).Where(p => p.Id == 13).Select(p => (PegasusGame.EntitiesChosen)p.Message)
            .Single(p => p.ChooseEntities.Id == offer.Id);
        Check(chosen.ChooseEntities.Entities.SequenceEqual(new[] { selected }), "timeout acknowledges real NPC choice");
        Check(capture.Packets.Skip(before).Any(p => p.Id == 19), "choice continuation and end history published");
        match.TickTurnTimer(75001);
        Check(game.Turn == turn + 1, "timeout cannot end twice");
        capture.SerializeAll();

        foreach (bool runner in new[] { false, true })
        {
            var timeoutRewind = OpposingRewindMatch(runner);
            match = timeoutRewind.Match; game = match.Game;
            typeof(NativeMatch).GetField("timedTurn", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(match, -1);
            match.TickTurnTimer(0);
            turn = game.Turn;
            before = timeoutRewind.Capture.Packets.Count;
            var rewindReplay = game.CloneExact(history: true);
            var rewindJournal = new StringWriter();
            using var rewindRecord = new PlaytestRecord(rewindJournal, new Dictionary<string, object> { { "type", "header" } });
            match.Recorder = rewindRecord;
            match.TickTurnTimer(75000);
            Check(rewindRecord.FlushAndWait(5000), "Rewind journal flush timed out");
            ReplayTimeoutJournal(rewindReplay, rewindJournal.ToString(), runner);
            var rewindPackets = timeoutRewind.Capture.Packets.Skip(before).ToList();
            int offerIndex = rewindPackets.FindIndex(p => p.Id == 17);
            int answerIndex = rewindPackets.FindIndex(p => p.Id == 13);
            Check(offerIndex >= 0 && answerIndex > offerIndex, "timeout publishes and answers Rewind choice");
            Check(game.Turn == turn + 1, "timeout resolves Rewind and ends once");
        }

        var rewindFixture = OpposingRewindMatch(runner: true);
        match = rewindFixture.Match; game = match.Game;
        typeof(NativeMatch).GetField("timedTurn", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(match, -1);
        match.NativeNpcTurnSynchronized = _ => true;
        match.TickTurnTimer(0);
        turn = game.Turn;
        match.TickTurnTimer(55000);
        Check(rewindFixture.Capture.Last<PegasusGame.TurnTimer>(9).Show, "rope visible before actual reset");
        int rewindPick = game.Rewind.RewindEntityId;
        AnswerOpposingRewind(match, rewindPick, runner: true);
        Check(game.Turn == turn && game.Rewind == null, "real Rewind restored same turn");
        Check(!rewindFixture.Capture.Last<PegasusGame.TurnTimer>(9).Show, "reset hides old rope");
        match.TickTurnTimer(56000);
        Check(rewindFixture.Capture.Last<PegasusGame.TurnTimer>(9).Seconds == 19, "Rewind retains original deadline");

        Console.WriteLine("PASS opponent rope clock, choice pause, expiry, transition, abort, Nozdormu and Rewind");
    }
    private static void ReplayTimeoutJournal(SabberStoneCore.Model.Game replay, string journal, bool richDecision)
    {
        int actions = 0, answers = 0;
        foreach (var line in journal.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            using var document = JsonDocument.Parse(line);
            var row = document.RootElement;
            if (!row.TryGetProperty("sequence", out var sequence)) continue;
            Check(sequence.GetInt64() == ++actions && row.GetProperty("seat").GetInt32() == 2, "NPC timeout row missing sequence/seat");
            string action = row.GetProperty("action").GetString();
            if (action.StartsWith("CHOOSE:", StringComparison.Ordinal))
            {
                answers++;
                var picks = action.Split(':').Last().Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
                var decision = replay.GetPendingDecisions().Single(d => d.ControllerId == replay.Player2.Id);
                Check(replay.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, picks), out _), "journal timeout answer not replayable");
                Check(row.TryGetProperty("driverKind", out var driver) == richDecision
                    && (!richDecision || driver.GetString() == "timeout"), "AnswerDecision lost rich timeout metadata");
            }
            else
                Check(replay.Process(replay.Player2.Options().Single(t => BotAction.From(t).ToString() == action)), "journal timeout action not replayable");
            Check(MatchRecorder.Fingerprint(replay) == row.GetProperty("fingerprint").GetString(), "timeout journal replay fingerprint mismatch");
            Check(MatchRecorder.RandomState(replay) == row.GetProperty("rngState").GetString(), "timeout journal replay RNG mismatch");
        }
        Check(answers == 1 && actions == 2, "timeout journal must record answer and end-turn exactly once");
    }

}
