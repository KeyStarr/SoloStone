using System.Text.Json;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

static class Program
{
    const ulong Seed = 0x534f4c4f53544f4eUL;
    static readonly string[] PreBE = { "EMERALD_DREAM", "THE_LOST_CITY", "TIME_TRAVEL", "CATACLYSM", "ESCAPEFROM_VIOLET_HOLD" };
    static int Cap(string id) => Cards.FromId(id).Rarity == Rarity.LEGENDARY ? 1 : 2;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Reject(Action action)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new Exception("Invalid input accepted");
    }
    static string Receipt(FairPackDraw draw) => string.Join(",", draw.CardIds) + ":" +
        $"{draw.After.PacksOpened}/{draw.After.PacksSinceLegendary}/{draw.After.PacksSinceEpic}/{draw.After.FirstLegendaryReceived}";

    static void Main()
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        RandomSequences();
        Calibration();
        Manifest();
        SignatureCapabilities();
        PackExclusions();
        ActiveMeta();
        Boundaries();
        Golden();
        int packs = Environment.GetEnvironmentVariable("HS_FAIRPACK_FULL") == "1" ? 1_000_000 : 100_000;
        foreach (string type in FairPackRules.PackTypes) Statistics(type, packs);
        Console.WriteLine("FairPackTests PASS");
    }

    static void RandomSequences()
    {
        // The old seed XOR cancels the type hash, shifting consecutive streams by one roll.
        var random = typeof(FairPackRules).GetNestedType("PackRandom", System.Reflection.BindingFlags.NonPublic)!;
        var next = random.GetMethod("Next", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        foreach (string type in FairPackRules.PackTypes)
        {
            ulong hash = 14695981039346656037UL;
            unchecked { foreach (char c in type) { hash ^= c; hash *= 1099511628211UL; } }
            ulong[] Rolls(long sequence)
            {
                object rng = Activator.CreateInstance(random, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                    null, new object[] { hash, sequence, type }, null)!;
                return Enumerable.Range(0, 16).Select(_ => (ulong)next.Invoke(rng, null)!).ToArray();
            }
            foreach (long k in Enumerable.Range(0, 128).Select(i => (long)i).Append(long.MaxValue - 1))
                Check(!Rolls(k).Intersect(Rolls(k + 1)).Any(), "Consecutive pack RNG streams overlap: " + type + " sequence=" + k);
        }
    }

    static void Calibration()
    {
        foreach (var target in new[] { (rate: .054738, pity: 40, weight: FairPackRules.LegendaryWeight),
                                      (rate: .217663, pity: 10, weight: FairPackRules.EpicWeight) })
        {
            double lo = 0, hi = target.rate;
            for (int i = 0; i < 80; i++)
            {
                double hazard = (lo + hi) / 2;
                if (hazard / (1 - Math.Pow(1 - hazard, target.pity)) > target.rate) hi = hazard;
                else lo = hazard;
            }
            int weight = (int)Math.Round((1 - Math.Pow(1 - (lo + hi) / 2, .2)) * FairPackRules.WeightTotal);
            Check(Math.Abs(weight - target.weight) <= 1, "Weights must calibrate with hard pity included");
            Console.WriteLine($"calibration target={target.rate:P4} pity={target.pity} slot-weight={weight}/{FairPackRules.WeightTotal}");
        }
        Console.WriteLine($"statistics seed=0x{Seed:x16}");
    }

    static void SignatureCapabilities()
    {
        var ids = LocalCardPremium.SignatureCards.ToArray();
        Check(ids.Length == 307 && ids.All(LocalCardPremium.HasPackSignature), "Rule 2 pins all metas' Signature union");
        Check(LocalCardPremium.HasPackSignature("BE_009") && !LocalCardPremium.HasSignature("BE_009", "violet-hold")
            && LocalCardPremium.HasSignature("BE_009", "black-empire"), "Pack Signature capability is independent of active meta");
        LocalCardPremium.ValidatePackSignatures(ids.Reverse().Concat(ids));
        foreach (var changed in new[] { ids.Skip(1), ids.Append("FUTURE_SIGNATURE") }) {
            bool rejected = false;
            try { LocalCardPremium.ValidatePackSignatures(changed); } catch (System.IO.InvalidDataException) { rejected = true; }
            Check(rejected, "Changed Signature union accepted without new pack rule version");
        }
    }

    static void Manifest()
    {
        using var manifest = JsonDocument.Parse(Resource("FairPackEligibilityV1.json"));
        var policy = OfflinePoolPolicy.Load();
        var active = policy.PackEligibleSets.Where(s => s != "CORE").ToHashSet();
        var types = new[] { "STANDARD" }.Concat(manifest.RootElement.GetProperty("knownSets").EnumerateArray()
            .Select(s => s.GetString()!).Where(active.Contains)).ToArray();
        Check(types.SequenceEqual(FairPackRules.PackTypes), "Active types in policy order");
        Check(types.SequenceEqual(new[] { "STANDARD" }.Concat(PreBE)) && policy.ActiveMetaId == "violet-hold",
            "Real pool selects STANDARD plus five pre-BE expansion sets");
        Check(types.All(t => FairPackRules.NativeBoosterId(t) == manifest.RootElement.GetProperty("boosters").GetProperty(t).GetInt32()), "Native pack art IDs");
        const string canonicalMeta = "violet-hold\nEMERALD_DREAM,THE_LOST_CITY,TIME_TRAVEL,CATACLYSM,ESCAPEFROM_VIOLET_HOLD\nESCAPEFROM_VIOLET_HOLD\n";
        Check(FairPackRules.MetaHash == Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonicalMeta))).ToLowerInvariant(),
            "Meta hash pins canonical active meta");
        Check(FairPackRules.IsKnown("LEGACY") && !FairPackRules.PackTypes.Contains("LEGACY") && FairPackRules.NativeBoosterId("LEGACY") == 1, "Legacy only routed");
        Check(FairPackRules.NewestSet == policy.NewestSet && FairPackRules.NewestSet == "ESCAPEFROM_VIOLET_HOLD", "Newest reward set comes from real pool");
        Check(!FairPackRules.IsKnown(null) && !FairPackRules.IsKnown("standard"), "Exact type identity");
        var union = types.Skip(1).SelectMany(t => FairPackRules.Eligible(t, 1)).Select(c => c.Id).Order().ToArray();
        Check(union.SequenceEqual(FairPackRules.Eligible("STANDARD", 1).Select(c => c.Id)), "Standard union");
        foreach (string type in types.Skip(1))
        {
            var expected = manifest.RootElement.GetProperty("sets").GetProperty(type).GetProperty("cards").EnumerateArray().ToArray();
            var actual = FairPackRules.Eligible(type, 1);
            Check(expected.Select(c => c.GetProperty("id").GetString()).SequenceEqual(actual.Select(c => c.Id)), "Embedded manifest consumed exactly");
            Check(actual.All(c => c.Collectible && (int)c.Set == manifest.RootElement.GetProperty("sets").GetProperty(type).GetProperty("setId").GetInt32()), "Collectible original set printing");
            Check(actual.Zip(expected).All(p => p.First.Rarity.ToString() == p.Second.GetProperty("rarity").GetString()), "Export rarity agrees with engine");
        }
        string[] grants = { "EDR_000", "EDR_105", "EDR_844", "TLC_106", "TLC_110", "TLC_252", "TLC_468", "TIME_000", "TIME_003", "TIME_038", "TIME_064", "CATA_615", "CATA_720", "JAIL_407", "JAIL_850", "BE_036", "BE_055", "BE_EVENT_100", "BE_EVENT_101", "BE_EVENT_102", "BE_EVENT_103" };
        Check(!union.Intersect(grants).Any(), "Special Normal grants excluded");
        Check(manifest.RootElement.GetProperty("sets").GetProperty("BE").GetProperty("count").GetInt32() == 133, "BE eligibility catalogue retained");
        if (active.Contains("BE"))
            Check(new[] { "BE_004", "BE_013", "BE_264", "BE_703", "BE_801", "BE_852" }.All(union.Contains), "Premium-only BE grants retain Normal printing");
        foreach (var exception in new[] { (set: "CATACLYSM", id: "CATA_553"), (set: "ESCAPEFROM_VIOLET_HOLD", id: "CAP_407"), (set: "CATACLYSM", id: "MEND_900") })
            Check(union.Contains(exception.id) == active.Contains(exception.set), "Pack-eligible grant/bundle exceptions follow meta");
        Console.WriteLine($"real meta={policy.ActiveMetaId} types={string.Join(",", FairPackRules.PackTypes)} newest={FairPackRules.NewestSet} hash={FairPackRules.MetaHash}");
    }

    static byte[] Resource(string name)
    {
        using var stream = typeof(FairPackRules).Assembly.GetManifestResourceStream(name)!;
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }

    static void PackExclusions()
    {
        var policy = OfflinePoolPolicy.Load();
        foreach (string type in FairPackRules.PackTypes)
            Check(FairPackRules.Eligible(type, 1).All(c => (int)c.Set != 1941 && !policy.ExtraCards.Contains(c.Id)),
                "Event and active-meta extra cards excluded from " + type);
        var extra = FairPackRules.Eligible("EMERALD_DREAM", 1)[0].Clone();
        // A mislabeled extra must still fail even if its set looks pack-eligible.
        typeof(Card).GetProperty(nameof(Card.Id))!.SetValue(extra, policy.ExtraCards.First());
        var eventCard = FairPackRules.Eligible("EMERALD_DREAM", 1)[1].Clone();
        eventCard.Tags[GameTag.CARD_SET] = 1941;
        foreach (var excluded in new[] { extra, eventCard })
        {
            bool rejected = false;
            try { _ = new FairPackRules.Pool(FairPackRules.Eligible("STANDARD", 1).Append(excluded)); }
            catch (InvalidOperationException e) { rejected = e.Message == "Pack pool contains excluded card: " + excluded.Id; }
            Check(rejected, "Pool must reject extra/Event card at load: " + excluded.Id);
        }
    }

    static void ActiveMeta()
    {
        // Reverse meta order to prove policy release order wins.
        var pre = FairPackRules.LoadCatalogue("violet-hold", PreBE.Reverse().Append("CORE"), "ESCAPEFROM_VIOLET_HOLD");
        Check(pre.PackTypes.SequenceEqual(new[] { "STANDARD" }.Concat(PreBE)), "Pre-BE pack types");
        Check(pre.NewestSet == "ESCAPEFROM_VIOLET_HOLD" && !pre.IsKnown("BE"), "Pre-BE newest and inactive BE");
        Check(pre.Eligible("STANDARD", 1).Count == 862 && !pre.Eligible("STANDARD", 1).Any(c => c.Id.StartsWith("BE_")), "Pre-BE Standard excludes BE cards");
        var union = PreBE.SelectMany(t => pre.Eligible(t, 1)).Select(c => c.Id).Order().ToArray();
        Check(union.SequenceEqual(pre.Eligible("STANDARD", 1).Select(c => c.Id)), "Pre-BE active Standard union");
        Reject(() => pre.NativeBoosterId("BE"));
        Reject(() => pre.Eligible("BE", 1));
        Check(pre.IsKnown("LEGACY") && pre.NativeBoosterId("LEGACY") == 1, "Pre-BE Legacy routing retained");
        Reject(() => pre.Eligible("LEGACY", 1));
        var be = FairPackRules.LoadCatalogue("black-empire", new[] { "BE", "CORE" }.Concat(PreBE.Reverse()), "BE");
        Check(be.PackTypes.SequenceEqual(new[] { "STANDARD" }.Concat(PreBE).Append("BE")), "BE pack types in policy order");
        Check(be.IsKnown("BE") && be.NewestSet == "BE" && be.NativeBoosterId("BE") == 1057, "BE active and newest");
        Check(be.Eligible("BE", 1).Count == 133 && be.Eligible("STANDARD", 1).Count == 995, "BE Standard union");
        Check(be.Eligible("STANDARD", 1).Select(c => c.Id).Except(union).Order()
            .SequenceEqual(be.Eligible("BE", 1).Select(c => c.Id)), "Only BE cards added to Standard");
        Check(pre.MetaHash != be.MetaHash, "Meta hashes distinguish active pools");
        Check(pre.MetaHash == FairPackRules.MetaHash && pre.MetaHash == FairPackRules.LoadCatalogue("violet-hold", PreBE, "ESCAPEFROM_VIOLET_HOLD").MetaHash,
            "Canonical hash ignores input set order and CORE");
        Check(pre.MetaHash != FairPackRules.LoadCatalogue("other-id", PreBE, "ESCAPEFROM_VIOLET_HOLD").MetaHash, "Meta hash includes id");
        var earlierNewest = FairPackRules.LoadCatalogue("violet-hold", PreBE, "EMERALD_DREAM");
        Check(earlierNewest.NewestSet == "EMERALD_DREAM" && earlierNewest.PackTypes.SequenceEqual(pre.PackTypes)
            && earlierNewest.MetaHash != pre.MetaHash, "Explicit newestSet wins over release order and participates in hash");
        foreach (var bad in new (string id, string[] sets, string newest)[] {
            ("bad", new[] { "CORE", "UNKNOWN" }, "UNKNOWN"), ("bad", new[] { "STANDARD" }, "STANDARD"),
            ("bad", new[] { "LEGACY" }, "LEGACY"), ("bad", new[] { "CORE" }, "CORE"), ("bad", Array.Empty<string>(), "BE"),
            ("bad", null!, "BE"), ("bad", new[] { "" }, ""), ("bad", new[] { (string)null! }, "BE"),
            ("", PreBE, "ESCAPEFROM_VIOLET_HOLD"), (null!, PreBE, "ESCAPEFROM_VIOLET_HOLD"),
            ("bad\nid", PreBE, "ESCAPEFROM_VIOLET_HOLD"), ("bad", PreBE, "BE"), ("bad", PreBE, "CORE"), ("bad", PreBE, null!) })
        {
            bool rejected = false;
            try { FairPackRules.LoadCatalogue(bad.id, bad.sets, bad.newest); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Missing or malformed active meta must fail at load");
        }
        foreach (var catalogue in new[] { pre, be })
            foreach (string type in catalogue.PackTypes)
                Check(catalogue.Eligible(type, 1).All(c => (int)c.Set != 1941 && !OfflinePoolPolicy.Load().ExtraCards.Contains(c.Id)),
                    "Synthetic meta excludes Event and extra cards from " + type);
        Console.WriteLine($"meta pre-BE={string.Join(",", pre.PackTypes)} newest={pre.NewestSet}; with-BE={string.Join(",", be.PackTypes)} newest={be.NewestSet}");
    }

    static void Boundaries()
    {
        Reject(() => FairPackRules.Eligible("LEGACY", 1));
        Reject(() => FairPackRules.Eligible("STANDARD", 3));
        Reject(() => FairPackRules.NativeBoosterId("unknown"));
        Reject(() => FairPackRules.Generate("STANDARD", 1, 0, -1, new(), _ => 0));
        Reject(() => FairPackRules.Generate("STANDARD", 1, 0, 0, null!, _ => 0));
        Reject(() => FairPackRules.Generate("STANDARD", 1, 0, 0, new(), null!));
        foreach (var bad in new[] {
            new FairPackCounters { PacksOpened = -1 },
            new FairPackCounters { PacksOpened = 100, PacksSinceLegendary = 40, FirstLegendaryReceived = true },
            new FairPackCounters { PacksOpened = 100, PacksSinceEpic = 10, FirstLegendaryReceived = true },
            new FairPackCounters { PacksOpened = 10 },
            new FairPackCounters { PacksOpened = int.MaxValue, FirstLegendaryReceived = true } })
            Reject(() => FairPackRules.Generate("STANDARD", 1, 0, 0, bad, _ => 0));
        Reject(() => FairPackRules.Generate("STANDARD", 1, 0, 0, new(), _ => -1));
        // a998b480: copies above today's playset (a rarity correction) count as a full playset instead of soft-locking packs.
        Check(FairPackRules.Generate("STANDARD", 1, 0, 0, new(), _ => 3).CardIds.Count == 5, "Owned above playset must clamp, not throw");
        foreach (string type in FairPackRules.PackTypes)
        {
            for (ulong seed = 0; seed < 1000; seed++)
            {
                var before = new FairPackCounters { PacksOpened = 99, PacksSinceLegendary = 39, PacksSinceEpic = 9, FirstLegendaryReceived = true };
                var copy = before.Copy();
                var forced = FairPackRules.Generate(type, 1, seed, 99, before, Cap);
                Check(forced.CardIds.Any(id => Cards.FromId(id).Rarity == Rarity.LEGENDARY) && forced.CardIds.Any(id => Cards.FromId(id).Rarity == Rarity.EPIC), "Simultaneous pity preserves both rarities");
                Check(forced.After.PacksOpened == 100 && forced.After.PacksSinceLegendary == 0 && forced.After.PacksSinceEpic == 0, "Counter reset and increment");
                Check(before.PacksOpened == copy.PacksOpened && before.PacksSinceLegendary == 39 && before.PacksSinceEpic == 9 && !ReferenceEquals(forced.After, before), "Counters are immutable inputs");
                var first = new FairPackCounters();
                for (int i = 0; i < 10; i++) first = FairPackRules.Generate(type, 1, seed, i, first, _ => 0).After;
                Check(first.FirstLegendaryReceived, "First ten across seeds");
            }
        }
    }

    static void Golden()
    {
        // Pin both release metas; v1 receipts must retain the meta that selected their Standard pool.
        using var golden = JsonDocument.Parse(Resource("FairPackGoldenV1.json"));
        using var premiumGolden = JsonDocument.Parse(Resource("FairPackGoldenV2.json"));
        var states = new[] { new FairPackCounters(), new FairPackCounters { PacksOpened = 9, PacksSinceLegendary = 9, PacksSinceEpic = 9 },
            new FairPackCounters { PacksOpened = 100, PacksSinceLegendary = 12, PacksSinceEpic = 4, FirstLegendaryReceived = true } };
        for (int i = 0; i < FairPackRules.PackTypes.Count; i++)
        {
            string type = FairPackRules.PackTypes[i];
            string key = type == "STANDARD" ? type + ":" + string.Join(",", FairPackRules.PackTypes.Skip(1)) : type;
            var expected = golden.RootElement.GetProperty(key).EnumerateArray().Select(r => r.GetString()).ToArray();
            for (int scenario = 0; scenario < states.Length; scenario++)
            {
                long sequence = scenario == 2 ? long.MaxValue : 42;
                Func<string, int> acquired = scenario == 2 ? Cap : _ => 0;
                var draw = FairPackRules.Generate(type, 1, Seed, sequence, states[scenario], acquired);
                Check(Receipt(draw) == expected[scenario], "Golden receipt " + type);
                var premiumDraw = FairPackRules.Generate(type, 2, Seed, sequence, states[scenario], acquired);
                string qualityReceipt = string.Join(",", premiumDraw.CardIds.Select((id, slot) => id + (premiumDraw.Premiums[slot] == 0 ? "" : ":" + premiumDraw.Premiums[slot])))
                    + "|" + Receipt(premiumDraw).Split(':')[1];
                Check(qualityReceipt == premiumGolden.RootElement.GetProperty(key)[scenario].GetString(), "Rule 2 golden receipt " + type);
                Check(Receipt(draw) == Receipt(FairPackRules.Generate(type, 1, Seed, sequence, states[scenario], acquired)), "Determinism " + type);
            }
        }
    }

    static void Statistics(string type, int packs)
    {
        var pool = FairPackRules.Eligible(type, 1).ToDictionary(c => c.Id);
        var rarityPools = pool.Values.GroupBy(c => c.Rarity).ToDictionary(g => g.Key, g => g.Select(c => c.Id).ToArray());
        var counts = pool.Keys.ToDictionary(id => id, _ => 0);
        var remaining = rarityPools.ToDictionary(g => g.Key, g => g.Value.Sum(Cap));
        var counters = new FairPackCounters();
        int legendary = 0, epic = 0, longestL = 0, longestE = 0, goldenPacks = 0, signaturePacks = 0;
        var qualityCounts = new long[5, 4];
        var goldenByRarity = new int[4];
        int QualityBucket(string id) => pool[id].Rarity == Rarity.COMMON ? 0 : pool[id].Rarity == Rarity.RARE ? 1
            : pool[id].Rarity == Rarity.EPIC ? 2 : LocalCardPremium.HasPackSignature(id) ? 4 : 3;
        for (int i = 0; i < packs; i++)
        {
            var before = counters;
            var draw = FairPackRules.Generate(type, 2, Seed, i, before, id => counts[id]);
            var normal = FairPackRules.Generate(type, 1, Seed, i, before, id => counts[id]);
            Check(Receipt(draw) == Receipt(normal) && normal.Premiums.All(p => p == 0), "Rule 2 preserves rule 1 identity and counters");
            if (i < 100) Check(draw.Premiums.SequenceEqual(FairPackRules.Generate(type, 2, Seed, i, before, id => counts[id]).Premiums), "Deterministic quality replay");
            var exhausted = FairPackRules.Generate(type, 1, Seed, i, before, Cap);
            Check(draw.CardIds.Count == 5 && draw.CardIds.All(pool.ContainsKey), "Five eligible cards");
            Check(draw.CardIds.Select(id => pool[id].Rarity).SequenceEqual(exhausted.CardIds.Select(id => pool[id].Rarity)), "Protection must never alter rarity");
            Check(draw.After.PacksSinceLegendary == exhausted.After.PacksSinceLegendary && draw.After.PacksSinceEpic == exhausted.After.PacksSinceEpic, "Protection must never alter pity");
            foreach (var ids in new[] { draw.CardIds, exhausted.CardIds })
                Check(ids.GroupBy(id => id).All(g => g.Count() <= Cap(g.Key)), "Within-pack playset even when exhausted");
            Check(draw.CardIds.Any(id => pool[id].Rarity != Rarity.COMMON), "Rare or better guarantee");
            foreach (string id in draw.CardIds)
            {
                if (counts[id] >= Cap(id))
                    Check(remaining[pool[id].Rarity] == 0, "No extra until entire rarity acquired");
                else remaining[pool[id].Rarity]--;
                counts[id] = Math.Min(Cap(id), counts[id] + 1);
            }
            for (int slot = 0; slot < 5; slot++) {
                int premium = draw.Premiums[slot];
                string id = draw.CardIds[slot];
                Check(premium == 0 || premium == 1 || premium == 3, "No Diamond or unknown pack quality");
                Check(premium != 3 || pool[id].Rarity == Rarity.LEGENDARY && LocalCardPremium.HasPackSignature(id), "Only capable legendaries roll Signature");
                qualityCounts[QualityBucket(id), premium]++;
            }
            for (int rarity = 0; rarity < 4; rarity++)
                if (Enumerable.Range(0, 5).Any(slot => draw.Premiums[slot] == 1 && Math.Min(3, QualityBucket(draw.CardIds[slot])) == rarity)) goldenByRarity[rarity]++;
            if (draw.Premiums.Contains(1)) goldenPacks++;
            if (draw.Premiums.Contains(3)) signaturePacks++;
            bool hasL = draw.CardIds.Any(id => pool[id].Rarity == Rarity.LEGENDARY);
            bool hasE = draw.CardIds.Any(id => pool[id].Rarity == Rarity.EPIC);
            if (hasL) legendary++;
            if (hasE) epic++;
            counters = draw.After;
            Check(counters.PacksOpened == i + 1 && counters.PacksSinceLegendary == (hasL ? 0 : before.PacksSinceLegendary + 1)
                && counters.PacksSinceEpic == (hasE ? 0 : before.PacksSinceEpic + 1), "Exact counter transition");
            Check(counters.PacksSinceLegendary < 40 && counters.PacksSinceEpic < 10, "Hard pity limit");
            Check(i < 9 || counters.FirstLegendaryReceived, "First ten guarantee");
            longestL = Math.Max(longestL, counters.PacksSinceLegendary);
            longestE = Math.Max(longestE, counters.PacksSinceEpic);
        }
        double l = (double)legendary / packs, e = (double)epic / packs;
        // Six binomial standard errors, tightened with sample size; pity reduces variance further.
        Check(Math.Abs(l - .054738) < 6 * Math.Sqrt(.054738 * (1 - .054738) / packs), "Legendary calibration " + l);
        Check(Math.Abs(e - .217663) < 6 * Math.Sqrt(.217663 * (1 - .217663) / packs), "Epic calibration " + e);
        void Rate(long hits, long trials, double target, string name) {
            Check(trials > 0 && Math.Abs((double)hits / trials - target) < 6 * Math.Sqrt(target * (1 - target) / trials), "Premium rate " + type + "/" + name);
        }
        var targets = new[] { .015, .02, .05, .12, .10 };
        for (int bucket = 0; bucket < 5; bucket++) {
            long total = qualityCounts[bucket, 0] + qualityCounts[bucket, 1] + qualityCounts[bucket, 3];
            Rate(qualityCounts[bucket, 1], total, targets[bucket], "Golden bucket " + bucket);
            if (bucket == 4) Rate(qualityCounts[bucket, 3], total, 1.0 / 6, "Signature");
        }
        Console.WriteLine($"premium {type}: n={packs} Golden/card C={qualityCounts[0,1] / (double)(qualityCounts[0,0]+qualityCounts[0,1]):P5} R={qualityCounts[1,1] / (double)(qualityCounts[1,0]+qualityCounts[1,1]):P5} E={qualityCounts[2,1] / (double)(qualityCounts[2,0]+qualityCounts[2,1]):P5} L(no signature)={qualityCounts[3,1] / (double)(qualityCounts[3,0]+qualityCounts[3,1]):P5} L(signature capable)={qualityCounts[4,1] / (double)(qualityCounts[4,0]+qualityCounts[4,1]+qualityCounts[4,3]):P5}; per pack Golden C/R/E/L={string.Join("/",goldenByRarity.Select(n => ((double)n/packs).ToString("P5")))} any={goldenPacks/(double)packs:P5} Signature={signaturePacks/(double)packs:P5}");
        Console.WriteLine($"{type}: n={packs} L={l:P6} E={e:P6} max-drought={longestL}/{longestE}");
    }
}
