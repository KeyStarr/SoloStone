using SabberStoneCore.Actions;
using SabberStoneCore.Enums;

internal static class Batch13TagIsolationProof
{
    internal static void Run()
    {
        Func<GameTag, bool>[] facilities = { PayloadRules.IsInternalTag, RepeatRules.IsInternalTag,
            DiscoverRealRules.IsInternalTag, CthunRules.IsInternalTag };
        for (int value = 30000; value < 40000; value++)
        {
            var tag = (GameTag)value;
            Core2026Proof.Check(facilities.Count(owns => owns(tag)) <= 1, "batch 13 facilities share private tag " + value);
        }
        Console.WriteLine("PASS batch 13 private tags: payload/repeat and Discover-real/C'Thun isolated");
    }
}
