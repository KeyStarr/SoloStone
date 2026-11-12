using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class StoryOfTheWaygateProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_364", (g, p, id) => {
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory, "Waygate declares consumer");
        var original = p.DeckZone[0]; Generic.Draw(p, original);
        var generated = Add(p, original.Card.Id);
        var enemy = Add(p.Opponent, original.Card.Id);
        var inDeck = Entity.FromCard(p, Cards.FromId(original.Card.Id)); p.DeckZone.Add(inDeck);
        Play(g, p, id);
        Core2026Proof.Check(original.Cost == 4 && generated.Cost == 3 && generated[GameTag.COST] == 3
            && enemy.Cost == 4 && inDeck.Cost == 4, "only foreign own current hand discounted; identical IDs distinguish origin");
        Generic.ChangeEntityBlock(p, original, Cards.FromId("CS2_200"), false);
        Generic.ChangeEntityBlock(p, generated, Cards.FromId("CS2_200"), false);
        original = g.IdEntityDic[original.Id]; generated = g.IdEntityDic[generated.Id];
        int originalCost = original.Cost, generatedCost = generated.Cost;
        End(g); End(g);
        var clone = g.Clone(); Play(clone, clone.Player1, id);
        Core2026Proof.Check(clone.IdEntityDic[original.Id].Cost == originalCost
            && clone.IdEntityDic[generated.Id].Cost == generatedCost - 1 && generated.Cost == generatedCost,
            "transform retains physical origin across turns; clone reduction isolated");
        var late = Add(p, "CS2_200"); Core2026Proof.Check(late.Cost == 6, "future hand entries not discounted retroactively");
        var coin = Add(p, "GAME_005"); Play(g, p, id);
        Core2026Proof.Check(coin.Cost == 0 && generated.Cost == generatedCost - 1 && late.Cost == 5, "zero floor and repeated discount");
        var fair = FairBotState.Create(g, p.PlayerId, 71, DeckStyle.Control);
        var projected = HistoryFairOriginProof.FairGame(fair);
        Play(projected, projected.Player1, id);
        Core2026Proof.Check(projected.IdEntityDic[original.Id].Cost == originalCost
            && projected.IdEntityDic[late.Id].Cost == 4, "own visible origins usable in fair search");
    });
}
