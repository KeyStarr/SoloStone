using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class NpcPresentationTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception("NpcPresentation: " + message); }

    public static void Run()
    {
        NpcPresentationPublicTests.Run();
        MockLimits();
        PurposeStreams();
        foreach (var profile in NpcCatalog.All)
        {
            ThinkingRuffle(profile);
            Policy(profile);
            CapabilityIsolation(profile);
        }
        AppliedKnobs();
        NpcWorldPresentationTests.Run();
        NpcMemoryTests.Run();
        Console.WriteLine("PASS NPC presentation knobs, streams, policy and capability isolation (all three profiles), world presentation and memory emotes");
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
}
