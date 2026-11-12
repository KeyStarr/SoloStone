using System;
using System.Linq;

internal static class NpcPresentationPublicTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception("Public emotes: " + message); }
    private static NpcPublicMinion M(int id, int attack, int health, bool plain = true) => new NpcPublicMinion(id, attack, health, plain);
    private static NpcPublicPosition P(NpcPublicMinion[] own, NpcPublicMinion[] enemy, int mana = 0, int hand = 3) =>
        new NpcPublicPosition(own, enemy, 30, 30, mana, hand);
    public static void Run()
    {
        var before = P(new[] { M(1, 2, 1) }, new[] { M(2, 5, 6) });
        var after = P(new NpcPublicMinion[0], new[] { M(2, 5, 4) });
        var detector = new NpcPublicTurn();
        detector.Observe(8, NpcPublicAction.Attack, before, after, 1, 2);
        Check(detector.Observe(8, NpcPublicAction.EndTurn, after, after).Single() == "BadTrade", "bad trade missed");
        detector = new NpcPublicTurn();
        var good = P(new[] { M(1, 2, 1) }, new[] { M(2, 1, 2) });
        var goodAfter = P(new NpcPublicMinion[0], new NpcPublicMinion[0]);
        detector.Observe(8, NpcPublicAction.Attack, good, goodAfter, 1, 2);
        Check(detector.Observe(8, NpcPublicAction.EndTurn, goodAfter, goodAfter).Length == 0, "good trade mocked");
        detector = new NpcPublicTurn();
        detector.Observe(8, NpcPublicAction.Attack, before, after, 1, 2);
        detector.Observe(8, NpcPublicAction.Other, after, goodAfter);
        Check(detector.Observe(8, NpcPublicAction.EndTurn, goodAfter, goodAfter).Length == 0, "setup trade mocked");
        var idle = P(new NpcPublicMinion[0], new[] { M(2, 5, 6) }, 4);
        Check(new NpcPublicTurn().Observe(8, NpcPublicAction.EndTurn, idle, idle).Single() == "IdleMana", "idle mana missed");
        var normal = P(new NpcPublicMinion[0], new[] { M(2, 5, 6) }, 3);
        Check(new NpcPublicTurn().Observe(8, NpcPublicAction.EndTurn, normal, normal).Length == 0, "normal turn mocked");
        var empty = P(new NpcPublicMinion[0], new[] { M(2, 5, 6) }, 8, 0);
        Check(new NpcPublicTurn().Observe(8, NpcPublicAction.EndTurn, empty, empty).Length == 0, "empty hand mocked");
        detector = new NpcPublicTurn();
        detector.Observe(8, NpcPublicAction.Other, idle, idle);
        Check(detector.Observe(8, NpcPublicAction.EndTurn, idle, idle).Length == 0, "active turn mocked");
        before = P(new[] { M(1, 4, 4), M(3, 4, 4) }, new[] { M(2, 5, 6) }, hand: 3);
        after = P(new NpcPublicMinion[0], new[] { M(2, 5, 6) }, hand: 2);
        detector = new NpcPublicTurn(); detector.Observe(8, NpcPublicAction.Spell, before, after);
        Check(detector.Observe(8, NpcPublicAction.EndTurn, after, after).Single() == "SelfHarm", "self destruction missed");
        var damaged = P(new[] { M(1, 4, 1), M(3, 4, 1) }, new[] { M(2, 5, 6) }, hand: 2);
        detector = new NpcPublicTurn(); detector.Observe(8, NpcPublicAction.Spell, before, damaged);
        Check(detector.Observe(8, NpcPublicAction.EndTurn, damaged, damaged).Single() == "SelfHarm", "severe friendly damage missed");
        var payoff = P(new NpcPublicMinion[0], new NpcPublicMinion[0], hand: 2);
        detector = new NpcPublicTurn(); detector.Observe(8, NpcPublicAction.Spell, before, payoff);
        Check(detector.Observe(8, NpcPublicAction.EndTurn, payoff, payoff).Length == 0, "board clear mocked");
        var draw = P(new NpcPublicMinion[0], new[] { M(2, 5, 6) }, hand: 5);
        detector = new NpcPublicTurn(); detector.Observe(8, NpcPublicAction.Spell, before, draw);
        Check(detector.Observe(8, NpcPublicAction.EndTurn, draw, draw).Length == 0, "sacrifice for draw mocked");
        before = P(new[] { M(1, 4, 4, false), M(3, 4, 4, false) }, new[] { M(2, 5, 6) });
        detector = new NpcPublicTurn(); detector.Observe(8, NpcPublicAction.Spell, before, after);
        Check(detector.Observe(8, NpcPublicAction.EndTurn, after, after).Length == 0, "deathrattle sacrifice mocked");
        Console.WriteLine("PASS public emote blunders and conservative trade/clear/draw false positives");
    }
}
