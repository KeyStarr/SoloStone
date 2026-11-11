using SabberStoneCore.Enums;

// Live PC 2026-10-29: a pack holding TIME_209 (Muradin, Fabled) blocked with "ownership delta mismatch" because OwnedCollection
// pinned Fabled primaries at their playset. Owner 2026-10-30 removed virtual ownership: nothing is pinned, so the stored
// and projected counts move together for every card, Fabled included, and native receives the Fabled copy.
internal static class PackFabledProjectionTests
{
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
    static void Rejects(Action action, string label)
    {
        try { action(); } catch (InvalidOperationException) { Console.WriteLine("PASS " + label); return; }
        throw new Exception("Expected rejection: " + label);
    }
    static Dictionary<string, int> Map(params (string Id, int Count)[] rows) => rows.ToDictionary(row => row.Id, row => row.Count, StringComparer.Ordinal);
    static Dictionary<string, int> Ownership(LocalProfile profile) => profile.StoredOwnedCards.ToDictionary(
        card => LocalPackCard.ReceiptKey(card.Id, card.Premium), card => card.Count, StringComparer.Ordinal);

    const string Fabled = "TIME_209";

    internal static void Run()
    {
        Check(typeof(LocalProfile).GetMethod("ProjectionPinned", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic) == null, "no card is projection-pinned");
        Synthetic();
        string root = Path.Combine(Path.GetTempPath(), "pack-fabled-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { RealProfile(root); }
        finally { Directory.Delete(root, true); }
    }

    static void Synthetic()
    {
        foreach (int premium in new[] { 0, 1 })
        {
            string key = LocalPackCard.ReceiptKey(Fabled, premium);
            var grants = NativeUIProjection.NewGrantCounts(new[] { new LocalPackCard { Id = Fabled, Premium = premium } });
            NativeUIProjection.ValidatePackOwnership(Map((key, 1)), Map((key, 2)), grants);
            Check(grants.Count == 1 && grants[key] == 1, "stored Fabled grant validated by receipt key: " + key);
            Rejects(() => NativeUIProjection.ValidatePackOwnership(Map((key, 1)), Map((key, 1)), grants),
                "lost Fabled copy rejected: " + key);
            Rejects(() => NativeUIProjection.ValidatePackOwnership(Map((key, 1)), Map((key, 3)), grants),
                "duplicated Fabled copy rejected: " + key);
            string wrongKey = LocalPackCard.ReceiptKey(Fabled, 1 - premium);
            Rejects(() => NativeUIProjection.ValidatePackOwnership(Map((key, 1)), Map((key, 1), (wrongKey, 1)), grants),
                "wrong-premium Fabled copy rejected: " + key);
        }
        Rejects(() => NativeUIProjection.ValidatePackOwnership(Map(), Map(("CS2_179", 1)), Map()),
            "ungranted ordinary copy rejected");
    }

    static void RealProfile(string root)
    {
        string packType = FairPackRules.PackTypes.First(type => type != FairPackRules.Legacy
            && FairPackRules.Eligible(type, FairPackRules.RuleVersion).Any(card => card.Id == Fabled));
        string path = Path.Combine(root, "profile.xml");
        int SeedFor(LocalProfile profile, int premium)
        {
            var counters = profile.PackCounters.Single(row => row.PackType == packType).Counters;
            var acquired = profile.StoredOwnedCards.GroupBy(card => card.Id)
                .ToDictionary(group => group.Key, group => group.Sum(card => card.Acquired), StringComparer.Ordinal);
            for (int seed = 1; seed < 2_000_000; seed++)
            {
                var draw = FairPackRules.Generate(packType, FairPackRules.RuleVersion, (ulong)seed, profile.PackSequence, counters.Copy(),
                    id => acquired.TryGetValue(id, out int count) ? count : 0);
                if (draw.CardIds.Where((id, slot) => id == Fabled && draw.Premiums[slot] == premium).Any()) return seed;
            }
            throw new Exception("no seed draws " + Fabled);
        }
        void Open(LocalProfile profile, int premium, int expectedStoredBefore, string label)
        {
            int seed = SeedFor(profile, premium);
            profile.CommitChange(next => {
                next.PackSeed = seed;
                next.UnopenedPacks.RemoveAll(row => row.PackType == packType);
                next.UnopenedPacks.Add(new LocalPackCount { PackType = packType, Count = 1 });
                return true;
            });
            var storedBefore = Ownership(profile);
            string fabledKey = LocalPackCard.ReceiptKey(Fabled, premium);
            Check(profile.StoredOwnedCount(Fabled, premium) == expectedStoredBefore
                && profile.OwnedCount(Fabled, premium) == expectedStoredBefore,
                label + ": Fabled projects exactly its stored premium copies before the pack");
            var batch = profile.BeginNativePacks(packType, 1);
            Check(batch.Single().Cards.Any(card => card.Id == Fabled && card.Premium == premium),
                label + ": committed pack contains " + fabledKey);
            var grants = NativeUIProjection.NewGrantCounts(batch.SelectMany(pack => pack.Cards));
            NativeUIProjection.ValidatePackOwnership(storedBefore, Ownership(profile), grants);
            Check(grants[fabledKey] == 1 && profile.StoredOwnedCount(Fabled, premium) == expectedStoredBefore + 1
                && profile.OwnedCount(Fabled, premium) == expectedStoredBefore + 1,
                label + ": stored premium ownership gains exactly one copy");
            var nativeCounts = new Dictionary<string, int>(storedBefore, StringComparer.Ordinal);
            int NativeCount(string id, int quality) => nativeCounts.TryGetValue(LocalPackCard.ReceiptKey(id, quality), out int count) ? count : 0;
            var deltas = NativeUIProjection.PackGrantDeltas(profile, grants, NativeCount);
            Check(deltas.Single(card => card.Id == Fabled && card.Premium == premium).Count == 1
                && deltas.Length == grants.Count && deltas.All(card => grants[LocalPackCard.ReceiptKey(card.Id, card.Premium)] == card.Count),
                label + ": native receives every pack grant with its premium intact");
            foreach (var card in deltas)
                nativeCounts[LocalPackCard.ReceiptKey(card.Id, card.Premium)] = NativeCount(card.Id, card.Premium) + card.Count;
            Check(grants.Keys.All(key => {
                var card = LocalPackCard.FromKey(key);
                return NativeCount(card.Id, card.Premium) == Math.Max(profile.StoredOwnedCount(card.Id, card.Premium), profile.OwnedCount(card.Id, card.Premium));
            }) && NativeUIProjection.PackGrantDeltas(profile, grants, NativeCount).Length == 0,
                label + ": final native counts match and refreshed copies are not added twice");
            var storedAfter = Ownership(profile).OrderBy(pair => pair.Key).ToArray();
            string[] receipt = NativeUIProjection.PendingReceipt(batch);
            int sequence = profile.PackSequence, packs = profile.UnopenedPacks, gold = profile.StoredGoldBalance;
            profile.Dispose();
            using (var reopened = new LocalProfile(path))
            {
                var resumed = reopened.BeginNativePacks(packType, 1);
                Check(reopened.PackRevealPending && NativeUIProjection.PendingReceipt(resumed).SequenceEqual(receipt)
                    && receipt.Contains(fabledKey) && Ownership(reopened).OrderBy(pair => pair.Key).SequenceEqual(storedAfter)
                    && reopened.PackSequence == sequence && reopened.UnopenedPacks == packs && reopened.StoredGoldBalance == gold
                    && reopened.OwnedCount(Fabled, premium) == expectedStoredBefore + 1,
                    label + ": restart resumes exact premium receipt without changing durable economy");
                Check(NativeUIProjection.PackGrantDeltas(reopened, grants, NativeCount).Length == 0,
                    label + ": resumed ownership requires no duplicate native grants");
                reopened.CompleteNativePackReveal(reopened.PackSequence, receipt);
            }
            using (var completed = new LocalProfile(path))
                Check(!completed.PackRevealPending && completed.StoredOwnedCount(Fabled, premium) == expectedStoredBefore + 1,
                    label + ": acknowledged premium copy survives another restart");
        }
        var first = new LocalProfile(path);
        Open(first, 0, 0, "first Normal Muradin");
        // Second copy: fair packs only redraw an owned legendary once every pool legendary is owned.
        using (var seed = new LocalProfile(path))
            seed.CommitChange(next => {
                foreach (var card in FairPackRules.Eligible(packType, FairPackRules.RuleVersion).Where(card => card.Rarity == Rarity.LEGENDARY))
                {
                    var row = next.Collection.SingleOrDefault(entry => entry.Id == card.Id);
                    if (row == null) next.Collection.Add(row = new LocalCardCount { Id = card.Id });
                    row.Count = Math.Max(row.Count, 1); row.Acquired = Math.Max(row.Acquired, 1);
                }
                return true;
            });
        Open(new LocalProfile(path), 0, 1, "second Normal Muradin");
        Open(new LocalProfile(path), 1, 0, "first Golden Muradin");
    }
}
