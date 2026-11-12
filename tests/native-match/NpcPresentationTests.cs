using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PegasusGame;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class NpcPresentationTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception("NpcPresentation: " + message); }
    public static void Run()
    {
        AnimationSettle();
        NpcPresentationPublicTests.Run();
        ReviewRegressions();
        PacketMapping();
        MockLimits();
        HiddenPublicBoundary();
        NativeTriggers();
        PurposeStreams();
        foreach (var profile in NpcCatalog.All)
        {
            ThinkingRuffle(profile);
            Policy(profile);
            CapabilityIsolation(profile);
            IdentityAndPackets(profile);
            ConcessionHistoryAndSettlement(profile);
            UnsafeDecisionsCannotConcede(profile);
        }
        PendingConcessionInterrupts();
        EarnedWellPlayed();
        AppliedKnobs();
        NativeSquelchAndName();
        NpcWorldPresentationTests.Run();
        NpcMemoryTests.Run();
        Console.WriteLine("PASS NPC presentation, identity, concession history and exactly-once rewards (all three profiles)");
    }
    internal static void AnimationSettle()
    {
        foreach (var trigger in new[] { NpcReaction.OwnBigTurn, NpcReaction.HumanBigTurn,
            NpcReaction.HumanMisplay, NpcReaction.ReplyToHumanEmote, NpcReaction.BotLethalFound })
        {
            double now = 0;
            var original = NpcCatalog.All[2]; var personality = original.Personality;
            var profile = new NpcProfile(original.Id, 1, original.DisplayName, original.DeckId, original.HeroSkin, 0,
                new Personality(personality.PreTurnMinSeconds, personality.PreTurnMaxSeconds,
                    personality.ActionMinSeconds, personality.ActionMaxSeconds,
                    new EmoteProfile(6, 0, new EmoteRule(trigger, "Thanks", 1, 2)),
                    personality.ReplyBlendBeta, personality.ConcessionWillingness, personality.ConcedeFromTurn),
                original.Budget, original.PlanId);
            var events = new List<double>();
            var presentation = new NpcPresentation(profile, new NpcPresentationSettings { Emotes = true },
                "settle", 2, () => now, () => false, e => { events.Add(now); return true; });
            presentation.Queue(trigger, 1);
            now = 3000; presentation.Tick(false, false);
            now = 3100; presentation.Tick(true, false);
            Check(events.Count == 0, trigger + " fired on first idle frame after animation");
            now = 3600; presentation.Tick(true, false);
            Check(events.Count == 0, trigger + " skipped natural beat");
            // A second animation interrupts the beat; require continuous idle again.
            now = 3650; presentation.Tick(false, false);
            now = 3700; presentation.Tick(true, false);
            now = 4299; presentation.Tick(true, false);
            Check(events.Count == 0, trigger + " did not restart beat after busy frame");
            for (; now <= 4901 && events.Count == 0; now++) presentation.Tick(true, false);
            Check(events.Count == 1 && events[0] >= 4300 && events[0] <= 4900,
                trigger + " reaction outside 0.6-1.2 seconds after settled animation");
            Check(presentation.WaitingForReaction, "next action can overlap emitted reaction");
            now += 1201;
            Check(!presentation.WaitingForReaction, "reaction blocks next action indefinitely");
            presentation.Queue(trigger, 2);
            presentation.Tick(true, true); now += 1300; presentation.Tick(true, true);
            Check(events.Count == (trigger == NpcReaction.ReplyToHumanEmote ? 2 : 1),
                "stale move reaction crossed human turn boundary");
        }
        var packets = new List<object>(); var ui = new List<UserUI>();
        var match = Match(NpcCatalog.All[2], packets, ui, true);
        double clock = 0; match.NpcClock = () => clock;
        Probe(match, NpcReaction.OwnBigTurn);
        match.SendChoices(packets.OfType<EntityChoices>().Last(p => p.PlayerId == 1).Id, new List<int>());
        EndHuman(match, packets);
        var pending = (NpcPresentation)typeof(NativeMatch).GetField("npcPresentation",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(match);
        pending.Queue(NpcReaction.OwnBigTurn, 12);
        match.NativeNpcIdle = () => false;
        clock = 16000; match.TickNpc();
        Check(!pending.WaitingForReaction && !ui.Any(p => p.HasEmote), "stale reaction blocked busy client indefinitely");
        pending.Queue(NpcReaction.OwnBigTurn, 13);
        var bot = match.Game.Player2;
        var offer = EngineEntity.FromCard(bot, SabberStoneCore.Model.Cards.FromId("CS2_231"), zone: bot.SetasideZone);
        bot.Choice = new SabberStoneCore.Model.Choice(bot) { ChoiceType = ChoiceType.GENERAL,
            ChoiceAction = SabberStoneCore.Model.ChoiceAction.HAND, Choices = new List<int> { offer.Id }, SourceId = bot.Hero.Id };
        match.TickNpc();
        Check(!pending.WaitingForReaction && !ui.Any(p => p.HasEmote), "reaction blocked pending bot choice");
        match.StopNpc();
        Console.WriteLine("PASS NPC reactions: continuous queue idle, randomized beat, busy reset, action spacing, stale expiry and choice interruption");
    }
    private static NativeMatch DeadMatch(NpcProfile profile, List<object> packets, List<UserUI> userUi)
    {
        var match = Match(profile, packets, userUi, false);
        match.SendChoices(packets.OfType<EntityChoices>().Last(p => p.PlayerId == 1).Id, new List<int>());
        match.Game.Process(EndTurnTask.Any(match.Game.Player1));
        match.Game.Turn = profile.Personality.ConcedeFromTurn;
        foreach (var card in match.Game.Player2.BoardZone.ToArray()) { match.Game.Player2.BoardZone.Remove(card); match.Game.Player2.SetasideZone.Add(card); }
        foreach (var card in match.Game.Player1.BoardZone.ToArray()) { match.Game.Player1.BoardZone.Remove(card); match.Game.Player1.SetasideZone.Add(card); }
        match.Game.Player2.Hero.Damage = match.Game.Player2.Hero.BaseHealth - 4;
        SabberStoneCore.Model.Entities.Entity.FromCard(match.Game.Player1, SabberStoneCore.Model.Cards.FromId("CS2_182"), zone: match.Game.Player1.BoardZone);
        match.MatchId = Enumerable.Range(0, 1000).Select(i => "native-concede-" + i).First(id =>
            MatchRecorder.PersonalityRoll(profile.Version, id, match.NpcPublicEventIndex, "concede") < profile.Personality.ConcessionWillingness);
        Check(NpcConcession.ProvesPublicBoardLethal(match.Game, 2), profile.Id + " dead fixture not proven");
        return match;
    }
    private static GoldenDecision End(NativeMatch match, string kind = "search", string stop = "Exhausted", string reason = null) =>
        new GoldenDecision(BotAction.From(EndTurnTask.Any(match.Game.Player2)), 0, 1, 1, stop, reason, null, kind);
    private static void UnsafeDecisionsCannotConcede(NpcProfile profile)
    {
        foreach (string cause in new[] { "timeout", "fallback", "clock-cut", "turn-nodes" })
        {
            var match = DeadMatch(profile, new List<object>(), new List<UserUI>());
            match.ActNpc(End(match, cause == "clock-cut" || cause == "turn-nodes" ? "search" : cause,
                cause == "clock-cut" ? "Clock" : "Forced", cause == "turn-nodes" ? cause : null));
            match.TickNpc();
            Check(!match.OpponentConceded && match.Game.State != State.COMPLETE && match.Game.CurrentPlayer == match.Game.Player1,
                "unsafe decision conceded: " + cause);
        }
    }
    private static void PendingConcessionInterrupts()
    {
        var packets = new List<object>();
        var match = DeadMatch(NpcCatalog.All[0], packets, new List<UserUI>());
        match.ActNpc(End(match));
        var human = match.Game.Player1;
        var offer = SabberStoneCore.Model.Entities.Entity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CS2_231"), zone: human.SetasideZone);
        human.Choice = new SabberStoneCore.Model.Choice(human) { ChoiceType = ChoiceType.GENERAL, ChoiceAction = SabberStoneCore.Model.ChoiceAction.HAND,
            Choices = new List<int> { offer.Id }, SourceId = human.Hero.Id };
        int cursor = packets.Count;
        match.TickNpc();
        Check(packets.Skip(cursor).OfType<EntityChoices>().Any(p => p.PlayerId == 1), "pending concession stranded human choice");
        match.SendChoices(packets.OfType<EntityChoices>().Last().Id, new List<int> { offer.Id });
        Check(match.NpcRunner?.Active == true && !match.OpponentConceded, "pending choice did not resume NPC");
        match.StopNpc();

        packets.Clear(); match = DeadMatch(NpcCatalog.All[0], packets, new List<UserUI>());
        match.ActNpc(End(match)); match.Game.Process(EndTurnTask.Any(match.Game.Player2));
        cursor = packets.Count; match.TickNpc();
        Check(packets.Skip(cursor).OfType<AllOptions>().Any() && !match.OpponentConceded,
            "pending concession stranded changed player");
    }
    private static void EarnedWellPlayed()
    {
        var packets = new List<object>(); var ui = new List<UserUI>();
        var match = Match(NpcCatalog.All[0], packets, ui, true);
        match.NativeNpcStartingTaunt = () => true;
        double now = 0; match.NpcClock = () => now;
        match.SendChoices(packets.OfType<EntityChoices>().Last(p => p.PlayerId == 1).Id, new List<int>());
        match.Game.Player2.Hero.Damage = match.Game.Player2.Hero.BaseHealth - 4;
        SabberStoneCore.Model.Entities.Entity.FromCard(match.Game.Player1, SabberStoneCore.Model.Cards.FromId("CS2_182"), zone: match.Game.Player1.BoardZone);
        var options = packets.OfType<AllOptions>().Last();
        match.SendOption(options.Id, options.Options.FindIndex(o => o.Type_ == PegasusGame.Option.Type.END_TURN), 0, -1, 0);
        match.TickNpc(); now = 1201; match.TickNpc();
        Check(ui.Any(p => p.HasEmote && p.Emote == (int)EmoteType.WELL_PLAYED), "public lethal did not earn Well Played");
        match.StopNpc();
    }
    private static void AppliedKnobs()
    {
        var queue = PlaytestQueue.Parse("npc=npc.golden.1\nemotes=on\nhover=on\nskin=on\ncardback=321\nturnNodes=7\nnativeStartingTaunt=off");
        var settings = queue.Presentation();
        Check(settings.Emotes && settings.Hover && settings.Skin && settings.CardBack && !settings.NativeStartingTaunt,
            "queue presentation/taunt settings lost");
        var header = PlaytestRecord.Header("knobs", queue.Profile, LocalDecks.All[0], LocalDecks.Get(queue.Profile.DeckId),
            1, "source", "pool", "build", queue, settings, 9);
        var applied = (Dictionary<string, object>)header["knobs"];
        Check((int)applied["turnNodes"] == 9 && (int)applied["cardback"] == 321
            && (string)applied["nativeStartingTaunt"] == "off", "header recorded requested instead of applied knobs");
        header = PlaytestRecord.Header("defaults", queue.Profile, LocalDecks.All[0], LocalDecks.Get(queue.Profile.DeckId),
            1, "source", "pool", "build", null);
        applied = (Dictionary<string, object>)header["knobs"];
        Check((int)applied["turnNodes"] == queue.Profile.Budget.TurnNodes && (string)applied["nativeStartingTaunt"] == "on",
            "default applied knobs absent");
    }
    private static void PurposeStreams()
    {
        var purposes = new[] { "greeting", "wellplayed", "wow", "threaten", "concede", "pause-pre", "pause-act", "hover" };
        foreach (string first in purposes)
        foreach (string second in purposes.Where(p => string.CompareOrdinal(first, p) < 0))
        {
            Check(MatchRecorder.PersonalityRoll(1, "streams", 42, first) != MatchRecorder.PersonalityRoll(1, "streams", 42, second), "purpose collision");
            int both = Enumerable.Range(0, 4096).Count(i => MatchRecorder.PersonalityRoll(1, "streams", i, first) < .5
                && MatchRecorder.PersonalityRoll(1, "streams", i, second) < .5);
            Check(both > 850 && both < 1200, "correlated personality streams");
        }
    }
    private static void ThinkingRuffle(NpcProfile profile)
    {
        foreach (var candidates in new[] { new[] { 999, 13, 13, 10, 11, 12 }, new[] { 999, 13, 13 }, new int[0] })
        {
            var runs = new List<KeyValuePair<double, int>>[2];
            for (int run = 0; run < runs.Length; run++)
            {
                double now = 0;
                var events = runs[run] = new List<KeyValuePair<double, int>>();
                var settings = new NpcPresentationSettings { Hover = true };
                var own = new HashSet<int> { 10, 11, 12, 13 };
                var presentation = new NpcPresentation(profile, settings, "ruffle", 2, () => now, () => false,
                    e => { events.Add(new KeyValuePair<double, int>(now, e.OverCard.Value)); return true; });
                for (; now <= 20000; now++) presentation.Hover(candidates, own, new[] { 999, 10, 11, 12 });
                Check(events.Select(e => e.Value).Distinct().Count() == 3, "long think did not cycle three cards");
                Check(events.All(e => own.Contains(e.Value)), "ruffle hovered foreign id");
                var expectedCards = candidates.Length == 0 ? new[] { 10, 11, 12 } : new[] { 13, 10, 11 };
                for (int i = 0; i < events.Count; i++)
                {
                    Check(events[i].Value == expectedCards[i % 3], "candidate order, distinctness or hand fallback lost");
                    if (i == 0) continue;
                    double dwell = events[i].Key - events[i - 1].Key;
                    double expected = 700 + 800 * MatchRecorder.PersonalityRoll(profile.Version, "ruffle", i - 1, "hover");
                    Check(dwell >= 700 && dwell <= 1500 && dwell >= expected && dwell < expected + 1,
                        "hover dwell outside deterministic 0.7-1.5 second range");
                }
                settings.Hover = false; int count = events.Count;
                now += 2000; presentation.Hover(candidates, own, new[] { 10, 11 });
                Check(events.Count == count, "disabled hover emitted packet");
                settings.Hover = true; presentation.Cancel(); count = events.Count;
                now += 2000; presentation.Hover(candidates, own, new[] { 10, 11 });
                Check(events.Count == count, "cancelled hover emitted packet");
            }
            Check(runs[0].SequenceEqual(runs[1]), "same match produced different hover sequence");
        }
        double time = 0;
        var focused = new List<int>();
        var focus = new NpcPresentation(profile, new NpcPresentationSettings { Hover = true }, "focus", 2,
            () => time, () => false, e => { focused.Add(e.OverCard.Value); return true; });
        var ids = new HashSet<int> { 10, 11 };
        focus.Hover(new[] { 10, 11 }, ids, new[] { 10, 11 });
        time = 600;
        Check(focus.ActedCardDwell(11, ids) == 300 && focused.Last() == 11,
            "thinking dwell blocked acted-card focus");
    }

    private static void Policy(NpcProfile profile)
    {
        double now = 0;
        bool squelched = false;
        var events = new List<NpcPresentationEvent>();
        var settings = new NpcPresentationSettings();
        var p = new NpcPresentation(profile, settings, "policy", 2, () => now, () => squelched,
            e => { events.Add(e); return true; });
        p.Queue(NpcReaction.MatchStart, 0);
        now = 10000; p.Tick(true, true);
        Check(events.Count == 0, "disabled emotes sent greeting");
        settings.Emotes = true;
        // Fixed streams and repeated public events exercise caps independently of chance.
        for (int i = 1; i < 100; i++)
        {
            p.Queue(NpcReaction.ReplyToHumanEmote, i, i % 2 == 0 ? "Greetings" : "WellPlayed");
            now += 1201;
            Check(!p.Tick(false, true), "emoted while human animation active");
            p.Tick(true, true); now += 1201; p.Tick(true, true);
            now += 30000;
        }
        Check(events.Count == 2, profile.Id + " replies not capped at two");
        now += 30000;
        squelched = true;
        p.Queue(NpcReaction.BotDoomed, 200);
        now += 1000; p.Tick(true, false);
        Check(events.Count == 2, "squelch bypassed");
        squelched = false;
        for (int i = 201; i < 400; i++)
        {
            p.Queue(NpcReaction.OwnBigTurn, i);
            p.Queue(NpcReaction.HumanBigTurn, i);
            p.Queue(NpcReaction.BotDoomed, i);
            now += 1201; p.Tick(true, false); now += 30000;
        }
        Check(events.Count <= profile.Personality.Emotes.Budget, "match budget exceeded");
        Check(events.All(e => e.PlayerId == 2), "wrong emote seat");
        int count = events.Count;
        p.Cancel(); p.Queue(NpcReaction.MatchStart, 999); now += 40000; p.Tick(true, false);
        Check(events.Count == count, "cancel emitted stale event");
        foreach (var rule in profile.Personality.Emotes.Rules)
        {
            bool fired = false;
            for (int seed = 0; seed < 100 && !fired; seed++)
            {
                var single = new NpcPresentation(profile, settings, "rule-" + seed, 2, () => now, () => false,
                    e => { fired = e.Emote == rule.Emote; return true; });
                single.Queue(rule.Trigger, 7, rule.HumanEmote ?? "BadTrade"); single.Tick(true, false); now += 1201;
                single.Tick(true, false);
            }
            Check(fired, profile.Id + " unreachable rule " + rule.Trigger + "/" + rule.Emote);
            var repeated = new List<string>();
            var limited = new NpcPresentation(profile, settings, "rule-0", 2, () => now, () => false,
                e => { repeated.Add(e.Emote); return true; });
            for (int i = 0; i < 100; i++)
            {
                limited.Queue(rule.Trigger, i, rule.HumanEmote ?? "BadTrade"); limited.Tick(true, false);
                now += 1201; limited.Tick(true, false); now += 30001;
            }
            Check(repeated.Count(e => e == rule.Emote) <= rule.MaxUses, "per-rule use limit exceeded");
        }
        var runs = new List<string>[2];
        for (int run = 0; run < 2; run++)
        {
            double time = 0, last = double.NegativeInfinity;
            var emitted = runs[run] = new List<string>();
            var full = new NpcPresentation(profile, settings, "budget", 2, () => time, () => false,
                e => {
                    Check(time - last >= profile.Personality.Emotes.MinGapMilliseconds, "profile minimum gap violated");
                    last = time; emitted.Add(e.Emote); return true;
                });
            for (int i = 0; i < 300; i++)
            {
                foreach (var trigger in new[] { NpcReaction.MatchStart, NpcReaction.ReplyToHumanEmote,
                    NpcReaction.OwnBigTurn, NpcReaction.HumanBigTurn, NpcReaction.BotDoomed, NpcReaction.HumanMisplay })
                {
                    full.Queue(trigger, i, trigger == NpcReaction.ReplyToHumanEmote ? i % 2 == 0 ? "Greetings" : "WellPlayed" : "BadTrade");
                    full.Tick(true, false); time += 1201; full.Tick(true, false);
                }
                time += 30000;
            }
            Check(emitted.Count == profile.Personality.Emotes.Budget, "profile budget not usable");
        }
        Check(runs[0].SequenceEqual(runs[1]), "same match/events produced different emotes");
        if (profile.Id == "npc.golden.3")
        {
            bool lateEmote = false;
            for (int seed = 0; seed < 20 && !lateEmote; seed++)
            {
                var slow = new NpcPresentation(profile, settings, "slow-" + seed, 2, () => now, () => false,
                    e => { lateEmote = true; return true; });
                slow.Queue(NpcReaction.OwnBigTurn, 7);
                now += 12000; slow.Tick(false, false); slow.Tick(true, false); now += 1201; slow.Tick(true, false);
            }
            Check(lateEmote, "long Battlecry animation swallowed every reaction");
        }
        var quiet = new NpcPresentation(profile, settings, "unhandled", 2, () => now, () => false, e => true);
        quiet.Queue(profile.Id == "npc.golden.3" ? NpcReaction.ReplyToHumanEmote : NpcReaction.OwnBigTurn, 1, "Sorry");
        Check(!quiet.WaitingForReaction, "unsupported trigger delayed bot action");
        var warm = new NpcPresentation(profile, settings, "rule-0", 2, () => now, () => false,
            e => { events.Add(e); return true; });
        count = events.Count;
        warm.Queue(NpcReaction.MatchStart, 0, delayMilliseconds: 3500); warm.Tick(true, true);
        now += 3499; Check(!warm.Tick(true, true), "greeting overlapped starting taunt");
        now += 1; warm.Tick(true, true);
        Check(events.Count == count + 1, profile.Id + " native taunt swallowed greeting");
        var slowStart = new NpcPresentation(profile, settings, "rule-0", 2, () => now, () => false,
            e => { events.Add(e); return true; });
        slowStart.Queue(NpcReaction.MatchStart, 0, delayMilliseconds: 3500);
        now += 12000; slowStart.Tick(false, true);
        slowStart.Tick(true, true); now += 3500; slowStart.Tick(true, true);
        Check(events.Count == count + 2, "slow native opening swallowed greeting");
        warm.Queue(NpcReaction.MatchStart, 1); now += 31000; warm.Tick(true, true);
        Check(events.Count == count + 2, "duplicate match greeting");
    }
    private static void CapabilityIsolation(NpcProfile profile)
    {
        foreach (int capability in new[] { 0, 1, 2, 3 })
        {
            var settings = new NpcPresentationSettings { Emotes = capability == 0, Hover = capability == 1,
                Skin = capability == 2, CardBack = capability == 3, CardBackOverride = 123 };
            var events = new List<NpcPresentationEvent>();
            var p = new NpcPresentation(profile, settings, "rule-0", 2, () => 0, () => false,
                e => { events.Add(e); return true; });
            p.Queue(NpcReaction.MatchStart, 0, delayMilliseconds: 0); p.Tick(true, true);
            p.Hover(new[] { 999, 10 }, new HashSet<int> { 10 });
            Check(events.Count == (capability < 2 ? 1 : 0), "capability enabled another transport");
            Check(p.HeroCardId(10, 10, "base") == (capability == 2 ? profile.HeroSkin : "base")
                && p.HeroCardId(11, 10, "human") == "human", "skin switch crossed entities");
            Check(p.CardBack(2, 7) == (capability == 3 ? 123 : 7) && p.CardBack(1, 7) == 7,
                "cardback switch crossed seats");
            settings.CardBackOverride = null;
            Check(p.CardBack(2, 7) == (capability == 3 ? profile.CardBack : 7), "profile cardback fallback missing");
        }
    }
    private static void PacketMapping()
    {
        var ui = new List<UserUI>();
        var match = Match(NpcCatalog.All[0], new List<object>(), ui, true);
        var values = new Dictionary<string, int> { { "Greetings", (int)EmoteType.GREETINGS },
            { "WellPlayed", (int)EmoteType.WELL_PLAYED }, { "Thanks", (int)EmoteType.THANKS },
            { "Wow", 33 }, { "Oops", (int)EmoteType.OOPS }, { "Sorry", (int)EmoteType.SORRY },
            { "Threaten", (int)EmoteType.THREATEN } };
        foreach (var pair in values)
        {
            Check(match.SendNpcPresentation(new NpcPresentationEvent(2, pair.Key)), "missing packet mapping " + pair.Key);
            Check(ui.Last().PlayerId == 2 && ui.Last().HasEmote && ui.Last().Emote == pair.Value, "wrong pinned mapping " + pair.Key);
            using (var bytes = new MemoryStream()) ui.Last().Serialize(bytes);
        }
        Check(NpcCatalog.All.SelectMany(p => p.Personality.Emotes.Rules).All(r => values.ContainsKey(r.Emote)), "profile has unmapped emote");
        int count = ui.Count;
        Check(!match.SendNpcPresentation(new NpcPresentationEvent(2, "invalid")) && ui.Count == count, "invalid emote sent");
        Console.WriteLine("PASS pinned emote values " + string.Join(",", values.Select(p => p.Key + "=" + p.Value)));
        match.StopNpc();
    }
    private static void ReviewRegressions()
    {
        var failures = new List<string>();
        foreach (Action test in new Action[] { NativeEmoteReplies, PresentationInputSafety, ProvenLethalPresentation, ConcedeDropsOrdinaryReactions })
        {
            try { test(); Console.WriteLine("PASS " + test.Method.Name); }
            catch (Exception error) { failures.Add(test.Method.Name + ": " + error.GetBaseException().Message); }
        }
        Check(failures.Count == 0, string.Join("; ", failures));
    }
    private static void NativeEmoteReplies()
    {
        // Exercise the numeric native-send boundary, including the non-ordinal WOW value.
        foreach (var pair in new Dictionary<int, string> { { 1, "Greetings" }, { 2, "WellPlayed" }, { 3, "Oops" },
            { 4, "Threaten" }, { 5, "Thanks" }, { 33, "Wow" }, { 6, "Sorry" }, { 7, null }, { -1, null } })
        {
            double now = 0;
            var packets = new List<object>(); var ui = new List<UserUI>();
            var match = Match(NpcCatalog.All[2], packets, ui, true);
            match.NpcClock = () => now;
            Probe(match, NpcReaction.ReplyToHumanEmote, pair.Value);
            match.SendChoices(packets.OfType<EntityChoices>().Last(p => p.PlayerId == 1).Id, new List<int>());
            if (pair.Key == 6) match.Game.Player1[SabberStoneCore.Actions.NativeTailRules.SorryEnabled] = 1;
            Check(match.ReceiveNativeEmote(pair.Key) == (pair.Value != null), "native send acceptance mismatch " + pair.Key);
            match.TickNpc(); now = 1201; match.TickNpc();
            Check(ui.Count(p => p.HasEmote) == (pair.Value == null ? 0 : 1), "native send mapping/reply missing: " + pair.Key);
            int count = ui.Count;
            Check(!match.ReceiveNativeEmote(pair.Key), "native send bypassed rate limit");
            now = 16000; match.TickNpc();
            Check(ui.Count == count, "native send bypassed human emote throttle");
            match.StopNpc();
        }
    }
    private static void PresentationInputSafety()
    {
        foreach (string interaction in new[] { "held card", "targeting", "sub-option", "choice" })
        {
            double now = 0; bool clear = true;
            var packets = new List<object>(); var ui = new List<UserUI>();
            var match = Match(NpcCatalog.All[2], packets, ui, true);
            match.NpcClock = () => now;
            Probe(match, NpcReaction.MatchStart);
            match.NativePresentationInputReady = () => clear;
            match.NativeHumanInputReady = () => true; // Native OPTION input remains enabled mid-drag.
            match.SendChoices(packets.OfType<EntityChoices>().Last(p => p.PlayerId == 1).Id, new List<int>());
            // Prime the delayed greeting while idle, then begin interaction before it is due.
            match.TickNpc(); clear = false; now = 1000; match.TickNpc();
            Check(!ui.Any(p => p.HasEmote), "greeting interrupted " + interaction);
            clear = true; match.TickNpc();
            Check(!ui.Any(p => p.HasEmote), "greeting skipped fresh beat after " + interaction);
            now += 1201; match.TickNpc();
            Check(ui.Count(p => p.HasEmote) == 1, "deferred greeting lost after " + interaction);
            match.StopNpc();
        }
    }
    private static void ProvenLethalPresentation()
    {
        foreach (string kind in new[] { "spell", "battlecry", "multi-action" })
        {
            double now = 0;
            var packets = new List<object>(); var ui = new List<UserUI>();
            var match = Match(NpcCatalog.All[2], packets, ui, true);
            match.NpcClock = () => now;
            Probe(match, NpcReaction.BotLethalFound);
            match.SendChoices(packets.OfType<EntityChoices>().Last(p => p.PlayerId == 1).Id, new List<int>());
            EndHuman(match, packets);
            var human = match.Game.Player1; var bot = match.Game.Player2;
            foreach (var card in bot.HandZone.ToArray()) { bot.HandZone.Remove(card); bot.SetasideZone.Add(card); }
            bot.BaseMana = 10; bot.UsedMana = 0;
            human.Hero.Damage = human.Hero.BaseHealth - (kind == "battlecry" ? 1 : kind == "multi-action" ? 12 : 6);
            string cardId = kind == "battlecry" ? "CS2_189" : "CS2_029"; // Elven Archer / Fireball.
            var first = EngineEntity.FromCard(bot, SabberStoneCore.Model.Cards.FromId(cardId), zone: bot.HandZone);
            if (kind == "multi-action") EngineEntity.FromCard(bot, SabberStoneCore.Model.Cards.FromId(cardId), zone: bot.HandZone);
            int health = human.Hero.Health; bool afterFirstAction = false;
            match.NativeNpcUserUI = packet => {
                ui.Add(packet);
                if (packet.HasEmote) afterFirstAction = human.Hero.Health < health && !bot.HandZone.Contains(first);
                return true;
            };
            for (int i = 0; i < 10000 && match.Game.State != State.COMPLETE && match.Game.CurrentPlayer == bot; i++)
            { now += 100; match.TickNpc(); }
            Check(match.Game.State == State.COMPLETE, "solver fixture did not finish " + kind + " lethal");
            Check(kind == "multi-action" ? afterFirstAction && ui.Count(p => p.HasEmote) == 1 : !ui.Any(p => p.HasEmote),
                "proven " + kind + " lethal reacted before resolving or after game end");
            match.StopNpc();
        }
    }
    private static void ConcedeDropsOrdinaryReactions()
    {
        foreach (bool botConcedes in new[] { false, true })
        foreach (bool farewell in new[] { false, true })
        {
            double now = 0;
            var packets = new List<object>(); var ui = new List<UserUI>();
            var match = botConcedes ? DeadMatch(NpcCatalog.All[0], packets, ui) : Match(NpcCatalog.All[2], packets, ui, true);
            match.PresentationSettings.Emotes = true; match.NpcClock = () => now;
            var original = NpcCatalog.All[2]; var p = original.Personality;
            var profile = new NpcProfile(original.Id, 1, original.DisplayName, original.DeckId, original.HeroSkin, 0,
                new Personality(0, 0, 0, 0, new EmoteProfile(6, 0,
                    new EmoteRule(NpcReaction.MatchStart, "Greetings", 1),
                    new EmoteRule(NpcReaction.ReplyToHumanEmote, "Thanks", 1),
                    new EmoteRule(NpcReaction.Concession, "WellPlayed", farewell ? 1 : 0)),
                    p.ReplyBlendBeta, p.ConcessionWillingness, p.ConcedeFromTurn), original.Budget, original.PlanId);
            var presentation = new NpcPresentation(profile, match.PresentationSettings, "farewell", 2,
                () => now, () => false, match.SendNpcPresentation);
            typeof(NativeMatch).GetField("npcPresentation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(match, presentation);
            presentation.Queue(NpcReaction.MatchStart, 0);
            presentation.Tick(true, false); // Start the greeting's idle delay.
            presentation.Queue(NpcReaction.ReplyToHumanEmote, 1, "Greetings");
            now = 1000;
            if (botConcedes) { match.ActNpc(End(match)); match.TickNpc(); }
            else match.Concede();
            now = 2000; match.TickNpc();
            Check(ui.Where(packet => packet.HasEmote).Select(packet => packet.Emote)
                .SequenceEqual(farewell ? new[] { 2 } : new int[0]), "concede flushed ordinary reactions (bot=" + botConcedes + ", farewell=" + farewell + ")");
            match.StopNpc();
        }
    }
    private static void MockLimits()
    {
        double now = 0;
        foreach (var profile in NpcCatalog.All)
        {
            var events = new List<string>();
            var p = new NpcPresentation(profile, new NpcPresentationSettings { Emotes = true }, "mocks", 2,
                () => now, () => false, e => { events.Add(e.Emote); return true; });
            foreach (string kind in new[] { "BadTrade", "IdleMana", "SelfHarm" })
            {
                for (int i = 0; i < 100; i++)
                {
                    now += 30001;
                    p.Queue(NpcReaction.HumanMisplay, events.Count * 1000 + i, kind);
                    p.Tick(true, false); now += 1201; p.Tick(true, false);
                }
                Check(events.Count == (profile.Id == "npc.golden.3" ? kind == "BadTrade" ? 1 : 2 : 0),
                    "mock cap/repeated kind/polite personality failed");
            }
        }
    }
    private static void HiddenPublicBoundary()
    {
        var packets = new List<object>();
        var match = Match(NpcCatalog.All[2], packets, new List<UserUI>(), false);
        match.SendChoices(packets.OfType<EntityChoices>().Last(p => p.PlayerId == 1).Id, new List<int>());
        match.Game.Player1.BaseMana = 6;
        EngineEntity.FromCard(match.Game.Player2, SabberStoneCore.Model.Cards.FromId("CS2_182"), zone: match.Game.Player2.BoardZone);
        var capture = typeof(NativeMatch).GetMethod("NpcPublicPosition", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var before = (NpcPublicPosition)capture.Invoke(match, null);
        foreach (var card in match.Game.Player1.HandZone.Concat(match.Game.Player1.DeckZone).ToArray())
            card.Card = SabberStoneCore.Model.Cards.FromId("CS2_231");
        var after = (NpcPublicPosition)capture.Invoke(match, null);
        Check(before.Value == after.Value && before.Mana == after.Mana && before.HandCount == after.HandCount
            && before.Own.Count == after.Own.Count && before.Enemy.Count == after.Enemy.Count, "hidden cards influenced public snapshot");
        var profile = NpcCatalog.All[2];
        var runs = new List<string>[2];
        for (int run = 0; run < 2; run++)
        {
            double now = 0;
            var emitted = runs[run] = new List<string>();
            var presentation = new NpcPresentation(profile, new NpcPresentationSettings { Emotes = true }, "hidden", 2,
                () => now, () => false, e => { emitted.Add(e.Emote); return true; });
            foreach (string kind in new NpcPublicTurn().Observe(8, NpcPublicAction.EndTurn, run == 0 ? before : after, run == 0 ? before : after))
                presentation.Queue(NpcReaction.HumanMisplay, 3, kind);
            presentation.Tick(true, false); now = 1201; presentation.Tick(true, false);
        }
        Check(runs[0].Count == 1 && runs[0].SequenceEqual(runs[1]), "hidden permutation changed emotes");
        match.StopNpc();
    }
    private static void ProcessPublic(NativeMatch match, PlayerTask task) =>
        typeof(NativeMatch).GetMethod("Process", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(match, new object[] { task });
    private static void Probe(NativeMatch match, NpcReaction trigger, string humanEmote = null)
    {
        var original = NpcCatalog.All[2];
        var p = original.Personality;
        var profile = new NpcProfile(original.Id, 1, original.DisplayName, original.DeckId, original.HeroSkin, 0,
            new Personality(p.PreTurnMinSeconds, p.PreTurnMaxSeconds, p.ActionMinSeconds, p.ActionMaxSeconds,
                new EmoteProfile(6, 15, new EmoteRule(trigger, "Thanks", 1, humanEmote: humanEmote)), p.ReplyBlendBeta, p.ConcessionWillingness, p.ConcedeFromTurn),
            original.Budget, original.PlanId);
        typeof(NativeMatch).GetField("npcPresentation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(match, new NpcPresentation(profile, match.PresentationSettings, "native-probe", 2,
                () => match.NpcClock(), () => match.EmotesSquelched, match.SendNpcPresentation));
    }
    // A forced NPC action must be one of the legal options (minion plays carry their board position); an unresolvable
    // action silently takes NpcFallback, which then tests fallback policy instead of the presentation trigger.
    private static PlayerTask LegalPlay(SabberStoneCore.Model.Entities.Controller bot, IPlayable source, ICharacter target = null) =>
        bot.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id && t.Target?.Id == target?.Id);
    private static void EndHuman(NativeMatch match, List<object> packets)
    {
        var options = packets.OfType<AllOptions>().Last();
        match.SendOption(options.Id, options.Options.FindIndex(o => o.Type_ == PegasusGame.Option.Type.END_TURN), 0, -1, 0);
    }
    private static void NativeTriggers()
    {
        foreach (string scenario in new[] { "BadTrade", "IdleMana", "SelfHarm", "HumanBigTurn", "OwnBigTurn", "Reply", "Lethal", "Concession", "DoubledBattlecry" })
        {
            double now = 0; bool idle = true;
            var packets = new List<object>(); var ui = new List<UserUI>();
            var match = Match(NpcCatalog.All[2], packets, ui, true);
            match.NpcClock = () => now; match.NativeNpcIdle = () => idle;
            var trigger = scenario == "Reply" ? NpcReaction.ReplyToHumanEmote : scenario == "Concession" ? NpcReaction.Concession
                : scenario == "Lethal" ? NpcReaction.BotLethalFound : scenario == "HumanBigTurn" ? NpcReaction.HumanBigTurn
                : scenario == "OwnBigTurn" || scenario == "DoubledBattlecry" ? NpcReaction.OwnBigTurn : NpcReaction.HumanMisplay;
            Probe(match, trigger);
            match.SendChoices(packets.OfType<EntityChoices>().Last(p => p.PlayerId == 1).Id, new List<int>());
            var human = match.Game.Player1; var bot = match.Game.Player2;
            if (scenario == "Reply")
            {
                Check(match.TryEmote(human.Id, NativeMatch.OfflineEmote.WellPlayed), "human emote rejected");
            }
            else if (scenario == "Concession") match.Concede();
            else
            {
                if (scenario == "BadTrade")
                {
                    var source = (Minion)EngineEntity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CS2_231"), zone: human.BoardZone);
                    source.IsExhausted = false;
                    var target = (Minion)EngineEntity.FromCard(bot, SabberStoneCore.Model.Cards.FromId("CS2_182"), zone: bot.BoardZone);
                    ProcessPublic(match, MinionAttackTask.Any(human, source, target));
                }
                if (scenario == "IdleMana")
                {
                    human.BaseMana = 6; human.UsedMana = 0;
                    EngineEntity.FromCard(bot, SabberStoneCore.Model.Cards.FromId("CS2_182"), zone: bot.BoardZone);
                }
                if (scenario == "SelfHarm")
                {
                    human.BaseMana = 10; human.UsedMana = 0;
                    EngineEntity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CS2_182"), zone: human.BoardZone);
                    EngineEntity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CS2_182"), zone: human.BoardZone);
                    var spell = EngineEntity.FromCard(human, SabberStoneCore.Model.Cards.FromId("EX1_312"), zone: human.HandZone);
                    ProcessPublic(match, PlayCardTask.Any(human, spell));
                }
                if (scenario == "HumanBigTurn")
                    EngineEntity.FromCard(human, SabberStoneCore.Model.Cards.FromId("CS2_182"), zone: human.BoardZone);
                EndHuman(match, packets);
                if (scenario == "OwnBigTurn")
                {
                    bot.BaseMana = 10; bot.UsedMana = 0;
                    var minion = EngineEntity.FromCard(bot, SabberStoneCore.Model.Cards.FromId("CS2_182"), zone: bot.HandZone);
                    match.ActNpc(new GoldenDecision(BotAction.From(LegalPlay(bot, minion)), 0, 0, 0, "Forced", null, null));
                }
                if (scenario == "DoubledBattlecry")
                {
                    bot.BaseMana = 10; bot.UsedMana = 0;
                    // The fifth minion is a 1/1 Battlecry: below the normal eight-point swing threshold.
                    for (int i = 0; i < 4; i++)
                    {
                        var wisp = EngineEntity.FromCard(bot, SabberStoneCore.Model.Cards.FromId("CS2_231"), zone: bot.HandZone);
                        ProcessPublic(match, PlayCardTask.Any(bot, wisp));
                    }
                    var position = (NpcPublicPosition)typeof(NativeMatch).GetMethod("NpcPublicPosition",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(match, null);
                    typeof(NativeMatch).GetField("npcTurnStartValue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .SetValue(match, position.Value);
                    var caller = EngineEntity.FromCard(bot, SabberStoneCore.Model.Cards.FromId("CS2_189"), zone: bot.HandZone);
                    match.ActNpc(new GoldenDecision(BotAction.From(LegalPlay(bot, caller, human.Hero)), 0, 0, 0, "Forced", null, null));
                }
                if (scenario == "Lethal")
                {
                    human.Hero.Damage = human.Hero.BaseHealth - 4;
                    var attacker = (Minion)EngineEntity.FromCard(bot, SabberStoneCore.Model.Cards.FromId("CS2_182"), zone: bot.BoardZone);
                    attacker.IsExhausted = false;
                    match.ActNpc(new GoldenDecision(BotAction.From(MinionAttackTask.Any(bot, attacker, human.Hero)), 0, 0, 0, "Forced", null, null));
                    Check(match.Game.State == State.COMPLETE && !ui.Any(p => p.HasEmote), "final attack emitted an unresolved reaction");
                }
            }
            if (scenario != "Lethal" && scenario != "Concession")
            {
                Check(!ui.Any(p => p.HasEmote), scenario + " reacted before settle delay");
                now = 601; idle = false; match.TickNpc();
                Check(!ui.Any(p => p.HasEmote), scenario + " reacted during native action");
                idle = true;
                if (scenario == "Reply")
                {
                    match.NativeHumanInputReady = () => false; match.TickNpc();
                    Check(!ui.Any(p => p.HasEmote), "reply interrupted human input");
                    match.NativeHumanInputReady = () => true;
                }
                match.TickNpc();
                Check(!ui.Any(p => p.HasEmote), scenario + " reacted on first idle frame");
                now += 1201; match.TickNpc();
            }
            Check(ui.Count(p => p.HasEmote && p.Emote == (int)EmoteType.THANKS) == (scenario == "Lethal" ? 0 : 1), "native trigger missing " + scenario);
            match.StopNpc();
        }
    }
    // Native squelch (EnemyEmoteHandler, loaded for local matches since 2026-10-25) silences the NPC only: the human's
    // own emotes still go out. Each NPC emote is logged (LOCAL_AI) as well as recorded; the opponent name is the NPC's.
    private static void NativeSquelchAndName()
    {
        var profile = NpcCatalog.All[1];
        var packets = new List<object>(); var userUi = new List<UserUI>(); var log = new List<string>();
        var match = Match(profile, packets, userUi, true);
        bool squelched = false;
        match.NativeNpcSquelched = () => squelched; match.BotTrace = log.Add;
        Check(match.OpponentName == profile.DisplayName && match.Game.Player2.Name == profile.DisplayName, "opponent name is the NPC name");
        Check(match.SendNpcPresentation(new NpcPresentationEvent(2, "Greetings")) && userUi.Count(u => u.HasEmote) == 1,
            "unsquelched NPC emote sent");
        Check(log.Any(l => l.Contains(" emote=Greetings ")), "NPC emote logged: " + string.Join(" | ", log));
        squelched = true;
        Check(!match.SendNpcPresentation(new NpcPresentationEvent(2, "WellPlayed")) && userUi.Count(u => u.HasEmote) == 1,
            "native squelch silences the NPC");
        Check(match.SendNpcPresentation(new NpcPresentationEvent(2, overCard: 0)), "squelch does not block hover");
        Check(match.TryEmote(match.Game.Player1.Id, NativeMatch.OfflineEmote.Greetings), "native squelch must not block the human's emote");
        match.NativeNpcSquelched = () => { throw new InvalidOperationException("probe"); };
        Check(match.SendNpcPresentation(new NpcPresentationEvent(2, "Thanks")), "a failing squelch probe never blocks presentation");
        match.StopNpc();
    }
    private static NativeMatch Match(NpcProfile profile, List<object> packets, List<UserUI> userUi, bool identity)
    {
        var deck = LocalDecks.Get(profile.DeckId);
        var match = new NativeMatch((id, packet) => packets.Add(packet), deck.Id, deck.Id, 8427) {
            MatchId = "native-presentation", NativeNpcStartingTaunt = () => false,
            PresentationSettings = new NpcPresentationSettings { Emotes = identity, Hover = identity, Skin = identity,
                CardBack = identity, CardBackOverride = 321 },
            NativeNpcUserUI = packet => { userUi.Add(packet); return true; }
        };
        match.Start(); return match;
    }
    private static void IdentityAndPackets(NpcProfile profile)
    {
        foreach (bool enabled in new[] { false, true })
        {
            var packets = new List<object>(); var userUi = new List<UserUI>();
            var match = Match(profile, packets, userUi, enabled);
            var history = packets.OfType<PowerHistory>().SelectMany(p => p.List).ToArray();
            var create = history.First(p => p.HasCreateGame).CreateGame;
            Check(create.Players.Single(p => p.Id == 2).CardBack == (enabled ? 321 : 0), "bot cardback mismatch");
            Check(create.Players.Single(p => p.Id == 1).CardBack == 0, "human cardback changed");
            var heroes = history.Where(p => p.HasFullEntity).Select(p => p.FullEntity).ToArray();
            Check(heroes.Last(e => e.Entity == match.Game.Player2.Hero.Id).Name == (enabled ? profile.HeroSkin : match.Game.Player2.Hero.Card.Id), "bot skin mismatch");
            Check(heroes.Last(e => e.Entity == match.Game.Player1.Hero.Id).Name == match.Game.Player1.Hero.Card.Id, "same-class human got bot skin");
            string power = match.Game.Player2.Hero.HeroPower.Card.Id;
            bool basePower = power == SabberStoneCore.Model.Cards.FromAssetId(match.Game.Player2.Hero.Card[GameTag.HERO_POWER]).Id;
            string expectedPower = enabled && basePower ? profile.HeroSkinPower : NativeMatch.TransportCardId(power);
            Check(heroes.Last(e => e.Entity == match.Game.Player2.Hero.HeroPower.Id).Name == expectedPower,
                profile.Id + " bot hero-power skin mismatch: " + power + ", expected " + expectedPower);
            Check(heroes.Last(e => e.Entity == match.Game.Player1.Hero.HeroPower.Id).Name
                == NativeMatch.TransportCardId(match.Game.Player1.Hero.HeroPower.Card.Id), "human hero-power skin changed");
            double now = 0;
            match.NpcClock = () => { now += .1; return now; };
            match.SendChoices(packets.OfType<EntityChoices>().Last(p => p.PlayerId == 1).Id, new List<int>());
            SabberStoneCore.Model.Entities.Entity.FromCard(match.Game.Player2, SabberStoneCore.Model.Cards.FromId("CS2_231"), zone: match.Game.Player2.HandZone);
            var options = packets.OfType<AllOptions>().Last();
            match.SendOption(options.Id, options.Options.FindIndex(o => o.Type_ == PegasusGame.Option.Type.END_TURN), 0, -1, 0);
            now += 2000; match.TickNpc(); match.TickNpc();
            Check(userUi.Count(p => p.HasMouseInfo) == (enabled ? 1 : 0), "capability switch hover mismatch");
            now += 4000; match.TickNpc();
            int hand = enabled ? userUi.Last(p => p.HasMouseInfo).MouseInfo.OverCard : 0;
            if (enabled)
            {
                Check(userUi.Any(p => p.HasEmote), "delayed match greeting missing");
                var hover = userUi.Last(p => p.HasMouseInfo);
                Check(hover.MouseInfo.OverCard == hand && hover.MouseInfo.HeldCard == 0
                    && !hover.HasEmote && userUi.All(p => p.PlayerId == 2), "hover packet leaked card or wrong seat");
                int count = packets.Count;
                match.StopNpc(); Check(userUi.Last(p => p.HasMouseInfo).MouseInfo.OverCard == 0 && packets.Count == count, "cancel failed to clear without revealing hand");
                foreach (var packet in userUi) using (var bytes = new MemoryStream()) packet.Serialize(bytes);
            }
            foreach (var packet in packets.OfType<IProtoBuf>()) using (var bytes = new MemoryStream()) packet.Serialize(bytes);
        }
    }
    private static void ConcessionHistoryAndSettlement(NpcProfile profile)
    {
        var packets = new List<object>(); var userUi = new List<UserUI>();
        var match = DeadMatch(profile, packets, userUi);
        int cursor = packets.Count;
        var decision = new GoldenDecision(BotAction.From(EndTurnTask.Any(match.Game.Player2)), 0, 1, 1, "Exhausted", null, null);
        match.ActNpc(decision);
        Check(match.Game.State != State.COMPLETE, "conceded before next safe frame");
        match.TickNpc();
        Check(match.Game.State == State.COMPLETE && match.OpponentConceded && !match.HumanConceded, "bot concession did not complete");
        var data = packets.Skip(cursor).OfType<PowerHistory>().SelectMany(p => p.List).ToArray();
        Check(data.Any(p => p.HasTagChange && p.TagChange.Entity == match.Game.Player2.Id
            && p.TagChange.Tag == (int)GameTag.PLAYSTATE && p.TagChange.Value == (int)PlayState.CONCEDED), "native bot CONCEDED history missing");
        Check(data.Any(p => p.HasTagChange && p.TagChange.Entity == match.Game.Player1.Id
            && p.TagChange.Tag == (int)GameTag.PLAYSTATE && p.TagChange.Value == (int)PlayState.WON), "native human WON history missing");
        Check(data.Any(p => p.HasTagChange && p.TagChange.Entity == match.Game.Id
            && p.TagChange.Tag == (int)GameTag.STATE && p.TagChange.Value == (int)State.COMPLETE), "native complete history missing");
        int settled = packets.Count; match.TickNpc(); Check(settled == packets.Count, "duplicate concession history");
        var outcome = MatchOutcome.From(match.Game, match.HumanConceded);
        string directory = Path.Combine(Path.GetTempPath(), "npc-presentation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "profile.xml");
            using (var local = new LocalProfile(path))
            {
                var record = local.Begin("mage", profile.DeckId, 8427);
                int gold = local.Gold; long dust = local.DustBalance;
                var result = local.Settle(record.Id, outcome.Result, outcome.Turns, outcome.Conceded, match.RewardFacts);
                Check(result.Result == "Won" && !result.Conceded && result.Gold + result.DustReward > 0 && local.Gold == gold + result.Gold
                    && local.DustBalance == dust + result.DustReward, "normal win rewards missing");
                byte[] bytes = File.ReadAllBytes(path);
                local.Settle(record.Id, outcome.Result, outcome.Turns, outcome.Conceded, match.RewardFacts);
                Check(bytes.SequenceEqual(File.ReadAllBytes(path)) && local.Gold == gold + result.Gold && local.DustBalance == dust + result.DustReward
                    && local.History.Count == 1, "reward paid twice");
            }
        }
        finally { Directory.Delete(directory, true); }
    }
}
