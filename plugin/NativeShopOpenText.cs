internal static class NativeShopOpenText
{
    internal const string PackRevealPopupId = "solostone-pack-reveal-pending";
    internal const string PackRevealOkId = "packs.pending.okay";

    internal static string DialogButtonId(string popupId, string button) =>
        popupId == PackRevealPopupId && button == "okay" ? PackRevealOkId : null;

    internal static (string Id, string Header, string Text)? OpenBlocker(bool packRevealPending) =>
        packRevealPending ? (PackRevealPopupId, "Unfinished packs",
            "The game closed while packs were open, before you pressed Done. Their cards are already in your collection.\n\n" +
            "Open Packs and drag a pack to see them again, then press Done.") : null;
}
