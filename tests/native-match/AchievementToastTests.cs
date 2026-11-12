using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.Model;

internal static partial class Program
{
    private static void AchievementToastQueue()
    {
        AchievementPrimeRefreshesQuestRewardOptions();
        string root = Path.Combine(Path.GetTempPath(), "achievement-toast-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "profile.xml");
        try
        {
            using (var profile = new LocalProfile(path))
            {
                var context = profile.AchievementContextFor(LocalDecks.Get("test-deathknight-rainbow"), false,
                    "npc-toast", 2500, "Diamond");
                var capture = new Capture(); NativeMatch match = null;
                var record = profile.BeginLaunch("test-deathknight-rainbow", "mage", 73, "npc-toast", "mage", 2500, context, pending => {
                    match = new NativeMatch(capture.Send, "test-deathknight-rainbow", "mage", 73) { MatchId = pending.Id };
                    match.Start();
                });
                profile.MarkStarted(record.Id);
                bool rejected = false;
                try { match.PrimeAchievementForTest("death-knight-grave-expenditure"); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "achievement prime rejects mulligan");
                match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new List<int>());
                match.PrimeAchievementForTest("death-knight-grave-expenditure");
                match.PrimeAchievementForTest("death-knight-grave-expenditure");
                Check(match.Game.HistoryFacts.Records.OfType<CorpseSpentFact>().Where(row => row.ControllerId == match.Game.Player1.Id)
                    .Sum(row => row.Amount) == 30 && profile.Achievements.Unlocks.Count == 0 && profile.PendingAchievementToast == null,
                    "prime is idempotent progress, never a completion or grant");
                match.ConcedeNpcForTest();
                var evidence = match.AchievementEvidence(profile.Pending.AchievementContext);
                Check(!evidence.EvidenceInvalid, "primed corpse fact remains authoritative evidence");
                byte[] before = File.ReadAllBytes(path);
                LocalProfile.CommitFaultHook = phase => { if (phase == "replace") throw new IOException("toast settlement fault"); };
                try { profile.Settle(record.Id, "Won", match.Game.Turn, false, match.RewardFacts, 2500, true, evidence); throw new Exception("Expected save fault"); }
                catch (IOException) { }
                finally { LocalProfile.CommitFaultHook = null; }
                Check(File.ReadAllBytes(path).SequenceEqual(before) && profile.PendingAchievementToast == null
                    && profile.Achievements.Grants.Count == 0, "failed settlement leaves queue and grants unchanged");
                var settled = profile.Settle(record.Id, "Won", match.Game.Turn, false, match.RewardFacts, 2500, true, evidence);
                Check(settled.AchievementGrants.Any(row => row.AchievementId == "death-knight-grave-expenditure" && row.HeroSkinId == "HERO_11n")
                    && profile.OwnedHeroSkins.Contains("HERO_11n") && profile.PendingAchievementToast?.Id == record.Id,
                    "normal DK completion atomically saves cosmetic grant and pending toast");
                profile.Settle(record.Id, "Won", match.Game.Turn, false, match.RewardFacts, 2500, true, evidence);
                Check(profile.FeatsSnapshot().PendingAchievementToastMatchIds.Count == 1, "settlement retry never duplicates toast");
                profile.PendingAchievementToast.AchievementGrants.Clear();
                Check(profile.PendingAchievementToast.AchievementGrants.Count > 0, "queued receipt is a defensive copy");
                foreach (var invalid in new[] { new List<string> { "unknown-match" }, new List<string> { record.Id, record.Id } })
                {
                    byte[] validBytes = File.ReadAllBytes(path); bool invalidRejected = false;
                    try { profile.CommitChange(next => { next.PendingAchievementToastMatchIds = invalid; return true; }); }
                    catch (InvalidOperationException) { invalidRejected = true; }
                    Check(invalidRejected && File.ReadAllBytes(path).SequenceEqual(validBytes), "invalid queue is rejected without rewriting save");
                }
                string legacyPath = Path.Combine(root, "legacy.xml");
                var legacy = profile.FeatsSnapshot(); legacy.PendingAchievementToastMatchIds = null;
                using (var stream = File.Create(legacyPath)) new System.Xml.Serialization.XmlSerializer(typeof(LocalProfileData)).Serialize(stream, legacy);
                using (var adopted = new LocalProfile(legacyPath))
                {
                    Check(adopted.HasPendingAchievementToast(record.Id), "old pending receipt adopts a missing optional queue");
                    adopted.AcknowledgeRewardPresentation(record.Id);
                }
                using (var adopted = new LocalProfile(legacyPath)) Check(adopted.HasPendingAchievementToast(record.Id), "old pending toast survives Done and restart");
                string shownPath = Path.Combine(root, "shown-before-done.xml");
                legacy.PendingAchievementToastMatchIds = new List<string>();
                using (var stream = File.Create(shownPath)) new System.Xml.Serialization.XmlSerializer(typeof(LocalProfileData)).Serialize(stream, legacy);
                using (var shown = new LocalProfile(shownPath)) Check(!shown.HasPendingAchievementToast(record.Id) && shown.PendingRewardPresentation != null,
                    "acknowledged toast is not replayed while reward boxes remain pending");
                profile.AcknowledgeRewardPresentation(record.Id);
                Check(profile.PendingAchievementToast?.Id == record.Id, "reward Done cannot consume a failed toast");
            }
            using (var profile = new LocalProfile(path))
            {
                string id = profile.PendingAchievementToast.Id;
                Check(profile.PendingRewardPresentation == null && profile.HasPendingAchievementToast(id), "toast survives reward acknowledgement and restart");
                byte[] before = File.ReadAllBytes(path);
                LocalProfile.CommitFaultHook = phase => { if (phase == "replace") throw new IOException("toast ack fault"); };
                try { profile.AcknowledgeAchievementToast(id); throw new Exception("Expected acknowledgement fault"); }
                catch (IOException) { }
                finally { LocalProfile.CommitFaultHook = null; }
                Check(File.ReadAllBytes(path).SequenceEqual(before) && profile.HasPendingAchievementToast(id), "failed toast ack preserves pending queue");
                Check(profile.AcknowledgeAchievementToast(id) && !profile.AcknowledgeAchievementToast(id), "toast acknowledgement is idempotent");
                Check(profile.Achievements.Grants.Count(row => row.AchievementId == "death-knight-grave-expenditure") == 1,
                    "presentation never regrants cosmetic");
            }
            using (var profile = new LocalProfile(path)) Check(profile.PendingAchievementToast == null, "completed toast never replays after restart");

            var mage = new NativeMatch((_, __) => { }, "mage", "priest", 73); mage.Start();
            bool wrongClass = false;
            try { mage.PrimeAchievementForTest("death-knight-grave-expenditure"); } catch (InvalidOperationException) { wrongClass = true; }
            Check(wrongClass, "prime refuses wrong class");
            Console.WriteLine("PASS achievement toast: real DK prime/evaluation, cosmetic grant, atomic queue, retry, reward ack, restart, durable toast ack");
        }
        finally { LocalProfile.CommitFaultHook = null; Directory.Delete(root, true); }
    }

