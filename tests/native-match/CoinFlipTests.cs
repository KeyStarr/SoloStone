using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PegasusGame;
using SabberStoneCore.Enums;

internal static partial class Program
{
    private static void CoinFlipTests()
    {
        int? previous = StableSeed.TestFirstPlayer;
        bool previousClock = NativeMatch.ClockFreeBot;
        NativeMatch.ClockFreeBot = true;
        StableSeed.TestFirstPlayer = null;
        try
        {
            int first = 0;
            for (int seed = 0; seed < 10000; seed++)
            {
                int seat = StableSeed.MatchFirstPlayer(seed);
                Check(seat == 1 || seat == 2, "seat is an engine player id");
                Check(seat == StableSeed.MatchFirstPlayer(seed), "same seed repeats seat");
                if (seat == 1) first++;
            }
            Check(first > 4700 && first < 5300, "seat roll is roughly 50/50: " + first + "/10000");
            Console.WriteLine("PASS seat distribution: human first " + first + "/10000");
            var reached = new HashSet<int>();
            for (int seed = 0; seed < 12; seed++)
            {
                var firstMatch = new NativeMatch((_, __) => { }, "mage", "priest", seed);
                var repeat = new NativeMatch((_, __) => { }, "mage", "priest", seed);
                firstMatch.Start(); repeat.Start();
                reached.Add(firstMatch.FirstPlayer);
                Check(firstMatch.FirstPlayer == StableSeed.MatchFirstPlayer(seed) && repeat.FirstPlayer == firstMatch.FirstPlayer,
                    "standalone NativeMatch.Start rolls and repeats its seeded seat");
            }
            Check(reached.SetEquals(new[] { 1, 2 }), "live match start reaches both seats");
            CoinFlipEngineSeats();
            CoinFlipNativeTurns();
            CoinFlipScenarioHand();
            CoinFlipOpeningTimer();
            CoinFlipOpeningBarrier();
            CoinFlipPersistence();
        }
        finally { StableSeed.TestFirstPlayer = previous; NativeMatch.ClockFreeBot = previousClock; }
        Console.WriteLine("PASS coin flip: seeded seats, native mulligan/bot turn 1/handoff/concede, pending/restart/history/legacy, launch-bound control");
    }

    private static void CoinFlipEngineSeats()
    {
        foreach (int rolled in new[] { 1, 2 })
        foreach (int ayaOwner in new[] { 0, 1, 2, 3 })
        {
            var a = Enumerable.Repeat(SabberStoneCore.Model.Cards.FromId("CS2_182"), 30).ToList();
            var b = a.ToList();
            if (ayaOwner == 1 || ayaOwner == 3) a[0] = SabberStoneCore.Model.Cards.FromId("JAIL_504");
            if (ayaOwner == 2 || ayaOwner == 3) b[0] = SabberStoneCore.Model.Cards.FromId("JAIL_504");
            var game = new SabberStoneCore.Model.Game(new SabberStoneCore.Config.GameConfig {
                Player1Deck = a, Player2Deck = b, Player1HeroClass = CardClass.ROGUE, Player2HeroClass = CardClass.ROGUE,
                StartPlayer = StableSeed.ApplyFirstPlayerRules(rolled, a.Select(c => c.Id), b.Select(c => c.Id)), RandomSeed = 4242, Shuffle = true, SkipMulligan = false, History = true
            });
            game.StartGame();
            int expected = ayaOwner == 0 || ayaOwner == 3 ? rolled : 3 - ayaOwner;
            Check(game.FirstPlayer.PlayerId == expected && game.FirstPlayer[GameTag.FIRST_PLAYER] == 1
                && game.FirstPlayer.Opponent[GameTag.FIRST_PLAYER] == 0,
                "exactly one FIRST_PLAYER tag after roll/card rules: roll=" + rolled + " Aya=" + ayaOwner);
            Check(game.FirstPlayer.HandZone.Count == 3 && game.FirstPlayer.Opponent.HandZone.Count == 5
                && game.FirstPlayer.Opponent.HandZone.Last().Card.Id == "GAME_005", "resolved second player gets fourth card and final Coin");
            var oldDecks = LocalDecks.ExternalDecks;
            try
            {
                var human = new LocalDeck("Coin human", "HERO_03", a.Select(c => c.Id).ToArray());
                var bot = new LocalDeck("Coin NPC", "HERO_03", b.Select(c => c.Id).ToArray());
                LocalDecks.ExternalDecks = id => id == "coin-human" ? human : id == "coin-npc" ? bot : oldDecks?.Invoke(id);
                var native = new NativeMatch((_, __) => { }, "coin-human", "coin-npc", 4242) { StartingPlayer = rolled };
                native.Start();
                Check(native.FirstPlayer == expected && native.Game.FirstPlayer.Opponent[GameTag.FIRST_PLAYER] == 0,
                    "real NativeMatch startup applies pinned card rule before tagging: roll=" + rolled + " Aya=" + ayaOwner);
            }
            finally { LocalDecks.ExternalDecks = oldDecks; }

        }
    }

