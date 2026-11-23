using System;
using System.IO;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    // If a player action throws mid-resolution the task queue is left stranded. A human concession afterwards must
    // still record a real terminal outcome (a loss, no replay of the interrupted queue) so the match settles exactly
    // once. The test injects a failing Fireball (CS2_029) task to reproduce the stranded queue. The same file covers
    // atomic save failure/retry and achievement evidence invalidated by a failed Rewind validation.
    private static void ResultSettlementTests()
    {
        LossRewardParticipationNative();
        var (match, capture) = SogStart(new[] { "JAIL_509" }, new[] { "JAIL_509" });
        SogComplete(match, capture);
        var game = match.Game;
        if (game.CurrentPlayer != game.Player2)
            Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "NPC turn begins before interrupted action");
        Check(game.Player1.Hero[(GameTag)4717] == 1 && game.Player2.Hero[(GameTag)4717] == 1,
            "both Godfrey portals active before interrupted action");
        var schema = typeof(SabberStoneCore.Model.Game).Assembly.GetType("SabberStoneCore.Model.Snapshots.SnapshotSchema");
        var fields = (FieldInfo[])schema.GetMethod("Fields", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { typeof(SabberStoneCore.Model.Game) });
        Check(fields.All(field => field.Name != "_interruptedAction"), "transient marker must not alter snapshot schema");
        var player = game.CurrentPlayer;
        var definition = Cards.FromId("CS2_029");
        var prior = definition.Power.PowerTask;
        string root = Path.Combine(Path.GetTempPath(), "settlement-regression-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "profile.xml");
        try
        {
            using (var profile = new LocalProfile(path))
            {
                var context = profile.AchievementContextFor(LocalDecks.Get("mage"), false, "npc-proof", 2500, "Diamond");
                var record = profile.BeginLaunch("mage", "priest", 74, "npc-proof", "priest", 2500, context,
                    queued => match.MatchId = queued.Id);
                profile.MarkStarted(record.Id);
                definition.Power.PowerTask = new CustomTask((g, c, s, t, stack) => throw new InvalidOperationException("injected-action"));
                var card = EngineEntity.FromCard(player, definition, zone: player.HandZone);
                card.Cost = 0;
                var action = player.Options().First(x => x.Source?.Id == card.Id && x.Target?.Id == player.Opponent.Hero.Id);
                var faultFlag = typeof(SabberStoneCore.Model.Game).GetField("_interruptedAction", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(faultFlag.IsNotSerialized, "interrupted marker is excluded from snapshot schema");
                var snapshot = game.SaveSnapshot();
                Check(SabberStoneCore.Model.Game.LoadSnapshot(snapshot).State == game.State, "ordinary snapshot remains loadable");
                var clone = game.CloneExact(history: true);
                var cloneAction = clone.CurrentPlayer.Options().First(x => x.Source?.Id == card.Id && x.Target?.Id == clone.CurrentPlayer.Opponent.Hero.Id);
                try { clone.Process(cloneAction); throw new Exception("clone fault missing"); }
                catch (InvalidOperationException error) when (error.Message == "injected-action") { }
                Check((bool)faultFlag.GetValue(clone) && !(bool)faultFlag.GetValue(game), "speculative fault cannot taint authority");
                try { game.Process(action); throw new Exception("fault missing"); }
                catch (InvalidOperationException error) when (error.Message == "injected-action") { }
                var queue = game.TaskQueue.GetType().GetProperty("IsQuiescent", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(!(bool)queue.GetValue(game.TaskQueue), "injected fault must strand queue");
                capture.Packets.Clear();
                match.Concede();
                var terminal = game.HistoryFacts.Query.Events<MatchTerminalFact>().ToArray();
                Console.WriteLine($"TERMINAL|state={game.State}|p1={game.Player1.PlayState}|queue={queue.GetValue(game.TaskQueue)}|facts={terminal.Length}");
                var evidence = match.AchievementEvidence(profile.Pending.AchievementContext);
                Check(game.State == State.COMPLETE && game.Player1.PlayState == PlayState.LOST && terminal.Length == 1
                    && !(bool)queue.GetValue(game.TaskQueue), "concede records actual loss without replaying interrupted queue");
                foreach (var controller in new[] { game.Player1, game.Player2 })
                    Check(controller.Hero[(GameTag)4717] == 0 && DrawRows(capture).Any(row => row.HasTagChange
                        && row.TagChange.Entity == controller.Hero.Id && row.TagChange.Tag == 4717 && row.TagChange.Value == 0),
                        "interrupted concession clears both outgoing Godfrey portals");
                Check(evidence.Available && evidence.Result == "Lost" && evidence.PlayerConceded
                    && ReferenceEquals(evidence, match.AchievementEvidence(profile.Pending.AchievementContext)),
                    "collector freezes truthful concession and terminal evidence");
                var before = File.ReadAllBytes(path);
                LocalProfile.CommitFaultHook = phase => { if (phase == "replace") throw new IOException("injected-save"); };
                bool failed = false;
                try { profile.Settle(record.Id, "Lost", game.Turn, true, match.RewardFacts, 2500, null, evidence); }
                catch (IOException) { failed = true; }
                finally { LocalProfile.CommitFaultHook = null; }
                Check(failed && profile.Pending?.Id == record.Id && File.ReadAllBytes(path).SequenceEqual(before),
                    "save failure retains original pending bytes");
                var settled = profile.Settle(record.Id, "Lost", game.Turn, true, match.RewardFacts, 2500, null, evidence);
                var saved = File.ReadAllBytes(path);
                Check(settled.Result == "Lost" && settled.Conceded && settled.Gold == 0 && settled.DustReward == 0
                    && !settled.PackReward && settled.AchievementGrants.Count == 0 && profile.Pending == null && profile.History.Count == 1,
                    "concession settles exactly once with no grants");
                profile.Settle(record.Id, "Lost", game.Turn, true, match.RewardFacts, 2500, null, evidence);
                Check(File.ReadAllBytes(path).SequenceEqual(saved), "idempotent retry cannot duplicate receipt");
            }
            using (var reload = new LocalProfile(path))
                Check(reload.Pending == null && reload.History.Count == 1 && reload.History[0].Result == "Lost"
                    && reload.History[0].Conceded, "receipt survives reload");
            var (recovered, _) = AnimationMatch();
            var flag = typeof(SabberStoneCore.Model.Game).GetField("_interruptedAction", BindingFlags.Instance | BindingFlags.NonPublic);
            flag.SetValue(recovered.Game, true);
            Check(recovered.Game.Process(EndTurnTask.Any(recovered.Game.CurrentPlayer)) && !(bool)flag.GetValue(recovered.Game),
                "successful quiescent action clears the interrupted marker");
            foreach (bool opponent in new[] { false, true })
            {
                var (normal, _) = AnimationMatch();
                var controller = opponent ? normal.Game.Player2 : normal.Game.Player1;
                Check(normal.Game.Process(ConcedeTask.Any(controller)), "normal concession");
                Check(normal.Game.HistoryFacts.Query.Events<MatchTerminalFact>().Count() == 1
                    && normal.Game.Player1.PlayState == (opponent ? PlayState.WON : PlayState.LOST), "normal outcome and terminal fact unchanged");
            }
            // Live-invalidated achievement evidence (e.g. a Rewind validation failure) settles the win with no achievement progress.
            var evidenceFailure = typeof(HistoryFacts).GetField("_evidenceFailure", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (bool invalid in new[] { false, true })
            {
                string evidencePath = Path.Combine(root, (invalid ? "invalid" : "valid") + "-evidence.xml");
                using (var profile = new LocalProfile(evidencePath))
                {
                    var (won, _) = AnimationMatch();
                    var context = profile.AchievementContextFor(LocalDecks.Get("mage"), false, "npc-proof", 2500, "Diamond");
                    var record = profile.BeginLaunch("mage", "priest", 74, "npc-proof", "priest", 2500, context, queued => won.MatchId = queued.Id);
                    profile.MarkStarted(record.Id);
                    if (invalid) evidenceFailure.SetValue(won.Game.HistoryFacts, "proof: unexpected fact shape");
                    Check(won.Game.Process(ConcedeTask.Any(won.Game.Player2)) && won.Game.Player1.PlayState == PlayState.WON, "opponent concession wins");
                    var evidence = won.AchievementEvidence(profile.Pending.AchievementContext);
                    Check(evidence.Available && evidence.EvidenceInvalid == invalid && evidence.Result == "Won"
                        && (!invalid || evidence.InvalidReason == "proof: unexpected fact shape"), "collector reports live evidence verdict");
                    bool? npcConceded = won.OpponentConceded ? true : (bool?)null;
                    var settled = profile.Settle(record.Id, "Won", won.Game.Turn, false, won.RewardFacts, 2500, npcConceded, evidence);
                    Check(settled.Result == "Won" && settled.AchievementEvidenceFingerprint == evidence.Fingerprint
                        && profile.Achievements.Career.EligibleWins == (invalid ? 0 : 1)
                        && (!invalid || settled.AchievementGrants.Count == 0 && profile.Achievements.Unlocks.Count == 0),
                        (invalid ? "invalid" : "valid") + " evidence settles " + (invalid ? "with no achievement progress" : "with career progress"));
                    var saved = File.ReadAllBytes(evidencePath);
                    profile.Settle(record.Id, "Won", won.Game.Turn, false, won.RewardFacts, 2500, npcConceded, evidence);
                    Check(File.ReadAllBytes(evidencePath).SequenceEqual(saved), "evidence settlement retry is idempotent");
                }
                using (var reload = new LocalProfile(evidencePath))
                    Check(reload.Pending == null && reload.History.Count == 1 && reload.History[0].Result == "Won", "evidence receipt survives reload");
            }
            Console.WriteLine("PASS interrupted concession, truthful evidence, atomic save retry, reload and normal outcomes, invalid evidence settles without achievements");
        }
        finally
        {
            definition.Power.PowerTask = prior;
            LocalProfile.CommitFaultHook = null;
            Directory.Delete(root, true);
        }
    }
    private static void LossRewardParticipationNative()
    {
        foreach (bool concede in new[] { false, true })
        {
            long clock = 1000;
            var (match, capture) = SogStart(Array.Empty<string>(), Array.Empty<string>());
            match.RewardClock = () => clock;
            // A long mulligan must not accrue time, and several moves on one turn count once.
            clock += 600000;
            SogComplete(match, capture);
            var game = match.Game;
            for (int turn = 1; turn <= 6; turn++)
            {
                var human = game.Player1;
                Check(game.CurrentPlayer == human, "test is on the human turn");
                if (turn == 3)
                    InvokeProcess(match, human.Options().OfType<HeroPowerTask>().First());
                else if (turn == 4)
                    InvokeProcess(match, human.Options().OfType<MinionAttackTask>()
                        .First(task => task.ActionTarget?.Id == game.Player2.Hero.Id));
                else if (turn == 5)
                {
                    var card = Generic.DrawCard(human, Cards.FromId("TIME_038"));
                    card.Cost = 0;
                    FlushHistory(match);
                    InvokeProcess(match, human.Options().OfType<PlayCardTask>().First(task => task.Source?.Id == card.Id));
                    Check(game.Rewind != null, "the only player move this turn offers a Rewind");
                    InvokeProcess(match, ChooseTask.Pick(human, game.Rewind.RewindEntityId));
                    if (game.Rewind != null)
                        InvokeProcess(match, ChooseTask.Pick(human, game.Rewind.KeepEntityId));
                    Check(!game.HistoryFacts.Query.PlayerActions().Any(action => action.Turn == game.Turn && action.ControllerId == human.Id),
                        "the Rewind discards the action fact so the committed attempt must count separately");
                }
                else if (turn != 2)
                    InvokeProcess(match, human.Options().OfType<PlayCardTask>().First());
                if (turn == 1)
                    InvokeProcess(match, human.Options().OfType<PlayCardTask>().First());
                InvokeProcess(match, EndTurnTask.Any(human));
                if (turn == 6) break;
                if (turn == 1)
                    InvokeProcess(match, game.Player2.Options().OfType<PlayCardTask>().First());
                InvokeProcess(match, EndTurnTask.Any(game.Player2));
            }
            // The last qualifying turn already ended, so either ending gives the same participation evidence.
            clock += 539999;
            if (concede) match.Concede();
            else
            {
                game.Player1.Hero.Armor = 0;
                game.Player1.Hero.Damage = game.Player1.Hero.BaseHealth - 1;
                var lethal = game.Player2.Options().OfType<MinionAttackTask>()
                    .First(task => task.ActionTarget?.Id == game.Player1.Hero.Id);
                InvokeProcess(match, lethal);
            }
            Check(game.State == State.COMPLETE && game.Player1.PlayState == PlayState.LOST
                && match.HumanConceded == concede, "natural defeat and concede use the real native terminal path");
            clock += 10000;
            var facts = match.RewardFacts;
            Check(facts.ActiveTurns == 5 && facts.PlayMilliseconds == 539999,
                "only distinct human move turns and post-mulligan time up to the terminal action count");
        }
        Console.WriteLine("PASS native loss rewards count accepted human moves on distinct turns and freeze the clock for both endings");
    }

}
