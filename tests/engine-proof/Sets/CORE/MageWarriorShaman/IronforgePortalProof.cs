using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class IronforgePortalProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_WON_337", (g, p, id) => {
        CoreCostProof.Count(g, id); int armor = p.Hero.Armor;
        var source = Play(g, p, id);
        Core2026Proof.Check(p.Hero.Armor == armor + 4 && p.BoardZone.Single().Card.Cost == 4, "gain four Armor and summon four-cost minion");
        p.BoardZone.Add((BoardObject)Entity.FromCard(p, Cards.FromId("CORE_REV_990")));
        Summon(p, "CS2_182"); p.BoardZone.Add((BoardObject)Entity.FromCard(p, Cards.FromId("CORE_REV_990")));
        var slots = p.BoardZone.Slots.Select(c => c.Id).ToArray();
        Resume(g, other => {
            var spell = Play(other, other.Player1, id); var board = other.Player1.BoardZone;
            Core2026Proof.Check(other.Player1.Hero.Armor == armor + 8 && board.SlotCount == 5
                && board.Slots.Take(4).Select(c => c.Id).SequenceEqual(slots)
                && board.GetSlot(4) is Minion generated && generated.Card.Cost == 4 && generated[GameTag.DISPLAYED_CREATOR] == spell.Id,
                "Portal appends at physical far right after trailing location on mixed board");
        });
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(p.Hero.Armor == armor + 12 && p.BoardZone.SlotCount == 7, "Armor gain survives full mixed board");
    });
}
