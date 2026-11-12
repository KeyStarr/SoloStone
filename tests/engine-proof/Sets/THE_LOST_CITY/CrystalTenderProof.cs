using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CrystalTenderProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_255", (g, p, id) => {
        var source = Add(p, id); p.BaseMana = 3; p.UsedMana = 0; p.Opponent.BaseMana = 8;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source)), "play at three Mana");
        Core2026Proof.Check(p.BaseMana == 8 && p.UsedMana == 7 && p.RemainingMana == 1 && p.Opponent.BaseMana == 8, "fills positive maximum-Mana difference with empty crystals");
        ClearBoard(g, p); source = Add(p, id); p.BaseMana = 8; p.UsedMana = 0; p.Opponent.BaseMana = 3;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source)) && p.BaseMana == 8 && p.UsedMana == 2, "never loses crystals when ahead");
        ClearBoard(g, p); source = Add(p, id); p.BaseMana = 10; p.UsedMana = 0; p.Opponent.BaseMana = 10;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source)) && p.BaseMana == 10 && p.UsedMana == 2, "equal maximum Mana unchanged");
        var trade = Add(p, id); int hand = p.HandZone.Count, deck = p.DeckZone.Count; p.UsedMana = 0; p.BaseMana = 4;
        Core2026Proof.Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.TRADE_CARD && t.Source?.Id == trade.Id)
            && g.Process(TradeCardTask.Any(p, trade)), "Tradeable option accepted");
        Core2026Proof.Check(trade.Zone == p.DeckZone && p.HandZone.Count == hand && p.DeckZone.Count == deck && p.UsedMana == 1 && p.BaseMana == 4, "trade costs one and replaces card without Battlecry");
    });
}
