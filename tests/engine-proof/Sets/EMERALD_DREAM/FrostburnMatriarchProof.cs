using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static DarkGiftConsumerProof;

internal static class FrostburnMatriarchProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_901", (g, p, id) => {
        Core2026Proof.Check(Cards.FromId(id).IsRace(Race.DRAGON), "Matriarch is a Dragon");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 1, "empty hand does not summon"); Clear(p);
        InvalidHoldings(p); Play(g, p, id);
        Core2026Proof.Check(!p.BoardZone.Any(m => m.Card.Id == "FIR_901t"), "opponent/deck/board gifts, stale flag and silenced hand minion do not count"); Clear(p);
        var self = Gift(p, id); PlayEntity(g, p, self);
        Core2026Proof.Check(!p.BoardZone.Any(m => m.Card.Id == "FIR_901t"), "played gifted source is no longer held"); Clear(p);

        var token = Cards.FromId("FIR_901t");
        Core2026Proof.Check(token.AssetId == 117286 && token.Implemented && !token.Collectible && token.ATK == 4 && token.Health == 4 &&
            token.Cost == 4 && token.Taunt && token.IsRace(Race.DRAGON), "exact 4/4 Taunt Dragon token");
        var left = Entity.FromCard(p, Cards.FromId("CORE_REV_990"), zone: p.BoardZone);
        var right = Summon(p, "CS2_231"); Gift(p);
        var source = Add(p, id); p.UsedMana = 0;
        PoolPilotProof.Resume(g, next => {
            var owner = next.Player1;
            Core2026Proof.Check(next.Process(PlayCardTask.Any(owner, next.IdEntityDic[source.Id], zonePosition: 1)), "play in physical slot");
            var board = owner.BoardZone;
            Core2026Proof.Check(board.SlotCount == 5 && board.GetSlot(0).Id == left.Id && board.GetSlot(1).Id == source.Id &&
                board.GetSlot(4).Id == right.Id, "Dragons beside source, preserving location and right neighbor");
            var dragons = new[] { (Minion)board.GetSlot(2), (Minion)board.GetSlot(3) };
            Core2026Proof.Check(dragons.All(m => m.Card.Id == "FIR_901t" && m.AttackDamage == 4 && m.Health == 4 && m.HasTaunt) &&
                dragons[0].Id < dragons[1].Id, "two native Dragons in creation order");
        }); Clear(p);

        foreach (int filled in new[] { 5, 6 }) {
            Entity.FromCard(p, Cards.FromId("CORE_REV_990"), zone: p.BoardZone);
            for (int i = 1; i < filled; i++) Summon(p, "CS2_231");
            Gift(p); Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "FIR_901t") == 6 - filled,
                "locations consume slots; only available token slots used"); Clear(p);
        }
        Gift(p); var gone = Entity.FromCard(p, Cards.FromId(id), zone: p.SetasideZone);
        var neighbor = Summon(p, "CS2_231");
        PoolPilotProof.Resolve(g, gone);
        Core2026Proof.Check(p.BoardZone.SlotCount == 3 && p.BoardZone.GetSlot(0) == neighbor &&
            p.BoardZone.GetSlot(1).Card.Id == "FIR_901t" && p.BoardZone.GetSlot(2).Card.Id == "FIR_901t", "gone source appends");
    });
}
