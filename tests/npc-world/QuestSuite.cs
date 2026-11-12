using System;
using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class QuestSuite
{
    internal static void Run()
    {
        var library = NpcDeckLibrary.Parse(Program.Data("decks-v1.json"));
        var skills = SkillCatalog.Parse(Program.Data("skills-v1.json"));
        var decks = library.Admitted.Where(d => d.Cards.Any(c => QuestValue.IsQuest(c.Id))).ToArray();
        Program.Require(decks.Length == 8, "update explicit NPC quest coverage when shipped inventory changes");
        int decisions = 0;
        foreach (var deck in decks) {
            var plan = AutoDeckPlan.Derive(deck);
            foreach (string id in deck.Cards.Where(c => QuestValue.IsQuest(c.Id)).Select(c => c.Id)) {
                var role = CardRoleClassifier.Classify(id);
                Program.Require(role.Auto == AutoRole.Quest && role.Roles == CardRole.Quest,
                    id + " reward text cannot turn quest into removal, minion development or payoff");
                Program.Require(!plan.Holds.Any(h => h.CardIds.Contains(id)), id + " no quest hold");
                var offered = new MulliganView(new[] { new MulliganCard(1, id, 1), new MulliganCard(2, "CS2_200", 6) },
                    true, deck.ExpandedCardIds(), CardClass.MAGE);
                foreach (var skill in skills.Tiers)
                    for (int seed = 0; seed < 64; seed++)
                        Program.Require(new SkilledMulligan(plan, skill, seed).Keep(offered).Contains(1), id + " every tier keeps quest");

                var fillers = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList();
                var game = new Game(new GameConfig { Player1HeroClass = (CardClass)Enum.Parse(typeof(CardClass), deck.Class), Player2HeroClass = CardClass.MAGE,
                    Player1Deck = fillers, Player2Deck = fillers.ToList(), StartPlayer = 1, Shuffle = false,
                    SkipMulligan = true, History = true, Logging = false, RandomSeed = 7 });
                game.StartGame();
                foreach (var c in game.Player1.HandZone.ToArray()) { game.Player1.HandZone.Remove(c); game.Player1.SetasideZone.Add(c); }
                game.Player1.BaseMana = 1;
                var quest = Entity.FromCard(game.Player1, Cards.FromId(id), zone: game.Player1.HandZone);
                var profile = NpcCatalog.All[0];
                var view = FairBotState.Create(game, 1, 71, DeckStyle.Tempo,
                    new FairOptions { RemoveOpponentSecrets = true, TrackOwnDeck = true, MaterializeOwnDeckTriggers = true });
                foreach (var skill in skills.Tiers)
                    for (int seed = 0; seed < 4; seed++) {
                        var pilot = new SkilledPilot(new GoldenPilot(profile, plan), null, skill, seed);
                        var decision = pilot.Decide(view, new GoldenContext(game.Turn, 0, 71, profile));
                        Program.Require(decision.Action.Type == PlayerTaskType.PLAY_CARD && decision.Action.Source == quest.Id,
                            deck.Id + " " + id + " " + skill.Id + " must start quest");
                        decisions++;
                    }
                var alwaysHold = new SkillProfile("quest-test", 1, 0, 1, profile.Budget, 100, 10, 1,
                    new[] { new MistakeRule(MistakeKind.HoldTooLong, 1, 1000) }, 1000);
                for (int seed = 0; seed < 16; seed++) {
                    var pilot = new SkilledPilot(new GoldenPilot(profile, plan), null, alwaysHold, seed);
                    Program.Require(pilot.Decide(view, new GoldenContext(game.Turn, 0, 71, profile)).Action.Source == quest.Id,
                        "guaranteed hold mistake and unbounded noise cannot drop " + id);
                    Program.Require(pilot.MistakeCount(MistakeKind.HoldTooLong) == 0 && pilot.SoftmaxDeviations == 0,
                        "quest bypass is explicit, not a lucky mistake roll");
                }
            }
        }
        Console.WriteLine("PASS quests: " + decks.Length + " NPC decks; " + decisions + " tier/seed early plays; all-tier mulligans");
        OwlFirstOpportunity(library, skills);
    }

    private static void OwlFirstOpportunity(NpcDeckLibrary library, SkillCatalog skills)
    {
        // Journal 04f3c5ed, sequence 30: Storm is first drawn on state turn 10 (Hunter turn 5).
        var deck = library.Admitted.Single(d => d.Id == "npc-hunter-quest");
        var plan = AutoDeckPlan.Derive(deck);
        var profile = new NpcProfile("npc.g.0942", 1, "owl828", deck.Id, null, 3,
            PersonalityCatalog.Parse(Program.Data("personalities-v1.json")).Find("grinder").ToPersonality(1),
            skills.Find("casual").Budget, plan.Id);
        var g = new Game(new GameConfig { Player1HeroClass = CardClass.HUNTER, Player2HeroClass = CardClass.MAGE,
            Player1Deck = deck.ExpandedCardIds().Select(Cards.FromId).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), StartPlayer = 1,
            Shuffle = false, SkipMulligan = true, History = true, Logging = false, RandomSeed = 7 });
        g.StartGame(); g.Turn = 10;
        foreach (var p in new[] { g.Player1, g.Player2 }) {
            foreach (var c in p.HandZone.ToArray()) { p.HandZone.Remove(c); p.SetasideZone.Add(c); }
            p.BaseMana = 5; p.UsedMana = 0;
            int count = p == g.Player1 ? 20 : 22;
            while (p.DeckZone.Count > count) { var c = p.DeckZone[0]; p.DeckZone.Remove(c); p.SetasideZone.Add(c); }
        }
        g.Player1.Hero.Damage = 1;
        foreach (string id in new[] { "TLC_830", "TLC_366", "DINO_138", "TLC_366", "CATA_554", "TLC_823", "TLC_EVENT_400" })
            Entity.FromCard(g.Player1, Cards.FromId(id), zone: g.Player1.HandZone);
        for (int i = 0; i < 7; i++) Entity.FromCard(g.Player2, Cards.FromId("CS2_182"), zone: g.Player2.HandZone);
        var hyena = (Minion)Entity.FromCard(g.Player1, Cards.FromId("BAR_035t"), zone: g.Player1.BoardZone);
        hyena.IsExhausted = false; hyena[GameTag.NUM_TURNS_IN_PLAY] = 1; g.SummonedMinions.Clear();
        var root = FairBotState.Create(g, 1, 71, DeckStyle.Tempo,
            new FairOptions { RemoveOpponentSecrets = true, TrackOwnDeck = true, MaterializeOwnDeckTriggers = true });
        root.FaultReporter = message => { throw new Exception(message); };
        int storm = root.Facts.Own.Hand.Single(c => c.CardId == "TLC_EVENT_400").EntityId;
        foreach (int cap in new[] { 1926, 2500, 7000 }) {
            var job = TurnPlanner.Job(root, profile, plan, 71, cap);
            var scheduler = new NodeScheduler(100000); scheduler.Enqueue(job);
            while (scheduler.Step(1000) == NodeStepResult.Running) { }
            Program.Require(job.IsDone && job.Result.Line.Any(a => a.Source == storm), "Owl plays Storm at first affordable turn, cap " + cap);
        }
        foreach (int seed in new[] { 23, 71, 101, 202 }) {
            var pilot = SkilledPilot.Wrap(profile, skills.Find("casual"), seed, plan);
            var state = root; bool played = false;
            for (int i = 0; i < 12; i++) {
                var decision = pilot.Decide(state, new GoldenContext(g.Turn, i, 71, pilot.Profile));
                played |= decision.Action.Source == storm;
                if (decision.Action.Type == PlayerTaskType.END_TURN) break;
                state = state.Apply(decision.Action);
            }
            Program.Require(played && state.Facts.Own.Quests.Any(c => c.CardId == "TLC_EVENT_400"), "casual Owl retains first-opportunity Storm play, seed " + seed);
        }
        Console.WriteLine("PASS Owl828 first Storm draw: state turn 10; caps 1926/2500/7000; four casual seeds");
    }
}
