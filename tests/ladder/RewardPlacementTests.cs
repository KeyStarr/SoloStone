// GAMEPLAY reward boxes (lane-1 blocker 2026-10-25): the HUB-authored prefab must be framed for the end-game camera.
internal static class RewardPlacementTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static bool Near(double a, double b, double tolerance = 1e-6) => Math.Abs(a - b) <= tolerance;
    static void Reject(Action action, string message)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new Exception("Expected rejection: " + message);
    }
    static LocalVec Unit(LocalVec v) => v * (1 / v.Length);
    static LocalVec Cross(LocalVec a, LocalVec b) => new LocalVec(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    // Perspective projection to screen shares (0..1 from left/bottom) at the point's own depth.
    static (double x, double y, double depth) Screen(LocalVec p, LocalVec cam, LocalVec right, LocalVec up, LocalVec forward, double tanHalf, double aspect)
    {
        LocalVec d = p - cam; double depth = LocalVec.Dot(d, forward), half = depth * tanHalf;
        return (0.5 + LocalVec.Dot(d, right) / (2 * half * aspect), 0.5 + LocalVec.Dot(d, up) / (2 * half), depth);
    }

    internal static void Run()
    {
        // The prefab as authored for the HUB: box target above Done in world Z, both on the y=131.2 plane, root at origin.
        var box = new LocalVec(0, 131.2, 1.5); var done = new LocalVec(0, 131.2, -1.3); var root = new LocalVec(0, 0, 0);
        double tanHalf = Math.Tan(30 * Math.PI / 180), aspect = 16.0 / 9;
        var cameras = new[] {
            // HUB-like: straight down.
            (cam: new LocalVec(0, 200, 0), forward: new LocalVec(0, -1, 0), up: new LocalVec(0, 0, 1)),
            // Board-like: tilted, offset, below the HUB prefab plane (the live failure: the HUB pose sat behind/above it).
            (cam: new LocalVec(3, 45, -38), forward: Unit(new LocalVec(0, -0.85, 0.53)), up: Unit(new LocalVec(0, 0.53, 0.85))),
            // Rolled oblique camera.
            (cam: new LocalVec(-10, 5, 7), forward: Unit(new LocalVec(0.3, -0.6, 0.74)), up: Unit(Cross(Unit(new LocalVec(0.3, -0.6, 0.74)), new LocalVec(1, 0.2, 0)))) };
        foreach (var c in cameras)
        {
            LocalVec right = Cross(c.up, c.forward);   // Unity: right = up x forward in its left-handed basis
            double depth = 12, half = depth * tanHalf;
            var placement = LocalRewardPlacement.Fit(new[] { box }, done, root, c.cam, right, c.up, c.forward, depth, half);
            LocalVec anchor = (box + done) * 0.5, spot = c.cam + c.forward * depth + c.up * ((LocalRewardPlacement.AnchorHeightShare - 0.5) * 2 * half);
            LocalVec mappedBox = placement.Map(box, anchor, spot), mappedDone = placement.Map(done, anchor, spot);
            var b = Screen(mappedBox, c.cam, right, c.up, c.forward, tanHalf, aspect);
            var d = Screen(mappedDone, c.cam, right, c.up, c.forward, tanHalf, aspect);
            Check(Near(b.depth, depth) && Near(d.depth, depth), "boxes face the camera at the end-game depth");
            Check(Near(b.x, 0.5) && Near(d.x, 0.5) && b.y > d.y, "box centred above Done on screen");
            Check(Near(b.y - d.y, LocalRewardPlacement.BoxToDoneScreenShare), "HUB box-to-Done spacing kept: " + (b.y - d.y));
            Check(Near((b.y + d.y) / 2, LocalRewardPlacement.AnchorHeightShare), "layout at the HUB screen height");
            Check(b.y < 1 && d.y > 0 && b.x > 0 && b.x < 1, "on screen");
            // Rigid: the root (and every child) moves with the same rotation and scale.
            var offset = new LocalVec(2, -1, 0.5);
            LocalVec moved = placement.Map(root + offset, anchor, spot) - placement.Position, expected = placement.Rotate(offset * placement.Scale);
            Check(Near(moved.X, expected.X) && Near(moved.Y, expected.Y) && Near(moved.Z, expected.Z), "rigid display move");
            Check(Near(placement.Qx * placement.Qx + placement.Qy * placement.Qy + placement.Qz * placement.Qz + placement.Qw * placement.Qw, 1), "unit rotation");
            // Prefab +X stays screen right.
            var east = Screen(placement.Map(box + new LocalVec(1, 0, 0), anchor, spot), c.cam, right, c.up, c.forward, tanHalf, aspect);
            Check(east.x > b.x && Near(east.y, b.y), "prefab right stays screen right");
            // The placed layout passes the viewport check (Unity viewport = these screen shares, z = depth).
            Check(LocalRewardSafety.OffScreen(new[] { new LocalViewportPoint("box0", b.x, b.y, b.depth),
                new LocalViewportPoint("done", d.x, d.y, d.depth) }, 0.3) == null, "placed boxes pass the viewport check");
        }
        // Native floatyObj drift adds a world-space vector each frame: scaled with the layout it moves the box the same
        // share of the screen as in the HUB, so the mesh stays on its glow and collider (Deck/PC live 2026-10-29).
        foreach (var c in cameras)
        {
            LocalVec right = Cross(c.up, c.forward);
            double depth = 12, half = depth * tanHalf;
            var placement = LocalRewardPlacement.Fit(new[] { box }, done, root, c.cam, right, c.up, c.forward, depth, half);
            LocalVec anchor = (box + done) * 0.5, spot = c.cam + c.forward * depth + c.up * ((LocalRewardPlacement.AnchorHeightShare - 0.5) * 2 * half);
            double drift = LocalRewardPlacement.WorldDrift(1.5f, placement.Scale);
            LocalVec target = placement.Map(box, anchor, spot), drifted = target + new LocalVec(drift, drift, drift);
            var t = Screen(target, c.cam, right, c.up, c.forward, tanHalf, aspect);
            var d = Screen(drifted, c.cam, right, c.up, c.forward, tanHalf, aspect);
            var raw = Screen(target + new LocalVec(1.5, 1.5, 1.5), c.cam, right, c.up, c.forward, tanHalf, aspect);
            double shift = Math.Sqrt((d.x - t.x) * (d.x - t.x) + (d.y - t.y) * (d.y - t.y));
            double unscaledShift = Math.Sqrt((raw.x - t.x) * (raw.x - t.x) + (raw.y - t.y) * (raw.y - t.y));
            Check(placement.Scale < 1 && Near(drift, 1.5 * placement.Scale, 1e-6) && shift < unscaledShift * placement.Scale * 1.5, "drift shrinks with the layout: " + shift + " vs " + unscaledShift);
        }
        Check(LocalRewardPlacement.WorldDrift(0.25f, 1) == 0.25f && LocalRewardPlacement.WorldDrift(0.25f, 0) == 0.25f
            && LocalRewardPlacement.WorldDrift(0.25f, double.NaN) == 0.25f && LocalRewardPlacement.WorldDrift(0.25f, double.PositiveInfinity) == 0.25f,
            "unplaced or invalid scale keeps the native drift");
        // The HUB pose seen by the board-like camera (the live failure) is caught: behind it, so the popup takes over.
        var hubPose = Screen(box, cameras[1].cam, Cross(cameras[1].up, cameras[1].forward), cameras[1].up, cameras[1].forward, tanHalf, aspect);
        string caught = LocalRewardSafety.OffScreen(new[] { new LocalViewportPoint("box0", hubPose.x, hubPose.y, hubPose.depth) }, 0.3);
        Check(caught != null && caught.StartsWith("box0 behind the near plane"), "unplaced HUB pose rejected: " + caught);
        var straight = LocalRewardPlacement.Fit(new[] { box }, done, root, cameras[0].cam, new LocalVec(1, 0, 0), new LocalVec(0, 0, 1), new LocalVec(0, -1, 0), 12, 12 * tanHalf);
        Check(Near(straight.Qw, 1) && Near(straight.Qx, 0) && Near(straight.Qy, 0) && Near(straight.Qz, 0), "HUB-aligned camera keeps the authored rotation");
        // Orthographic framing: halfHeight is the orthographic size; the scale follows it.
        var ortho = LocalRewardPlacement.Fit(new[] { box }, done, root, cameras[0].cam, new LocalVec(1, 0, 0), new LocalVec(0, 0, 1), new LocalVec(0, -1, 0), 5, 3);
        Check(Near(ortho.Scale, LocalRewardPlacement.BoxToDoneScreenShare * 6 / 2.8), "orthographic scale");
        Reject(() => LocalRewardPlacement.Fit(new LocalVec[0], done, root, cameras[0].cam, new LocalVec(1, 0, 0), new LocalVec(0, 0, 1), new LocalVec(0, -1, 0), 5, 3), "no layout");
        Reject(() => LocalRewardPlacement.Fit(new[] { done }, done, root, cameras[0].cam, new LocalVec(1, 0, 0), new LocalVec(0, 0, 1), new LocalVec(0, -1, 0), 5, 3), "degenerate layout");
        Reject(() => LocalRewardPlacement.Fit(new[] { box }, done, root, cameras[0].cam, new LocalVec(1, 0, 0), new LocalVec(1, 0, 0), new LocalVec(0, -1, 0), 5, 3), "skewed camera");
        Reject(() => LocalRewardPlacement.Fit(new[] { box }, done, root, cameras[0].cam, new LocalVec(1, 0, 0), new LocalVec(0, 0, 1), new LocalVec(0, -1, 0), 0, 3), "behind camera");
        float below = LocalRewardPresentation.UnderDoneHeights;
        Check(below > LocalRewardPresentation.DoneScale * 0.5f + 0.5f && below < LocalRewardPresentation.DoneScale * 0.5f + 1.5f,
            "the secondary row under the enlarged Done clears it and stays tucked right under it");
        Check(LocalRewardPresentation.UnderDoneColumn(0, 1) == 0, "a single secondary button is centred under Done");
        Check(LocalRewardPresentation.DoneDropHeights > (LocalRewardPresentation.DoneScale - 1) * 0.5f,
            "the enlarged Done keeps a margin under the reward above it");
        float first = LocalRewardPresentation.UnderDoneColumn(0, 2), second = LocalRewardPresentation.UnderDoneColumn(1, 2);
        Check(first == -second && second - first > 1 && second - first < 1.3f, "two secondary buttons sit side by side, centred under Done");
        Console.WriteLine("PASS reward placement: GAMEPLAY boxes face the end-game camera, centred, HUB spacing, rigid move, DETAILED gap");
        Safety();
    }

    // Review 2026-10-25: the presentation can never strand the player (viewport check, watchdog, HUB gate, boosters).
    static void Safety()
    {
        LocalViewportPoint P(string name, double x, double y, double z = 10, bool loose = false) => new LocalViewportPoint(name, x, y, z, loose);
        string OffScreen(params LocalViewportPoint[] points) => LocalRewardSafety.OffScreen(points, 0.3);
        Check(OffScreen(P("box0", 0.5, 0.6), P("done", 0.5, 0.45), P("start0", 0.5, -0.1, loose: true)) == null, "on-screen layout accepted");
        Check(OffScreen(P("box0", 0.5, 1.3)) == "box0 off screen (x=0.5,y=1.3)", "above the screen rejected: " + OffScreen(P("box0", 0.5, 1.3)));
        Check(OffScreen(P("done", -0.01, 0.5)) != null && OffScreen(P("done", 0.99, 0.5)) != null, "Done hugging an edge rejected (inset)");
        Check(OffScreen(P("done", 0.03, 0.97)) == null, "Done just inside the inset accepted");
        Check(OffScreen(P("box0", 0.5, 0.5, 0.3)) != null && OffScreen(P("box0", 0.5, 0.5, -4)) != null, "at/behind the near plane rejected");
        Check(OffScreen(P("box0", double.NaN, 0.5)) != null, "NaN projection rejected");
        Check(OffScreen(P("start0", 1.2, -0.2, loose: true)) == null && OffScreen(P("start0", 1.4, 0.5, loose: true)) != null,
            "start bones may sit at the edge, not far off it");
        Check(OffScreen(P("box0", 0.5, 0.5), P("box1", 2, 0.5), P("done", 0.5, -3)).StartsWith("box1 "), "first failure named");

        // Camera: the dedicated layer-19 camera beats a board camera with an Everything mask that happens to come first.
        var board = new LocalCameraFacts { CullingMask = -1, Depth = 0, Enabled = true };
        var endGame = new LocalCameraFacts { CullingMask = 1 << 19, Depth = 10, Enabled = true };
        var shared = new LocalCameraFacts { CullingMask = 1 << 19 | 1 << 8, Depth = 20, Enabled = true };
        Check(LocalRewardSafety.PickCamera(new[] { board, endGame }, 19) == 1, "dedicated end-game camera preferred");
        Check(LocalRewardSafety.PickCamera(new[] { board, shared, endGame }, 19) == 2, "fewest layers wins over depth");
        Check(LocalRewardSafety.PickCamera(new[] { board, new LocalCameraFacts { CullingMask = -1, Depth = 5, Enabled = true } }, 19) == 1,
            "equal masks: the camera drawn last");
        Check(LocalRewardSafety.PickCamera(new[] { board, new LocalCameraFacts { CullingMask = 1 << 19, Enabled = false },
            new LocalCameraFacts { CullingMask = 1 << 19, Enabled = true, RendersToTexture = true } }, 19) == 0, "disabled/texture cameras skipped");
        Check(LocalRewardSafety.PickCamera(new[] { new LocalCameraFacts { CullingMask = 1 << 8, Enabled = true } }, 19) == -1, "no layer-19 camera");

        // Watchdog: boxes must become reachable, Done must follow the last box, and stay clickable.
        string W(LocalRewardPhase phase, float sinceReveal, bool reachable, float sinceAllOpened, float sinceDone = 0, bool doneClickable = true, bool details = false) =>
            LocalRewardSafety.Watchdog(phase, sinceReveal, reachable, sinceAllOpened, sinceDone, doneClickable, details);
        Check(W(LocalRewardPhase.Revealing, 5, false, -1) == null, "boxes still flying in");
        Check(W(LocalRewardPhase.Revealing, LocalRewardSafety.BoxesTimeout + 1, false, -1) != null, "boxes never reachable: fallback");
        Check(W(LocalRewardPhase.Revealing, 300, true, -1) == null, "a slow player is never timed out");
        Check(W(LocalRewardPhase.Revealing, 300, true, 3) == null, "Done birth in progress");
        Check(W(LocalRewardPhase.Revealing, 300, true, LocalRewardSafety.DoneTimeout + 1) != null, "Done never shown: fallback");
        Check(W(LocalRewardPhase.DoneReady, 300, true, 0, 5, true) == null, "clickable Done");
        Check(W(LocalRewardPhase.DoneReady, 300, true, 0, 1, false) == null, "Done grace");
        Check(W(LocalRewardPhase.DoneReady, 300, true, 0, 5, false) != null, "Done not clickable on screen: fallback");
        Check(W(LocalRewardPhase.DoneReady, 300, true, 0, 5, false, details: true) == null, "DETAILED RESULTS owns input");
        Check(W(LocalRewardPhase.Closing, 300, false, 99, 99, false) == null && W(LocalRewardPhase.Idle, 300, false, -1) == null, "other phases unwatched");

        // Clickable: the input system's own pick decides; an occluding hitbox (live: EndGameScreen "Click to continue"
        // over every box) is not clickable, so the watchdog's REWARDS SAVED fallback fires instead of stranding the player.
        bool C(bool live, string offscreen, params LocalHitProbe[] probes) => LocalRewardSafety.Clickable(live, offscreen, probes);
        Check(C(true, null, LocalHitProbe.Target), "picked target is clickable");
        Check(!C(true, null, LocalHitProbe.Covered), "occluded target is not clickable");
        Check(!C(true, null, LocalHitProbe.Covered, LocalHitProbe.Covered, LocalHitProbe.Nothing), "covered at every plate");
        Check(!C(true, null, LocalHitProbe.Nothing), "pick reaching nothing is not clickable");
        Check(C(true, null, LocalHitProbe.Covered, LocalHitProbe.Target), "one reachable plate suffices");
        Check(C(true, null) && C(true, null, LocalHitProbe.Unsupported) && LocalRewardSafety.Clickable(true, null, null),
            "no mouse hit test (touch mode): screen checks decide");
        Check(!C(true, null, LocalHitProbe.Unsupported, LocalHitProbe.Covered), "unsupported samples never mask an occlusion");
        Check(!C(false, null, LocalHitProbe.Target) && !C(true, "box0 off screen", LocalHitProbe.Target), "inactive or off-screen never clickable");
        bool occluded = C(true, null, LocalHitProbe.Covered);
        Check(W(LocalRewardPhase.Revealing, LocalRewardSafety.BoxesTimeout + 1, occluded, -1) != null, "occluded boxes: fallback after BoxesTimeout");
        Check(W(LocalRewardPhase.DoneReady, 300, true, 0, LocalRewardSafety.DoneClickableGrace + 1, occluded) != null, "occluded Done: fallback");

        // HUB gate: only the display or the presentation's own popups block; a stale phase never does.
        Check(!LocalRewardSafety.BlocksHomeInput(false, new string[0]) && !LocalRewardSafety.BlocksHomeInput(false, null), "nothing up: HUB live");
        Check(LocalRewardSafety.BlocksHomeInput(true, new string[0]), "boxes up: gated");
        foreach (string id in new[] { LocalRewardPresentation.NoItemsPopupPrefix + "m_1", LocalRewardPresentation.DetailsPopupPrefix + "m_1",
            LocalRewardSafety.FallbackPopupPrefix + "m_1", LocalRewardSafety.AckRetryPopupPrefix + "m", LocalRewardSafety.SettleRetryPopupId })
            Check(LocalRewardSafety.BlocksHomeInput(false, new[] { null, "OTHER", id }), "own popup gates: " + id);
        Check(!LocalRewardSafety.BlocksHomeInput(false, new[] { "LOCAL_TIER_REWARD_FALLBACK_m", "RANKED_INTRO", null }), "foreign popups do not gate");
        Check(LocalRewardSafety.ResetToIdle(LocalRewardPhase.Acknowledging, true, false, false), "stale Acknowledging resets in a receipt-free HUB");
        Check(!LocalRewardSafety.ResetToIdle(LocalRewardPhase.Acknowledging, true, true, false), "a pending receipt keeps its phase");
        Check(!LocalRewardSafety.ResetToIdle(LocalRewardPhase.NativeFallback, true, false, true), "an up popup keeps its phase");
        Check(!LocalRewardSafety.ResetToIdle(LocalRewardPhase.Acknowledging, false, false, false), "not in HUB: no reset");
        Check(!LocalRewardSafety.ResetToIdle(LocalRewardPhase.Idle, true, false, false), "Idle stays Idle");

        // Boosters: V2 receipts keep booster 1; V3 uses the receipt's set when it maps to a usable native booster.
        int Id(string type) => type == "CORE" ? 1637 : type == "BROKEN" ? throw new ArgumentOutOfRangeException(nameof(type)) : type == "ZERO" ? 0 : 1900;
        bool Usable(int id) => id == 1637;
        Check(LocalRewardSafety.BoosterId(2, "CORE", Id, Usable) == 1 && LocalRewardSafety.BoosterId(0, "CORE", Id, Usable) == 1, "V2/V1 keep booster 1");
        Check(LocalRewardSafety.BoosterId(LocalEconomy.RollRewardVersion, "CORE", Id, Usable) == 1637, "V3 set booster");
        Check(LocalRewardSafety.BoosterId(4, "CORE", Id, Usable) == 1637, "V4 keeps set booster beside V5");
        Check(LocalRewardSafety.BoosterId(3, "BROKEN", Id, Usable) == 1 && LocalRewardSafety.BoosterId(3, "ZERO", Id, Usable) == 1
            && LocalRewardSafety.BoosterId(3, "UNSHIPPED", Id, Usable) == 1 && LocalRewardSafety.BoosterId(3, null, Id, Usable) == 1,
            "unknown/unusable V3 sets fall back to booster 1, never the popup");
        Check(LocalRewardSafety.BoosterId(3, "CORE", Id, _ => throw new InvalidOperationException()) == 1, "availability failure: booster 1");

        // IMGUI Result panel, V3: the roll, half-weight bonus rows and dust-only wins.
        var facts = new LocalRewardFacts { DistinctCardsPlayed = 6, EnemyMinionsDefeated = 2, MinimumHealth = 7 };
        var both = LocalResultPanel.Rolled("Won", 9, false, facts, true, 45 + 15, 33, 15, true);
        Check(both.Total == "+60 local gold · +33 dust · bonus +15 gold" && both.Gate == "Victory roll: +45 local gold · +33 dust"
            && both.Pack == "Great win: 1 pack", "V3 gold+dust panel: " + both.Total + " | " + both.Gate);
        Check(both.Rows.Count == 3 && both.Rows[0].Key && both.Rows[0].Value == "6 distinct cards played +5"
            && !both.Rows[1].Key && both.Rows[1].Value == "2/5 enemy minions defeated"
            && both.Rows[2].Key && both.Rows[2].Value == "Won after observed 1-10 health +10", "V3 half-weight rows");
        var dust = LocalResultPanel.Rolled("Won", 9, false, new LocalRewardFacts { DistinctCardsPlayed = 6, EnemyMinionsDefeated = 5 }, true, 0, 62, 10, true);
        Check(dust.Total == "+62 dust · bonus +10 dust" && dust.Gate == "Victory roll: +52 dust" && !dust.Total.Contains("gold"),
            "V3 dust-only panel: " + dust.Total + " | " + dust.Gate);
        var loss = LocalResultPanel.Rolled("Lost", 12, false, facts, true, 0, 0, 0, false, 4);
        Check(loss.Total == "No gold or dust" && loss.Gate == "No gold or dust: only victories roll a reward" && loss.Rows.All(row => !row.Key)
            && loss.Pack == "No bonus pack" && loss.Rows[2].Value == "Minimum health: 7", "V3 loss panel");
        Check(LocalResultPanel.Rolled("Won", 9, true, facts, true, 0, 0, 0, false).Gate == "No rewards: conceded"
            && LocalResultPanel.Rolled("Won", 3, false, facts, true, 0, 0, 0, false).Gate == "No rewards: fewer than four engine turns", "V3 gated panels");
        var pending = LocalResultPanel.Rolled("Won", 9, false, facts, false, 0, 0, 0, false);
        Check(pending.Gate == "Victory roll pending" && pending.Pack == "Pack reward pending", "V3 unsaved panel");
        Console.WriteLine("PASS reward safety: viewport check, end-game camera pick, reveal watchdog, HUB gate reset, V3 boosters and panel");
    }
}
