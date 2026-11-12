// Owner 2026-10-30: test and showcase loaners (and legacy harness-only loaners) start only on Sandbox saves; Core
// starter loaners (#57) start on every save. The bootstrap tray uses the same AllowsTestLoaners gate.
internal static class TestLoanerGateChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }

    internal static void Run(string root)
    {
#if ENGINE_SOURCE
        string dir = Path.Combine(root, "test-loaner-gate"); Directory.CreateDirectory(dir);
        string[] tests = { LocalDecks.TestDecisionsId, LocalDecks.TestShowcaseId, "test-standard-b11-mage", "mage" };
        string starter = LocalDecks.PlayerCoreStarterDecks.First().Id;
        bool Starts(LocalProfile profile, string key)
        {
            try { return NativeUIProjection.ValidateStart(profile, key, LocalDecks.Get(key), false) == null; }
            catch (InvalidOperationException error) when (error.Message == NativeUIProjection.TestLoanerRefusal) { return false; }
        }
        using (var normal = new LocalProfile(Path.Combine(dir, "normal.xml"), new ProfileOptions()))
        {
            Check(!normal.IsSandbox && !NativeUIProjection.AllowsTestLoaners(normal), "a normal save is not a Sandbox save");
            Check(tests.All(key => !Starts(normal, key)), "a normal save refuses test, showcase and harness-only loaners");
            Check(Starts(normal, starter), "a normal save still starts Core starter loaners");
        }
        foreach (var options in new[] { new ProfileOptions { AllCards = true }, new ProfileOptions { InfiniteGold = true },
            new ProfileOptions { AllDecks = true }, new ProfileOptions { StartLegendOne = true, LegendRating = 1 } })
        {
            using var sandbox = new LocalProfile(Path.Combine(dir, "sandbox-" + Guid.NewGuid() + ".xml"), options);
            Check(sandbox.IsSandbox && NativeUIProjection.AllowsTestLoaners(sandbox) && tests.All(key => Starts(sandbox, key))
                && Starts(sandbox, starter), "a Sandbox save starts test loaners and starters");
        }
        Check(!NativeUIProjection.AllowsTestLoaners(null), "no profile, no test loaners");
#endif
    }
}
