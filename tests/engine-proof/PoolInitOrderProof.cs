using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Model;

// Regression test for a plugin crash at load. Set pool classes call OfflinePoolDeclarations from their own static
// initializers, so if one of them ran first (as in Unity/Mono) the card aggregate read a Declarations list that was
// still null. Static-constructor order is per process, so each type is forced first in a child process; one process
// could only ever test the order it happens to start in.
internal static class PoolInitOrderProof
{
    internal static void Run()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var declarations = typeof(Cards).Assembly.GetTypes()
            .Where(type => type.GetMember("Declarations", flags).Length > 0).ToArray();
        if (declarations.Length < 13)
            throw new Exception($"pool init order: expected at least 13 declaration types, found {declarations.Length}");

        var types = declarations.Concat(new[] { typeof(OfflinePoolDeclarations), typeof(Cards) })
            .Distinct().OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
        var children = types.Select(type => Task.Run(() => RunChild(type))).ToArray();
        var failures = Task.WhenAll(children).GetAwaiter().GetResult().Where(result => result != null).ToArray();
        if (failures.Length > 0) throw new Exception(string.Join(Environment.NewLine, failures));
        Console.WriteLine($"PASS pool init order: {types.Length} fresh-process entry points ({declarations.Length} declaration types)");
    }

    private static string RunChild(Type type)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath) {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet")
            start.ArgumentList.Add(typeof(PoolInitOrderProof).Assembly.Location);
        start.ArgumentList.Add("--pool-init-order-child");
        start.ArgumentList.Add(type.FullName);
        using var child = Process.Start(start);
        var output = child.StandardOutput.ReadToEndAsync();
        var error = child.StandardError.ReadToEndAsync();
        bool exited = child.WaitForExit(30000);
        if (!exited) { child.Kill(); child.WaitForExit(); }
        return exited && child.ExitCode == 0 ? null
            : $"FAIL pool init order {type.FullName} ({(exited ? $"exit {child.ExitCode}" : "timeout")}):\n{output.Result}{error.Result}";
    }

    internal static void Child(string typeName)
    {
        var type = typeof(Cards).Assembly.GetType(typeName, throwOnError: true);
        RuntimeHelpers.RunClassConstructor(type.TypeHandle);
        CheckLoadedCards(typeName);
    }

    // Keep beforefieldinit aggregate access out of the JIT compilation of the forced entry point.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CheckLoadedCards(string typeName)
    {
        var cards = Cards.All.ToArray();
        var declarations = OfflinePoolDeclarations.Cards;
        if (cards.Length == 0 || cards.Any(card => card == null)
            || declarations == null || declarations.Count == 0 || declarations.Any(card => card == null))
            throw new Exception($"pool init order {typeName}: empty or null card/declaration entries");
    }
}
