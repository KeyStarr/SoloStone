using System.Reflection;
using System.Reflection.Emit;

if (GroupProofs.TryRun(args)) return;
OfflinePoolTests.Run();
var temp = Path.Combine(Path.GetTempPath(), "hs-groups-" + Guid.NewGuid());
Directory.CreateDirectory(temp);
try
{
    File.WriteAllText(Path.Combine(temp, "b.txt"), "B_001 B_002\n");
    File.WriteAllText(Path.Combine(temp, "a.txt"), "A_001\n");
    var batches = StandardBatchData.Load(temp);
    Check(batches.Keys.SequenceEqual(new[] { "a", "b" }) && batches.Values.Sum(ids => ids.Length) == 3,
        "new manifests participate in ordered totals without a call list");
    File.WriteAllText(Path.Combine(temp, "duplicate.txt"), "B_002");
    Reject(() => StandardBatchData.Load(temp), "duplicate");
    File.WriteAllText(Path.Combine(temp, "duplicate.txt"), "");
    Reject(() => StandardBatchData.Load(temp), "Empty");
    File.WriteAllText(Path.Combine(temp, "duplicate.txt"), "BAD-ID");
    Reject(() => StandardBatchData.Load(temp), "Invalid");
}
finally { Directory.Delete(temp, true); }

foreach (bool duplicate in new[] { false, true })
{
    var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Invalid" + duplicate), AssemblyBuilderAccess.Run);
    var module = assembly.DefineDynamicModule("Proofs");
    var type = module.DefineType("BadProof");
    for (int i = 0; i < (duplicate ? 2 : 1); i++)
    {
        var method = type.DefineMethod("Run" + i, MethodAttributes.Public | MethodAttributes.Static,
            typeof(void), duplicate ? Type.EmptyTypes : new[] { typeof(int) });
        method.GetILGenerator().Emit(OpCodes.Ret);
        method.SetCustomAttribute(new CustomAttributeBuilder(typeof(GroupProofAttribute).GetConstructors(
            BindingFlags.Instance | BindingFlags.Public).Single(), new object[] { "same-name", ProofKind.Facility }));
    }
    type.CreateType();
    Reject(() => GroupProofs.Discover(assembly), duplicate ? "Duplicate" : "Invalid");
}
GroupProofs.Run(ProofKind.Facility);
GroupProofs.Run(ProofKind.PoweredUp);
Check(Probe.Calls.SequenceEqual(new[] { "a", "z", "glow" }), "group dispatch is ordered, exactly once and kind-specific");
RejectArgument(() => GroupProofs.TryRun(new[] { "--registry-a-only", "--registry-z-only" }));
RejectArgument(() => GroupProofs.TryRun(new[] { "--not-registered-only" }));
RejectArgument(() => GroupProofs.TryRun(new[] { "--legacy-only", "--not-registered-only" }, allowUnmatched: true));
if (args.Length == 2 && args[0] == "--merge-check")
{
    var batches = StandardBatchData.Load(args[1]);
    Check(batches.Values.SelectMany(ids => ids).Order().SequenceEqual(new[] { "MERGE_A", "MERGE_B" }),
        "both independently added manifests contribute to totals");
    Console.WriteLine("PASS merged test-only batches: groups=" + batches.Count + "; ids=" + batches.Values.Sum(ids => ids.Length));
}
Console.WriteLine("PASS parallel group discovery, dispatch and manifest validation");

static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static void Reject(Action action, string message)
{
    try { action(); } catch (InvalidOperationException error) when (error.Message.Contains(message)) { return; }
    throw new Exception("Expected rejection: " + message);
}
static void RejectArgument(Action action)
{
    try { action(); } catch (ArgumentException) { return; }
    throw new Exception("Expected invalid group flag rejection");
}
internal static class Probe
{
    internal static readonly List<string> Calls = new();
    [GroupProof("registry-z")] internal static void Z() => Calls.Add("z");
    [GroupProof("registry-a")] internal static void A() => Calls.Add("a");
    [GroupProof("registry-glow", ProofKind.PoweredUp)] internal static void Glow() => Calls.Add("glow");
}
