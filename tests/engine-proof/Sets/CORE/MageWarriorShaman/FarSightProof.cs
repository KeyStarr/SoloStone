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
internal static class FarSightProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_053", (g, p, id) => {
        var expensive = Entity.FromCard(p, Cards.FromId("CS2_200"), zone: p.DeckZone);
        var existing = Add(p, "CS2_200"); Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Contains(expensive) && expensive.Cost == 3 && existing.Cost == 6 && expensive.AppliedEnchantments.Any(e => e.Card.Id == "CS2_053e"), "only drawn entity gains exact three-cost discount enchantment");
        var cheap = Entity.FromCard(p, Cards.FromId("CS2_171"), zone: p.DeckZone); Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Contains(cheap) && cheap.Cost == 0, "discount clamps at zero");
        PoolPilotProof.Resume(g, next => {
            End(next); End(next);
            Core2026Proof.Check(next.IdEntityDic[expensive.Id].Cost == 3 && next.IdEntityDic[cheap.Id].Cost == 0, "discount persists across turns, clone and snapshot");
        });
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        int damage = p.Hero.Damage; int hand = p.HandZone.Count; Play(g, p, id);
        Core2026Proof.Check(p.Hero.Damage == damage + 1 && p.HandZone.Count == hand && existing.Cost == 6, "empty deck fatigues once, discounts no existing card");
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        Play(g, p, "CORE_EX1_559"); var burn = Entity.FromCard(p, Cards.FromId("CS2_200"), zone: p.DeckZone);
        while (p.HandZone.Count < 9) Add(p, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(p.HandZone.IsFull && burn.Zone == p.GraveyardZone && burn.Cost == 6, "cast trigger fills hand; overdraw is burned without discount");
    }, legacyPoolMember: true);
}
