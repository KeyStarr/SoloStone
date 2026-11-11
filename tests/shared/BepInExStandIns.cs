// Stand-in for the part of BepInEx.Configuration that the plugin's option classes (TurnTimerSettings, FpsCounterSettings,
// DeckInferenceSettings) use: a config file with bound entries, an explicit Save() and the SaveOnConfigSet switch.
// Like BepInEx, setting an entry's Value saves the file at once while SaveOnConfigSet is true, so the option classes
// that switch it off first (and roll back a failed Save) are really exercised. It does not model BepInEx's file format.
namespace BepInEx.Configuration
{
    internal sealed class ConfigEntry<T>
    {
        private readonly ConfigFile owner;
        private T value;
        internal ConfigEntry(ConfigFile owner, T value) { this.owner = owner; this.value = value; }
        public T Value
        {
            get => value;
            set { this.value = value; if (owner.SaveOnConfigSet) owner.Save(); }
        }
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
            var entry = new ConfigEntry<T>(this, stored.TryGetValue(id, out string text) ? (T)Convert.ChangeType(text, typeof(T)) : defaultValue);
            entries.Add((id, () => entry.Value));
            return entry;
        }

        internal void Save() => File.WriteAllLines(path, entries.Select(entry => entry.Id + "=" + entry.Read()));
    }
}
