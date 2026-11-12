using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

// face-tiebreak. Synthetic distillation of a Deck playtest turn (2026-11-03): the NPC's own Critter Caretaker heals both
// heroes at end of turn and the enemy hero is at full health, so a ready 2/1's face hit is healed straight back. Attack
// and pass then tie exactly; Rank used to fall back to the shorter line and the NPC looked passive.
public static class FaceTieBreakTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object value, string field, object data) => value.GetType().GetField(field, Fields).SetValue(value, data);
    private static T Get<T>(object value, string field) => (T)value.GetType().GetField(field, Fields).GetValue(value);
    private const long Penalty = 1000000;

    private static Game Position(bool caretaker, CardClass own = CardClass.WARLOCK, CardClass enemy = CardClass.PRIEST, int usedMana = 10)
    {
        var g = Fixture(own, enemy);
        g.Player1.UsedMana = usedMana; g.Player1.Hero.Damage = 6;
        Board(g, true, "CATA_725", true);
        if (caretaker) Board(g, true, "EDR_971");
        return g;
    }
    // A (2/1) dies killing a 1/2; B (the Disciple) stays ready. Everything B does to the face is healed back.
    private static Game TradePosition(out Minion a, out Minion blocker, bool shield = false)
    {
        var g = Position(true);
        a = Board(g, true, "CS2_168", true); a.HasDivineShield = shield;
        blocker = Board(g, false, "CS2_231"); blocker.AttackDamage = 1; blocker[GameTag.HEALTH] = 2;
        return g;
    }
    private static NpcProfile Profile(bool on, int replyL = -1)
    {
        var p = NpcCatalog.All[0].WithFixes(new AiFixes(on ? new string[0] : new[] { "face-tiebreak" }));
        if (replyL < 0) return p;
        var b = p.Budget;
        return new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin, p.CardBack, p.Personality,
            new Budget(b.TurnNodes, b.PlanNodes, b.LethalNodes, b.ReplyK, replyL, b.ReplyR, b.OwnBeamWidth, b.Dedupe, 45, 55, b.Brain),
            p.PlanId, p.HeroSkinPower, p.Fixes);
    }
    private static PlannedTurn Plan(Game g, bool on, int replyL = -1, BotAction first = null)
    {
        var job = TurnPlanner.Job(View(g), Profile(on, replyL), DeckPlans.DragonWarrior, 71, 10000, firstAction: first);
        Run(job); return job.Result;
    }
    private static bool Face(Game g, BotAction a) => a.Type == PlayerTaskType.MINION_ATTACK && a.Target == g.Player2.Hero.Id;
    private static string Text(IEnumerable<BotAction> line) => string.Join(";", line);
    private static TurnBeam Beam(FairBotState root, int width)
    {
        var beam = new TurnBeam(root, s => 0, 100000, width, false, faceTieBreak: true);
        while (!beam.IsDone) beam.StepNode();
        return beam;
    }
    private static long Key(TurnBeam beam, params BotAction[] line) =>
        beam.Leaves.First(n => n.Key == string.Join(";", line.Select(a => a.ToString()))).TurnFace;

    public static void TestHealedFaceHitWinsExactTie()
    {
        var g = Position(true);
        var on = Plan(g, true); var off = Plan(g, false);
        Check(Face(g, on.Line[0]), "ON hits face on an exact tie: " + Text(on.Line));
        Check(off.Line[0].Type == PlayerTaskType.END_TURN, "OFF keeps the shorter END_TURN line: " + Text(off.Line));
        Check(on.Value == off.Value, "tie-break never changes the value: " + on.Value + " vs " + off.Value);
        double attack = on.Candidates.Single(p => Face(g, p.Key)).Value, pass = on.Candidates.Single(p => p.Key.Type == PlayerTaskType.END_TURN).Value;
        Check(attack == pass, "fixture premise: the healed face hit ties the pass exactly (" + attack + " vs " + pass + ")");
    }

    public static void TestUnhealedFaceHitUnchanged()
    {
        var g = Position(false);
        var on = Plan(g, true); var off = Plan(g, false);
        Check(Face(g, on.Line[0]) && on.Line.SequenceEqual(off.Line) && on.Value == off.Value,
            "a face hit that keeps its value is chosen with the flag ON and OFF alike");
    }

    // Live decisions, the reused plan step and a fresh plan from the same state all agree.
    public static void TestPilotAttacksThenReusedAndFreshAgree()
    {
        var g = Position(true);
        var pilot = new GoldenPilot(Profile(true));
        var first = pilot.Job(View(g), new GoldenContext(g.Turn, 1, 71, pilot.Profile)); Run(first);
        Check(Face(g, first.Result.Action), "pilot attacks face: " + first.Result.Action);
        g.Process(first.Result.Action.Resolve(g.Player1.Options()));
        var second = pilot.Job(View(g), new GoldenContext(g.Turn, 2, 71, pilot.Profile)); Run(second);
        Check(second.Result.Action.Type == PlayerTaskType.END_TURN && second.Result.SearchInfo.Reused,
            "next step reuses the planned END_TURN: " + second.Result.Action + " reused=" + second.Result.SearchInfo.Reused);
        var fresh = Plan(g, true);
        Check(fresh.Line[0].Equals(second.Result.Action), "fresh plan agrees with the reused step: " + Text(fresh.Line));
    }

    // Review finding: the baseline is the decision root. [A trades, B face] and [A trades] both lose A, and must keep their
    // order so the cached plan does the same as a fresh plan from the post-trade state.
    public static void TestLostMinionKeepsFaceContinuationReusedAndFresh()
    {
        var g = TradePosition(out var a, out var blocker);
        var root = View(g);
        var trade = root.Actions.Single(x => x.Source == a.Id && x.Target == blocker.Id);
        var on = Plan(g, true, first: trade); var off = Plan(g, false, first: trade);
        Check(on.Value == off.Value, "value unchanged: " + on.Value + " vs " + off.Value);
        Check(on.Line.Count == 3 && on.Line[0].Equals(trade) && Face(g, on.Line[1]) && on.Line[2].Type == PlayerTaskType.END_TURN,
            "ON keeps the face continuation after the trade: " + Text(on.Line));
        Check(off.Line.Count == 2 && off.Line[1].Type == PlayerTaskType.END_TURN, "OFF ends after the trade: " + Text(off.Line));

        var pilot = new GoldenPilot(Profile(true));
        pilot.Job(root, new GoldenContext(g.Turn, 1, 71, pilot.Profile));
        pilot.Accept(root, on.Line, 0, LethalOutcome.None, interrupted: false);
        g.Process(trade.Resolve(g.Player1.Options()));
        Check(!g.Player1.BoardZone.Any(m => m.Id == a.Id), "fixture: A died in the trade");
        var reused = pilot.Job(View(g), new GoldenContext(g.Turn, 2, 71, pilot.Profile)); Run(reused);
        var fresh = Plan(g, true);
        Check(reused.Result.SearchInfo.Reused && Face(g, reused.Result.Action), "reused step attacks: " + reused.Result.Action);
        Check(fresh.Line[0].Equals(reused.Result.Action), "fresh plan from the post-trade root agrees: " + Text(fresh.Line));
    }

    public static void TestLossAndArmorAndShieldKeys()
    {
        var g = TradePosition(out var a, out var blocker);
        var root = View(g); var beam = Beam(root, 64);
        var trade = root.Actions.Single(x => x.Source == a.Id && x.Target == blocker.Id);
        var hit = root.Actions.Single(x => Face(g, x) && x.Source != a.Id);
        var afterTrade = root.Apply(trade); var hitAfter = afterTrade.Actions.Single(x => x.Source == hit.Source && x.Target == g.Player2.Hero.Id);
        Check(Key(beam, hit) == 2, "plain face hit counts its damage");
        Check(Key(beam, trade) == -Penalty, "a lost root minion is a dominant penalty");
        Check(Key(beam, trade, hitAfter) == 2 - Penalty, "a loss keeps the continuation's face order");
        Check(Key(beam, hit) > Key(beam, trade, hitAfter) && beam.Leaves.First(n => n.Actions.Count == 0).TurnFace == 0, "no loss beats any lost line");

        // Divine Shield: the attacker survives, so the trade is no loss.
        g = TradePosition(out a, out blocker, shield: true);
        root = View(g); beam = Beam(root, 64);
        trade = root.Actions.Single(x => x.Source == a.Id && x.Target == blocker.Id);
        Check(Key(beam, trade) == 0, "a shielded attacker is not lost");

        // Armor is life: the hit lands on it and counts.
        g = Position(true); g.Player2.Hero.Armor = 5;
        root = View(g); beam = Beam(root, 64);
        Check(Key(beam, root.Actions.Single(x => Face(g, x))) == 2, "a hit on armor counts");

        // Immune: the hero is not attackable, so there is no face line to prefer.
        g = Position(true); g.Player2.Hero.IsImmune = true;
        Check(!View(g).Actions.Any(x => Face(g, x)), "fixture: an immune hero is no attack target");
        var on = Plan(g, true); var off = Plan(g, false);
        Check(on.Line.SequenceEqual(off.Line) && on.Value == off.Value, "immune: ON equals OFF: " + Text(on.Line) + " vs " + Text(off.Line));
        Check(on.Line[0].Type == PlayerTaskType.END_TURN, "immune: no pointless swing: " + Text(on.Line));
    }

    public static void TestEnemySecretRemovesFaceTerm()
    {
        var g = Position(true, enemy: CardClass.MAGE);
        Entity.FromCard(g.Player2, Cards.FromId("EX1_287"), zone: g.Player2.SecretZone);
        var root = View(g);
        Check(root.Facts.OpponentSecretCount == 1, "fixture: one hidden Secret");
        var beam = Beam(root, 64);
        Check(Key(beam, root.Actions.Single(x => Face(g, x))) == 0, "a hit into a Secret earns no tie-break");
        var on = Plan(g, true); var off = Plan(g, false);
        Check(on.Line.SequenceEqual(off.Line) && on.Value == off.Value, "Secret: ON equals OFF: " + Text(on.Line) + " vs " + Text(off.Line));
    }

    // Site: beam frontier. Width 1 keeps one of two tied prefixes; without the key "HERO_POWER" sorts first by Key.
    public static void TestNarrowBeamKeepsFacePrefix()
    {
        var g = Position(false, CardClass.WARRIOR, usedMana: 8);
        var root = View(g); var beam = Beam(root, 1);
        var best = TurnLine.Rank(beam.ScoredLeaves).First();
        Check(Face(g, best.Actions[0]), "tied frontier keeps the face prefix: " + best.Key);
    }

    // Site: finalist sort. One reply slot prices the leader only.
    public static void TestSingleReplySlotPricesFaceLeader()
    {
        var g = Position(true);
        var on = Plan(g, true, replyL: 1); var off = Plan(g, false, replyL: 1);
        Check(Face(g, on.Line[0]), "the face hit takes the only reply slot: " + Text(on.Line));
        Check(off.Line[0].Type == PlayerTaskType.END_TURN, "OFF prices the pass: " + Text(off.Line));
        Check(on.Value == off.Value, "same value: " + on.Value + " vs " + off.Value);
    }

    private static List<TurnLine> Priced(Game g)
    {
        var priced = new List<TurnLine>();
        var job = TurnPlanner.Job(View(g), Profile(true), DeckPlans.DragonWarrior, 71, 10000);
        job.ObserveLeaf = (line, phase) => { if (phase == "reply") priced.Add(line); };
        Run(job); return priced;
    }
    // Review finding: a static tie must not cost the pass its reply. With three other commitments the kept-hand slot stays
    // the pass; ordered by the tie-break it would flip to the face hit and the pass would never be priced.
    public static void TestPlayableHandKeepsPassAsPricedHold()
    {
        var g = Position(true, usedMana: 0); g.Player1.Hero.HeroPower.IsExhausted = true;
        foreach (string id in new[] { "CS2_231", "CS2_120", "CS2_119" }) Entity.FromCard(g.Player1, Cards.FromId(id), zone: g.Player1.HandZone);
        var priced = Priced(g);
        Check(priced.Any(n => n.Actions.Count == 0), "the pass is priced: " + string.Join(" | ", priced.Select(n => n.Key)));
    }
    // A full, unplayable hand: leader and hold are the tied pair, both priced, and Rank decides a proven tie.
    public static void TestKeptHandPricesFaceAndPass()
    {
        var g = Position(true);
        foreach (string id in new[] { "CS2_231", "CS2_120", "CS2_119" }) {
            var c = Entity.FromCard(g.Player1, Cards.FromId(id), zone: g.Player1.HandZone); c.Cost = 5;
        }
        var priced = Priced(g);
        var pass = priced.SingleOrDefault(n => n.Actions.Count == 0);
        var face = priced.SingleOrDefault(n => n.Actions.Count == 1 && Face(g, n.Actions[0]));
        Check(pass != null && face != null, "both are priced: " + string.Join(" | ", priced.Select(n => n.Key)));
        Check(pass.Value == face.Value && TurnLine.Rank(priced).First() == face, "a true priced tie goes to the face hit (" + pass.Value + " vs " + face.Value + ")");
    }

    // Commit-useful-plays extension: same trigger as OFF, and the tied face variant is the one priced next.
    private static TurnPlannerJob Extension(Game g, out TurnLine hold, out TurnLine kept, out TurnLine plain, out TurnLine hit)
    {
        var root = View(g); var job = TurnPlanner.Job(root, Profile(true), DeckPlans.DragonWarrior, 71, 100000);
        var play = root.Actions.First(a => a.Type == PlayerTaskType.PLAY_CARD);
        var attack = root.Actions.Single(a => Face(g, a));
        hold = new TurnLine { State = root, Value = 1 };
        kept = new TurnLine { State = root.Apply(attack), Value = 1, TurnFace = 2 }; kept.Actions.Add(attack);
        plain = new TurnLine { State = root.Apply(play), Value = 5 }; plain.Actions.Add(play);
        hit = new TurnLine { State = root.Apply(play).Apply(attack), Value = 5, TurnFace = 2 }; hit.Actions.Add(play); hit.Actions.Add(attack);
        var beam = Get<TurnBeam>(job, "beam");
        beam.Leaves.Add(plain); beam.Leaves.Add(hit); Set(beam, "<IsDone>k__BackingField", true);
        Set(job, "originalHand", root.Facts.Own.Hand.Select(c => c.EntityId).ToArray());
        Set(job, "holdLine", hold); Set(job, "holdStaticValue", 1.0);
        Set(job, "leaves", new List<TurnLine> { hold });
        return job;
    }
    private static bool Extend(TurnPlannerJob job, params TurnLine[] scored)
    {
        var list = Get<List<TurnLine>>(job, "scored"); list.Clear(); list.AddRange(scored);
        return (bool)typeof(TurnPlannerJob).GetMethod("ExtendHoldComparison", Fields).Invoke(job, null);
    }
    private static Game ExtensionPosition()
    {
        var g = Position(true, usedMana: 0);
        Entity.FromCard(g.Player1, Cards.FromId("CS2_231"), zone: g.Player1.HandZone);
        g.Player1.Hero.HeroPower.IsExhausted = true;
        return g;
    }
    public static void TestExtensionTriggerMatchesOffAndPricesFaceVariant()
    {
        var job = Extension(ExtensionPosition(), out var hold, out var kept, out var plain, out var hit);
        Check(Extend(job, hold), "a winning hold extends");
        Check(Get<List<TurnLine>>(job, "leaves").Last() == hit, "the tied face variant is priced next, not the shorter line");

        job = Extension(ExtensionPosition(), out hold, out kept, out plain, out hit);
        kept.Value = hold.Value;
        Check(Extend(job, hold, kept), "a face hit tied with the hold does not skip the extension OFF would run");

        job = Extension(ExtensionPosition(), out hold, out kept, out plain, out hit);
        kept.Value = hold.Value + 1;
        Check(!Extend(job, hold, kept), "no tie, no effect: a strict hand-keeping winner gains no extension, as OFF");
    }
}
