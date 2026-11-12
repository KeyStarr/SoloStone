using System.Reflection.Metadata;
using System.Text.RegularExpressions;

internal static class UnityRuntimeGuard
{
    private static readonly Regex BannedSource = new(
        @"\b(?:System\s*\.\s*Runtime\s*\.\s*Serialization|System\s*\.\s*Text\s*\.\s*Json|System\s*\.\s*ServiceModel|Newtonsoft|DataContractJsonSerializer|DataContractSerializer|DataContract(?:Attribute)?|DataMember(?:Attribute)?|JsonSerializer)\b");

    internal static void Check(string root, MetadataReader plugin)
    {
        var failures = new List<string>();
        foreach (string path in Directory.EnumerateFiles(Path.Combine(root, "plugin"), "*.cs", SearchOption.AllDirectories)) {
            string source = File.ReadAllText(path);
            foreach (Match match in BannedSource.Matches(source)) {
                int line = source.Take(match.Index).Count(c => c == '\n') + 1;
                failures.Add(Path.GetRelativePath(root, path) + ":" + line + ": unavailable Unity API " + match.Value);
            }
        }
        // BCL names come from main addeae84's built plugin; see README.md for provenance.
        var allowed = Directory.EnumerateFiles(Path.Combine(root, "references/game"), "*.dll")
            .Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.Ordinal);
        allowed.UnionWith(new[] { "mscorlib", "System", "System.Core", "System.Xml" });
        // Explicit deployed plugin dependencies; not part of the client's Managed folder.
        allowed.UnionWith(new[] { "BepInEx", "0Harmony", "SabberStoneCore" });
        foreach (var handle in plugin.AssemblyReferences) {
            string name = plugin.GetString(plugin.GetAssemblyReference(handle).Name);
            if (!allowed.Contains(name) || UnavailableNamespace(name))
                failures.Add("Plugin assembly references unavailable/unapproved assembly: " + name);
        }
        foreach (var handle in plugin.TypeReferences) {
            var type = plugin.GetTypeReference(handle);
            string space = plugin.GetString(type.Namespace), name = plugin.GetString(type.Name);
            if (UnavailableNamespace(space) || name == "DataContractJsonSerializer" || name == "DataContractSerializer"
                || name == "DataContractAttribute" || name == "DataMemberAttribute")
                failures.Add("Plugin assembly references unavailable Unity API: " + space + "." + name);
        }
        if (failures.Count != 0) throw new Exception(string.Join("\n", failures));
        Console.WriteLine("PASS UnityRuntimeGuard: plugin source, assembly references and serializer type references");
    }
    private static bool UnavailableNamespace(string name) => new[] {
        "System.Runtime.Serialization", "System.Text.Json", "System.ServiceModel", "Newtonsoft"
    }.Any(prefix => name == prefix || name.StartsWith(prefix + ".", StringComparison.Ordinal));
}
