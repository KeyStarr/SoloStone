using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class CoreTradeProof
{
    internal static void Verify(Game game, Controller player, string id)
    {
        var card = Cards.FromId(id);
        Core2026Proof.Check(card[GameTag.TRADEABLE] == 1 && card[GameTag.DECK_ACTION_COST] == 1, "pinned client Tradeable and cost tags");
        Summon(player.Opponent, id == "CORE_EX1_002" ? "CS2_179" : "CS2_186");
        var source = Add(player, id);
        player.BaseMana = 1; player.UsedMana = 0; player.TemporaryMana = 0;
        var tasks = player.Options();
        Core2026Proof.Check(tasks.Any(t => t is TradeCardTask && t.Source == source)
            && !tasks.Any(t => t is PlayCardTask && t.Source == source), "trade available below printed play cost");
        var wire = PowerOptionsBuilder.AllOptions(game, tasks);
        Core2026Proof.Check(wire.PowerOptionList.Any(o => o.MainOption?.EntityId == source.Id
            && o.MainOption.PowerKeyword == (int)GameTag.TRADEABLE && o.MainOption.Targets == null), "native trade keyword and no target");
        player.BaseMana = 10;
        tasks = player.Options();
        Core2026Proof.Check(tasks.Any(t => t is PlayCardTask && t.Source == source)
            && tasks.Any(t => t is TradeCardTask && t.Source == source), "play and trade both offered");
        int hand = player.HandZone.Count, deck = player.DeckZone.Count, draws = player.NumCardsDrawnThisTurn;
        int played = player.NumCardsPlayedThisTurn, spent = player.TotalManaSpentThisGame;
        int[] board = player.BoardZone.Concat(player.Opponent.BoardZone).Select(m => m.Id).ToArray();
        int[] order = player.DeckZone.Select(c => c.Id).ToArray();
        int replacement = player.DeckZone.TopCard.Id;
        var clone = game.CloneExact(history: true);
        var rng = (Util.DeepCloneableRandom)typeof(Game).GetProperty("Random",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(game);
        long cursor = rng.DrawCount;
        int insertion = rng.Clone().Next(deck + 1);
        Core2026Proof.Check(game.Process(TradeCardTask.Any(player, source)), "Core trade accepted");
        Core2026Proof.Check(rng.DrawCount == cursor + 1 && player.DeckZone.ToList().IndexOf(source) == Math.Min(insertion, deck - 1),
            "shuffle consumes exactly one seeded game RNG draw");
        Core2026Proof.Check(clone.Process(TradeCardTask.Any(clone.Player1, (IPlayable)clone.IdEntityDic[source.Id])), "exact clone trade accepted");
        Core2026Proof.Check(Canonical.State(game) == Canonical.State(clone), "trade uses cloneable game RNG");
        Core2026Proof.Check(player.UsedMana == 1 && player.TotalManaSpentThisGame == spent + 1, "trade costs one mana");
        Core2026Proof.Check(player.HandZone.Count == hand && player.DeckZone.Count == deck && source.Zone == player.DeckZone
            && player.HandZone.Any(c => c.Id == replacement), "trade shuffles same entity and draws original top card");
        Core2026Proof.Check(player.NumCardsDrawnThisTurn == draws + 1 && player.NumCardsPlayedThisTurn == played
            && board.SequenceEqual(player.BoardZone.Concat(player.Opponent.BoardZone).Select(m => m.Id)), "trade draws without playing or Battlecry");
        Core2026Proof.Check(player.DeckZone.Where(c => c != source).Select(c => c.Id).SequenceEqual(order.Where(entity => entity != replacement)),
            "trade preserves unrelated deck order");

        // A definition's cost tag is authoritative, including explicit zero; absent means one.
        try
        {
            foreach (int? cost in new int?[] { 2, 0, null })
            {
                if (cost.HasValue) card.Tags[GameTag.DECK_ACTION_COST] = cost.Value;
                else card.Tags.Remove(GameTag.DECK_ACTION_COST);
                var next = Add(player, id);
                int expected = cost ?? 1;
                player.BaseMana = 0; player.UsedMana = 0; player.TemporaryMana = expected;
                if (expected > 0)
                {
                    player.TemporaryMana--;
                    var before = Canonical.State(game);
                    long beforeDraws = rng.DrawCount;
                    Core2026Proof.Check(!player.Options().Any(t => t is TradeCardTask && t.Source == next)
                        && !game.Process(TradeCardTask.Any(player, next)) && Canonical.State(game) == before && rng.DrawCount == beforeDraws,
                        "insufficient trade cost rejected without mutation");
                    player.TemporaryMana++;
                }
                spent = player.TotalManaSpentThisGame;
                Core2026Proof.Check(game.Process(TradeCardTask.Any(player, next)) && player.TemporaryMana == 0 && player.UsedMana == 0
                    && player.TotalManaSpentThisGame == spent + expected, "definition cost paid from temporary mana");
            }
            card.Tags[GameTag.DECK_ACTION_COST] = -1;
            var invalid = Add(player, id);
            Core2026Proof.Check(!TradeCardTask.IsValid(player, invalid) && !game.Process(TradeCardTask.Any(player, invalid)),
                "negative definition cost cannot grant mana");
        }
        finally { card.Tags[GameTag.DECK_ACTION_COST] = 1; }
    }
}
