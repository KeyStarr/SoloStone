internal static partial class NativeArena
{
    // An Arena match owns the match while its pending record says Arena. After settlement the record is in history and the
    // client is still in GAMEPLAY (results, then the post-game route): the last settled match decides until the scene changes.
    private static bool OwnsArenaMatch()
    {
        var profile = LocalLadder.Profile;
        if (profile == null) return false;
        if (profile.HasPendingMatch) return ArenaRules.IsArena(profile.PendingMode);
        return SceneMgr.IsInitialized() && SceneMgr.Get().GetMode() == SceneMgr.Mode.GAMEPLAY && ArenaRules.IsArena(profile.LastSettledMode);
    }
}
