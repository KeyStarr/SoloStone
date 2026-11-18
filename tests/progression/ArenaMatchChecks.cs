using System.Xml.Serialization;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

// Arena (#6) matches on top of the run core: the Arena match mode, settling into the run in the same commit, what an Arena
// match pays and what it must not, restart recovery and the generated NPC decks.
internal static class ArenaMatchChecks
{
    private static readonly XmlSerializer Xml = new XmlSerializer(typeof(LocalProfileData));
    private const string Npc = "npc.g.0001", NpcDeck = "npc-arena-0001";
    private static void Check(bool ok, string message) { if (!ok) throw new Exception("ArenaMatch: " + message); }
    private static LocalProfileData Read(string path) { using var input = File.OpenRead(path); return (LocalProfileData)Xml.Deserialize(input)!; }
    private static void Write(string path, LocalProfileData data) { using var output = File.Create(path); Xml.Serialize(output, data); }
    private static void Reject(Action action, string message)
    {
        try { action(); } catch (Exception error) when (error is InvalidOperationException || error is ArgumentException) { return; }
        throw new Exception("ArenaMatch accepted " + message);
    }
    private static void Ok(ArenaView view) => Check(view.Error == null, view.Error ?? "");
    private static void Fund(LocalProfile profile, int gold = 100000) => profile.CommitChange(next => { next.GoldBalance = gold; return true; });
    private static int Packs(LocalProfile profile) => profile.FeatsSnapshot().UnopenedPacks.Sum(row => row.Count);

    // Loads a copy of a save that is still open (the open profile holds the lock beside the original).
    private static void Reload(string path)
    {
        string copy = path + "-copy.xml"; File.Copy(path, copy, true);
        using (new LocalProfile(copy)) { }
    }

    private static ArenaService Ready(LocalProfile profile, string request)
    {
        var service = new ArenaService(profile);
        Ok(service.Begin(request)); Ok(service.Choose(0, 0));
        for (int slot = 1; slot <= 30; slot++) Ok(service.Choose(slot, slot % 3));
        Check(service.View().Phase == ArenaPhase.Ready, "run ready");
        return service;
    }

    private static LocalMatchRecord Start(LocalProfile profile, bool context = true, int seed = 11)
    {
        var run = profile.ArenaRun;
        string key = ArenaRules.DeckKey(run.RunId);
        var frozen = context ? profile.AchievementContextFor(LocalDecks.ArenaDeckFor(key, run.Hero, run.Deck), true, Npc, 1300, "Gold", null, null, true) : null;   // as LocalLadder: owned cards only
        return profile.BeginLaunch(key, NpcDeck, seed, Npc, NpcDeck, 1300, frozen, ArenaRules.Mode, _ => { });
    }

    internal static void Run(string root)
    {
        Begin(root); Settle(root); Ends(root); Recovery(root); Pays(root); Unchanged(root); Tamper(root); Hook(root); Decks();
        Console.WriteLine("PASS ArenaMatch: Begin gates, settle into the run, 12th win/3rd loss pay once, restart recovery, no per-match payouts, XP/mastery/pets, Ranked unchanged, NPC decks deterministic");
    }

