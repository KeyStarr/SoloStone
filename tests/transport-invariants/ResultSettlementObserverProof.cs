using System.Reflection;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

namespace TransportInvariants;

internal static class ResultSettlementObserverProof
{
    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "settlement-observer-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "profile.xml");
        var definition = Cards.FromId("CS2_029");
        var prior = definition.Power.PowerTask;
        try
        {
            using (var profile = new LocalProfile(path))
            {
                var game = OfflinePoolTestRecipes.NewGame(true);
                NativeSession session = null;
                var match = new NativeMatch((id, packet) => session.Receive(id, packet));
                typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
                session = new NativeSession(match);
                var context = profile.AchievementContextFor(LocalDecks.Get("mage"), false, "npc-proof", 2500, "Diamond");
                var record = profile.BeginLaunch("mage", "priest", 74, "npc-proof", "priest", 2500, context,
                    queued => match.MatchId = queued.Id);
                profile.MarkStarted(record.Id);
                var owner = game.CurrentPlayer;
                definition.Power.PowerTask = new CustomTask((g, c, s, t, stack) => throw new InvalidOperationException("injected-action"));
                var card = EngineEntity.FromCard(owner, definition, zone: owner.HandZone);
                card.Cost = 0;
                var action = owner.Options().First(x => x.Source?.Id == card.Id && x.Target?.Id == owner.Opponent.Hero.Id);
                try { game.Process(action); throw new Exception("fault missing"); }
                catch (InvalidOperationException error) when (error.Message == "injected-action") { }
                match.Concede();
                LadderBinding.Bind(profile);
                LocalLadder.NativeStarted();
                typeof(LocalLadder).GetField("active", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, record);
                LocalLadder.MarkConcede();
                LocalLadder.NativeFinished();
                byte[] before = File.ReadAllBytes(path);
                LocalProfile.CommitFaultHook = phase => { if (phase == "replace") throw new IOException("injected-save"); };
                string diagnostic = null;
                try { LocalLadder.Observe(match, message => diagnostic = message); }
                finally { LocalProfile.CommitFaultHook = null; }
                SelfChecks.Check(diagnostic?.Contains("IOException") == true && LocalLadder.RewardSaveError != null && profile.Pending?.Id == record.Id
                    && File.ReadAllBytes(path).SequenceEqual(before), "observer preserves pending bytes on failed commit");
                LocalLadder.RetryRewardSave();
                LocalLadder.Observe(match);
                var receipt = profile.History.Single();
                var saved = File.ReadAllBytes(path);
                SelfChecks.Check(LocalLadder.RewardSaveError == null && receipt.Result == "Lost" && receipt.Conceded
                    && receipt.AchievementGrants.Count == 0 && profile.Pending == null, "observer retries exact concession once");
                LocalLadder.Observe(match);
                SelfChecks.Check(File.ReadAllBytes(path).SequenceEqual(saved), "observer repeat does not duplicate result");
            }
            using (var reload = new LocalProfile(path))
                SelfChecks.Check(reload.History.Count == 1 && reload.Pending == null && reload.History[0].Conceded,
                    "observer receipt survives reload");
            Console.WriteLine("PASS settlement observer failure and retry");
        }
        finally
        {
            definition.Power.PowerTask = prior;
            LocalProfile.CommitFaultHook = null;
            Directory.Delete(root, true);
        }
    }
}
