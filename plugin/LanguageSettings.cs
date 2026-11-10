using BepInEx.Configuration;

// Global, like FPS Counter. Read once at startup (GameLanguage.StartLocale); a change applies from the next start. A failed save
// restores the previous value.
internal sealed class LanguageSettings
{
    private readonly ConfigFile file;
    private readonly ConfigEntry<string> locale;
    internal string Locale => locale.Value;

    internal LanguageSettings(ConfigFile file)
    {
        this.file = file;
        locale = file.Bind("Display", "Language", "",
            "Game language for the next start, as a client locale (enUS, deDE, frFR, ...). Empty: the client's own (English). "
            + "Only languages whose files are in this copy of the game are used. Also in Options > SoloStone Options.");
    }

    internal void Set(string value)
    {
        string previous = Locale;
        bool automatic = file.SaveOnConfigSet;
        try
        {
            file.SaveOnConfigSet = false;
            locale.Value = value;
            try { file.Save(); }
            catch { locale.Value = previous; throw; }
        }
        finally { file.SaveOnConfigSet = automatic; }
    }
}
