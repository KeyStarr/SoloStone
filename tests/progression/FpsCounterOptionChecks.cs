using BepInEx.Configuration;

internal static class FpsCounterOptionChecks
{
    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "hs-fps-counter-option-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "settings.cfg");
            var file = new ConfigFile(path, false);
            var option = new FpsCounterSettings(file);
            if (option.Enabled) throw new Exception("FPS counter must default off");
            option.Set(true);
            if (!new FpsCounterSettings(new ConfigFile(path, false)).Enabled) throw new Exception("FPS counter enable did not persist");
            option.Set(false);
            if (new FpsCounterSettings(new ConfigFile(path, false)).Enabled) throw new Exception("FPS counter disable did not persist");
            // A failed write restores the in-memory choice and the config policy; the previous durable setting survives.
            File.Delete(path); Directory.CreateDirectory(path);
            try { option.Set(true); throw new Exception("Expected config write failure"); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { }
            if (option.Enabled || !file.SaveOnConfigSet) throw new Exception("Failed save changed FPS counter setting or config policy");
            Console.WriteLine("PASS FPS Counter default, enable/disable persistence, failed save rollback");
        }
        finally { Directory.Delete(root, true); }
    }
}
