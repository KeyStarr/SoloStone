using BepInEx.Configuration;

internal static class OpponentPetsOptionChecks
{
    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "hs-opponent-pets-option-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "settings.cfg");
            var file = new ConfigFile(path, false);
            var option = new OpponentPetsSettings(file);
            if (!option.Enabled) throw new Exception("Show Opponents' Pets must default on");
            option.Set(false);
            if (new OpponentPetsSettings(new ConfigFile(path, false)).Enabled) throw new Exception("Opponent pets off did not persist");
            option.Set(true);
            if (!new OpponentPetsSettings(new ConfigFile(path, false)).Enabled) throw new Exception("Opponent pets on did not persist");
            // A failed write restores the in-memory choice and the config policy; the previous durable setting survives.
            File.Delete(path); Directory.CreateDirectory(path);
            try { option.Set(false); throw new Exception("Expected config write failure"); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { }
            if (!option.Enabled || !file.SaveOnConfigSet) throw new Exception("Failed save changed the opponent pets setting or config policy");
            Console.WriteLine("PASS Show Opponents' Pets default on, off/on persistence, failed save rollback");
        }
        finally { Directory.Delete(root, true); }
    }
}
