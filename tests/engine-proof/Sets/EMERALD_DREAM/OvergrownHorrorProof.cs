using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static DarkGiftConsumerProof;

internal static class OvergrownHorrorProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_654", (g, p, id) => {
        Core2026Proof.Check(Cards.FromId(id).Taunt, "printed Taunt");
        Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == 0, "empty hand harmless"); Clear(p);
        InvalidHoldings(p);
        var before = g.IdEntityDic.Values.Where(e => e is Minion || e is Spell).ToDictionary(e => e.Id, e => e.Cost);
        Play(g, p, id);
        Core2026Proof.Check(before.All(pair => g.IdEntityDic[pair.Key].Cost == pair.Value), "ungifted/stale flag/silenced hand, board/deck/opponent untouched"); Clear(p);

        var drawn = Gift(p, zone: Zone.DECK);
        Generic.Draw(p, drawn);
        Core2026Proof.Check(drawn.Zone == p.HandZone && DarkGifts.Has(drawn), "draw retains original gift");
        Core2026Proof.Check(DarkGifts.Give(drawn, p.Hero), "second gift fixture");
        var zero = Gift(p, "CS2_231");
        int cost = drawn.Cost; Play(g, p, id);
        int discounted = Math.Max(0, cost - 2);
        Core2026Proof.Check(drawn.Cost == discounted && drawn[GameTag.COST] == discounted && zero.Cost == 0 && zero[GameTag.COST] == 0,
            "one fixed discount per gifted minion, visible COST, zero floor");
        Core2026Proof.Check(drawn.AppliedEnchantments.All(e => e.Card.Id != "EDR_654e"), "obsolete +5 Health attachment not used");
        PoolPilotProof.Resume(g, next => {
            var target = next.IdEntityDic[drawn.Id]; End(next); End(next);
            Core2026Proof.Check(target.Zone == next.Player1.HandZone && target.Cost == discounted && target[GameTag.COST] == discounted,
                "discount on drawn minion persists while held across turns, clone and snapshot");
        });
        p.UsedMana = 0; Play(g, p, id);
        Core2026Proof.Check(drawn.Cost == Math.Max(0, discounted - 2), "repeat Battlecry stacks flat discount");
        // Existing HandZone.Remove resets cost modifications; D6 also strips gifts on HAND -> DECK.
        p.DeckZone.Add(p.HandZone.Remove(drawn));
        Core2026Proof.Check(!DarkGifts.Has(drawn) && drawn.Cost == drawn.Card.Cost, "backwards move loses discount under existing engine cost rules");
        Generic.Draw(p, drawn);
        Core2026Proof.Check(drawn.Zone == p.HandZone && drawn.Cost == drawn.Card.Cost, "redraw does not restore removed discount");

        Clear(p);
        Summon(p.Opponent, "CS2_231"); Summon(p.Opponent, "CS2_231");
        var adaptive = Add(p, "BE_733");
        Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_100t3e"), p.Hero, adaptive);
        Core2026Proof.Check(DarkGifts.Has(adaptive) && adaptive.Cost == 6, "gifted Titanic Thrasher costs 6 against two enemy minions before discount");
        Play(g, p, id); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(adaptive.Cost == 4 && adaptive[GameTag.COST] == 4,
            "flat discount does not bake adaptive discount into base cost");
        PoolPilotProof.Resume(g, next => {
            var target = next.IdEntityDic[adaptive.Id];
            var enemy = Summon(next.Player2, "CS2_231");
            Core2026Proof.Check(target.Cost == 3 && target[GameTag.COST] == 3, "adaptive discount still tracks a third enemy after clone/snapshot");
            Kill(next, enemy);
            Core2026Proof.Check(target.Cost == 4 && target[GameTag.COST] == 4, "adaptive discount increases correctly when enemy leaves");
            Play(next, next.Player1, id);
            Core2026Proof.Check(target.Cost == 2 && target[GameTag.COST] == 2, "repeated flat discounts stack independently of adaptive discount");
        });
    });
}
