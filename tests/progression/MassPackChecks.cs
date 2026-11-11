using System.Xml.Serialization;

// Native mass pack opening (owner 2026-10-29): one batch commit of N packs must grant and settle exactly like N single
// native opens, survive relaunch while pending, refuse tampering, and leave the save untouched when its commit fails.
internal static class MassPackChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Reject(Action action, string message)
    {
        try { action(); } catch (Exception error) when (error is InvalidOperationException || error is IOException) { Console.WriteLine("PASS " + message); return; }
        throw new Exception("Expected rejection: " + message);
    }

    internal static void Run(string root)
    {
        string dir = Path.Combine(root, "mass-packs"); Directory.CreateDirectory(dir);
        string seed = Path.Combine(dir, "seed", "profile.xml"); Directory.CreateDirectory(Path.GetDirectoryName(seed)!);
        const int packs = 12;
        using (var profile = new LocalProfile(seed))
        {
            profile.CommitChange(next => { next.GoldBalance = LocalEconomy.PackPrice * (packs + 1); return true; });
            for (int i = 0; i < packs + 1; i++) profile.BuyPack();
            Check(profile.UnopenedPacks == packs + 1, "seed save holds " + (packs + 1) + " Standard packs");
        }
        string Copy(string name) { string path = Path.Combine(dir, name, "profile.xml"); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.Copy(seed, path); return path; }
        string singles = Copy("singles"), mass = Copy("mass"), faulted = Copy("faulted");

        // N single native opens, each acknowledged.
        var singleIds = new List<string>();
        using (var profile = new LocalProfile(singles))
            for (int i = 0; i < packs; i++)
            {
                var pack = profile.BeginNativePack(FairPackRules.Standard);
                singleIds.AddRange(pack.Cards.Select(card => card.Key));
                profile.CompleteNativePackReveal(profile.PackSequence, pack.Cards.Select(card => card.Key));
            }

        // One mass opening of the same N.
        string[] batchIds; int sequence;
        using (var profile = new LocalProfile(mass))
        {
            Reject(() => profile.BeginNativePacks(FairPackRules.Standard, LocalProfile.MassPackLimit + 1), "more than the retail limit (40) is refused");
            Reject(() => profile.BeginNativePacks(FairPackRules.Standard, packs + 2), "more packs than owned is refused");
            Reject(() => profile.BeginNativePacks(FairPackRules.Legacy, 2), "Legacy packs never mass open");
            var batch = profile.BeginNativePacks(FairPackRules.Standard, packs);
            batchIds = batch.SelectMany(pack => pack.Cards.Select(card => card.Key)).ToArray();
            sequence = profile.PackSequence;
            Check(batch.Count == packs && batchIds.SequenceEqual(singleIds, StringComparer.Ordinal),
                "a batch draws the same cards, in the same order, as N single opens");
            Check(profile.PackRevealPending && profile.PendingPacks.Count == packs && profile.UnopenedPacks == 1
                && profile.PendingPacks.Last().Cards.Select(card => card.Key).SequenceEqual(profile.LastPack.Cards.Select(card => card.Key)),
                "one commit: N packs spent, pending receipt holds every pack, LastPack is the last");
            Check(profile.BeginNativePacks(FairPackRules.Standard, 3).SelectMany(pack => pack.Cards.Select(card => card.Key)).SequenceEqual(batchIds)
                && profile.BeginNativePack(FairPackRules.Standard).Cards.Select(card => card.Key).SequenceEqual(profile.LastPack.Cards.Select(card => card.Key))
                && profile.PackSequence == sequence, "a pending batch resumes whole, whatever count is asked, without drawing again");
            byte[] beforeHighlights = File.ReadAllBytes(mass);
            Check(NativeUIProjection.CanAcknowledgePackReveal(true, true, true, true, batchIds,
                batchIds.Skip(3).Take(5).Reverse().ToArray(), new[] { true, true, true, true, true }, false, batchIds.Skip(3).Take(5).ToArray()),
                "a revealed highlight group spanning packs is valid without settling the batch");
            Check(profile.PackRevealPending && profile.PendingPacks.Count == packs
                && File.ReadAllBytes(mass).SequenceEqual(beforeHighlights),
                "highlight acknowledgement preserves the whole durable receipt for restart");
            Reject(() => profile.OpenPack(FairPackRules.Standard), "a pending batch blocks another draw");
            Reject(() => profile.CompleteNativePackReveal(sequence, profile.LastPack.Cards.Select(card => card.Key)),
                "acknowledging only the last pack of a batch is refused");
        }
        using (var profile = new LocalProfile(mass))
        {
            Check(profile.PackRevealPending && profile.PackSequence == sequence
                && profile.PendingPacks.SelectMany(pack => pack.Cards.Select(card => card.Key)).SequenceEqual(batchIds),
                "relaunch: the pending batch loads, re-validated pack by pack, unchanged");
            Check(profile.CompleteNativePackReveal(sequence, batchIds) && !profile.PackRevealPending && profile.PendingPacks.Count == 0,
                "native mass Done acknowledges the whole batch");
            // The batch record is gone once settled, so a repeated acknowledgement is refused rather than replayed.
            Reject(() => profile.CompleteNativePackReveal(sequence, batchIds), "a repeated batch acknowledgement is refused");
        }
        Check(File.ReadAllBytes(mass).SequenceEqual(File.ReadAllBytes(singles)),
            "settled save is byte-identical to N single opens (collection, acquisition, counters, sequence, gold, achievements)");

        // A failed batch commit leaves the save exactly as it was; the retry then commits.
        using (var profile = new LocalProfile(faulted))
        {
            byte[] before = File.ReadAllBytes(faulted);
            LocalProfile.CommitFaultHook = phase => { if (phase == "replace") throw new IOException("mass fault"); };
            try { Reject(() => profile.BeginNativePacks(FairPackRules.Standard, 5), "mass commit fault surfaces"); }
            finally { LocalProfile.CommitFaultHook = null; }
            Check(File.ReadAllBytes(faulted).SequenceEqual(before) && !profile.PackRevealPending && profile.UnopenedPacks == packs + 1,
                "a failed batch commit changes nothing");
            Check(profile.BeginNativePacks(FairPackRules.Standard, 5).Count == 5 && profile.PendingPacks.Count == 5, "retry commits the batch");
        }

        // Tampered pending batches are refused on load, without rewriting the file.
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        LocalProfileData Read(string path) { using var stream = File.OpenRead(path); return (LocalProfileData)serializer.Deserialize(stream)!; }
        void Tamper(string name, Action<LocalProfileData> change)
        {
            var data = Read(faulted); change(data);
            string bad = Path.Combine(dir, "bad-" + name + ".xml");
            using (var stream = File.Create(bad)) serializer.Serialize(stream, data);
            byte[] bytes = File.ReadAllBytes(bad);
            Reject(() => { using var ignored = new LocalProfile(bad); }, "tampered batch refused: " + name);
            Check(File.ReadAllBytes(bad).SequenceEqual(bytes), "refusal left the tampered file as it was: " + name);
        }
        Tamper("swapped-card", data => data.PackBatch[1].Cards[0].Id = data.PackBatch[2].Cards[0].Id == data.PackBatch[1].Cards[0].Id
            ? data.PackBatch[3].Cards[0].Id : data.PackBatch[2].Cards[0].Id);
        Tamper("premium", data => data.PackBatch[0].Cards[0].Premium = data.PackBatch[0].Cards[0].Premium == 0 ? 1 : 0);
        Tamper("reordered", data => { var first = data.PackBatch[0]; data.PackBatch[0] = data.PackBatch[1]; data.PackBatch[1] = first; });
        // (Dropping the first pack reads as that pack having settled earlier, like clearing a single pending flag: no history to check.)
        Tamper("dropped-middle-pack", data => data.PackBatch.RemoveAt(2));
        Tamper("single-pack", data => data.PackBatch.RemoveRange(0, data.PackBatch.Count - 1));
        Tamper("not-pending", data => data.PackRevealPending = false);
        Tamper("last-mismatch", data => data.LastPack = data.PackBatch[0]);
        Tamper("legacy", data => { foreach (var pack in data.PackBatch) pack.PackType = FairPackRules.Legacy; data.LastPack.PackType = FairPackRules.Legacy; });

        // Native projection of a batch: five cards per pack, the summary must show exactly the committed cards.
        var ids = Read(faulted).PackBatch.SelectMany(pack => pack.Cards.Select(card => card.Key)).ToArray();
        Check(NativeUIProjection.BatchReceipt(ids, 5).SequenceEqual(ids) && NativeUIProjection.Receipt(ids.Take(5).ToArray()).Length == 5,
            "batch receipt is five cards per pack in draw order");
        Reject(() => NativeUIProjection.BatchReceipt(ids, 4), "a receipt of the wrong size is refused");
        Reject(() => NativeUIProjection.BatchReceipt(ids.Take(5 * 41).ToArray(), 41), "a batch above the limit is refused");
        Check(NativeUIProjection.CanAcknowledgeMassReveal(true, true, true, true, ids, 5, ids.ToArray(), 5, false, true)
            && !NativeUIProjection.CanAcknowledgeMassReveal(true, true, true, true, ids, 5, ids.Reverse().ToArray(), 5, false, true)
            && !NativeUIProjection.CanAcknowledgeMassReveal(true, true, true, true, ids, 5, ids, 4, false, true)
            && !NativeUIProjection.CanAcknowledgeMassReveal(true, true, false, true, ids, 5, ids, 5, false, true)
            && !NativeUIProjection.CanAcknowledgeMassReveal(true, true, true, false, ids, 5, ids, 5, false, true)
            && !NativeUIProjection.CanAcknowledgeMassReveal(true, true, true, true, ids, 5, ids, 5, true, true)
            && !NativeUIProjection.CanAcknowledgeMassReveal(true, false, true, true, ids, 5, ids, 5, false, true),
            "mass Done acknowledges only the armed director's mass flow showing exactly the committed batch, once");
        Check(!NativeUIProjection.CanAcknowledgeMassReveal(true, true, true, true, ids, 5, ids, 5, false, false),
            "exact summary contents cannot settle before highlights and summary Done finish");
        Check(NativeUIProjection.MassOpenable(FairPackRules.NativeBoosterId(FairPackRules.Standard), false)
            && !NativeUIProjection.MassOpenable(FairPackRules.NativeBoosterId(FairPackRules.Legacy), false) && !NativeUIProjection.MassOpenable(-5, false),
            "only known non-Legacy boosters hoover");
        foreach (string type in new[] { FairPackRules.Legacy }.Concat(FairPackRules.PackTypes))
            Check(!NativeUIProjection.MassOpenable(FairPackRules.NativeBoosterId(type), true), "pending reveal blocks hoovering: " + type);
        Tooltip(dir, Copy("tooltip-batch"));
        ExpansionBatch(dir);
    }

    private static void Tooltip(string root, string batchPath)
    {
        const string tooltip = "PLAYER_FLAGS_HAS_SEEN_MASS_PACK_OPENING_TOOLTIP";
        Check(NativeUIProjection.AllowedPreferenceSave("COLLECTION_MANAGER", "anything", null)
            && NativeUIProjection.AllowedPreferenceSave("PLAYER_FLAGS", tooltip, new long[] { 1 }), "collection preferences and exact tooltip seen save are accepted");
        foreach (long[] values in new long[][] { null, Array.Empty<long>(), new long[] { 0 }, new long[] { 2 }, new long[] { 1, 1 } })
            Check(!NativeUIProjection.AllowedPreferenceSave("PLAYER_FLAGS", tooltip, values), "tooltip save requires exactly one numeric value of 1");
        foreach (string subkey in new[] { "PLAYER_FLAGS_UNLOCKED_WILD", "PLAYER_FLAGS_HAS_COMPLETED_APPRENTICE", "UNKNOWN", null })
            Check(!NativeUIProjection.AllowedPreferenceSave("PLAYER_FLAGS", subkey, new long[] { 1 }), "other player flag writes stay denied: " + subkey);
        Check(!NativeUIProjection.AllowedPreferenceSave("QUESTS", tooltip, new long[] { 1 }), "other preference keys stay denied");
        string path = Path.Combine(root, "tooltip.xml");
        using (var profile = new LocalProfile(path))
        {
            profile.SelectCardBack(0);
            Check(!profile.SeenMassPackTooltip && !File.ReadAllText(path).Contains("SeenMassPackTooltip"), "new save writes no mass tooltip element");
            byte[] before = File.ReadAllBytes(path);
            profile.CommitFailure = phase => { if (phase == "replace") throw new IOException("tooltip fault"); };
            try { Reject(() => profile.MarkMassPackTooltipSeen(), "tooltip commit fault surfaces"); }
            finally { profile.CommitFailure = null; }
            Check(!profile.SeenMassPackTooltip && File.ReadAllBytes(path).SequenceEqual(before), "failed tooltip mark changes neither memory nor save");
            int commits = 0;
            profile.CommitFailure = phase => { if (phase == "replace") commits++; };
            Check(profile.MarkMassPackTooltipSeen() && !profile.MarkMassPackTooltipSeen() && commits == 1,
                "marking mass tooltip seen commits exactly once");
            profile.CommitFailure = null;
        }
        using (var profile = new LocalProfile(path))
            Check(profile.SeenMassPackTooltip && !profile.MarkMassPackTooltipSeen(), "mass tooltip seen survives reload");

        string[] ids; int sequence;
        using (var profile = new LocalProfile(batchPath))
        {
            ids = profile.BeginNativePacks(FairPackRules.Standard, 5).SelectMany(pack => pack.Cards.Select(card => card.Key)).ToArray();
            sequence = profile.PackSequence;
            var before = System.Xml.Linq.XDocument.Load(batchPath);
            Check(profile.MarkMassPackTooltipSeen() && profile.PackRevealPending && profile.PendingPacks.Count == 5,
                "marking tooltip during batch preserves pending reveal");
            var after = System.Xml.Linq.XDocument.Load(batchPath);
            after.Root!.Element("SeenMassPackTooltip")!.Remove();
            Check(System.Xml.Linq.XNode.DeepEquals(before, after), "tooltip commit preserves entire pending batch save");
        }
        using (var profile = new LocalProfile(batchPath))
            Check(profile.SeenMassPackTooltip && profile.PackRevealPending && profile.PendingPacks.Count == 5
                && profile.PackSequence == sequence && NativeUIProjection.PendingReceipt(profile.PendingPacks).SequenceEqual(ids),
                "tooltip flag and intact pending batch survive reload");
    }

    private static void ExpansionBatch(string root)
    {
        const int count = 3;
        string type = "CATACLYSM";
        var grantOnly = LocalWelcomeGift.Cards.Select(card => card.CardId).ToHashSet(StringComparer.Ordinal);
        string seed = Path.Combine(root, "event-seed.xml"), singles = Path.Combine(root, "event-singles.xml"), mass = Path.Combine(root, "event-mass.xml");
        using (var profile = new LocalProfile(seed))
            profile.CommitChange(next => { next.UnopenedPacks.Add(new LocalPackCount { PackType = type, Count = count }); return true; });
        File.Copy(seed, singles); File.Copy(seed, mass);
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        LocalProfileData Read(string path) { using var stream = File.OpenRead(path); return (LocalProfileData)serializer.Deserialize(stream)!; }
        var prior = Read(seed);
        var singleIds = new List<string>();
        using (var profile = new LocalProfile(singles))
            for (int i = 0; i < count; i++)
            {
                var pack = profile.BeginNativePack(type);
                Check(pack.Cards.Count == 5 && pack.Cards.All(card => !grantOnly.Contains(card.Id)), "expansion pack contains only random cards: " + i);
                if (i == 0)
                {
                    // Batch validation consumes this prior state when undoing packs last to first.
                    var before = (LocalProfileData)typeof(LocalProfile).GetMethod("ValidateFairPending",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, new object[] { Read(singles) })!;
                    Check(before.Collection.All(row => row.Count == (prior.Collection.SingleOrDefault(card => card.Id == row.Id)?.Count ?? 0)
                            && row.Acquired == (prior.Collection.SingleOrDefault(card => card.Id == row.Id)?.Acquired ?? 0)),
                        "pending validation returns collection from before the pack");
                }
                singleIds.AddRange(pack.Cards.Select(card => card.Key));
                profile.CompleteNativePackReveal(profile.PackSequence, pack.Cards.Select(card => card.Key));
            }
        foreach (string stage in new[] { "write", "flush", "replace" })
        {
            byte[] bytes = File.ReadAllBytes(mass);
            using (var profile = new LocalProfile(mass))
            {
                profile.CommitFailure = point => { if (point == stage) throw new IOException("expansion batch failure"); };
                Reject(() => profile.BeginNativePacks(type, count), "expansion batch failure surfaces: " + stage);
                Check(!profile.PackRevealPending && profile.UnopenedPacks == count && File.ReadAllBytes(mass).SequenceEqual(bytes),
                    "failed expansion batch preserves memory and disk: " + stage);
            }
            using var restarted = new LocalProfile(mass);
            Check(!restarted.PackRevealPending && restarted.UnopenedPacks == count, "failed expansion batch survives restart: " + stage);
        }
        string[] ids; int sequence;
        using (var profile = new LocalProfile(mass))
        {
            var batch = profile.BeginNativePacks(type, count);
            Check(batch.All(pack => pack.Cards.Count == 5 && pack.Cards.All(card => !grantOnly.Contains(card.Id)))
                && grantOnly.All(id => profile.OwnedCount(id) == (prior.Collection.SingleOrDefault(card => card.Id == id)?.Count ?? 0)),
                "expansion batch adds only random cards, no welcome-table cards");
            ids = batch.SelectMany(pack => pack.Cards.Select(card => card.Key)).ToArray(); sequence = profile.PackSequence;
            Check(ids.SequenceEqual(singleIds), "expansion batch draws exactly N singles without extra grants");
        }
        byte[] pending = File.ReadAllBytes(mass);
        using (var profile = new LocalProfile(mass))
        {
            Check(profile.PackRevealPending && profile.PendingPacks.Count == count && File.ReadAllBytes(mass).SequenceEqual(pending),
                "pending expansion batch without extra grants validates on relaunch without rewriting");
            profile.CompleteNativePackReveal(sequence, ids);
        }
        Check(File.ReadAllBytes(mass).SequenceEqual(File.ReadAllBytes(singles)), "settled expansion save without extra grants is byte-identical to N single opens");

        string oldPath = Path.Combine(root, "retired-batch.xml");
        var old = System.Xml.Linq.XDocument.Parse(System.Text.Encoding.UTF8.GetString(pending));
        old.Root!.Add(new System.Xml.Linq.XElement("EventGrantsClaimed", new System.Xml.Linq.XElement("string", "BE_036")));
        foreach (var pack in old.Root.Element("PackBatch")!.Elements().Append(old.Root.Element("LastPack")!))
            pack.Add(new System.Xml.Linq.XElement("EventGrant", new System.Xml.Linq.XElement("LocalEventGrant",
                new System.Xml.Linq.XElement("CardId", "BE_036"), new System.Xml.Linq.XElement("Count", 1))));
        old.Save(oldPath);
        using (var profile = new LocalProfile(oldPath))
        {
            Check(profile.PendingPacks.Count == count && profile.OwnedCount("BE_036") == 0, "retired XML inside mass receipts loads without grants");
            profile.CompleteNativePackReveal(sequence, ids);
        }
        Check(!File.ReadAllText(oldPath).Contains("EventGrant"), "retired mass receipt fields omitted after commit");
        using (var profile = new LocalProfile(oldPath))
            Check(!profile.PackRevealPending && profile.OwnedCount("BE_036") == 0, "retired mass receipt settlement survives restart exactly once");
    }
}
