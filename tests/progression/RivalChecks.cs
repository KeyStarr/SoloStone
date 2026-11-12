using System.Text;
using System.Xml.Serialization;

// Rivals-lite (backlog #9): the per-NPC head-to-head is derived from the settled ledger; the one-time rivalry gold is the
// only stored field (LocalMatchRecord.RivalGold, optional element, no schema bump).
internal static class RivalChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception("Rivals: " + message); }
    static void Reject(Action action, string message)
    {
        try { action(); } catch (Exception e) when (e is InvalidOperationException || e is ArgumentException || e is IOException) { return; }
        throw new Exception("Rivals: expected rejection: " + message);
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
    static string Rec(LocalNpcRecord r) => r.Wins + "-" + r.Losses + "-" + r.Ties + ":" + (r.LastResult ?? "none") + (r.IsRival ? ":rival" : "");

    internal static void Run(string root)
    {
        Fold();
        Threshold();
        Accounting(root);
        RivalGoldLedger(root);
        FailedCommit(root);
        Tamper(root);
        LegacyBytes(root);
        Console.WriteLine("PASS rivals-lite: record fold (W/L/concede/abandon/tie, pending and other NPCs excluded), rival threshold "
            + "and transitions, one-time rivalry gold per NPC (ledger, reload, restart, failed commit), validation, byte-stable old saves");
    }

    static LocalMatchRecord M(string npc, string result, int rivalGold = 0) =>
        new LocalMatchRecord { NpcId = npc, Result = result, RivalGold = rivalGold };

    static void Fold()
    {
        var rows = new[] { M("a", "Won"), M("b", "Lost"), M("a", "Lost"), M(null, "Won"), M("a", "Abandoned"), M("a", "Tied"),
            M("a", null), M("a", "Lost") };
        Check(Rec(LocalNpcRecord.Of(rows, "a")) == "1-3-1:Lost:rival", "fold " + Rec(LocalNpcRecord.Of(rows, "a")));
        Check(Rec(LocalNpcRecord.Of(rows, "b")) == "0-1-0:Lost:rival", "other NPC (0-1 is a rival)");
        Check(Rec(LocalNpcRecord.Of(rows, "c")) == "0-0-0:none" && LocalNpcRecord.Of(rows, null).Games == 0, "unknown NPC");
        Check(LocalNpcRecord.Of(new[] { M("a", "Lost"), M("a", "Tied") }, "a").LastResult == "Tied", "last result is the latest");
        Check(LocalNpcRecord.Of(new[] { M("a", "Abandoned") }, "a").LastResult == "Lost", "abandoned reads as a loss");
    }

    static void Threshold()
    {
        foreach (var (w, l, rival) in new[] { (0, 0, false), (0, 1, true), (1, 1, false), (1, 0, false), (0, 2, true), (1, 2, true),
            (2, 2, false), (2, 3, true), (3, 2, false), (5, 6, true) })
            Check(LocalNpcRecord.IsRivalRecord(w, l) == rival, "rival threshold " + w + "-" + l);
        Check(LocalNpcRecord.RivalSettleGold == 50, "rule constants");
    }

    static LocalMatchRecord Play(LocalProfile p, string npc, string result, int seed, bool conceded = false)
    {
        var m = p.Begin("mage", "npc-dragon-warrior", seed, npc, "npc-dragon-warrior", 1500);
        var settled = p.Settle(m.Id, result, 10, conceded);
        p.AcknowledgeRewardPresentation(m.Id);
        return settled;
    }

    static void Accounting(string root)
    {
        string path = Path.Combine(root, "rival-accounting.xml");
        using (var p = new LocalProfile(path))
        {
            Play(p, "npc.n.a", "Won", 1);
            Play(p, "npc.n.a", "Lost", 2, conceded: true);
            Play(p, "npc.n.b", "Lost", 3);
            Play(p, "npc.n.a", "Tied", 4);
            p.Begin("mage", "npc-dragon-warrior", 5, "npc.n.a", "npc-dragon-warrior", 1500);
        }
        using (var p = new LocalProfile(path))
        {
            Check(Rec(p.NpcRecord("npc.n.a")) == "1-1-1:Tied", "pending excluded before recovery: " + Rec(p.NpcRecord("npc.n.a")));
            p.RecoverInterrupted();
            Check(Rec(p.NpcRecord("npc.n.a")) == "1-2-1:Lost:rival", "abandoned counts as a loss: " + Rec(p.NpcRecord("npc.n.a")));
            Check(Rec(p.NpcRecord("npc.n.b")) == "0-1-0:Lost:rival" && p.NpcRecord("npc.n.c").Games == 0, "other NPCs excluded");
            Check(p.History.All(m => m.RivalGold == 0), "no rivalry, no gold");
        }
    }

    static void RivalGoldLedger(string root)
    {
        string path = Path.Combine(root, "rival-ledger.xml");
        string settleId;
        int balance;
        using (var p = new LocalProfile(path))
        {
            Check(Play(p, "npc.n.r", "Lost", 1).RivalGold == 0 && p.NpcRecord("npc.n.r").IsRival, "0-1 is already a rivalry, a loss pays nothing");
            Play(p, "npc.n.r", "Lost", 2);
            Check(p.NpcRecord("npc.n.r").IsRival, "0-2 is a rivalry");
            Check(Play(p, "npc.n.r", "Won", 3).RivalGold == 0 && p.NpcRecord("npc.n.r").IsRival, "1-2 still a rivalry, no gold");
            int before = p.StoredGoldBalance;
            var m = p.Begin("mage", "npc-dragon-warrior", 4, "npc.n.r", "npc-dragon-warrior", 1500);
            var settled = p.Settle(m.Id, "Won", 10, false);
            settleId = m.Id;
            Check(settled.RivalGold == 50 && p.StoredGoldBalance == before + settled.Gold + 50, "settling pays 50 gold once in the receipt");
            Check(!p.NpcRecord("npc.n.r").IsRival && Rec(p.NpcRecord("npc.n.r")) == "2-2-0:Won", "2-2 settles the rivalry");
            int after = p.StoredGoldBalance;
            Check(p.Settle(m.Id, "Won", 10, false).RivalGold == 50 && p.StoredGoldBalance == after, "idempotent replay keeps the receipt, pays nothing");
            p.AcknowledgeRewardPresentation(m.Id);
            balance = p.StoredGoldBalance;
        }
        Check(Text(path).Contains("<RivalGold>50</RivalGold>") && Read(path).Matches.Count(r => r.RivalGold != 0) == 1, "rivalry gold on disk once");
        using (var p = new LocalProfile(path))
        {
            Check(p.StoredGoldBalance == balance && p.History.Single(r => r.Id == settleId).RivalGold == 50
                && Rec(p.NpcRecord("npc.n.r")) == "2-2-0:Won", "reload keeps record, receipt and balance");
            Play(p, "npc.n.r", "Lost", 5);
            Check(p.NpcRecord("npc.n.r").IsRival, "2-3 is a rivalry again");
            int before = p.StoredGoldBalance;
            var again = Play(p, "npc.n.r", "Won", 6);
            Check(again.RivalGold == 0 && p.StoredGoldBalance == before + again.Gold && !p.NpcRecord("npc.n.r").IsRival,
                "second settlement with the same NPC pays nothing");
            Play(p, "npc.n.s", "Lost", 7); Play(p, "npc.n.s", "Lost", 8); Play(p, "npc.n.s", "Lost", 9);
            Check(Play(p, "npc.n.s", "Won", 10).RivalGold == 0, "1-3: still a rivalry, no gold");
            Check(Play(p, "npc.n.s", "Won", 11).RivalGold == 0, "2-3: still a rivalry, no gold");
            Check(Play(p, "npc.n.s", "Won", 12).RivalGold == 50, "3-3 against a different NPC pays again");
            Check(Play(p, "npc.n.s", "Won", 13).RivalGold == 0, "no rivalry left, no gold");
            Play(p, "npc.n.t", "Lost", 14); Play(p, "npc.n.t", "Lost", 15);
            Check(Play(p, "npc.n.t", "Tied", 16).RivalGold == 0 && p.NpcRecord("npc.n.t").IsRival, "a tie never settles");
            // 0-1 then a win to 1-1 is the shortest rivalry: it settles and pays once.
            Check(Play(p, "npc.n.e", "Lost", 17).RivalGold == 0 && p.NpcRecord("npc.n.e").IsRival, "npc.n.e: 0-1 is a rivalry");
            int beforeEven = p.StoredGoldBalance;
            var even = Play(p, "npc.n.e", "Won", 18);
            Check(even.RivalGold == 50 && p.StoredGoldBalance == beforeEven + even.Gold + 50, "0-1 then a win pays 50 once");
            Check(Rec(p.NpcRecord("npc.n.e")) == "1-1-0:Won" && !p.NpcRecord("npc.n.e").IsRival, "1-1 is not a rivalry");
            // Second cycle on the same NPC: 1-2 is a rivalry again, and the win back to 2-2 pays nothing.
            Check(Play(p, "npc.n.e", "Lost", 19).RivalGold == 0 && p.NpcRecord("npc.n.e").IsRival, "1-2 is a rivalry again");
            int beforeCycle = p.StoredGoldBalance;
            var second = Play(p, "npc.n.e", "Won", 20);
            Check(second.RivalGold == 0 && p.StoredGoldBalance == beforeCycle + second.Gold && !p.NpcRecord("npc.n.e").IsRival,
                "second cycle with the same NPC pays nothing");
            balance = p.StoredGoldBalance;
        }
        // Restart after settlement: a fresh LocalProfile on the same file sees the same ledger and balance.
        using (var p = new LocalProfile(path))
            Check(p.StoredGoldBalance == balance && p.History.Count(r => r.RivalGold == 50) == 3
                && p.History.Where(r => r.RivalGold > 0).Select(r => r.NpcId).SequenceEqual(new[] { "npc.n.r", "npc.n.s", "npc.n.e" }), "restart consistent");
    }

    static void FailedCommit(string root)
    {
        string path = Path.Combine(root, "rival-fault.xml");
        using var p = new LocalProfile(path);
        Play(p, "npc.n.f", "Lost", 1); Play(p, "npc.n.f", "Lost", 2); Play(p, "npc.n.f", "Won", 3);
        var m = p.Begin("mage", "npc-dragon-warrior", 4, "npc.n.f", "npc-dragon-warrior", 1500);
        byte[] before = File.ReadAllBytes(path);
        int gold = p.StoredGoldBalance;
        LocalProfile.CommitFaultHook = phase => { if (phase == "replace") throw new IOException("rival fault"); };
        try { Reject(() => p.Settle(m.Id, "Won", 10, false), "settlement save fault"); }
        finally { LocalProfile.CommitFaultHook = null; }
        Check(File.ReadAllBytes(path).SequenceEqual(before), "failed commit changed the save");
        Check(p.StoredGoldBalance == gold && p.Pending?.Id == m.Id, "failed commit changed gold or pending");
        Check(Rec(p.NpcRecord("npc.n.f")) == "1-2-0:Won:rival" && p.History.All(r => r.RivalGold == 0), "failed commit changed the record: "
            + Rec(p.NpcRecord("npc.n.f")));
        var settled = p.Settle(m.Id, "Won", 10, false);
        Check(settled.RivalGold == 50 && p.StoredGoldBalance == gold + settled.Gold + 50, "retry after the fault pays once");
    }

    static void Tamper(string root)
    {
        string path = Path.Combine(root, "rival-tamper.xml");
        using (var p = new LocalProfile(path))
        {
            Play(p, "npc.n.x", "Lost", 1); Play(p, "npc.n.x", "Lost", 2); Play(p, "npc.n.x", "Won", 3);
            Check(Play(p, "npc.n.x", "Won", 4).RivalGold == 50, "fixture settles a rivalry");
            Play(p, "npc.n.x", "Won", 5);
            p.Begin("mage", "npc-dragon-warrior", 6, "npc.n.x", "npc-dragon-warrior", 1500);
        }
        var good = Read(path);
        void Bad(Action<LocalProfileData> change, string message)
        {
            var copy = Read(path); change(copy); Write(path, copy);
            Reject(() => { using var p = new LocalProfile(path); }, message);
            Write(path, good);
        }
        Bad(d => d.Matches[0].RivalGold = 50, "rivalry gold on a loss");
        Bad(d => d.Matches[3].RivalGold = 77, "rivalry gold of 77");
        Bad(d => d.Matches[4].RivalGold = 50, "two rivalry rewards for one NPC");
        Bad(d => d.Pending.RivalGold = 50, "rivalry gold on a pending match");
        Bad(d => { d.Matches[3].NpcId = null; d.Matches[3].NpcDeckId = null; d.Matches[3].NpcRating = null; }, "rivalry gold without an NPC");
        Bad(d => d.Matches[3].RivalGold = -50, "negative rivalry gold");
        using (var p = new LocalProfile(path)) Check(p.History[3].RivalGold == 50 && p.Pending.NpcId == "npc.n.x", "restored profile loads");
    }

    // A save whose matches never paid rivalry gold has no RivalGold element and re-saves byte-identically.
    static void LegacyBytes(string root)
    {
        string path = Path.Combine(root, "rival-legacy.xml");
        // Won then Lost: 1-1 is no rivalry, so nothing settles (a Lost then Won would be 0-1 then 1-1 and pay).
        using (var p = new LocalProfile(path)) { Play(p, "npc.n.l", "Won", 1); Play(p, "npc.n.l", "Lost", 2); }
        byte[] saved = File.ReadAllBytes(path);
        Check(!Text(path).Contains("RivalGold"), "zero rivalry gold writes no element");
        using (var p = new LocalProfile(path))
        {
            Check(File.ReadAllBytes(path).SequenceEqual(saved) && p.History.All(r => r.RivalGold == 0), "load does not rewrite");
            p.SelectCardBack(0);
        }
        Check(File.ReadAllBytes(path).SequenceEqual(saved), "re-save of an old-shape profile is byte-identical");
    }
}
