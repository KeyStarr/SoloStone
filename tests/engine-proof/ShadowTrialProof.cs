// Moved from tests/transport-invariants/TransportDebtProof.cs: pure engine, no client.
internal static class ShadowTrialProof
{
    internal static void Run()
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
            Core2026Proof.Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(p, trial)), "play trial " + length);
            var transformed = game.IdEntityDic[shadow.Id];
            Core2026Proof.Check(transformed.Card.Id == length && tags.All(tag => transformed[tag] == trial[tag]),
                "Shadow of Demise retains ordered trial components: " + length);
        }
        Console.WriteLine("PASS Shadow of Demise copies all three trial recipes");
    }
}
