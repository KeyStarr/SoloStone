using System.Xml.Linq;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class TimeTravelPoolProof
{
    internal static OfflinePoolFilter Filter(string id) => OfflinePoolDeclarations.Cards.Single(c => c.CardId == id).Pools.Single().Filter;

    internal static IPlayable[] Offers(Game game, IPlayable source)
    {
        var player = source.Controller;
        var offers = player.Choice.Choices.Select(id => game.IdEntityDic[id]).ToArray();
        var candidates = OfflineCardPools.Candidates(game, player.HeroClass, Filter(source.Card.Id), source.Card.AssetId);
        Core2026Proof.Check(offers.Length is > 0 and <= 3 && offers.Select(c => c.Card.Id).Distinct().Count() == offers.Length,
            "at most three distinct Discover offers");
        Core2026Proof.Check(offers.All(c => c.Zone.Type == Zone.SETASIDE && c.Controller == player && c.Id != source.Id &&
            c.Card.Id != source.Card.Id && c[GameTag.CREATOR] == source.Id &&
            candidates.Any(member => member.AssetId == c.Card.AssetId)), "offers from declared pool, source excluded, owner/creator/zone");
        foreach (var offer in offers)
        {
            var member = game.OfflinePools.Standard.Single(c => c.CardId == offer.Card.Id);
            Core2026Proof.Check(offer.Card.Type == CardType.MINION && member.Type == "MINION" &&
                (member.Classes.Contains("NEUTRAL") || member.Classes.Contains(player.HeroClass.ToString())) &&
                offer.Card.Cost == member.Cost && offer.Card.ATK == member.Attack && offer.Card.Health == member.Health,
                "every minion offer matches printed class/type/cost/stats in pinned catalog");
        }
        return offers;
    }

    internal static int Answer(Game game, IPlayable source, int index = 0, bool resume = true)
    {
        var offers = Offers(game, source);
        var decision = game.GetPendingDecisions().Single();
        var answer = new DecisionAnswer(decision.Id, decision.ControllerId, new[] { offers[index % offers.Length].Id });
        void Accept(Game next) => Core2026Proof.Check(next.TryAnswer(answer, out var error), "Discover answer: " + error);
        if (resume) PoolPilotProof.Resume(game, Accept); else Accept(game);
        return answer.SelectedOptionIds.Single();
    }

    internal static void BotAnswers(Game game)
    {
        byte[] before = game.SaveSnapshot();
        var bot = FairBotState.Create(game, game.CurrentPlayer.PlayerId, 901, DeckStyle.Value);
        Core2026Proof.Check(bot.CanExpand && bot.Actions.Length == game.CurrentPlayer.Choice.Choices.Count, "bot sees each Discover offer");
        foreach (var action in bot.Actions) Core2026Proof.Check(bot.Apply(action) != null, "bot can answer each offer");
        Core2026Proof.Check(before.SequenceEqual(game.SaveSnapshot()), "bot branches leave authoritative game unchanged");
    }

    // Only transient shortage fixtures use an injected manifest; snapshots always use the installed manifest.
    internal static void WithPool(Game game, string sourceId, int count, Action action)
    {
        var installed = game.OfflinePools;
        var filter = Filter(sourceId);
        var candidates = OfflineCardPools.Candidates(game, game.Player1.HeroClass, filter, Cards.FromId(sourceId).AssetId);
        var xml = OfflinePoolFixtures.Catalog();
        var rows = xml.Root.Element("Members");
        var selected = rows.Elements("Card").Where(r => candidates.Take(count).Any(c => c.Id == (string)r.Attribute("cardId"))).ToArray();
        if (count == 0)
        {
            selected = rows.Elements("Card").Take(1).ToArray();
            selected[0].SetAttributeValue("scope", "Standard");
            selected[0].SetAttributeValue("set", "CORE");
            selected[0].SetAttributeValue("type", "WEAPON");
            selected[0].Elements("Class").Remove();
            selected[0].Add(new XElement("Class", new XAttribute("name", "NEUTRAL")));
        }
        rows.ReplaceNodes(selected);
        var manifest = OfflinePoolFixtures.Manifest(xml);
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, game.Player1.HeroClass, filter, Cards.FromId(sourceId).AssetId).Count == count,
            "shortage fixture candidate count");
        var property = typeof(Game).GetProperty(nameof(Game.OfflinePools));
        try { property.SetValue(game, manifest); action(); }
        finally { property.SetValue(game, installed); }
    }

    internal static void ClearQuietly(Game game, Controller player)
    {
        foreach (var minion in player.BoardZone.ToArray())
            if (minion.IsDormant) player.SetasideZone.Add(player.BoardZone.Remove(minion));
            else minion.Silence();
        StandardSetsProof.ClearBoard(game, player);
        foreach (var location in player.BoardZone.GetLocations().ToArray()) location.Destroy();
        game.DeathProcessingAndAuraUpdate();
    }
    internal static void RandomGenerated(Game game, IPlayable source, IEnumerable<IPlayable> outputs,
        int expected, Controller owner = null, int discount = 0)
    {
        owner ??= source.Controller;
        var filter = Filter(source.Card.Id);
        var candidates = OfflineCardPools.Candidates(game, source.Controller.HeroClass, filter, source.Card.AssetId);
        var cards = outputs.ToArray();
        Core2026Proof.Check(cards.Length == expected && cards.Select(c => c.Id).Distinct().Count() == expected,
            "random output count and distinct entity ids");
        foreach (var entity in cards)
        {
            var member = game.OfflinePools.Standard.Single(c => c.CardId == entity.Card.Id);
            Core2026Proof.Check(candidates.Any(c => c.AssetId == entity.Card.AssetId) && entity.Card.Id != source.Card.Id &&
                entity.Controller == owner && (entity.Zone.Type == Zone.PLAY ? entity.Zone == owner.BoardZone : entity.Zone == owner.HandZone),
                "declared pool, source exclusion, controller and zone");
            Core2026Proof.Check(entity.Card.Type == (CardType)Enum.Parse(typeof(CardType), member.Type) &&
                entity.Card.Cost == member.Cost && entity.Card.ATK == member.Attack && entity.Card.Health == member.Health,
                "generated pinned identity/type/cost/stats");
            Core2026Proof.Check(source.Card.Id != "TIME_872" || (entity.Card.Type == CardType.MINION && entity.Card.Cost == 1 && member.Cost == 1),
                "Undefeated Champion summons a printed 1-Cost minion independently of the declared filter");
            Core2026Proof.Check(!filter.Type.HasValue || entity.Card.Type == filter.Type, "random type filter");
            Core2026Proof.Check((!filter.MinCost.HasValue || member.Cost >= filter.MinCost) &&
                (!filter.MaxCost.HasValue || member.Cost <= filter.MaxCost), "random printed-cost filter");
            Core2026Proof.Check(!filter.Rarity.HasValue || entity.Card.Rarity == filter.Rarity, "random rarity filter");
            Core2026Proof.Check(!filter.Keyword.HasValue || entity.Card[filter.Keyword.Value] > 0, "random keyword filter");
            Core2026Proof.Check(source.Card.Id != "END_029" || (entity.Card.Type == CardType.SPELL &&
                entity.Card[GameTag.SPELL_SCHOOL] == 6 && member.SpellSchool == "SHADOW"),
                "Voodoo Totem generates a Shadow spell independently of the declared filter");
            if (entity is Minion minion && (expected == 1 || source.Card.Id == "TIME_872"))
            {
                int attack = member.Attack;
                if (entity.Card.Id == "CORE_EX1_509" && entity.Zone.Type == Zone.PLAY)
                    attack += cards.Count(other => other.Id > entity.Id && other.Card.IsRace(Race.MURLOC));
                int manaDelta = CostRules.HasManaNumbers(entity) && entity.Zone == owner.HandZone ? CostRules.ManaNumbers(entity) - member.Cost : 0;
                Core2026Proof.Check(minion.AttackDamage == attack + manaDelta && minion.Health == member.Health + manaDelta,
                    "generated live stats include later Murloc Tidecaller triggers");
            }
            Core2026Proof.Check(entity[GameTag.DISPLAYED_CREATOR] == source.Id, "runtime creator");
            if (entity.Zone.Type == Zone.HAND)
            {
                Core2026Proof.Check(entity.ZonePosition == owner.HandZone.ToList().FindIndex(c => c.Id == entity.Id), "hand position");
                // Void Metal also has its own hand-size discount.
                int cost = (CostRules.HasManaNumbers(entity) ? CostRules.ManaNumbers(entity) : entity.Card.Cost) - discount;
                if (entity.Card.Id == "JAIL_514") cost -= owner.HandZone.Count;
                if (entity.Card.Id == "CATA_616") cost -= CostRules.LastPlayedCost(owner);
                Core2026Proof.Check(entity.Cost == Math.Max(0, cost) && entity[GameTag.COST] == entity.Cost, "visible hand cost " + entity.Card.Id);
            }
        }
    }

    internal static void RandomBoundaries(Game game, Controller player, string id, Action<IPlayable> resolve, int discount = 0)
    {
        var source = Entity.FromCard(player, Cards.FromId(id));
        PoolPilotProof.EmptyHand(player);
        WithPool(game, id, 0, () => {
            var random = game.Random.CaptureState(); int entities = game.IdEntityDic.Count;
            resolve(source);
            Core2026Proof.Check(player.HandZone.Count == 0 && game.IdEntityDic.Count == entities &&
                game.Random.CaptureState().Equals(random), "empty random pool has no entities or RNG");
        });
        WithPool(game, id, 1, () => {
            resolve(source);
            RandomGenerated(game, source, player.HandZone, 1, discount: discount);
            resolve(source);
            RandomGenerated(game, source, player.HandZone, 2, discount: discount);
            Core2026Proof.Check(player.HandZone.Select(c => c.Card.Id).Distinct().Count() == 1, "picks with replacement");
        });
        PoolPilotProof.EmptyHand(player); PoolPilotProof.FillHand(player, 9);
        resolve(source); Core2026Proof.Check(player.HandZone.Count == 10, "last hand slot filled");
        long draws = game.Random.DrawCount;
        resolve(source); Core2026Proof.Check(player.HandZone.Count == 10 && game.Random.DrawCount == draws + 1,
            "full hand burns result but still consumes random pick");
        PoolPilotProof.EmptyHand(player);
    }

}
