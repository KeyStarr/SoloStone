using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// One id manifest per batch (see GroupProofs for why): parallel branches add files instead of editing a shared list. Empty
// files and repeated or malformed ids are rejected rather than skipped.
internal static class StandardBatchData
{
    internal static SortedDictionary<string, string[]> Load(string directory)
    {
        var batches = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in Directory.GetFiles(directory, "*.txt").OrderBy(path => path, StringComparer.Ordinal))
        {
            string[] ids = File.ReadAllText(path).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (ids.Length == 0) throw new InvalidOperationException("Empty Standard batch: " + path);
            foreach (string id in ids)
                if (!Regex.IsMatch(id, "^[A-Za-z][A-Za-z0-9_]*$") || !seen.Add(id))
                    throw new InvalidOperationException("Invalid or duplicate Standard batch identity: " + id);
            batches.Add(Path.GetFileNameWithoutExtension(path), ids);
        }
        if (batches.Count == 0) throw new InvalidOperationException("No Standard batch manifests");
        return batches;
    }
}
