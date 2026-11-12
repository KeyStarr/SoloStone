using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Model;

internal static class OfflinePoolTests
{
    internal static void Run()
    {
        var legacy = new OfflinePoolDeclaration("Z_LEGACY", 1, Requirement());
        var cards = OfflinePoolAttribute.Collect(new[] { legacy }, new[] { typeof(GroupB), typeof(GroupA) });
        Check(cards.Select(c => c.CardId).SequenceEqual(new[] { "Z_LEGACY", "A_LOCAL", "Z_LOCAL" }),
            "legacy order retained, independent group declarations appended ordinally");
        Check(ReferenceEquals(cards[0], legacy) && cards[1].Pools.Single().Name == "local",
            "local requirements reach the aggregate without a shared registration");
        Check(cards.Select(c => c.CardId).SequenceEqual(OfflinePoolAttribute.Collect(new[] { legacy },
            new[] { typeof(GroupA), typeof(GroupB) }).Select(c => c.CardId)), "reflection order independent");
        Reject(() => OfflinePoolAttribute.Collect(new[] { legacy }, new[] { typeof(GroupA), typeof(GroupA) }), "Duplicate");
        Reject(() => OfflinePoolAttribute.Collect(new[] { new OfflinePoolDeclaration("A_LOCAL", 9, Requirement()) },
            new[] { typeof(GroupA) }), "Duplicate");
        Reject(() => OfflinePoolAttribute.Collect(new[] { new OfflinePoolDeclaration("OTHER", 2, Requirement()) },
            new[] { typeof(GroupA) }), "Duplicate");
        foreach (var type in new[] { typeof(Missing), typeof(Empty), typeof(BadSignature), typeof(Instance),
            typeof(Generic), typeof(BadIdentity) })
            Reject(() => OfflinePoolAttribute.Collect(Array.Empty<OfflinePoolDeclaration>(), new[] { type }), "Invalid offline pool");
        Console.WriteLine("PASS local offline pool discovery: stable append; missing/invalid providers; local/legacy duplicate identities");
    }

    private static PoolRequirement Requirement() => new("local", OfflinePoolFilter.Random(), new[] { "MAGE" });
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Reject(Action action, string message)
    {
        try { action(); } catch (InvalidOperationException error) when (error.Message.Contains(message)) { return; }
        throw new Exception("Expected rejection: " + message);
    }
    private static class GroupB
    {
        [OfflinePool("Z_LOCAL", 3)] private static PoolRequirement[] Pools() => new[] { Requirement() };
    }
    private static class GroupA
    {
        [OfflinePool("A_LOCAL", 2)] private static PoolRequirement[] Pools() => new[] { Requirement() };
    }
    private static class Missing
    {
        [OfflinePool("MISSING", 4)] private static PoolRequirement[] Pools() => null;
    }
    private static class Empty
    {
        [OfflinePool("EMPTY", 4)] private static PoolRequirement[] Pools() => Array.Empty<PoolRequirement>();
    }
    private static class BadSignature
    {
        [OfflinePool("BAD", 4)] private static void Pools(int unused) { }
    }
    private sealed class Instance
    {
        [OfflinePool("BAD", 4)] private PoolRequirement[] Pools() => new[] { Requirement() };
    }
    private static class Generic
    {
        [OfflinePool("BAD", 4)] private static PoolRequirement[] Pools<T>() => new[] { Requirement() };
    }
    private static class BadIdentity
    {
        [OfflinePool("", 0)] private static PoolRequirement[] Pools() => new[] { Requirement() };
    }
}
