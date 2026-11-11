using System.Xml.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using HsSim;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class PoolPilotProof
{
    internal static OfflinePoolFilter Filter(string id) => OfflinePoolDeclarations.Cards.Single(c => c.CardId == id).Pools.Single().Filter;
    internal static void EmptyHand(Controller p)
    {
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
    }
    internal static void FillHand(Controller p, int count = 10)
    { while (p.HandZone.Count < count) Add(p, "CS2_182"); }
    internal static void ClassDeck(Controller p)
    {
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
    }
    internal static void Resolve(Game g, IPlayable source, IPlayable target = null, bool death = false)
    {
        (death ? source.Card.Power.DeathrattleTask : source.Card.Power.PowerTask)
            .Process(g, source.Controller, source, target, new TaskStack());
        g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
    }
    // Test-only manifest injection: exercise the real module with zero/one candidates, never change Card.
    internal static void Pool(Game g, string source, int count, Action action)
    {
        var installed = g.OfflinePools;
        var filter = Filter(source);
        var candidates = OfflineCardPools.Candidates(g, g.Player1.HeroClass, filter, Cards.FromId(source).AssetId);
        var xml = OfflinePoolFixtures.Catalog();
        var rows = xml.Root.Element("Members");
        var selected = rows.Elements("Card").Where(r => candidates.Take(count).Any(c => c.Id == (string)r.Attribute("cardId"))).ToArray();
        if (count == 0)
        {
            selected = rows.Elements("Card").Take(1).ToArray();
            selected[0].SetAttributeValue("scope", "Standard");
            selected[0].SetAttributeValue("set", "CORE");
            selected[0].SetAttributeValue("type", filter.Type == CardType.MINION ? "SPELL" : "MINION");
            selected[0].SetAttributeValue("cost", 999);
            selected[0].SetAttributeValue("spellSchool", "NONE");
            selected[0].Elements("Class").Remove();
            selected[0].Add(new System.Xml.Linq.XElement("Class", new System.Xml.Linq.XAttribute("name", "NEUTRAL")));
        }
        rows.ReplaceNodes(selected);
        var manifest = OfflinePoolFixtures.Manifest(xml);
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, g.Player1.HeroClass, filter, Cards.FromId(source).AssetId).Count == count, "pilot fixture candidate count");
        var property = typeof(Game).GetProperty(nameof(Game.OfflinePools));
        try { property.SetValue(g, manifest); action(); }
        finally { property.SetValue(g, installed); }
    }
    internal static void Generated(Game g, IPlayable source, IEnumerable<IPlayable> outputs, int expected, bool discount = false, OfflinePoolFilter? filter = null)
    {
        var candidates = OfflineCardPools.Candidates(g, source.Controller.HeroClass, filter ?? Filter(source.Card.Id), source.Card.AssetId);
        var cards = outputs.ToArray();
        IPlayable Root(IPlayable card)
        {
            while (card.Card.Shatter is { } family && card[GameTag.DISPLAYED_CREATOR] != source.Id &&
                g.IdEntityDic.TryGetValue(card[GameTag.DISPLAYED_CREATOR], out var parent) &&
                parent.Id < card.Id && parent.Card.Shatter?.ParentId == family.ParentId) card = parent;
            return card;
        }
        var roots = g.IdEntityDic.Values.Where(card => card[GameTag.DISPLAYED_CREATOR] == source.Id &&
            (cards.Contains(card) || card.Card.Shatter is { } family && card.Card.Id == family.ParentId &&
                card[SabberStoneCore.Actions.ShatterRules.AlreadyCombined] == 0 && card[GameTag.ZONE] == (int)Zone.REMOVEDFROMGAME)).ToArray();
        int halves = cards.Count(card => card.Card.Shatter is { } family && card.Card.Id != family.ParentId);
        Core2026Proof.Check(roots.Length == expected && halves % 2 == 0 && cards.Length == expected + halves / 2
            && cards.Where(card => card.Card.Shatter != null).GroupBy(card => card.Card.Shatter.ParentId).All(group =>
                group.Count(card => card.Card.Id == card.Card.Shatter.FirstHalfId) == group.Count(card => card.Card.Id == card.Card.Shatter.SecondHalfId))
            && cards.All(card => roots.Contains(Root(card)) && card.Controller == source.Controller)
            && roots.All(card => candidates.Any(x => x.Id == card.Card.Id && x.AssetId == card.Card.AssetId)
                && card.Card.Id != source.Card.Id && card.Controller == source.Controller),
            "generated exact root/live counts, complementary Shatter halves, identity/filter/source/owner/creator");
        foreach (var c in cards)
        {
            var member = g.OfflinePools.Standard.Concat(g.OfflinePools.Past).Single(m => m.CardId == Root(c).Card.Id);
            var printed = c.Card.Id == member.CardId ? Root(c).Card : Cards.FromId(c.Card.Id);
            Core2026Proof.Check(c.Card.Id == member.CardId || member.DependencyIds.Contains(c.Card.Id), "generated audited half dependency");
            Core2026Proof.Check(c.Card.Cost == printed.Cost && c.Card.ATK == printed.ATK && c.Card.Health == printed.Health
                && Root(c).Card.Cost == member.Cost && Root(c).Card.ATK == member.Attack && Root(c).Card.Health == member.Health,
                "generated pinned definition stats");
            int cost = (CostRules.HasManaNumbers(c) ? CostRules.ManaNumbers(c) : c.Card.Cost)
                - (discount ? c[SabberStoneCore.Actions.ShatterRules.AlreadyCombined] == 1 ? 4 : 2 : 0);
            if (c.Card.Id == "JAIL_514") cost -= c.Controller.HandZone.Count;
            if (c.Card.Id == "CATA_616" && c.Zone == c.Controller.HandZone) cost -= CostRules.LastPlayedCost(c.Controller);
            if (c.Card.Id == "JAIL_307" && c.Controller.DeckZone.Count >= 25) cost -= 2;
            if (c.Card.Id == "JAIL_204" && !c.Controller.BoardZone.Any(m => m.Id != c.Id)
                && !c.Controller.Opponent.BoardZone.Any(m => m.Id != c.Id)) cost = 2;
            if (c.Card.Id == "TLC_365" && g.HistoryFacts.Query.Discoveries(g.HistoryFacts.Query.ThisTurn).Any(f => f.ControllerId == c.Controller.Id)) cost = 0;
            // Eye Beam's printed Outcast aura sets its Cost to (1) at either end of the hand.
            if (c.Card.Id == "CORE_BT_801" && c.Zone == c.Controller.HandZone
                && (c.ZonePosition == 0 || c.ZonePosition == c.Controller.HandZone.Count - 1)) cost = 1;
            Core2026Proof.Check(c.Cost == Math.Max(0, cost), "generated entity cost " + c.Card.Id);
            if (c is Minion m)
            {
                int manaDelta = CostRules.HasManaNumbers(m) && m.Zone == m.Controller.HandZone ? CostRules.ManaNumbers(m) - m.Card.Cost : 0;
                int attack = m.Card.ATK + manaDelta + (m.Card.Id == "JAIL_311" && m.Controller.DeckZone.Count >= 25 ? 5 : 0);
                Core2026Proof.Check(m.AttackDamage == attack && m.Health == m.Card.Health + manaDelta, "generated effective stats " + c.Card.Id);
            }
        }
    }
    internal static void Resume(Game game, Action<Game> action)
    {
        byte[] before = game.SaveSnapshot();
        int historyCount = game.PowerHistory.Full.Count;
        var clone = game.CloneExact(history: true); var loaded = Game.LoadSnapshotWithPools(before, game.OfflinePools);
        action(clone); action(loaded);
        Core2026Proof.Check(before.SequenceEqual(game.SaveSnapshot()), "pilot clone/load isolation");
        action(game);
        foreach (var other in new[] { clone, loaded })
        {
            Core2026Proof.Check(Canonical.State(game) == Canonical.State(other), "pilot resumed state");
            var expectedHistory = ReferenceEquals(other, clone) ? game.PowerHistory.Full.Skip(historyCount) : game.PowerHistory.Full;
            Core2026Proof.Check(Canonical.Value(expectedHistory) == Canonical.Value(other.PowerHistory.Full), "pilot resumed history");
            Core2026Proof.Check(game.Random.CaptureState().Equals(other.Random.CaptureState()) && game.OfflinePools.Hash == other.OfflinePools.Hash,
                "pilot resumed RNG/manifest");
        }
    }
    internal static void SourceExcluded(Game g, string source)
    {
        int dbfId = Cards.FromId(source).AssetId;
        Core2026Proof.Check(OfflineCardPools.Candidates(g, g.Player1.HeroClass, Filter(source), dbfId).All(c => c.AssetId != dbfId), "pilot source excluded");
    }
    internal static void HandBoundaries(Game g, Controller p, string id, int picks, bool death = false, bool discount = false)
    {
        SourceExcluded(g, id);
        var source = Entity.FromCard(p, Cards.FromId(id));
        EmptyHand(p);
        Pool(g, id, 0, () => {
            var random = g.Random.CaptureState(); int entities = g.IdEntityDic.Count;
            Resolve(g, source, p.Opponent.Hero, death);
            Core2026Proof.Check(p.HandZone.Count == 0 && entities == g.IdEntityDic.Count && g.Random.CaptureState().Equals(random), "empty pool creates nothing and draws no RNG");
        });
        Pool(g, id, 1, () => {
            Resolve(g, source, p.Opponent.Hero, death);
            Generated(g, source, p.HandZone, picks, discount);
            Core2026Proof.Check(p.HandZone.Select(c => c.Card.Id).Distinct().Count() == 1, "independent picks allow duplicates");
        });
        EmptyHand(p); FillHand(p, 9);
        var before = g.Random.DrawCount;
        Resolve(g, source, p.Opponent.Hero, death);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount - before == picks, "one hand slot: all independent picks consumed");
        FillHand(p); before = g.Random.DrawCount;
        Resolve(g, source, p.Opponent.Hero, death);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount - before == picks, "full hand: all picks consumed");
        EmptyHand(p);
    }
}
