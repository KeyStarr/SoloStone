using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using SabberStoneCore.Enums;

// Live regression (lane 2, 2026-10-25): a native-builder "Custom Mage" (2 Golden CORE_SW_108 + 28 Normal, incl. the
// pack card CATA_485) saved and counted as complete through the wide ledger universe, but match start rebuilt it with
// the narrower LocalDecks.Custom list ("Unknown local card.") after Begin had committed Pending: a trapped save.
internal static partial class Program
{
    private static readonly string[] LiveCustomMage = ("CATA_485,CORE_BAR_541,CORE_BAR_541,CORE_CS2_023,CORE_CS2_023,CORE_DRG_107,"
        + "CORE_DRG_107,CORE_EDR_001,CORE_EDR_001,CORE_EX1_287,CORE_EX1_287,CORE_LOOT_101,CORE_LOOT_101,CORE_SW_108,CORE_SW_108,"
        + "CS2_024,CS2_024,CS2_025,CS2_025,CS2_026,CS2_027,CS2_027,CS2_031,CS2_031,EX1_277,EX1_277,EX1_608,EX1_608,NEW1_012,NEW1_012").Split(',');

    private static void StartUniverseAndTrap()
    {
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        LocalProfileData Read(string file) { using var stream = File.OpenRead(file); return (LocalProfileData)serializer.Deserialize(stream); }
        void Write(string file, LocalProfileData data) { using var stream = File.Create(file); serializer.Serialize(stream, data); }

        // Root cause, pinned: the narrow list refuses this deck while the deck-saving universe accepts it.
        Check(!LocalDecks.IsCustomCard("CATA_485") && LocalProfile.KnownLedgerCard("CATA_485"), "CATA_485 no longer splits the universes");
        try { LocalDecks.Custom("Custom Mage", "HERO_08", LiveCustomMage); throw new Exception("narrow list accepted the live deck"); }
        catch (InvalidOperationException error) { Check(error.Message == "Unknown local card.", "narrow list failed differently: " + error.Message); }
        Check(LocalProfile.PlayableDeck("Custom Mage", "HERO_08", LiveCustomMage, null).CardIds.Count == 30, "playable universe refused the live deck");

        string root = Path.Combine(Path.GetTempPath(), "hs-start-universe-" + Guid.NewGuid());
        string path = Path.Combine(root, "profile.xml");
        try
        {
            using (var fresh = new LocalProfile(path)) fresh.SelectCardBack(0);
            // Current-schema fixture: seed the two Golden First Flames explicitly; startup tests do not exercise migration.
            var setup = Read(path);
            Check(setup.Version == 15, "fresh fixture schema drifted");
            setup.PremiumCollection.Add(new LocalCardCount { Id = "CORE_SW_108", Premium = 1, Count = 2, Acquired = 2 });
            foreach (var group in LiveCustomMage.Where(id => id != "CORE_SW_108").GroupBy(id => id))
            {
                var row = setup.Collection.SingleOrDefault(card => card.Id == group.Key);
                if (row == null) setup.Collection.Add(row = new LocalCardCount { Id = group.Key });
                row.Count = Math.Max(row.Count, group.Count());
                row.Acquired = Math.Max(row.Acquired, group.Count());
            }
            Write(path, setup);

            long deckId;
            LocalDeck resolved;
            using (var profile = new LocalProfile(path))
            {
                Check(profile.OwnedCount("CORE_SW_108", 1) == 2, "fixture lacks the explicit Golden First Flame ownership");
                deckId = profile.CreateDraft("Custom Mage", "HERO_08").Id;
                var entries = LiveCustomMage.Select(id => new LocalDeckCard { Id = id, Premium = id == "CORE_SW_108" ? 1 : 0 }).ToList();
                profile.SavePremiumDraft(deckId, "Custom Mage", "HERO_08", entries, deckId);
                Check(profile.IsDeckComplete(deckId), "live deck shape is not complete");
                var saved = profile.GetDraft(deckId);
                // The native picker's snapshot, built the way NativeUIProbe projects saved decks.
                resolved = LocalProfile.LedgerDeck(saved.Name, saved.Hero, saved.Cards, saved.Sideboards, false);

                // Every refusal happens before Begin: a deck naming a genuinely unknown card leaves no Pending.
                byte[] before = File.ReadAllBytes(path);
                var unknown = new LocalDeck("Custom Mage", "HERO_08", saved.Cards.Select(id => id == "CATA_485" ? "NOT_A_CARD_999" : id).ToArray());
                try { NativeUIProjection.ValidateStart(profile, "custom:" + deckId, unknown, true); throw new Exception("unknown card passed start validation"); }
                catch (InvalidOperationException) { }
                try { LocalProfile.PlayableDeck("Custom Mage", "HERO_08", unknown.CardIds, null); throw new Exception("unknown card is playable"); }
                catch (InvalidOperationException) { }
                Check(profile.Pending == null && before.SequenceEqual(File.ReadAllBytes(path)), "refused start touched the profile");

                // The exact live shape passes every start check and the match itself starts with its Golden copies.
                var saved2 = NativeUIProjection.ValidateStart(profile, "custom:" + deckId, resolved, true);
                var premium = NativeMatch.PremiumEntries(resolved.CardIds, saved2);
                Check(premium != null && premium.Count(card => card.Premium == 1) == 2, "Golden entries lost before the match");
                var capture = new Capture();
                var match = new NativeMatch(capture.Send, resolved, "warrior", 4242, 0, premium);
                LocalMatchRecord started = profile.BeginLaunch("custom:" + deckId, "warrior", 4242, record => match.Start());
                Check(match.Game.State == State.RUNNING && profile.Pending?.Id == started.Id
                    && match.Game.Player1.DeckZone.Concat(match.Game.Player1.HandZone).Any(card => card.Card.Id == "CATA_485"),
                    "live Custom Mage did not start");
                profile.Settle(started.Id, "Lost", 1, true);
                profile.AcknowledgeRewardPresentation(started.Id);
            }

            // A launch that throws after Begin clears the unstarted Pending: no history, ladder or star change, durable.
            using (var profile = new LocalProfile(path))
            {
                var ladder = profile.Ladder;
                int history = profile.History.Count, stars = profile.Stars;
                Exception failure = null;
                try { profile.BeginLaunch("custom:" + deckId, "warrior", 7, record => throw new InvalidOperationException("Unknown local card.")); }
                catch (InvalidOperationException error) { failure = error; }
                Check(failure?.Message == "Unknown local card.", "launch error was not surfaced");
                Check(profile.Pending == null && Read(path).Pending == null && profile.History.Count == history && profile.Stars == stars
                    && profile.Ladder.StarIndex == ladder.StarIndex && profile.Ladder.Rating == ladder.Rating
                    && profile.Ladder.StreakWins == ladder.StreakWins, "forced launch failure left a Pending or a ladder change");
            }
            using (var profile = new LocalProfile(path))
            {
                profile.RecoverInterrupted();
                Check(profile.History.All(match => match.Result != "Abandoned"), "cleared start settled as Abandoned on restart");
                var next = profile.BeginLaunch("custom:" + deckId, "warrior", 8, record => { });
                Check(profile.Pending?.Id == next.Id, "the next start was still blocked");
                // Stage timeout (engine game never created): the bridge cancels, nothing is abandoned.
                int history = profile.History.Count;
                profile.CancelUnstarted(next.Id);
                Check(profile.Pending == null && profile.History.Count == history, "stage timeout left a Pending or history");
                // A crash after staging but before the engine started recovers as a cancel; once started it is Abandoned.
                var crashed = profile.BeginLaunch("custom:" + deckId, "warrior", 9, record => { });
                profile.RecoverInterrupted();
                Check(profile.Pending == null && profile.History.Count == history && crashed.Id != null, "unstarted crash was abandoned");
                var engine = new NativeMatch(new Capture().Send, resolved, "warrior", 10, 0, null);
                var live = profile.BeginLaunch("custom:" + deckId, "warrior", 10, record => engine.Start());
                profile.MarkStarted(live.Id);
                profile.RecoverInterrupted();
                Check(profile.Pending == null && profile.History.Count == history + 1 && profile.History.Last().Id == live.Id
                    && profile.History.Last().Result == "Abandoned", "a started engine match must recover as Abandoned");
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        Console.WriteLine("PASS start universe: live Custom Mage starts; refusals precede Begin; launch failure clears Pending");
    }
}
