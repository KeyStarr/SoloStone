using System;

// The two members of TransportInvariants.SelfChecks and Runner that the shared transport sources need. Both classes are
// partial: transport-invariants has the rest of each, native-match links only this.
namespace TransportInvariants;

internal static partial class SelfChecks
{
    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("SELF-CHECK: " + message);
    }
}

internal sealed partial class Runner
{
    internal const string Output = "sim/out/transport";
}
