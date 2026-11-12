using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;
using static VioletHoldPoolProof;

internal static class HexmarshalProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_806", (g, p, id) => {
        // Starting definitions are preserved in Controller.DeckCards (Model/Game.cs), independent of live deck.
        HandLimits(g, p, id, 1, reduction: 5);
        ClassDeck(p); p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_029")));
        var expected = OfflineCardPools.Pick(g.CloneExact(), VioletHoldPoolProof.Filter(id), p.HeroClass, Cards.FromId(id).AssetId);
        IPlayable source = null;
        Resume(g, next => { source = Play(next, next.Player1, id); });
        var result = HandResults(g, source, 1, reduction: 5).Single();
        Core2026Proof.Check(result.Card.Id == expected.Id, "seeded expensive spell, current spell in deck does not block discount");
        Resume(g, next => Core2026Proof.Check(next.IdEntityDic[result.Id].Cost == Math.Max(0, result.Card.Cost - 5 - (result.Card.Id == "JAIL_514" ? p.HandZone.Count : 0)), "discount clone and snapshot"));
        Core2026Proof.Check(Entity.FromCard(p, result.Card).Cost == result.Card.Cost, "discount is not a shared Card mutation");
        EmptyHand(p); ClearBoard(g, p); ClassDeck(p);
        var other = g.CloneExact(history: true);
        other.Player1.DeckCards.Add(Cards.FromId("CS2_029"));
        source = Play(other, other.Player1, id);
        HandResults(other, source, 1);
        Core2026Proof.Check(other.Player1.DeckZone.IsEmpty, "starting spell blocks discount even after live deck emptied");
        source = Play(g, p, id); HandResults(g, source, 1, reduction: 5);
        Core2026Proof.Check(!p.DeckCards.Any(c => c.Type == CardType.SPELL), "clone starting definitions stay independent");
    });
}
