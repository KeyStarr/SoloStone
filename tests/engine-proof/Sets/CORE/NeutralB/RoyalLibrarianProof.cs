using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class RoyalLibrarianProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_SW_066", (g, p, id) => {
        var target=Summon(p.Opponent,"CORE_EX1_110"); var source=Add(p,id);
        Core2026Proof.Check(p.Options().Where(t=>t is PlayCardTask && t.Source?.Id==source.Id).All(t=>t.Target is Minion),"minion-only Battlecry targets");
        PlayEntity(g,p,source,target); Core2026Proof.Check(target.IsSilenced && !target.HasTaunt && !target.HasDeathrattle,"enemy silence removes Taunt and Deathrattle");
        var friend=Summon(p,"GVG_085"); Play(g,p,id,friend); Core2026Proof.Check(friend.IsSilenced && !friend.HasTaunt && !friend.HasDivineShield,"friendly silence removes both keywords");
        Kill(g,target); Core2026Proof.Check(!p.Opponent.BoardZone.Any(m=>m.Card.Id=="EX1_110t"),"silenced Deathrattle does not fire");
        ParkBoard(p); ParkBoard(p.Opponent); Play(g,p,id); Core2026Proof.Check(p.BoardZone.Count==1,"no available minion target still allows Battlecry minion play");
        var trade = Add(p, id); p.BaseMana = 1; p.UsedMana = 0; p.TemporaryMana = 0;
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
    }, legacyPoolMember: true);
}
