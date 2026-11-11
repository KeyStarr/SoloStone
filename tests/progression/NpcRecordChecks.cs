using System.Text;
using System.Xml.Serialization;

// NPC world v1 persistence (spec WP5): optional NpcId/NpcDeckId/NpcRating/NpcConceded on match records, no schema bump.
internal static class NpcRecordChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Reject(Action action, string message)
    {
        try { action(); } catch (Exception e) when (e is InvalidOperationException || e is ArgumentException) { return; }
        throw new Exception("Expected rejection: " + message);
    }
    static LocalProfileData Read(string path)
    {
        using var stream = File.OpenRead(path);
        return (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(stream);
    }
    static void Write(string path, LocalProfileData data)
    {
        using var stream = File.Create(path);
        new XmlSerializer(typeof(LocalProfileData)).Serialize(stream, data);
    }
    static string Text(string path) => Encoding.UTF8.GetString(File.ReadAllBytes(path));
    static bool NoNpc(LocalMatchRecord m) => m.NpcId == null && m.NpcDeckId == null && m.NpcRating == null && m.NpcConceded == null;

    internal static void Run(string root)
    {
        LegacyShape(root);
        RoundTrip(root);
        AbandonUsesStoredRating(root);
        Tamper(root);
        Console.WriteLine("PASS NPC records: legacy shape and byte-stable re-save, round-trip, restart mid-pending, "
            + "stored-rating settle and retries, NPC concession, abandon, tamper rejection");
    }

    // A V10 save without the fields loads with nulls and re-saves without any Npc element (byte-stable).
    static void LegacyShape(string root)
    {
        string path = Path.Combine(root, "npc-legacy.xml");
        using (var p = new LocalProfile(path)) {
            var m = p.Begin("mage", "priest", 1);
            var settled = p.Settle(m.Id, "Won", 10, false);
            Check(NoNpc(settled) && settled.Ladder.OpponentRating == settled.Ladder.RatingBefore, "legacy settle unrated by NPC");
            p.AcknowledgeRewardPresentation(m.Id);
            p.Begin("mage", "warrior", 2);
        }
        byte[] legacy = File.ReadAllBytes(path);
        int current = (int)typeof(LocalProfile).GetField("CurrentVersion", System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Static)!.GetRawConstantValue()!;
        Check(Read(path).Version == current, "fixture is the current profile version");
        Check(!Text(path).Contains("Npc"), "legacy save writes no Npc element");
        using (var p = new LocalProfile(path)) {
            Check(p.History.All(NoNpc) && NoNpc(p.Pending), "V10 save loads with null NPC fields");
            Check(File.ReadAllBytes(path).SequenceEqual(legacy), "load does not rewrite the save");
            p.SelectCardBack(0);
        }
        Check(File.ReadAllBytes(path).SequenceEqual(legacy), "re-save of a legacy profile is byte-identical");
    }

    static void RoundTrip(string root)
    {
        string path = Path.Combine(root, "npc-roundtrip.xml");
        string id;
        using (var p = new LocalProfile(path)) {
            Reject(() => p.Begin("mage", "npc-dragon-warrior", 1, "npc.n.bad\n", "npc-dragon-warrior", 1500), "control char NPC id");
            Reject(() => p.Begin("mage", "npc-dragon-warrior", 1, "npc.n.x", "npc-dragon-warrior", double.NaN), "NaN NPC rating");
            Check(p.Pending == null, "rejected Begin leaves no pending match");
            var m = p.Begin("mage", "npc-dragon-warrior", 7, "npc.n.dragonyyyy", "npc-dragon-warrior", 1612.5);
            id = m.Id;
            Check(m.Opponent == "npc-dragon-warrior" && m.NpcId == "npc.n.dragonyyyy" && m.NpcDeckId == "npc-dragon-warrior"
                && m.NpcRating == 1612.5 && m.NpcConceded == null, "Begin records the encounter");
        }
        string xml = Text(path);
        Check(xml.Contains("<NpcId>npc.n.dragonyyyy</NpcId>") && xml.Contains("<NpcDeckId>npc-dragon-warrior</NpcDeckId>")
            && xml.Contains("<NpcRating>1612.5</NpcRating>") && !xml.Contains("NpcConceded"), "pending NPC fields on disk, null omitted");

        // Restart mid-pending: the pending record keeps its encounter; settling without a rating uses the stored one.
        LocalMatchRecord settled;
        using (var p = new LocalProfile(path)) {
            var pending = p.Pending;
            Check(pending.Id == id && pending.NpcId == "npc.n.dragonyyyy" && pending.NpcDeckId == "npc-dragon-warrior"
                && pending.NpcRating == 1612.5, "pending encounter survives restart");
            Reject(() => p.Settle(id, "Lost", 10, false, null, null, true), "NPC concession on a loss");
            Check(p.Pending?.Id == id, "rejected settle keeps the pending match");
            settled = p.Settle(id, "Won", 10, false, null, null, true);
            Check(settled.Ladder.OpponentRating == 1612.5 && settled.NpcConceded == true && settled.NpcRating == 1612.5,
                "settle falls back to the stored NPC rating and records the concession");
        }
        Check(Text(path).Contains("<NpcConceded>true</NpcConceded>"), "concession on disk");

        // Retries are idempotent with the stored rating; conflicting retries are rejected without a write.
        using (var p = new LocalProfile(path)) {
            var h = p.History.Single();
            Check(h.NpcId == "npc.n.dragonyyyy" && h.NpcDeckId == "npc-dragon-warrior" && h.NpcRating == 1612.5 && h.NpcConceded == true
                && h.Ladder.OpponentRating == 1612.5, "settled encounter survives restart");
            byte[] bytes = File.ReadAllBytes(path);
            Check(p.Settle(id, "Won", 10, false).Ladder.StarsAfter == settled.Ladder.StarsAfter, "retry without rating");
            Check(p.Settle(id, "Won", 10, false, null, 1612.5, true).Ladder.RatingAfter == settled.Ladder.RatingAfter, "retry with the same rating");
            Reject(() => p.Settle(id, "Won", 10, false, null, 900), "retry with a different rating");
            Reject(() => p.Settle(id, "Won", 10, false, null, null, false), "retry with a different concession");
            Check(File.ReadAllBytes(path).SequenceEqual(bytes), "retries never write");
            p.AcknowledgeRewardPresentation(id);
            // An explicit caller rating still wins over the stored one.
            var next = p.Begin("mage", "npc-zee-shaman", 8, "npc.g.0042", "npc-zee-shaman", 1300);
            Check(p.Settle(next.Id, "Lost", 10, false, null, 1350, false).Ladder.OpponentRating == 1350, "explicit rating wins");
        }
        using (var p = new LocalProfile(path))
            Check(p.History.Last().NpcConceded == false && p.History.Last().NpcRating == 1300, "recorded non-concession survives restart");
    }

    // A crash mid-match is settled as Abandoned on recovery; the loss is rated against the stored NPC.
    static void AbandonUsesStoredRating(string root)
    {
        string path = Path.Combine(root, "npc-abandon.xml");
        using (var p = new LocalProfile(path)) p.Begin("mage", "npc-pure-paladin", 3, "npc.n.fatheruther", "npc-pure-paladin", 1400);
        using (var p = new LocalProfile(path)) {
            Check(p.Pending.NpcRating == 1400, "pending rating survives a crash");
            p.RecoverInterrupted();
            var h = p.History.Single();
            Check(h.Result == "Abandoned" && h.Ladder.OpponentRating == 1400 && h.NpcId == "npc.n.fatheruther" && h.NpcConceded == null,
                "abandon rated against the stored NPC");
        }
    }

    static void Tamper(string root)
    {
        string path = Path.Combine(root, "npc-tamper.xml");
        using (var p = new LocalProfile(path)) {
            var m = p.Begin("mage", "priest", 1, "npc.n.a", "priest", 1500);
            p.Settle(m.Id, "Lost", 10, false);
            p.AcknowledgeRewardPresentation(m.Id);
            p.Begin("mage", "priest", 2, "npc.n.b", "priest", 1500);
        }
        var good = Read(path);
        void Bad(Action<LocalProfileData> change, string message)
        {
            var copy = Read(path); change(copy); Write(path, copy);
            Reject(() => { using var p = new LocalProfile(path); }, message);
            Write(path, good);
        }
        Bad(d => d.Matches[0].NpcConceded = true, "NPC concession on a lost match");
        Bad(d => d.Pending.NpcConceded = false, "pending match with a concession flag");
        Bad(d => d.Pending.NpcRating = double.PositiveInfinity, "infinite NPC rating");
        Bad(d => d.Pending.NpcId = new string('x', 129), "overlong NPC id");
        using (var p = new LocalProfile(path)) Check(p.Pending.NpcId == "npc.n.b", "restored profile loads");
    }
}
