using System.Xml.Serialization;

// #57 (owner 2026-10-28): deleting a Core starter loaner in the tray hides it for this save; the menu restores them.
internal static class HiddenLoanerChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Reject(Action action, string message)
    {
        try { action(); } catch (InvalidOperationException) { Console.WriteLine("PASS " + message); return; }
        throw new Exception("Expected rejection: " + message);
    }

    internal static void Run(string root)
    {
#if ENGINE_SOURCE
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        LocalProfileData Read(string file) { using var stream = File.OpenRead(file); return (LocalProfileData)serializer.Deserialize(stream)!; }
        void Write(string file, LocalProfileData data) { using var stream = File.Create(file); serializer.Serialize(stream, data); }
        string dir = Path.Combine(root, "hidden-loaners"); Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "profile.xml");
        string[] starters = LocalDecks.PlayerCoreStarterDecks.Select(deck => deck.Id).ToArray();
        string mage = starters.First(key => key == "player-core-mage"), priest = starters.First(key => key == "player-core-priest");
        Check(starters.Length == NativeUIProjection.PlayerLoanerCapacity && starters.All(LocalProfile.IsHideableLoaner)
            && !LocalProfile.IsHideableLoaner("player-rogue-imbue") && !LocalProfile.IsHideableLoaner(LocalDecks.TestDecisionsId)
            && !LocalProfile.IsHideableLoaner("custom:910000010") && !LocalProfile.IsHideableLoaner(null),
            "only the Core starter loaners can be hidden");

        using (var profile = new LocalProfile(path))
        {
            profile.SelectCardBack(0);
            Check(profile.HiddenLoaners.Count == 0 && !File.ReadAllText(path).Contains("HiddenLoaners"), "a new save hides nothing and writes no element");
            Check(profile.HideLoaner(mage) && !profile.HideLoaner(mage) && profile.IsLoanerHidden(mage) && profile.HiddenLoaners.SequenceEqual(new[] { mage }),
                "hiding a starter commits once (idempotent)");
            Reject(() => profile.HideLoaner("player-rogue-imbue"), "a retired meta loaner cannot be hidden");
            Reject(() => profile.HideLoaner(LocalDecks.TestDecisionsId), "a test loaner cannot be hidden");
            Reject(() => profile.HideLoaner("910000010"), "a personal deck cannot be hidden");
            profile.HideLoaner(priest);
            profile.SelectCardBack(0);
        }
        byte[] bytes = File.ReadAllBytes(path);
        using (var profile = new LocalProfile(path))
        {
            Check(profile.HiddenLoaners.SequenceEqual(new[] { mage, priest }) && profile.Collection.Count > 0, "hidden starters persist across reload; cards untouched");
            profile.SelectCardBack(0);
        }
        Check(File.ReadAllBytes(path).SequenceEqual(bytes), "snapshot round trip keeps the hidden list byte-equal");

        // Projection: hidden starters drop out of the player tray; the others keep their stable slots.
        var all = NativeUIProjection.ProjectedLoanerKeys(0);
        var shown = NativeUIProjection.VisibleLoanerKeys(all, new[] { mage, priest });
        Check(shown.SequenceEqual(all.Where(key => key != mage && key != priest)) && shown.Count == all.Count - 2, "hidden starters are not projected");
        var personal = new[] { new KeyValuePair<long, long>(910000010L, 0) };
        var fullRanks = NativeUIProjection.ExpectedNativeSortOrders(personal, all);
        var hiddenRanks = NativeUIProjection.ExpectedNativeSortOrders(personal, shown);
        Check(shown.All(key => hiddenRanks[NativeUIProjection.DeckId(key)] == fullRanks[NativeUIProjection.DeckId(key)])
            && hiddenRanks[910000010L] == fullRanks[910000010L], "remaining starters keep their sort orders");
        Check(NativeUIProjection.ShowDeckBoxDeleteButton(true, true, NativeUIProjection.DeckId(mage), true)
            && NativeUIProjection.HideableLoanerKey(NativeUIProjection.DeckId(mage)) == mage
            && !NativeUIProjection.ShowDeckBoxDeleteButton(true, true, NativeUIProjection.DeckId("player-rogue-imbue"), true)
            && !NativeUIProjection.ShowDeckBoxDeleteButton(true, true, NativeUIProjection.DeckId(LocalDecks.TestDecisionsId), true)
            && NativeUIProjection.ShowDeckBoxDeleteButton(true, true, 910000010L, true)
            && !NativeUIProjection.ShowDeckBoxDeleteButton(true, true, NativeUIProjection.DeckId(mage), false)
            && NativeUIProjection.HideableLoanerKey(910000010L) == null,
            "starters show the native delete (hide) button; retired/test loaners never; personal decks unchanged");

        // A hidden starter cannot start a match (stale selection falls back to the picker's choice).
        using (var profile = new LocalProfile(path))
        {
            var deck = LocalDecks.Get(mage);
            Reject(() => NativeUIProjection.ValidateStart(profile, mage, deck, false), "a hidden starter cannot start a match");
            var visible = LocalDecks.Get(starters.First(key => key != mage && key != priest));
            Check(NativeUIProjection.ValidateStart(profile, visible.Id, visible, false) == null, "a shown starter still starts");
            Check(profile.RestoreLoaners() && !profile.RestoreLoaners() && profile.HiddenLoaners.Count == 0
                && !File.ReadAllText(path).Contains("HiddenLoaners"), "restore shows every starter again (idempotent; element gone)");
        }

        // A starter-shaped id with no current deck (a later build renamed or dropped that starter) loads and hides nothing.
        {
            var data = Read(path); data.HiddenLoaners = new List<string> { "player-core-bard" };
            string gone = Path.Combine(dir, "gone-starter.xml"); Write(gone, data);
            using var profile = new LocalProfile(gone);
            Check(profile.IsLoanerHidden("player-core-bard") && starters.All(key => !profile.IsLoanerHidden(key)),
                "a hidden id for a starter this build no longer has keeps the save loadable");
        }

        // Tamper: non-starter or repeated ids are rejected on load.
        foreach (var (name, hidden) in new[] { ("retired", new[] { "player-rogue-imbue" }),
            ("duplicate", new[] { mage, mage }), ("personal", new[] { "custom:910000010" }) })
        {
            var data = Read(path); data.HiddenLoaners = hidden.ToList();
            string bad = Path.Combine(dir, "bad-" + name + ".xml"); Write(bad, data);
            Reject(() => { using var profile = new LocalProfile(bad); }, "hidden starter list rejected: " + name);
        }
#endif
    }
}
