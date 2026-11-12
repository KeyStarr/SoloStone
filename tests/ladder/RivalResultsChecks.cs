// Rivals-lite (backlog #9): the one-time rivalry gold shows in the post-match reward items, text and results receipt.
internal static class RivalResultsChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception("Rival results: " + message); }

    internal static void Run(string root)
    {
        string path = Path.Combine(root, "rival-results.xml");
        var names = LocalProfile.NpcDisplayName;
        try
        {
            using var p = new LocalProfile(path);
            LocalMatchRecord Play(string result, int seed)
            {
                var m = p.Begin("mage", "npc-dragon-warrior", seed, "npc.n.rivalshow", "npc-dragon-warrior", 1500);
                var settled = p.Settle(m.Id, result, 10, false);
                p.AcknowledgeRewardPresentation(m.Id);
                return settled;
            }
            Play("Lost", 1);
            var won = Play("Won", 2);
            Check(won.RivalGold == 50, "fixture settles a rivalry: 0-1 then a win to 1-1");
            LocalProfile.NpcDisplayName = null;
            var summary = LocalRewardPresentation.Summary(won);
            int itemGold = summary.Items.Where(i => i.Kind == LocalRewardKind.Gold).Sum(i => i.Amount);
            Check(itemGold == won.Gold + 50 + won.AchievementGrants.Sum(g => g.Gold), "gold item includes rivalry gold: " + itemGold);
            Check(summary.Body.Contains("Rivalry settled with your rival: +50 gold"), "unnamed rival line: " + summary.Body);
            LocalProfile.NpcDisplayName = id => id == "npc.n.rivalshow" ? "Nemo" : null;
            Check(LocalRewardPresentation.Summary(won).Body.Contains("Rivalry settled with Nemo: +50 gold"), "named rival line");
            LocalProfile.NpcDisplayName = id => throw new InvalidOperationException("world down");
            Check(LocalRewardPresentation.Summary(won).Body.Contains("with your rival:"), "a failing name seam falls back");
            var receipt = LocalResultsReceipt.FromHistory(p.History, won);
            Check(receipt.Gold == won.Gold + 50 && receipt.HasItems, "results receipt gold includes rivalry gold");
            var plain = p.History[0];
            Check(!LocalRewardPresentation.Summary(plain).Body.Contains("Rivalry") && LocalResultsReceipt.FromHistory(p.History, plain).Gold == plain.Gold,
                "no rivalry, no line");
        }
        finally { LocalProfile.NpcDisplayName = names; }
        Console.WriteLine("PASS rivalry gold in reward items, summary text and results receipt");
    }
}
