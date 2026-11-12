using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using static TransportInvariants.SelfChecks;

namespace TransportInvariants;
internal static class HeraldCandidateChecks
{
    // Inspect the actual net48 candidate, whose build symbols differ from this test harness.
    internal static void Run()
    {
        foreach (string path in (Environment.GetEnvironmentVariable("HERALD_CANDIDATES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            var type = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition)
                .Single(t => metadata.GetString(t.Name) == "NativeMatch");
            var methods = type.GetMethods().GroupBy(h => metadata.GetString(metadata.GetMethodDefinition(h).Name)).ToDictionary(g => g.Key, g => g.First());
            Check(methods.ContainsKey("AppendHeraldState"), "candidate must compile Herald projection: " + path);
            int token = MetadataTokens.GetToken(methods["AppendHeraldState"]);
            var body = pe.GetMethodBody(metadata.GetMethodDefinition(methods["SendHistory"]).RelativeVirtualAddress).GetILBytes();
            Check(Enumerable.Range(0, body.Length - 4).Any(i => body[i] == 0x28 && BitConverter.ToInt32(body, i + 1) == token),
                "candidate SendHistory must call Herald projection: " + path);
            Console.WriteLine("PASS Herald candidate projection: " + path);
        }
    }
}
