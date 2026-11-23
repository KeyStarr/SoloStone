using BepInEx.Configuration;

// Global, like Turn Timer. Matches read it once when they begin; a failed save restores the previous value.
internal sealed class OpponentPetsSettings
{
    private readonly ConfigFile file;
    private readonly ConfigEntry<bool> enabled;
    internal bool Enabled => enabled.Value;

    internal OpponentPetsSettings(ConfigFile file)
    {
        this.file = file;
        enabled = file.Bind("Display", "OpponentPets", true,
            "Show the pet an NPC opponent brings to a match. Off, the opponent has no pet at all; your own pet and its XP are unchanged. Applies from the next match. On by default. Also in Options > SoloStone Options.");
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
