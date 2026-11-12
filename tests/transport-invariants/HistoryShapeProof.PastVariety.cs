using PegasusGame;
using SabberStoneCore.Enums;

namespace TransportInvariants;

internal static partial class HistoryShapeProof
{
    internal static void FutureFullMetadata()
    {
        var declared = Seed();
        Feed(declared, Meta(HistoryMeta.Type.TARGET, 0, 999), Full(999, "CS2_182"));
        Check(!declared.Findings.Any(f => f.Rule == "META_REF"), "same-packet FULL declares metadata entity before queued tasks execute");
        foreach (string kind in new[] { "missing", "tag", "show", "later-packet" })
        {
            var invalid = Seed();
            var target = Meta(HistoryMeta.Type.TARGET, 0, 999);
            if (kind == "tag") Feed(invalid, target, TC(999, GameTag.ZONE, (int)SabberStoneCore.Enums.Zone.PLAY));
            else if (kind == "show") Feed(invalid, target, Show(999, "CS2_182", SabberStoneCore.Enums.Zone.PLAY));
            else Feed(invalid, target);
            if (kind == "later-packet") Feed(invalid, Full(999, "CS2_182"));
            Check(invalid.Findings.Any(f => f.Rule == "META_REF"), "metadata still rejects undeclared entity: " + kind);
        }
        Console.WriteLine("PASS metadata entity declarations: same-packet FULL; missing/TAG/SHOW/later-packet rejection");
    }
}
