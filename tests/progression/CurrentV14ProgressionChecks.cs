internal static class CurrentV14ProgressionChecks
{
    internal static void Run()
    {
        ActiveMetaChecks.Run();
        PlayerDefaultChecks.Run();
        ShowcaseChecks.Run();
        AchievementCatalogChecks.Run();
        AchievementEvaluatorContractChecks.Run();

        string root = Path.Combine(Path.GetTempPath(), "hs-current-progression-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            AchievementPersistenceChecks.Run(root);
            HeroSkinRewardChecks.Run(root);
            PremiumTrackChecks.Run(root);
            CurrentProfileContractChecks.Run(root);
            PremiumChecks.GrantOnlyReplacementChecks(root);
            BackupChecks.Run(root);
            ReviewRegressions.Run(root);
            SeenChecks.Run(root);
            RivalChecks.Run(root);
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine("PASS current V14 progression/profile checks");
    }
}
