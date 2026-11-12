// Unity host entry points are outside this test; LocalLadder.Observe and profile persistence stay real.
internal static class NativeUIProbe
{
    internal static bool HideMenu => false;
    internal static void Navigate(SceneMgr.Mode mode) => throw new System.NotSupportedException();
}

internal static class ClientBridge
{
    internal static string DiagnosticRunId => "test";
    internal sealed class PreparedLocalMatch
    {
        internal NativeMatch Match => throw new System.NotSupportedException();
    }
    internal static PreparedLocalMatch PrepareLocalMatch(string deck, string opponent, int seed, int stars, LocalDeck player = null,
        System.Collections.Generic.IReadOnlyList<LocalDeckCard> premiumEntries = null) => throw new System.NotSupportedException();
    internal static void StartLocalMatch(LocalMatchRecord match, PreparedLocalMatch prepared, PlaytestQueue playtest = null,
        bool nativeMatchmaking = false, DebugScenario scenario = null)
        => throw new System.NotSupportedException();
    // No native client defs here; debug scenarios are never armed in transport.
    internal static bool ClientKnowsCard(string cardId) => false;
    internal static void ConcedeLocal() => throw new System.NotSupportedException();
}

internal static class NativeTestControl
{
    internal static bool DiagnosticsEnabled => false;
}

internal static class NativeMatchmaking
{
    internal static bool Visible => false;
}

internal sealed class LocalRewardSummary { internal string Body => ""; }
internal static class LocalRewardPresentation
{
    internal static LocalRewardSummary Summary(LocalMatchRecord match, LocalProfile profile) => throw new System.NotSupportedException();
}
