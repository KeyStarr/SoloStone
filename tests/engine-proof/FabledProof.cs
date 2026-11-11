using System.IO.Compression;
using System.Text.Json;
using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using static Core2026Proof;

internal static class FabledProof
{
    internal static void Run()
    {
        using var stream = new GZipStream(File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz"), CompressionMode.Decompress);
        using var export = JsonDocument.Parse(stream);
        var definitions = export.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(c => c.GetProperty("cardId").GetString());
        string[] bodies = "TIME_009 TIME_020 TIME_209 TIME_211 TIME_609 TIME_619 TIME_850 TIME_852 TIME_875 TIME_890 TIME_005".Split(' ');
        string[][] bundles = {
            new[] { "TIME_009t1", "TIME_009t2" }, new[] { "TIME_020t1", "TIME_020t2" },
            new[] { "TIME_209t", "TIME_209t2" }, new[] { "TIME_211t1", "TIME_211t2" },
            new[] { "TIME_609t1", "TIME_609t2" }, new[] { "TIME_619t", "TIME_619t2" },
            new[] { "TIME_850t", "TIME_850t1" }, new[] { "TIME_852t1", "TIME_852t3" },
            new[] { "TIME_875t", "TIME_875t1" }, new[] { "TIME_890t", "TIME_890t2" },
            Enumerable.Range(1, 9).Select(i => "TIME_005t" + i).ToArray()
        };
        for (int i = 0; i < bodies.Length; i++)
        {
            string body = bodies[i]; var bundle = bundles[i];
            Core2026Proof.Check(FabledRules.IsFabled(body) && definitions[body].GetProperty("tags").GetProperty("2936").GetInt32() == 1, "Fabled export identity " + body);
            Core2026Proof.Check(FabledRules.Bundle(body).SequenceEqual(bundle), "exact bundle " + body);
            var complete = new[] { body }.Concat(bundle).ToArray();
            Core2026Proof.Check(!FabledRules.DeckErrors(complete).Any(), "complete bundle accepted " + body);
            Core2026Proof.Check(FabledRules.ExpandForDeckbuilding(new[] { "CS2_182", body }).SequenceEqual(new[] { "CS2_182" }.Concat(complete)), "deckbuilding expansion " + body);
            Core2026Proof.Check(FabledRules.DeckErrors(new[] { body }).Any(), "missing bundle " + body);
            foreach (string companion in bundle)
            {
                Core2026Proof.Check(FabledRules.IsCompanion(companion) && definitions[companion].GetProperty("tags").GetProperty("4445").GetInt32() == 1, "companion export identity");
                Core2026Proof.Check(FabledRules.DeckErrors(new[] { companion }).Any(), "orphan companion");
                Core2026Proof.Check(FabledRules.DeckErrors(complete.Where(id => id != companion)).Any(), "missing single companion");
                Core2026Proof.Check(FabledRules.DeckErrors(complete.Append(companion)).Any(), "extra companion");
            }
            Core2026Proof.Check(FabledRules.DeckErrors(complete.Append(body)).Any(), "second Fabled copy needs second bundle");
            Core2026Proof.Check(FabledRules.DeckErrors(complete.Concat(complete)).Any() == (body == "TIME_005"), "per-copy multiplicity (ordinary legendary limit enforced by caller)");
        }
        Core2026Proof.Check(!FabledRules.IsFabled("CS2_182") && !FabledRules.IsCompanion("CS2_182") && !FabledRules.Bundle("CS2_182").Any(), "ordinary card");
        Core2026Proof.Check(!FabledRules.DeckErrors(new[] { "CS2_182" }).Any() && !FabledRules.DeckErrors(Array.Empty<string>()).Any(), "ordinary and empty decks");
        Console.WriteLine("PASS Fabled primitive: 11 exact bundles, per-copy counts, missing/extra/orphan companions, duplicate Fabled+ rejection, expansion");
    }
}
