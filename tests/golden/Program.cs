using System;
using System.Linq;
using System.Reflection;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--lethal-benchmark")
            return LethalBenchmarks.Run(args.Skip(1).ToArray());
        bool audit = Environment.GetEnvironmentVariable("AUDIT") == "1";
        var tests = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.Name.EndsWith("Tests", StringComparison.Ordinal))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => (audit ? m.Name.StartsWith("Audit20261031_", StringComparison.Ordinal) : m.Name.StartsWith("Test", StringComparison.Ordinal)) && m.ReturnType == typeof(void)
                    && m.GetParameters().Length == 0 && !m.ContainsGenericParameters)
                .OrderBy(m => m.Name, StringComparer.Ordinal)).ToArray();
        if (Environment.GetEnvironmentVariable("GOLDEN_DIGEST_CHILD") == "1")
            tests = tests.Where(t => t.Name == "TestCrossProcessDeterminism").ToArray();
        // The child only needs its own test; running every test before it made the 60 s child timeout depend on suite size.
        else if (Environment.GetEnvironmentVariable("HS_LETHAL_TEST_CHILD") == "1")
            tests = tests.Where(t => t.Name == "TestLethalAcrossProcesses").ToArray();
        else if (args.Length > 0)
            tests = tests.Where(t => args.Any(a => (t.DeclaringType.Name + "." + t.Name).Contains(a))).ToArray();
        int failed = 0;
        foreach (var test in tests)
        {
            string name = test.DeclaringType.Name + "." + test.Name;
            try { test.Invoke(null, null); Console.WriteLine("PASS " + name); }
            catch (Exception error)
            {
                failed++;
                Console.Error.WriteLine("FAIL " + name + ": " + (error.InnerException ?? error));
            }
        }
        Console.WriteLine((tests.Length - failed) + "/" + tests.Length + " golden tests passed");
        return failed == 0 && tests.Length != 0 ? 0 : 1;
    }
}
