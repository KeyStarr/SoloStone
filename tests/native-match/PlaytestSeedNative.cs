using System;
using System.IO;
using System.Linq;
internal static partial class Program
{
    private static void PlaytestSeedNative()
    {
        var q = PlaytestQueue.Parse("npc=npc.golden.1\nseed=9");
        int seed = q.SelectSeed(4242);
        string root = Path.Combine(Path.GetTempPath(), "hs-seed-" + Guid.NewGuid());
        try {
            using var profile = new LocalProfile(Path.Combine(root, "profile.xml"));
            var first = new NativeMatch(new Capture().Send, "mage", "warrior", seed);
            var record = profile.BeginLaunch("mage", "warrior", seed, _ => first.Start());
            var second = new NativeMatch(new Capture().Send, "mage", "warrior", seed); second.Start();
            Check(record.Seed == 9 && profile.Pending.Seed == 9, "selected seed must persist in pending record");
            Check(first.Game.Player1.HandZone.Select(c => c.Card.Id).SequenceEqual(second.Game.Player1.HandZone.Select(c => c.Card.Id)),
                "selected seed must reproduce normal native opening");
            Console.WriteLine("PASS playtest seed native start and durable pending");
        } finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
