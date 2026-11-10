using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

// trigger-value. What one firing is worth is measured data; how often it fires is a public estimate, both seats alike. Synthetic
// shapes of the Spider Rider playtest (enemy engine, enablers public, an equal-stat vanilla body beside it) plus general cases:
// no rule may name a card.
public static class TriggerValueTests
{
    private const string Rider = "JAIL_872", Acolyte = "CORE_EX1_007", Supplier = "CAP_003", Maiev = "JAIL_850";
    private static readonly DeckPlan Plan = DeckPlans.PurePaladin;
    private static NpcProfile Profile(bool on, params string[] off)
        => NpcCatalog.All[0].WithFixes(new AiFixes(on ? off : off.Concat(new[] { "trigger-value" }).ToArray()));
    private static DeckPlanEvaluator Eval(bool on = true) => new DeckPlanEvaluator(Plan) { TriggerFixes = on ? new AiFixes() : new AiFixes(new[] { "trigger-value" }) };
    private static double Term(Game g, DeckPlanEvaluator e = null, TriggerEvidence own = default, TriggerEvidence enemy = default)
    {
        var f = View(g).Facts; var ev = e ?? Eval(); ev.SetTriggerEvidence(own, enemy);
        return ev.TriggerTerm(f.Own, f.Opponent);
    }
    // A 1/4 with no text at the same printed cost: stats equal to the engine's, nothing else.
    private static Minion Plain(Game g, bool own, int attack, int health, int cost, bool ready = false)
    {
        var m = Board(g, own, "CS2_182", ready); m.AttackDamage = attack; m.BaseHealth = health; m[GameTag.COST] = cost; return m;
    }
    private static Game Position(CardClass own = CardClass.PRIEST, CardClass enemy = CardClass.DRUID, int usedMana = 10)
    {
        var g = Fixture(own, enemy);
        g.Player1.UsedMana = usedMana; g.Player1.Hero.HeroPower[GameTag.COST] = 20;
        return g;
    }
    private static Weapon Arm(Game g, bool own, string id = "CS2_106")
    {
        var p = own ? g.Player1 : g.Player2; var w = (Weapon)Entity.FromCard(p, Cards.FromId(id));
        p.Hero.AddWeapon(w); return w;
    }

