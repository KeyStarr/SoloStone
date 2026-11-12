using System.Xml.Linq;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class VioletHoldPoolProof
{
    internal static OfflinePoolFilter Filter(string id)
    {
        // Card-text expectations stay independent of the production pool declarations.
        var expected = id switch {
            "JAIL_986" => OfflinePoolFilter.Random(type: CardType.SPELL),
            "JAIL_313" => OfflinePoolFilter.Random(type: CardType.SPELL),
            "JAIL_879" => new OfflinePoolFilter(PoolScope.Standard, PoolClassMode.Any, type: CardType.MINION, minCost: 5, maxCost: 5, race: Race.BEAST),
            "JAIL_460" => new OfflinePoolFilter(PoolScope.Standard, PoolClassMode.Any, type: CardType.WEAPON),
            "CAP_105" => new OfflinePoolFilter(PoolScope.Standard, PoolClassMode.ControllerAndNeutral, type: CardType.MINION, race: Race.PIRATE, discoverFallback: true),
            "CAP_407" => new OfflinePoolFilter(PoolScope.Standard, PoolClassMode.ControllerAndNeutral, type: CardType.MINION, minCost: 5, discoverFallback: true),
            "JAIL_407" => new OfflinePoolFilter(PoolScope.Standard, PoolClassMode.Any, type: CardType.MINION, keyword: GameTag.BATTLECRY),
            "JAIL_448" => new OfflinePoolFilter(PoolScope.Standard, PoolClassMode.Any, type: CardType.MINION, rarity: Rarity.LEGENDARY),
            "JAIL_876" => new OfflinePoolFilter(PoolScope.Standard, PoolClassMode.Any, type: CardType.MINION, minCost: 4, maxCost: 4),
            "JAIL_878" => new OfflinePoolFilter(PoolScope.Standard, PoolClassMode.Any, type: CardType.MINION, minCost: 1, maxCost: 1, keyword: GameTag.DEATHRATTLE),
            "JAIL_912" => new OfflinePoolFilter(PoolScope.Standard, PoolClassMode.Any, type: CardType.MINION, minCost: 6, maxCost: 6),
            "JAIL_201" => new OfflinePoolFilter(PoolScope.Standard, PoolClassMode.Exact, exactClass: CardClass.DRUID),
            "JAIL_706" => new OfflinePoolFilter(PoolScope.Standard, PoolClassMode.Any, type: CardType.SPELL, minCost: 4, maxCost: 4),
            "JAIL_806" => new OfflinePoolFilter(PoolScope.Standard, PoolClassMode.Any, type: CardType.SPELL, minCost: 5),
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "No card-text pool expectation")
        };
        Core2026Proof.Check(OfflinePoolDeclarations.Cards.Single(c => c.CardId == id).Pools.Single().Filter == expected,
            id + " pool matches card text and class domain");
        return expected;
    }

    internal static void Member(Game g, IPlayable source, IPlayable result, OfflinePoolFilter filter)
    {
        // Osk rolls to a noncollectible ability identity on hand entry; the admitted member remains its base printing.
        var identity = SabberStoneCore.CardSets.Client.Sets.TitanographerOsk.Variants.Contains(result.Card.Id)
            ? Cards.FromId("TLC_452") : result.Card;
        var candidates = OfflineCardPools.Candidates(g, source.Controller.HeroClass, filter, source.Card.AssetId);
        Core2026Proof.Check(candidates.Any(c => c.Id == identity.Id && c.AssetId == identity.AssetId)
            && identity.Id != source.Card.Id && result.Id != source.Id && result.Controller == source.Controller
            && result[GameTag.DISPLAYED_CREATOR] == source.Id, "pool member identity/controller/creator/source exclusion: "
            + source.Card.Id + "/" + result.Card.Id + "/candidate=" + candidates.Any(c => c.Id == result.Card.Id)
            + "/owner=" + (result.Controller == source.Controller) + "/creator=" + result[GameTag.DISPLAYED_CREATOR] + "/source=" + source.Id);
        var member = g.OfflinePools.Standard.Single(c => c.CardId == identity.Id);
        Core2026Proof.Check(result.Card.ATK == member.Attack && result.Card.Health == member.Health && result.Card.Cost == member.Cost,
            "generated pinned definition stats");
        if (result is Minion minion && result.Zone == result.Controller.BoardZone)
        {
            int attack = member.Attack + (minion.Card.Id == "JAIL_311" && minion.Controller.DeckZone.Count >= 25 ? 5 : 0);
            Core2026Proof.Check(minion.AttackDamage == attack && minion.Health == member.Health,
                $"summoned stats {minion.Card.Id}: {minion.AttackDamage}/{minion.Health}, expected {attack}/{member.Health}");
        }
        Core2026Proof.Check(!filter.Type.HasValue || result.Card.Type == filter.Type, "generated type");
        Core2026Proof.Check(!filter.MinCost.HasValue || result.Card.Cost >= filter.MinCost, "generated minimum printed cost");
        Core2026Proof.Check(!filter.MaxCost.HasValue || result.Card.Cost <= filter.MaxCost, "generated maximum printed cost");
        Core2026Proof.Check(!filter.Race.HasValue || result.Card.IsRace(filter.Race.Value), "generated race");
        Core2026Proof.Check(!filter.Keyword.HasValue || result.Card[filter.Keyword.Value] > 0, "generated keyword");
        Core2026Proof.Check(!filter.Rarity.HasValue || result.Card.Rarity == filter.Rarity, "generated rarity");
        if (filter.ClassMode == PoolClassMode.Exact)
            Core2026Proof.Check(member.Classes.Contains(filter.ExactClass.Value.ToString()), "exact generated class");
        if (filter.ClassMode == PoolClassMode.ControllerAndNeutral)
            Core2026Proof.Check(member.Classes.Contains("NEUTRAL") || member.Classes.Contains(source.Controller.HeroClass.ToString()), "Discover class rule");
    }

    internal static void Offers(Game g, IPlayable source, int expected = 3)
    {
        var choice = source.Controller.Choice;
        Core2026Proof.Check(choice != null && choice.Choices.Count == expected && expected <= 3
            && choice.Choices.Select(id => g.IdEntityDic[id].Card.Id).Distinct().Count() == expected, "distinct Discover offers");
        foreach (int id in choice.Choices)
        {
            var result = g.IdEntityDic[id];
            Member(g, source, result, Filter(source.Card.Id));
            Core2026Proof.Check(result.Zone == source.Controller.SetasideZone, "offer entity set aside");
        }
        Core2026Proof.Check(g.GetPendingDecisions().Single().Options.Count() == expected, "all offers available to decision consumers");
    }

    internal static void Choose(Game g, int index = 0)
    {
        var view = g.GetPendingDecisions().Single();
        Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(view.Id, view.ControllerId, new[] { view.Options[index].OptionId }), out var error), error);
        Core2026Proof.Check(!g.Resolution.HasSuspendedEffect && g.Player1.Choice == null && g.Player2.Choice == null, "continuation finishes once");
    }

    internal static void EachAnswer(Game g, Action<Game, int> verify)
    {
        var offers = g.GetPendingDecisions().Single().Options;
        for (int i = 0; i < offers.Count(); i++)
        {
            var branch = g.CloneExact(history: true);
            Choose(branch, i);
            verify(branch, offers[i].OptionId);
        }
        PoolPilotProof.Resume(g, next => Choose(next));
    }

    internal static IPlayable[] HandResults(Game g, IPlayable source, int count, int reduction = 0, bool ones = false)
    {
        var cards = source.Controller.HandZone.Where(c => c[GameTag.DISPLAYED_CREATOR] == source.Id).OrderBy(c => c.Id).ToArray();
        Core2026Proof.Check(cards.Length == count, "generated hand count");
        foreach (var card in cards)
        {
            Member(g, source, card, Filter(source.Card.Id));
            Core2026Proof.Check(card.Zone == source.Controller.HandZone && card.ZonePosition == source.Controller.HandZone.ToList().IndexOf(card), "generated hand zone/position");
            int expectedCost = ones ? 1 : Math.Max(0, card.Card.Cost - reduction - (card.Card.Id == "JAIL_514" ? card.Controller.HandZone.Count : 0));
            Core2026Proof.Check(card.Cost == expectedCost, $"entity cost visible in hand {card.Card.Id}: actual={card.Cost} expected={expectedCost}");
            if (ones) Core2026Proof.Check(card is Minion m && m.AttackDamage == 1 && m.Health == 1, "generated 1/1 stats");
        }
        return cards;
    }

    internal static void HandLimits(Game g, Controller p, string id, int picks, int reduction = 0, bool death = false, bool ones = false)
    {
        var source = Entity.FromCard(p, Cards.FromId(id));
        foreach (int count in new[] { 0, 1 }) WithPool(g, id, count, () => {
            PoolPilotProof.EmptyHand(p);
            long before = g.Random.DrawCount;
            PoolPilotProof.Resolve(g, source, death: death);
            Core2026Proof.Check(g.Random.DrawCount == before + (count == 0 ? 0 : picks), "empty/singleton random pool draws");
            var generated = HandResults(g, source, count == 0 ? 0 : picks, reduction, ones);
            Core2026Proof.Check(count == 0 || generated.Select(c => c.Card.Id).Distinct().Count() == 1, "independent picks allow duplicates");
        });
        // Singleton pool: a random Shatter parent would burn its right half too (ShatterRulesProof owns that count).
        foreach (int filled in new[] { 9, 10 }) WithPool(g, id, 1, () => {
            PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p, filled);
            long before = g.Random.DrawCount; int graveyard = p.GraveyardZone.Count;
            PoolPilotProof.Resolve(g, source, death: death);
            Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount == before + picks
                && p.GraveyardZone.Count == graveyard + picks - (10 - filled), "hand capacity burns overflow and consumes all picks");
            HandResults(g, source, 10 - filled, reduction, ones);
        });
        PoolPilotProof.EmptyHand(p);
    }

    internal static Minion[] SummonedResults(Game g, IPlayable source, string recipe, int count, int position)
    {
        var cards = source.Controller.BoardZone.Where(c => c[GameTag.DISPLAYED_CREATOR] == source.Id).OrderBy(c => c.Id).ToArray();
        Core2026Proof.Check(cards.Length == count, "random summon count");
        for (int i = 0; i < cards.Length; i++)
        {
            Member(g, source, cards[i], Filter(recipe));
            Core2026Proof.Check(cards[i].ZonePosition == position + i && source.Controller.BoardZone.GetSlot(position + i).Id == cards[i].Id,
                "physical summon position in creation order");
        }
        Core2026Proof.Check(source.Controller.BoardZone.SlotCount <= 7, "seven-slot board capacity");
        return cards;
    }

    // Test-only manifest injection follows PoolPilotProof; no shared Card or admission changes.
    internal static void WithPool(Game g, string source, int count, Action action, int? cost = null)
    {
        var installed = g.OfflinePools;
        var filter = cost.HasValue ? Filter(source).WithCost(cost.Value) : Filter(source);
        var candidates = OfflineCardPools.Candidates(g, g.Player1.HeroClass, filter, Cards.FromId(source).AssetId);
        var xml = OfflinePoolFixtures.Catalog();
        var rows = xml.Root.Element("Members");
        var selected = rows.Elements("Card").Where(r => candidates.Take(count).Any(c => c.Id == (string)r.Attribute("cardId"))).ToArray();
        if (count == 0)
        {
            selected = rows.Elements("Card").Take(1).ToArray();
            selected[0].SetAttributeValue("scope", "Standard");
            selected[0].SetAttributeValue("set", "CORE");
            selected[0].SetAttributeValue("type", filter.Type == CardType.WEAPON ? "MINION" : "WEAPON");
            selected[0].Elements("Class").Remove();
            selected[0].Add(new XElement("Class", new XAttribute("name", "NEUTRAL")));
            selected[0].SetAttributeValue("cost", 999);
        }
        rows.ReplaceNodes(selected);
        var manifest = OfflinePoolFixtures.Manifest(xml);
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, g.Player1.HeroClass, filter, Cards.FromId(source).AssetId).Count == count, "fixture candidate count");
        var property = typeof(Game).GetProperty(nameof(Game.OfflinePools));
        try { property.SetValue(g, manifest); action(); }
        finally { property.SetValue(g, installed); }
    }
}
