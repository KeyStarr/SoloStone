using System.Xml.Linq;

internal static class DeathKnightTipChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }

    internal static void Run(string root)
    {
        string[] names = {
            "HAS_SEEN_DK_DECK_BUILDING_INTRO_TUTORIAL",
            "HAS_SEEN_DK_DECK_BUILDING_TRIPLE_RUNES_POPUP",
            "HAS_SEEN_DK_DECK_BUILDING_RUNE_SLOT_AVAILABLE_POPUP",
            "HAS_SEEN_DK_DECK_BUILDING_CANNOT_ADD_RUNES_POPUP"
        };
        for (int bit = 0; bit < names.Length; bit++)
            Check(NativeUIProjection.DeathKnightTipBit(names[bit]) == bit, "DK subkey " + (964 + bit) + " maps to bit " + bit);
        foreach (string name in new[] { null, "", "FTUE", "HAS_SEEN_MULLIGAN", "HAS_SEEN_DK_DECK_BUILDING_UNKNOWN" })
            Check(NativeUIProjection.DeathKnightTipBit(name) == -1, "unknown DK subkey falls through: " + name);
        Check(!NativeUIProjection.AllowedPreference("FTUE")
            && !NativeUIProjection.AllowedPreferenceSave("FTUE", names[0], new long[] { 1 }), "DK tips never enable FTUE preferences");

        string path = Path.Combine(root, "dk-tips.xml");
        using (var profile = new LocalProfile(path))
        {
            profile.SelectCardBack(0);
            Check(profile.SeenDeathKnightTips == 0 && !File.ReadAllText(path).Contains("SeenDeathKnightTips"),
                "new save writes no DK tips element");
            for (int bit = 0; bit < 4; bit++)
            {
                Check(!profile.SeenDeathKnightTip(bit), "unmarked DK tip reads unseen");
                byte[] before = File.ReadAllBytes(path);
                int mask = profile.SeenDeathKnightTips;
                profile.CommitFailure = phase => { if (phase == "replace") throw new IOException("DK tip fault"); };
                bool failed = false;
                try { profile.MarkDeathKnightTipSeen(bit); }
                catch (IOException) { failed = true; }
                finally { profile.CommitFailure = null; }
                Check(failed && profile.SeenDeathKnightTips == mask && File.ReadAllBytes(path).SequenceEqual(before),
                    "failed DK tip commit changes neither memory nor file");
                int commits = 0;
                profile.CommitFailure = phase => { if (phase == "replace") commits++; };
                try
                {
                    Check(profile.MarkDeathKnightTipSeen(bit) && !profile.MarkDeathKnightTipSeen(bit) && commits == 1,
                        "DK tip mark commits once");
                }
                finally { profile.CommitFailure = null; }
                var after = XDocument.Load(path);
                after.Root!.Element("SeenDeathKnightTips")!.Remove();
                var prior = XDocument.Parse(System.Text.Encoding.UTF8.GetString(before).TrimStart('\uFEFF'));
                prior.Root!.Element("SeenDeathKnightTips")?.Remove();
                Check(XNode.DeepEquals(prior, after), "DK tip commit preserves other profile data");
            }
            // An unrelated commit must keep the mask through Snapshot too.
            profile.MarkMassPackTooltipSeen();
        }
        using (var profile = new LocalProfile(path))
            Check(profile.SeenDeathKnightTips == 15 && Enumerable.Range(0, 4).All(bit => profile.SeenDeathKnightTip(bit)
                && !profile.MarkDeathKnightTipSeen(bit)), "all DK tips survive reload and unrelated commits");
        var legacy = XDocument.Load(path);
        legacy.Root!.Element("SeenDeathKnightTips")!.Remove();
        legacy.Save(path);
        byte[] legacyBytes = File.ReadAllBytes(path);
        using (var profile = new LocalProfile(path))
            Check(profile.SeenDeathKnightTips == 0 && Enumerable.Range(0, 4).All(bit => !profile.SeenDeathKnightTip(bit)),
                "old save without DK element reads unseen");
        Check(File.ReadAllBytes(path).SequenceEqual(legacyBytes), "loading old save preserves file bytes");
    }
}