    public static void TestProbeReadsTheEngineNotTheText()
    {
        Row(Rider, TriggerKind.HeroAttack, r => r.Fire.Cards == 1 && r.Share == 1);
        Row(Acolyte, TriggerKind.SelfDamage, r => r.Fire.Cards == 1);
        Row(Supplier, TriggerKind.SelfAttack, r => r.Fire.Cards == 1);
        Row(Maiev, TriggerKind.OwnMinion, r => r.Fire.Attack == 3 && r.Fire.Health == 3);
        Row("JAIL_440", TriggerKind.SelfDamage, r => r.Fire.Bodies == 2);
        // Unpriced rows carry the reason: tribe or school, one-shot.
        Status("FIR_913", TriggerKind.OwnSpell, TriggerMeasures.Status.Partial);
        Status("CORE_WC_042", TriggerKind.OwnMinion, TriggerMeasures.Status.Partial);
        Status("JAIL_030", TriggerKind.SelfAttack, TriggerMeasures.Status.SelfRemoving);
        Check(TriggerMeasures.For("CS2_182") == null, "a vanilla minion has no trigger row");
        Check(TriggerMeasures.HeroPowerArms("CS2_017") && TriggerMeasures.HeroPowerArms("CS2_083b") && !TriggerMeasures.HeroPowerArms("CS2_102"), "arming hero powers come from the sweep");
    }
    private static void Row(string id, TriggerKind kind, Func<TriggerMeasures.Row, bool> ok)
    {
        var row = TriggerMeasures.Rows.Single(r => r.Id == id && r.Kind == kind);
        Check(row.Status == TriggerMeasures.Status.Priced && ok(row), id + " row: " + row.Serialize());
    }
    private static void Status(string id, TriggerKind kind, TriggerMeasures.Status status)
    {
        var row = TriggerMeasures.Rows.Single(r => r.Id == id && r.Kind == kind);
        Check(row.Status == status, id + " status: " + row.Serialize());
        Check(TriggerMeasures.For(id)?.All(m => m.Kind != kind) ?? true, id + " is not priced");
    }
    public static void TestProbeMixIsWhatItClaims()
    {
        var schools = TriggerProbe.Spells.Select(id => Cards.FromId(id)[GameTag.SPELL_SCHOOL]).ToArray();
        Check(schools.Distinct().Count() == schools.Length, "one spell per school: " + string.Join(",", schools));
        Check(TriggerProbe.Spells.All(id => Cards.FromId(id).Type == CardType.SPELL) && TriggerProbe.Minions.All(id => Cards.FromId(id).Type == CardType.MINION), "mix types");
        // Every event in the mix is possible on the canonical board: a silently impossible event would read as a condition.
        foreach (var row in TriggerMeasures.Rows.Where(r => r.Id == "TIME_013" || r.Id == "BE_701"))
            Check(row.Share == 1, row.Id + " unconditional trigger fires on the whole mix: " + row.Share);
        // The credit is for future rounds, so a Rush minion can swing at the hero like any other: a minion-only trigger fires on 2 of 3 targets.
        foreach (var id in new[] { "DINO_401", "FIR_953" }) {
            var row = TriggerMeasures.Rows.Single(r => r.Id == id && r.Kind == TriggerKind.SelfAttack);
            Check(Math.Abs(row.Share - 2.0 / 3) < .001, id + ": minion-only trigger: " + row.Share);
        }
        foreach (var id in new[] { "CS3_025", "TSC_030t2" }) {
            var row = TriggerMeasures.Rows.Single(r => r.Id == id && r.Kind == TriggerKind.SelfAttack);
            Check(row.Share == 1, id + ": fires on every target: " + row.Share);
        }
        Check(TriggerMeasures.Rows.Where(r => r.Status == TriggerMeasures.Status.Priced).All(r => r.Share >= TriggerMeasures.MinShare && r.Fire.Any), "priced rows fired on the mix");
        Check(TriggerMeasures.Rows.All(r => r.Status != TriggerMeasures.Status.Error), "no probe error hides behind a status");
    }
    public static void TestTableMatchesTheCardSet()
    {
        var generated = TriggerProbe.Generate().Select(r => r.Serialize()).ToArray();
        var shipped = TriggerMeasures.Rows.Select(r => r.Serialize()).ToArray();
        var stale = generated.Except(shipped).Concat(shipped.Except(generated)).ToArray();
        Check(stale.Length == 0, "regenerate with `hs ai trigger-values --write`: " + string.Join(",", stale.Take(10)));
        var armed = TriggerProbe.ArmingHeroPowers();
        Check(armed.All(TriggerMeasures.HeroPowerArms) && armed.Count > 0, "hero power sweep in sync");
    }
    // The Spider Rider shape: same stats, same cost, and the engine is worth more than the vanilla body.
    public static void TestEngineOutscoresEqualStatVanilla()
    {
        var engine = Position(); Board(engine, false, Rider);
        var plain = Position(); Plain(plain, false, 1, 4, 2);
        double a = Eval().Evaluate(View(engine).Facts), b = Eval().Evaluate(View(plain).Facts);
        Check(a < b, $"an enemy engine costs the bot more than its stat twin: {a} vs {b}");
        Check(Eval(false).Evaluate(View(engine).Facts) == Eval(false).Evaluate(View(plain).Facts), "flag off prices stats only (the bug)");
        // The same body on the bot's side: an NPC holding it values keeping it.
        var mine = Position(); Board(mine, true, Rider);
        var mineP = Position(); Plain(mineP, true, 1, 4, 2);
        Check(Eval().Evaluate(View(mine).Facts) > Eval().Evaluate(View(mineP).Facts), "an own engine is worth keeping");
    }
    // The whole damage class: an enemy body whose hit pays its owner never scores better for the bot than its stat twin, with or
    // without attackers on the bot's side. The first version priced these bodies at no health at all and inverted exactly this.
    public static void TestDamageEnginesAreWorthMoreThanStatTwins()
    {
        var rows = TriggerMeasures.Rows.Where(r => r.Kind == TriggerKind.SelfDamage && r.Status == TriggerMeasures.Status.Priced && r.Fire.Worth > 0).ToArray();
        Check(rows.Length >= 20, "the class is there: " + rows.Length);
        foreach (var row in rows) {
            var card = Cards.FromId(row.Id);
            if (card.Power?.DeathrattleTask != null || card[GameTag.REBORN] != 0 || card.Power?.Aura != null) continue;
            foreach (int attackers in new[] { 0, 3 }) {
                var engine = Position(); var e = Board(engine, false, row.Id);
                var plain = Position(); var t = Plain(plain, false, e.AttackDamage, e.Health, card.Cost);
                if (e.HasTaunt) t[GameTag.TAUNT] = 1;
                if (e.HasDivineShield) t[GameTag.DIVINE_SHIELD] = 1;
                for (int i = 0; i < attackers; i++) { Attacker(engine, 3, 3); Attacker(plain, 3, 3); }
                double a = Eval().Evaluate(View(engine).Facts), b = Eval().Evaluate(View(plain).Facts);
                // With no bot minions a firing that only hits enemies of its owner has nothing to hit, so the two can tie.
                Check(attackers == 0 ? a <= b : a < b, $"{row.Id} with {attackers} attackers: engine {a} must cost the bot more than its twin {b}");
            }
        }
    }
    // Public enablers set the rate; a hidden deck does not. A class with no arming hero power and no weapon gets the small prior.
    public static void TestRateFollowsPublicEnablers()
    {
        var none = Position(enemy: CardClass.MAGE); Board(none, false, Rider);
        var power = Position(enemy: CardClass.DRUID); Board(power, false, Rider);
        var weapon = Position(enemy: CardClass.MAGE); Board(weapon, false, Rider); Arm(weapon, false);
        double n = Term(none), p = Term(power), w = Term(weapon);
        Check(w < p && p < n && n < 0, $"weapon > arming power > nothing: {w} {p} {n}");
        // A record of hero attacks moves it, with or without an enabler in view.
        var record = new TriggerEvidence { Turns = 6, HeroAttacks = 5 };
        Check(Term(none, enemy: record) < n, "a public record of hero attacks raises the estimate");
        // Armed heroes read no record: flat beats it (npc-ai-tuning.md).
        Check(Term(power, enemy: new TriggerEvidence { Turns = 6, HeroAttacks = 0 }) == p, "an armed estimate is flat");
        // Weapon that cannot swing is no enabler.
        var spent = Position(enemy: CardClass.MAGE); Board(spent, false, Rider); var axe = Arm(spent, false); axe.Durability = 0;
        Check(Term(spent) == n, "no durability, no enabler");
    }
    public static void TestHiddenCardsChangeNothing()
    {
        foreach (var id in new[] { Rider, Maiev, Acolyte, "CORE_NEW1_020", "CORE_RLK_083" }) {
            var g = Fixture(CardClass.PRIEST, CardClass.MAGE); Board(g, false, id); Board(g, true, id);
            // A real public record on both sides, then hidden cards in the opponent's hand and deck that differ between twins.
            Cast(g, g.Player1, "CS2_008", g.Player2.Hero); g.Process(EndTurnTask.Any(g.Player1));
            Cast(g, g.Player2, "CS2_008", g.Player1.Hero); g.Process(EndTurnTask.Any(g.Player2));
            Entity.FromCard(g.Player2, Cards.FromId("CS2_182"), zone: g.Player2.HandZone);
            var twin = Variant(g);
            var v = View(g); var w = View(twin);
            Check(v.OpponentEvidence.Spells == 1 && w.OpponentEvidence.Spells == 1 && v.OwnEvidence.Spells == 1, id + ": the record is there");
            double a = Term(g, own: v.OwnEvidence, enemy: v.OpponentEvidence), b = Term(twin, own: w.OwnEvidence, enemy: w.OpponentEvidence);
            Check(a == b, id + ": hidden twins differ");
        }
    }
    private static void Cast(Game g, Controller p, string id, ICharacter target)
    {
        var spell = Entity.FromCard(p, Cards.FromId(id), zone: p.HandZone);
        Check(g.Process(PlayCardTask.SpellTarget(p, spell, target)), id + " cast");
    }
    // Completed turns only, both players in the same frame; a mid-turn replan reads what the turn's first decision read.
    public static void TestEvidenceReadsCompletedTurns()
    {
        var g = Fixture(CardClass.PRIEST, CardClass.MAGE);
        var start = View(g);
        Check(start.OwnEvidence.Turns == 0 && start.OpponentEvidence.Turns == 0, "no turn completed yet, both seats alike: " + start.OwnEvidence.Turns + "/" + start.OpponentEvidence.Turns);
        Cast(g, g.Player1, "CS2_008", g.Player2.Hero);
        Check(View(g).OwnEvidence.Spells == 0, "this turn's spell waits for the turn to end");
        g.Process(EndTurnTask.Any(g.Player1));
        Cast(g, g.Player2, "CS2_008", g.Player1.Hero);
        g.Process(EndTurnTask.Any(g.Player2));
        // The opponent's cast is public, whatever was hidden about it before it was played.
        var view = View(g);
        Check(view.OwnEvidence.Spells == 1 && view.OwnEvidence.Turns == 1, "own record");
        Check(view.OpponentEvidence.Spells == 1 && view.OpponentEvidence.Turns == 1, "opponent record: " + view.OpponentEvidence.Spells + "/" + view.OpponentEvidence.Turns);
    }
    public static void TestFlagOffIsTheBaseEvaluator()
    {
        var g = Position(); Board(g, false, Rider); Board(g, true, Maiev); Board(g, false, Acolyte);
        var f = View(g).Facts;
        Check(new DeckPlanEvaluator(Plan).Evaluate(f) == Eval(false).Evaluate(f), "off equals an evaluator that never heard of it");
        Check(Eval().Evaluate(f) != Eval(false).Evaluate(f), "on moves the value");
        // The whole difference is the term (no paying body on this board), so the term is what reaches a line's score.
        var plain = Position(); Board(plain, false, Rider); Board(plain, true, Maiev);
        var p = View(plain).Facts; var on = Eval(); on.SetTriggerEvidence(default, default);
        Check(Math.Abs(on.Evaluate(p) - Eval(false).Evaluate(p) - on.TriggerTerm(p.Own, p.Opponent)) < 1e-9, "the term reaches the score once");
    }
    public static void TestSubFlagsNarrowOneFamily()
    {
        var attack = Position(); Board(attack, false, Rider);
        var plays = Position(); Board(plays, false, Maiev);
        var hit = Position(); Board(hit, false, Acolyte);
        DeckPlanEvaluator Off(string id) => new DeckPlanEvaluator(Plan) { TriggerFixes = new AiFixes(new[] { id }) };
        Check(Term(attack, Off("trigger-attack")) == 0 && Term(attack) != 0 && Term(attack, Off("trigger-plays")) == Term(attack), "trigger-attack");
        Check(Term(plays, Off("trigger-plays")) == 0 && Term(plays) != 0 && Term(plays, Off("trigger-damage")) == Term(plays), "trigger-plays");
        Check(Term(hit, Off("trigger-damage")) == 0 && Term(hit) != 0 && Term(hit, Off("trigger-attack")) == Term(hit), "trigger-damage");
    }
    public static void TestDrawsRespectCapacity()
    {
        var open = Position(); Board(open, true, Rider);
        var full = Position(); Board(full, true, Rider);
        for (int i = 0; i < 10; i++) Entity.FromCard(full.Player1, Cards.FromId("CS2_182"), zone: full.Player1.HandZone);
        var empty = Position(); Board(empty, true, Rider);
        foreach (var c in empty.Player1.DeckZone.ToArray()) empty.Player1.DeckZone.Remove(c);
        Check(Term(open) > 0 && Term(full) == 0 && Term(empty) == 0, $"no room or no deck, no draw: {Term(open)} {Term(full)} {Term(empty)}");
        // A generated card has no value in a full hand either, including what the card carries.
        var gen = Position(); Board(gen, true, "CORE_EX1_559");
        var genFull = Position(); Board(genFull, true, "CORE_EX1_559");
        for (int i = 0; i < 10; i++) Entity.FromCard(genFull.Player1, Cards.FromId("CS2_182"), zone: genFull.Player1.HandZone);
        Check(Term(gen) > 0 && Term(genFull) == 0, $"generated card and full hand: {Term(gen)} {Term(genFull)}");
    }
    public static void TestNotWorkingNotPriced()
    {
        var silenced = Position(); Board(silenced, false, Rider).Silence();
        Check(Term(silenced) == 0, "silenced");
        var dormant = Position(); Board(dormant, false, Rider)[GameTag.DORMANT] = 1;
        Check(Term(dormant) == 0, "dormant");
        var noAttack = Position(); Board(noAttack, false, Supplier).AttackDamage = 0;
        Check(Term(noAttack) == 0, "a self-attack engine with no Attack does not attack");
        // Doomed: it will not see the rounds the credit is for.
        var doomed = Position(usedMana: 0); var target = Board(doomed, false, Rider);
        Cast(doomed, doomed.Player1, "CS2_063", target);
        var zero = new DeckPlanEvaluator(Plan) { TriggerFixes = new AiFixes(), ZeroDoomed = true };
        Check(Term(doomed, zero) == 0 && Term(doomed, new DeckPlanEvaluator(Plan) { TriggerFixes = new AiFixes() }) != 0, "doomed");
    }
    public static void TestHarmfulFiringNeverCostsItsOwner()
    {
        var g = Position(); Board(g, true, "CORE_NEW1_020"); Board(g, true, "CS2_182"); Board(g, true, "CS2_182");
        Check(Term(g) >= 0, "the owner times its own spells");
        // The real board decides: a Pyromancer alone hurts its owner less than one among friends.
        var alone = Position(); Board(alone, true, "CORE_NEW1_020"); Plain(alone, false, 1, 4, 2); Plain(alone, false, 1, 4, 2);
        var crowd = Position(); Board(crowd, true, "CORE_NEW1_020"); Plain(crowd, false, 1, 4, 2); Plain(crowd, false, 1, 4, 2);
        for (int i = 0; i < 2; i++) Plain(crowd, true, 2, 2, 2);
        Check(Term(alone) > Term(crowd), $"own board reach: {Term(alone)} {Term(crowd)}");
        // The opponent chooses when a hit lands, so a firing that hurts the owner does count.
        var hurt = TriggerMeasures.Rows.First(r => r.Kind == TriggerKind.SelfDamage && r.Status == TriggerMeasures.Status.Priced && r.Fire.Worth < 0);
        var mine = Position(); Board(mine, true, hurt.Id);
        Check(Term(mine) < 0, hurt.Id + " hurts its owner when hit: " + Term(mine));
    }
    public static void TestStacksSaturate()
    {
        var g = Position(); for (int i = 0; i < 7; i++) Board(g, false, Maiev);
        var one = Position(); Board(one, false, Maiev);
        double single = -Term(one), raw = 7 * single;
        Check(raw > DeckPlanEvaluator.DeathCap, "the stack passes the cap: " + raw);
        Check(Math.Abs(-Term(g) - (DeckPlanEvaluator.DeathCap + (raw - DeckPlanEvaluator.DeathCap) * .25)) < 1e-9, $"past the cap a stack pays a quarter: {Term(g)} from {raw}");
    }
    // A hit on a body that hands its owner a card earns nothing, and the kill still removes the whole body.
    public static void TestChipOnAPayingBodyEarnsNothing()
    {
        var whole = Position(); Board(whole, false, Acolyte);
        var chipped = Position(); var m = Board(chipped, false, Acolyte); m.Damage = 2;
        var gone = Position();
        double w = Eval().Evaluate(View(whole).Facts), c = Eval().Evaluate(View(chipped).Facts), k = Eval().Evaluate(View(gone).Facts);
        Check(w == c, $"chipping an Acolyte earns the bot nothing: {w} {c}");
        Check(k > c, $"the kill still pays: {k} {c}");
        Check(Eval(false).Evaluate(View(whole).Facts) < Eval(false).Evaluate(View(chipped).Facts), "flag off rewards the chip (the bug)");
        // A Taunt must be chipped through.
        var taunt = Position(); Board(taunt, false, Acolyte)[GameTag.TAUNT] = 1;
        var cut = Position(); var t = Board(cut, false, Acolyte); t[GameTag.TAUNT] = 1; t.Damage = 2;
        Check(Eval().Evaluate(View(taunt).Facts) < Eval().Evaluate(View(cut).Facts), "a Taunt keeps its chip credit");
        // Summons land on the board, where the evaluator sees them: that body keeps the ordinary price.
        var tower = Position(); Board(tower, false, "JAIL_440");
        var cracked = Position(); var h = Board(cracked, false, "JAIL_440"); h.Damage = 2;
        Check(Eval().Evaluate(View(tower).Facts) < Eval().Evaluate(View(cracked).Facts), "a summoner keeps its ordinary health price");
    }
    // Reply leaves read the same term in the bot's frame.
    public static void TestReplyValueKeepsTheBotFrame()
    {
        double Reply(Action<Game> place)
        {
            var g = Position(); place(g);
            var leaf = View(g); var ended = leaf.Apply(leaf.Actions.Single(a => a.Type == PlayerTaskType.END_TURN));
            var reply = ended.Determinize(new[] { "CS2_182" }, 91).ForReply();
            return Eval().ReplyValue(reply.Facts, leaf.Facts, reply.ForReplyHand(leaf.Facts));
        }
        double enemyEngine = Reply(g => Board(g, false, Rider)), enemyTwin = Reply(g => Plain(g, false, 1, 4, 2));
        double ownEngine = Reply(g => Board(g, true, Rider)), ownTwin = Reply(g => Plain(g, true, 1, 4, 2));
        Check(enemyEngine < enemyTwin, $"enemy engine in a reply: {enemyEngine} {enemyTwin}");
        Check(ownEngine > ownTwin, $"own engine in a reply: {ownEngine} {ownTwin}");
    }
    // Decisions, through the real search.
    private static PlannedTurn Search(Game g, bool on, params string[] off)
    { var job = TurnPlanner.Job(View(g), Profile(on, off), Plan, 71, 10000); Run(job); return job.Result; }
    private static bool Hits(PlannedTurn t, int source, int target) => t.Line.Any(a => a.Source == source && a.Target == target);
    // One attacker kills either a 1/4 engine or its equal-stat twin; both boards public. The engine goes.
    public static void TestRemovesEnemyEngineOverStatTwin()
    {
        int offTwin = 0;
        foreach (bool engineFirst in new[] { true, false }) {
            var g = Position();
            var attacker = Attacker(g, 5, 8); Minion rider, twin;
            if (engineFirst) { rider = Board(g, false, Rider); twin = Plain(g, false, 1, 4, 2); }
            else { twin = Plain(g, false, 1, 4, 2); rider = Board(g, false, Rider); }
            var on = Search(g, true);
            Check(Hits(on, attacker.Id, rider.Id), "kills the engine, not the twin: " + string.Join(";", on.Line));
            if (Hits(Search(g, false), attacker.Id, twin.Id)) offTwin++;
        }
        // Red check: flag off sees two equal bodies, so board order decides and it takes the twin in one order.
        Check(offTwin > 0, "flag off is blind to the engine");
    }
    private static Minion Attacker(Game g, int attack, int health)
    { var m = Board(g, true, "CS2_120", true); m.AttackDamage = attack; m.BaseHealth = health; return m; }
    // The damage class through the search: one big attacker kills either a paying body or its twin. Takes the engine in both orders.
    // Only a body whose firing dies with it: killing Acolyte or Tower of Ghouls fires them one last time (a card, two Ghouls), so
    // leaving them and hitting the twin can be right, and the search says so.
    public static void TestRemovesDamageEngineOverStatTwin()
    {
        foreach (var id in new[] { "EX1_399" })
            foreach (bool engineFirst in new[] { true, false }) {
                var g = Position();
                var attacker = Attacker(g, 9, 12); Minion engine, twin;
                var printed = Cards.FromId(id);
                if (engineFirst) { engine = Board(g, false, id); twin = Plain(g, false, printed.ATK, printed.Health, printed.Cost); }
                else { twin = Plain(g, false, printed.ATK, printed.Health, printed.Cost); engine = Board(g, false, id); }
                var on = Search(g, true);
                Check(Hits(on, attacker.Id, engine.Id), id + " goes, not the twin: " + string.Join(";", on.Line));
            }
    }
    // The same board against a class with no public enabler is a much smaller reason: removal of a stat twin is not forced by it.
    public static void TestEnablerlessEngineIsCheap()
    {
        var mage = Position(enemy: CardClass.MAGE); Board(mage, false, Rider);
        var druid = Position(enemy: CardClass.DRUID); Board(druid, false, Rider);
        Check(Math.Abs(Term(mage)) < Math.Abs(Term(druid)) / 2, "Spider Rider in a deck with no visible way to attack is worth little");
    }
    // An NPC holding an engine protects it: with one body to trade, it trades the other.
    public static void TestProtectsOwnEngine()
    {
        int offTrades = 0;
        foreach (bool engineFirst in new[] { true, false }) {
            var g = Position(CardClass.DRUID, CardClass.MAGE); Minion rider, twin;
            if (engineFirst) { rider = Board(g, true, Rider, true); twin = Plain(g, true, 2, 4, 2, true); }
            else { twin = Plain(g, true, 2, 4, 2, true); rider = Board(g, true, Rider, true); }
            rider.AttackDamage = 2;
            var enemy = Plain(g, false, 3, 2, 2); enemy[GameTag.TAUNT] = 1;
            var on = Search(g, true);
            Check(!Hits(on, rider.Id, enemy.Id) && Hits(on, twin.Id, enemy.Id), "the twin takes the trade: " + string.Join(";", on.Line));
            if (Hits(Search(g, false), rider.Id, enemy.Id)) offTrades++;
        }
        Check(offTrades > 0, "flag off is blind to its own engine");
    }
    // An NPC holding an engine uses it: an armed hero attacks to draw. This is the search's own simulation, and passes with the flag
    // off too; it guards that the new term does not take the swing away.
    public static void TestUsesOwnEngine()
    {
        var g = Position(CardClass.ROGUE, CardClass.MAGE); Board(g, true, Rider);
        var axe = Arm(g, true); axe.AttackDamage = 1; g.Player1.Hero.IsExhausted = false;
        var enemy = Plain(g, false, 1, 8, 3);
        var on = Search(g, true);
        Check(on.Line.Any(a => a.Type == PlayerTaskType.HERO_ATTACK), "armed hero with a draw engine attacks: " + string.Join(";", on.Line));
    }
    public static void TestHotPathDoesNotAllocate()
    {
        var g = Position(); for (int i = 0; i < 3; i++) Board(g, false, Rider);
        for (int i = 0; i < 3; i++) Board(g, true, Maiev);
        var f = View(g).Facts; var ev = Eval(); double total = 0;
        for (int i = 0; i < 1000; i++) total += ev.TriggerTerm(f.Own, f.Opponent);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) total += ev.TriggerTerm(f.Own, f.Opponent);
        Check(GC.GetAllocatedBytesForCurrentThread() == before && total != 0, "trigger term allocates nothing");
    }
}
