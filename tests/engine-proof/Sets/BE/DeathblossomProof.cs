using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class DeathblossomProof
{
    internal static void Run() => StandardSetsProof.Case("BE_802", (g, p, id) => {
        Definition("BE_802t"); Definition("BE_002");
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 2 && p.HandZone.All(c => c.Card.Id == "BE_802t" && c.AppliedEnchantments == null), "two fresh Plants; no redundant cost enchantment");
        foreach (var c in p.HandZone) Visible(c, 2);
        var left = Summon(p, "CS2_182");
        var plant = (Minion)p.HandZone.First(); PlayEntity(g, p, plant);
        var location = (BoardObject)Entity.FromCard(p, Cards.FromId("CORE_REV_990")); p.BoardZone.Add(location);
        var right = Summon(p, "CS2_182");
        var positions = p.BoardZone.Slots.Select(c => c.Id).ToArray();
        Resume(g, next => Kill(next, (Minion)next.IdEntityDic[plant.Id]));
        var bug = p.BoardZone.Single(c => c.Card.Id == "BE_002");
        Core2026Proof.Check(bug.AttackDamage == 5 && bug.Health == 5 && bug.HasTaunt
            && positions.Select(x => x == plant.Id ? bug.Id : x).SequenceEqual(Enumerable.Range(0, p.BoardZone.SlotCount).Select(i => p.BoardZone.GetSlot(i).Id)), "Waterstrider replaces dead Plant beside physical location slot");
        plant = (Minion)p.HandZone.Single(); PlayEntity(g, p, plant); plant.Silence(); Kill(g, plant);
        Core2026Proof.Check(p.BoardZone.Count(c => c.Card.Id == "BE_002") == 1, "silenced Plant has no Deathrattle");
        plant = Summon(p, "BE_802t");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Resolve(g, plant, death: true);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(c => c.Card.Id == "BE_002") == 1, "full physical board blocks summon");
        Kill(g, plant); Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(c => c.Card.Id == "BE_002") == 2, "dead Plant frees exactly one slot");
        EmptyHand(p); FillHand(p, 9); var source = Entity.FromCard(p, Cards.FromId(id));
        Resolve(g, source); Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Count(c => c.Card.Id == "BE_802t") == 1, "one slot; second Plant burns");
        int burned = p.GraveyardZone.Count(c => c.Card.Id == "BE_802t");
        Resolve(g, source); Core2026Proof.Check(p.HandZone.Count == 10 && p.GraveyardZone.Count(c => c.Card.Id == "BE_802t") == burned + 2, "full hand burns both Plants");
    });
}
