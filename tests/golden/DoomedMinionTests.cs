using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

// doomed-minions. A minion carrying a delayed self-destroy (Nightmare, Corruption, Power Overwhelming, ...) earns no board
// credit on either side. The live case (Deck playtest JadeBrewer #108): the NPC cast Nightmare (+5/+5, destroyed at the start
// of its next turn) on its own exhausted 4/4 into an empty enemy board and the static value rose 137.92 -> 153.42, a +17.5
// board credit for a minion that would be gone before it could act.
public static class DoomedMinionTests
{
    private static string Line(IEnumerable<BotAction> line) => string.Join(";", line);
    private const int Full = 10000, Starved = 300;

    private static NpcProfile Profile(bool on, int replyL = -1)
    {
        var p = NpcCatalog.All[0].WithFixes(new AiFixes(on ? new string[0] : new[] { "doomed-minions" }));
        if (replyL < 0) return p;
        var b = p.Budget;
        return new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin, p.CardBack, p.Personality,
            new Budget(b.TurnNodes, b.PlanNodes, b.LethalNodes, b.ReplyK, replyL, b.ReplyR, b.OwnBeamWidth, b.Dedupe, 45, 55, b.Brain),
            p.PlanId, p.HeroSkinPower, p.Fixes);
    }
    // cap 300 leaves no room for a reply (static-only decision, the live #108 shape); asserted by DiagnosticReplyLimit.
    private static TurnPlannerJob Plan(Game g, bool on, int cap, int replyL = -1, BotAction first = null)
    {
        var job = TurnPlanner.Job(View(g), Profile(on, replyL), DeckPlans.DragonWarrior, 71, cap, firstAction: first);
        Run(job); return job;
    }
    private static Game Position(CardClass own, int life = 30)
    {
        var g = Fixture(own, CardClass.MAGE);
        g.Player1.BaseMana = 10; g.Player1.UsedMana = 0; g.Player1.Hero.Damage = 30 - life;
        return g;
    }
    private static IPlayable HandCard(Game g, string id) => Entity.FromCard(g.Player1, Cards.FromId(id), zone: g.Player1.HandZone);
    private static bool Plays(PlannedTurn r, IPlayable card) => r.Line.Any(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == card.Id);
    private static void Cast(Game g, IPlayable spell, IEntity target)
    {
        var action = View(g).Actions.Single(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == spell.Id && a.Target == target.Id);
        Check(g.Process(action.Resolve(g.Player1.Options())), "cast rejected");
    }
    private static FairCard Mine(Game g, IEntity minion) => View(g).Facts.Own.Board.Single(c => c.EntityId == minion.Id);
    private static FairCard Theirs(Game g, IEntity minion) => View(g).Facts.Opponent.Board.Single(c => c.EntityId == minion.Id);

    private static bool DiesWithinARound(Card enchantment)
    {
        var g = Fixture(CardClass.WARRIOR, CardClass.MAGE);
        var minion = Board(g, true, "CS2_182");
        SabberStoneCore.Actions.Generic.AddEnchantmentBlock(g, enchantment, minion, minion);
        for (int i = 0; i < 2 && g.State != SabberStoneCore.Enums.State.COMPLETE; i++) g.Process(EndTurnTask.Any(g.CurrentPlayer));
        return !g.Player1.BoardZone.Any(m => m.Id == minion.Id);
    }

    // Generated list, not a hand-written one: every printed enchantment is attached to a vanilla minion and the engine is
    // asked whether the minion is gone within a full round. Detection must match that behaviour, with no card ids involved.
    public static void TestDetectionMatchesEngineBehaviour()
    {
        var enchantments = Cards.All.Where(c => c.Type == CardType.ENCHANTMENT).ToArray();
        var died = new HashSet<string>(); int tried = 0;
        foreach (var c in enchantments) {
            try { if (DiesWithinARound(c)) died.Add(c.Id); tried++; } catch (Exception) { }
        }
        Check(tried > 1000, "the sweep must reach most enchantments: " + tried + "/" + enchantments.Length);
        var detected = new HashSet<string>(enchantments.Where(c => DoomedMinions.EnchantmentDooms(c.Id)).Select(c => c.Id));
        // Not self-destroys: two control swaps that return the minion to its owner at end of turn (Shadow Madness and a
        // sibling) and two bare-attach artifacts whose scripted numbers are unset in this harness.
        var other = new HashSet<string> { "CFM_603e", "EX1_334e", "CFM_063e", "ICC_827e" };
        var missed = died.Except(detected).Except(other).ToArray();
        Check(missed.Length == 0, "an enchantment the engine destroys its minion with is not detected: " + string.Join(",", missed));
        var wrong = detected.Except(died).ToArray();
        Check(wrong.Length == 0, "a detected enchantment that does not destroy its minion: " + string.Join(",", wrong));
        foreach (var id in new[] { "EX1_316e", "UNG_831e", "DREAM_05e", "BOT_566e", "BOT_437e", "CS2_063e", "BE_553e", "DAL_607e", "DINO_417e" })
            Check(detected.Contains(id), "known delayed destroy not detected: " + id);
        Check(detected.Count == 9, "detected set: " + string.Join(",", detected.OrderBy(x => x)));
        // Printed own-trigger minions: only a bare end-of-turn self-destroy (Frail Ghoul) qualifies; Doomsayer, Gurubashi
        // Offering and Alarm-o-Matic carry other payloads and stay out.
        var own = Cards.All.Where(c => c.Type == CardType.MINION && DoomedMinions.Dooms(c.Power?.Trigger, false)).Select(c => c.Id).ToArray();
        Check(own.SequenceEqual(new[] { "HERO_11bpt" }), "printed self-destroy minions: " + string.Join(",", own));
    }

    public static void TestFlagsOnPublicState()
    {
        var g = Position(CardClass.WARLOCK);
        var yeti = Board(g, true, "CS2_182", true); var plain = Board(g, true, "CS2_182", true);
        var ghoul = Board(g, true, "HERO_11bpt"); var ogre = Board(g, false, "CS2_200"); var power = Board(g, true, "CS2_182", true);
        var nightmare = HandCard(g, "DREAM_05"); var overwhelming = HandCard(g, "EX1_316"); var corruption = HandCard(g, "CS2_063");
        Cast(g, nightmare, yeti); Cast(g, corruption, ogre); Cast(g, overwhelming, power);
        Check(Mine(g, yeti).Doomed && Mine(g, power).Doomed && Mine(g, ghoul).Doomed, "Nightmare, Power Overwhelming and the printed Frail Ghoul are doomed");
        Check(Theirs(g, ogre).Doomed, "an enemy minion under the NPC's Corruption is doomed");
        Check(!Mine(g, plain).Doomed, "an untouched minion is not doomed");
        yeti.Silence(); ogre.Silence();
        Check(!Mine(g, yeti).Doomed && !Theirs(g, ogre).Doomed, "Silence removes the doom");
        Check(Mine(g, ghoul).Doomed, "silencing others leaves the Ghoul doomed");
        ghoul.Silence();
        Check(!Mine(g, ghoul).Doomed, "a silenced printed trigger is no doom");
    }

    public static void TestZeroCreditOnBothSides()
    {
        var g = Position(CardClass.DRUID);
        var mine = Board(g, true, "CS2_182", true); var theirs = Board(g, false, "CS2_200"); Board(g, false, "CS2_182");
        var first = HandCard(g, "DREAM_05"); var second = HandCard(g, "DREAM_05");
        var off = new DeckPlanEvaluator(DeckPlans.DragonWarrior); var on = new DeckPlanEvaluator(DeckPlans.DragonWarrior) { ZeroDoomed = true };
        Check(View(g).Evaluate(off) == View(g).Evaluate(on), "no doomed minion: the flag changes nothing");
        Cast(g, first, mine); Cast(g, second, theirs);
        var state = View(g);
        double ownCredit = off.FriendlyBoardCredit(Mine(g, mine)), enemyCredit = off.FriendlyBoardCredit(Theirs(g, theirs));
        double enemySwing = off.FriendlyAttackCredit(Theirs(g, theirs), Theirs(g, theirs).Attack);
        Check(ownCredit > 20 && enemyCredit > 30, "fixture: the buffed bodies carried a large credit: " + ownCredit + "/" + enemyCredit);
        // Mid-turn it is our turn: both bodies are worth nothing, and no swing credit exists (it would punish attacking).
        double delta = state.Evaluate(on) - state.Evaluate(off);
        Check(Math.Abs(delta - (enemyCredit - ownCredit)) < 1e-9, "our turn: ON drops both bodies: delta " + delta + " vs " + (enemyCredit - ownCredit));
        // End of our turn: our minion has no swing left, the enemy's swings once in its reply and is gone at our next turn.
        var ended = state.ForReplyEndTurn();
        delta = ended.Evaluate(on) - ended.Evaluate(off);
        Check(Math.Abs(delta - (enemyCredit - enemySwing - ownCredit)) < 1e-9, "enemy turn: ON keeps only the enemy's swing: delta " + delta + " vs " + (enemyCredit - enemySwing - ownCredit));
        mine.Silence(); theirs.Silence();
        Check(View(g).Evaluate(on) == View(g).Evaluate(off), "silenced minions are credited alike");
    }

    // JadeBrewer #108: own exhausted minion, empty enemy board, Nightmare in hand. Static only (no reply fits the cap).
    public static void TestOwnNightmareOnExhaustedMinionIsNoGain()
    {
        var g = Position(CardClass.WARRIOR);
        var yeti = Board(g, true, "CS2_182"); // summoned this turn: cannot attack
        var nightmare = HandCard(g, "DREAM_05");
        var off = Plan(g, false, Starved); var on = Plan(g, true, Starved);
        Check(off.DiagnosticReplyLimit == 0 && on.DiagnosticReplyLimit == 0, "fixture: static-only decision");
        Check(Plays(off.Result, nightmare), "OFF reproduces the live #108 blunder: " + Line(off.Result.Line));
        Check(!Plays(on.Result, nightmare), "ON does not buff a minion that dies before it can act: " + Line(on.Result.Line));
        Check(!Plays(Plan(g, true, Full).Result, nightmare) && !Plays(Plan(g, false, Full).Result, nightmare), "with replies neither arm does it");
        var root = View(g); var cast = root.Apply(root.Actions.Single(a => a.Type == PlayerTaskType.PLAY_CARD && a.Target == yeti.Id));
        var eoff = new DeckPlanEvaluator(DeckPlans.DragonWarrior); var eon = new DeckPlanEvaluator(DeckPlans.DragonWarrior) { ZeroDoomed = true };
        double gainOff = cast.ForReplyEndTurn().Evaluate(eoff) - root.ForReplyEndTurn().Evaluate(eoff);
        double gainOn = cast.ForReplyEndTurn().Evaluate(eon) - root.ForReplyEndTurn().Evaluate(eon);
        Check(gainOff > 10, "OFF values the cast as a gain (the +15.5 seen live): " + gainOff);
        Check(gainOn < -10, "ON values it as the loss of the body: " + gainOn);
    }

    // The common Warlock line: buff, then trade with it. The buff dies at end of turn, the attack is in the line.
    public static void TestPowerOverwhelmingTradeStaysAttractive()
    {
        foreach (int life in new[] { 20, 6 }) {
            var g = Position(CardClass.WARRIOR, life);
            var po = HandCard(g, "EX1_316"); var yeti = Board(g, true, "CS2_182", true);
            var ogre = Board(g, false, "CS2_200"); ogre.IsExhausted = false;
            foreach (int cap in new[] { Full, Starved }) {
                var off = Plan(g, false, cap); var on = Plan(g, true, cap);
                foreach (var job in new[] { off, on }) {
                    var line = job.Result.Line.ToList();
                    int buff = line.FindIndex(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == po.Id && a.Target == yeti.Id);
                    int trade = line.FindIndex(a => a.Type == PlayerTaskType.MINION_ATTACK && a.Source == yeti.Id && a.Target == ogre.Id);
                    Check(buff >= 0 && trade > buff, "life " + life + " cap " + cap + ": buff, then trade: " + Line(line));
                }
                Check(Line(on.Result.Line) == Line(off.Result.Line) && on.Result.Value == off.Result.Value,
                    "life " + life + " cap " + cap + ": an end-of-turn doom is already priced by the endpoint, so ON equals OFF");
            }
        }
    }

    // A friendly Nightmare'd minion that is ready can swing this turn, and the search must still see the swing.
    public static void TestNightmareOnReadyMinionStillAttacks()
    {
        var g = Position(CardClass.DRUID);
        var nightmare = HandCard(g, "DREAM_05"); var yeti = Board(g, true, "CS2_182", true);
        var ogre = Board(g, false, "CS2_200"); ogre.IsExhausted = false;
        var cast = View(g).Actions.Single(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == nightmare.Id && a.Target == yeti.Id);
        foreach (int cap in new[] { Full, Starved }) {
            var line = Plan(g, true, cap, first: cast).Result.Line.ToList();
            Check(line.Count > 1 && line[0].Equals(cast) && line.Any(a => a.Type == PlayerTaskType.MINION_ATTACK && a.Source == yeti.Id && a.Target == ogre.Id),
                "cap " + cap + ": after the buff the ready 9/10 still trades into the Ogre: " + Line(line));
        }
        // Lethal: 9 life, the 9/10 swings for exactly lethal.
        g.Player2.Hero.Damage = 21;
        foreach (bool onFlag in new[] { false, true }) {
            var r = Plan(g, onFlag, Full).Result;
            Check(r.Value >= 999000 && Plays(r, nightmare) && r.Line.Any(a => a.Type == PlayerTaskType.MINION_ATTACK && a.Target == g.Player2.Hero.Id),
                "lethal through the buff, flag " + onFlag + ": " + Line(r.Line) + " " + r.Value);
        }
    }

    // At 6 life a ready 6/7 Ogre is lethal next turn; the only answer is the buff-then-trade. Unforced, both arms must find it.
    public static void TestNightmareThenTradeWhenItIsTheOnlyAnswer()
    {
        var g = Position(CardClass.WARRIOR, 6);
        var nightmare = HandCard(g, "DREAM_05"); var yeti = Board(g, true, "CS2_182", true);
        var ogre = Board(g, false, "CS2_200"); ogre.IsExhausted = false;
        foreach (int cap in new[] { Full, Starved })
            foreach (bool on in new[] { false, true }) {
                var r = Plan(g, on, cap).Result; var line = r.Line.ToList();
                int buff = line.FindIndex(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == nightmare.Id && a.Target == yeti.Id);
                int trade = line.FindIndex(a => a.Type == PlayerTaskType.MINION_ATTACK && a.Source == yeti.Id && a.Target == ogre.Id);
                Check(buff >= 0 && trade > buff, "cap " + cap + " flag " + on + ": buff, then trade: " + Line(line));
            }
    }

    // An enemy doomed minion keeps attacking in the reply: the replies still see the lethal, and only the static leaf
    // stops crediting it.
    public static void TestEnemyDoomedMinionStillAttacksInReply()
    {
        var g = Position(CardClass.WARRIOR, 9);
        var nightmare = HandCard(g, "DREAM_05"); var ogre = Board(g, false, "CS2_200"); ogre.IsExhausted = false;
        var leaf = View(g); leaf = leaf.Apply(leaf.Actions.Single(a => a.Type == PlayerTaskType.PLAY_CARD && a.Target == ogre.Id));
        foreach (bool on in new[] { false, true }) {
            var reply = ReplyModel.Job(leaf, Profile(on), 71, new DeckPlanEvaluator(DeckPlans.DragonWarrior) { ZeroDoomed = on }); Run(reply);
            Check(reply.Survival == 0, "flag " + on + ": the 11/12 doomed Ogre still kills the 9-life NPC in the reply (survival " + reply.Survival + ")");
        }
        // Static only: the doomed enemy keeps only the credit of its one swing, so Nightmare on a wall (a 2/20 here) is a
        // removal worth its card, while the same cast on a small threat (a 6/7) is not free either.
        g = Position(CardClass.WARRIOR);
        nightmare = HandCard(g, "DREAM_05"); ogre = Board(g, false, "CS2_200"); ogre.IsExhausted = false;
        ogre.AttackDamage = 2; ogre[GameTag.HEALTH] = 20;
        Check(!Plays(Plan(g, false, Starved).Result, nightmare), "OFF never casts it on the enemy wall statically (a 7/25 looks bigger)");
        Check(Plays(Plan(g, true, Starved).Result, nightmare), "ON prices the doomed wall as its one swing and casts it statically");
        g = Position(CardClass.WARRIOR);
        nightmare = HandCard(g, "DREAM_05"); ogre = Board(g, false, "CS2_200"); ogre.IsExhausted = false;
        Check(!Plays(Plan(g, true, Starved).Result, nightmare), "ON does not treat a doomed 6/7 as a free removal: its 11-point swing stays priced");
        // With replies the price is reply-tested, not assumed: the wall cast is a finalist and the replies decide.
        g = Position(CardClass.WARRIOR);
        nightmare = HandCard(g, "DREAM_05"); ogre = Board(g, false, "CS2_200"); ogre.IsExhausted = false;
        ogre.AttackDamage = 2; ogre[GameTag.HEALTH] = 20;
        var job = TurnPlanner.Job(View(g), Profile(true, 3), DeckPlans.DragonWarrior, 71, Full);
        var finalists = new List<string>();
        job.ObserveLeaf = (n, tag) => { if (tag == "shortlisted") finalists.Add(Line(n.Actions)); };
        Run(job);
        Check(finalists.Any(f => f.Contains("PLAY_CARD:" + nightmare.Id + ":" + ogre.Id)), "the cast is a reply finalist: " + string.Join(" | ", finalists));
    }

    // The reply frontier: the same reply root, ranked the way ReplyJob ranks it (TurnBeam on ReplyValue, width 4). A doomed
    // enemy attack must rank exactly as before; an unspent-swing credit would make attacking look like a loss.
    private sealed class ReplyFrame { public FairBotState Root; public FairFacts Leaf; }
    private static ReplyFrame FirstReplyFrame(FairBotState leaf, bool on)
    {
        var frame = new ReplyFrame { Leaf = leaf.Facts };
        var job = ReplyModel.Job(leaf, Profile(on), 71, new DeckPlanEvaluator(DeckPlans.DragonWarrior) { RecurringValue = true, ZeroDoomed = on }, null, 160);
        job.ObserveSample = (root, line, score) => { if (frame.Root == null) frame.Root = root; };
        Run(job);
        return frame;
    }
    private static TurnBeam ReplyBeam(ReplyFrame frame, bool on, BotAction first = null)
    {
        var ev = new DeckPlanEvaluator(DeckPlans.DragonWarrior) { RecurringValue = true, ZeroDoomed = on };
        var beam = new TurnBeam(frame.Root, st => -ev.ReplyValue(st.Facts, frame.Leaf, st.ForReplyHand(frame.Leaf)), 160, ReplyModel.BeamWidth, true, firstAction: first);
        while (!beam.IsDone) beam.StepNode();
        return beam;
    }
    public static void TestEnemyDoomedAttackRanksAsBeforeInReplyBeam()
    {
        foreach (int vanilla in new[] { 0, 2, 5 }) {
            var counts = new int[2]; var survival = new double?[2]; var best = new double[2]; var forced = new double[2];
            foreach (bool on in new[] { false, true }) {
                var g = Position(CardClass.WARRIOR, 30);
                g.Player2.BaseMana = 9;
                var nightmare = HandCard(g, "DREAM_05"); var ogre = Board(g, false, "CS2_200"); ogre.IsExhausted = false;
                for (int i = 0; i < vanilla; i++) { var m = Board(g, false, "CS2_182"); m.IsExhausted = false; }
                var root = View(g); var leaf = root.Apply(root.Actions.Single(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == nightmare.Id && a.Target == ogre.Id));
                var frame = FirstReplyFrame(leaf, on); Check(frame.Root != null, "a reply sample exists");
                var beam = ReplyBeam(frame, on); int i1 = on ? 1 : 0;
                counts[i1] = beam.Leaves.Count(l => l.Actions.Any(a => a.Type == PlayerTaskType.MINION_ATTACK && a.Source == ogre.Id));
                best[i1] = beam.Leaves.Max(l => l.Value);
                var attack = frame.Root.OrderedActions.First(a => a.Type == PlayerTaskType.MINION_ATTACK && a.Source == ogre.Id);
                forced[i1] = ReplyBeam(frame, on, attack).Leaves.Max(l => l.Value);
                var job = ReplyModel.Job(leaf, Profile(on), 71, new DeckPlanEvaluator(DeckPlans.DragonWarrior) { RecurringValue = true, ZeroDoomed = on }, null, 160); Run(job);
                survival[i1] = job.Survival;
            }
            Check(counts[0] > 0 && counts[1] == counts[0], "vanilla " + vanilla + ": reply lines with the doomed attack, OFF " + counts[0] + " ON " + counts[1]);
            Check(Math.Abs(forced[1] - best[1] - (forced[0] - best[0])) < 1e-9 && (forced[1] >= best[1] - 1e-9) == (forced[0] >= best[0] - 1e-9),
                "vanilla " + vanilla + ": the forced attack ranks against the beam's pick as before: OFF " + (forced[0] - best[0]) + " ON " + (forced[1] - best[1]));
            Check(survival[0] == survival[1], "vanilla " + vanilla + ": reply survival OFF " + survival[0] + " ON " + survival[1]);
        }
    }

    // Enemy Power Overwhelming in a reply is a doomed buff too: the buffed attack must still be found, lethal included.
    public static void TestEnemyPowerOverwhelmingLethalSurvivesReplyRanking()
    {
        foreach (bool on in new[] { false, true }) {
            var g = Position(CardClass.WARRIOR, 5);
            g.Player2.BaseMana = 9;
            var yeti = Board(g, false, "CS2_182"); yeti.IsExhausted = false; // 4 attack, the NPC has 5 life: only the buff is lethal
            var root = View(g);
            var reply = root.Apply(root.Actions.Single(a => a.Type == PlayerTaskType.END_TURN)).Determinize(new[] { "EX1_316" }, 91).ForReply();
            var ev = new DeckPlanEvaluator(DeckPlans.DragonWarrior) { RecurringValue = true, ZeroDoomed = on };
            var facts = root.Facts;
            var beam = new TurnBeam(reply, st => -ev.ReplyValue(st.Facts, facts, st.ForReplyHand(facts)), 160, ReplyModel.BeamWidth, true);
            while (!beam.IsDone) beam.StepNode();
            Check(beam.Leaves.Max(l => l.Value) >= 999000, "flag " + on + ": the buffed lethal is on the reply frontier: " + string.Join(" | ", beam.Leaves.OrderByDescending(l => l.Value).Take(3).Select(l => l.Value.ToString("F1") + " " + Line(l.Actions))));
        }
    }

    // The starved bounded fallback (the live #108 path when no search budget remains) honors the flag for our own minions
    // and never turns Nightmare on an enemy into a free removal.
    public static void TestBoundedFallbackHonorsFlag()
    {
        var g = Position(CardClass.WARRIOR);
        var yeti = Board(g, true, "CS2_182"); var nightmare = HandCard(g, "DREAM_05");
        var view = View(g);
        foreach (bool on in new[] { false, true }) {
            var job = new BoundedFallbackJob(view, view.Actions, on); while (!job.IsDone) job.StepNode();
            bool casts = job.Result.Type == PlayerTaskType.PLAY_CARD && job.Result.Source == nightmare.Id;
            Check(casts == !on, "own exhausted minion, flag " + on + ": picks " + job.Result);
        }
        foreach (var shape in new[] { new[] { 30, 6, 7 }, new[] { 10, 2, 20 }, new[] { 30, 2, 20 } }) {
            g = Position(CardClass.WARRIOR, shape[0]);
            nightmare = HandCard(g, "DREAM_05"); var ogre = Board(g, false, "CS2_200"); ogre.IsExhausted = false;
            ogre.AttackDamage = shape[1]; ogre[GameTag.HEALTH] = shape[2];
            view = View(g);
            foreach (bool on in new[] { false, true }) {
                var job = new BoundedFallbackJob(view, view.Actions, on); while (!job.IsDone) job.StepNode();
                bool casts = job.Result.Type == PlayerTaskType.PLAY_CARD && job.Result.Source == nightmare.Id;
                Check(!casts, "enemy " + shape[1] + "/" + shape[2] + " at " + shape[0] + " life, flag " + on + ": the fallback must not cast Nightmare on it: " + job.Result);
            }
        }
    }

    public static void TestOrdinaryPositionsUnchanged()
    {
        var g = Position(CardClass.WARRIOR);
        Board(g, true, "CS2_182", true); var ogre = Board(g, false, "CS2_200"); ogre.IsExhausted = false;
        HandCard(g, "CS2_005"); HandCard(g, "CS2_182");
        foreach (int cap in new[] { Full, Starved }) {
            var off = Plan(g, false, cap).Result; var on = Plan(g, true, cap).Result;
            Check(Line(on.Line) == Line(off.Line) && on.Value == off.Value && on.Nodes == off.Nodes, "no doomed minion: same search, cap " + cap);
        }
    }

    private static DeckPlanEvaluator Flags(bool doom, bool death) => new DeckPlanEvaluator(DeckPlans.DragonWarrior)
        { ZeroDoomed = doom, DeathFixes = death ? new AiFixes() : new AiFixes(new[] { "death-value" }) };

    // doomed-minions x death-value on a Deathrattle body (Egg of Khelos stages), under all four flag combinations. The doomed
    // body's printed-stat credit is zero (Priced wraps Board, which death-value rewrote), the chip rule has nothing left to
    // scale, and the Deathrattle it WILL pay (the doom guarantees the death) stays as one pending term with no killability
    // credit: so ON/ON minus ON/death-off is exactly the swap of the flat prior for the measured pending term, once.
    public static void TestDoomedDeathrattleUnderBothFlags()
    {
        foreach (string stage in new[] { "DINO_410", "DINO_410t4", "DINO_410t5" })
        foreach (string spell in new[] { "DREAM_05", "EX1_316" })
        foreach (bool mine in spell == "EX1_316" ? new[] { true } : new[] { true, false }) {
            string what = stage + " " + spell + (mine ? " own" : " enemy");
            var g = Position(CardClass.DRUID);
            var egg = Board(g, mine, stage); Board(g, !mine, "CS2_182", true);
            Cast(g, HandCard(g, spell), egg);
            var state = View(g); var facts = state.Facts;
            var owner = mine ? facts.Own : facts.Opponent; var other = mine ? facts.Opponent : facts.Own;
            Check((mine ? Mine(g, egg) : Theirs(g, egg)).Doomed, what + ": doomed fixture");
            var v = new Dictionary<string, double>();
            foreach (bool doom in new[] { false, true })
            foreach (bool death in new[] { false, true }) {
                double value = state.Evaluate(Flags(doom, death));
                Check(!double.IsNaN(value) && !double.IsInfinity(value) && Math.Abs(value) < 5000, what + ": sane value " + value);
                // The reply frame and the end-of-turn leaf must not crash or blow up either.
                double ended = state.ForReplyEndTurn().Evaluate(Flags(doom, death));
                Check(!double.IsNaN(ended) && Math.Abs(ended) < 5000, what + ": sane end-of-turn value " + ended);
                v[doom + "/" + death] = value;
            }
            // Flags off = base: death-value and doom both off is the plain printed-stat price on either arm.
            double sign = mine ? 1 : -1;
            double pending = sign * Flags(true, true).DeathValue(owner, other), flat = sign * Flags(true, false).DeathValue(owner, other);
            double pendingUndoomed = sign * Flags(false, true).DeathValue(owner, other);
            // The base prices a Deathrattle at a flat prior; death-value swaps that one term for the measured one.
            Check(Math.Abs(v["True/True"] - v["True/False"] - (pending - flat)) < 1e-9,
                what + ": doom ON, death-value swaps the pending term once: " + (v["True/True"] - v["True/False"]) + " vs " + (pending - flat));
            Check(Math.Abs(pending) > 1e-9, what + ": the certain Deathrattle payoff still counts");
            Check(pending * sign <= pendingUndoomed * sign + 1e-9 && Math.Abs(pending) <= Math.Abs(pendingUndoomed) + 1e-9, what + ": no killability credit on a doomed body");
            // The doomed body drops its printed-stat credit exactly once, with death-value off and on alike.
            double drop = Math.Abs(v["False/False"] - v["True/False"]);
            Check(drop > 1, what + ": doom removes the stat credit: " + drop);
            Check((v["True/True"] < v["False/True"]) == mine, what + ": doom lowers an own body's value and raises an enemy's");
            Check(Math.Abs(v["False/False"] - v["True/False"]) > 0 && Math.Abs(v["False/True"] - v["True/True"]) > 0, what + ": doom matters under both death-value arms");
            // A reply search under the combined flags terminates cleanly.
            var job = ReplyModel.Job(state.ForReplyEndTurn(), Profile(true), 71, Flags(true, true), null, 40); Run(job);
            Check(!double.IsNaN(job.Value), what + ": reply search value");
        }
    }

    public static void TestFlagRegistered()
    {
        Check(AiFixes.Registered.Contains("doomed-minions") && new AiFixes().On("doomed-minions"), "default ON");
        Check(!new AiFixes(new[] { "doomed-minions" }).On("doomed-minions"), "can be disabled by id");
    }
}
