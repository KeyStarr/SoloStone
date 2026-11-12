using SabberStoneCore.Model.Zones;
using System.Xml.Linq;
using HsSim;
using SabberStoneCore.Enchants;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static B14K3BEProof;

internal static class B14K3BEProof
{
    [GroupProof("b14-k3-mage-cards", ProofKind.Cards)]
    internal static void Run()
    {
        CardsProof(); B14K3CATACLYSMProof.CardsProof();
        B14K3EMERALDDREAMProof.CardsProof(); B14K3ESCAPEFROMVIOLETHOLDProof.CardsProof(); B14K3TIMETRAVELProof.CardsProof();
    }
    internal static OfflinePoolFilter Filter(string id) => OfflinePoolDeclarations.Cards.Single(d => d.CardId == id).Pools.Single().Filter;
    internal static void CheckPoolBuckets(Game game, string id, int poolCount = 1)
    {
        var declarations = OfflinePoolDeclarations.Cards.Where(d => d.CardId == id).ToArray();
        Core2026Proof.Check(declarations.Length == 1 && declarations[0].DbfId == Cards.FromId(id).AssetId
            && declarations[0].Pools.Count == poolCount, id + " local pool provider discovered exactly once");
        foreach (var pool in declarations[0].Pools)
        {
            Core2026Proof.Check(pool.ClassDomain.SequenceEqual(new[] { "MAGE" }), id + " printed pool class domain");
            var costs = pool.CostBinding == PoolCostBinding.None ? new[] { -1 }
                : Enumerable.Range(0, Math.Max(10, OfflineCardPools.Candidates(game, CardClass.MAGE, pool.Filter, declarations[0].DbfId).Max(c => c.Cost)) + 1).ToArray();
            var sizes = costs.Select(cost => {
                var filter = cost < 0 ? pool.Filter : pool.Filter.WithCost(cost);
                int count = OfflineCardPools.Candidates(game, CardClass.MAGE, filter, declarations[0].DbfId).Count;
                if (ClientExport.Oracle)
                    Core2026Proof.Check(count == OfflinePoolExpectations.Count(filter, "MAGE", declarations[0].DbfId)
                        && (pool.CostBinding != PoolCostBinding.None || count >= 3), id + " independent admitted bucket count " + cost);
                else
                {
                    ClientExport.Skip("B14 K3 pool bucket counts against the independent derivation from the client export");
                    Core2026Proof.Check(pool.CostBinding != PoolCostBinding.None || count >= 3, id + " admitted bucket count " + cost);
                }
                return count;
            });
            Console.WriteLine($"b14-k3 pool {id}/{pool.Name} MAGE costs={string.Join(",", costs)} counts={string.Join(",", sizes)}");
        }
    }
    internal static void Pool(Game game, string[] ids, Action action)
    {
        var installed = game.OfflinePools;
        var xml = OfflinePoolFixtures.FixtureCatalog(ids);
        var members = xml.Root.Element("Members");
        members.ReplaceNodes(members.Elements("Card").Where(c => ids.Contains((string)c.Attribute("cardId"))).ToArray());
        if (ids.Length == 0) members.Add(OfflinePoolFixtures.Catalog().Root.Element("Members").Elements("Card").First(c => (string)c.Attribute("scope") == "Past"));
        var manifest = OfflinePoolFixtures.Manifest(xml);
        var property = typeof(Game).GetProperty(nameof(Game.OfflinePools));
        try { property.SetValue(game, manifest); action(); }
        finally { property.SetValue(game, installed); }
    }
    internal static void Future(Game game, Action<Game> action)
    {
        var bytes = game.SaveSnapshot();
        var clone = game.CloneExact(history: true);
        var loaded = Game.LoadSnapshotWithPools(bytes, game.OfflinePools);
        action(clone); action(loaded);
        Core2026Proof.Check(bytes.SequenceEqual(game.SaveSnapshot()), "branch leaves original unchanged");
        action(game);
        Core2026Proof.Check(Canonical.State(game) == Canonical.State(clone) && Canonical.State(game) == Canonical.State(loaded), "clone/snapshot future identical");
    }
    internal static void CardsProof() => StandardSetsProof.Case("BE_150", Coilbearer);
    private static void Coilbearer(Game g, Controller p, string id)
    {
        CheckPoolBuckets(g, id);
        p.AddHeroAndPower(Cards.FromId("HERO_08"));
        var filter = Filter(id); var candidates = OfflineCardPools.Candidates(g, p.HeroClass, filter, Cards.FromId(id).AssetId);
        Core2026Proof.Check(candidates.Count >= 3, "Mage Discover pool minimum");
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var branch = g.CloneExact(history: true); var owner = branch.Player1;
            FillHand(owner, 9); var location = LocationProofs.Location(owner); Summon(owner, "CS2_182");
            var ordered = owner.DeckZone.Select(c => c.Id).ToArray();
            branch.SetRandomSeed(seed);
            var expectedGame = branch.CloneExact(history: true);
            var expected = OfflineCardPools.SampleDistinct(expectedGame, filter, owner.HeroClass, Cards.FromId(id).AssetId).Select(c => c.Id).ToArray();
            var source = PlayAt(branch, owner, id, seed % 2 == 0 ? 0 : 1);
            var offers = owner.Choice.Choices.ToArray();
            Core2026Proof.Check(offers.Select(i => branch.IdEntityDic[i].Card.Id).SequenceEqual(expected) && expected.Distinct().Count() == 3
                && offers.All(i => candidates.Contains(branch.IdEntityDic[i].Card)), "seeded distinct class-filtered offers, source excluded");
            var selected = branch.IdEntityDic[offers[seed - 1]];
            Future(branch, next => {
                var player = next.Player1;
                PlayerDecisionProof.Answer(next, selected.Id);
                var generated = next.IdEntityDic[selected.Id];
                Core2026Proof.Check(player.DeckZone.TopCard.Id == selected.Id && generated.Cost == Math.Max(0, generated.Card.Cost - 2)
                    && generated[GameTag.COST] == generated.Cost && player.HandZone.Count == 9
                    && player.DeckZone.Take(ordered.Length).Select(c => c.Id).SequenceEqual(ordered), "chosen discounted spell is exact topdeck; full hand irrelevant; prior order unchanged");
                Generic.Draw(player);
                Core2026Proof.Check(player.HandZone.Contains(generated) && generated.Cost == Math.Max(0, generated.Card.Cost - 2), "topdeck discount survives draw");
                ((Minion)next.IdEntityDic[source.Id]).Silence();
                Kill(next, (Minion)next.IdEntityDic[source.Id]); End(next); End(next);
                Core2026Proof.Check(generated.Cost == Math.Max(0, generated.Card.Cost - 2), "discount persists after Silence/death/turn");
            });
        }
        Pool(g, Array.Empty<string>(), () => { Play(g, p, id); Core2026Proof.Check(p.Choice == null, "empty Discover completes without offer"); });
        ParkBoard(p);
        while (p.DeckZone.Count < DeckZone.DeckMaximumCapcity) Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        Play(g, p, id); PlayerDecisionProof.Answer(g, p.Choice.Choices[0]);
        Core2026Proof.Check(p.DeckZone.Count == DeckZone.DeckMaximumCapcity && p.Choice == null, "full deck completes without insertion");
        ParkBoard(p); LostCityPoolProof.FullBoard(p); var held = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == held.Id), "full mixed board prevents minion play");
    }
    internal static void FillHand(Controller p, int count)
    { while (p.HandZone.Count < count) Add(p, "CS2_182"); }

}
