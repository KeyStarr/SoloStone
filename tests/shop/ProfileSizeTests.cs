using System.Text;
using System.Xml.Serialization;

internal static class ProfileSizeTests
{
    internal static void Run(string root)
    {
        const long limit = 32L * 1024 * 1024;
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        string seedPath = Path.Combine(root, "size-seed.xml");
        using (var p = new LocalProfile(seedPath)) p.SelectCardBack(0);
        LocalProfileData seed;
        using (var file = File.OpenRead(seedPath)) seed = (LocalProfileData)serializer.Deserialize(file)!;
        seed.Version = 9;
        string Xml() { using var stream = new MemoryStream(); serializer.Serialize(stream, seed); return Encoding.UTF8.GetString(stream.ToArray()); }
        string empty = Xml();
        void Fixture(string path, bool reward)
        {
            seed.Matches = new List<LocalMatchRecord> { new LocalMatchRecord {
                Id = Guid.Empty.ToString(), Deck = "mage", Opponent = "priest", StartedUtc = "2026-10-24T00:00:00Z",
                Result = "Tied", Turns = 10, RewardVersion = 2, Gold = 25, RewardFacts = new LocalRewardFacts()
            } };
            if (reward) {
                var match = seed.Matches[0]; match.Result = "Won"; match.Gold = 70; match.BonusGold = 20; match.PackReward = true;
                match.RewardFacts.DistinctCardsPlayed = 6; match.RewardFacts.EnemyMinionsDefeated = 5;
            }
            string xml = Xml();
            int start = xml.IndexOf("    <LocalMatchRecord>", StringComparison.Ordinal);
            int end = xml.IndexOf("  </Matches>", start, StringComparison.Ordinal);
            string row = xml.Substring(start, end - start);
            int count = (int)((limit - 65536 - Encoding.UTF8.GetByteCount(empty)) / Encoding.UTF8.GetByteCount(row));
            int split = empty.IndexOf("<Matches />", StringComparison.Ordinal);
            using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.Write(empty.Substring(0, split)); writer.Write("<Matches>\n");
            for (int i = 0; i < count; i++) writer.Write(row.Replace(Guid.Empty.ToString(), i.ToString("x8") + "-0000-0000-0000-000000000000"));
            writer.Write("  </Matches>"); writer.Write(empty.Substring(split + "<Matches />".Length));
        }
        string near = Path.Combine(root, "near-limit.xml"); Fixture(near, false);
        using (var p = new LocalProfile(near)) {
            if (p.History.Count < 10000 || new FileInfo(near).Length > limit || new FileInfo(near).Length < limit - 131072)
                throw new Exception("near-limit migration must fit its serialized growth budget");
        }
        using (var p = new LocalProfile(near)) {
            if (p.History.Count < 10000) throw new Exception("near-limit migrated profile must reopen");
        }
        string over = Path.Combine(root, "migration-growth.xml"); Fixture(over, true);
        byte[] original = File.ReadAllBytes(over);
        for (int attempt = 0; attempt < 2; attempt++) {
            bool rejected = false;
            try { using var p = new LocalProfile(over); }
            catch (InvalidOperationException error) when (error.Message == "Local profile exceeds the size limit.") { rejected = true; }
            if (!rejected || !File.ReadAllBytes(over).SequenceEqual(original)
                || !File.ReadAllBytes(over + ".v9.bak").SequenceEqual(original) || File.Exists(over + ".tmp"))
                throw new Exception("oversized migration must preserve V9 bytes and backup, including on reopen");
        }
        Console.WriteLine("PASS profile size: near-limit migration/reopen; oversized growth refuses replacement and preserves V9/backup");
    }
}
