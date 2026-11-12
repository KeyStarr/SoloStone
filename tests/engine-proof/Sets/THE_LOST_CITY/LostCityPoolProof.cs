using System.Xml.Linq;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class LostCityPoolProof
{
    internal static IPlayable Start(Game g, Controller p, string id)
    {
        var source = Add(p, id);
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]));
        return source;
    }
    internal static void TinyPools(Game g, string id)
    {
        var candidates = OfflineCardPools.Candidates(g, g.Player1.HeroClass, Filter(id), Cards.FromId(id).AssetId).Take(3).Select(c => c.Id).ToArray();
        foreach (int count in new[] { 2, 3 })
        {
            var copy = g.CloneExact(history: true);
            WithPool(copy, id, candidates.Take(count).ToArray(), () => {
                var rng = copy.Random.CaptureState(); var source = Play(copy, copy.Player1, id);
                var offers = Offers(copy, copy.Player1, source, _ => true);
                Core2026Proof.Check(offers.Length == count && copy.Random.CaptureState().Equals(rng), "tiny pool offers all without RNG");
                Answer(copy, offers[0]);
                Core2026Proof.Check(copy.Player1.Choice == null && !copy.Resolution.HasSuspendedEffect, "tiny pool resumes to completion");
            });
        }
    }
    internal static OfflinePoolFilter Filter(string id) => OfflinePoolDeclarations.Cards.Single(c => c.CardId == id).Pools.Single().Filter;
    internal static int[] Offers(Game g, Controller p, IPlayable source, Func<Card, bool> matches)
    {
        var offers = p.Choice.Choices.ToArray();
        var pool = OfflineCardPools.Candidates(g, p.HeroClass, Filter(source.Card.Id), source.Card.AssetId);
        Core2026Proof.Check(offers.Length == Math.Min(3, pool.Count) && offers.Distinct().Count() == offers.Length &&
            offers.Select(i => g.IdEntityDic[i].Card.Id).Distinct().Count() == offers.Length, "three distinct pool offers");
        foreach (int id in offers)
        {
            var offer = g.IdEntityDic[id];
            Core2026Proof.Check(pool.Any(c => c.Id == offer.Card.Id) && matches(offer.Card) && offer.Card.Id != source.Card.Id,
                "offer belongs to declared filter and excludes source");
            Core2026Proof.Check(offer.Controller == p && offer.Zone == p.SetasideZone && offer[GameTag.DISPLAYED_CREATOR] == source.Id,
                "offer identity, owner, setaside zone and creator");
            Core2026Proof.Check(g.OfflinePools.Standard.Single(c => c.CardId == offer.Card.Id).Classes.Contains(p.HeroClass.ToString()) || offer.Card.Class == CardClass.NEUTRAL || Filter(source.Card.Id).ClassMode == PoolClassMode.Any,
                "Discover class rule");
        }
        var search = FairBotState.Create(g, p.PlayerId, 901, DeckStyle.Value);
        Core2026Proof.Check(search.CanExpand && search.Actions.Length == offers.Length, "bot enumerates every offer");
        foreach (var action in search.Actions) Core2026Proof.Check(search.Apply(action) != null, "bot answers each offer");
        return offers;
    }
    internal static void WithPool(Game g, string id, string[] members, Action action, string[] dependencies = null)
    {
        var installed = g.OfflinePools;
        var retained = members.Concat(dependencies ?? Array.Empty<string>()).ToArray();
        var xml = OfflinePoolFixtures.FixtureCatalog(retained); var rows = xml.Root.Element("Members");
        rows.ReplaceNodes(rows.Elements("Card").Where(c => retained.Contains((string)c.Attribute("cardId"))).ToArray());
        // Empty manifest is invalid; retain one nonmatching Past member for an empty Standard fixture.
        if (members.Length == 0) rows.Add(OfflinePoolFixtures.Catalog().Root.Element("Members").Elements("Card").First(c => (string)c.Attribute("scope") == "Past"));
        var manifest = OfflinePoolFixtures.Manifest(xml);
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, g.Player1.HeroClass, Filter(id), Cards.FromId(id).AssetId).Count == members.Length,
            "hand-selected real pool fixture");
        var property = typeof(Game).GetProperty(nameof(Game.OfflinePools));
        try { property.SetValue(g, manifest); action(); }
        finally { property.SetValue(g, installed); }
    }
    internal static void Answer(Game g, int chosen) => PlayerDecisionProof.Answer(g, chosen);
    internal static void FullBoard(Controller p)
    {
        p.BoardZone.Add((BoardObject)Entity.FromCard(p, Cards.FromId("CORE_REV_990")));
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count == 6, "full board includes location slot");
    }
    internal static void Empty(Game g, Controller p, string id)
    {
        WithPool(g, id, Array.Empty<string>(), () => {
            var rng = g.Random.CaptureState(); int board = p.BoardZone.SlotCount;
            Play(g, p, id);
            Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect && p.HandZone.Count == 0 && p.BoardZone.SlotCount == board &&
                g.Random.CaptureState().Equals(rng), "empty pool finishes with no choice, output or RNG");
        });
    }
}
