using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class QuestValueTests
{
    internal static readonly string[] ReleaseQuests = { "TLC_602", "TLC_817", "TLC_513", "TLC_631", "TLC_830",
        "TLC_EVENT_400", "TLC_460", "TLC_426", "TLC_433", "TLC_229", "TLC_239", "TLC_446", "END_017" };
    private static DeckPlanEvaluator Evaluator() => new DeckPlanEvaluator(DeckPlans.DragonWarrior);
    internal static Spell Install(Game g, string id, bool own = true)
    {
        var p = own ? g.Player1 : g.Player2;
        var quest = (Spell)Entity.FromCard(p, Cards.FromId(id));
        Check(QuestRules.Install(quest), "install " + id); return quest;
    }
    private static FairBotState Settle(FairBotState state)
    {
        for (int i = 0; state.HasPendingChoice && i < 12; i++) state = state.Apply(state.Actions.First());
        Check(!state.HasPendingChoice, "settled choices"); return state;
    }
    private static void Near(double a, double b, string why) => Check(Math.Abs(a - b) < 1e-8, why + ": " + a + "/" + b);

    // Also usable with the baseline production sources: these prove the original symptom fails there.
    public static void TestEveryReleaseQuestPlayedOnFirstTwoTurns()
    {
        foreach (string id in ReleaseQuests)
        foreach (int mana in new[] { 1, 2 }) {
            var g = Fixture(); g.Turn = mana * 2 - 1; g.Player1.BaseMana = mana;
            var q = Entity.FromCard(g.Player1, Cards.FromId(id), zone: g.Player1.HandZone);
            var state = View(g); var eval = Evaluator();
            double pass = state.ForReplyEndTurn().Evaluate(eval);
            double play = Settle(state.Apply(state.Actions.First(a => a.Source == q.Id))).ForReplyEndTurn().Evaluate(eval);
            Check(play > pass, id + " mana=" + mana + " play=" + play + " pass=" + pass);
            var job = TurnPlanner.Job(state, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 1926); Run(job);
            Check(job.Result.Line.Any(a => a.Source == q.Id), id + " must be played: " + PlannerTests.Describe(job.Result));
        }
    }

    public static void TestWarriorQuestBeforeDevelopmentAndTurnSixRepro()
    {
        foreach (int turn in new[] { 1, 6, 16 }) {
            var g = Fixture(); g.Turn = turn; g.Player1.BaseMana = 1;
            var quest = Entity.FromCard(g.Player1, Cards.FromId("TLC_602"), zone: g.Player1.HandZone);
            Entity.FromCard(g.Player1, Cards.FromId("CS2_171"), zone: g.Player1.HandZone);
            var state = View(g);
            var job = TurnPlanner.Job(state, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 1926); Run(job);
            Check(job.Result.Line.First().Source == quest.Id, "Warrior starts quest instead of wasting another turn");
            var played = state.Apply(job.Result.Line.First());
            Check(played.Facts.Own.Quests.Single().CardId == "TLC_602", "quest is public asset");
            var ended = played.ForReplyEndTurn();
            Check(DeckPlanEvaluator.Tag(ended.Facts.Own.Quests.Single(), GameTag.QUEST_PROGRESS) == 1, "surviving own turn advances");
            Check(ended.Evaluate(Evaluator()) > played.Evaluate(Evaluator()), "survival progress earns value");
        }
    }

    public static void TestPublicSymmetryFairnessAndContinuationKeys()
    {
        foreach (string id in ReleaseQuests) {
            var own = Fixture(); Install(own, id);
            var enemy = Fixture(); Install(enemy, id, false);
            var a = View(own); var b = View(enemy); var eval = Evaluator();
            Near(eval.QuestAssets(a.Facts.Own, a.Facts.Own.Hand, true),
                eval.QuestAssets(b.Facts.Opponent, b.Facts.Opponent.Hand, false), id + " symmetric public quest");
            var changed = Variant(enemy); var c = View(changed);
            Near(b.Evaluate(eval), c.Evaluate(eval), id + " hidden variants must score identically");
            Check(GoldenPublicKey.For(b) == GoldenPublicKey.For(c), id + " key hides private identities/RNG");
            var quest = enemy.Player2.SecretZone.First(q => q.Card.IsQuest); quest.QuestProgress++;
            Check(View(enemy).Evaluate(eval) < b.Evaluate(eval), id + " opponent progress is harmful");
            Check(GoldenPublicKey.For(View(enemy)) != GoldenPublicKey.For(b), id + " progress invalidates reuse");
        }
    }

    public static void TestNpcConditionsEarnOnlyActualEngineProgress()
    {
        // One real action for each NPC condition; Priest's two component quests are independent.
        foreach (var pair in new[] {
            new[] { "TLC_817t", "CORE_CS1_130" }, new[] { "TLC_817t2", "JAIL_940" },
            new[] { "TLC_513", "TLC_518" }, new[] { "TLC_631", "DS1_185" },
            new[] { "TLC_830", "CS2_171" }, new[] { "TLC_EVENT_400", "CS2_171" },
            new[] { "TLC_460", "CATA_484" }
        }) {
            var g = Fixture(); var quest = Install(g, pair[0]); Board(g, false, "CS2_200");
            var card = Entity.FromCard(g.Player1, Cards.FromId(pair[1]), zone: g.Player1.HandZone);
            var state = View(g); var eval = Evaluator();
            var action = state.Actions.First(a => a.Source == card.Id
                && (pair[0] != "TLC_631" || a.Target == g.Player2.Hero.Id));
            var after = Settle(state.Apply(action));
            var q = after.Facts.Own.Quests.Single();
            Check(DeckPlanEvaluator.Tag(q, GameTag.QUEST_PROGRESS) == 1, pair[0] + " real condition " + pair[1]);
            Check(eval.QuestAssets(after.Facts.Own, after.Facts.Own.Hand, true) >
                eval.QuestAssets(state.Facts.Own, state.Facts.Own.Hand, true), pair[0] + " progress credit");
            // Nonqualifying minions must not get an invented quest-action bonus.
            var before = View(g).Evaluate(eval); quest.QuestProgress = 0;
            Near(before, View(g).Evaluate(eval), "no authoritative mutation from fair simulation");
            quest.QuestProgress = quest.QuestTotalProgress - 1;
            var ready = View(g);
            // Static evaluator pin: since starved-replies a 1926 cap replies here, and for TLC_817t2 it and the full tier search both prefer Armor Up.
            var job = TurnPlanner.Job(ready, NpcCatalog.All[0].WithFixes(new AiFixes(new[] { "starved-replies" })), DeckPlans.DragonWarrior, 71, 1926); Run(job);
            Check(job.Result.Line.Any(a => a.Source == card.Id), pair[0] + " planner chooses sensible completing action");
        }
    }

    public static void TestRogueProgressPlayBeatsPassing()
    {
        var g = Fixture(CardClass.ROGUE); Install(g, "TLC_513"); g.Player1.BaseMana = 2;
        g.Player1.Hero.HeroPower.IsExhausted = true;
        var card = Entity.FromCard(g.Player1, Cards.FromId("TLC_518"), zone: g.Player1.HandZone);
        var state = View(g); var eval = Evaluator();
        var after = state.Apply(state.Actions.First(a => a.Source == card.Id));
        Check(after.Evaluate(eval) > state.Evaluate(eval), "Interrogation investment beats holding at zero progress: "
            + after.Evaluate(eval) + "/" + state.Evaluate(eval));
        var job = TurnPlanner.Job(state, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 1926); Run(job);
        Check(job.Result.Line.Any(a => a.Source == card.Id), "planner advances Rogue quest");
    }

    public static void TestCompletionTransfersValueToRewardAndUsesIt()
    {
        foreach (var recipe in QuestValue.Recipes.Where(r => r.Id.StartsWith("TLC_") && r.Id != "TLC_EVENT_400"
            && r.Id != "TLC_426")) {
            var g = Fixture(); var quest = Install(g, recipe.Id); quest.QuestProgress = quest.QuestTotalProgress - 1;
            var eval = Evaluator(); double before = View(g).Evaluate(eval);
            Check(QuestRules.Advance(quest), recipe.Id + " completion");
            var state = View(g);
            Check(state.Facts.Own.Quests.Count == 0, recipe.Id + " pending value removed");
            var reward = state.Facts.Own.Hand.Single(c => c.CardId == recipe.Reward);
            Check(state.Evaluate(eval) >= before, recipe.Id + " completing must not lose reward value");
            var played = Settle(state.Apply(state.Actions.First(a => a.Source == reward.EntityId)));
            Check(played.Evaluate(eval) > state.Evaluate(eval), recipe.Id + " use reward instead of hoarding: "
                + played.Evaluate(eval) + "/" + state.Evaluate(eval));
            var job = TurnPlanner.Job(state, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 1926); Run(job);
            Check(job.Result.Line.Any(a => a.Source == reward.EntityId), recipe.Id + " planner uses reward");
        }
    }

    public static void TestParallelStagesRepeatableAndMulligan()
    {
        var g = Fixture(); Install(g, "TLC_817");
        Check(View(g).Facts.Own.Quests.Count == 2, "parallel Priest components exposed");
        var first = g.Player1.SecretZone.First(q => q.Card.Id == "TLC_817t");
        var second = g.Player1.SecretZone.First(q => q.Card.Id == "TLC_817t2");
        QuestRules.Advance(first, 4); double half = View(g).Evaluate(Evaluator()); QuestRules.Advance(second, 4);
        Check(g.Player1.HandZone.Single().Card.Id == "TLC_817t5", "halves combine through engine");
        Check(View(g).Evaluate(Evaluator()) > half, "second half/combined reward gains value");
        var repeat = Fixture(); var quest = Install(repeat, "TLC_426"); quest.QuestProgress = 5;
        double before = View(repeat).Evaluate(Evaluator()); QuestRules.Advance(quest);
        var after = View(repeat);
        Check(quest.QuestProgress == 0 && after.Facts.Own.Quests.Count == 1 && after.Facts.Own.QuestEffects.Count == 1,
            "repeatable resets and retains public stack");
        Check(after.Evaluate(Evaluator()) > before, "wrap never loses already earned reward");
        foreach (string id in ReleaseQuests) {
            var view = new MulliganView(new[] { new MulliganCard(1, id, 1), new MulliganCard(2, "CS2_200", 6) }, false,
                new[] { id, "CS2_200" }, CardClass.MAGE);
            Check(new DeckPlanMulligan(DeckPlans.DragonWarrior).Keep(view).Contains(1), id + " kept even by unrelated handwritten plan");
        }
    }

    public static void TestOpponentCompletionKnownRewardAndFullHand()
    {
        foreach (bool full in new[] { false, true }) {
            var g = Fixture(); var quest = Install(g, "TLC_602", false); quest.QuestProgress = 9;
            if (full) for (int i = 0; i < 10; i++) Entity.FromCard(g.Player2, Cards.FromId("CS2_182"), zone: g.Player2.HandZone);
            var before = View(g); var eval = Evaluator();
            Check(g.Process(EndTurnTask.Any(g.Player1)) && g.Process(EndTurnTask.Any(g.Player2)), "real opponent survival completes");
            var after = View(g);
            Check(after.Facts.Opponent.Quests.Count == 0, "completed public quest removed");
            if (full) {
                Check(after.Facts.Opponent.PendingQuestRewards.Single() == "TLC_602t", "full hand retains publicly owed reward");
                var entry = g.Player2.PendingQuestRewards.Dequeue();
                g.Player2.PendingQuestRewards.Enqueue(("CS2_029", entry.CreatorId));
                Near(eval.QuestAssets(after.Facts.Opponent, after.Facts.Opponent.Hand, false),
                    eval.QuestAssets(View(g).Facts.Opponent, View(g).Facts.Opponent.Hand, false), "queued private definition is never read");
            }
            else {
                Check(after.Facts.Opponent.Hand.Any(c => c.CardId == "TLC_602t"), "public quest recipe reconstructs fixed reward");
                // Mutate only face-down definitions; CloneExact intentionally drops wire-packet history.
                foreach (var actor in g.Player2.HandZone) actor.Card = Cards.FromId("CS2_029");
                g.SetRandomSeed(99812);
                var hidden = View(g);
                Near(after.Evaluate(eval), hidden.Evaluate(eval), "hidden reward identity cannot change public inference");
                Check(GoldenPublicKey.For(after) == GoldenPublicKey.For(hidden), "known reward key ignores hidden replacement");
            }
            Check(eval.QuestAssets(after.Facts.Opponent, after.Facts.Opponent.Hand, false) >
                eval.QuestAssets(before.Facts.Opponent, before.Facts.Opponent.Hand, false), "enemy completion retains threat");
        }
    }


    public static void TestOpponentPriestCombinationAndRewardLeavesHand()
    {
        var g = Fixture(); Install(g, "TLC_817", false);
        foreach (var quest in g.Player2.SecretZone.Where(q => q.Card.IsQuest).ToArray()) QuestRules.Advance(quest, 4);
        var state = View(g);
        Check(state.Facts.Opponent.Hand.Single().CardId == "TLC_817t5", "combined reward inferred from two public promises");
        var reward = g.Player2.HandZone.Single();
        g.Player2.HandZone.Remove(reward); g.Player2.DeckZone.Add(reward);
        Check(!View(g).Facts.Opponent.Hand.Any(c => c.CardId == "TLC_817t5"), "shuffled reward no longer counted as held");
    }

    public static void TestFullThenEmptyStageAndSidequestChoice()
    {
        var g = Fixture(); var quest = Install(g, "END_017");
        for (int i = 0; i < 10; i++) Generic.AddHandPhase(g.Player1, Entity.FromCard(g.Player1, Cards.FromId("GAME_005")));
        Check(g.Process(g.Player1.Options().First(t => t.Source?.Id == g.Player1.HandZone[0].Id)), "settle full-hand trigger through a real action");
        Check(quest.QuestProgress == 1, "fill advances first stage");
        foreach (var c in g.Player1.HandZone.ToArray()) Check(g.Process(g.Player1.Options().First(t => t.Source?.Id == c.Id)), "play held Coin");
        Check(g.Player1.HandZone.Single().Card.Id == "END_017t", "empty advances second stage and delivers Tick and Tock");
        var state = View(g); var reward = state.Facts.Own.Hand.Single(); var eval = Evaluator();
        Check(state.Apply(state.Actions.First(a => a.Source == reward.EntityId)).Evaluate(eval) > state.Evaluate(eval), "use staged reward");

        var side = Fixture(); var sq = Install(side, "TLC_EVENT_400"); sq.QuestProgress = 2;
        var beast = Entity.FromCard(side.Player1, Cards.FromId("CS2_171"), zone: side.Player1.HandZone);
        var start = View(side); var done = Settle(start.Apply(start.Actions.First(a => a.Source == beast.Id)));
        Check(done.Facts.Own.Quests.Count == 0 && done.Facts.Own.Hand.Count == 1, "sidequest settles real Zombeast choices");
        Check(done.Evaluate(eval) > start.Evaluate(eval), "sidequest completion gains value");
        var zombeast = done.Facts.Own.Hand.Single();
        Check(done.Apply(done.Actions.First(a => a.Source == zombeast.EntityId)).Evaluate(eval) > done.Evaluate(eval), "use actual chosen Zombeast");
    }
}