    private static void Begin(string root)
    {
        string path = Path.Combine(root, "arena-m-begin.xml");
        using var profile = new LocalProfile(path); Fund(profile);
        Check(ArenaRules.IsArena("Arena") && !ArenaRules.IsRanked("Arena") && !ArenaRules.IsRanked("Casual") && ArenaRules.IsRanked(null), "mode helpers");
        Check(!ArenaRules.CarriesNpcMemory(ArenaRules.Mode) && ArenaRules.CarriesNpcMemory(null) && ArenaRules.CarriesNpcMemory(CasualRules.Mode), "Arena opponents bring no rival memory");
        Reject(() => profile.Begin("arena:none", "npc-x", 1, Npc, NpcDeck, 1300, ArenaRules.Mode), "Arena begin without a run");
        var service = new ArenaService(profile); Ok(service.Begin("gate"));
        string key = ArenaRules.DeckKey(profile.ArenaRun.RunId);
        Reject(() => profile.Begin(key, "npc-x", 1, Npc, NpcDeck, 1300, ArenaRules.Mode), "Arena begin while choosing a hero");
        Ok(service.Choose(0, 0));
        Reject(() => profile.Begin(key, "npc-x", 1, Npc, NpcDeck, 1300, ArenaRules.Mode), "Arena begin while drafting");
        for (int slot = 1; slot <= 30; slot++) Ok(service.Choose(slot, 0));
        var bytes = File.ReadAllBytes(path);
        Reject(() => profile.Begin("arena:other", "npc-x", 1, Npc, NpcDeck, 1300, ArenaRules.Mode), "another run's deck key");
        Reject(() => profile.Begin("mage", "npc-x", 1, Npc, NpcDeck, 1300, ArenaRules.Mode), "Arena mode with a loaner deck");
        Reject(() => profile.Begin(key, "npc-x", 1, Npc, NpcDeck, 1300, null), "Arena deck in a Ranked match");
        Reject(() => profile.Begin(key, "npc-x", 1, Npc, NpcDeck, 1300, CasualRules.Mode), "Arena deck in a Casual match");
        Reject(() => profile.Begin(key, "npc-x", 1, Npc, NpcDeck, 1300, "Bogus"), "unknown mode");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)) && profile.Pending == null, "rejections write nothing");
        var started = Start(profile);
        Check(started.Mode == ArenaRules.Mode && started.ArenaRunId == profile.ArenaRun.RunId && started.DeckHero == profile.ArenaRun.Hero
            && started.Deck == key && profile.Pending.ArenaRunId == started.ArenaRunId, "pending Arena record names run, deck and class");
        Reject(() => Start(profile), "second pending match");
        Check(service.Retire().Error != null, "no retire with a pending match");
    }

    private static void Settle(string root)
    {
        foreach (var row in new[] { ("Won", false, 1, 0), ("Lost", false, 0, 1), ("Lost", true, 0, 1), ("Tied", false, 0, 0) }) {
            string path = Path.Combine(root, "arena-m-" + row.Item1 + row.Item2 + ".xml");
            string runId;
            using (var profile = new LocalProfile(path)) {
            Fund(profile);
            var service = Ready(profile, "settle"); runId = profile.ArenaRun.RunId;
            int gold = profile.Gold; long dust = profile.DustBalance; int packs = Packs(profile);
            var ladder = profile.Ladder; double casual = profile.CasualRating; int ranked = profile.RankedMatchCount;
            var pending = Start(profile);
            var done = profile.Settle(pending.Id, row.Item1, 9, row.Item2, null, 1300);
            string label = row.Item1 + (row.Item2 ? " conceded" : "");
            Check(done.Mode == ArenaRules.Mode && done.Gold == 0 && done.BonusGold == 0 && !done.PackReward && done.DustReward == 0
                && done.RivalGold == 0 && done.Ladder == null && done.Casual == null && (done.TierRewards?.Count ?? 0) == 0
                && done.AchievementGrants.Count == 0 && done.Packs.Count == 0, label + " pays nothing per match");
            Check(profile.Gold == gold && profile.DustBalance == dust && Packs(profile) == packs, label + " balances unchanged");
            Check(profile.Ladder.StarIndex == ladder.StarIndex && profile.Ladder.Rating == ladder.Rating && profile.Ladder.StreakWins == ladder.StreakWins
                && profile.CasualRating == casual && profile.RankedMatchCount == ranked && profile.RankedHistory.Count == 0, label + " moves no rank or rating");
            Check(profile.ArenaRun.Wins == row.Item3 && profile.ArenaRun.Losses == row.Item4 && profile.ArenaRun.Phase == ArenaPhase.Ready
                && done.ArenaWins == row.Item3 && done.ArenaLosses == row.Item4, label + " settled into the run");
            Check(done.ClassXpAfter - done.ClassXpBefore == LocalProfile.MatchClassXp(done) && (row.Item1 != "Won" || done.ClassXpAfter > done.ClassXpBefore),
                label + " pays the Ranked class XP");
            Check(LocalNpcRecord.Of(profile.History, Npc).Games == 0, label + " leaves no rivalry record");
            // The reward presentation gate: the next Arena match waits for the results screen, then starts.
            Check(profile.HasPendingRewardPresentation, label + " reward presentation pending");
            Reject(() => Start(profile), "next Arena match before the results screen acknowledged");
            Check(profile.AcknowledgeRewardPresentation(done.Id), "results screen acknowledges");
            Start(profile, true, 12);
            var receipt = LocalResultsReceipt.FromHistory(profile.History, profile.History.Single(m => m.Id == done.Id));
            Check(receipt.Arena && !receipt.Casual && !receipt.HasRank && !receipt.HasItems && receipt.RankText == "Arena run: " + row.Item3 + " wins, " + row.Item4 + " losses"
                && receipt.SeasonGamesAfter == receipt.SeasonGamesBefore && receipt.SeasonWinsAfter == receipt.SeasonWinsBefore, label + " results screen shows the run record");
            }
            using (var again = new LocalProfile(path)) {
                Check(again.ArenaRun.Wins == row.Item3 && again.ArenaRun.Losses == row.Item4 && again.Pending.ArenaRunId == runId, row.Item1 + " reopen");
            }
        }
    }

    private static void Ends(string root)
    {
        foreach (var row in new[] { (11, 0, "Won", 12, 0), (0, 2, "Lost", 0, 3), (0, 2, "Abandoned", 0, 3) }) {
            string path = Path.Combine(root, "arena-m-end-" + row.Item3 + ".xml");
            string runId; int gold, packs; long dust;
            using (var profile = new LocalProfile(path)) {
                Fund(profile);
                var service = Ready(profile, "end"); runId = profile.ArenaRun.RunId;
                profile.CommitChange(next => { next.ArenaRun.Wins = row.Item1; next.ArenaRun.Losses = row.Item2; return true; });
                var pending = Start(profile);
                int goldBefore = profile.Gold, packsBefore = Packs(profile); long dustBefore = profile.DustBalance;
                if (row.Item3 == "Abandoned") {
                    profile.MarkStarted(pending.Id);
                    foreach (string stage in new[] { "write", "flush", "replace" }) {
                        byte[] before = File.ReadAllBytes(path);
                        profile.CommitFailure = point => { if (point == stage) throw new IOException("Arena recovery injected"); };
                        try { profile.RecoverInterrupted(); throw new Exception("expected failure"); } catch (IOException) { } finally { profile.CommitFailure = null; }
                        Check(before.SequenceEqual(File.ReadAllBytes(path)) && profile.Pending != null && profile.ArenaRun.Losses == 2, "abandon failure " + stage);
                    }
                    profile.RecoverInterrupted();
                } else {
                    foreach (string stage in new[] { "write", "flush", "replace" }) {
                        byte[] before = File.ReadAllBytes(path);
                        profile.CommitFailure = point => { if (point == stage) throw new IOException("Arena settle injected"); };
                        try { profile.Settle(pending.Id, row.Item3, 8, false); throw new Exception("expected failure"); } catch (IOException) { } finally { profile.CommitFailure = null; }
                        Check(before.SequenceEqual(File.ReadAllBytes(path)) && profile.Pending?.Id == pending.Id && profile.ArenaRun.Wins == row.Item1
                            && profile.ArenaRun.Phase == ArenaPhase.Ready && profile.Gold == goldBefore && profile.DustBalance == dustBefore, "settle failure " + stage + " leaves the save byte-identical");
                    }
                    profile.Settle(pending.Id, row.Item3, 8, false);
                }
                var run = profile.ArenaRun;
                Check(run.Phase == ArenaPhase.RewardPending && run.Wins == row.Item4 && run.Losses == row.Item5 && run.Reward.Wins == row.Item4, row.Item3 + " ends the run");
                Check(profile.Gold == goldBefore + run.Reward.Gold && profile.DustBalance == dustBefore + run.Reward.Dust
                    && Packs(profile) == packsBefore + run.Reward.PackTypes.Count && profile.FeatsSnapshot().ArenaReceipts.Count(r => r.RunId == runId) == 1,
                    row.Item3 + " pays the run reward exactly once, in the settle commit");
                gold = profile.Gold; dust = profile.DustBalance; packs = Packs(profile);
                if (row.Item3 != "Abandoned") {
                    var settled = profile.History.Single(m => m.Id == pending.Id);
                    var again = profile.Settle(pending.Id, row.Item3, 8, false);
                    Check(again.Id == settled.Id && profile.Gold == gold && profile.DustBalance == dust && Packs(profile) == packs
                        && profile.FeatsSnapshot().ArenaReceipts.Count == 1, "repeated settle pays nothing more");
                }
            }
            using (var reopened = new LocalProfile(path)) {
                var service = new ArenaService(reopened);
                Check(service.View().Phase == ArenaPhase.RewardPending && service.View().Reward.RunId == runId && reopened.Gold == gold
                    && reopened.DustBalance == dust && Packs(reopened) == packs, row.Item3 + " restart shows the unclaimed reward with the same balances");
                if (reopened.HasPendingRewardPresentation) Check(reopened.AcknowledgeRewardPresentation(reopened.History.Last().Id), "match presentation");
                Ok(service.AcknowledgeReward(runId));
                Check(service.View().Phase == ArenaPhase.None && reopened.Gold == gold, row.Item3 + " claim pays nothing twice");
            }
        }
    }

    private static void Recovery(string root)
    {
        string path = Path.Combine(root, "arena-m-recover.xml");
        string runId;
        using (var profile = new LocalProfile(path)) {
            Fund(profile); Ready(profile, "recover"); runId = profile.ArenaRun.RunId;
            Start(profile);   // never reached the engine (Unstarted): no penalty
            profile.RecoverInterrupted();
            Check(profile.Pending == null && profile.ArenaRun.Losses == 0 && profile.History.Count == 0, "unstarted Arena match is cancelled, not a loss");
            var started = Start(profile, true, 21); profile.MarkStarted(started.Id);
        }
        using (var profile = new LocalProfile(path)) {
            Check(profile.Pending != null && profile.Pending.ArenaRunId == runId, "interrupted Arena match survives a restart");
            profile.RecoverInterrupted();
            var lost = profile.History.Single();
            Check(lost.Result == "Abandoned" && lost.Mode == ArenaRules.Mode && lost.Gold == 0 && lost.Ladder == null && lost.ArenaLosses == 1
                && profile.ArenaRun.Losses == 1 && profile.ArenaRun.Wins == 0 && profile.ArenaRun.Phase == ArenaPhase.Ready, "abandoned Arena match is a loss for the run");
            Check(lost.ClassXpAfter >= lost.ClassXpBefore && profile.RankedMatchCount == 0 && profile.Ladder.StarIndex == 0, "abandoned Arena match moves no rank");
            profile.RecoverInterrupted();
            Check(profile.ArenaRun.Losses == 1, "recovery is idempotent");
            Start(profile, true, 22);   // an abandoned match needs no results screen
        }
        // A save whose run is gone (hand edit, cheat): settling still records the match and never wedges the profile.
        string orphan = Path.Combine(root, "arena-m-orphan.xml");
        using (var profile = new LocalProfile(orphan)) {
            Fund(profile); Ready(profile, "orphan"); var pending = Start(profile);
            profile.CommitChange(next => { next.ArenaRun = null; return true; });
            var done = profile.Settle(pending.Id, "Won", 8, false);
            Check(done.Mode == ArenaRules.Mode && done.ArenaWins == 0 && profile.ArenaRun == null && profile.Pending == null, "orphan Arena match settles without a run");
        }
        using (new LocalProfile(orphan)) { }
    }

    private static void Pays(string root)
    {
        string path = Path.Combine(root, "arena-m-pays.xml");
        using var profile = new LocalProfile(path); Fund(profile, 200000);
        profile.Purchase("pet-PET_3_1", 1, 5000, "arena-pet");
        var service = Ready(profile, "pays");
        string pet = profile.EffectiveSelectedPet;
        Check(pet != null, "pet owned");
        int xp = profile.PetXp(LocalShop.Default.PetFamily(pet));
        var run = profile.ArenaRun;
        // Owner 2026-11-15, owned cards only: the player owns (normal copies) some drafted cards, not the rest.
        var distinct = run.Deck.Distinct().ToList();
        var mine = distinct.Take(Math.Max(1, distinct.Count / 3)).ToList();
        profile.CommitChange(next => { foreach (string id in mine) if (next.Collection.All(row => row.Id != id)) next.Collection.Add(new LocalCardCount { Id = id, Count = 1, Acquired = 1 }); return true; });
        var ownedNow = new HashSet<string>(profile.StoredCollection.Where(row => row.Value > 0).Select(row => LocalCardCatalog.CopyGroup(row.Key)));
        // A card owned only as a golden copy is owned too.
        string goldOnly = distinct.First(id => !ownedNow.Contains(LocalCardCatalog.CopyGroup(id)));
        profile.CommitChange(next => { next.PremiumCollection.Add(new LocalCardCount { Id = goldOnly, Premium = 1, Count = 1, Acquired = 1 }); return true; });
        ownedNow.Add(LocalCardCatalog.CopyGroup(goldOnly));
        var unowned = distinct.Where(id => !ownedNow.Contains(LocalCardCatalog.CopyGroup(id))).ToList();
        Check(unowned.Count > 0 && mine.All(id => ownedNow.Contains(LocalCardCatalog.CopyGroup(id))), "test deck mixes owned and unowned cards");
        var pending = Start(profile);
        int gold = profile.Gold; long dust = profile.DustBalance;
        var done = profile.Settle(pending.Id, "Won", 12, false, null, 1300, null, null, new LocalPetXpCounts { Medium = 4 });
        Check(done.Pet == pet && done.PetXpBefore == xp && done.PetXpAfter > xp && profile.PetXp(LocalShop.Default.PetFamily(pet)) == done.PetXpAfter, "pet XP applies");
        Check(done.ClassXpAfter - done.ClassXpBefore == LocalProfile.MatchClassXp(done) && done.ClassXpAfter > done.ClassXpBefore, "class XP is the pace-scaled Ranked amount");
        Check(LocalCardMastery.EligibleWin(done) && !LocalProfile.XpWin(done), "mastery counts the win, win portraits do not");
        var counts = LocalCardMastery.WinCounts(profile.History);
        Check(distinct.Where(id => ownedNow.Contains(LocalCardCatalog.CopyGroup(id))).All(id => counts.TryGetValue(id, out int wins) && wins == 1), "owned drafted cards earn the mastery win");
        Check(counts.TryGetValue(goldOnly, out int goldWins) && goldWins == 1, "a golden-only owned card earns the mastery win");
        Check(unowned.All(id => !counts.ContainsKey(id)), "unowned drafted cards earn nothing");
        // Many wins: an unowned card never reaches a threshold, so no premium copy of it is ever granted; an owned one does.
        string copyPath = path + "-mastery.xml"; File.Copy(path, copyPath, true);
        var data = Read(copyPath);
        var template = done;
        for (int i = 0; i < 60; i++) {
            var clone = (LocalMatchRecord)new XmlSerializer(typeof(LocalMatchRecord)).Deserialize(new StringReader(Serialize(template)))!;
            clone.Id = "arena-mastery-" + i; clone.MasteryGrants = null; clone.AchievementContext.MatchId = clone.Id;
            data.Matches.Add(clone);
        }
        data.Matches.RemoveAll(m => m.Id == done.Id);
        data.MasteryGrants.Clear(); data.PremiumCollection.Clear();
        LocalCardMastery.Grant(data, null);
        Check(data.MasteryGrants.Count > 0 && data.MasteryGrants.All(g => !unowned.Contains(g.CardId)), "only owned cards reach a mastery grant");
        Check(data.PremiumCollection.All(row => !unowned.Contains(row.Id)), "no premium copy of an unowned card is granted");
        Check(profile.Gold == gold && profile.DustBalance == dust && profile.Achievements.Unlocks.Count == 0 && done.AchievementGrants.Count == 0
            && done.AchievementEvidenceFingerprint == null, "no gold, dust or achievement");
        Reload(path);   // the saved Arena record (frozen context, no fingerprint) loads again
    }

    private static string Serialize(LocalMatchRecord record) { var text = new StringWriter(); new XmlSerializer(typeof(LocalMatchRecord)).Serialize(text, record); return text.ToString(); }

    private static void Unchanged(string root)
    {
        string path = Path.Combine(root, "arena-m-ranked.xml");
        using var profile = new LocalProfile(path); Fund(profile);
        Ready(profile, "beside");
        // A Ready run does not change Ranked or Casual: both still start, pay and rate as before.
        var ranked = profile.Begin("mage", "mage", 31);
        var won = profile.Settle(ranked.Id, "Won", 9, false);
        Check(won.Mode == null && won.Ladder != null && won.ArenaRunId == null && profile.RankedMatchCount == 1 && profile.ArenaRun.Wins == 0, "Ranked settles as before");
        profile.AcknowledgeRewardPresentation(won.Id);
        var casual = profile.Begin("mage", "mage", 32, null, null, null, CasualRules.Mode);
        var played = profile.Settle(casual.Id, "Won", 9, false);
        Check(played.Mode == CasualRules.Mode && played.Casual != null && played.Ladder == null && played.ArenaRunId == null
            && profile.ArenaRun.Wins == 0 && profile.RankedMatchCount == 1, "Casual settles as before");
        profile.AcknowledgeRewardPresentation(played.Id);
        // Arena history stays out of the ranked ledger even between ranked matches.
        var arena = Start(profile, true, 33); profile.Settle(arena.Id, "Lost", 9, false); profile.AcknowledgeRewardPresentation(arena.Id);
        Check(profile.RankedHistory.Count == 1 && profile.CasualHistory.Count == 1 && profile.History.Count == 3, "ledgers stay separate");
        Check(LocalDeckRecord.Of(profile.History, ArenaRules.DeckKey(profile.ArenaRun.RunId)).Games == 0, "Arena decks have no deck record");
        Reload(path);
    }

    private static void Tamper(string root)
    {
        string path = Path.Combine(root, "arena-m-tamper.xml");
        using (var profile = new LocalProfile(path)) {
            Fund(profile); Ready(profile, "tamper"); var pending = Start(profile); profile.Settle(pending.Id, "Won", 8, false);
        }
        var valid = Read(path);
        foreach (Action<LocalProfileData> mutate in new Action<LocalProfileData>[] {
            data => data.Matches[0].Gold = 5, data => data.Matches[0].PackReward = true, data => data.Matches[0].DustReward = 3,
            data => data.Matches[0].ArenaRunId = null, data => data.Matches[0].Mode = null,
            data => data.Matches[0].Casual = new LocalCasualReceipt { RatingBefore = 1000, RatingAfter = 1016, OpponentRating = 1000 },
            data => data.Matches[0].RivalGold = 50,
        }) {
            Write(path, valid); var tampered = Read(path); mutate(tampered); Write(path, tampered);
            Reject(() => { using var bad = new LocalProfile(path); }, "tampered Arena match record");
        }
        Write(path, valid); using (new LocalProfile(path)) { }
    }

    private static void Hook(string root)
    {
        using var profile = new LocalProfile(Path.Combine(root, "arena-m-hook.xml")); Fund(profile);
        var service = new ArenaService(profile);
        int calls = 0;
        ArenaMatchHook.MatchStarter = (ArenaRun run, out string error) => { calls++; error = null; return true; };
        try {
            Check(!service.StartMatch(out string error) && error != null && calls == 0, "no start without a run");
            Ok(service.Begin("hook")); Ok(service.Choose(0, 0));
            Check(!service.StartMatch(out error) && calls == 0, "no start while drafting");
            for (int slot = 1; slot <= 30; slot++) Ok(service.Choose(slot, 0));
            Check(service.StartMatch(out error) && calls == 1, "Ready run starts through the hook");
            Start(profile);
            Check(!service.StartMatch(out error) && error.Contains("pending") && calls == 1, "no start with a pending match");
        } finally { ArenaMatchHook.MatchStarter = null; }
        Check(!new ArenaService(profile).StartMatch(out string none) && none != null, "no hook, no start");
    }

    private static void Decks()
    {
        string json = ArenaDeckGenerator.Json();
        Check(json == ArenaDeckGenerator.Json(), "generator is deterministic");
        string repo = RepoRoot.Find("data/npc/arena-decks-v1.json") ?? throw new Exception("ArenaMatch: data/npc/arena-decks-v1.json not found");
        Check(File.ReadAllBytes(Path.Combine(repo, "data/npc/arena-decks-v1.json")).SequenceEqual(new System.Text.UTF8Encoding(false).GetBytes(json)),
            "data/npc/arena-decks-v1.json is stale: regenerate with ProgressionTests --gen-arena-decks data/npc/arena-decks-v1.json");
        var rows = ArenaDeckGenerator.Decks(); var rules = DraftRules.Arena();
        Check(rows.Count == ArenaDeckGenerator.DeckCount && rows.Select(r => r.Id).Distinct().Count() == rows.Count && rows.Select(r => r.Seed).Distinct().Count() == rows.Count, "200 distinct decks");
        foreach (var row in rows) {
            var ids = row.Cards.SelectMany(card => Enumerable.Repeat(card.Key, card.Value)).ToList();
            var deck = LocalDecks.ArenaDeckFor("arena:test", row.Hero, ids);
            var built = deck.Build();
            Check(ids.Count == 30 && built.Count == 30 && LocalDecks.PlayerHeroes.Contains(row.Hero) && built.All(card => card != null && card.Implemented && rules.Eligible(card)
                && LocalDecks.IsAllowedInDeck(card, LocalDecks.HeroClass(row.Hero))), "deck loads " + row.Id);
            LocalDecks.ValidateRunes(built, LocalDecks.HeroClass(row.Hero));
            Check(built.All(card => card.Collectible && LocalCardCatalog.IsStandardLegal(card) && card.Id != "GIL_213" && card.Id != "CFM_305"),
                "NPC deck has only collectible Standard-legal cards (no Wild, Basic, Classic, Hall of Fame) " + row.Id);
        }
        Check(rows.Select(r => r.Class).Distinct().Count() >= 8 && rows.Select(r => r.Skill).Distinct().Count() == ArenaDeckGenerator.Skills.Length, "mixed classes and drafter skill");
        double Mean(double skill) => rows.Where(r => r.Skill == skill).Average(r => r.Quality);
        Check(Mean(0.95) > Mean(0.15), "strong drafters score higher");
    }
}
