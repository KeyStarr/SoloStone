using BepInEx.Configuration;

internal static class DeckInferenceOptionChecks
{
    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "hs-deck-inference-option-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "settings.cfg");
            var file = new ConfigFile(path, false);
            var option = new DeckInferenceSettings(file);
            if (option.Enabled) throw new Exception("Deck inference must default off");
            option.Set(true);
            if (!new DeckInferenceSettings(new ConfigFile(path, false)).Enabled) throw new Exception("Deck inference enable did not persist");
            option.Set(false);
            if (new DeckInferenceSettings(new ConfigFile(path, false)).Enabled) throw new Exception("Deck inference disable did not persist");
            // A failed write restores the in-memory choice and the config policy; the previous durable setting survives.
            File.Delete(path); Directory.CreateDirectory(path);
            try { option.Set(true); throw new Exception("Expected config write failure"); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { }
            if (option.Enabled || !file.SaveOnConfigSet) throw new Exception("Failed save changed Deck inference setting or config policy");
            Console.WriteLine("PASS NPC Deck Guess default, enable/disable persistence, failed save rollback");
        }
        finally { Directory.Delete(root, true); }
    }
}
