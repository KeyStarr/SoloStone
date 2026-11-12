using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class PaladinTempoTests
{
    public static void TestActiveAuraRetainsFutureValue()
    {
        var game = Fixture(CardClass.PALADIN, CardClass.PALADIN);
        var aura = Entity.FromCard(game.Player1, Cards.FromId("END_011"), zone: game.Player1.HandZone);
        Check(game.Process(game.Player1.Options().Single(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD
            && t.Source.Id == aura.Id)), "cast Acceleration Aura");
        var evaluator = new DeckPlanEvaluator(DeckPlans.PurePaladin);
        var active = View(game);
        aura[GameTag.QUEST_PROGRESS] = 2;
        var lastTick = View(game);
        Check(active.Evaluate(evaluator) > lastTick.Evaluate(evaluator),
            "three public future mana ticks must be worth more than one");
        Check(lastTick.Evaluate(evaluator) == View(Variant(game)).Evaluate(evaluator),
            "hidden cards cannot price the aura");
    }

    public static void TestAuraDurationInvalidatesCachedTurn()
    {
        var game = Fixture(CardClass.PALADIN, CardClass.PALADIN);
        var aura = Entity.FromCard(game.Player1, Cards.FromId("END_011"), zone: game.Player1.HandZone);
        Check(game.Process(game.Player1.Options().Single(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD
            && t.Source.Id == aura.Id)), "cast Acceleration Aura");
        string before = GoldenPublicKey.For(View(game));
        aura[GameTag.QUEST_PROGRESS] = 2;
        Check(before != GoldenPublicKey.For(View(game)), "public remaining aura duration must invalidate a cached turn");
    }
}
