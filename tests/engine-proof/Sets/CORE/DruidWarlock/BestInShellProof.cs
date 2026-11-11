using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BestInShellProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_SW_429", (g, p, id) => {
        var left = Summon(p, "CS2_182"); Play(g, p, id);
        var turtles = p.BoardZone.Skip(1).ToArray();
        Core2026Proof.Check(turtles.Length == 2 && turtles.All(m => m.Card.Id == "SW_429t" && m.Card.Implemented && m.AttackDamage == 2
            && m.Health == 7 && m.HasTaunt && m.IsRace(Race.BEAST)), "two exact 2/7 Taunt Beasts");
        Core2026Proof.Check(p.BoardZone.First() == left && turtles[0].Id < turtles[1].Id, "spell appends summons in creation order");
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Last().Card.Id == "SW_429t", "one free space summons one Turtle");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7, "full board adds no Turtle");
        var trade = Add(p, id); p.BaseMana = 1; p.UsedMana = 0;
        Core2026Proof.Check(trade.Card[GameTag.TRADEABLE] == 1 && p.Options().Any(t => t is TradeCardTask && t.Source == trade)
            && !p.Options().Any(t => t is PlayCardTask && t.Source == trade), "Tradeable offered below play cost");
        int hand = p.HandZone.Count, deck = p.DeckZone.Count, top = p.DeckZone.TopCard.Id;
        var clone = g.CloneExact(history: true);
        Core2026Proof.Check(clone.Process(TradeCardTask.Any(clone.Player1, (IPlayable)clone.IdEntityDic[trade.Id])), "clone trade accepted");
        Core2026Proof.Check(trade.Zone == p.HandZone && p.UsedMana == 0, "clone trade leaves original untouched");
        Core2026Proof.Check(g.Process(TradeCardTask.Any(p, trade)) && trade.Zone == p.DeckZone && p.UsedMana == 1
            && p.HandZone.Count == hand && p.DeckZone.Count == deck && p.HandZone.Any(c => c.Id == top)
            && p.BoardZone.Count == 7, "trade costs one, replaces card, does not summon");
    });
}
