/// <summary>Client-free decisions behind NativeHighlightRefresh (#239): when the client's cached highlight silhouette must be
/// rendered again. The client renders a board minion's glow from its current meshes, once, and reuses that picture until a
/// tagged renderer is switched on or off or a refresh is forced; a Taunt frame that was still animating then leaves a stale,
/// oversized glow for good.</summary>
internal static class HighlightRefreshRules
{
    /// <summary>The Taunt frame spell types (ActivateTaunt): normal 8, Stealth/Immune/ignored 80, golden 74 and 81, diamond 194 and 195.</summary>
    internal static bool IsTauntFrame(int spellType)
        => spellType == 8 || spellType == 80 || spellType == 74 || spellType == 81 || spellType == 194 || spellType == 195;

    /// <summary>SpellStateType.IDLE: the frame has finished its birth animation and holds its resting shape.</summary>
    internal const int SpellIdle = 2;

    /// <summary>True when the glow goes from off to on: only then is a fresh silhouette forced (about once per glow, not per frame).</summary>
    internal static bool GlowTurnsOn(bool wasOff, bool nowOff) => wasOff && !nowOff;
}
