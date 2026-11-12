using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

// death-value. A minion's death value is measured data, chain stages recurse through that data, and health on a harmless shell
// earns nothing. Synthetic shapes of the Mocha playtest (rows #37, #73, #74, #124) plus general cases: no rule may name a card.
// Outside the horizon by design: "break now, remove the 20/20 next turn" needs a two-turn plan; search covers this turn and one reply.
public static class DeathValueTests
{
    private static readonly DeckPlan Control = DeckPlans.PurePaladin, Midrange = DeckPlans.DragonWarrior, Aggro = DeckPlans.ZeeShaman;
    private static readonly DeckPlan[] Plans = { Control, Midrange, Aggro };
    private static NpcProfile Profile(bool on, params string[] off)
        => NpcCatalog.All[0].WithFixes(new AiFixes(on ? off : off.Concat(new[] { "death-value" }).ToArray()));
    private static PlannedTurn Search(Game g, DeckPlan plan, bool on, params string[] off)
    {
        var job = TurnPlanner.Job(View(g), Profile(on, off), plan, 71, 10000);
        Run(job); return job.Result;
    }
    private static DeckPlanEvaluator Eval(DeckPlan plan, bool on) => new DeckPlanEvaluator(plan) { DeathFixes = on ? new AiFixes() : new AiFixes(new[] { "death-value" }) };
    private static Game Position(int usedMana = 10)
    {
        var g = Fixture(CardClass.PRIEST, CardClass.DEATHKNIGHT);
        g.Player1.UsedMana = usedMana; g.Player1.Hero.HeroPower[GameTag.COST] = 20;
        return g;
    }
    private static Minion Attacker(Game g, int attack, int health = 3)
    {
        var m = Board(g, true, "CS2_120", true); m.AttackDamage = attack; m.BaseHealth = health; return m;
    }
    private static bool Hits(BotAction a, int target) => a.Type == PlayerTaskType.MINION_ATTACK && a.Target == target;
    private static string Text(IEnumerable<BotAction> line) => string.Join(";", line);
    private static double ForcedValue(Game g, DeckPlan plan, bool on, params BotAction[] line)
    {
        var state = View(g);
        foreach (var a in line) state = state.Apply(a);
        return Eval(plan, on).Evaluate(state.Facts);
    }
    private static void AddHand(Game g, string id) => Entity.FromCard(g.Player1, Cards.FromId(id), zone: g.Player1.HandZone);
    private static BotAction Attack(Game g, Minion a, int target) => View(g).Actions.Single(x => x.Type == PlayerTaskType.MINION_ATTACK && x.Source == a.Id && x.Target == target);
    private static BotAction CardOn(Game g, string id, int target) {
        var card = g.Player1.HandZone.First(c => c.Card.Id == id);
        return View(g).Actions.First(x => x.Type == PlayerTaskType.PLAY_CARD && x.Source == card.Id && x.Target == target);
    }

