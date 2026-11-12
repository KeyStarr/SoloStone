using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-pastvar-minions", ProofKind.PoweredUp)]
    private static void PastVarietyMinionsGlow()
    {
        foreach (string id in new[] { "BRM_004", "AT_017" })
            Scenario(id, (fixture, card, met) => {
                if (met) fixture.Draw("EX1_561");
                else fixture.Summon("EX1_561", fixture.Player);
            }, (fixture, card) => PlayCardTask.Minion(fixture.Player, card),
            (fixture, card) => id == "BRM_004"
                ? ((SabberStoneCore.Model.Entities.Minion)card).Health == 3
                : ((SabberStoneCore.Model.Entities.Minion)card).HasTaunt);
    }
}
