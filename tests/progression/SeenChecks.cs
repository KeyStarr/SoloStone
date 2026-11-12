using System.Xml.Linq;
using System.Xml.Serialization;

// Collection "NEW" state: acquired copies start unseen, viewing clears durably, old saves and builds read as seen.
internal static class SeenChecks
{
    static readonly XmlSerializer Serializer = new(typeof(LocalProfileData));
    static void Check(bool ok, string message) { if (!ok) throw new Exception("seen: " + message); }
    static LocalProfileData Read(string path) { using var stream = File.OpenRead(path); return (LocalProfileData)Serializer.Deserialize(stream); }
    static void Write(string path, LocalProfileData data) { using var stream = File.Create(path); Serializer.Serialize(stream, data); }
    static void Rejected(string path, string message)
    {
        try { using var p = new LocalProfile(path); } catch (InvalidOperationException) { return; }
        throw new Exception("seen: expected load rejection: " + message);
    }
    static LocalCardSeen Row(string id, int premium, int seen) => new LocalCardSeen { Id = id, Premium = premium, Seen = seen };

    internal static void Run(string root)
    {
        using var cosmetics = new CosmeticsFixture();
        string path = Path.Combine(root, "seen.xml");
        using (var p = new LocalProfile(path)) p.CreateDraft("Seen", "HERO_08");
        Check(XDocument.Load(path).Root!.Element("UnseenCards") == null, "a fully seen profile writes no seen element");
        string packType = FairPackRules.PackTypes.First();
        var save = Read(path);
        save.UnopenedPacks = new List<LocalPackCount> { new LocalPackCount { PackType = packType, Count = 2 } };
        save.DustBalance = 1000;
        // This suite exercises Normal seen batching; premium NEW markers are covered by PremiumPackChecks.
        save.PackSeed = Enumerable.Range(0, 1000).First(seed => {
            var draw = FairPackRules.Generate(packType, 2, (ulong)seed, 0, new FairPackCounters(), _ => 0);
            return draw.Premiums.All(premium => premium == 0) && draw.CardIds.Distinct().Count() == 5;
        });
        Write(path, save);

        string[] gained;
        Dictionary<string, int> before;
        using (var p = new LocalProfile(path))
        {
            Check(p.UnseenCards.Count == 0, "ownership from before seen tracking reads as seen (no NEW flood)");
            before = p.Collection.ToDictionary(pair => pair.Key, pair => pair.Value);
            var pack = p.OpenPack(packType);
            gained = pack.Cards.Select(card => card.Id).Distinct()
                .Where(id => p.OwnedCount(id) > (before.TryGetValue(id, out int n) ? n : 0)).ToArray();
            Check(gained.Length > 0, "the pack added copies");
            foreach (string id in gained)
                Check(p.SeenCopies(id, 0) == (before.TryGetValue(id, out int n) ? n : 0), "pack copies start unseen: " + id);
            Check(p.UnseenCards.Count == gained.Length, "only the pack's new copies are unseen");
            Check(before.Keys.Except(gained).All(id => p.SeenCopies(id, 0) == int.MaxValue), "untouched cards stay seen");
        }
        using (var p = new LocalProfile(path))
        {
            Check(p.UnseenCards.Count == gained.Length && gained.All(id => p.SeenCopies(id, 0) < p.OwnedCount(id)),
                "unseen copies survive a restart");
            Check(p.MarkSeen(gained[0], 0) && p.SeenCopies(gained[0], 0) == int.MaxValue, "viewing a card clears its NEW state");
            Check(!p.MarkSeen(gained[0], 0), "a seen card needs no commit");
            Check(p.UnseenCards.Count == gained.Length - 1, "viewing clears only that card");
        }
        using (var p = new LocalProfile(path))
            Check(p.SeenCopies(gained[0], 0) == int.MaxValue && p.UnseenCards.Count == gained.Length - 1, "viewed state persists after a restart");

        // Viewed cards batch in memory: same profile as one commit per card, other commits carry them, exit loses nothing.
        string[] rest = Read(path).UnseenCards.Select(row => row.Id).ToArray();
        Check(rest.Length >= 3, "enough unseen cards for the batching checks");
        string perCard = Path.Combine(root, "seen-per-card.xml"), batched = Path.Combine(root, "seen-batched.xml"),
            carried = Path.Combine(root, "seen-carried.xml"), exited = Path.Combine(root, "seen-exit.xml");
        foreach (string copy in new[] { perCard, batched, carried, exited }) File.Copy(path, copy);
        using (var p = new LocalProfile(perCard))
            foreach (string id in rest) { Check(p.MarkSeen(id, 0), "per-card mark"); Check(p.FlushSeen() && p.PendingSeenCount == 0, "per-card flush"); }
        using (var p = new LocalProfile(batched))
        {
            string before1 = File.ReadAllText(batched);
            foreach (string id in rest) Check(p.MarkSeen(id, 0), "batched mark");
            Check(p.PendingSeenCount == rest.Length && p.UnseenCards.Count == 0 && rest.All(id => p.SeenCopies(id, 0) == int.MaxValue),
                "queued seen state applies in memory at once");
            Check(File.ReadAllText(batched) == before1, "queued seen state is not written yet (no per-card commit)");
            Check(!p.MarkSeen(rest[0], 0), "a queued card is not queued twice");
            Check(p.FlushSeen() && p.PendingSeenCount == 0 && !p.FlushSeen(), "one flush writes the whole queue");
        }
        Check(XDocument.Load(batched).Root!.Element("UnseenCards")?.ToString() == XDocument.Load(perCard).Root!.Element("UnseenCards")?.ToString()
            && Read(batched).Collection.Count == Read(perCard).Collection.Count, "a batched flush gives the same profile as per-card commits");
        using (var p = new LocalProfile(carried))
        {
            Check(p.MarkSeen(rest[0], 0) && p.MarkSeen(rest[1], 0), "carried marks queue");
            p.CreateDraft("Carried", "HERO_08"); // another commit
            Check(p.PendingSeenCount == 0 && Read(carried).UnseenCards.All(row => row.Id != rest[0] && row.Id != rest[1]),
                "another commit includes the pending seen keys");
            Check(Read(carried).UnseenCards.Any(row => row.Id == rest[2]), "unviewed cards stay NEW");
        }
        using (var p = new LocalProfile(exited)) Check(p.MarkSeen(rest[0], 0), "exit mark queues");
        using (var p = new LocalProfile(exited))
            Check(p.SeenCopies(rest[0], 0) == int.MaxValue && p.SeenCopies(rest[1], 0) < int.MaxValue, "closing the profile flushes the queue");

        // Crafting adds a seen copy (native seenBefore) but keeps an earlier unseen copy NEW; disenchants never reveal one.
        save = Read(path);
        string craft = "CATA_452";
        save.Collection.RemoveAll(card => card.Id == craft);
        save.Collection.Add(new LocalCardCount { Id = craft, Count = 1, Acquired = 1 });
        save.UnseenCards.RemoveAll(row => row.Id == craft);
        save.UnseenCards.Add(Row(craft, 0, 0));
        Write(path, save);
        using (var p = new LocalProfile(path))
        {
            var ledger = new LocalCraftingLedger(p);
            Check(ledger.CommitBatch(craft, CraftDirection.Craft, 1, 1).Ok, "craft commits");
            Check(p.OwnedCount(craft) == 2 && p.SeenCopies(craft, 0) == 1, "crafted copy is seen; the earlier unseen copy stays NEW");
            // Native RemoveCounts keeps SeenCount, so the remaining copy reads seen there too.
            Check(ledger.CommitBatch(craft, CraftDirection.Disenchant, 1, 2).Ok && p.SeenCopies(craft, 0) == int.MaxValue,
                "seen copies are clamped to ownership after a disenchant");
            Check(ledger.CommitBatch(craft, CraftDirection.Craft, 1, 1).Ok && p.SeenCopies(craft, 0) == int.MaxValue,
                "crafting a seen card leaves it seen");
        }

        // Older builds: XmlSerializer skips unknown elements, so a newer save loads and an older rewrite reads as all seen.
        var xml = XDocument.Load(path);
        Check(xml.Root!.Element("UnseenCards") != null, "unseen rows are saved");
        xml.Root.Add(new XElement("FutureField", "1"));
        xml.Save(path);
        using (var p = new LocalProfile(path)) Check(p.UnseenCards.Count > 0, "unknown elements are tolerated on load");
        xml = XDocument.Load(path);
        xml.Root!.Element("UnseenCards")!.Remove();
        xml.Save(path);
        using (var p = new LocalProfile(path)) Check(p.UnseenCards.Count == 0, "a save without seen rows reads as all seen");

        // Stale rows (not owned, or already fully seen) load and are dropped by the next commit; junk is refused.
        save = Read(path);
        save.UnseenCards = new List<LocalCardSeen> { Row("CATA_458", 0, 0), Row(gained[0], 0, 99) };
        Write(path, save);
        using (var p = new LocalProfile(path))
        {
            Check(p.SeenCopies("CATA_458", 0) == 0 || p.OwnedCount("CATA_458") > 0, "stale rows load");
            p.CreateDraft("Seen 2", "HERO_08");
            Check(p.UnseenCards.All(row => p.OwnedCount(row.Id, row.Premium) > row.Seen), "the next commit normalizes stale rows");
        }
        foreach (var junk in new[] {
            new List<LocalCardSeen> { Row(craft, 0, -1) }, new List<LocalCardSeen> { Row(craft, 4, 0) },
            new List<LocalCardSeen> { Row("", 0, 0) }, new List<LocalCardSeen> { Row(craft, 0, 0), Row(craft, 0, 1) } })
        {
            save = Read(path); save.UnseenCards = junk; Write(path, save);
            Rejected(path, "invalid seen rows");
        }
        save = Read(path); save.UnseenCards = new List<LocalCardSeen>(); Write(path, save);
        using (var p = new LocalProfile(path)) { }
        Console.WriteLine("PASS collection seen state: pack copies start NEW, viewing clears durably, crafting seen, old saves/builds safe");
    }
}
