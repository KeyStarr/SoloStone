using System.IO.Compression;
using System.Text.Json;
using HsSim;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

namespace CoreLegacy;

// Tier D: pinned export 37.0.0.254477. Tier A: Blizzard 15.2 (no class weighting),
// https://hearthstone.blizzard.com/en-gb/news/23097372 (15.2); source exclusion:
// https://us.forums.blizzard.com/en/hearthstone/t/1801-server-hotfix/39314 (18.0.1).
internal static class CorePoolProof
{
    // The printed rows are the independent oracle of these pool proofs: they are compared in the client tier only (ClientExport).
    private static IReadOnlyDictionary<string, JsonElement> Printed => ClientExport.RequireCards();

    internal static void Generated(Game game, Controller player, string sourceId, IPlayable output, int? cost = null)
    {
        if (ClientExport.Oracle && sourceId is "CORE_KAR_057" or "CORE_KAR_062" or "CORE_LOE_039" or "CORE_GIL_836")
        {
            // These Discover cards use the controller's class or Neutral, regardless of the production filter.
            var printedClasses = Printed[output.Card.Id].GetProperty("cardClasses").EnumerateArray().Select(x => x.GetString());
            Core2026Proof.Check(printedClasses.Contains(player.HeroClass.ToString()) || printedClasses.Contains("NEUTRAL"),
                sourceId + " printed controller-class/Neutral Discover domain: " + output.Card.Id);
        }
        var declaration = OfflinePoolDeclarations.Cards.Single(d => d.CardId == sourceId);
        var requirement = declaration.Pools.Single();
        var filter = cost.HasValue ? requirement.Filter.WithCost(cost.Value) : requirement.Filter;
        var candidates = OfflineCardPools.Candidates(game, player.HeroClass, filter, Cards.FromId(sourceId).AssetId);
        Core2026Proof.Check(candidates.Count >= 3 && candidates.All(c => c.Id != sourceId), sourceId + " admitted pool >=3; source excluded");
        Core2026Proof.Check(candidates.Any(c => c.Id == output.Card.Id), sourceId + " generates admitted identity: " + output.Card.Id);
        foreach (var card in candidates)
        {
            if (!ClientExport.Oracle) { ClientExport.Skip("Core pool members against the printed client export (class, type, cost, race, rarity, keyword filters)"); continue; }
            var data = Printed[card.Id];
            Core2026Proof.Check(card.AssetId == data.GetProperty("dbfId").GetInt32() && card.Cost == data.GetProperty("cost").GetInt32()
                && card.ATK == data.GetProperty("atk").GetInt32() && card.Health == data.GetProperty("health").GetInt32()
                && card.Text == data.GetProperty("text").GetString(), "pool member matches pinned identity/cost/stats/text: " + card.Id);
            var classes = data.GetProperty("cardClasses").EnumerateArray().Select(x => x.GetString()).ToArray();
            string hero = player.HeroClass.ToString();
            Core2026Proof.Check(filter.ClassMode == PoolClassMode.Any
                || filter.ClassMode == PoolClassMode.ControllerAndNeutral && (classes.Contains(hero) || classes.Contains("NEUTRAL"))
                || filter.ClassMode == PoolClassMode.OtherClass && !classes.Contains(hero) && !classes.Contains("NEUTRAL")
                || filter.ClassMode == PoolClassMode.Exact && classes.Contains(filter.ExactClass.Value.ToString()), "printed class filter");
            Core2026Proof.Check(!filter.Type.HasValue || card.Type == filter.Type.Value && data.GetProperty("cardType").GetString() == filter.Type.Value.ToString(), "printed type filter");
            Core2026Proof.Check((!filter.MinCost.HasValue || card.Cost >= filter.MinCost) && (!filter.MaxCost.HasValue || card.Cost <= filter.MaxCost), "printed cost filter");
            if (filter.Race.HasValue)
                Core2026Proof.Check(card.IsRace(filter.Race.Value) && data.GetProperty("races").EnumerateArray().Any(x => x.GetString() == filter.Race.Value.ToString() || x.GetString() == "ALL"), "printed race filter");
            Core2026Proof.Check(!filter.Rarity.HasValue || data.GetProperty("rarity").GetString() == filter.Rarity.Value.ToString(), "printed rarity filter");
            if (filter.Keyword.HasValue)
                Core2026Proof.Check(card[filter.Keyword.Value] > 0 && data.GetProperty("tags").TryGetProperty(((int)filter.Keyword.Value).ToString(), out var tag) && tag.GetInt32() > 0, "printed keyword filter");
        }
        var report = OfflinePoolAdmission.Evaluate(game.OfflinePools, new[] { declaration });
        Core2026Proof.Check(report.Cards.Single().Registered, "pool recipe registered");
        report.ValidateRegistered();
    }

    internal static void Offers(Game game, Controller player, string sourceId)
    {
        Core2026Proof.Check(player.Choice != null && player.Choice.Choices.Count == 3, "three offers");
        var offers = player.Choice.Choices.Select(key => game.IdEntityDic[key]).ToArray();
        Core2026Proof.Check(offers.Select(o => o.Card.Id).Distinct().Count() == 3, "three distinct card identities");
        foreach (var offer in offers) Generated(game, player, sourceId, offer);
    }

    // Play the same recipe as Rogue then Mage in one process, including actual choice resolution.
    internal static void CrossClass(Game current, string id, Action<Controller> prepare = null, bool attack = false)
    {
        long seed = current.Seed;
        var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            Player1HeroClass = CardClass.ROGUE, Player2HeroClass = CardClass.MAGE, History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), FormatType = FormatType.FT_STANDARD });
        game.StartGame();
        foreach (var player in new[] { game.Player1, game.Player2 }) PoolPilotProof.EmptyHand(player);
        foreach (var player in new[] { game.Player1, game.Player2 })
        {
            prepare?.Invoke(player);
            var held = Add(player, id); var clone = game.CloneExact(history: true);
            foreach (var next in new[] { game, clone })
            {
                var owner = next.ControllerById(player.Id);
                PlayEntity(next, owner, next.IdEntityDic[held.Id]);
                if (attack) Attack(next, (Minion)next.IdEntityDic[held.Id], owner.Opponent.Hero);
                if (owner.Choice != null)
                {
                    Offers(next, owner, id);
                    int key = owner.Choice.Choices[0];
                    CoreCostProof.Answer(next, key);
                    Generated(next, owner, id, next.IdEntityDic[key]);
                }
                else Generated(next, owner, id, owner.HandZone.Last());
                Core2026Proof.Check(owner.Opponent.HandZone.All(c => c[GameTag.DISPLAYED_CREATOR] != held.Id), "opponent gains no generated card");
            }
            Core2026Proof.Check(Canonical.State(game) == Canonical.State(clone), "same seed clone produces same offers/pick");
            End(game);
        }
    }
}
