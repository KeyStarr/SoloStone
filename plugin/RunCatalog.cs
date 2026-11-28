using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

// Plugin-only. The run modes this build ships: data/runs/*.json, embedded as SoloStone.Runs.<id>-v<n>.json and parsed with
// the strict EmbeddedJson parser once, on first use. A definition that fails to parse fails loudly: it is shipped data.
internal static class RunCatalog
{
    private const string Prefix = "SoloStone.Runs.";
    private static readonly Lazy<IReadOnlyList<RunDefinition>> all = new Lazy<IReadOnlyList<RunDefinition>>(Load);

    internal static IReadOnlyList<RunDefinition> All => all.Value;

    // The run mode whose client scenario this is (Dungeon Run: 2663), or null.
    internal static RunDefinition ForScenario(int scenario) =>
        scenario == 0 ? null : All.FirstOrDefault(definition => definition.Client.Scenario == scenario);

    private static IReadOnlyList<RunDefinition> Load()
    {
        if (NpcWorldJson.Parser == null) NpcWorldJson.Parser = EmbeddedJson.Parse;
        var assembly = Assembly.GetExecutingAssembly();
        var definitions = new List<RunDefinition>();
        foreach (string name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal))
        {
            using (Stream stream = assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream))
            {
                RunDefinition definition = RunDefinition.Parse(reader.ReadToEnd());
                if (definition.ResourceName != name) throw new InvalidDataException(name + " holds " + definition.ResourceName);
                if (definitions.Any(d => d.Client.Scenario == definition.Client.Scenario))
                    throw new InvalidDataException(name + ": scenario " + definition.Client.Scenario + " belongs to two run modes");
                definitions.Add(definition);
            }
        }
        return definitions;
    }
}