    private sealed class CoinMulligan : MulliganPolicy
    {
        internal readonly List<MulliganView> Views = new List<MulliganView>();
        public override IReadOnlyList<int> Keep(MulliganView view)
        {
            Views.Add(view);
            return view.Offered.Where(c => c.CardId != "GAME_005").Select(c => c.EntityId).ToArray();
        }
    }

    private static void CoinFlipNativeTurns()
    {
        var oldHook = NpcWorldHooks.MulliganFor;
        var policy = new CoinMulligan();
        NpcWorldHooks.MulliganFor = (_, __) => policy;
        try
        {
            int[] shuffleRng = null;
            foreach (int seat in new[] { 1, 2 })
            {
                var capture = new Capture();
                double now = 0;
                bool idle = false;
                var writer = new StringWriter();
                var match = new NativeMatch(capture.Send, "mage", "npc-zee-shaman", 4242) {
                    StartingPlayer = seat, MatchId = "coin-" + seat, NpcTurnNodes = 1000, SynchronousNpc = false,
                    NpcClock = () => now, NativeNpcIdle = () => idle, NativeNpcStartingTaunt = () => false,
                    PresentationSettings = new NpcPresentationSettings { Emotes = false, Hover = false }
                };
                match.Recorder = new PlaytestRecord(writer, PlaytestRecord.Header(match.MatchId,
                    NpcCatalog.ForDeck("npc-zee-shaman"), LocalDecks.Get("mage"), LocalDecks.Get("npc-zee-shaman"),
                    match.Seed, "test", null, "test", null, firstPlayer: seat));
                match.Start();
                var game = match.Game;
                var rng = ((SabberStoneCore.Model.Util.DeepCloneableRandom)typeof(SabberStoneCore.Model.Game)
                    .GetProperty("Random", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(game)).Clone();
                var random = Enumerable.Range(0, 8).Select(_ => rng.Next()).ToArray();
                if (shuffleRng != null) Check(random.SequenceEqual(shuffleRng), "seat draw does not consume engine shuffle/match RNG");
                shuffleRng = random;
                var native = capture.HistorySnapshots().SelectMany(history => history.List).ToArray();
                Check(native.Any(row => row.HasTagChange && row.TagChange.Entity == game.FirstPlayer.Id
                        && row.TagChange.Tag == (int)GameTag.FIRST_PLAYER && row.TagChange.Value == 1)
                    || native.Where(row => row.HasCreateGame).SelectMany(row => row.CreateGame.Players)
                        .Any(player => player.Id == seat && player.Entity.Tags.Any(tag => tag.Name == (int)GameTag.FIRST_PLAYER && tag.Value == 1)),
                    "native wire publishes FIRST_PLAYER for coin-flip and turn banner");
                Check(match.FirstPlayer == seat && game.FirstPlayer.PlayerId == seat, "engine uses explicit seat");
                Check(game.FirstPlayer[GameTag.FIRST_PLAYER] == 1 && game.FirstPlayer.Opponent[GameTag.FIRST_PLAYER] == 0,
                    "FIRST_PLAYER tags select correct native coin-flip presentation");
                Check(policy.Views.Last().Coin == (seat == 1), "bot mulligan sees actual coin holder");
                Check(game.Player1.HandZone.Count(c => c.Card.Id != "GAME_005") == (seat == 1 ? 3 : 4), "human opening card count");
                Check(game.Player1.HandZone.Any(c => c.Card.Id == "GAME_005") == (seat == 2), "human coin ownership");
                Check(game.Player2.HandZone.Any(c => c.Card.Id == "GAME_005") == (seat == 1), "bot coin ownership");
                Check(match.EngineStartCount == 30 && !match.EngineStartCards.Split(',').Contains("GAME_005"), "coin is not a deck card in inventory audit");
                var humanChoice = capture.Last<EntityChoices>(17);
                Check(humanChoice.PlayerId == game.Player1.PlayerId, "human mulligan is last published choice in either seat");
                Check(humanChoice.Entities.All(id => game.IdEntityDic[id].Card.Id != "GAME_005")
                    && humanChoice.Entities.Count == (seat == 1 ? 3 : 4) && humanChoice.CountMax == humanChoice.Entities.Count,
                    "native human mulligan offers only replaceable cards so native starting actors and test-control holds agree");
                match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
                Check(game.CurrentPlayer.PlayerId == seat && game.Turn == 1, "main-begin hands turn 1 to first player");
                if (seat == 2)
                {
                    Check(match.HumanOptionsId == -1 && game.Player1.HandZone.Count == 5, "human waits with four cards plus Coin");
                    idle = true; match.SynchronousNpc = true;
                    for (int i = 0; game.CurrentPlayer == game.Player2 && i < 10000; i++) { now += 100; match.TickNpc(); }
                    Check(game.Turn == 2 && game.CurrentPlayer == game.Player1 && match.HumanOptionsId >= 0,
                        "bot takes turn 1 and publishes human first-turn options on turn 2");
                    var decisions = writer.ToString().Split('\n').Where(line => line.Contains("\"type\":\"decision\"")).ToArray();
                    Check(decisions.Length > 0 && decisions.Any(line => line.Contains("\"turn\":1")), "journal records bot decisions on turn 1");
                }
                idle = false; match.SynchronousNpc = false;
                var options = capture.Last<AllOptions>(14);
                match.SendOption(options.Id, options.Options.FindIndex(o => o.Type_ == PegasusGame.Option.Type.END_TURN), 0, -1, 0);
                Check(game.CurrentPlayer == game.Player2, "first human end-turn hands control back to bot in either seat");
                match.Concede();
                Check(game.State == State.COMPLETE && match.HumanConceded, "concede ends match even during NPC turn");
                using var header = JsonDocument.Parse(writer.ToString().Split('\n')[0]);
                Check(header.RootElement.GetProperty("firstPlayer").GetInt32() == seat, "journal header records seat");
                capture.SerializeAll();

                var early = new NativeMatch((_, __) => { }, "mage", "priest", 4242) { StartingPlayer = seat };
                early.Start(); early.Concede();
                Check(early.Game.State == State.COMPLETE && early.Game.Turn == 1, "turn-1 concede works for both seats");
            }
        }
        finally { NpcWorldHooks.MulliganFor = oldHook; }
    }

    private static void CoinFlipScenarioHand()
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, "mage", "npc-zee-shaman", 4242) {
            StartingPlayer = 2, DebugScenario = DebugScenario.Parse("humanHand=CS2_182,CS2_187"), SynchronousNpc = false
        };
        match.Start();
        var choices = capture.Last<EntityChoices>(17);
        match.SendChoices(choices.Id, choices.Entities.ToList());
        Check(match.Game.CurrentPlayer == match.Game.Player2 && match.Game.Player1.HandZone.Count == 7
            && match.Game.Player1.HandZone.Last().Card.Id == "GAME_005"
            && match.Game.Player1.HandZone.Skip(4).Take(2).Select(c => c.Card.Id).SequenceEqual(new[] { "CS2_182", "CS2_187" }),
            "human-second scenario keeps opening Coin last for retail coin animation while preserving staged card order");
        match.StopNpc();
    }

    private static void CoinFlipOpeningTimer()
    {
        bool ready = false;
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, "mage", "npc-zee-shaman", 4242) {
            StartingPlayer = 2, SynchronousNpc = false, NativeNpcTurnSynchronized = _ => ready, NativeHumanInputReady = () => ready
        };
        match.Start();
        match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
        match.Game.Player2.TimeOut = 15;
        match.TickTurnTimer(120000);
        Check(match.Game.CurrentPlayer == match.Game.Player2 && match.Game.Turn == 1,
            "opening NPC timer cannot expire while native mulligan/coin presentation is not synchronized");
        ready = true; match.TickTurnTimer(120000); match.TickTurnTimer(134999);
        Check(match.Game.CurrentPlayer == match.Game.Player2 && match.Game.Turn == 1,
            "opening NPC receives full shortened turn after native readiness");
        for (int i = 0; match.Game.Turn == 1 && i < 10; i++) match.TickTurnTimer(135000);
        Check(match.Game.CurrentPlayer == match.Game.Player1 && match.Game.Turn == 2,
            "opening NPC deadline hands off once after its full native-ready turn: turn=" + match.Game.Turn
            + " step=" + match.Game.Step + " humanChoice=" + match.Game.Player1.Choice?.ChoiceType
            + " npcChoice=" + match.Game.Player2.Choice?.ChoiceType);
        match.StopNpc();
    }

    private static void CoinFlipOpeningBarrier()
    {
        foreach (bool fallback in new[] { false, true })
        {
            double now = 0;
            bool synchronized = false, presentationReady = false, idle = false;
            var logs = new List<string>();
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, "mage", "npc-zee-shaman", 4242) {
                StartingPlayer = 2, SynchronousNpc = false, NpcTurnNodes = 1000, NpcClock = () => now,
                NativeNpcTurnSynchronized = _ => synchronized, NativeNpcIdle = () => idle,
                NativePresentationInputReady = () => presentationReady, BotTrace = logs.Add,
                PresentationSettings = new NpcPresentationSettings { Emotes = false, Hover = false }
            };
            match.Start();
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            int before = capture.HistorySnapshots().Count();
            for (int i = 0; i < 40; i++) { now += 1000; match.TickNpc(); }
            Check(match.NpcRunner == null && capture.HistorySnapshots().Count() == before && match.Game.Turn == 1,
                "opening actions cannot cross idle timeout or sync fallback bound while mulligan presentation remains active");
            idle = true;
            match.TickNpc();
            Check(match.NpcRunner == null, "idle alone cannot bypass opening presentation");
            if (!fallback) synchronized = true;
            else presentationReady = true;
            match.SynchronousNpc = true;
            for (int i = 0; match.Game.Turn == 1 && i < 100; i++) { now += 100; match.TickNpc(); }
            Check(match.Game.Turn == 2 && match.Game.CurrentPlayer == match.Game.Player1,
                "opening NPC starts and completes after native sync or safe bounded fallback");
            Check(logs.Count(line => line.Contains("opening-sync-timeout=")) == (fallback ? 1 : 0),
                "opening fallback logs once; synchronized start needs no fallback");
            match.StopNpc();
        }
        Console.WriteLine("PASS opening NPC barrier: mulligan blocks past timeout, native sync releases, settled-board fallback logs once");
    }

    private static void CoinFlipPersistence()
    {
        string root = Path.Combine(Path.GetTempPath(), "coin-flip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "profile.xml");
        try
        {
            int seed = Enumerable.Range(0, 100).First(s => StableSeed.MatchFirstPlayer(s) == 2);
            string id;
            using (var profile = new LocalProfile(path))
            {
                var pending = profile.Begin("mage", "priest", seed);
                id = pending.Id;
                Check(pending.FirstPlayer == 2 && profile.Pending.FirstPlayer == 2, "pending saves rolled seat");
            }
            Check(File.ReadAllText(path).Contains("<FirstPlayer>2</FirstPlayer>"), "seat is durable XML");
            StableSeed.TestFirstPlayer = 1; // A different new-match result must not replace the saved seat.
            using (var profile = new LocalProfile(path))
            {
                var pending = profile.Pending;
                var resumed = new NativeMatch((_, __) => { }, "mage", "priest", seed) { StartingPlayer = pending.FirstPlayer };
                resumed.Start();
                Check(resumed.FirstPlayer == 2, "restart uses pending seat without reroll");
                profile.Settle(id, "Lost", 1, true);
                Check(profile.History.Single().FirstPlayer == 2, "settled history keeps seat");
            }
            using (var profile = new LocalProfile(path)) Check(profile.History.Single().FirstPlayer == 2, "settled seat survives restart");
            File.WriteAllText(path, File.ReadAllText(path).Replace("<FirstPlayer>2</FirstPlayer>", ""));
            using (var profile = new LocalProfile(path))
            {
                Check(profile.History.Single().FirstPlayer == 1, "old settled record without field loads human-first");
                profile.AcknowledgeRewardPresentation(id);
                profile.ArmFirstPlayer(2);
                var pending = profile.Begin("mage", "priest", seed);
                Check(pending.FirstPlayer == 2, "one-shot forced seat stored in pending");
            }
            using (var profile = new LocalProfile(path))
            {
                profile.RecoverInterrupted();
                Check(profile.Pending == null && profile.History.Last().FirstPlayer == 2 && profile.History.Last().Result == "Abandoned",
                    "existing abandoned-match recovery retains seat");
            }
            using (var profile = new LocalProfile(path))
            {
                Check(profile.Begin("mage", "priest", seed).FirstPlayer == 1, "forced seat is consumed by one match only");
            }
            using (var profile = new LocalProfile(Path.Combine(root, "launch.xml")))
            {
                profile.BeginLaunch("rogue", "mage", seed, queued => {
                    Check(queued.FirstPlayer == 1, "ordinary launch persists its roll before the engine starts");
                    // The engine's card rules may resolve a different seat; MarkStarted must freeze that result.
                    profile.MarkStarted(queued.Id, 2);
                });
                Check(profile.Pending.FirstPlayer == 2 && !profile.Pending.Unstarted, "engine-start commit stores actual seat after card rules");
            }
            using (var profile = new LocalProfile(Path.Combine(root, "launch.xml")))
                Check(profile.Pending.FirstPlayer == 2, "actual engine-start seat survives restart");
            // Missing seat in a pending legacy match also means human-first, even when its seed would roll second.
            File.WriteAllText(path, File.ReadAllText(path).Replace("<FirstPlayer>2</FirstPlayer>", ""));
            using (var profile = new LocalProfile(path)) Check(profile.Pending.FirstPlayer == 1, "legacy pending defaults human-first");
        }
        finally { StableSeed.TestFirstPlayer = null; Directory.Delete(root, true); }
    }

}
