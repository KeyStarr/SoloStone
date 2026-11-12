using System.Xml.Linq;
using System.Xml.Serialization;

internal static class NoPackGrantTests
{
    internal static void Run(string root)
    {
        int assertions = 0;
        void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        LocalProfileData Read(string path) { using var file = File.OpenRead(path); return (LocalProfileData)serializer.Deserialize(file)!; }
        void Write(string path, LocalProfileData data) { using var file = File.Create(path); serializer.Serialize(file, data); }
        var grantOnly = LocalWelcomeGift.Cards.Select(card => card.CardId).ToHashSet(StringComparer.Ordinal);
        var types = new[] { FairPackRules.Legacy }.Concat(FairPackRules.PackTypes).ToArray();
        foreach (string type in FairPackRules.PackTypes)
            Check(!FairPackRules.Eligible(type, FairPackRules.RuleVersion).Any(card => grantOnly.Contains(card.Id)), "no event random drops: " + type);
        foreach (int version in new[] { 1, LocalCardCatalog.CurrentPoolVersion }.Distinct())
            Check(!LocalCardCatalog.PackPool(version).Any(card => grantOnly.Contains(card.Id)), "no legacy event random drops: " + version);

        foreach (string type in types) {
            string path = Path.Combine(root, "no-pack-grants-" + type + ".xml");
            using (var profile = new LocalProfile(path)) profile.SelectCardBack(0);
            var initial = Read(path);
            initial.UnopenedPacks.Add(new LocalPackCount { PackType = type, Count = 2 });
            Write(path, initial);
            var before = Read(path).Collection.ToDictionary(card => card.Id, card => card.Count);
            foreach (string stage in new[] { "write", "flush", "replace" }) {
                byte[] bytes = File.ReadAllBytes(path);
                using (var profile = new LocalProfile(path)) {
                    var memory = profile.Collection.OrderBy(pair => pair.Key).ToArray();
                    bool injected = false, failed = false;
                    profile.CommitFailure = point => { if (point == stage) { injected = true; throw new IOException("pack failure"); } };
                    try { profile.BeginNativePack(type); } catch (IOException) { failed = true; }
                    Check(injected && failed && memory.SequenceEqual(profile.Collection.OrderBy(pair => pair.Key))
                        && bytes.SequenceEqual(File.ReadAllBytes(path)), "pack failure leaves memory and disk unchanged: " + type + "/" + stage);
                }
                using var reloaded = new LocalProfile(path);
                Check(!reloaded.PackRevealPending && reloaded.UnopenedPacks == 2, "failed pack restart preserves inventory: " + type);
            }
            LocalPackResult receipt;
            using (var profile = new LocalProfile(path)) receipt = profile.BeginNativePack(type);
            Check(receipt.Cards.Count == 5 && receipt.Cards.All(card => !grantOnly.Contains(card.Id)), "five random cards, no event: " + type);
            var delta = receipt.Cards.Where(card => card.DuplicateGold == 0 && card.Premium == 0).GroupBy(card => card.Id).ToDictionary(group => group.Key, group => group.Count());
            var after = Read(path).Collection.ToDictionary(card => card.Id, card => card.Count);
            Check(before.Keys.Concat(after.Keys).Distinct().All(id => after.GetValueOrDefault(id) - before.GetValueOrDefault(id) == delta.GetValueOrDefault(id)),
                "opening adds exactly random contents: " + type);
            // Rule-2 packs may roll Golden/Signature copies of their own random cards; nothing else is premium.
            Check(Read(path).PremiumCollection.Select(card => (card.Id, card.Premium, card.Count)).OrderBy(row => row)
                .SequenceEqual(receipt.Cards.Where(card => card.Premium != 0).GroupBy(card => (card.Id, card.Premium))
                    .Select(group => (group.Key.Id, group.Key.Premium, group.Count())).OrderBy(row => row)),
                "opening adds premium copies only for rolled pack cards: " + type);
            byte[] pending = File.ReadAllBytes(path);
            using (var profile = new LocalProfile(path)) {
                Check(profile.BeginNativePack(type).Cards.Select(card => card.Id).SequenceEqual(receipt.Cards.Select(card => card.Id))
                    && pending.SequenceEqual(File.ReadAllBytes(path)) && profile.UnopenedPacks == 1, "restart replays exactly once: " + type);
                profile.CompleteNativePackReveal(profile.PackSequence, receipt.Cards.Select(card => card.Key));
                profile.BeginNativePack(type);
            }
            using (var profile = new LocalProfile(path)) {
                Check(profile.UnopenedPacks == 0 && profile.PackRevealPending, "second pack durable: " + type);
                Check(grantOnly.All(id => !profile.Collection.TryGetValue(id, out int count) || count == before.GetValueOrDefault(id)), "no fallback event grants: " + type);
            }
            // Retired V10 XML is ignored, including a pending receipt; existing ownership is retained.
            var old = Read(path);
            string owned = grantOnly.First(LocalProfile.KnownLedgerCard);
            old.Collection.Add(new LocalCardCount { Id = owned, Count = 1, Acquired = 1 });
            Write(path, old);
            var xml = XDocument.Load(path);
            xml.Root!.Add(new XElement("EventGrantsClaimed", new XElement("string", owned)));
            xml.Root.Element("LastPack")!.Add(new XElement("EventGrant", new XElement("LocalEventGrant",
                new XElement("CardId", owned), new XElement("Count", 1), new XElement("AcquiredBefore", 0))));
            xml.Save(path);
            using (var profile = new LocalProfile(path)) {
                Check(profile.PackRevealPending && profile.Collection[owned] == 1, "old event fields load safely and preserve ownership: " + type);
                profile.CompleteNativePackReveal(profile.PackSequence, profile.LastPack.Cards.Select(card => card.Key));
            }
            Check(!File.ReadAllText(path).Contains("EventGrant"), "retired event fields omitted on next commit: " + type);
        }
        Console.WriteLine("PASS no pack-open grants: " + assertions + " assertions; pools, exact deltas, failures, restart/replay, retired XML");
    }
}
