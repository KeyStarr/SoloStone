using SabberStoneCore.Model;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace HsSim;

public sealed class InvariantException(string invariant, string detail) : Exception(invariant + ": " + detail)
{
    public string Invariant { get; } = invariant;
}

#if SOURCE_ENGINE
// The existing schema-3 manifest hash is also the content-addressed snapshot reference.
public static class PoolSnapshots
{
    private static string ContentHash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static string PathFor(string hash)
    {
        if (hash == null || hash.Length != 64 || hash.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new InvariantException("replay.pool", "missing or malformed pool manifest hash");
        return Path.Combine("tests/goldens/pools", hash + ".xml");
    }

    public static OfflinePoolManifest Read(string path, string hash)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            // The legacy manifest digest omits some persisted metadata; check every XML byte too.
            if (File.ReadAllText(path + ".sha256").Trim() != ContentHash(bytes))
                throw new InvalidDataException("pool snapshot content checksum mismatch");
            var manifest = OfflinePoolPolicy.Parse(XDocument.Load(new MemoryStream(bytes))).BuildManifest();
            if (manifest.Hash != hash) throw new InvalidDataException("pool snapshot hash mismatch");
            return manifest;
        }
        catch (Exception error) when (error is IOException or System.Xml.XmlException or InvalidDataException)
        { throw new InvariantException("replay.pool", "invalid pool snapshot: " + error.Message); }
    }

    public static OfflinePoolManifest ForReplay(string hash)
    {
        return Read(PathFor(hash), hash);
    }

    public static OfflinePoolManifest Save(string snapshot = null)
    {
        var manifest = snapshot == null ? OfflinePoolPolicy.Load().BuildManifest()
            : Read(snapshot, Path.GetFileNameWithoutExtension(snapshot));
        string path = PathFor(manifest.Hash);
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var input = snapshot == null
                ? typeof(OfflinePoolPolicy).Assembly.GetManifestResourceStream("SabberStoneCore.Resources.OfflinePoolCatalog.xml")
                : File.OpenRead(snapshot);
            using var buffer = new MemoryStream();
            input.CopyTo(buffer);
            byte[] bytes = buffer.ToArray();
            using (var output = new FileStream(path, FileMode.CreateNew)) output.Write(bytes);
            File.WriteAllText(path + ".sha256", ContentHash(bytes) + "\n");
        }
        return Read(path, manifest.Hash);
    }
}
#endif

