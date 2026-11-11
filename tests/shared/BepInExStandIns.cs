// Stand-in for the part of BepInEx.Configuration that the plugin's option classes (TurnTimerSettings, FpsCounterSettings,
// DeckInferenceSettings) use: a config file with bound entries, an explicit Save() and the SaveOnConfigSet switch.
// The tests that use it check our own rollback and key naming, not BepInEx's file format.
namespace BepInEx.Configuration
{
    internal sealed class ConfigEntry<T>
    {
        internal ConfigEntry(T value) { Value = value; }
        public T Value { get; set; }
    }

    internal sealed class ConfigFile
    {
        private readonly string path;
        private readonly Dictionary<string, string> stored = new();
        private readonly List<(string Id, Func<object> Read)> entries = new();

        internal ConfigFile(string path, bool saveOnInit)
        {
            this.path = path;
            if (File.Exists(path))
                foreach (string line in File.ReadAllLines(path))
                {
                    int split = line.IndexOf('=');
                    if (split > 0) stored[line.Substring(0, split)] = line.Substring(split + 1);
                }
        }

        internal bool SaveOnConfigSet { get; set; } = true;

        internal ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
        {
            string id = section + "." + key;
            var entry = new ConfigEntry<T>(stored.TryGetValue(id, out string text) ? (T)Convert.ChangeType(text, typeof(T)) : defaultValue);
            entries.Add((id, () => entry.Value));
            return entry;
        }

        internal void Save() => File.WriteAllLines(path, entries.Select(entry => entry.Id + "=" + entry.Read()));
    }
}
