using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class BigGameHunterProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_005", (g, p, id) => {
        var friendly = Summon(p, "CS2_186"); var enemy = Summon(p.Opponent, "CS2_186"); var small = Summon(p.Opponent, "CS2_200");
        var source = Add(p, id); var plays = p.Options().Where(t => t is PlayCardTask && t.Source == source).ToArray();
        Core2026Proof.Check(plays.Any(t => t.Target == friendly) && plays.Any(t => t.Target == enemy) && plays.All(t => t.Target == friendly || t.Target == enemy), "targets either side at seven Attack, excludes six");
        PlayEntity(g, p, source, enemy); Play(g, p, id, friendly);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && friendly.Zone.Type == Zone.GRAVEYARD && small.Zone == p.Opponent.BoardZone, "destroy chosen seven-Attack targets only");
        Play(g, p, id); var trade = Add(p, id); p.BaseMana = 1; p.UsedMana = 0; p.TemporaryMana = 0;
        Core2026Proof.Check(trade.Card[GameTag.TRADEABLE] == 1 && p.Options().Any(t => t is TradeCardTask && t.Source == trade)
            && !p.Options().Any(t => t is PlayCardTask && t.Source == trade), "Tradeable available for one Mana below play cost");
        int replacement = p.DeckZone.TopCard.Id; int hand = p.HandZone.Count; var slots = p.BoardZone.Slots.Select(m => m.Id).ToArray();
        var clone = g.CloneExact(history: true);
        Core2026Proof.Check(g.Process(TradeCardTask.Any(p, trade))
            && clone.Process(TradeCardTask.Any(clone.Player1, (IPlayable)clone.IdEntityDic[trade.Id])), "trade accepted in original and clone");
        Core2026Proof.Check(trade.Zone == p.DeckZone && p.UsedMana == 1 && p.HandZone.Count == hand && p.HandZone.Any(c => c.Id == replacement)
            && p.BoardZone.Slots.Select(m => m.Id).SequenceEqual(slots) && Canonical.State(g) == Canonical.State(clone), "same entity shuffled, top drawn, no Battlecry; deterministic clone");
        p.BaseMana = 0; p.UsedMana = 0; var blocked = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t is TradeCardTask && t.Source == blocked), "zero Mana cannot trade");
        End(g);
    });
}
