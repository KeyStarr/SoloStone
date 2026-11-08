using System;

// These tests compile OfflineGuard + ProfileCatalog without the save engine. Save creation is never
// reached from the guard (it only resolves existing saves), so these members fail loudly if it ever is.
internal sealed class LocalProfile : IDisposable
{
    internal LocalProfile(string path, ProfileOptions options) => throw new NotSupportedException("Save creation is outside the guard boundary test.");
    internal static double LegendOneCreationRating() => throw new NotSupportedException("Save creation is outside the guard boundary test.");
    internal void PersistNewProfile(ProfileOptions options) => throw new NotSupportedException();
    // Save deletion (Describe) reads profiles through these; the guard never deletes.
    internal static LocalProfileData ReadFile(string path) => throw new NotSupportedException("Save summaries are outside the guard boundary test.");
    public static string RankName(int stars) => throw new NotSupportedException();
    // The startup preflight asks this after ReadFile; ReadFile throws here, so a guard-boundary launch always lands on the picker.
    internal static bool IsSupportedVersion(int version) => version >= 13 && version <= 15;
    public void Dispose() { }
}
internal sealed class LocalLadderState { public int StarIndex; }
internal sealed class LocalProfileData
{
    public object Pending; public LocalLadderState LadderState; public int GoldBalance, Version;
    public System.Collections.Generic.List<object> CustomDecks = new System.Collections.Generic.List<object>();
}
