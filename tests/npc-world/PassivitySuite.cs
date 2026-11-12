using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// Engine-backed regressions; fixed search output isolates the mistake layer from search randomness.
// These cover NPCs that played too passively: a Warrior skipped turns with Armor Up and affordable plays available, and kept
// Axe in hand as a "board clear" instead of equipping it. Causes: (1) the mistake layer's softmax could choose END_TURN while
// other actions remained; (2) the board-clear hold bonus applied to weapons and minions, not only spells; (3) a hold could drop
// a free (0 mana) play; (4) mistakes were priced one ply deep, so a loss bound could still let a larger real loss through.
// 1926 is a reduced live search cap (see RoundTwoSuite.Profile).
internal static class PassivitySuite
{
    internal static Game Fixture(int mana, params string[] hand)
    {
        var cards = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList();
        var game = new Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.MAGE,
            Player1Deck = cards, Player2Deck = cards.ToList(), StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            History = true, Logging = false, RandomSeed = 7 });
        game.StartGame();
        foreach (var player in new[] { game.Player1, game.Player2 })
            foreach (var card in player.HandZone.ToArray()) { player.HandZone.Remove(card); player.SetasideZone.Add(card); }
        game.Player1.BaseMana = mana; game.Player1.UsedMana = 0;
        foreach (string id in hand) Entity.FromCard(game.Player1, Cards.FromId(id), zone: game.Player1.HandZone);
        return game;
    }
    internal static FairBotState View(Game game) => FairBotState.Create(game, 1, 71, DeckStyle.Control,
        new FairOptions { RemoveOpponentSecrets = true, TrackOwnDeck = true, MaterializeOwnDeckTriggers = true });
    internal static DeckPlan Plan() => AutoDeckPlan.Derive(NpcDeckLibrary.Parse(Program.Data("decks-v1.json")).Find("npc-warrior-quest"));
    private static PlannedTurn Search(FairBotState view, DeckPlan plan, int cap)
    {
        var job = TurnPlanner.Job(view, NpcCatalog.All[0], plan, 71, cap);
        while (!job.IsDone) job.StepNode();
        return job.Result;
    }
    private static GoldenDecision Decision(PlannedTurn turn, bool sparse) => new GoldenDecision(turn.Line[0], turn.Value,
        turn.Nodes, turn.Line.Count, turn.Stop.ToString(), null, sparse
            ? new[] { Pair(turn.Line[0], turn.Value) } : turn.Candidates);
    private static KeyValuePair<BotAction, double> Pair(BotAction action, double value) => new KeyValuePair<BotAction, double>(action, value);
    internal static void Run()
    {
        RoundThreeSuite.Run();
        RoundTwo();
        var plan = Plan(); var skills = SkillCatalog.Parse(Program.Data("skills-v1.json"));
        Program.Require(!plan.Holds.Any(h => h.CardIds.Contains("TLC_478")), "Axe is not held as one-shot removal");
        Program.Require(NpcDeckLibrary.Parse(Program.Data("decks-v1.json")).Decks.SelectMany(d => AutoDeckPlan.Derive(d).Holds)
            .Any(h => h.Condition == HoldCondition.AoeWideBoard && h.CardIds.All(id => Cards.FromId(id).Type == CardType.SPELL)), "real AoE spells retain holds");
        foreach (string id in new[] { "TLC_478", "CS2_106" })
        {
            var view = View(Fixture(5, id)); var result = Search(view, plan, 1926);
            Program.Require(result.Line.Any(a => a.Type == PlayerTaskType.PLAY_CARD) && result.Line.Any(a => a.Type == PlayerTaskType.HERO_ATTACK), "equip and swing " + id);
            var end = view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
            Program.Require(result.Value > view.Apply(end).Evaluate(new DeckPlanEvaluator(plan)), "weapon beats pass " + id);
        }
        // A larger equipped weapon must not be overwritten by the smaller Axe just to spend mana.
        var replacement = Fixture(10, "CS2_112", "TLC_478");
        var arcanite = replacement.Player1.HandZone.Single(c => c.Card.Id == "CS2_112");
        replacement.Process(replacement.Player1.Options().First(a => a.Source == arcanite));
        var replaceView = View(replacement); var replacePlan = Search(replaceView, plan, 1926);
        int axe = replacement.Player1.HandZone.Single(c => c.Card.Id == "TLC_478").Id;
        Program.Require(!replacePlan.Line.Any(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == axe), "retain stronger equipped weapon");
        var clear = View(Fixture(6, "CATA_581"));
        Program.Require(Search(clear, plan, 1926).Line.All(a => a.Type != PlayerTaskType.PLAY_CARD), "hold clear on empty board");
        var coin = View(Fixture(4, "GAME_005", "EDR_979"));
        var setup = Search(coin, plan, 1926);
        Program.Require(setup.Line[0].Source == coin.Facts.Own.Hand.Single(c => c.CardId == "GAME_005").EntityId
            && setup.Line.Any(a => a.Source == coin.Facts.Own.Hand.Single(c => c.CardId == "EDR_979").EntityId), "Coin funds Ancient of Yore");
        var sparse = Decision(setup, true);
        foreach (var skill in skills.Tiers)
            for (int seed = 0; seed < 256; seed++)
            {
                var pilot = Wrap(sparse, skill, plan, seed);
                var result = pilot.Decide(coin, Context(coin));
                Program.Require(result.Action.Equals(sparse.Action), "unsearched Armor Up cannot inherit Coin continuation: " + skill.Id);
                Program.Require(pilot.PerturbFailures == 0, "mistake selection must not fail open");
            }
        // Isolate the +2.2 Armor Up/pass gap: an affordable quest now correctly takes priority.
        var early = View(Fixture(2, "CS2_200", "CATA_585", "END_020", "CORE_CS2_108", "CATA_586", "CATA_581"));
        var earlyResult = Decision(Search(early, plan, 1926), false);
        Program.Require(earlyResult.Action.Type == PlayerTaskType.HERO_POWER, "early best is Armor Up");
        foreach (var skill in skills.Tiers)
            for (int seed = 0; seed < 256; seed++)
                Program.Require(Wrap(earlyResult, skill, plan, seed).Decide(early, Context(early)).Action.Type != PlayerTaskType.END_TURN,
                    "no softmax whole-turn pass: " + skill.Id);
        // Frame budget cuts use the same guarded candidate set.
        var cut = Wrap(sparse, skills.Find("novice"), plan, 7).Begin(coin, Context(coin));
        Program.Require(((INpcSearchContinuation)cut).StopSearch(SearchStop.Clock).Action.Equals(sparse.Action), "clock cut preserves setup");
        Console.WriteLine("PASS passivity: weapons, clear hold, replacement, Coin continuation, early pass, clock cut");
    }
    internal static void RoundTwo()
    {
        var failures = new List<string>();
        foreach (var check in new Action[] { FreePlay, NoSoftmaxPass, IndirectClears, RoundTwoSuite.MasterTrades, RoundTwoSuite.Holds, RoundTwoSuite.Owl, RoundTwoSuite.Width, RoundTwoSuite.BoundedKinds })
            try { check(); } catch (Exception e) { failures.Add(e.Message); Console.WriteLine(e.Message); }
        Program.Require(failures.Count == 0, "round-2 regressions: " + failures.Count);
    }
    private static void FreePlay()
    {
        var game = Fixture(0, "CORE_EX1_606"); game.Player1.HandZone[0][GameTag.COST] = 0;
        var view = View(game); var plan = Plan(); var raw = Decision(Search(view, plan, 1926), false);
        var end = view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
        Program.Require(raw.Action.Type == PlayerTaskType.PLAY_CARD && raw.Value > view.Apply(end).Evaluate(new DeckPlanEvaluator(plan)) + 4,
            "free Shield Block has beneficial searched continuation");
        foreach (var skill in SkillCatalog.Parse(Program.Data("skills-v1.json")).Tiers)
            for (int seed = 0; seed < 2000; seed++)
                Program.Require(Wrap(raw, skill, plan, seed).Decide(view, Context(view)).Action.Type != PlayerTaskType.END_TURN,
                    "free Shield Block must not be held or softmax-passed: " + skill.Id + "/" + seed);
        Console.WriteLine("PASS free Shield Block: 14000 selectors");
    }
    private static void IndirectClears()
    {
        var library = NpcDeckLibrary.Parse(Program.Data("decks-v1.json"));
        foreach (string id in new[] { "DINO_429", "TIME_209t2", "EDR_820" })
        {
            var plans = library.Decks.Where(d => d.Cards.Any(c => c.Id == id)).Select(AutoDeckPlan.Derive).ToArray();
            Program.Require(plans.Length > 0 && plans.All(p => !p.Holds.Any(h => h.Condition == HoldCondition.AoeWideBoard && h.CardIds.Contains(id))),
                "indirect/development spell must not inherit clear hold: " + id);
        }
        foreach (string id in new[] { "CS2_032", "CATA_582", "TIME_619t2" })
            Program.Require((CardRoleClassifier.Classify(id).Auto & AutoRole.ImmediateClear) != 0,
                "immediate clear retains its role even with a secondary buff: " + id);
        var game = Fixture(4, "DINO_429"); game.Player1.Hero.HeroPower.IsExhausted = true;
        var enemy = (Minion)Entity.FromCard(game.Player2, Cards.FromId("CS2_182"), zone: game.Player2.BoardZone);
        enemy[GameTag.ATK] = 5; enemy[GameTag.HEALTH] = 4;
        var plan = AutoDeckPlan.Derive(library.Find("npc-elemental-mage"));
        var result = Search(View(game), plan, 1926);
        Program.Require(result.Line[0].Type == PlayerTaskType.PLAY_CARD, "Sheep Mask debuffs lone 5/4");
        Console.WriteLine("PASS indirect clears and lone Sheep Mask target");
    }
    private static void NoSoftmaxPass()
    {
        var game = Fixture(3, "CORE_EX1_606"); game.Player1.Hero.HeroPower.IsExhausted = true;
        var view = View(game); var plan = Plan(); var raw = Decision(Search(view, plan, 1926), false);
        var skill = new SkillProfile("noise-only", 1, 0, 1, NpcCatalog.All[0].Budget, 100, 3, 1, new MistakeRule[0], 20);
        Program.Require(raw.Action.Type == PlayerTaskType.PLAY_CARD, "paid Shield Block beats pass");
        for (int seed = 0; seed < 256; seed++)
            Program.Require(Wrap(raw, skill, plan, seed).Decide(view, Context(view)).Action.Type != PlayerTaskType.END_TURN,
                "softmax cannot end turn even when only a paid card remains");
        Console.WriteLine("PASS sole paid card: softmax cannot pass");
    }
    private static GoldenContext Context(FairBotState view) => new GoldenContext(view.Facts.Turn, 1, 71, NpcCatalog.All[0]);
    private static SkilledPilot Wrap(GoldenDecision decision, SkillProfile skill, DeckPlan plan, int seed) =>
        new SkilledPilot(skill, seed, plan, v => new FixedPilot(decision), "passivity");
    private sealed class FixedPilot : IFrameSlicedGoldenPilot
    {
        private readonly GoldenDecision decision;
        public FixedPilot(GoldenDecision decision) { this.decision = decision; }
        public GoldenDecision Decide(FairBotState view, GoldenContext context) => decision;
        public IReadOnlyList<int> Mulligan(IGoldenMulliganView view, GoldenContext context) => new int[0];
        public IGoldenDecisionJob Begin(FairBotState view, GoldenContext context) => new FixedJob(decision);
    }
    private sealed class FixedJob : IGoldenDecisionJob
    {
        public FixedJob(GoldenDecision decision) { Result = decision; }
        public bool IsDone => true;
        public GoldenDecision Result { get; }
        public string LethalResult => "none";
        public bool PlanReused => false;
        public void StepNode() { }
    }
    internal static void Distribution() => RoundTwoDistribution.Run();
}
