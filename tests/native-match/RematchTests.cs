using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// #28 Rematch (owner 2026-10-28) over a real save and the real data/npc world: the offer comes from the newest settled match
// (no timer, survives restart), plays as a normal ranked game, refuses a weaker NPC, caps a series at three, and refuses
// pending matches, other decks, legacy opponents and test fixtures. The matchmaker gate itself: tests/npc-world MatchmakerSuite.
internal static class RematchTests
{
    private const string Deck = "mage";
    private static void Check(bool value, string message) { if (!value) throw new Exception("Rematch: " + message); }
    private static string Data(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "npc", name));

    private static NpcWorld Generate()
    {
        NpcWorldJson.Parser = EmbeddedJson.Parse;
        return NpcWorld.Generate(new NpcWorldCatalogs(NpcDeckLibrary.Parse(Data("decks-v1.json")), SkillCatalog.Parse(Data("skills-v1.json")),
            PersonalityCatalog.Parse(Data("personalities-v1.json")), NpcNotables.Parse(Data("notables-v1.json")),
            NpcNameTables.Parse(Data("names-v1.json")), NpcCosmeticPool.Parse(Data("cosmetics-v1.json"))));
    }

    public static void Run()
    {
        var world = Generate();
        var mm = new LadderMatchmaker(world);
        string root = Path.Combine(Path.GetTempPath(), "rematch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Refusals(mm, Path.Combine(root, "refusals.xml"));
            SeriesAndRestart(mm, Path.Combine(root, "series.xml"));
            RankedAccounting(mm, root);
            WeakerNpcRefused(mm, Path.Combine(root, "weak.xml"));
            PersonalDeckEdits(mm, Path.Combine(root, "personal.xml"));
            ResultsScreen(mm, root);
        }
        finally { Directory.Delete(root, true); }
        QueuedFromResults();
        Console.WriteLine("PASS rematch: offer from the last match, restart, series cap, ranked accounting, weaker NPC, personal deck edits, "
            + "refusals, results-screen offer and the queued picker start");
    }

    private static MatchRequest Request(LocalProfile profile, LadderMatchmaker mm) =>
        LocalRematch.Request(profile, profile.History, mm.World.Catalogs.Decks, mm.Rules.HistoryDepth, mm.Legend);

    // A Bronze-allowed NPC clearly above the profile's matchmaker target: every rematch rule except the series passes.
    private static NpcIdentity Harder(LadderMatchmaker mm, LocalProfile profile, int skip = 0) => mm.World.All
        .Where(npc => npc.DeckIds.Count > 0 && mm.World.Catalogs.Decks.Find(npc.DeckIds[0])?.Admitted == true
            && mm.World.Catalogs.Decks.Find(npc.DeckIds[0]).Tags.Allows(LadderBand.Bronze)
            && npc.HomeRating > profile.Ladder.Rating + 150 && npc.HomeRating < profile.Ladder.Rating + 400)
        .OrderBy(npc => npc.Id, StringComparer.Ordinal).Skip(skip).First();

    private static LocalMatchRecord Play(LocalProfile profile, NpcIdentity npc, string deckId, double rating, string result,
        bool conceded = false, string deck = Deck)
    {
        var record = profile.Begin(deck, deckId, 7, npc?.Id, npc == null ? null : deckId, npc == null ? (double?)null : rating);
        var settled = profile.Settle(record.Id, result, 6, conceded);
        Check(profile.AcknowledgeRewardPresentation(record.Id), "reward acknowledgement");
        return settled;
    }

    private static LocalMatchRecord Play(LocalProfile profile, NpcEncounter encounter, string result, bool conceded = false) =>
        Play(profile, encounter.Npc, encounter.Deck.Id, encounter.Rating, result, conceded);

    private static void Refusals(LadderMatchmaker mm, string path)
    {
        using (var profile = new LocalProfile(path))
        {
            string refusal;
            Check(LocalRematch.Encounter(profile, mm, Deck, out refusal) == null && refusal == "last opponent is not a world NPC", "empty history: " + refusal);
            var npc = Harder(mm, profile);
            Play(profile, null, "priest", 0, "Won");
            Check(LocalRematch.Encounter(profile, mm, Deck, out refusal) == null && refusal == "last opponent is not a world NPC", "legacy opponent: " + refusal);
            Play(profile, npc, npc.DeckIds[0], mm.Legend.DriftedRating(npc, profile.MatchCount), "Lost", conceded: true);
            Check(LocalRematch.Encounter(profile, mm, Deck, out refusal) != null, "a player concede offers a rematch: " + refusal);
            Check(LocalRematch.Encounter(profile, mm, "warrior", out refusal) == null && refusal == "a different deck is selected", "other deck: " + refusal);
            Check(LocalRematch.Encounter(profile, mm, "test-mage", out refusal) == null && refusal == "not a ladder deck", "test fixture: " + refusal);
            Check(LocalRematch.Encounter(profile, null, Deck, out refusal) == null && refusal == "no NPC world", "no world: " + refusal);
            var pending = profile.Begin(Deck, "priest", 3);
            Check(LocalRematch.Encounter(profile, mm, Deck, out refusal) == null && refusal == "a match is pending", "pending: " + refusal);
            profile.Settle(pending.Id, "Tied", 4, false);
            profile.AcknowledgeRewardPresentation(pending.Id);
            Check(LocalRematch.Encounter(profile, mm, Deck, out refusal) == null, "a newer match ends the offer");
            // #37: Rematch is Ranked-only. A Casual last match (even vs a world NPC on the same deck) has no offer, and a
            // later Ranked match against an NPC offers again over Ranked history.
            Play(profile, npc, npc.DeckIds[0], mm.Legend.DriftedRating(npc, profile.RankedMatchCount), "Lost");
            Check(LocalRematch.Encounter(profile, mm, Deck, out refusal) != null, "ranked offer before Casual: " + refusal);
            var casual = profile.Begin(Deck, npc.DeckIds[0], 5, npc.Id, npc.DeckIds[0], mm.Legend.DriftedRating(npc, profile.RankedMatchCount), CasualRules.Mode);
            profile.Settle(casual.Id, "Lost", 6, false);
            profile.AcknowledgeRewardPresentation(casual.Id);
            Check(LocalRematch.Encounter(profile, mm, Deck, out refusal) == null && refusal.StartsWith(LocalRematch.CasualRefusal, StringComparison.Ordinal),
                "a Casual last match refuses: " + refusal);
        }
    }

    // Both entry points share Encounter: results asks before acknowledgement; picker asks after it.
    private static void ResultsScreen(LadderMatchmaker mm, string root)
    {
        foreach (string outcome in new[] { "Won", "Lost", "Tied", "Conceded", "Abandoned", "NpcConceded" })
        {
            string path = Path.Combine(root, "results-" + outcome + ".xml");
            using (var profile = new LocalProfile(path))
            {
                var npc = Harder(mm, profile, 2);
                var record = profile.Begin(Deck, npc.DeckIds[0], 7, npc.Id, npc.DeckIds[0], mm.Legend.DriftedRating(npc, 0));
                if (outcome == "Abandoned") profile.RecoverInterrupted();
                else profile.Settle(record.Id, outcome == "Conceded" ? "Lost" : outcome == "NpcConceded" ? "Won" : outcome,
                    6, outcome == "Conceded", null, null, npcConceded: outcome == "NpcConceded");
                Check(outcome == "Abandoned" ? profile.PendingRewardPresentation == null : profile.PendingRewardPresentation?.Id == record.Id,
                    outcome + ": recovery has no results receipt; other settlements wait for results");
                Check(profile.LastMatchId == record.Id && profile.LastMatchDeck == Deck, outcome + ": newest settled match");
                bool eligible = outcome != "Won" && outcome != "NpcConceded";
                var offer = LocalRematch.Encounter(profile, mm, Deck, out string refusal);
                Check((offer != null) == eligible, outcome + ": results offer: " + refusal);
                if (!eligible) Check(refusal == "last match was not a loss or tie", outcome + ": result refusal");
                if (outcome == "Abandoned") Check(profile.History.Last().Result == "Abandoned", "recovery records Abandoned");
                if (outcome != "Abandoned") Check(profile.AcknowledgeRewardPresentation(record.Id), "reward acknowledgement");
                Check((LocalRematch.Encounter(profile, mm, Deck, out refusal) != null) == eligible,
                    outcome + ": picker offer: " + refusal);
            }
            using (var restarted = new LocalProfile(path))
                Check((LocalRematch.Encounter(restarted, mm, Deck, out _) != null) == (outcome != "Won" && outcome != "NpcConceded"),
                    outcome + ": picker eligibility survives restart");
        }
    }

    // REMATCH on the results screen queues a start that the picker runs once ready, through the same refusals.
    private static void QueuedFromResults()
    {
        var queued = new LocalRematchRequest();
        string why;
        Check(queued.Next(0, "m1", true, true, null, out why) == LocalRematchStep.Idle, "nothing queued");
        queued.Request("m1", 10);
        Check(queued.Next(11, "m1", false, false, null, out why) == LocalRematchStep.Wait, "waits while routing to the picker");
        Check(queued.Next(12, "m1", true, true, "other deck selected", out why) == LocalRematchStep.Wait, "a loading tray is not judged at once");
        Check(queued.Next(12.2f, "m1", true, true, null, out why) == LocalRematchStep.Wait, "settles before starting");
        Check(queued.Next(12.6f, "m1", true, true, null, out why) == LocalRematchStep.Start && queued.Match == null, "starts once settled");
        Check(queued.Next(13, "m1", true, true, null, out why) == LocalRematchStep.Idle, "starts once");

        queued.Request("m1", 0);
        queued.Next(1, "m1", true, true, null, out why);
        Check(queued.Next(1.1f, "m1", true, false, null, out why) == LocalRematchStep.Wait, "a busy picker resets the settle");
        queued.Next(2, "m1", true, true, "series limit", out why);
        Check(queued.Next(2.6f, "m1", true, true, "series limit", out why) == LocalRematchStep.Drop && why == "series limit" && queued.Match == null,
            "a refusal on the settled picker drops it: " + why);

        // Leaving the picker the route reached (even while it was still busy or settling) drops it: a later visit is the player's.
        queued.Request("m1", 0);
        Check(queued.Next(1, "m1", true, false, null, out why) == LocalRematchStep.Wait, "busy on arrival");
        Check(queued.Next(2, "m1", false, false, null, out why) == LocalRematchStep.Drop && why == "picker left", "back out while busy: " + why);
        queued.Request("m1", 0);
        queued.Next(1, "m1", true, true, null, out why);
        Check(queued.Next(1.2f, "m1", false, false, null, out why) == LocalRematchStep.Drop && why == "picker left", "back out while settling: " + why);
        Check(queued.Next(3, "m1", true, true, null, out why) == LocalRematchStep.Idle, "a later picker visit starts nothing");

        queued.Request("m1", 0);
        Check(queued.Next(1, "m2", true, true, null, out why) == LocalRematchStep.Drop && why == "another match was played", "another match drops it");
        queued.Request("m1", 0);
        Check(queued.Next(LocalRematchRequest.TimeoutSeconds + 1, "m1", false, false, null, out why) == LocalRematchStep.Drop
            && why == "picker not ready in time", "a picker that never gets ready drops it");
        bool threw = false;
        try { queued.Request(null, 0); } catch (ArgumentException) { threw = true; }
        Check(threw, "a request needs a settled match");
    }

    // Losses and ties offer it; three in a row is the cap; a restart keeps the offer (derived from history).
    private static void SeriesAndRestart(LadderMatchmaker mm, string path)
    {
        string refusal;
        NpcIdentity npc;
        using (var profile = new LocalProfile(path))
        {
            npc = Harder(mm, profile, 1);
            Play(profile, npc, npc.DeckIds[0], mm.Legend.DriftedRating(npc, 0), "Tied");
        }
        using (var profile = new LocalProfile(path))
        {
            var second = LocalRematch.Encounter(profile, mm, Deck, out refusal);
            Check(second != null && second.Npc.Id == npc.Id && second.Deck.Id == npc.DeckIds[0] && second.Reason == "rematch",
                "the offer survives a restart: " + refusal);
            Check(second.Rating == mm.Legend.DriftedRating(npc, profile.MatchCount), "rematch rating is the NPC's drifted rating now");
            Play(profile, second, "Lost");
            var third = LocalRematch.Encounter(profile, mm, Deck, out refusal);
            Check(third != null, "game three of a series is offered after a loss: " + refusal);
            Play(profile, third, "Lost");
            Check(LocalRematch.Encounter(profile, mm, Deck, out refusal) == null && refusal == "series limit", "a fourth game in a row: " + refusal);
            Check(mm.Next(Request(profile, mm)).Npc.Id != npc.Id, "normal matchmaking keeps that NPC away after the series");
        }
    }

    // A rematch is a normal ranked game: the same ladder step as any NPC at the same rating, and the record carries the NPC.
    private static void RankedAccounting(LadderMatchmaker mm, string root)
    {
        string a = Path.Combine(root, "ranked-a.xml"), b = Path.Combine(root, "ranked-b.xml");
        NpcIdentity npc, other;
        using (var profile = new LocalProfile(a))
        {
            npc = Harder(mm, profile, 2); other = Harder(mm, profile, 3);
            Play(profile, npc, npc.DeckIds[0], mm.Legend.DriftedRating(npc, 0), "Lost");
        }
        File.Copy(a, b);
        LocalMatchRecord rematched, plain;
        double rating;
        using (var profile = new LocalProfile(a))
        {
            var encounter = LocalRematch.Encounter(profile, mm, Deck, out string refusal);
            Check(encounter != null, "rematch after a loss: " + refusal);
            rating = encounter.Rating;
            var record = profile.Begin(Deck, encounter.Deck.Id, 7, encounter.Npc.Id, encounter.Deck.Id, encounter.Rating);
            rematched = profile.Settle(record.Id, "Won", 6, false);
            Check(LocalRematch.Encounter(profile, mm, Deck, out refusal) == null && refusal == "last match was not a loss or tie",
                "lose/rematch/win: results offers no further rematch: " + refusal);
            profile.AcknowledgeRewardPresentation(record.Id);
            Check(LocalRematch.Encounter(profile, mm, Deck, out refusal) == null && refusal == "last match was not a loss or tie",
                "lose/rematch/win: picker offers no further rematch: " + refusal);
        }
        using (var profile = new LocalProfile(b)) plain = Play(profile, other, other.DeckIds[0], rating, "Won");
        Check(rematched.NpcId == npc.Id && rematched.NpcDeckId == npc.DeckIds[0] && rematched.Opponent == npc.DeckIds[0]
            && rematched.NpcRating == rating, "the rematch record names the NPC, deck and rating");
        var x = rematched.Ladder; var y = plain.Ladder;
        Check(x != null && y != null && x.StarsBefore == y.StarsBefore && x.StarsAfter == y.StarsAfter && x.StarsGained == y.StarsGained
            && x.RatingAfter == y.RatingAfter && x.MultiplierAfter == y.MultiplierAfter && x.StreakAfter == y.StreakAfter
            && x.OpponentRating == y.OpponentRating && x.StarsGained > 0, "a rematch win is the normal ranked step");
    }

    // A clearly weaker NPC after a result is never offered again (no farming down the ladder).
    private static void WeakerNpcRefused(LadderMatchmaker mm, string path)
    {
        using (var profile = new LocalProfile(path))
        {
            var weak = mm.World.All.Where(npc => npc.DeckIds.Count > 0 && mm.World.Catalogs.Decks.Find(npc.DeckIds[0])?.Admitted == true
                    && mm.World.Catalogs.Decks.Find(npc.DeckIds[0]).Tags.Allows(LadderBand.Bronze))
                .OrderBy(npc => npc.HomeRating).ThenBy(npc => npc.Id, StringComparer.Ordinal).First();
            Play(profile, weak, weak.DeckIds[0], mm.Legend.DriftedRating(weak, 0), "Lost");
            Check(mm.Legend.DriftedRating(weak, profile.MatchCount) < mm.RematchFloor(Request(profile, mm)), "fixture: the weakest NPC sits below the floor");
            Check(LocalRematch.Encounter(profile, mm, Deck, out string refusal) == null && refusal == "opponent now too weak", "weaker NPC: " + refusal);
        }
    }

    // A personal deck keeps its custom: id across edits: the rematch needs the recorded cards, sideboards and hero (review #1).
    private static void PersonalDeckEdits(LadderMatchmaker mm, string path)
    {
        using (var profile = new LocalProfile(path, new ProfileOptions { AllCards = true }))
        {
            string[] cards = LocalDecks.Get("mage").CardIds.ToArray();
            var draft = profile.CreateDraft("Rematch Mage", "HERO_08");
            profile.SaveDraft(draft.Id, draft.Name, draft.Hero, cards, draft.SortOrder);
            string selected = "custom:" + draft.Id;
            var npc = Harder(mm, profile, 4);
            double rating = mm.Legend.DriftedRating(npc, profile.MatchCount);

            // Without the recorded deck (no achievement context) a personal deck never rematches.
            var bare = profile.Begin(selected, npc.DeckIds[0], 5, npc.Id, npc.DeckIds[0], rating);
            profile.Settle(bare.Id, "Lost", 5, false); profile.AcknowledgeRewardPresentation(bare.Id);
            Check(LocalRematch.Encounter(profile, mm, selected, out string refusal) == null && refusal == "played deck not recorded",
                "personal deck without a recorded list: " + refusal);

            var played = LocalProfile.LedgerDeck(draft.Name, draft.Hero, cards, null, false);
            var context = profile.AchievementContextFor(played, true, npc.Id, rating, "Bronze");
            var record = profile.BeginLaunch(selected, npc.DeckIds[0], 6, npc.Id, npc.DeckIds[0], rating, context, _ => { });
            var pending = record.AchievementContext;
            // A player concede: the evidence is bound to the frozen context, and settlement skips achievement evaluation.
            var evidence = new LocalAchievementMatchEvidence(record.Id, "Lost", pending.StartingHero, pending.QueuedNpcId, pending.QueuedNpcBand,
                true, false, false, LocalAchievementContextBinding.Create(pending), new string('A', 64), true, 1, 2, 3, 4
#if ENGINE_SOURCE
                , null
#endif
                );
            profile.Settle(record.Id, "Lost", 5, true, null, null, null, evidence); profile.AcknowledgeRewardPresentation(record.Id);
            Check(LocalRematch.Encounter(profile, mm, selected, out refusal) != null, "unchanged personal deck rematches: " + refusal);

            string[] edited = cards.ToArray();
            edited[0] = LocalDecks.Get("mage-frost").CardIds.First(id => !cards.Contains(id));
            profile.SaveDraft(draft.Id, draft.Name, draft.Hero, edited, draft.SortOrder);
            Check(LocalRematch.Encounter(profile, mm, selected, out refusal) == null && refusal == "deck changed since the match",
                "an edited personal deck is refused: " + refusal);
            profile.SaveDraft(draft.Id, draft.Name, draft.Hero, cards.Reverse().ToArray(), draft.SortOrder);
            Check(LocalRematch.Encounter(profile, mm, selected, out refusal) != null, "the same list in another order still rematches: " + refusal);
        }
    }
}
