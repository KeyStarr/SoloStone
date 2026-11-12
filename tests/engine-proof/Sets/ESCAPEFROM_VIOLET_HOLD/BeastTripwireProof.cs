using SabberStoneCore.Model.Zones;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BeastTripwireProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_879", (g, p, id) => {
        KnownMetaProofFixture.UseBlackEmpire(g);
        var source = Play(g, p, id);
        VioletHoldPoolProof.SummonedResults(g, source, id, 1, 0);
        var traps = p.DeckZone.Where(c => c.Card.Id == "JAIL_879t").ToArray();
        Core2026Proof.Check(traps.Length == 2 && traps.All(c => c.Card.AssetId == 126421 && c.Cost == 4
            && c.Card.Type == CardType.SPELL && c.Card[GameTag.CASTSWHENDRAWN] == 1 && c.Card.Implemented
            && c.Controller == p && c.Zone == p.DeckZone && c[GameTag.DISPLAYED_CREATOR] == source.Id),
            "two exact Casts When Drawn traps shuffled into own deck");
        Core2026Proof.Check(p.Opponent.BoardZone.SlotCount == 0 && p.HandZone.Count == 0, "summon only on friendly board; no hand generation");
        ClearBoard(g, p);
        p.SetasideZone.Add(p.DeckZone.Remove(traps[1]));
        var left = Summon(p, "CS2_182");
        var clone = g.CloneExact(history: true);
        int slotsAtReplacement = -1;
        void ObserveDraw(IEntity card)
        {
            if (card.Controller == p && card.Card.Id == "CS2_182") slotsAtReplacement = p.BoardZone.SlotCount;
        }
        g.TriggerManager.DrawTrigger += ObserveDraw;
        Generic.Draw(p, traps[0]); g.DeathProcessingAndAuraUpdate();
        g.TriggerManager.DrawTrigger -= ObserveDraw;
        Generic.Draw(clone.Player1, clone.IdEntityDic[traps[0].Id]); clone.DeathProcessingAndAuraUpdate();
        VioletHoldPoolProof.SummonedResults(g, traps[0], id, 1, 1);
        Core2026Proof.Check(traps[0].Zone == p.SetasideZone && p.HandZone.Single().Card.Id == "CS2_182"
            && slotsAtReplacement == 2 && Canonical.State(g) == Canonical.State(clone), "drawn trap summons before replacement draw; exact clone deterministic");
        ClearBoard(g, p); PoolPilotProof.EmptyHand(p);
        var playedTrap = Play(g, p, "JAIL_879t");
        VioletHoldPoolProof.SummonedResults(g, playedTrap, id, 1, 0);
        Core2026Proof.Check(!p.DeckZone.Any(c => c.Card.Id == "JAIL_879t") && p.HandZone.Count == 0,
            "manually played trap summons without shuffling or drawing");

        ClearBoard(g, p);
        // Locations consume physical slots, including when the board contains fewer than seven minions.
        var location = LocationProofs.Location(p, "JAIL_877");
        for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        source = Play(g, p, id);
        VioletHoldPoolProof.SummonedResults(g, source, id, 1, 6);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && location.ZonePosition == 0, "spell appends into final physical slot");
        source = Play(g, p, id);
        VioletHoldPoolProof.SummonedResults(g, source, id, 0, 7);
        Core2026Proof.Check(p.DeckZone.Count(c => c.Card.Id == "JAIL_879t") == 4, "full board still shuffles two traps");
        var drawn = p.DeckZone.First(c => c.Card.Id == "JAIL_879t");
        foreach (var other in p.DeckZone.Where(c => c.Card.Id == "JAIL_879t" && c.Id != drawn.Id).ToArray())
            p.SetasideZone.Add(p.DeckZone.Remove(other));
        Generic.Draw(p, drawn); g.DeathProcessingAndAuraUpdate();
        VioletHoldPoolProof.SummonedResults(g, drawn, id, 0, 7);
        Core2026Proof.Check(p.HandZone.Single().Card.Id == "CS2_182", "full board drawn trap still draws replacement");

        ClearBoard(g, p); location.Zone.Remove(location);
        PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p, 10);
        p.DeckZone.Add(p.SetasideZone.Remove(traps[1]));
        long before = g.Random.DrawCount; int deck = p.DeckZone.Count;
        Generic.Draw(p, traps[1]); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.SlotCount == 0 && p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1
            && g.Random.DrawCount == before, "burned trap neither summons nor draws replacement nor samples pool");
        PoolPilotProof.EmptyHand(p);

        foreach (int count in new[] { 0, 1 }) VioletHoldPoolProof.WithPool(g, id, count, () => {
            int prior = p.DeckZone.Count;
            var generatedBy = Play(g, p, id);
            VioletHoldPoolProof.SummonedResults(g, generatedBy, id, count, 0);
            Core2026Proof.Check(p.DeckZone.Count == prior + 2, "empty/singleton pool preserves independent shuffle");
            ClearBoard(g, p);
        });
        location = LocationProofs.Location(p);
        var copiedSource = Summon(p, "CS2_182");
        var right = Summon(p, "CS2_182");
        Cards.FromId(id).Power.PowerTask.Process(g, p, copiedSource, null, new TaskStack());
        g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        VioletHoldPoolProof.SummonedResults(g, copiedSource, id, 1, 2);
        Core2026Proof.Check(right.ZonePosition == 3, "copied recipe summons right of actual live source across physical slots");
        ClearBoard(g, p); p.BoardZone.Remove(location);
        Summon(p, "CS2_182");
        Cards.FromId(id).Power.PowerTask.Process(g, p, copiedSource, null, new TaskStack());
        g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        // Earlier summons are in the graveyard; only the new entity is on the board.
        VioletHoldPoolProof.SummonedResults(g, copiedSource, id, 1, 1);
        Core2026Proof.Check(copiedSource.Zone == p.GraveyardZone, "copied recipe appends after source leaves play");
        while (p.DeckZone.Count < (DeckZone.DeckMaximumCapcity - 1)) Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.Count == DeckZone.DeckMaximumCapcity, "trap shuffle respects current deck capacity");
    });
}
