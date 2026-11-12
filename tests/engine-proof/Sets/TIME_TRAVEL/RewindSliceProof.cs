using System.Xml.Linq;
using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class RewindSliceProof
{
    internal static readonly OfflinePoolFilter Rewind = OfflinePoolFilter.Random(keyword: (GameTag)3934);
    // Same transient manifest seam as TimeTravelPoolProof; never changes installed admission.
    internal static void WithRewindPool(Game g, Action action)
    {
        var installed = g.OfflinePools;
        var xml = OfflinePoolFixtures.Catalog();
        var rows = new[] { "TIME_002", "TIME_003", "TIME_004", "TIME_035", "END_036" }.Select(id => {
            var card = Cards.FromId(id);
            return new XElement("Card", new XAttribute("dbfId", card.AssetId), new XAttribute("cardId", id),
                new XAttribute("scope", "Standard"), new XAttribute("set", "TIME_TRAVEL"),
                new XAttribute("proofKey", "RewindSliceProof"), new XAttribute("registrationKind", "standard-module"),
                new XAttribute("type", "MINION"), new XAttribute("cost", card.Cost), new XAttribute("attack", card.ATK),
                new XAttribute("health", card.Health), new XAttribute("rarity", card.Rarity), new XAttribute("spellSchool", "NONE"),
                new XElement("Class", new XAttribute("name", "NEUTRAL")),
                card.Tags.Select(t => new XElement("Tag", new XAttribute("id", (int)t.Key), new XAttribute("value", t.Value))));
        }).ToArray();
        xml.Root.Element("Members").ReplaceNodes(rows);
        xml.Root.Element("Dependencies").RemoveNodes();
        var property = typeof(Game).GetProperty(nameof(Game.OfflinePools));
        try { property.SetValue(g, OfflinePoolFixtures.Manifest(xml)); action(); }
        finally { property.SetValue(g, installed); }
    }

    internal static int Answer(Game g, bool resume = true)
    {
        var view = g.GetPendingDecisions().Single();
        int chosen = g.Player1.Choice.Choices[0];
        var answer = new DecisionAnswer(view.Id, view.ControllerId, new[] { chosen });
        void Accept(Game next) => Core2026Proof.Check(next.TryAnswer(answer, out var error), "Discover answer: " + error);
        if (resume) PoolPilotProof.Resume(g, Accept); else Accept(g);
        return chosen;
    }

    internal static void CheckCopies(Game g, Action<Game> continuation)
    {
        var clone = g.CloneExact(history: true); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
        foreach (var next in new[] { g, clone, loaded }) continuation(next);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(loaded),
            "source/clone/load future parity");
    }
}
