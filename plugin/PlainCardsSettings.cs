using BepInEx.Configuration;

// Options > SoloStone Options > Show Plain Cards: the saved switch and its ids. Global, like NPC Deck Guess; a match reads it once when it
// is prepared (ClientBridge.PrepareLocalMatch) and keeps it. A failed save restores the previous value. Display only: the collection,
// decks, crafting, dust and saves never read it. Unity-free for the progression suite.
internal sealed class PlainCardsSettings
{
    internal const string Id = "options.plain-cards", ObjectName = "SoloStonePlainCardsCheckbox", Label = "Show Plain Cards";
    private readonly ConfigFile file;
    private readonly ConfigEntry<bool> enabled;
    internal bool Enabled => enabled.Value;

    internal PlainCardsSettings(ConfigFile file)
    {
        this.file = file;
        enabled = file.Bind("Display", "ShowPlainCards", false,
            "Show golden, diamond and signature cards in their normal version in matches, on both sides. Display only: your collection keeps them. Applies from the next match. Off by default. Also in Options > SoloStone Options.");
    }

    internal void Set(bool value)
    {
        bool previous = Enabled, automatic = file.SaveOnConfigSet;
        try
        {
            file.SaveOnConfigSet = false;
            enabled.Value = value;
            try { file.Save(); }
            catch { enabled.Value = previous; throw; }
        }
        finally { file.SaveOnConfigSet = automatic; }
    }
}
