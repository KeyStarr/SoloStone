// #146: which start-of-turn task lists wait for the "Your Turn" banner, and when the wait ends. Client-free.
internal static class TurnBannerHoldTests
{
    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception("turn banner hold: " + name);
        Console.WriteLine("PASS turn banner hold: " + name);
    }

    internal static void Run()
    {
        // Held: the four controllers that ignore the banner.
        Check(TurnBannerHoldRules.IgnoresBanner(true, 1749, false), "Sigil keyword is held");
        Check(TurnBannerHoldRules.IgnoresBanner(true, 2311, false), "Objective keyword is held");
        Check(TurnBannerHoldRules.IgnoresBanner(true, 1192, false), "Sidequest keyword is held");
        Check(TurnBannerHoldRules.IgnoresBanner(true, 0, true), "Secret source is held");
        // Not held: everything the client already gates, and every other block type.
        Check(!TurnBannerHoldRules.IgnoresBanner(true, 0, false), "plain card or keyword-less Objective trigger is not held (client gates it)");
        Check(!TurnBannerHoldRules.IgnoresBanner(true, 4013, false), "other keyword is not held");
        Check(!TurnBannerHoldRules.IgnoresBanner(false, 1749, true), "a block that is not a trigger is not held");

        var hold = new TurnBannerHoldRules.Hold();
        object sigil = new object(), next = new object();
        Check(!hold.ShouldHold(null, true, 0), "no list, no hold");
        Check(hold.ShouldHold(sigil, true, 10.0) && hold.Holding && !hold.Released, "holds while the banner shows");
        Check(hold.ShouldHold(sigil, true, 11.5), "keeps holding mid-banner");
        Check(!hold.ShouldHold(sigil, false, 12.0) && hold.LastRelease == "banner-hidden", "releases when the banner is gone");
        Check(!hold.ShouldHold(sigil, true, 12.1), "a released list is never held again");
        Check(hold.ShouldHold(next, true, 20.0), "the next list starts its own hold");
        Check(!hold.ShouldHold(next, true, 20.0 + TurnBannerHoldRules.TimeoutSeconds) && hold.LastRelease == "timeout", "the timeout releases a banner that never leaves");
        Check(!hold.ShouldHold(next, true, 30.0), "after a timeout the list stays released");
        var late = new TurnBannerHoldRules.Hold();
        Check(!late.ShouldHold(sigil, false, 5.0), "banner already gone before the first check: no hold");
        var clock = new TurnBannerHoldRules.Hold();
        clock.ShouldHold(sigil, true, 100.0);
        Check(!clock.ShouldHold(sigil, true, 50.0) && clock.LastRelease == "timeout", "a clock that goes backwards releases");
    }
}
