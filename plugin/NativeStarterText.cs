using System.Collections.Generic;
using System.Linq;
// #57 hidden starter decks (owner 2026-10-28): copy and test-control ids. Pure text, so test-control checks it on the Mac.
internal static class NativeStarterText
{
    // Native tray delete of a Core starter loaner hides it for this save; the native confirm says so.
    internal const string HideHeader = "Hide Starter Deck";
    internal const string HideBody = "Hide this starter deck? Its cards and your class progress stay. You can restore starter decks in Options > SoloStone Options.";
    // Native Options button (a rare fallback), shown only while this save hides at least one starter.
    internal const string OptionsObject = "SoloStoneRestoreStarters", OptionsLabel = "Restore Starter Decks", OptionsId = "options.restore-starters";
    internal const string RestorePopupId = "LOCAL_SOLOSTONE_RESTORE_STARTERS";
    internal const string RestoreHeader = "Restore Starter Decks"; // Title case like the hide confirm: the native header plate squeezes capitals.
    internal const string RestoreBody = "Show every hidden starter deck again? They return to your deck list the next time SoloStone starts.";
    internal const string RestoreLabel = "RESTORE", CancelLabel = "CANCEL";
    internal const string ConfirmId = "starters.restore", CancelId = "starters.cancel";
    internal static string DeleteButtonId(long deckId) => "deck:" + deckId + ".delete";

    internal static string DialogButtonId(string popupId, string button) =>
        popupId != RestorePopupId ? null : button == "confirm" ? ConfirmId : button == "cancel" ? CancelId : null;
}

// #87: a saved Standard deck that still holds Wild (Classic/Basic) cards. Shown when Play is pressed on such a deck.
internal static class NativeStandardText
{
    internal const string WildDeckHeader = "Wild Cards";
    internal const string FormatDeckHeader = "Not a Standard Deck";
    internal static string FormatDeckBody(string format) =>
        "This is a " + format + " deck. This version of SoloStone plays Standard decks only.";
    internal const int NamedCards = 6;
    internal static string WildDeckBody(IReadOnlyList<string> ids)
    {
        var names = ids.Take(NamedCards).Select(CardName).ToList();
        if (ids.Count > NamedCards) names.Add("and " + (ids.Count - NamedCards) + " more");
        return "This deck has cards that are not in Standard: " + string.Join(", ", names)
            + ". Edit the deck and replace them to play.";
    }
    private static string CardName(string id)
    {
        try { return SabberStoneCore.Model.Cards.FromId(id)?.Name ?? id; }
        catch (KeyNotFoundException) { return id; }
    }
}