    // #37: a ready 2/3 and an empty board except the Egg. Face, on every archetype; OFF reproduces the chip on Control.
    public static void TestEggChipLosesToFace()
    {
        foreach (var plan in Plans) {
            var g = Position(); Attacker(g, 2); var egg = Board(g, false, "DINO_410");
            var line = Search(g, plan, true).Line;
            Check(Hits(line[0], g.Player2.Hero.Id), plan.Id + " ON goes face: " + Text(line));
            Check(!line.Any(a => Hits(a, egg.Id)), plan.Id + " ON never chips: " + Text(line));
        }
        var off = Position(); Attacker(off, 2); var shell = Board(off, false, "DINO_410");
        Check(Hits(Search(off, Control, false).Line[0], shell.Id), "OFF reproduces the chip on Control");
    }
    // #73/#74: four stages and a real threat out of reach; the second attacker meets a chipped stage.
    public static void TestStageRowFaceNotBreak()
    {
        foreach (var plan in Plans)
        foreach (bool chipped in new[] { false, true }) {
            var g = Position();
            Board(g, false, "TLC_106");
            var stages = new[] { "DINO_410t3", "DINO_410t4", "DINO_410t2", "DINO_410t5" }.Select(id => Board(g, false, id)).ToArray();
            if (chipped) stages[0].Damage = 2;
            Attacker(g, 2, 2); Attacker(g, 2, 2);
            var line = Search(g, plan, true).Line;
            Check(!line.Any(a => stages.Any(s => Hits(a, s.Id))), plan.Id + " chipped=" + chipped + " leaves shells alone: " + Text(line));
            Check(line.Count(a => Hits(a, g.Player2.Hero.Id)) == 2, plan.Id + " both go face: " + Text(line));
        }
    }
    // t5 fell through the old regex because of its "(Break to hatch!)". Neither chip nor kill: the kill hatches a 20/20 Taunt.
    public static void TestLastStageIsNotBroken()
    {
        foreach (var plan in Plans)
        foreach (int attack in new[] { 2, 4 }) {
            var g = Position(); Attacker(g, attack); var t5 = Board(g, false, "DINO_410t5");
            var line = Search(g, plan, true).Line;
            Check(!line.Any(a => Hits(a, t5.Id)), plan.Id + " attack " + attack + ": " + Text(line));
        }
    }
    // Same rules for the owner: identical value on both sides, stages rise toward the payoff, own shell health is unpriced too.
    public static void TestOwnSideMirrorsEnemySide()
    {
        foreach (var plan in Plans) {
            var own = Position(); Board(own, true, "DINO_410t4"); var enemy = Position(); Board(enemy, false, "DINO_410t4");
            var ev = Eval(plan, true); var fo = View(own).Facts; var fe = View(enemy).Facts;
            Check(Math.Abs(ev.DeathValue(fo.Own, fo.Opponent) - ev.DeathValue(fe.Opponent, fe.Own)) < 1e-9, plan.Id + " symmetric pending value");
            double previous = 0;
            foreach (string id in new[] { "DINO_410", "DINO_410t2", "DINO_410t3", "DINO_410t4", "DINO_410t5" }) {
                var g = Position(); Board(g, false, id); var f = View(g).Facts;
                double value = ev.DeathValue(f.Opponent, f.Own);
                Check(value > previous, plan.Id + " " + id + " worth more than the stage before: " + value + " vs " + previous);
                previous = value;
            }
        }
    }
    // The AI must not hoard its own Egg: with mana it plays it; it never attacks with a 0-attack shell anyway.
    public static void TestOwnEggIsPlayedNotHoarded()
    {
        foreach (var plan in Plans) {
            var g = Position(7); AddHand(g, "DINO_410");
            var line = Search(g, plan, true).Line; var held = g.Player1.HandZone[0];
            Check(line.Any(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == held.Id), plan.Id + " plays the Egg: " + Text(line));
        }
    }
    // Nor may it sit on a last stage it can break itself, even with a play competing for the mana (mined 2:113: two own t5s,
    // Dissolving Ooze or Crocolisks at 3 mana into a 20-attack Taunt). Reply samples make either pick possible at master, on
    // both arms, so the guard is the rate over search seeds.
    public static void TestOwnLastStageIsBrokenOverACompetingPlay()
    {
        foreach (bool on in new[] { true, false }) {
            int broke = 0;
            for (int seed = 0; seed < 8; seed++) {
                var g = Position(7); g.Player1.Hero.Armor = 19;
                var t5 = Board(g, true, "DINO_410t5"); Board(g, true, "DINO_410t5"); Board(g, true, "CORE_EX1_007");
                AddHand(g, "TLC_252"); AddHand(g, "TIME_873");
                var ooze = g.Player1.HandZone[0];
                var big = Board(g, false, "DINO_410t"); big.Damage = 5; big.IsExhausted = false;
                Board(g, false, "CS3_025"); Board(g, false, "TIME_873t");
                var job = TurnPlanner.Job(View(g), Profile(on), Midrange, 7000 + seed * 13, 10000); Run(job);
                if (job.Result.Line.Any(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == ooze.Id && a.Target == t5.Id)) broke++;
            }
            Check(broke >= 6, (on ? "ON" : "OFF") + " breaks its own t5 over the competing play: " + broke + "/8");
        }
        foreach (var plan in Plans) {
            var g = Position(6); var t5 = Board(g, true, "DINO_410t5"); AddHand(g, "TLC_252");
            var threat = Board(g, false, "CS2_200"); threat.IsExhausted = false;
            var ooze = g.Player1.HandZone[0];
            var line = Search(g, plan, true).Line;
            Check(line.Any(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == ooze.Id && a.Target == t5.Id), plan.Id + " breaks its own t5: " + Text(line));
        }
    }
    // One point of Attack is worth about one point of Attack: no jump in pending credit or in health credit, on either side.
    public static void TestOneAttackIsNoCliff()
    {
        foreach (var plan in Plans)
        foreach (bool own in new[] { true, false }) {
            double previous = double.NaN;
            for (int attack = 0; attack <= 4; attack++) {
                var g = Position();
                var pressure = Board(g, !own, "CS2_200"); pressure.IsExhausted = false;
                var shell = Board(g, own, "DINO_410t5"); shell.AttackDamage = attack;
                var f = View(g).Facts; var ev = Eval(plan, true);
                double value = ev.Evaluate(f) * (own ? 1 : -1);
                if (!double.IsNaN(previous)) {
                    double step = value - previous;
                    Check(step > 0 && step < 3 * plan.Weights.BoardAttack + 2, plan.Id + " own=" + own + " attack " + attack + " step " + step);
                }
                previous = value;
            }
        }
    }
    // A Taunt on the last stage does not make the kill worth it: it hatches a bigger Taunt.
    public static void TestTauntedLastStageIsNotBroken()
    {
        foreach (var plan in Plans) {
            var g = Position(); Attacker(g, 3); var t5 = Board(g, false, "DINO_410t5"); t5.HasTaunt = true;
            var line = Search(g, plan, true).Line;
            Check(!line.Any(a => Hits(a, t5.Id)), plan.Id + " leaves the Taunt t5: " + Text(line));
        }
    }
    // A blocking Taunt shell must stay attackable: it keeps its chip credit, and face is not on offer.
    public static void TestTauntShellStillAttacked()
    {
        foreach (var plan in Plans) {
            var g = Position(); Attacker(g, 2); var egg = Board(g, false, "DINO_410"); egg.HasTaunt = true;
            var line = Search(g, plan, true).Line;
            Check(Hits(line[0], egg.Id), plan.Id + " attacks the Taunt shell: " + Text(line));
            Check(!DeckPlanEvaluator.ChipFree(View(g).Facts.Opponent.Board[0]), plan.Id + " Taunt keeps its chip credit");
        }
    }
    // General, not Egg: every 0-attack Deathrattle shell, including ones with very different payoffs.
    public static void TestOtherShellsSameChipRule()
    {
        foreach (string id in new[] { "BOT_537", "DINO_130", "CATA_210", "TLC_237", "TLC_831" })
        foreach (var plan in new[] { Control, Midrange }) {
            var g = Position(); Attacker(g, 1); var shell = Board(g, false, id);
            Check(DeckPlanEvaluator.ChipFree(View(g).Facts.Opponent.Board[0]), id + " is a shell");
            var line = Search(g, plan, true).Line;
            Check(Hits(line[0], g.Player2.Hero.Id), plan.Id + " " + id + " face: " + Text(line));
        }
        // An ordinary minion keeps its reward: a 0/3 with nothing on death and any attacker with a body.
        var plain = Position(); var body = Board(plain, false, "CS2_120");
        Check(!DeckPlanEvaluator.ChipFree(View(plain).Facts.Opponent.Board[0]), "a body with attack is not a shell");
        var a = Attacker(plain, 2);
        double chip = ForcedValue(plain, Control, true, Attack(plain, a, body.Id)), still = ForcedValue(plain, Control, false, Attack(plain, a, body.Id));
        Check(Math.Abs(chip - still) < 1e-9, "damaging an ordinary minion prices the same ON and OFF: " + chip + " vs " + still);
    }
    // Same turn: break the last stage, then hard removal on the hatched 20/20. Beats leaving it when the removal is in hand.
    public static void TestBreakThenRemoveWhenBest()
    {
        foreach (var plan in Plans) {
            var g = Position(5); Attacker(g, 3); var t5 = Board(g, false, "DINO_410t5"); AddHand(g, "CS2_076");
            var line = Search(g, plan, true).Line;
            int kill = line.ToList().FindIndex(a => Hits(a, t5.Id));
            var state = View(g); foreach (var a in line) { if (a.Type == PlayerTaskType.END_TURN) break; state = state.Apply(a); }
            Check(kill >= 0 && line.Any(a => a.Type == PlayerTaskType.PLAY_CARD) && state.Facts.Opponent.Board.Count == 0,
                plan.Id + " breaks and removes: " + Text(line));
        }
    }
    // Silence and transform erase the death value; each outscores chipping the same shell.
    public static void TestSilenceAndTransformBeatChipping()
    {
        foreach (var plan in Plans)
        foreach (string answer in new[] { "EX1_332", "CORE_EX1_246" })
        foreach (string id in new[] { "DINO_410t4", "BOT_537", "TLC_237" }) {
            var g = Position(0); var a = Attacker(g, 2); var shell = Board(g, false, id); AddHand(g, answer);
            double chip = ForcedValue(g, plan, true, Attack(g, a, shell.Id));
            double erased = ForcedValue(g, plan, true, CardOn(g, answer, shell.Id));
            Check(erased > chip, plan.Id + " " + answer + " on " + id + " " + erased + " vs chip " + chip);
        }
    }
    // Lethal through a Taunt shell: the break is the way through, whatever it hatches.
    public static void TestBreakingOnTheWayToLethal()
    {
        foreach (var plan in Plans) {
            var open = Position(); open.Player2.Hero.Damage = 25;
            var stage = Board(open, false, "DINO_410t3"); stage.HasTaunt = true;
            Attacker(open, 3); Attacker(open, 5);
            var win = Search(open, plan, true);
            var state = View(open); foreach (var a in win.Line) { if (a.Type == PlayerTaskType.END_TURN) break; state = state.Apply(a); }
            Check(state.Facts.Opponent.Hero.Health <= 0, plan.Id + " breaks the stage for lethal: " + Text(win.Line));
        }
    }
    // Big chain values must not outbid survival: four stages and one real attacker at lethal range; the removal goes on the attacker.
    public static void TestStackedChainsDoNotDistortSurvival()
    {
        foreach (var plan in Plans) {
            var g = Position(5); g.Player1.Hero.Damage = 24; // 6 life
            var threat = Board(g, false, "CS2_200"); threat.AttackDamage = 8; threat.IsExhausted = false;
            foreach (string id in new[] { "DINO_410t2", "DINO_410t3", "DINO_410t4", "DINO_410t5" }) Board(g, false, id);
            AddHand(g, "CS2_076");
            var line = Search(g, plan, true).Line; var removal = g.Player1.HandZone[0];
            Check(line.Any(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == removal.Id && a.Target == threat.Id), plan.Id + " answers the attacker: " + Text(line));
        }
        // The saturation itself: more chains add at a quarter rate above the cap.
        var one = Position(); Board(one, false, "DINO_410t5"); var two = Position(); Board(two, false, "DINO_410t5"); Board(two, false, "DINO_410t5");
        var four = Position(); for (int i = 0; i < 4; i++) Board(four, false, "DINO_410t5");
        var ev = Eval(Control, true); var a1 = View(one).Facts; var a2 = View(two).Facts; var a4 = View(four).Facts;
        double v1 = ev.DeathValue(a1.Opponent, a1.Own), v2 = ev.DeathValue(a2.Opponent, a2.Own), v4 = ev.DeathValue(a4.Opponent, a4.Own);
        Check(v2 <= 2 * v1 && v4 < 4 * v1, "pending value saturates: " + v1 + "/" + v2 + "/" + v4);
    }
    // #124: Slime 'em into a shell board. Deathrattles leave a t5 and a Khelos; the spell brings all three back.
    private static Game SlimeShape(out IPlayable slime)
    {
        var g = Position(6); g.Player2.Hero.Damage = 8;
        var ownTaunt = Board(g, true, "JAIL_912"); var charger = Board(g, true, "CORE_EX1_414", true); charger.Damage = 0;
        Board(g, true, "TIME_052", true);
        var a = Board(g, false, "DINO_410t4"); a.Damage = 1; Board(g, false, "DINO_410t"); Board(g, false, "DINO_410t5");
        AddHand(g, "CAP_805"); slime = g.Player1.HandZone[0];
        return g;
    }
    public static void TestSlimeCountsWhatComesBack()
    {
        var g = SlimeShape(out var slime);
        var play = View(g).Actions.First(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == slime.Id);
        var after = View(g).Apply(play).Facts;
        var enemySpell = after.Opponent.Hand.Single(c => c.CardId == "CAP_805t");
        Check(enemySpell.Held != null && enemySpell.Held.Count == 3 && enemySpell.Held.Contains("DINO_410t"), "enemy spell holds the public board: " + string.Join(",", enemySpell.Held ?? new string[0]));
        var ownSpell = after.Own.Hand.Single(c => c.CardId == "CAP_805t");
        Check(ownSpell.Held != null && ownSpell.Held.Count == 3, "own spell holds own board");
        double on = Eval(Control, true).Evaluate(after), off = Eval(Control, false).Evaluate(after), rootOn = Eval(Control, true).Evaluate(View(g).Facts), rootOff = Eval(Control, false).Evaluate(View(g).Facts);
        Console.WriteLine("slime-124 static leaf ON " + on.ToString("F1") + " (root " + rootOn.ToString("F1") + ") OFF " + off.ToString("F1") + " (root " + rootOff.ToString("F1") + ")");
        Check(on - rootOn < off - rootOff, "ON sees less gain from Slime 'em than OFF: " + (on - rootOn) + " vs " + (off - rootOff));
        foreach (var plan in Plans) {
            var resultOn = Search(g, plan, true); var resultOff = Search(g, plan, false);
            Console.WriteLine("slime-124 " + plan.Id + " ON " + resultOn.Value.ToString("F1") + " " + Text(resultOn.Line) + " | OFF " + resultOff.Value.ToString("F1") + " " + Text(resultOff.Line));
        }
    }
    // A hand holder is priced by its contents; with no capture it is an ordinary 3-cost spell. The hidden version reveals nothing.
    public static void TestHeldCardPricedByWhatItBrings()
    {
        var g = Position(); Board(g, true, "DINO_410t"); AddHand(g, "CAP_805t");
        var bare = View(g).Facts.Own.Hand.Single();
        Check(bare.Held == null, "an empty holder holds nothing");
        var s = SlimeShape(out var slime);
        var after = View(s).Apply(View(s).Actions.First(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == slime.Id)).Facts;
        var ev = Eval(Control, true);
        Check(ev.Hand(after.Own.Hand.Where(c => c.CardId == "CAP_805t")) > ev.Hand(new[] { bare }), "holder with minions outprices an empty one");
    }
    // Bounce (Twilight Mistress) and steal (Cursed Chains) are in NPC decks. Both move a body and its death value through real
    // engine play; nothing here special-cases them. Bounce strips the enemy's pending value, the steal hands it to the thief.
    public static void TestBounceAndStealMoveTheValue()
    {
        foreach (var plan in Plans) {
            var g = Position(0); var shell = Board(g, false, "DINO_410t5");
            AddHand(g, "CATA_201"); var mistress = g.Player1.HandZone[0];
            var root = View(g);
            var bounce = root.Apply(root.Actions.First(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == mistress.Id));
            var ev = Eval(plan, true);
            Check(ev.DeathValue(bounce.Facts.Opponent, bounce.Facts.Own) == 0, plan.Id + " bounce empties pending value");
            Check(ev.Evaluate(bounce.Facts) > ev.Evaluate(root.Facts), plan.Id + " bounce on a shell gains");
            var s = Position(0); var other = Board(s, false, "DINO_410t5"); AddHand(s, "CATA_496");
            double before = Eval(plan, true).Evaluate(View(s).Facts);
            var stolen = View(s).Apply(CardOn(s, "CATA_496", other.Id));
            Check(stolen.Facts.Own.Board.Any(c => c.CardId == "DINO_410t5") && ev.DeathValue(stolen.Facts.Own, stolen.Facts.Opponent) > 0, plan.Id + " thief holds the value");
            Check(ev.Evaluate(stolen.Facts) > before, plan.Id + " stealing a shell gains");
        }
    }
    // Root view after a real Slime 'em: the opponent's generated spell and its public contents stay known with the sub-flag.
    public static void TestPublicCaptureSurvivesTheMask()
    {
        var g = SlimeShape(out var slime);
        var legal = g.Player1.Options().First(t => t.Source == slime);
        Check(g.Process(legal), "real Slime 'em resolves");
        var quiet = FairBotState.Create(g, 1, 71, DeckStyle.Tempo, new FairOptions { RemoveOpponentSecrets = true });
        var shown = FairBotState.Create(g, 1, 71, DeckStyle.Tempo, new FairOptions { RemoveOpponentSecrets = true, ShowPublicCaptures = true });
        Check(quiet.Facts.Opponent.Hand.All(c => c.Unknown && c.Held == null), "flag off: the hand stays masked");
        var known = shown.Facts.Opponent.Hand.Single(c => !c.Unknown);
        Check(known.CardId == "CAP_805t" && known.Held != null && known.Held.Count == 3, "flag on: holder and its public minions are known");
        Check(shown.Facts.Own.Hand.Single(c => c.CardId == "CAP_805t").Held.Count == 3, "own holder unchanged");
        Check(Eval(Control, true).Evaluate(shown.Facts) < Eval(Control, true).Evaluate(quiet.Facts), "the known spell counts against us");
    }
    // A clock cut plays by the same rules: the search-free fallbacks score with the pilot's evaluator when the flag is on.
    private static FairBotState PilotView(Game g, NpcProfile profile, DeckPlan plan) =>
        new GoldenPilot(profile, plan).Options.FairViewFactory(g, 1, 71, DeckStyle.Tempo);
    private static BotAction Minimum(Game g, bool on, DeckPlan plan)
    {
        var profile = Profile(on); var view = PilotView(g, profile, plan);
        var job = new BoundedFallbackJob(view, view.Actions, profile.Fixes.On("doomed-minions"));
        while (!job.IsDone) job.StepNode();
        Check(job.Compared.Count == view.Actions.GroupBy(a => a.WithoutPosition).Count() || job.Compared.Count == 17, "fallback records what it compared");
        return job.Result;
    }
    public static void TestFallbackFaceOverChip()
    {
        foreach (string id in new[] { "DINO_410t5", "DINO_410", "BOT_537", "TLC_237" })
        foreach (var plan in Plans) {
            var g = Position(); Attacker(g, 2); var shell = Board(g, false, id);
            Check(Hits(Minimum(g, true, plan), g.Player2.Hero.Id), plan.Id + " " + id + " fallback goes face");
            if (shell.Health > 2) Check(Hits(Minimum(g, false, plan), shell.Id), plan.Id + " " + id + " OFF keeps the legacy chip");
            var finish = FastFinish.Choose(PilotView(g, Profile(true), plan), View(g).Actions, out _);
            Check(Hits(finish, g.Player2.Hero.Id), plan.Id + " " + id + " fast finish goes face, not through the shell");
        }
    }
    public static void TestFallbackStillDoesTheObvious()
    {
        foreach (var plan in Plans) {
            // Lethal past a shell.
            var lethal = Position(); lethal.Player2.Hero.Damage = 28; Attacker(lethal, 2); Board(lethal, false, "DINO_410t5");
            Check(Hits(Minimum(lethal, true, plan), lethal.Player2.Hero.Id), plan.Id + " fallback takes lethal");
            // A Taunt shell in the way is still attacked; a kill that hatches a bigger Taunt is not forced by the fallback alone.
            var taunt = Position(); Attacker(taunt, 2); var shell = Board(taunt, false, "DINO_410"); shell.HasTaunt = true;
            Check(Hits(Minimum(taunt, true, plan), shell.Id), plan.Id + " fallback attacks a blocking Taunt");
            // An ordinary trade is untouched.
            var trade = Position(); Attacker(trade, 3, 5); var body = Board(trade, false, "CS2_120");
            Check(Hits(Minimum(trade, true, plan), body.Id), plan.Id + " fallback still trades into an ordinary body");
            // Survival: the only answer goes on the real attacker, not on a shell.
            var danger = Position(5); danger.Player1.Hero.Damage = 24;
            var threat = Board(danger, false, "CS2_200"); threat.AttackDamage = 8; threat.IsExhausted = false;
            Board(danger, false, "DINO_410t5"); AddHand(danger, "CS2_076");
            var removal = Minimum(danger, true, plan);
            Check(removal.Type == PlayerTaskType.PLAY_CARD && removal.Target == threat.Id, plan.Id + " fallback answers the attacker: " + removal);
        }
    }
    // Why and what: a clock cut before the planner, a node cut, and a fast-finish host path all leave a reason and a comparison.
    public static void TestFallbackDecisionLogsWhyAndWhat()
    {
        foreach (bool on in new[] { true, false }) {
            var g = Position(); Attacker(g, 2); Board(g, false, "DINO_410t5");
            var profile = Profile(on); var pilot = new GoldenPilot(profile, Control);
            var view = pilot.Options.FairViewFactory(g, 1, 71, DeckStyle.Tempo);
            var job = pilot.Job(view, new GoldenContext(g.Turn, 1, 71, profile));
            while (job.Preparing) job.StepNode();
            var stopped = job.StopSearch(SearchStop.Clock);
            Check(stopped.DriverKind == "fallback" && stopped.SearchInfo.FallbackWhy == "minimum-answer:Clock:no-planner", "why: " + stopped.SearchInfo.FallbackWhy);
            Check(stopped.FallbackReason == null, "the runner's control-flow reason is unchanged");
            Check(stopped.Candidates.Count >= 2 && stopped.Candidates.Any(c => c.Key.Equals(stopped.Action)), "what it compared: " + stopped.Candidates.Count);
        }
        var fallback = Position(); Attacker(fallback, 2);
        var v = PilotView(fallback, Profile(true), Control); int budget = 16;
        var picked = FastFinish.Fallback(v, v.Actions.ToArray(), "clock-cut", () => budget-- > 0, out _, out var compared);
        Check(picked != null && compared != null && compared.Any(c => c.Key.Equals(picked)), "host fallback reports its comparison");
    }
    // Flag anatomy: each sub-flag switches only its own mechanism, and the OFF evaluator is the old one.
    public static void TestSubFlagsAndOffPath()
    {
        var g = Position(); Attacker(g, 2); var egg = Board(g, false, "DINO_410"); var f = View(g).Facts;
        var off = new DeckPlanEvaluator(Control); var offFlag = Eval(Control, false);
        Check(off.Evaluate(f) == offFlag.Evaluate(f), "flag OFF equals an evaluator that never heard of it");
        var noChip = new DeckPlanEvaluator(Control) { DeathFixes = new AiFixes(new[] { "death-chip" }) };
        var noMeasure = new DeckPlanEvaluator(Control) { DeathFixes = new AiFixes(new[] { "death-measured" }) };
        var all = Eval(Control, true);
        Check(noChip.DeathMeasured && !noChip.DeathChip && noChip.DeathHeld, "death-chip only");
        Check(!noMeasure.DeathMeasured && noMeasure.DeathChip && noMeasure.DeathHeld, "death-measured only");
        Check(all.Evaluate(f) != noChip.Evaluate(f) && all.Evaluate(f) != noMeasure.Evaluate(f), "each switch moves the value");
    }
    // No card ids in the rules; a new card only needs the generated row. The sync test is this and `hs ai death-values --check` (holders).
    public static void TestTableMatchesTheCardSet()
    {
        var generated = DeathProbe.Generate().ToDictionary(r => r.Id, r => r.Serialize());
        var shipped = DeathMeasures.Rows.ToDictionary(p => p.Key, p => p.Value.Serialize());
        var stale = generated.Where(p => !shipped.TryGetValue(p.Key, out var row) || row != p.Value).Select(p => p.Key)
            .Concat(shipped.Keys.Where(k => !generated.ContainsKey(k))).ToArray();
        Check(stale.Length == 0, "regenerate with `hs ai death-values --write`: " + string.Join(",", stale.Take(20)));
        Check(shipped.ContainsKey("DINO_410t5") && DeathMeasures.Rows["DINO_410"].Next.Single().Key == "DINO_410t2", "chain comes from the probe");
        Check(!DeathMeasures.Rows["DINO_410t5"].Opaque && DeathMeasures.Rows["TLC_237"].Now.Bodies == 4, "regex misses are measured");
    }
    public static void TestHotPathDoesNotAllocateMore()
    {
        var g = Position(); for (int i = 0; i < 4; i++) Board(g, false, "DINO_410t5");
        var f = View(g).Facts; var ev = Eval(Control, true); double total = 0;
        for (int i = 0; i < 1000; i++) total += ev.DeathValue(f.Opponent, f.Own);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) total += ev.DeathValue(f.Opponent, f.Own);
        Check(GC.GetAllocatedBytesForCurrentThread() == before && total != 0, "pending value allocates nothing");
    }
}
