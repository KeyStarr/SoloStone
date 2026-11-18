using System.Xml.Serialization;
using SabberStoneCore.Model;
using SabberStoneCore.Enums;

internal static class ArenaChecks
{
    private static readonly XmlSerializer Xml = new XmlSerializer(typeof(LocalProfileData));
    private static void Check(bool ok, string message) { if (!ok) throw new Exception("Arena: " + message); }
    private static LocalProfileData Read(string path) { using var input = File.OpenRead(path); return (LocalProfileData)Xml.Deserialize(input)!; }
    private static void Write(string path, LocalProfileData data) { using var output = File.Create(path); Xml.Serialize(output, data); }
    private static void Reject(Action action, string message)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new Exception("Arena accepted " + message);
    }
    private static void Ok(ArenaView view) => Check(view.Error == null, view.Error ?? "");
    private static void Fund(LocalProfile profile, int gold = 1000) => profile.CommitChange(next => { next.GoldBalance = gold; return true; });
    private static void Draft(ArenaService service)
    {
        Ok(service.Choose(0, 0));
        for (int slot = 1; slot <= 30; slot++) Ok(service.Choose(slot, slot % 3));
        Check(service.View().Phase == ArenaPhase.Ready && service.View().Slot == 30 && service.View().Deck.Count == 30, "30 picks ready");
    }
    private static void Unchanged(LocalProfile profile, string path, Func<ArenaView> action, string message)
    {
        var bytes = File.ReadAllBytes(path); var before = profile.ArenaRun;
        Check(action().Error != null && bytes.SequenceEqual(File.ReadAllBytes(path)), message + " bytes");
        Check(profile.ArenaRun?.Picks.Count == before?.Picks.Count && profile.ArenaRun?.Phase == before?.Phase, message + " memory");
    }
    private static void FailsAtomically(LocalProfile profile, string path, Func<ArenaView> action)
    {
        foreach (string stage in new[] { "write", "flush", "replace" }) {
            profile.CommitFailure = point => { if (point == stage) throw new IOException("Arena injected " + stage); };
            try { Unchanged(profile, path, action, "failed " + stage); }
            finally { profile.CommitFailure = null; }
        }
    }

    internal static void Run(string root)
    {
        Offers(); Rewards();
        using (var poor = new LocalProfile(Path.Combine(root, "arena-poor.xml"))) {
            Fund(poor, 40);
            Check(new ArenaService(poor).Begin("poor-1").Error == "Arena costs 150 gold. You have 40 gold.", "refusal names the cost and the gold");
        }
        string path = Path.Combine(root, "arena-run.xml");
        string runId; List<string> offers;
        using (var profile = new LocalProfile(path)) {
            Fund(profile);
            var service = new ArenaService(profile);
            FailsAtomically(profile, path, () => service.Begin("entry-1"));
            Unchanged(profile, path, () => service.Begin(""), "empty request");
            Ok(service.Begin("entry-1")); runId = service.View().RunId;
            Check(profile.Gold == 850 && service.View().Phase == ArenaPhase.ChoosingHero, "entry cost/phase");
            var bytes = File.ReadAllBytes(path);
            Ok(service.Begin("entry-1")); Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "retry entry no write");
            Unchanged(profile, path, () => service.Begin("entry-2"), "second active run");
            Unchanged(profile, path, () => service.Choose(-1, 0), "wrong hero slot");
            Unchanged(profile, path, () => service.Choose(0, 3), "wrong hero index");
            FailsAtomically(profile, path, () => service.Choose(0, 1));
            Ok(service.Choose(0, 1));
            bytes = File.ReadAllBytes(path); Ok(service.Choose(0, 1)); Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "hero retry no write");
            Unchanged(profile, path, () => service.Choose(0, 2), "different old hero");
            offers = service.View().Offers;
        }
        // Reopen is the simulated crash after a successful entry/pick commit but before the caller got its reply.
        using (var profile = new LocalProfile(path)) {
            var service = new ArenaService(profile);
            Ok(service.Begin("entry-1")); Check(profile.Gold == 850 && profile.ArenaRun.RunId == runId, "restart entry not charged twice");
            Check(service.View().Offers.SequenceEqual(offers), "restart exact offer");
            FailsAtomically(profile, path, () => service.Choose(1, 2));
            Ok(service.Choose(1, 2));
            var bytes = File.ReadAllBytes(path); Ok(service.Choose(1, 2)); Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "card retry no write");
            Unchanged(profile, path, () => service.Choose(1, 1), "different old card");
            Unchanged(profile, path, () => service.Choose(3, 0), "future slot");
            Unchanged(profile, path, () => service.Choose(2, -1), "negative index");
            for (int slot = 2; slot <= 30; slot++) Ok(service.Choose(slot, 0));
            Check(service.View().Phase == ArenaPhase.Ready && service.View().Slot == 30, "draft ready");
            bytes = File.ReadAllBytes(path); Ok(service.Choose(30, 0)); Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "last card retry");
            Unchanged(profile, path, () => service.Choose(31, 0), "31st pick");
            Check(!service.StartMatch(out string error) && error == "Arena matches are not available here.", "no match starter without the plugin");
            profile.CommitChange(next => { next.ArenaRun.Wins = 5; return true; });   // a non-zero reward: 0 wins would pass trivially
            FailsAtomically(profile, path, service.Retire);
            long dust = profile.DustBalance; int gold = profile.Gold, packs = profile.FeatsSnapshot().UnopenedPacks.Sum(row => row.Count);
            Ok(service.Retire());
            var reward = service.View().Reward;
            Check(reward.Wins == 5 && reward.Gold == profile.GrindTier.Gold(375) && reward.Dust == profile.GrindTier.Dust(140) && reward.Gold > 0
                && reward.PackTypes.Count == 2 && profile.Gold == gold + reward.Gold && profile.DustBalance == dust + reward.Dust
                && profile.FeatsSnapshot().UnopenedPacks.Sum(row => row.Count) == packs + 2, "retire paid atomically");
            bytes = File.ReadAllBytes(path); Ok(service.Retire()); Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "retire retry");
            Unchanged(profile, path, () => service.AcknowledgeReward("wrong"), "wrong reward ack");
            FailsAtomically(profile, path, () => service.AcknowledgeReward(runId));
        }
        using (var profile = new LocalProfile(path)) {
            var service = new ArenaService(profile);
            Check(service.View().Phase == ArenaPhase.RewardPending && service.View().Reward.RunId == runId, "reward restart");
            Ok(service.AcknowledgeReward(runId)); Ok(service.AcknowledgeReward(runId));
            Check(service.View().Phase == ArenaPhase.None && profile.FeatsSnapshot().ArenaReceipts.Count == 1, "ack keeps receipt");
            Unchanged(profile, path, () => service.Begin("entry-1"), "spent entry id is refused, not a fake success"); var bytes = File.ReadAllBytes(path);
            Ok(service.Begin("entry-2")); Ok(service.Retire());
            Check(service.View().Reward.Wins == 0, "retire at hero choice");
            Ok(service.AcknowledgeReward(service.View().RunId));
            Ok(service.Begin("entry-3")); Ok(service.Choose(0, 0)); Ok(service.Retire());
            Check(service.View().Reward.Wins == 0, "retire before first card");
            Ok(service.AcknowledgeReward(service.View().RunId));
            for (int i = 0; i < 21; i++) {
                Fund(profile); Ok(service.Begin("rolling-" + i)); Ok(service.Retire());
                Ok(service.AcknowledgeReward(service.View().RunId));
            }
            Check(profile.FeatsSnapshot().ArenaReceipts.Count == 20 && !profile.FeatsSnapshot().ArenaReceipts.Any(row => row.RunId == runId), "audit receipts bounded");
            Unchanged(profile, path, () => service.Begin("entry-1"), "pruned entry id stays spent");
        }
        Gates(root); Settlement(root); Compatibility(root); SaveVersion(root); PreBumpFixture(root); Registry(root); Bot();
        StoredHistory(root);
        ArenaMatchChecks.Run(root);
        Console.WriteLine("PASS Arena: deterministic draft, rune/exclusion rules, restart/retries, atomic rewards, tamper rejection, current schema/V15 Arena upgrade, NPC drafter");
    }

    private static void Offers()
    {
        var rules = DraftRules.Arena();
        var activeIds = new HashSet<string>(LocalCardCatalog.ActivePoolIds(), StringComparer.Ordinal);
        // Arena uses the Ranked pool: every card it can offer is one a player may put in a current Standard deck.
        Console.WriteLine("ARENA pool size " + rules.Pool.Count + " (ActivePoolIds " + activeIds.Count + ")");
        Check(rules.Pool.Count > 0 && rules.Pool.All(LocalCardCatalog.IsStandardLegal), "pool is only Standard-legal cards");
        Check(!rules.Pool.Any(card => card.Id == "GIL_213" || card.Id == "CFM_305"), "Wild pack-pool cards are not in the pool");
        // A pool change can leave a rarity bucket under three cards: offers fall back deterministically instead of failing the pick.
        var tiny = DraftRules.WithPool(Cards.All.Where(c => c.Collectible && c.Rarity == Rarity.COMMON && c.Class == CardClass.NEUTRAL).Take(5));
        var logged = new List<string>(); DraftRules.FallbackLog = logged.Add; DraftRules.fallbackLogged = false;
        try {
            string tinyHero = rules.Heroes[0];
            for (int slot = 1; slot <= 2; slot++) {
                var deck = slot == 1 ? new string[0] : new[] { "x" };
                if (slot == 2) deck = new[] { tiny.Pool[0].Id };
                var a = tiny.Offers(7, slot, tinyHero, deck);
                Check(a.Count == 3 && a.Distinct().Count() == 3 && a.SequenceEqual(tiny.Offers(7, slot, tinyHero, deck)), "tiny pool offer falls back deterministically, slot " + slot);
            }
            Check(logged.Count == 1, "the fallback is logged once");
        } finally { DraftRules.FallbackLog = null; }
        foreach (string hero in rules.Heroes)
            for (int seed = 0; seed < 60; seed++)
                foreach (string id in rules.Offers(seed * 7919L + 13, 1, hero, new string[0]))
                    Check(LocalCardCatalog.IsStandardLegal(Cards.FromId(id)) && id != "GIL_213" && id != "CFM_305", "offered card outside the Ranked pool: " + id);
        foreach (var card in Cards.All.Where(card => SabberStoneCore.Actions.FabledRules.IsFabled(card.Id)
            || SabberStoneCore.Actions.FabledRules.IsCompanion(card.Id) || SabberStoneCore.Actions.DeckbuildingRules.HasSideboard(card.Id)
            || card[(GameTag)2931] > 0 || card[(GameTag)997] > 0 || card[(GameTag)1048] > 0))
            Check(!rules.Eligible(card), "excluded metadata " + card.Id);
        // denyIds that do not exist in this engine are dead entries (reported by name below, never silently passing as "excluded").
        var missing = new[] { "TLC_248", "ETC_080", "TOY_330", "REV_018", "BOT_914", "TOY_700", "DAL_800", "GIL_692", "GIL_826" }.Where(x => !Cards.All.Any(c => c.Id == x)).ToList();
        Console.WriteLine("ARENA denyIds not in the engine: " + string.Join(",", missing));
        foreach (string id in new[] { "TLC_248", "BOT_914", "DAL_800", "GIL_692", "GIL_826" })
        {
            var card = Cards.All.FirstOrDefault(c => c.Id == id);
            Check(card != null, "explicit exclusion id exists " + id);
            Check(!rules.Eligible(card), "explicit exclusion " + id);
        }
        // The cards the dead ids meant, found by name: each must exist and be ineligible.
        foreach (string name in new[] { "Zilliax", "Renathal", "Whizbang", "Ultragigasaur", "E.T.C., Band Manager" })
        {
            var named = Cards.All.Where(c => c.Name != null && c.Name.Contains(name) && c.Collectible).ToList();
            Console.WriteLine("ARENA named " + name + ": " + string.Join(", ", named.Select(c => c.Id + (rules.Eligible(c) ? " eligible" : " excluded"))));
        }
        // One real card per printed-text heuristic: collectible, implemented and otherwise eligible, so the heuristic alone excludes it.
        var deny = new HashSet<string>(new[] { "TLC_248", "ETC_080", "TOY_330", "REV_018", "BOT_914", "TOY_700", "DAL_800", "GIL_692", "GIL_826", "CORE_GIL_692", "CORE_GIL_826" });
        bool Otherwise(Card c) => c.Collectible && c.Implemented && !deny.Contains(c.Id) && !SabberStoneCore.Actions.FabledRules.IsFabled(c.Id)
            && !SabberStoneCore.Actions.FabledRules.IsCompanion(c.Id) && !SabberStoneCore.Actions.DeckbuildingRules.HasSideboard(c.Id)
            && c[(GameTag)2931] == 0 && c[(GameTag)997] == 0 && c[(GameTag)1048] == 0;
        string Plain(Card c) => System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(c.Text ?? "", "<[^>]+>", ""), @"[\s_]+", " ").ToLowerInvariant();
        foreach (var heuristic in new (string, Func<Card, bool>)[] {
            ("your deck has", c => Plain(c).Contains("your deck has")), ("start of game", c => Plain(c).Contains("start of game:") || Plain(c).Contains("start of game,")),
            ("cost over 10 without text", c => c.Cost > 10 && string.IsNullOrWhiteSpace(c.Text)) }) {
            var found = Cards.All.Where(c => Otherwise(c) && activeIds.Contains(c.Id) && heuristic.Item2(c)).ToList();
            Check(found.Count > 0 || heuristic.Item1.StartsWith("cost over"), "a real card for the heuristic: " + heuristic.Item1);   // none exists in today's pool
            foreach (var card in found) Check(!rules.Eligible(card), "heuristic excludes " + heuristic.Item1 + " " + card.Id);
        }
        int runeOffers = 0;
        for (int seed = 0; seed < 200; seed++) {
            var heroes = rules.HeroOffers(seed);
            Check(heroes.Count == 3 && heroes.Distinct().Count() == 3 && heroes.SequenceEqual(rules.HeroOffers(seed)), "hero offers");
            string hero = seed % 2 == 0 ? "HERO_11" : LocalCoreProgression.Heroes[seed % LocalCoreProgression.Heroes.Length];
            var deck = new List<string>();
            for (int slot = 1; slot <= 30; slot++) {
                var offers = rules.Offers(seed, slot, hero, deck);
                Check(offers.Count == 3 && offers.Distinct().Count() == 3 && offers.SequenceEqual(rules.Offers(seed, slot, hero, deck)), "card offers");
                foreach (string id in offers) {
                    var card = Cards.FromId(id);
                    Check(rules.Eligible(card) && activeIds.Contains(id)
                        && LocalDecks.IsAllowedInDeck(card, LocalDecks.HeroClass(hero)), "active class offer " + id);
                    Check((card.Rarity == Rarity.LEGENDARY) == (slot == 1), "legendary slot");
                    if (hero == "HERO_11") {
                        Check(LocalDecks.RuneCounts(deck.Concat(new[] { id }).Select(Cards.FromId)).Sum() <= 3, "DK rune bound");
                        if (LocalDecks.RuneCounts(new[] { card }).Sum() > 0) runeOffers++;
                    }
                }
                // Force rune commitments where possible, including repeated two/three-rune cards.
                deck.Add(hero == "HERO_11" ? offers.OrderByDescending(id => LocalDecks.RuneCounts(new[] { Cards.FromId(id) }).Sum()).First() : offers[seed % 3]);
            }
        }
        Check(runeOffers > 100, "rune test exercised requirements");
    }

    private static void Rewards()
    {
        var rules = DraftRules.Arena();
        foreach (var tier in GrindTiers.Selectable)
            for (int wins = 0; wins <= 12; wins++) {
                var reward = rules.Reward("test", wins, tier.Id, FairPackRules.NewestSet);
                int packs = wins < 3 ? 1 : wins < 7 ? 2 : wins < 10 ? 3 : wins < 12 ? 4 : 5;
                Check(reward.Gold == tier.Gold(75 * wins + (wins == 12 ? 200 : 0))
                    && reward.Dust == tier.Dust(28 * wins) && reward.PackTypes.Count == packs, "reward table " + tier.Id + "/" + wins);
            }
        // No run pays back more than its entry without a win (instant retire must never farm gold, on any pace).
        foreach (var pace in GrindTiers.All) {
            var none = rules.Reward("test", 0, pace.Id, FairPackRules.NewestSet);
            Check(none.Gold + none.Dust / 2.0 + 100 * none.PackTypes.Count < rules.EntryGold, "0-win reward below entry " + pace.Id);
        }
        double expectedWins = 0, games = 0, gold = 0, dust = 0, packValue = 0, probability = 0;
        void End(double p, int wins, int losses) {
            var reward = rules.Reward("test", wins, GrindTiers.Standard.Id, FairPackRules.NewestSet);
            probability += p; expectedWins += p * wins; games += p * (wins + losses);
            gold += p * reward.Gold; dust += p * reward.Dust; packValue += p * reward.PackTypes.Count * 100;
        }
        void Walk(int wins, int losses, double p) {
            if (wins == 12 || losses == 3) { End(p, wins, losses); return; }
            Walk(wins + 1, losses, p / 2); Walk(wins, losses + 1, p / 2);
        }
        Walk(0, 0, 1);
        Check(Math.Abs(probability - 1) < 1e-10 && Math.Abs(expectedWins - 2.99) < .01 && Math.Abs(games - 5.98) < .01
            && gold / games > 36.67 && dust / games > 13.33 && packValue > 150, "50% expectation beats Ranked and packs cover entry");
        Console.WriteLine($"PASS Arena 50% expectation: {expectedWins:F4} wins, {games:F4} games, {gold/games:F4} gold + {dust/games:F4} dust/game, {packValue/100:F4} packs/run (Ranked 36.67g + 13.33d)");
    }

    private static void Gates(string root)
    {
        string path = Path.Combine(root, "arena-gates.xml");
        using var profile = new LocalProfile(path);
        Fund(profile, 149); var service = new ArenaService(profile);
        Unchanged(profile, path, () => service.Begin("poor"), "under 150");
        Fund(profile, 150); Ok(service.Begin("exact")); Check(profile.Gold == 0, "exact 150 entry");
        Ok(service.Retire()); Ok(service.AcknowledgeReward(service.View().RunId)); Fund(profile);
        var match = profile.Begin("mage", "mage", 101);
        Unchanged(profile, path, () => service.Begin("pending"), "pending match");
        profile.Settle(match.Id, "Won", 12, false);
        Unchanged(profile, path, () => service.Begin("reward"), "unacknowledged match reward");
        foreach (var tier in GrindTiers.Selectable) {
            using var paced = new LocalProfile(Path.Combine(root, "arena-pace-" + tier.Id + ".xml"), new ProfileOptions { GrindTier = tier.Id });
            Fund(paced); paced.SetGrindTier(tier.Id);
            var arena = new ArenaService(paced); Ok(arena.Begin("pace"));
            Check(paced.Gold == 850 && paced.ArenaRun.GrindTier == tier.Id, "flat entry and stamped pace " + tier.Id);
            Draft(arena); paced.CommitChange(next => { next.ArenaRun.Wins = 5; return true; });
            paced.SetGrindTier(tier == GrindTiers.Retail ? GrindTiers.Relaxed6x.Id : GrindTiers.Retail.Id);
            Ok(arena.Retire());
            Check(arena.View().Reward.Wins == 5 && arena.View().Reward.Gold == tier.Gold(375) && arena.View().Reward.Dust == tier.Dust(140),
                "retire uses stamped pace " + tier.Id);
        }
    }

    private static void Settlement(string root)
    {
        string path = Path.Combine(root, "arena-settlement.xml");
        using var profile = new LocalProfile(path); Fund(profile, 10000);
        var service = new ArenaService(profile); Ok(service.Begin("settle")); Draft(service);
        var initial = profile.ArenaRun;
        var rules = DraftRules.Arena(); string pack = FairPackRules.NewestSet;
        foreach (string result in new[] { "Lost", "Conceded", "Abandoned" })
            Check(initial.SettleMatch(result, pack).Losses == 1 && initial.Losses == 0, result + " pure loss");
        Check(initial.SettleMatch("Won", pack).Wins == 1 && initial.Wins == 0, "pure win");
        Check(initial.SettleMatch("Tied", pack).Wins == 0 && initial.SettleMatch("Tied", pack).Losses == 0, "tie");
        Reject(() => initial.SettleMatch("bogus", pack), "bad match result");
        initial.Wins = 11;
        var finished = initial.SettleMatch("Won", pack);
        Check(finished.Wins == 12 && finished.Phase == ArenaPhase.RewardPending && finished.Reward.Gold == profile.GrindTier.Gold(1100), "12 win end");
        initial.Wins = 0; initial.Losses = 2; finished = initial.SettleMatch("Abandoned", pack);
        Check(finished.Losses == 3 && finished.Phase == ArenaPhase.RewardPending && finished.Reward.PackTypes.Count == 1, "3 loss end");
        // Profile-side integration API must credit terminal reward and receipt in that same commit.
        profile.CommitChange(next => { next.ArenaRun.Wins = 11; return true; });
        foreach (string stage in new[] { "write", "flush", "replace" }) {
            byte[] before = File.ReadAllBytes(path); int oldGold = profile.Gold;
            profile.CommitFailure = point => { if (point == stage) throw new IOException("Arena settle injected"); };
            try { profile.CommitChange(next => { LocalProfile.SettleArenaMatch(next, "Won"); return true; }); throw new Exception("expected failure"); }
            catch (IOException) { }
            finally { profile.CommitFailure = null; }
            Check(before.SequenceEqual(File.ReadAllBytes(path)) && profile.Gold == oldGold && profile.ArenaRun.Wins == 11, "terminal settlement failure");
        }
        int gold = profile.Gold; long dust = profile.DustBalance;
        profile.CommitChange(next => { LocalProfile.SettleArenaMatch(next, "Won"); return true; });
        Check(profile.ArenaRun.Phase == ArenaPhase.RewardPending && profile.Gold == gold + profile.ArenaRun.Reward.Gold
            && profile.DustBalance == dust + profile.ArenaRun.Reward.Dust && profile.FeatsSnapshot().ArenaReceipts.Count == 1, "terminal settlement atomic credits");
        Reject(() => profile.CommitChange(next => {
            next.ArenaRun.Retired = true; next.ArenaReceipts.Last().Retired = true; return true;
        }), "retired terminal run");
        Reject(() => profile.CommitChange(next => { LocalProfile.SettleArenaMatch(next, "Won"); return true; }), "double terminal settlement");
        Ok(service.AcknowledgeReward(service.View().RunId)); Ok(service.Begin("loss-settle")); Draft(service);
        profile.CommitChange(next => { next.ArenaRun.Losses = 2; return true; });
        profile.CommitChange(next => { LocalProfile.SettleArenaMatch(next, "Lost"); return true; });
        Check(profile.ArenaRun.Losses == 3 && profile.ArenaRun.Reward.Wins == 0 && profile.FeatsSnapshot().ArenaReceipts.Count == 2, "profile 3 losses pays once");
    }

    private static void Compatibility(string root)
    {
        string path = Path.Combine(root, "arena-compat.xml");
        using (var profile = new LocalProfile(path)) Fund(profile);
        var old = File.ReadAllBytes(path);
        Check(!File.ReadAllText(path).Contains("Arena"), "current profile omits empty Arena elements");
        var data = Read(path); Write(path, data); Check(old.SequenceEqual(File.ReadAllBytes(path)), "old XML round trip byte identical");
        using (var profile = new LocalProfile(path)) {
            Check(profile.ArenaRun == null, "current profile starts without a run");
            profile.CommitChange(next => true);
            Check(old.SequenceEqual(File.ReadAllBytes(path)), "current profile no-op commit byte identical");
            var service = new ArenaService(profile); Ok(service.Begin("tamper")); Ok(service.Choose(0, 0)); Ok(service.Choose(1, 0));
        }
        var valid = Read(path);
        foreach (Action<LocalProfileData> mutate in new Action<LocalProfileData>[] {
            next => next.ArenaRun.Wins = 13, next => next.ArenaRun.Deck[0] = "TLC_248",
            next => next.ArenaRun.Picks[0].Offers.Reverse(), next => next.ArenaRun.EntryGold = 0,
            next => next.ArenaRun.HeroOffers.Reverse(), next => next.ArenaEntryRequestIds.Clear(),
            next => next.ArenaRun.Phase = ArenaPhase.Ready,
        }) {
            Write(path, valid); var tampered = Read(path); mutate(tampered); Write(path, tampered);
            byte[] bytes = File.ReadAllBytes(path);
            Reject(() => { using var bad = new LocalProfile(path); }, "tampered run");
            Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "tamper rejection leaves file");
        }
    }

    private static void SaveVersion(string root)
    {
        string path = Path.Combine(root, "arena-v15-upgrade.xml");
        using (var profile = new LocalProfile(path)) {
            Fund(profile);
            Check(Read(path).Version == LocalProfile.SaveVersion, "fresh profiles write the current version");
            var service = new ArenaService(profile);
            Ok(service.Begin("v15-upgrade")); Ok(service.Choose(0, 1));
            Ok(service.Choose(1, 2)); Ok(service.Choose(2, 0));
        }
        var before = Read(path).ArenaRun;
        Check(before.Phase == ArenaPhase.Drafting && before.Picks.Count == 2 && Read(path).Version > 15,
            "any save holding Arena data must exceed V15, the last schema 0.2.x opens, so its preflight returns save-incompatible");
        string currentXml = File.ReadAllText(path);
        File.WriteAllText(path, currentXml.Replace("<Version>" + LocalProfile.SaveVersion + "</Version>", "<Version>15</Version>"));
        byte[] original = File.ReadAllBytes(path);
        Check(Read(path).Version == 15, "simulate an earlier Arena test build's V15 save");
        using (var profile = new LocalProfile(path)) {
            var run = profile.ArenaRun;
            Check(run.RunId == before.RunId && run.Phase == before.Phase && run.Picks.Count == before.Picks.Count
                && run.Picks.Zip(before.Picks, (a, b) => a.Index == b.Index && a.Offers.SequenceEqual(b.Offers)).All(same => same)
                && run.Deck.SequenceEqual(before.Deck) && run.PendingOffers.SequenceEqual(before.PendingOffers),
                "V15 upgrade keeps the run, phase, picks, deck and offer in progress");
        }
        Check(Read(path).Version == LocalProfile.SaveVersion && Read(path).Version > 15, "upgraded Arena save is current and 0.2.x preflight refuses it");
        Check(original.SequenceEqual(File.ReadAllBytes(path + ".v15.bak")), "V15 Arena upgrade keeps a byte-exact rollback backup");
        Check(File.ReadAllText(path) == currentXml, "V15 Arena upgrade changes only the version");
        byte[] upgraded = File.ReadAllBytes(path);
        using (var profile = new LocalProfile(path)) Check(profile.ArenaRun.RunId == before.RunId, "upgraded Arena run reopens");
        Check(upgraded.SequenceEqual(File.ReadAllBytes(path)), "second open leaves upgraded Arena bytes unchanged");
    }

    // The pre-bump Arena test build (566bc92, on the owner's Steam Deck and lane 0) wrote Arena data at V15. This fixture is that
    // build's own output (a synthetic save generated by running the 566bc92 code: one finished run with a win, a loss and a claimed
    // reward, a second run Ready at 1-0, three Arena matches). This build must open it with the run, matches, receipts and gold intact.
    private static void PreBumpFixture(string root)
    {
        string source = Path.Combine(AppContext.BaseDirectory, "profile-fixtures", "v15-arena-566bc92.xml");
        string path = Path.Combine(root, "arena-566bc92", "profile.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(source, path);
        byte[] original = File.ReadAllBytes(path);
        var before = Read(path);
        Check(before.Version == 15 && before.ArenaRun != null && before.Matches.Count(m => m.Mode == ArenaRules.Mode) == 3
            && before.ArenaReceipts.Count == 1 && before.ArenaEntryRequestIds.Count == 2, "fixture is the pre-bump V15 Arena save");
        using (var profile = new LocalProfile(path)) {
            var run = profile.ArenaRun;
            Check(run.RunId == "30e426b2-b08b-4489-9584-dddd439541b2" && run.Phase == ArenaPhase.Ready && run.Wins == 1 && run.Losses == 0
                && run.Deck.Count == 30 && run.Deck.SequenceEqual(before.ArenaRun.Deck), "the run in progress survives the upgrade");
            Check(profile.Gold == before.GoldBalance && profile.History.Count(m => m.Mode == ArenaRules.Mode) == 3, "gold and Arena matches survive");
            var view = new ArenaService(profile).View();
            Check(view.Error == null && view.Phase == ArenaPhase.Ready && view.Wins == 1 && view.Slot == 30, "the upgraded run is playable");
            profile.CommitChange(next => true);
        }
        var after = Read(path);
        Check(after.Version == LocalProfile.SaveVersion && after.ArenaReceipts.Count == 1 && after.ArenaEntryRequestIds.Count == 2,
            "upgraded to the current version with receipts and spent entry ids");
        Check(original.SequenceEqual(File.ReadAllBytes(path + ".v15.bak")), "byte-exact V15 backup for a rollback");
    }

    // Offers and rewards are stored history: a card-pool, rules or pace change after they were made must neither fail the load nor
    // reroll the pick in progress. Simulated by editing the saved record to values today's rules would not generate.
    private static void StoredHistory(string root)
    {
        string path = Path.Combine(root, "arena-stored.xml");
        string first, shown;
        using (var profile = new LocalProfile(path)) {
            Fund(profile); var service = new ArenaService(profile);
            Ok(service.Begin("old-run")); Ok(service.Retire()); Ok(service.AcknowledgeReward(service.View().RunId));
            Ok(service.Begin("live-run")); Ok(service.Choose(0, 0));
            for (int slot = 1; slot <= 5; slot++) Ok(service.Choose(slot, 1));
            first = profile.ArenaRun.Deck[0]; shown = string.Join(",", service.View().Offers);
            Check(service.View().Slot == 6 && service.View().Offers.Count == 3, "mid-draft");
        }
        string Other(Rarity rarity, IEnumerable<string> used) => Cards.All.First(c => c.Rarity == rarity && c.Collectible && !used.Contains(c.Id)).Id;
        var data = Read(path); var run = data.ArenaRun;
        run.PendingOffers[2] = Other(Cards.FromId(run.PendingOffers[2]).Rarity, run.PendingOffers.Concat(run.Deck));
        run.Picks[0].Offers[0] = Other(Rarity.LEGENDARY, run.Picks[0].Offers);   // an unchosen legendary the current pool would not have offered
        // A card the engine no longer knows, and a legendary in a later pick (a rarity fix in card data): neither may lock the save.
        run.Picks[0].Offers[2] = "GONE_ARENA_CARD"; run.Picks[1].Offers[0] = Other(Rarity.LEGENDARY, run.Picks[1].Offers);
        run.PendingOffers[0] = "GONE_ARENA_PENDING";
        run.HeroOffers[2] = run.HeroOffers[2] == "HERO_01" ? "HERO_02" : "HERO_01";
        if (run.HeroOffers.Contains(run.HeroOffers[2]) && run.HeroOffers.Count(h => h == run.HeroOffers[2]) > 1) run.HeroOffers[2] = "HERO_11";
        data.ArenaReceipts[0].Reward.Gold += 1; data.ArenaReceipts[0].Reward.Dust += 1;
        Write(path, data);
        string edited = string.Join(",", run.PendingOffers);
        using (var profile = new LocalProfile(path)) {
            var service = new ArenaService(profile);
            Check(string.Join(",", service.View().Offers) == edited && edited != shown, "stored offer is shown unchanged after the rules moved on");
            Ok(service.Choose(6, 2)); Check(profile.ArenaRun.Deck[5] == edited.Split(',')[2], "the pick takes the stored offer");
            Check(service.View().Slot == 7 && profile.FeatsSnapshot().ArenaReceipts[0].Reward.Gold > 0, "run continues, receipt loads");
        }
    }

    private static void Registry(string root)
    {
        using var profile = new LocalProfile(Path.Combine(root, "arena-registry.xml"));
        // Binding is explicit (NativeUIProbe.BindSave): the crafting ledger no longer sets Arena as a side effect.
        var service = new ArenaService(profile);
        ArenaServiceRegistry.Current = service;
        CraftingLedgerRegistry.Current = new LocalCraftingLedger(profile);
        CraftingLedgerRegistry.Current = null;
        Check(ReferenceEquals(ArenaServiceRegistry.Current, service), "ledger changes never rebind or clear Arena");
        ArenaServiceRegistry.Current = null;
    }

    private static void Bot()
    {
        double high = 0, low = 0;
        for (int seed = 0; seed < 100; seed++) {
            var best = DraftBot.Draft(seed, 1); var weaker = DraftBot.Draft(seed, 0);
            var repeat = DraftBot.Draft(seed, 1);
            Check(best.Deck.Count == 30 && weaker.Deck.Count == 30 && best.Deck.SequenceEqual(repeat.Deck)
                && best.Hero == repeat.Hero && best.Quality == repeat.Quality, "valid deterministic bot draft");
            LocalDecks.ValidateRunes(best.Deck.Select(Cards.FromId), LocalDecks.HeroClass(best.Hero));
            high += best.Quality; low += weaker.Quality;
        }
        Check(high > low, "skill 1 has higher mean quality");
        Console.WriteLine($"PASS Arena NPC draft quality: skill 1 {high/100:F4}, skill 0 {low/100:F4} (100 seeds)");
    }
}
