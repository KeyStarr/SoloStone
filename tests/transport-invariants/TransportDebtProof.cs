using System.Text.Json;

namespace TransportInvariants;

internal static class TransportDebtProof
{
    internal static void Run(string only = null)
    {
        if (only == null || only == "shadow-trial") ShadowTrial();
        if (only == "shapes") HistoryShapeProof.DebtShapes();
        foreach (string name in new[] { "helper-accepted", "runtime", "rewind-actor" })
        {
            if (only != null && name != only) continue;
            var replay = JsonSerializer.Deserialize<TransportReplay>(File.ReadAllText(
                $"tests/transport-invariants/replays/transport-debt/{name}.json"), Program.Json);
            var runner = new Runner();
            runner.Run(replay);
            if (name == "rewind-actor")
                SelfChecks.Check(runner.Rewinds > 0, "rewind actor regression restores the discarded timeline");
            SelfChecks.Check(runner.Completed.Values.Sum() == 1 && runner.ViolationCount == 0,
                name + " transport debt regression: " + string.Join("; ", runner.Buckets.Keys));
            Console.WriteLine("PASS transport debt " + name);
        }
    }
    private static void ShadowTrial()
    {
        foreach (string length in new[] { "CAP_405tb1", "CAP_405tb2", "CAP_405tb3" })
        {
            var game = OfflinePoolTestRecipes.NewGame();
            var p = game.Player1;
            var shadow = SabberStoneCore.Actions.Generic.DrawCard(p, SabberStoneCore.Model.Cards.FromId("CORE_RLK_567"));
            var trial = SabberStoneCore.Actions.Generic.DrawCard(p, SabberStoneCore.Model.Cards.FromId(length));
            var tags = new[] { SabberStoneCore.Enums.GameTag.MODULAR_ENTITY_PART_1, SabberStoneCore.Enums.GameTag.MODULAR_ENTITY_PART_2 };
            trial[tags[0]] = SabberStoneCore.Model.Cards.FromId("CAP_405t5").AssetId;
            trial[tags[1]] = SabberStoneCore.Model.Cards.FromId("CAP_405t8").AssetId;
            SelfChecks.Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(p, trial)), "play trial " + length);
            var transformed = game.IdEntityDic[shadow.Id];
            SelfChecks.Check(transformed.Card.Id == length && tags.All(tag => transformed[tag] == trial[tag]),
                "Shadow of Demise retains ordered trial components: " + length);
        }
        Console.WriteLine("PASS Shadow of Demise copies all three trial recipes");
    }
}