    private static void AchievementPrimeRefreshesQuestRewardOptions()
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, "test-deathknight-rainbow", "mage", 73);
        match.Start();
        match.SendChoices(capture.Last<PegasusGame.EntityChoices>(17).Id, new List<int>());
        var owner = match.Game.Player1;
        owner.BaseMana = 10; owner.UsedMana = 0;
        var quest = SabberStoneCore.Actions.Generic.DrawCard(owner, Cards.FromId("TLC_433"));
        FlushFieldHistory(match);
        typeof(NativeMatch).GetMethod("SendHumanOptions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(match, null);
        SendFixtureCard(match, capture, "TLC_433", 0);
        Check(owner.SecretZone.ModernQuests.Contains(quest) && !owner.HandZone.IsFull && owner.RemainingMana >= 5,
            "corpse prime fixture has active Terror quest, reward space and mana");
        var before = capture.Last<PegasusGame.AllOptions>(14);
        int cursor = capture.Packets.Count;
        match.PrimeAchievementForTest("death-knight-grave-expenditure");
        var reward = owner.HandZone.Single(card => card.Card.Id == "TLC_433t");
        var options = capture.Last<PegasusGame.AllOptions>(14);
        int index = options.Options.FindIndex(option => option.MainOption?.Id == reward.Id);
        Check(options.Id != before.Id && index >= 0 && History(capture, cursor).Any(entry => SnapshotEntity(entry) == reward.Id),
            "prime immediately publishes quest reward history and refreshed native option");
        Check(match.ValidateOption(options.Id, index, 0, -1, 1).Source.Id == reward.Id,
            "quest reward native option is immediately actionable");
    }
}
