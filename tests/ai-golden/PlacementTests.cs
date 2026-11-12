using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class PlacementTests
{
    private static BotAction[] Placements(Game game, int source)
    {
        var beam = new TurnBeam(View(game), s => s.Evaluate(), 100, 6, false);
        Run(beam);
        return beam.Leaves.Concat(beam.Progress).Where(n => n.Actions.Count > 0)
            .Select(n => n.Actions[0]).Where(a => a.Source == source).Distinct()
            .OrderBy(a => a.Position).ToArray();
    }
    public static void TestPlainMinionOnlyUsesRightEnd()
    {
        for (int count = 0; count <= 6; count++) {
            var game = Fixture(CardClass.SHAMAN);
            for (int i = 0; i < count; i++) Board(game, true, "CS2_182");
            var card = Entity.FromCard(game.Player1, Cards.FromId("CS2_120"), zone: game.Player1.HandZone);
            game.Player1.UsedMana = 10 - card.Cost;
            var plays = Placements(game, card.Id);
            Check(plays.Length == 1 && plays[0].Position == count, "plain minion must search only right end: " + count);
        }
    }
    public static void TestAdjacencyMinionGoesBetweenBothPartners()
    {
        foreach (string id in new[] { "EX1_565", "EX1_162" }) {
            var game = Fixture(CardClass.SHAMAN);
            Board(game, true, "CS2_182"); Board(game, true, "CS2_120");
            var card = Entity.FromCard(game.Player1, Cards.FromId(id), zone: game.Player1.HandZone);
            game.Player1.UsedMana = 10 - card.Cost;
            string expected = null;
            foreach (var variant in new[] { game, Variant(game) }) foreach (int slice in new[] { 1, 1000 }) {
                var job = TurnPlanner.Job(View(variant), NpcCatalog.All[2], DeckPlans.ZeeShaman, 71);
                Run(job, slice);
                var play = job.Result.Line.First(a => a.Source == card.Id);
                Check(play.Position == 1, "adjacency minion must buff both partners: " + PlannerTests.Describe(job.Result));
                var result = PlannerTests.Describe(job.Result);
                if (expected == null) expected = result; else Check(result == expected, "placement leaked hidden state or slice size");
            }
        }
    }
    public static void TestExistingAuraAndTauntKeepBoundedPlacements()
    {
        foreach (bool aura in new[] { true, false }) {
            var game = Fixture(CardClass.SHAMAN);
            Board(game, true, "CS2_182"); Board(game, true, "CS2_182");
            Board(game, true, aura ? "EX1_162" : "CS2_182"); Board(game, true, "CS2_182");
            var card = (Minion)Entity.FromCard(game.Player1, Cards.FromId("CS2_120"), zone: game.Player1.HandZone);
            card.HasTaunt = !aura;
            game.Player1.UsedMana = 10 - card.Cost;
            var plays = Placements(game, card.Id);
            Check(plays.Length == (aura ? 5 : 3) && plays.First().Position == 0 && plays.Last().Position == 4,
                "adjacency keeps every slot; plain Taunt keeps bounded placements");
            Check(!aura || plays.Any(a => a.Position == 2) && plays.Any(a => a.Position == 3), "both sides of existing aura remain available");
        }
    }
    public static void TestAuraWithoutTagAndHighestValuePartner()
    {
        var game = Fixture(CardClass.SHAMAN);
        for (int i = 0; i < 4; i++) Board(game, true, "CS2_182");
        game.Player1.BoardZone[2].AttackDamage = 20;
        var card = Entity.FromCard(game.Player1, Cards.FromId("CATA_153t"), zone: game.Player1.HandZone);
        Check(card.Card[GameTag.ADJACENT_BUFF] == 0 && card.Card[(GameTag)1576] == 0, "fixture must require aura inspection");
        game.Player1.UsedMana = 10 - card.Cost;
        Check(Placements(game, card.Id).Select(a => a.Position).SequenceEqual(new[] { 0, 1, 2, 3, 4 }),
            "aura without tags must retain every legal placement");
    }
    public static void TestSilencedAuraDoesNotExpandOrdinaryPlay()
    {
        var game = Fixture();
        var wolf = Board(game, true, "EX1_162"); wolf[GameTag.SILENCED] = 1;
        Board(game, true, "CS2_182");
        var card = Entity.FromCard(game.Player1, Cards.FromId("CS2_120"), zone: game.Player1.HandZone);
        game.Player1.UsedMana = 10 - card.Cost;
        Check(Placements(game, card.Id).Select(a => a.Position).SequenceEqual(new[] { 2 }),
            "silenced supplier must not add branches");
    }
    public static void TestAdjacencyBattlecryTagKeepsInterior()
    {
        var game = Fixture(); Board(game, true, "CS2_182"); Board(game, true, "CS2_120");
        var card = Entity.FromCard(game.Player1, Cards.FromId("CORE_EX1_058"), zone: game.Player1.HandZone);
        game.Player1.UsedMana = 10 - card.Cost;
        Check(Placements(game, card.Id).Select(a => a.Position).SequenceEqual(new[] { 0, 1, 2 }),
            "adjacency battlecry metadata must retain interior placement without an aura");
    }
}
