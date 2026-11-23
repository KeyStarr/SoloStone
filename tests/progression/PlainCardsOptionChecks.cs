using BepInEx.Configuration;

internal static class PlainCardsOptionChecks
{
    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "hs-plain-cards-option-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "settings.cfg");
            var file = new ConfigFile(path, false);
            var option = new PlainCardsSettings(file);
            if (option.Enabled) throw new Exception("Show Plain Cards must default off");
            option.Set(true);
            if (!new PlainCardsSettings(new ConfigFile(path, false)).Enabled) throw new Exception("Show Plain Cards enable did not persist");
            option.Set(false);
            if (new PlainCardsSettings(new ConfigFile(path, false)).Enabled) throw new Exception("Show Plain Cards disable did not persist");
            // A failed write restores the in-memory choice and the config policy; the previous durable setting survives.
            File.Delete(path); Directory.CreateDirectory(path);
            try { option.Set(true); throw new Exception("Expected config write failure"); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { }
            if (option.Enabled || !file.SaveOnConfigSet) throw new Exception("Failed save changed Show Plain Cards setting or config policy");
            Console.WriteLine("PASS Show Plain Cards default, enable/disable persistence, failed save rollback");
        }
        finally { Directory.Delete(root, true); }
    }
}
