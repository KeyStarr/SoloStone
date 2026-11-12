using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static class AncientRaptorProof
{
    // Tier E community reveal + tier D export; owner assumption: data/rules-evidence/misc-cards/raptor-option-ids.yaml.
    internal static void Run() => StandardSetsProof.Case("TLC_245", Unpinned_AncientRaptorOptions);
    private static void Unpinned_AncientRaptorOptions(Game g, Controller p, string id)
    {
        string[] options = { "UNG_999t3", "UNG_999t8", "UNG_999t2" };
        for (int i = 0; i < 3; i++)
        {
            StandardSetsProof.ParkBoard(p); EmptyHand(p);
            Minion source = null;
            var board = StandardSetsProof.DeathrattleSlotBoard(p, () => source = StandardSetsProof.PlayAt(g, p, id, 2));
            AncientStegodonProof.Options(g, p, options);
            int pick = p.Choice.Choices.ElementAt(i);
            Resume(g, next => AncientStegodonProof.Answer(next, pick));
            Core2026Proof.Check(source.AttackDamage == (i == 0 ? 5 : 2) && source.HasDivineShield == (i == 1), "chosen Attack or shield only");
            Kill(g, source);
            var plants = p.BoardZone.Where(m => m.Card.Id == "UNG_999t2t1").ToArray();
            Core2026Proof.Check(plants.Length == (i == 2 ? 2 : 0) && plants.All(m => m.AttackDamage == 1 && m.Health == 1 && m.Cost == 1), "only Living Spores summons exact Plants");
            if (i == 2) StandardSetsProof.CheckDeathrattleSlot(p, board, plants, "Plants occupy dying physical slot in order");
        }
        CostSliceProof.Definition("UNG_999t2t1");
        StandardSetsProof.ParkBoard(p); EmptyHand(p);
        Minion living = null;
        var liveBoard = StandardSetsProof.DeathrattleSlotBoard(p, () => living = StandardSetsProof.PlayAt(g, p, id, 2));
        AncientStegodonProof.Answer(g, p.Choice.Choices.Last());
        new ActivateDeathrattleTask(EntityType.TARGET).Process(g, p, p.Hero, living, new TaskStack()); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        var livePlants = p.BoardZone.Where(m => m.Card.Id == "UNG_999t2t1").ToArray();
        Core2026Proof.Check(p.BoardZone.Slots.Select(m => m.Id).SequenceEqual(new[] { liveBoard.Location.Id, liveBoard.Left.Id, living.Id }
            .Concat(livePlants.Select(m => m.Id)).Append(liveBoard.Right.Id)) && livePlants.Length == 2, "living granted Deathrattle summons immediately right in order");
        Summon(p, "CS2_182");
        new ActivateDeathrattleTask(EntityType.TARGET).Process(g, p, p.Hero, living, new TaskStack()); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "UNG_999t2t1") == 2, "living full-board Deathrattle summons nothing");
        End(g); End(g);
        StandardSetsProof.ParkBoard(p); EmptyHand(p);
        var silenced = (Minion)Play(g, p, id); AncientStegodonProof.Answer(g, p.Choice.Choices.Last()); silenced.Silence(); Kill(g, silenced);
        Core2026Proof.Check(p.BoardZone.SlotCount == 0, "silence removes granted Deathrattle");
        for (int occupied = 6; occupied <= 7; occupied++)
        {
            StandardSetsProof.ParkBoard(p); EmptyHand(p); LocationProofs.Location(p);
            var source = (Minion)Play(g, p, id); AncientStegodonProof.Answer(g, p.Choice.Choices.Last());
            while (p.BoardZone.SlotCount < occupied) Summon(p, "CS2_182");
            Kill(g, source);
            Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "UNG_999t2t1") == (occupied == 6 ? 2 : 1), "Plants respect physical seven-slot capacity");
        }
        End(g);
    }
}
