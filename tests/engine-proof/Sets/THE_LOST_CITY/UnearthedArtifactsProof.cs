using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class UnearthedArtifactsProof
{
    private static void Discover(Game g, Controller p)
    {
        new EffectContext(g, p.Id, p.Hero.Id).Discover(new[] { Cards.FromId("GAME_005") });
        Generic.ChoicePick(p, g, p.Choice.Choices[0]); g.DeathProcessingAndAuraUpdate();
    }
    private static void Cast(Game g, Controller p, string id, int cost, bool full = false)
    {
        var before = p.BoardZone.Slots.Select(e => e.Id).ToArray();
        var filter = OfflinePoolDeclarations.Cards.Single(c => c.CardId == id).Pools.Single(r => r.Filter.MinCost == cost).Filter;
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, filter, Cards.FromId(id).AssetId);
        Core2026Proof.Check(candidates.Count >= 3 && candidates.All(c => c.Type == CardType.MINION && c.Cost == cost && c.Id != id), "admitted exact-cost any-class filter");
        var source = Play(g, p, id);
        var made = p.BoardZone.Where(e => !before.Contains(e.Id)).ToArray();
        Core2026Proof.Check(made.Length == (full ? 0 : 1) && p.BoardZone.SlotCount <= 7, "exactly one summon unless physical board full");
        if (full) return;
        var m = made.Single(); var member = g.OfflinePools.Standard.Single(c => c.CardId == m.Card.Id);
        Core2026Proof.Check(candidates.Any(c => c.Id == m.Card.Id) && m.Card.Type == CardType.MINION && m.Card.Cost == cost
            && m.Card.ATK == member.Attack && m.Card.Health == member.Health && m.Controller == p && m[GameTag.DISPLAYED_CREATOR] == source.Id,
            "generated identity/stats/type/cost/owner/creator match pinned catalog");
        Core2026Proof.Check(m.Health == m.Card.Health && m.AttackDamage == m.Card.ATK + (m.Card.Id == "JAIL_311" && p.DeckZone.Count >= 25 ? 5 : 0), "summoned stats and own aura");
        Core2026Proof.Check(p.BoardZone.GetSlot(p.BoardZone.SlotCount - 1) == m, "spell summons at physical far right");
    }
    internal static void Run() => StandardSetsProof.Case("TLC_462", (g, p, id) => {
        // Tier D: pinned text; completed own semantic Discover uses history R8, shared with TLC_365.
        p.AddHeroAndPower(Cards.HeroCard(CardClass.MAGE));
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory, "history consumer declared");
        PoolPilotProof.Resume(g, next => Cast(next, next.Player1, id, 2)); ParkBoard(p);
        var ctx = new EffectContext(g, p.Id, p.Hero.Id);
        Core2026Proof.Check(!ctx.Discover(Array.Empty<Card>()), "empty pool opens no Discover");
        Generic.CreateChoiceCards(p, p.Hero, null, ChoiceType.GENERAL, ChoiceAction.HAND, new[] { Cards.FromId("GAME_005") }, null);
        Generic.ChoicePick(p, g, p.Choice.Choices[0]); Cast(g, p, id, 2); ParkBoard(p);
        Discover(g, p.Opponent); Cast(g, p, id, 2); ParkBoard(p);
        ctx.Discover(new[] { Cards.FromId("GAME_005") });
        Core2026Proof.Check(!ctx.History.Discoveries(ctx.History.ThisTurn).Any(f => f.ControllerId == p.Id), "pending choice is not a completed own Discover");
        PoolPilotProof.Resume(g, next => { Generic.ChoicePick(next.Player1, next, next.Player1.Choice.Choices[0]); Cast(next, next.Player1, id, 4); });
        ParkBoard(p); var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182");
        PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p, 9); Cast(g, p, id, 4);
        Core2026Proof.Check(p.BoardZone.GetSlot(0) == location && p.BoardZone.GetSlot(1) == left && p.HandZone.Count == 9, "mixed slot order and full hand do not prevent summon");
        ParkBoard(p); PoolPilotProof.EmptyHand(p); End(g); End(g); Cast(g, p, id, 2); ParkBoard(p);
        // Existing legacy Discover also produces the shared semantic history fact.
        new DiscoverTask(CardType.MINION).Process(g, p, p.Hero, null); Generic.ChoicePick(p, g, p.Choice.Choices[0]);
        Cast(g, p, id, 4); ParkBoard(p); End(g); End(g);
        PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p); Discover(g, p);
        PoolPilotProof.EmptyHand(p); Cast(g, p, id, 4); ParkBoard(p);
        LostCityPoolProof.FullBoard(p); Cast(g, p, id, 4, full: true); ParkBoard(p);
        End(g); End(g); LostCityPoolProof.FullBoard(p); Cast(g, p, id, 2, full: true);
    });
}
