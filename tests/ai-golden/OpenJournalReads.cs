using System.Collections.Generic;
using System.IO;
using System.Text;

// Reads a journal that its recorder still holds open for writing. File.ReadAll* asks for FileShare.Read, which Windows
// refuses while another handle writes the file; this allows the writer, as a player's own tools reading a live journal must.
internal static class OpenJournalReads
{
    private static FileStream Open(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    public static byte[] Bytes(string path)
    {
        using (var stream = Open(path))
        using (var output = new MemoryStream()) { stream.CopyTo(output); return output.ToArray(); }
    }

    public static string[] Lines(string path)
    {
        var lines = new List<string>();
        using (var reader = new StreamReader(Open(path), Encoding.UTF8))
            for (string line; (line = reader.ReadLine()) != null;) lines.Add(line);
        return lines.ToArray();
    }
}
