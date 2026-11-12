using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

public enum ProofKind { Facility, Cards, PoweredUp, Native }

[AttributeUsage(AttributeTargets.Method)]
public sealed class GroupProofAttribute : Attribute
{
    internal string Name { get; }
    internal ProofKind Kind { get; }
    public GroupProofAttribute(string name, ProofKind kind = ProofKind.Facility)
    { Name = name; Kind = kind; }
}

internal static class GroupProofs
{
    private static readonly Lazy<(GroupProofAttribute Spec, Action Run)[]> Entries = new(() =>
        Discover(typeof(GroupProofs).Assembly));

    internal static (GroupProofAttribute Spec, Action Run)[] Discover(Assembly assembly)
    {
        var entries = assembly.GetTypes().SelectMany(type => type.GetMethods(
                BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Select(method => (Method: method, Spec: method.GetCustomAttribute<GroupProofAttribute>()))
            .Where(entry => entry.Spec != null).ToArray();
        foreach (var entry in entries)
            if (!Regex.IsMatch(entry.Spec.Name, "^[a-z0-9]+(?:-[a-z0-9]+)*$")
                || !Enum.IsDefined(typeof(ProofKind), entry.Spec.Kind)
                || !entry.Method.IsStatic || entry.Method.ContainsGenericParameters
                || entry.Method.ReturnType != typeof(void) || entry.Method.GetParameters().Length != 0)
                throw new InvalidOperationException("Invalid group proof: " + entry.Method);
        if (entries.Select(entry => entry.Spec.Name).Distinct(StringComparer.Ordinal).Count() != entries.Length)
            throw new InvalidOperationException("Duplicate group proof name");
        return entries.OrderBy(entry => entry.Spec.Name, StringComparer.Ordinal)
            .Select(entry => (entry.Spec, (Action)entry.Method.CreateDelegate(typeof(Action)))).ToArray();
    }

    internal static void Run(ProofKind kind)
    {
        ValidateRequired(kind);
        foreach (var entry in Entries.Value.Where(entry => entry.Spec.Kind == kind)) entry.Run();
    }

    private static void ValidateRequired(ProofKind kind)
    {
        var assembly = typeof(GroupProofs).Assembly;
        foreach (string resource in assembly.GetManifestResourceNames()
                     .Where(name => name.StartsWith("GroupProofRequirements/", StringComparison.Ordinal)))
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(resource));
            string line;
            const string prefix = "Required group proofs:";
            while ((line = reader.ReadLine()) != null)
            {
                if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string[] requirements = line.Substring(prefix.Length).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (requirements.Length == 0) throw new InvalidOperationException("Empty group proof requirements: " + resource);
                foreach (string requirement in requirements)
                {
                    string[] parts = requirement.Split(':');
                    if (parts.Length != 2 || !Enum.TryParse(parts[0], out ProofKind requiredKind)
                        || !Enum.IsDefined(typeof(ProofKind), requiredKind)
                        || !Regex.IsMatch(parts[1], "^[a-z0-9]+(?:-[a-z0-9]+)*$"))
                        throw new InvalidOperationException("Invalid group proof requirement: " + requirement + " in " + resource);
                    if (requiredKind == kind && !Entries.Value.Any(entry => entry.Spec.Kind == kind && entry.Spec.Name == parts[1]))
                        throw new InvalidOperationException("Missing required group proof: " + requirement + " in " + resource);
                }
            }
        }
    }

    internal static bool TryRun(string[] args, Action<Action> runCards = null, bool allowUnmatched = false)
    {
        if (args.Count(arg => arg.StartsWith("--", StringComparison.Ordinal) && arg.EndsWith("-only", StringComparison.Ordinal)) > 1)
            throw new ArgumentException("Select one group proof");
        var selected = Entries.Value.Where(entry => args.Contains("--" + entry.Spec.Name + "-only")).ToArray();
        if (selected.Length == 0 && allowUnmatched) return false;
        string unknown = args.FirstOrDefault(arg => arg.StartsWith("--", StringComparison.Ordinal)
            && arg.EndsWith("-only", StringComparison.Ordinal)
            && !selected.Any(entry => arg == "--" + entry.Spec.Name + "-only"));
        if (unknown != null) throw new ArgumentException("No discovered group proof matches " + unknown);
        if (selected.Length == 0) return false;
        ValidateRequired(selected[0].Spec.Kind);
        if (selected[0].Spec.Kind == ProofKind.Cards)
        {
            if (runCards == null) throw new InvalidOperationException("Card proof runner required");
            runCards(selected[0].Run);
        }
        else selected[0].Run();
        return true;
    }
}
