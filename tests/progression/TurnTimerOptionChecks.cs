using BepInEx.Configuration;

internal static class TurnTimerOptionChecks
{
    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "hs-turn-timer-option-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "settings.cfg");
            var file = new ConfigFile(path, false);
            var option = new TurnTimerSettings(file);
            if (option.Enabled) throw new Exception("Timer must default off");
            option.Set(true);
            if (!new TurnTimerSettings(new ConfigFile(path, false)).Enabled) throw new Exception("Timer enable did not persist");
            option.Set(false);
            if (new TurnTimerSettings(new ConfigFile(path, false)).Enabled) throw new Exception("Timer disable did not persist");
            // Failed writes restore the in-memory choice too; the previous durable setting survives.
            File.Delete(path); Directory.CreateDirectory(path);
            try { option.Set(true); throw new Exception("Expected config write failure"); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { }
            if (option.Enabled || !file.SaveOnConfigSet) throw new Exception("Failed save changed timer setting or config policy");
            Console.WriteLine("PASS Turn Timer default, enable/disable persistence, failed save rollback");
        }
        finally { Directory.Delete(root, true); }
    }
}
