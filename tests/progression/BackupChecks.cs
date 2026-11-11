using System.Reflection;
using System.Xml.Serialization;

internal static class BackupChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static string Reject(Action action)
    {
        try { action(); }
        catch (Exception error) when (error is InvalidOperationException || error is IOException || error is ArgumentException)
        { return error.Message; }
        throw new Exception("Expected restore refusal");
    }
    static string ProfilePath(string root, string name)
    {
        string directory = Path.Combine(root, "backup-" + name);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "profile.xml");
    }
    static string DirectoryFor(string path) => Path.Combine(Path.GetDirectoryName(path), "backups");
    static LocalProfileData Read(string path) => LocalProfile.ReadFile(path);
    static void Write(string path, LocalProfileData data)
    {
        using var stream = File.Create(path);
        new XmlSerializer(typeof(LocalProfileData)).Serialize(stream, data);
    }
    static void Seed(string path)
    {
        using (var profile = new LocalProfile(path)) profile.CreateDraft("Seed", "HERO_08");
        var saved = Read(path);
        saved.GoldBalance = 1000;
        saved.UnopenedPacks = new List<LocalPackCount> { new LocalPackCount { PackType = FairPackRules.Legacy, Count = 5 } };
        saved.PackSeed = 17;
        Write(path, saved);
    }
    static void Exact(string path, LocalProfileBackups.Entry entry, byte[] expected)
    {
        Check(File.ReadAllBytes(Path.Combine(DirectoryFor(path), entry.Id + ".xml")).SequenceEqual(expected), "Backup bytes changed");
        Check(entry.Sha256 == LocalProfileBackups.Hash(expected) && entry.Bytes == expected.LongLength, "Backup hash/size mismatch");
    }
    static void Committed(string path, string reason)
    {
        var entry = LocalProfileBackups.List(path)[0];
        Check(entry.Reason == reason && entry.ProfileVersion == 15, "Missing " + reason + " backup");
        Exact(path, entry, File.ReadAllBytes(path));
    }
    static void Restart(string path)
    {
        byte[] saved = File.ReadAllBytes(path);
        var before = Read(path);
        using (var profile = new LocalProfile(path))
            Check(profile.Gold == before.GoldBalance && profile.History.Count == before.Matches.Count
                && profile.UnopenedPacks == before.UnopenedPacks.Sum(pack => pack.Count) && profile.PackSequence == before.PackSequence,
                "Restart changed saved progress");
        Check(File.ReadAllBytes(path).SequenceEqual(saved), "Restart rewrote current profile");
        foreach (var entry in LocalProfileBackups.List(path))
            Exact(path, entry, File.ReadAllBytes(Path.Combine(DirectoryFor(path), entry.Id + ".xml")));
    }
    static Dictionary<string, byte[]> Files(string directory) => Directory.GetFiles(directory)
        .ToDictionary(Path.GetFileName, File.ReadAllBytes);
    static void Unchanged(string directory, Dictionary<string, byte[]> files)
    {
        foreach (var pair in files)
            Check(File.ReadAllBytes(Path.Combine(directory, pair.Key)).SequenceEqual(pair.Value), "Previous backup modified");
    }
    static void NoStaging(string path) => Check(Directory.GetDirectories(DirectoryFor(path), "restore-staging-*").Length == 0,
        "Restore staging directory leaked");

    internal static void Run(string root)
    {
        DateTime clock = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        LocalProfileBackups.UtcNow = () => clock = clock.AddSeconds(1);
        int passed = 0;
        void Test(string name, Action action)
        {
            action(); passed++;
            Console.WriteLine("PASS backups: " + name);
        }
        try
        {
            Test("current startup, missing save, dedupe, validation and snapshot suppression", () => Startup(root));
            Test("partial commit replacement preserves new bytes and unchanged in-memory data", () => CommitReplaceFault(root));
            Test("partial restore replacement preserves new bytes and original failure", () => RestoreReplaceFault(root));
            Test("settlement, abandon, purchase, both pack paths, no deck/ack backups, restarts", () => Transactions(root));
            foreach (string stage in new[] { "data-written", "data-renamed", "manifest-written" })
                Test("automatic crash at " + stage + " preserves commit/backups and cleans on retry", () => Fault(root, stage));
            Test("clock jumps and same timestamps follow sequence for rotation, dedupe and latest", () => Sequence(root));
            Test("unverifiable manifest sequences stay reserved after repair and rotation", () => UnverifiedSequence(root));
            Test("equal sequences order by descending ordinal ID for latest and rotation", () => SequenceTies(root));
            Test("daily retention uses UTC date and highest sequence despite clock jumps", () => DailySequence(root));
            Test("pre-restores never dedupe and protected oldest target survives rotation", () => Protected(root));
            Test("rotation exact union of newest ten, seven UTC days and three pre-restores", () => Rotation(root));
            Test("restore round-trip preserves B and reopens A", () => RoundTrip(root));
            foreach (string pending in new[] { "match", "pack", "reward" })
                Test("restore refuses pending " + pending, () => Pending(root, pending));
            Test("restore refuses live lease", () => Locked(root));
            Test("tampered, invalid XML/schema, unsupported version and unsafe IDs rejected", () => Invalid(root));
            Test("strict manifest fields, missing pairs and reparse files rejected", () => Manifests(root));
            foreach (string stage in new[] { "data-written", "data-renamed", "manifest-written" })
                Test("mandatory pre-restore failure at " + stage + " leaves live bytes", () => Mandatory(root, stage));
            Test("corrupt or absent live profile restores current V15 bytes", () => CorruptAndMissing(root));
            Test("delete failures log and do not block writes; unverifiable pairs stay untouched", () => CleanupFailures(root));
            Test("transiently unreadable pairs stay untouched and uncounted", () => Unreadable(root));
            Test("restore temp fault consumes request and leaves live save loadable", () => RestoreFault(root, false));
            Test("post-swap hash failure reports replaced profile and pre-restore ID", () => RestoreFault(root, true));
            Test("requests consume before restore and refuse stale hash, mtime or failed consumption", () => StaleRequests(root));
            Test("request explicit/latest success, refusal consumption, IO failure and no throws", () => Requests(root));
            Console.WriteLine("PASS backups total: " + passed + " checks");
        }
        finally
        {
            LocalProfileBackups.FaultHook = null;
            LocalProfile.CommitFaultHook = null;
            LocalProfileBackups.Log = null;
            LocalProfileBackups.UtcNow = () => DateTime.UtcNow;
        }
    }

    static void CommitReplaceFault(string root)
    {
        string path = ProfilePath(root, "commit-replace-fault"); Seed(path);
        using (var profile = new LocalProfile(path))
        {
            int drafts = profile.CustomDecks.Count;
            byte[] next = null;
            var fault = new IOException("partial commit replace");
            LocalProfile.CommitFaultHook = at =>
            {
                if (at != "replace") return;
                next = File.ReadAllBytes(path + ".tmp");
                File.Delete(path);
                throw fault;
            };
            try
            {
                try { profile.CreateDraft("Recovered", "HERO_08"); throw new Exception("Commit did not throw"); }
                catch (IOException error) { Check(ReferenceEquals(error, fault), "Commit lost original failure"); }
            }
            finally { LocalProfile.CommitFaultHook = null; }
            Check(next != null && File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(next), "Partial commit lost new bytes");
            Check(profile.CustomDecks.Count == drafts && Read(path).CustomDecks.Any(deck => deck.Name == "Recovered"),
                "Failed commit changed in-memory data or did not preserve next profile");
        }
        Restart(path);
        Check(!File.Exists(path + ".tmp"), "Commit temp leaked after recovery");
    }

    static void RestoreReplaceFault(string root)
    {
        string path = ProfilePath(root, "restore-replace-fault"); Seed(path);
        byte[] next = File.ReadAllBytes(path);
        string id = LocalProfileBackups.Create(path, next, "startup", 9);
        using (var profile = new LocalProfile(path)) profile.CreateDraft("Later", "HERO_08");
        var fault = new IOException("partial restore replace");
        LocalProfileBackups.FaultHook = at =>
        {
            if (at != "replace") return;
            File.Delete(path);
            throw fault;
        };
        try
        {
            try { LocalProfileBackups.Restore(path, id); throw new Exception("Restore did not throw"); }
            catch (InvalidOperationException error)
            { Check(error.Message.StartsWith("ERROR:") && ReferenceEquals(error.InnerException, fault), "Restore lost original failure"); }
        }
        finally { LocalProfileBackups.FaultHook = null; }
        Check(File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(next), "Partial restore lost new bytes");
        Restart(path);
        Check(!File.Exists(path + ".restore.tmp"), "Restore temp leaked after recovery");
    }

    static void UnverifiedSequence(string root)
    {
        string path = ProfilePath(root, "unverified-sequence"); Seed(path);
        string id = LocalProfileBackups.Create(path, File.ReadAllBytes(path), "startup", 9);
        string file = Path.Combine(DirectoryFor(path), id + ".xml"), manifest = Path.ChangeExtension(file, ".manifest");
        byte[] original = File.ReadAllBytes(file);
        long reserved = LocalProfileBackups.List(path)[0].Seq + 100;
        File.WriteAllText(manifest, File.ReadAllText(manifest).Replace("seq=" + LocalProfileBackups.List(path)[0].Seq + "\n", "seq=" + reserved + "\n"));
        File.WriteAllText(file, "tampered");
        var changed = Read(path); changed.GoldBalance++; Write(path, changed);
        byte[] latest = File.ReadAllBytes(path);
        string next = LocalProfileBackups.Create(path, latest, "match", 9);
        Check(LocalProfileBackups.List(path).First().Seq == reserved + 1, "Unverifiable highest sequence reused");
        File.WriteAllBytes(file, original);
        Check(LocalProfileBackups.List(path).Take(2).Select(entry => entry.Id).SequenceEqual(new[] { next, id }), "Repair changed latest ordering");
        File.WriteAllBytes(path, original);
        LocalProfileBackups.Restore(path, "latest");
        Check(File.ReadAllBytes(path).SequenceEqual(latest), "Latest selected repaired older pair");
        for (int i = 0; i < 8; i++) LocalProfileBackups.Create(path, new[] { (byte)i }, "match", 9);
        Check(LocalProfileBackups.List(path).Any(entry => entry.Id == next) && !LocalProfileBackups.List(path).Any(entry => entry.Id == id),
            "Rotation retained repaired older pair ahead of newer backup");
    }

    static void SequenceTies(string root)
    {
        string path = ProfilePath(root, "sequence-ties"); Seed(path);
        byte[] before = File.ReadAllBytes(path);
        var ids = new List<string>();
        for (int i = 0; i < 12; i++)
        {
            var data = Read(path); data.GoldBalance++; Write(path, data);
            string id = LocalProfileBackups.Create(path, File.ReadAllBytes(path), "match", 9);
            var entry = LocalProfileBackups.List(path).Single(e => e.Id == id);
            string manifest = Path.Combine(DirectoryFor(path), id + ".manifest");
            File.WriteAllText(manifest, File.ReadAllText(manifest).Replace("seq=" + entry.Seq + "\n", "seq=100\n"));
            ids.Add(id);
        }
        string[] expected = ids.OrderByDescending(id => id, StringComparer.Ordinal).Take(10).ToArray();
        Check(LocalProfileBackups.List(path).Select(entry => entry.Id).SequenceEqual(expected), "Sequence ties used filesystem order");
        byte[] latest = File.ReadAllBytes(Path.Combine(DirectoryFor(path), expected[0] + ".xml"));
        File.WriteAllBytes(path, before);
        LocalProfileBackups.Restore(path, "latest");
        Check(File.ReadAllBytes(path).SequenceEqual(latest), "Latest ignored descending ordinal ID tie-break");
    }

    static void Startup(string root)
    {
        string path = ProfilePath(root, "startup-current");
        using (var profile = new LocalProfile(path)) { }
        Check(!Directory.Exists(DirectoryFor(path)), "Missing profile created a backup");
        Seed(path);
        var saved = Read(path);
        saved.CoreScheduleVersion = 0;
        Write(path, saved);
        byte[] original = File.ReadAllBytes(path);
        using (var profile = new LocalProfile(path)) { }
        var entry = LocalProfileBackups.List(path).Single();
        Check(entry.ProfileVersion == 15 && entry.Reason == "startup", "Current startup metadata");
        Exact(path, entry, original);
        Check(Read(path).CoreScheduleVersion != 0, "Grant fixture did not trigger a write");
        using (var profile = new LocalProfile(path)) { }
        int count = LocalProfileBackups.List(path).Count;
        Restart(path); Restart(path);
        Check(LocalProfileBackups.List(path).Count == count, "Unchanged startup did not dedupe");
        string validation = ProfilePath(root, "validate");
        File.Copy(path, validation);
        LocalProfile.ValidateFile(validation);
        Check(Read(validation).Version == 15 && !Directory.Exists(DirectoryFor(validation)), "Validation changed current bytes or took backups");
        string snapshot = ProfilePath(root, "snapshot");
        File.Copy(path, snapshot);
        var ctor = typeof(LocalProfile).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null,
            new[] { typeof(string), typeof(bool) }, null);
        using (var profile = (LocalProfile)ctor.Invoke(new object[] { snapshot, true })) { }
        Check(!Directory.Exists(DirectoryFor(snapshot)), "Snapshot took a backup");
    }

    static void Transactions(string root)
    {
        string path = ProfilePath(root, "transactions"); Seed(path);
        using (var profile = new LocalProfile(path))
        {
            int count = LocalProfileBackups.List(path).Count;
            var match = profile.Begin("mage", "warrior", 1);
            Check(LocalProfileBackups.List(path).Count == count, "Begin backed up");
            profile.Settle(match.Id, "Won", 12, false);
            Committed(path, "match");
            count = LocalProfileBackups.List(path).Count;
            profile.Settle(match.Id, "Won", 12, false);
            profile.AcknowledgeRewardPresentation(match.Id);
            profile.AcknowledgeRewardPresentation(match.Id);
            var draft = profile.CreateDraft("No backup", "HERO_08");
            profile.SaveDraft(draft.Id, "Edited", "HERO_08", Array.Empty<string>(), 1);
            Check(LocalProfileBackups.List(path).Count == count, "Replay/deck/ack backed up");
        }
        Restart(path);
        using (var profile = new LocalProfile(path)) { profile.Begin("mage", "warrior", 2); }
        using (var profile = new LocalProfile(path))
        {
            profile.RecoverInterrupted(); Committed(path, "abandon");
            int count = LocalProfileBackups.List(path).Count;
            profile.RecoverInterrupted();
            Check(LocalProfileBackups.List(path).Count == count, "No-op recovery backed up");
        }
        Restart(path);
        using (var profile = new LocalProfile(path)) { profile.BuyPack(); Committed(path, "purchase"); }
        Restart(path);
        using (var profile = new LocalProfile(path)) { profile.OpenPack(); Committed(path, "pack"); }
        Restart(path);
        using (var profile = new LocalProfile(path))
        {
            profile.BeginNativePack(); Committed(path, "pack");
            int count = LocalProfileBackups.List(path).Count;
            profile.BeginNativePack();
            Check(LocalProfileBackups.List(path).Count == count, "Native pack retry backed up");
        }
        Restart(path);
        using (var profile = new LocalProfile(path))
        {
            int count = LocalProfileBackups.List(path).Count;
            profile.CompleteNativePackReveal(profile.PackSequence, profile.LastPack.Cards.Select(card => card.Key));
            Check(LocalProfileBackups.List(path).Count == count, "Native reveal acknowledgement backed up");
        }
        Restart(path);
    }

    static void Fault(string root, string stage)
    {
        string path = ProfilePath(root, "fault-" + stage); Seed(path);
        var logs = new List<string>();
        LocalProfileBackups.Log = logs.Add;
        Dictionary<string, byte[]> before;
        int count;
        byte[] committed;
        using (var profile = new LocalProfile(path))
        {
            count = LocalProfileBackups.List(path).Count;
            before = Files(DirectoryFor(path));
            var match = profile.Begin("mage", "warrior", 1);
            LocalProfileBackups.FaultHook = at => { if (at == stage) throw new IOException("simulated " + stage); };
            try { profile.Settle(match.Id, "Won", 12, false); }
            finally { LocalProfileBackups.FaultHook = null; }
            committed = File.ReadAllBytes(path);
            Check(profile.History.Count == 1 && Read(path).Matches.Count == 1 && Read(path).Pending == null,
                "Backup failure interrupted settlement");
            Check(LocalProfileBackups.LastError?.Message == "simulated " + stage, "Backup failure diagnostic missing");
            Check(LocalProfileBackups.List(path).Count == count, "Crash published a valid half-backup");
        }
        Check(logs.Any(message => message.Contains("simulated " + stage)), "Automatic failure not logged");
        LocalProfileBackups.Log = null;
        Unchanged(DirectoryFor(path), before);
        string orphan = Path.Combine(DirectoryFor(path), "20990101T000000000Z-startup-0123abcd.xml");
        File.WriteAllText(orphan, "orphan");
        File.WriteAllText(orphan + ".tmp", "<truncated");
        // Reopening is the retry: startup must load the durable save before backing it up.
        using (var reopened = new LocalProfile(path))
            Check(reopened.History.Count == 1, "Crash left an unloadable live save");
        Check(LocalProfileBackups.LastError == null && File.ReadAllBytes(path).SequenceEqual(committed), "Retry changed live bytes");
        Unchanged(DirectoryFor(path), before);
        Committed(path, "startup");
        Check(Directory.GetFiles(DirectoryFor(path), "*.tmp").Length == 0, "Stale temporary leaked");
        Check(Directory.GetFiles(DirectoryFor(path), "*.xml").Length == LocalProfileBackups.List(path).Count, "Orphan data leaked");
        Restart(path);
    }

    static void Sequence(string root)
    {
        string path = ProfilePath(root, "sequence"); Seed(path);
        var clock = LocalProfileBackups.UtcNow;
        var ids = new List<string>();
        var date = new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);
        try
        {
            for (int i = 0; i < 24; i++)
            {
                // Backwards, forwards, then identical timestamps, all on one UTC day.
                DateTime now = date.AddHours(i < 12 ? (i % 2 == 0 ? 20 - i : i) : 12);
                LocalProfileBackups.UtcNow = () => now;
                var data = Read(path); data.GoldBalance = 1000 + i; Write(path, data);
                ids.Add(LocalProfileBackups.Create(path, File.ReadAllBytes(path), "match", 9));
            }
            Check(LocalProfileBackups.List(path).Select(e => e.Seq).SequenceEqual(Enumerable.Range(15, 10).Reverse().Select(i => (long)i))
                && LocalProfileBackups.List(path).Select(e => e.Id).SequenceEqual(ids.Skip(14).Reverse()),
                "Clock jumps or equal timestamps changed newest-ten order");
            Check(LocalProfileBackups.Create(path, File.ReadAllBytes(path), "startup", 9) == ids[23], "Dedupe ignored sequence");
            string request = Path.Combine(Path.GetDirectoryName(path), "restore-request.txt");
            File.WriteAllText(request, "latest");
            LocalProfileBackups.ProcessRestoreRequest(path);
            Check(Read(path).GoldBalance == 1022, "Latest ignored sequence");
        }
        finally { LocalProfileBackups.UtcNow = clock; }
    }

    static void DailySequence(string root)
    {
        string path = ProfilePath(root, "daily-sequence");
        var clock = LocalProfileBackups.UtcNow;
        var days = new List<string>();
        var date = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        try
        {
            for (int i = 0; i < 8; i++)
            {
                LocalProfileBackups.UtcNow = () => date.AddDays(i).AddHours(20);
                days.Add(LocalProfileBackups.Create(path, new[] { (byte)i }, "match", 9));
            }
            LocalProfileBackups.UtcNow = () => date.AddDays(7).AddHours(1);
            string daily = LocalProfileBackups.Create(path, new byte[] { 8 }, "match", 9);
            var recent = new List<string>();
            for (int i = 9; i < 21; i++)
            {
                LocalProfileBackups.UtcNow = () => date.AddMinutes(i);
                recent.Add(LocalProfileBackups.Create(path, new[] { (byte)i }, "match", 9));
            }
            var expected = days.Skip(1).Take(6).Concat(new[] { daily }).Concat(recent.Skip(2)).ToHashSet();
            Check(expected.SetEquals(LocalProfileBackups.List(path).Select(e => e.Id)), "Daily retention used timestamp order or wrong UTC dates");
        }
        finally { LocalProfileBackups.UtcNow = clock; }
    }

    static void Protected(string root)
    {
        string path = ProfilePath(root, "protected"); Seed(path);
        byte[] original = File.ReadAllBytes(path);
        string target = LocalProfileBackups.Create(path, original, "startup", 9);
        for (int i = 1; i < 10; i++)
        {
            var data = Read(path); data.GoldBalance += 1; Write(path, data);
            LocalProfileBackups.Create(path, File.ReadAllBytes(path), "match", 9);
        }
        LocalProfileBackups.Restore(path, target);
        Check(File.ReadAllBytes(path).SequenceEqual(original) && LocalProfileBackups.List(path).Any(e => e.Id == target),
            "Pre-restore rotated away its restore target");
        byte[] live = File.ReadAllBytes(path);
        LocalProfileBackups.Create(path, live, "match", 9);
        string first = LocalProfileBackups.Create(path, live, "pre-restore", 9);
        string second = LocalProfileBackups.Create(path, live, "pre-restore", 9);
        Check(first != second && LocalProfileBackups.List(path).First().Reason == "pre-restore", "Pre-restore deduped");
        for (int i = 0; i < 15; i++) LocalProfileBackups.Create(path, new[] { (byte)i }, "match", 9);
        Check(LocalProfileBackups.List(path).Any(e => e.Id == first) && LocalProfileBackups.List(path).Any(e => e.Id == second),
            "Newest pre-restores did not survive later automatic backups");
    }

    static void Rotation(string root)
    {
        string path = ProfilePath(root, "rotation");
        var clock = LocalProfileBackups.UtcNow;
        var all = new List<(string Id, DateTime Time, string Reason)>();
        DateTime date = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        Directory.CreateDirectory(DirectoryFor(path));
        string invalid = Path.Combine(DirectoryFor(path), "20200101T000000000Z-startup-0123abcd");
        File.WriteAllText(invalid + ".xml", "invalid pair");
        File.WriteAllText(invalid + ".manifest", "format=2");
        try
        {
            // More than seven days, then a dense recent day; old pre-restores survive beyond daily retention.
            for (int i = 0; i < 45; i++)
            {
                DateTime now = i < 15 ? date.AddDays(i) : date.AddDays(15).AddMinutes(i);
                string reason = i < 5 ? "pre-restore" : "match";
                LocalProfileBackups.UtcNow = () => now;
                string id = LocalProfileBackups.Create(path, new[] { (byte)i }, reason, 9);
                all.Add((id, now, reason));
            }
            var newest = all.OrderByDescending(item => item.Time).ToArray();
            var expected = newest.Take(10).Select(item => item.Id)
                .Concat(newest.GroupBy(item => item.Time.Date).Take(7).Select(group => group.First().Id))
                .Concat(newest.Where(item => item.Reason == "pre-restore").Take(3).Select(item => item.Id)).ToHashSet();
            var actual = LocalProfileBackups.List(path);
            Check(expected.SetEquals(actual.Select(item => item.Id)) && actual.Count == expected.Count, "Rotation keep union incorrect");
            Check(Directory.GetFiles(DirectoryFor(path), "*.xml").Length == expected.Count + 1
                && Directory.GetFiles(DirectoryFor(path), "*.manifest").Length == expected.Count + 1, "Rotation left unwanted pairs");
            Check(actual.Any(entry => entry.Id == all.Last().Id), "Rotation deleted just-written backup");
            Check(File.ReadAllText(invalid + ".xml") == "invalid pair" && File.ReadAllText(invalid + ".manifest") == "format=2", "Rotation deleted invalid pair");
            string orphan = Path.Combine(DirectoryFor(path), "20990101T000000000Z-startup-0123abcd.xml");
            File.WriteAllText(orphan, "orphan");
            File.WriteAllText(orphan + ".tmp", "stale");
            LocalProfileBackups.Create(path, new byte[] { 44 }, "startup", 9);
            Check(!File.Exists(orphan) && !File.Exists(orphan + ".tmp") && LocalProfileBackups.List(path).Count == actual.Count,
                "Dedupe skipped stale cleanup or wrote extra copy");
        }
        finally { LocalProfileBackups.UtcNow = clock; }
    }

    static void RoundTrip(string root)
    {
        string path = ProfilePath(root, "roundtrip"); Seed(path);
        byte[] a = File.ReadAllBytes(path);
        using (var profile = new LocalProfile(path))
        {
            var match = profile.Begin("mage", "warrior", 1);
            profile.Settle(match.Id, "Won", 12, false);
            profile.AcknowledgeRewardPresentation(match.Id);
        }
        var target = LocalProfileBackups.List(path).Single(entry => entry.Sha256 == LocalProfileBackups.Hash(a));
        byte[] b = File.ReadAllBytes(path);
        string summary = LocalProfileBackups.Restore(path, target.Id);
        Check(summary.Contains(target.Id) && summary.Contains("pre-restore"), "Restore summary missing IDs");
        Check(File.ReadAllBytes(path).SequenceEqual(a), "Restore did not preserve original bytes");
        Exact(path, LocalProfileBackups.List(path).First(entry => entry.Reason == "pre-restore"), b);
        using (var profile = new LocalProfile(path)) Check(profile.Gold == 1000 && profile.History.Count == 0, "Restore did not recover A");
        Check(!File.Exists(path + ".restore.tmp"), "Restore temporary leaked"); NoStaging(path);
    }

    static void Pending(string root, string kind)
    {
        string path = ProfilePath(root, "pending-" + kind); Seed(path);
        string target;
        using (var profile = new LocalProfile(path))
        {
            target = LocalProfileBackups.List(path).Single().Id;
            if (kind == "pack") profile.BeginNativePack();
            else
            {
                var match = profile.Begin("mage", "warrior", 1);
                if (kind == "reward") profile.Settle(match.Id, "Won", 12, false);
            }
        }
        byte[] before = File.ReadAllBytes(path);
        Check(Reject(() => LocalProfileBackups.Restore(path, target)).ToLowerInvariant().Contains("pending"), "Pending refusal not clear");
        Check(File.ReadAllBytes(path).SequenceEqual(before) && !LocalProfileBackups.List(path).Any(entry => entry.Reason == "pre-restore"),
            "Pending refusal changed save or made pre-restore backup");
    }

    static void Locked(string root)
    {
        string path = ProfilePath(root, "locked"); Seed(path);
        using var profile = new LocalProfile(path);
        byte[] before = File.ReadAllBytes(path);
        Check(Reject(() => LocalProfileBackups.Restore(path, LocalProfileBackups.List(path).Single().Id)).Contains("Close the game"),
            "Lock refusal not clear");
        Check(File.ReadAllBytes(path).SequenceEqual(before), "Lock refusal changed save");
    }

    static void Invalid(string root)
    {
        string path = ProfilePath(root, "invalid"); Seed(path);
        byte[] live = File.ReadAllBytes(path);
        string id = LocalProfileBackups.Create(path, live, "startup", 9);
        string data = Path.Combine(DirectoryFor(path), id + ".xml");
        File.AppendAllText(data, "tamper");
        Reject(() => LocalProfileBackups.Restore(path, id));
        Check(!LocalProfileBackups.List(path).Any(), "Corrupt backup listed as valid");
        File.WriteAllBytes(data, live);
        foreach (string bad in new[] { "../profile", id + "\n", id.Replace("202610", "202613"), "", null })
            Reject(() => LocalProfileBackups.Restore(path, bad));
        foreach (string bad in new[] { "<broken", "<!DOCTYPE x [<!ENTITY evil 'x'>]><LocalProfileData/>",
            System.Text.Encoding.UTF8.GetString(live).Replace("<GoldBalance>1000</GoldBalance>", "<GoldBalance>-1</GoldBalance>"),
            System.Text.Encoding.UTF8.GetString(live).Replace("<Version>15</Version>", "<Version>999</Version>") })
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(bad);
            string invalid = LocalProfileBackups.Create(path, bytes, "match", 9);
            Check(LocalProfileBackups.List(path).Any(entry => entry.Id == invalid), "Loader test requires integrity-valid backup");
            Reject(() => LocalProfileBackups.Restore(path, invalid));
            Check(File.ReadAllBytes(path).SequenceEqual(live), "Invalid backup changed live save");
            NoStaging(path);
        }
        Check(!LocalProfileBackups.List(path).Any(entry => entry.Reason == "pre-restore"), "Validation refusal backed up live");
    }

    static void Manifests(string root)
    {
        string path = ProfilePath(root, "manifests"); Seed(path);
        byte[] live = File.ReadAllBytes(path);
        string id = LocalProfileBackups.Create(path, live, "startup", 9);
        string manifest = Path.Combine(DirectoryFor(path), id + ".manifest"), file = Path.Combine(DirectoryFor(path), id + ".xml");
        string original = File.ReadAllText(manifest);
        foreach (string bad in new[] { original + "format=1\n", original.Replace("format=1", "format=2"),
            original.Replace("reason=startup", "reason=pack"), original.Replace("profile_version=9", "profile_version=-1"),
            original.Replace("seq=1", "seq=0"), original.Replace("seq=1", "seq=9223372036854775808"),
            original.Replace("bytes=" + live.Length, "bytes=1"), original.Replace("sha256=", "sha256=FF"),
            original.Replace("created_utc=2026", "created_utc=2025"), original.Replace("id=" + id, "id=../profile"),
            original + "extra=value\n", "", new string('x', 4097) })
        {
            File.WriteAllText(manifest, bad);
            Check(LocalProfileBackups.List(path).Count == 0, "Malformed manifest listed as valid");
            Reject(() => LocalProfileBackups.Restore(path, id));
            Check(File.ReadAllBytes(path).SequenceEqual(live), "Malformed manifest changed save");
        }
        File.WriteAllText(manifest, original);
        File.Delete(file);
        Check(LocalProfileBackups.List(path).Count == 0, "Missing data listed as valid");
        Reject(() => LocalProfileBackups.Restore(path, id));
        File.WriteAllBytes(file, live);
        File.Delete(manifest);
        Check(LocalProfileBackups.List(path).Count == 0, "Orphan data listed as valid");
        Reject(() => LocalProfileBackups.Restore(path, id));
        File.WriteAllText(manifest, original);
        if (!OperatingSystem.IsWindows())
        {
            string sentinel = Path.Combine(Path.GetDirectoryName(path), "sentinel.xml");
            File.WriteAllBytes(sentinel, live);
            File.Delete(file); File.CreateSymbolicLink(file, sentinel);
            Check(LocalProfileBackups.List(path).Count == 0, "Reparse data listed as valid");
            Reject(() => LocalProfileBackups.Restore(path, id));
            Check(File.ReadAllBytes(sentinel).SequenceEqual(live), "Reparse target changed");
            File.Delete(file); File.WriteAllBytes(file, live);
            File.Delete(manifest); File.CreateSymbolicLink(manifest, sentinel);
            Reject(() => LocalProfileBackups.Restore(path, id));
            File.Delete(manifest);
        }
    }

    static void Mandatory(string root, string stage)
    {
        string path = ProfilePath(root, "mandatory-" + stage); Seed(path);
        string id;
        using (var profile = new LocalProfile(path))
        {
            id = LocalProfileBackups.List(path).Single().Id;
            profile.CreateDraft("B", "HERO_08");
        }
        byte[] before = File.ReadAllBytes(path);
        LocalProfileBackups.FaultHook = at => { if (at == stage) throw new IOException("mandatory failure"); };
        try { Reject(() => LocalProfileBackups.Restore(path, id)); }
        finally { LocalProfileBackups.FaultHook = null; }
        Check(File.ReadAllBytes(path).SequenceEqual(before), "Mandatory backup failure changed live save"); NoStaging(path);
        Restart(path);
    }

    static void CorruptAndMissing(string root)
    {
        string path = ProfilePath(root, "corrupt"); Seed(path);
        byte[] good = File.ReadAllBytes(path);
        string id = LocalProfileBackups.Create(path, good, "startup", 9);
        byte[] corrupt = System.Text.Encoding.UTF8.GetBytes("<broken save");
        File.WriteAllBytes(path, corrupt);
        LocalProfileBackups.Restore(path, id);
        Check(File.ReadAllBytes(path).SequenceEqual(good), "Corrupt live restore failed");
        Exact(path, LocalProfileBackups.List(path).Single(entry => entry.Reason == "pre-restore"), corrupt);
        Restart(path);
        File.Delete(path);
        LocalProfileBackups.Restore(path, id);
        Check(File.ReadAllBytes(path).SequenceEqual(good), "Missing live restore failed");
        using (var profile = new LocalProfile(path)) Check(profile.History.Count == 0, "Restored current profile cannot load");
        NoStaging(path);
    }

    static void CleanupFailures(string root)
    {
        string path = ProfilePath(root, "cleanup"); Seed(path);
        string corrupt = LocalProfileBackups.Create(path, new byte[] { 100 }, "startup", 9);
        string corruptFile = Path.Combine(DirectoryFor(path), corrupt + ".xml");
        File.WriteAllText(corruptFile, "tampered");
        var prior = Files(DirectoryFor(path));
        string retained = LocalProfileBackups.Create(path, new byte[] { 99 }, "purchase", 9);
        string retainedFile = Path.Combine(DirectoryFor(path), retained + ".xml");
        string blocked = Path.Combine(DirectoryFor(path), "leftover.xml.tmp");
        string removable = Path.Combine(DirectoryFor(path), "removable.xml.tmp");
        File.WriteAllText(blocked, "<truncated"); File.WriteAllText(removable, "<truncated");
        string staging = Path.Combine(DirectoryFor(path), "restore-staging-leftover");
        Directory.CreateDirectory(staging); File.WriteAllText(Path.Combine(staging, "profile.xml"), "<truncated");
        var logs = new List<string>(); LocalProfileBackups.Log = logs.Add;
        LocalProfileBackups.FaultHook = at => { if (at == "delete:" + blocked || at == "delete:" + retainedFile) throw new IOException("undeletable leftover"); };
        try
        {
            for (int i = 0; i < 15; i++) LocalProfileBackups.Create(path, new[] { (byte)i }, "match", 9);
            Check(LocalProfileBackups.List(path).Count == 11 && File.Exists(blocked)
                && LocalProfileBackups.List(path).Any(e => e.Id == retained), "Delete failure blocked backup or lost retained pair");
            Check(logs.Any(message => message.Contains(retainedFile)), "Rotation delete failure not logged");
            Check(logs.Any(message => message.Contains(blocked) && message.Contains("undeletable leftover")), "Delete failure not logged");
            Check(!File.Exists(removable) && !Directory.Exists(staging), "One failed delete blocked other cleanup");
            Unchanged(DirectoryFor(path), prior);
        }
        finally { LocalProfileBackups.FaultHook = null; LocalProfileBackups.Log = null; }
        LocalProfileBackups.Create(path, new byte[] { 16 }, "match", 9);
        Check(!File.Exists(blocked) && !File.Exists(retainedFile) && LocalProfileBackups.List(path).Count == 10, "Cleanup did not retry leftover");
    }

    static void Unreadable(string root)
    {
        string path = ProfilePath(root, "unreadable");
        string id = LocalProfileBackups.Create(path, new byte[] { 100 }, "startup", 9);
        var before = Files(DirectoryFor(path));
        using (var locked = new FileStream(Path.Combine(DirectoryFor(path), id + ".manifest"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            for (int i = 0; i < 15; i++) LocalProfileBackups.Create(path, new[] { (byte)i }, "match", 9);
            Check(LocalProfileBackups.List(path).Count == 10 && !LocalProfileBackups.List(path).Any(e => e.Id == id), "Unreadable pair counted as verified");
        }
        Unchanged(DirectoryFor(path), before);
        Check(LocalProfileBackups.List(path).Any(e => e.Id == id), "Rotation deleted transiently unreadable pair");
    }

    static void RestoreFault(string root, bool afterSwap)
    {
        string path = ProfilePath(root, "restore-fault-" + afterSwap); Seed(path);
        byte[] targetBytes = File.ReadAllBytes(path);
        string id = LocalProfileBackups.Create(path, targetBytes, "startup", 9);
        using (var profile = new LocalProfile(path)) profile.CreateDraft("Later", "HERO_08");
        byte[] before = File.ReadAllBytes(path);
        string request = Path.Combine(Path.GetDirectoryName(path), "restore-request.txt");
        File.WriteAllText(request, id);
        var logs = new List<string>(); LocalProfileBackups.Log = logs.Add;
        bool reached = false;
        LocalProfileBackups.FaultHook = at =>
        {
            if (at != (afterSwap ? "restore-replaced" : "restore-temp-written")) return;
            reached = true;
            Check(!File.Exists(request), "Restore request was not consumed before restore");
            if (afterSwap) File.WriteAllBytes(path, before); // Valid changed bytes trigger post-swap hash verification failure.
            else throw new IOException("restore temp fault");
        };
        try { LocalProfileBackups.ProcessRestoreRequest(path); }
        finally { LocalProfileBackups.FaultHook = null; LocalProfileBackups.Log = null; }
        string outcome = File.ReadAllText(Path.Combine(Path.GetDirectoryName(path), "restore-result.txt"));
        var pre = LocalProfileBackups.List(path).First(e => e.Reason == "pre-restore");
        Exact(path, pre, before);
        Check(reached && File.ReadAllBytes(path).SequenceEqual(before), "Restore fault changed pre-swap bytes or stage was missed");
        Check(outcome.StartsWith(afterSwap ? "ERROR:" : "REFUSED:"), "Restore fault misreported outcome");
        if (afterSwap) Check(outcome.Contains("WAS replaced") && outcome.Contains(pre.Id), "Post-swap error lost recovery ID");
        Check(logs.Any(message => message.StartsWith(afterSwap ? "ERROR:" : "REFUSED:")), "Restore outcome not logged");
        Check(LocalProfileBackups.LastError != null, "Restore failure diagnostic missing");
        LocalProfileBackups.ProcessRestoreRequest(path);
        Check(!File.Exists(request) && File.ReadAllText(Path.Combine(Path.GetDirectoryName(path), "restore-result.txt")) == outcome
            && File.ReadAllBytes(path).SequenceEqual(before), "Next launch replayed consumed restore request");
        Restart(path); NoStaging(path);
        Check(!File.Exists(path + ".restore.tmp"), "Restore temporary leaked after fault");
    }

    static void StaleRequests(string root)
    {
        string path = ProfilePath(root, "stale"); Seed(path);
        string id = LocalProfileBackups.Create(path, File.ReadAllBytes(path), "startup", 9);
        using (var profile = new LocalProfile(path)) profile.CreateDraft("Later", "HERO_08");
        byte[] before = File.ReadAllBytes(path);
        string request = Path.Combine(Path.GetDirectoryName(path), "restore-request.txt"),
            result = Path.Combine(Path.GetDirectoryName(path), "restore-result.txt");
        void Refused(string expected)
        {
            LocalProfileBackups.ProcessRestoreRequest(path);
            Check(File.ReadAllText(result).StartsWith("REFUSED:") && File.ReadAllText(result).Contains(expected), "Stale refusal missing: " + expected);
            Check(File.ReadAllBytes(path).SequenceEqual(before), "Stale request changed live bytes");
        }
        File.WriteAllText(request, id + "\nexpected_sha256=" + new string('0', 64));
        Refused("SHA-256"); Check(!File.Exists(request), "Stale hash request not consumed");
        File.WriteAllText(request, id);
        File.SetLastWriteTimeUtc(request, File.GetLastWriteTimeUtc(path).AddSeconds(-5));
        Refused("changed after"); Check(!File.Exists(request), "Stale mtime request not consumed");
        File.WriteAllText(request, id);
        LocalProfileBackups.FaultHook = at => { if (at == "delete:" + request) throw new IOException("consume blocked"); };
        try { Refused("could not consume request"); }
        finally { LocalProfileBackups.FaultHook = null; }
        Check(File.Exists(request) && !LocalProfileBackups.List(path).Any(e => e.Reason == "pre-restore"), "Unconsumed request restored profile");
        File.Delete(path);
        File.WriteAllText(request, id + "\nexpected_sha256=" + LocalProfileBackups.Hash(before));
        LocalProfileBackups.ProcessRestoreRequest(path);
        Check(!File.Exists(path) && !File.Exists(request) && File.ReadAllText(result).StartsWith("REFUSED:"), "Expected hash allowed missing live save");
        File.WriteAllBytes(path, before);
        File.WriteAllText(request, "\n" + id + "\n\nexpected_sha256=" + LocalProfileBackups.Hash(before).ToUpperInvariant() + "\n");
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(request).AddSeconds(5));
        LocalProfileBackups.ProcessRestoreRequest(path);
        Check(File.ReadAllText(result).StartsWith("OK ") && !File.Exists(request)
            && LocalProfileBackups.Hash(File.ReadAllBytes(path)) == LocalProfileBackups.List(path).Single(e => e.Id == id).Sha256,
            "Matching expected hash refused despite clock skew");
    }

    static void Sidecars(string root)
    {
        string path = ProfilePath(root, "sidecars"); Seed(path);
        byte[] before = File.ReadAllBytes(path);
        var data = Read(path); data.Version = 8; Write(path, data);
        byte[] legacy = File.ReadAllBytes(path); File.WriteAllBytes(path, before);
        string id = LocalProfileBackups.Create(path, legacy, "startup", 8);
        string sidecar = path + ".v8.bak";
        foreach (string blocked in new[] { sidecar, sidecar + ".tmp" })
        {
            Directory.CreateDirectory(blocked);
            string refusal = Reject(() => LocalProfileBackups.Restore(path, id));
            Check(refusal.StartsWith("REFUSED:") && File.ReadAllBytes(path).SequenceEqual(before) && Directory.Exists(blocked),
                "Migration sidecar directory changed live files or was accepted");
            Check(!LocalProfileBackups.List(path).Any(e => e.Reason == "pre-restore"), "Sidecar directory refusal took pre-restore backup");
            Directory.Delete(blocked);
        }
        File.WriteAllText(sidecar, "different legacy bytes");
        string error = Reject(() => LocalProfileBackups.Restore(path, id));
        Check(error.StartsWith("REFUSED:") && error.Contains("Existing V8 backup"), "Legacy conflict not detected in staging");
        Check(File.ReadAllBytes(path).SequenceEqual(before) && File.ReadAllText(sidecar) == "different legacy bytes", "Legacy conflict changed live files");
        Check(!LocalProfileBackups.List(path).Any(e => e.Reason == "pre-restore"), "Legacy refusal took pre-restore backup");
        if (!OperatingSystem.IsWindows())
        {
            File.Delete(sidecar);
            foreach (string blocked in new[] { sidecar, sidecar + ".tmp" })
            {
                File.CreateSymbolicLink(blocked, path);
                Check(Reject(() => LocalProfileBackups.Restore(path, id)).Contains("Reparse"), "Migration sidecar symlink accepted");
                Check(File.ReadAllBytes(path).SequenceEqual(before), "Sidecar symlink refusal changed live file");
                File.Delete(blocked);
            }
        }
        File.WriteAllBytes(sidecar, legacy);
        LocalProfileBackups.Restore(path, id);
        Check(File.ReadAllBytes(path).SequenceEqual(legacy), "Matching legacy sidecar did not restore original bytes");
        using (var profile = new LocalProfile(path)) Check(Read(path).Version == 12, "Matching legacy sidecar did not reopen/migrate");
        Check(File.ReadAllBytes(sidecar).SequenceEqual(legacy), "Matching legacy backup changed"); NoStaging(path);
    }

    static void Requests(string root)
    {
        string path = ProfilePath(root, "requests"); Seed(path);
        string directory = Path.GetDirectoryName(path), request = Path.Combine(directory, "restore-request.txt"),
            result = Path.Combine(directory, "restore-result.txt");
        byte[] a = File.ReadAllBytes(path);
        string id = LocalProfileBackups.Create(path, a, "startup", 9);
        using (var profile = new LocalProfile(path)) profile.CreateDraft("B", "HERO_08");
        byte[] b = File.ReadAllBytes(path);
        LocalProfileBackups.Create(path, b, "match", 9);
        File.WriteAllText(request, "\nlatest\n");
        LocalProfileBackups.ProcessRestoreRequest(path);
        Check(File.ReadAllBytes(path).SequenceEqual(a) && !File.Exists(request) && File.ReadAllText(result).StartsWith("OK "), "Latest request failed");
        File.WriteAllBytes(path, b);
        File.WriteAllText(request, id + "\n");
        LocalProfileBackups.ProcessRestoreRequest(path);
        Check(File.ReadAllBytes(path).SequenceEqual(a) && !File.Exists(request) && File.ReadAllText(result).StartsWith("OK "), "Explicit request failed");
        foreach (string bad in new[] { "../bad", "", "no such backup" })
        {
            File.WriteAllText(request, bad);
            LocalProfileBackups.ProcessRestoreRequest(path);
            Check(!File.Exists(request) && File.ReadAllText(result).StartsWith("REFUSED: ") && File.ReadAllBytes(path).SequenceEqual(a),
                "Refused request not consumed safely");
        }
        using (var profile = new LocalProfile(path))
        {
            File.WriteAllText(request, id);
            LocalProfileBackups.ProcessRestoreRequest(path);
            Check(!File.Exists(request) && File.ReadAllText(result).Contains("Close the game"), "Locked request did not refuse");
        }
        using (var profile = new LocalProfile(path)) profile.Begin("mage", "warrior", 1);
        byte[] pending = File.ReadAllBytes(path);
        File.WriteAllText(request, id);
        LocalProfileBackups.ProcessRestoreRequest(path);
        Check(!File.Exists(request) && File.ReadAllText(result).Contains("pending") && File.ReadAllBytes(path).SequenceEqual(pending),
            "Pending request changed save");
        File.Delete(result); Directory.CreateDirectory(result);
        File.WriteAllText(request, "bad");
        LocalProfileBackups.ProcessRestoreRequest(path);
        Check(!File.Exists(request) && LocalProfileBackups.LastError != null, "Result IO failure threw or retried request");
        Directory.Delete(result);
        LocalProfileBackups.ProcessRestoreRequest(path);
        string unchanged = ProfilePath(root, "request-no-different"); Seed(unchanged);
        LocalProfileBackups.Create(unchanged, File.ReadAllBytes(unchanged), "startup", 9);
        string unchangedRequest = Path.Combine(Path.GetDirectoryName(unchanged), "restore-request.txt");
        File.WriteAllText(unchangedRequest, "latest");
        LocalProfileBackups.ProcessRestoreRequest(unchanged);
        Check(!File.Exists(unchangedRequest) && File.ReadAllText(Path.Combine(Path.GetDirectoryName(unchanged), "restore-result.txt"))
            .Contains("No different valid backup"), "Latest request without a different backup did not refuse");
        LocalProfileBackups.ProcessRestoreRequest(null);
    }
}
