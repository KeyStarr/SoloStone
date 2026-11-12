internal static class CurrentV14LadderChecks
{
    internal static void Run()
    {
        ResultsTests.Run();
        ResultsRegressionTests.Run();
        RewardPlacementTests.Run();
        TierRewardPlanTests.Run();
        MilestoneChecks.Run();

        string root = Path.Combine(Path.GetTempPath(), "hs-current-ladder-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            StartTrapChecks.Run(root);
            LadderLedgerChecks.RunCurrent(root);
            RatingPlacementChecks.RunCurrent(root);
            NpcMilestoneChecks.Run(root);
            AchievementLadderChecks.Run(root);
            RivalResultsChecks.Run(root);
            CasualModeChecks.Run(root);
            Console.WriteLine("PASS casual mode: no rank change, Ranked rewards and XP, hidden Casual rating, results, preference, tamper, portraits");
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine("PASS current V14 ladder checks");
    }
}
