// Pure status-routine helpers (plugin/StatusRoutine.cs): hash cache invalidation, screenshot requests, deep-scan gating.
internal static class StatusRoutineTests
{
    internal static void Run()
    {
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        var old = now.AddMinutes(-5);

        // Hash cache: a settled identity is hashed once; a changed identity recomputes.
        var cache = new SavedHashCache();
        int calls = 0;
        Func<string> hasher = () => "h" + (++calls);
        Check(cache.Get("1|2|3", old, now, hasher) == "h1" && cache.Get("1|2|3", old, now, hasher) == "h1" && cache.Computations == 1,
            "settled identity hashes once");
        Check(cache.Get("1|9|3", old, now, hasher) == "h2", "new write time recomputes");
        Check(cache.Get("2|9|3", old, now, hasher) == "h3", "new length recomputes");
        Check(cache.Get("2|9|4", old, now, hasher) == "h4", "new creation time recomputes");
        Check(cache.Get("2|9|4", old, now, hasher) == "h4" && cache.Computations == 4, "unchanged identity reuses the hash");
        // A just-written file can still change inside the timestamp granularity: never cached.
        var fresh = new SavedHashCache(); calls = 0;
        Check(fresh.Get("5|6|7", now.AddSeconds(-0.5), now, hasher) == "h1" && fresh.Get("5|6|7", now.AddSeconds(-0.5), now, hasher) == "h2",
            "recently written file is re-hashed");
        Check(fresh.Get("5|6|7", now.AddSeconds(-0.5), now.AddSeconds(3), hasher) == "h3" && fresh.Get("5|6|7", now.AddSeconds(-0.5), now.AddSeconds(4), hasher) == "h3",
            "file settles after two seconds");
        // Unknown identity (missing file) is never cached.
        var unknown = new SavedHashCache(); calls = 0;
        Check(unknown.Get(null, DateTime.MinValue, now, hasher) == "h1" && unknown.Get(null, DateTime.MinValue, now, hasher) == "h2", "null identity never cached");

        // Screenshot requests are consumed exactly once.
        string dir = Path.Combine(Path.GetTempPath(), "hs-status-routine-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Check(!StatusRoutine.ConsumeScreenshotRequest(dir), "no request, no capture");
            File.WriteAllText(Path.Combine(dir, StatusRoutine.ScreenshotRequestFile), "capture");
            Check(StatusRoutine.ConsumeScreenshotRequest(dir), "request triggers a capture");
            Check(!File.Exists(Path.Combine(dir, StatusRoutine.ScreenshotRequestFile)) && !StatusRoutine.ConsumeScreenshotRequest(dir), "request consumed once");
            Check(!StatusRoutine.ConsumeScreenshotRequest(Path.Combine(dir, "missing")), "missing reports directory is not an error");
        }
        finally { Directory.Delete(dir, true); }

        // Periodic gating.
        float next = 0;
        Check(StatusRoutine.Due(0, ref next, 3) && next == 3 && !StatusRoutine.Due(2.9f, ref next, 3) && StatusRoutine.Due(3, ref next, 3) && next == 6, "interval gate");

        // A player may leave the required pre-bind picker open across any number of status intervals.
        string waiting = Path.Combine(Path.GetTempPath(), "hs-profile-waiting-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(waiting);
        try
        {
            string run = Guid.NewGuid().ToString("D");
            ProfileStartupStatus.WriteWaiting(waiting, run, "LOGIN", false, true);
            DateTime first = File.GetLastWriteTimeUtc(Path.Combine(waiting, "native-ui-state.txt"));
            ProfileStartupStatus.WriteWaiting(waiting, run, "LOGIN", false, true);
            string native = File.ReadAllText(Path.Combine(waiting, "native-ui-state.txt"));
            string ladder = File.ReadAllText(Path.Combine(waiting, "ladder-state.txt"));
            Check(native.Contains("phase=SaveSelection") && native.Contains("selectorOwned=True") && native.Contains("profileBound=False")
                && ladder.Contains("screen=SaveSelection") && File.ReadAllLines(Path.Combine(waiting, "ladder-state.txt")).Contains("pending=") && ladder.Contains("profileBound=false")
                && File.GetLastWriteTimeUtc(Path.Combine(waiting, "native-ui-state.txt")) >= first, "unbound picker status remains fresh after another interval");
            string profile = Path.Combine(waiting, "profile.xml"); File.WriteAllText(profile, "profile");
            ProfileStartupStatus.WriteBound(waiting, run, "legacy", profile);
            string marker = File.ReadAllText(Path.Combine(waiting, ProfileStartupStatus.BoundMarkerName));
            Check(marker.Contains("schema=1") && marker.Contains("runId=" + run) && marker.Contains("slotId=legacy")
                && marker.Contains("profilePath=" + Path.GetFullPath(profile)), "bound marker records exact run, slot and canonical path");
            ProfileStartupStatus.ClearBound(waiting);
            Check(!File.Exists(Path.Combine(waiting, ProfileStartupStatus.BoundMarkerName)), "next startup clears stale bound marker");
        }
        finally { Directory.Delete(waiting, true); }

        // Deep scans: pack/collection scenes every tick, forced and first scan always, elsewhere rarely.
        Check(StatusRoutine.DeepScanDue("HUB", "HUB", false, 1, 0, false), "first scan always runs");
        Check(StatusRoutine.DeepScanDue("HUB", "HUB", true, 1, 0, true), "phase change forces a scan");
        Check(!StatusRoutine.DeepScanDue("HUB", "HUB", false, 10, 0, true) && !StatusRoutine.DeepScanDue("GAMEPLAY", "GAMEPLAY", false, 29.9f, 0, true), "idle scenes skip");
        Check(StatusRoutine.DeepScanDue("HUB", "HUB", false, 30, 0, true), "idle scenes refresh after the interval");
        Check(StatusRoutine.DeepScanDue("PACKOPENING", "PACKOPENING", false, 1, 0.9f, true) && StatusRoutine.DeepScanDue("COLLECTIONMANAGER", "COLLECTIONMANAGER", false, 1, 0.9f, true)
            && StatusRoutine.DeepScanDue("TOURNAMENT", "TOURNAMENT", false, 1, 0.9f, true), "pack, collection and deck picker scenes scan every tick");
        Check(StatusRoutine.DeepScanDue("HUB", "GAMEPLAY", false, 1, 0.9f, true) && StatusRoutine.DeepScanDue("HUB", null, false, 1, 0.9f, true),
            "a scene differing from the last deep scan's scene rescans at once");
    }
    private static void Check(bool passed, string message) { if (!passed) throw new Exception("StatusRoutine: " + message); }
}
