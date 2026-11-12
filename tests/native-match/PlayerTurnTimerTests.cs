using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    private static void PlayerTurnTimerTests()
    {
        NativeMatch.ClockFreeBot = true;
        PlayerTurnTimerPreArmActionTest();
        PlayerTurnTimerDeadlineRaces();
        PlayerTurnTimerTerminalRaces();
        var game = NativeTailGame();
        var capture = new Capture();
        bool ready = false;
        long now = 0;
        var match = new NativeMatch(capture.Send) { PlayerTurnTimerEnabled = true,
            NativeHumanInputReady = () => ready, TurnTimerClock = () => now };
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        int turn = game.Turn;
        match.TickTurnTimer(100000);
        Check(!capture.Packets.Any(p => p.Id == 9), "opening animations do not consume player clock");
        ready = true;
        match.TickTurnTimer(0);
        match.TickTurnTimer(24999);
        Check(!capture.Packets.Any(p => p.Id == 9), "opening 45-second timer has no early rope");
        ready = false;
        match.TickTurnTimer(25000);
        Check(capture.Last<PegasusGame.TurnTimer>(9).Seconds == 20, "native player rope starts with 20 seconds remaining even during animation");
        match.TickTurnTimer(44999);
        Check(game.Turn == turn, "player timeout cannot end early");
        match.TickTurnTimer(45000);
        Check(game.Turn > turn, "player timeout ends through real engine despite native animation");
        Check(capture.Packets.Any(p => p.Id == 9 && ((PegasusGame.TurnTimer)p.Message).Seconds == 0), "zero timer delivers native timeout cleanup contract (client cleanup requires live acceptance)");
        Check(capture.Packets.Where(p => p.Id == 19).SelectMany(p => ((PowerHistory)p.Message).List)
            .Any(e => e.TagChange?.Tag == (int)GameTag.TIMEOUT && e.TagChange.Value == 45), "player TIMEOUT tag published");
        capture.SerializeAll();

        // The next human turn after a fully idle timeout is short; a legal action restores the ordinary budget.
        ready = true; now = 50000;
        match.TickTurnTimer(now);
        Check(capture.Last<PegasusGame.TurnTimer>(9).Seconds == 7, "AFK next turn starts with short fuse");
        var card = Generic.DrawCard(game.Player1, Cards.FromId("CS2_182"));
        game.Player1.BaseMana = 10; game.Player1.UsedMana = 0;
        FlushFieldHistory(match);
        typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
        var options = capture.Last<AllOptions>(14);
        int play = options.Options.FindIndex(o => o.MainOption?.Id == card.Id);
        now = 51000;
        match.SendOption(options.Id, play, 0, -1, 1);
        Check(capture.Last<PegasusGame.TurnTimer>(9).Seconds == 74, "legal action restores full 75 seconds measured from turn start");
        turn = game.Turn;
        match.TickTurnTimer(124999);
        Check(game.Turn == turn, "restored budget does not expire early");
        match.TickTurnTimer(125000);
        Check(game.Turn > turn, "restored budget expires once");
        now = 130000; match.TickTurnTimer(now);
        match.TickTurnTimer(184999);
        Check(capture.Last<PegasusGame.TurnTimer>(9).Seconds != 7, "non-idle timeout does not trigger AFK penalty");

        game = NativeTailGame(); capture = new Capture();
        match = new NativeMatch(capture.Send) { NativeHumanInputReady = () => true };
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        turn = game.Turn;
        match.TickTurnTimer(0); match.TickTurnTimer(1000000);
        Check(game.Turn == turn && !capture.Packets.Any(p => p.Id == 9), "default player turn stays unbounded without timer packets");
        try { match.ShortenPlayerTurnForTest(5); throw new Exception("Expected disabled debug refusal"); }
        catch (InvalidOperationException) { }

        // Use the real native option/choice transport, not an invented pending Choice object.
        capture = new Capture(); now = 0;
        match = new NativeMatch(capture.Send, Cards.HeroCard(CardClass.ROGUE),
            Enumerable.Repeat(Cards.FromId("CS2_182"), 30), 1337) {
                PlayerTurnTimerEnabled = true, NativeHumanInputReady = () => true, TurnTimerClock = () => now };
        match.Start(); match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
        game = match.Game; game.Player1.BaseMana = 10;
        card = Generic.DrawCard(game.Player1, Cards.FromId("UNG_941"));
        FlushFieldHistory(match);
        typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
        match.TickTurnTimer(now);
        options = capture.Last<AllOptions>(14);
        play = options.Options.FindIndex(o => o.MainOption?.Id == card.Id);
        match.SendOption(options.Id, play, 0, -1, 0);
        var offer = capture.Last<EntityChoices>(17);
        Check(game.Player1.Choice != null && offer.CountMin == 1, "Discover pending on native path");
        match.ShortenPlayerTurnForTest(5);
        Check(capture.Last<PegasusGame.TurnTimer>(9).Seconds == 5, "debug hook uses same native countdown packet");
        turn = game.Turn; now = 5000;
        match.NativeHumanInputReady = () => false;
        match.TickTurnTimer(now);
        Check(game.Player1.Choice == null && game.Turn > turn, "timeout resolves pending Discover without waiting on native animation");
        var chosen = capture.Packets.Where(p => p.Id == 13).Select(p => (EntitiesChosen)p.Message)
            .Single(p => p.ChooseEntities.Id == offer.Id);
        Check(chosen.ChooseEntities.Entities.Count == 1 && offer.Entities.Contains(chosen.ChooseEntities.Entities[0]), "timeout acknowledges legal random Discover selection");
        int expectedPick = offer.Entities[new Random(match.Seed ^ 0x71a3).Next(offer.Entities.Count)];
        Check(chosen.ChooseEntities.Entities[0] == expectedPick, "Discover expiry samples the seeded random stream");
        turn = game.Turn; match.TickTurnTimer(now + 1);
        Check(game.Turn == turn, "expired turn cannot end next human turn before readiness");
        capture.SerializeAll();

        game = NativeTailGame(); capture = new Capture(); now = 0;
        match = new NativeMatch(capture.Send) { PlayerTurnTimerEnabled = true, TurnTimerClock = () => now,
            NativeHumanInputReady = () => false, NativeHumanTimerReady = () => true };
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        match.TickTurnTimer(now);
        now = 20000;
        game.Player1.TimeOut = 15;
        match.TickTurnTimer(now);
        Check(capture.Last<PegasusGame.TurnTimer>(9).Seconds == 15, "Nozdormu tag change resets native timer while menu blocks input");
        turn = game.Turn;
        match.TickTurnTimer(34999);
        Check(game.Turn == turn, "Nozdormu reset does not expire early");
        match.TickTurnTimer(35000);
        Check(game.Turn > turn, "Nozdormu reset expires while menu remains open");
        Console.WriteLine("PASS player turn timer: opening/normal budget, native rope, expiry, animation, AFK restore, default off, Discover, debug hook");
    }
    private static void PlayerTurnTimerPreArmActionTest()
    {
        var game = NativeTailGame();
        var capture = new Capture();
        long now = 0;
        var match = new NativeMatch(capture.Send) { PlayerTurnTimerEnabled = true,
            NativeHumanInputReady = () => false, NativeHumanTimerReady = () => false, TurnTimerClock = () => now };
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        game.Player1.BaseMana = 10;
        var card = Generic.DrawCard(game.Player1, Cards.FromId("CS2_182"));
        FlushFieldHistory(match);
        typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
        var options = capture.Last<AllOptions>(14);
        int play = options.Options.FindIndex(o => o.MainOption?.Id == card.Id);
        match.SendOption(options.Id, play, 0, -1, 1);
        int turn = game.Turn;
        match.TickTurnTimer(45000);
        Check(game.Turn > turn, "accepted pre-arm action starts timer despite blocked native readiness");
        match.NativeHumanTimerReady = () => true;
        now = 50000; match.TickTurnTimer(now);
        Check(!capture.Packets.Any(p => p.Id == 9 && ((PegasusGame.TurnTimer)p.Message).Seconds == 7),
            "accepted pre-arm action counts as activity and prevents AFK penalty");

        game = NativeTailGame(); game.Player1.BaseMana = 10;
        var discover = Generic.DrawCard(game.Player1, Cards.FromId("UNG_941"));
        Check(game.Process(PlayCardTask.Any(game.Player1, discover)), "pre-arm choice fixture plays through engine");
        (match, capture) = TimerFixture(game, () => now);
        match.NativeHumanInputReady = () => false; match.NativeHumanTimerReady = () => false;
        FlushFieldHistory(match);
        typeof(NativeMatch).GetMethod("SendHumanChoice", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
        var offer = capture.Last<EntityChoices>(17);
        match.SendChoices(offer.Id, new List<int> { offer.Entities[0] });
        turn = game.Turn;
        match.TickTurnTimer(now + 45000);
        Check(game.Turn > turn, "accepted pre-arm choice arms clock even while native readiness remains blocked");
        match.NativeHumanTimerReady = () => true;
        now += 50000; match.TickTurnTimer(now);
        Check(!capture.Packets.Any(p => p.Id == 9 && ((PegasusGame.TurnTimer)p.Message).Seconds == 7),
            "accepted pre-arm choice prevents AFK penalty");
    }

    private static (NativeMatch Match, Capture Capture) TimerFixture(SabberStoneCore.Model.Game game, Func<long> clock)
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send) { PlayerTurnTimerEnabled = true,
            NativeHumanInputReady = () => true, TurnTimerClock = clock };
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        game.Player1.BaseMana = 10;
        return (match, capture);
    }

    private static AllOptions TimerOptions(NativeMatch match, Capture capture)
    {
        FlushFieldHistory(match);
        typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
        return capture.Last<AllOptions>(14);
    }

    private static void ExpectTimerRejection(Action send, string name)
    {
        try { send(); }
        catch (InvalidOperationException error)
        {
            Check(error.GetType().Name == "TurnExpiredException", name + ": expected deadline rejection, got " + error.Message);
            return;
        }
        throw new Exception("Expected deadline rejection: " + name);
    }

    private static void PlayerTurnTimerDeadlineRaces()
    {
        foreach (bool heldSubOption in new[] { false, true })
        {
            long now = 0;
            var game = NativeTailGame();
            var (match, capture) = TimerFixture(game, () => now);
            var card = Generic.DrawCard(game.Player1, Cards.FromId(heldSubOption ? "EX1_160" : "CS2_029"));
            var options = TimerOptions(match, capture);
            int index = options.Options.FindIndex(o => o.MainOption?.Id == card.Id);
            int target = heldSubOption ? 0 : game.Player2.Hero.Id;
            int sub = heldSubOption ? 1 : -1;
            match.ValidateOption(options.Id, index, target, sub, 0); // A legal tuple held without submitting it.
            int turn = game.Turn;
            now = 45000;
            ExpectTimerRejection(() => match.SendOption(options.Id, index, target, sub, 0),
                heldSubOption ? "held Choose One loses at deadline" : "held targeting loses at deadline");
            Check(game.Turn > turn && game.Player1.HandZone.Contains(card), "expiry rejects held card without playing it");
            Check(capture.Packets.Count(p => p.Id == 9 && ((PegasusGame.TurnTimer)p.Message).Seconds == 0) == 1,
                "expiry publishes exactly one native cleanup packet");
            ExpectTimerRejection(() => match.SendOption(options.Id, index, target, sub, 0), "late held tuple remains deadline rejection");
            var recovery = Generic.DrawCard(game.Player1, Cards.FromId("CS2_182"));
            game.Player1.BaseMana = 10; game.Player1.UsedMana = 0;
            options = TimerOptions(match, capture);
            index = options.Options.FindIndex(o => o.MainOption?.Id == recovery.Id);
            now += 1;
            match.SendOption(options.Id, index, 0, -1, 1);
            Check(game.Player1.BoardZone.Contains(recovery), "next human turn accepts fresh native action after losing send");
            capture.SerializeAll();
        }

        foreach (bool expired in new[] { false, true })
        {
            var capture = new Capture(); long now = 0;
            var match = new NativeMatch(capture.Send, Cards.HeroCard(CardClass.ROGUE),
                Enumerable.Repeat(Cards.FromId("CS2_182"), 30), 1337) {
                    PlayerTurnTimerEnabled = true, NativeHumanInputReady = () => true, TurnTimerClock = () => now };
            match.Start(); match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            var game = match.Game; game.Player1.BaseMana = 10;
            var card = Generic.DrawCard(game.Player1, Cards.FromId("UNG_941"));
            var options = TimerOptions(match, capture);
            int index = options.Options.FindIndex(o => o.MainOption?.Id == card.Id);
            match.SendOption(options.Id, index, 0, -1, 0);
            var offer = capture.Last<EntityChoices>(17);
            match.ShortenPlayerTurnForTest(5);
            int turn = game.Turn;
            now = expired ? 5000 : 4999;
            var picks = new List<int> { offer.Entities.Last() };
            if (expired) ExpectTimerRejection(() => match.SendChoices(offer.Id, picks), "choice loses exactly at deadline");
            else match.SendChoices(offer.Id, picks);
            var acknowledgements = capture.Packets.Where(p => p.Id == 13).Select(p => (EntitiesChosen)p.Message)
                .Where(p => p.ChooseEntities.Id == offer.Id).ToArray();
            Check(acknowledgements.Length == 1 && (expired || acknowledgements[0].ChooseEntities.Entities.SequenceEqual(picks)),
                "choice race acknowledges exactly one accepted or automatic selection");
            Check(game.Player1.Choice == null && (expired ? game.Turn > turn : game.Turn == turn),
                "choice deadline determines whether player keeps turn");
            if (expired)
            {
                ExpectTimerRejection(() => match.SendChoices(offer.Id, picks), "late auto-resolved choice remains deadline rejection");
                options = TimerOptions(match, capture);
                index = options.Options.FindIndex(o => o.Type_ == PegasusGame.Option.Type.END_TURN);
                now++;
                match.SendOption(options.Id, index, 0, -1, 0);
                Check(game.Turn > turn + 2, "fresh end-turn works after choice deadline rejection");
            }
            capture.SerializeAll();
        }
    }

    private static void PlayerTurnTimerTerminalRaces()
    {
        foreach (bool expiryFirst in new[] { false, true })
        {
            long now = 0;
            var game = NativeTailGame();
            var (match, capture) = TimerFixture(game, () => now);
            TimerOptions(match, capture); match.TickTurnTimer(now);
            now = 45000;
            if (expiryFirst) match.TickTurnTimer(now);
            match.Concede();
            int turn = game.Turn, packets = capture.Packets.Count;
            match.TickTurnTimer(now); match.TickTurnTimer(now + 100000);
            Check(game.State == State.COMPLETE && match.HumanConceded && game.Turn == turn,
                "concede/expiry order reaches one terminal state");
            Check(!capture.Packets.Skip(packets).Any(p => p.Id == 14 || p.Id == 17 || p.Id == 19),
                "timer cannot process terminal game after concede");
        }
        foreach (bool expired in new[] { false, true })
        {
            long now = 0;
            var game = NativeTailGame();
            var (match, capture) = TimerFixture(game, () => now);
            var fireball = Generic.DrawCard(game.Player1, Cards.FromId("CS2_029"));
            game.Player2.Hero.Damage = game.Player2.Hero.Health - 6;
            var options = TimerOptions(match, capture);
            int index = options.Options.FindIndex(o => o.MainOption?.Id == fireball.Id);
            match.TickTurnTimer(now);
            now = expired ? 45000 : 44999;
            if (expired)
            {
                ExpectTimerRejection(() => match.SendOption(options.Id, index, game.Player2.Hero.Id, -1, 0), "lethal loses at deadline");
                Check(game.State != State.COMPLETE && game.Player1.HandZone.Contains(fireball), "expired lethal never executes");
            }
            else
            {
                match.SendOption(options.Id, index, game.Player2.Hero.Id, -1, 0);
                int turn = game.Turn;
                now++; match.TickTurnTimer(now);
                Check(game.State == State.COMPLETE && game.Player1.PlayState == PlayState.WON && game.Turn == turn,
                    "accepted lethal before deadline completes without timeout end-turn");
            }
        }
    }

}
