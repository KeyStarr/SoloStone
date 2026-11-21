// #239: when the client's cached highlight silhouette is rendered again. Client-free.
internal static class HighlightRefreshTests
{
    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception("highlight refresh: " + name);
        Console.WriteLine("PASS highlight refresh: " + name);
    }

    internal static void Run()
    {
        foreach (int type in new[] { 8, 74, 80, 81, 194, 195 })
            Check(HighlightRefreshRules.IsTauntFrame(type), "Taunt frame spell type " + type);
        foreach (int type in new[] { 0, 7, 9, 73, 75, 79, 82, 193, 196 })
            Check(!HighlightRefreshRules.IsTauntFrame(type), "spell type " + type + " is not a Taunt frame");
        Check(HighlightRefreshRules.SpellIdle == 2, "Idle is the state after Birth (1) and before Action (3)");
        Check(HighlightRefreshRules.GlowTurnsOn(true, false), "off to on forces a render");
        Check(!HighlightRefreshRules.GlowTurnsOn(false, false), "on to on does not force");
        Check(!HighlightRefreshRules.GlowTurnsOn(true, true), "off to off does not force");
        Check(!HighlightRefreshRules.GlowTurnsOn(false, true), "on to off does not force");
    }
}
