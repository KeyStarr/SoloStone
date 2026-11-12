using System.Xml.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B14F5DiscoverProof
{
    internal static void Run(Game g, Controller p, string id)
    {
        var declarations = SabberStoneCore.CardSets.Client.OfflinePoolDeclarations.Cards.Where(row => row.CardId == id).ToArray();
        Core2026Proof.Check(declarations.Length == 1, "Core Discover recipe has one discovered offline pool declaration: " + id);
        var report = OfflinePoolAdmission.Evaluate(g.OfflinePools, declarations);
        report.ValidateRegistered();
        foreach (var row in report.Cards.Single(c => c.CardId == id).PoolRequirements)
            Console.WriteLine($"POOL {id}/{row.Name}/{row.Class}={row.Count}");
        foreach (int branch in id == "Core_LOE_115" ? new[] { 1, 2, -1 } : new[] { -1 })
        {
            PoolPilotProof.EmptyHand(p); StandardSetsProof.ParkBoard(p);
            p.ChooseBoth = id == "Core_LOE_115" && branch == -1;
            p.BaseMana = 10; p.UsedMana = 0;
            var source = Add(p, id);
            g.Random.SetSeed(g.Seed);
            var reference = g.CloneExact();
            var filters = id == "Core_UNG_072" ? new[] { OfflinePoolFilter.Discover(type: CardType.MINION, keyword: GameTag.TAUNT) }
                : branch == -1 ? new[] { OfflinePoolFilter.Discover(type: CardType.MINION), OfflinePoolFilter.Discover(type: CardType.SPELL) }
                : new[] { OfflinePoolFilter.Discover(type: branch == 1 ? CardType.MINION : CardType.SPELL) };
            var expected = filters.Select(filter => OfflineCardPools.SampleDistinct(reference, filter, p.HeroClass, source.Card.AssetId)
                .Select(c => c.Id).ToArray()).ToArray();
            var task = p.Options().First(t => t.Source?.Id == source.Id && t.ChooseOne == branch);
            PoolPilotProof.Resume(g, next => Core2026Proof.Check(next.Process(next.ControllerById(p.Id).Options()
                .First(t => t.Source?.Id == source.Id && t.ChooseOne == branch)), "Discover play after clone/load"));
            int count = 0;
            while (p.Choice != null)
            {
                var offered = p.Choice.Choices.Select(n => g.IdEntityDic[n]).ToArray();
                Core2026Proof.Check(offered.Length == 3 && offered.Select(c => c.Card.Id).Distinct().Count() == 3, "three distinct admitted offers");
                Core2026Proof.Check(offered.All(c => c.Card.Id != id && (id == "Core_UNG_072" ? c.Card.Taunt :
                    c.Card.Type == (branch == 2 || branch == -1 && count == 1 ? CardType.SPELL : CardType.MINION))), "exact declared branch filter");
                Core2026Proof.Check(offered.Select(c => c.Card.Id).SequenceEqual(expected[count]), "seeded exact offer order; fixed first candidates fail");
                int pick = offered[(int)g.Seed - 1].Id;
                PoolPilotProof.Resume(g, next => Core2026Proof.Check(next.Process(ChooseTask.Pick(next.ControllerById(p.Id), pick)), "Discover resumes after clone/load"));
                count++;
            }
            Core2026Proof.Check(count == (id == "Core_LOE_115" && branch == -1 ? 2 : 1) && p.HandZone.Count >= count, "one chosen card per Discover, Choose Both keeps both");
            if (id == "Core_UNG_072") Core2026Proof.Check(p.BoardZone.Single().HasTaunt, "Stonehill retains printed Taunt");
            p.ChooseBoth = false;
        }
        Boundaries(g, p, id);
    }

    private static void Boundaries(Game g, Controller p, string id)
    {
        var installed = g.OfflinePools;
        var property = typeof(Game).GetProperty(nameof(Game.OfflinePools));
        foreach (int branch in id == "Core_LOE_115" ? new[] { 1, 2 } : new[] { -1 })
        {
            var filter = id == "Core_UNG_072" ? OfflinePoolFilter.Discover(type: CardType.MINION, keyword: GameTag.TAUNT)
                : OfflinePoolFilter.Discover(type: branch == 1 ? CardType.MINION : CardType.SPELL);
            var candidates = OfflineCardPools.Candidates(g, p.HeroClass, filter, Cards.FromId(id).AssetId);
            foreach (int size in new[] { 0, 1, 2 })
            {
                var xml = OfflinePoolFixtures.Catalog(); var rows = xml.Root.Element("Members");
                var selected = rows.Elements("Card").Where(row => candidates.Take(size).Any(c => c.Id == (string)row.Attribute("cardId")))
                    .Concat(rows.Elements("Card").Where(row => (string)row.Attribute("scope") == "Past")).ToArray();
                rows.ReplaceNodes(selected);
                try
                {
                    property.SetValue(g, OfflinePoolFixtures.Manifest(xml));
                    PoolPilotProof.EmptyHand(p); StandardSetsProof.ParkBoard(p); p.ChooseBoth = false;
                    var source = Add(p, id); p.BaseMana = 10; p.UsedMana = 0;
                    long draws = g.Random.DrawCount;
                    Core2026Proof.Check(g.Process(p.Options().First(t => t.Source?.Id == source.Id && t.ChooseOne == branch)), "shortage play");
                    Core2026Proof.Check(size == 0 ? p.Choice == null : p.Choice?.Choices.Count == size, "empty/tiny pool yields exactly its available identities");
                    if (size == 0) Core2026Proof.Check(g.Random.DrawCount == draws, "empty pool consumes no RNG");
                    else
                    {
                        int chosen = p.Choice.Choices[0];
                        Core2026Proof.Check(g.Process(ChooseTask.Pick(p, chosen)), "tiny pool selection");
                        Core2026Proof.Check(p.HandZone.Count >= 1, "shortage selected card enters hand");
                    }
                }
                finally { property.SetValue(g, installed); }
            }
            PoolPilotProof.EmptyHand(p); StandardSetsProof.ParkBoard(p);
            var fullSource = Add(p, id); p.BaseMana = 10; p.UsedMana = 0;
            Core2026Proof.Check(g.Process(p.Options().First(t => t.Source?.Id == fullSource.Id && t.ChooseOne == branch)), "capacity Discover play");
            int pick = p.Choice.Choices[0]; PoolPilotProof.FillHand(p);
            PoolPilotProof.Resume(g, next => Core2026Proof.Check(next.Process(ChooseTask.Pick(next.Player1, pick)), "full-hand choice accepted"));
            Core2026Proof.Check(p.HandZone.Count == 10 && g.IdEntityDic[pick].Zone != p.HandZone, "full hand burns chosen card without replacing held cards");
        }
    }
}
