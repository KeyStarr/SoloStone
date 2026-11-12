using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
internal static class BrightwingProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_189", (g, p, id) => {
        CorePoolProof.CrossClass(g, id);
        var held = Add(p, id); PoolPilotProof.Resume(g, next => {
            PlayEntity(next, next.Player1, (IPlayable)next.IdEntityDic[held.Id]); var generated = next.Player1.HandZone.Single();
            Core2026Proof.Check(generated.Card.Type == CardType.MINION && generated.Card.Rarity == Rarity.LEGENDARY
                && next.OfflinePools.Standard.Any(c => c.CardId == generated.Card.Id), "one Legendary minion from admitted Standard pool");
            Core2026Proof.Check(next.Player2.HandZone.Count == 0, "opponent gains nothing");
        });
        PoolPilotProof.EmptyHand(p); Summon(p, "LOE_077"); held = Add(p, id); PoolPilotProof.FillHand(p);
        PlayEntity(g, p, held);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Count(c => c.Card.Rarity == Rarity.LEGENDARY) == 1, "doubled Battlecry has one slot; second generated card cannot grow hand");
    }, legacyPoolMember: true);
}
