using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned data/carddb/37.0.0.254477/cards.json.gz (identity and current text).
internal static class WitchsApprenticeProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_GIL_531", (g, p, id) => {
        CorePoolProof.CrossClass(g, id);
        var source = (Minion)Play(g, p, id); var generated = p.HandZone.Single();
        Core2026Proof.Check(source.HasTaunt && generated.Card.Class == CardClass.SHAMAN && generated.Card.Type == CardType.SPELL && generated.Cost == generated.Card.Cost, "Taunt and one actual Shaman spell");
        int hand = p.HandZone.Count; Summon(p, id); Core2026Proof.Check(p.HandZone.Count == hand, "summoning does not execute Battlecry");
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        var fromHand = p.HandZone.First(); p.SetasideZone.Add(p.HandZone.Remove(fromHand));
        Summon(p, "LOE_077"); int burned = p.GraveyardZone.Count(x => x.Card.Class == CardClass.SHAMAN && x.Card.Type == CardType.SPELL);
        var card = Add(p, id); var clone = g.CloneExact(history: true); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
        foreach (var next in new[] { clone, loaded, g }) {
            var owner = next.Player1; var apprentice = next.IdEntityDic[card.Id];
            PlayEntity(next, owner, apprentice);
            Core2026Proof.Check(owner.HandZone.IsFull && owner.HandZone.Last().Card.Class == CardClass.SHAMAN && owner.HandZone.Last().Card.Type == CardType.SPELL && owner.GraveyardZone.Count(x => x.Card.Class == CardClass.SHAMAN && x.Card.Type == CardType.SPELL) == burned + 1, "Brann fills final hand slot then burns second generated spell across clone/snapshot");
        }
    }, legacyPoolMember: true);
}
