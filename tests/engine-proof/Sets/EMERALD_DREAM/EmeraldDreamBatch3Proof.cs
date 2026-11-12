using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Kettle;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EmeraldDreamBatch3Proof
{
    internal static void UsePower(Game game, Controller player)
    {
        player.BaseMana = 10; player.UsedMana = 0;
        var task = player.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER);
        Core2026Proof.Check(game.Process(task), "native Hero Power accepted");
    }
    internal static void EmptyDeck(Controller player)
    {
        foreach (var card in player.DeckZone.ToArray()) { player.DeckZone.Remove(card); player.SetasideZone.Add(card); }
    }
    internal static IPlayable InDeck(Controller player, string id)
    {
        var card = Entity.FromCard(player, Cards.FromId(id)); player.DeckZone.Add(card); return card;
    }
    internal static void Cost(IPlayable card, int expected)
        => Core2026Proof.Check(card.Cost == expected && card[GameTag.COST] == expected, "derived COST tag equals " + expected);
    internal static void PublishedCost(IPlayable card, int expected)
        => Core2026Proof.Check(card.Game.PowerHistory.Full.OfType<PowerHistoryTagChange>()
            .Last(change => change.EntityId == card.Id && change.Tag == GameTag.COST).Value == expected,
            "published COST equals " + expected);
    internal static void CopyCost(IPlayable card, int expected)
    {
        var clone = card.Game.CloneExact(history: true); var original = clone.IdEntityDic[card.Id]; var player = original.Controller;
        if (player.HandZone.IsFull) {
            var other = player.HandZone.First(c => c.Id != original.Id);
            player.HandZone.Remove(other); player.SetasideZone.Add(other);
        }
        var copy = Generic.Copy(player, original, original, Zone.HAND);
        Core2026Proof.Check(copy.Id != original.Id && copy.Cost == expected, card.Card.Id + " copy retains permanent discount: expected " + expected + ", got " + copy.Cost);
        Cost(copy, expected); PublishedCost(copy, expected); Cost(card, expected);
    }
    internal static void Battlecry(Game game, Controller player, Minion source)
    {
        source.Card.Power.PowerTask.Process(game, player, source, null, new TaskStack());
        game.ProcessTasks(); game.DeathProcessingAndAuraUpdate();
    }
}
