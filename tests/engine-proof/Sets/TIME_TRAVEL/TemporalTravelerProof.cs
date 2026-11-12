using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TemporalTravelerProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_434", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var source = (Minion)Play(g, p, id); var right = Summon(p, "CS2_182"); Kill(g, source);
        var shadow = p.BoardZone.Single(m => m.Card.Id == "TIME_434t");
        Core2026Proof.Check(shadow.AttackDamage == 4 && shadow.Health == 1 && shadow.Card.Implemented && p.Opponent.Hero.Damage == 0, "exact Shadow survives without enemy minion");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { shadow.Id, left.Id, right.Id }), "gone leftmost source's slot takes Shadow");
        ClearBoard(g, p); var first = Summon(p.Opponent, "CS2_182"); var second = Summon(p.Opponent, "CS2_182"); second.HasTaunt = true;
        source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(new[] { first, second }.Count(m => m.Damage == 4) == 1 && p.GraveyardZone.OfType<Minion>().Any(m => m.Card.Id == "TIME_434t"), "one random enemy attacked with normal retaliation");
        ClearBoard(g, p.Opponent); var shield = Summon(p.Opponent, "CS2_182"); shield.HasDivineShield = true;
        source = (Minion)Play(g, p, id); Kill(g, source); Core2026Proof.Check(!shield.HasDivineShield && shield.Damage == 0, "forced attack respects Shield");
        ClearBoard(g, p.Opponent); source = (Minion)Play(g, p, id); source.Silence(); Kill(g, source); Core2026Proof.Check(p.BoardZone.Count == 0, "Silence suppresses deathrattle");
        source = Summon(p, id); for (int i = 0; i < 6; i++) Summon(p, "CS2_182"); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "TIME_434t") == 1, "death frees slot even on full board");
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack()); Core2026Proof.Check(p.BoardZone.Count == 7, "full board suppresses extra Shadow and attack");
        foreach (var minion in p.BoardZone.ToArray()) minion.Silence();
        var board = DeathrattleSlotBoard(p, () => source = Summon(p, id)); Kill(g, source);
        CheckDeathrattleSlot(p, board, p.BoardZone.Where(m => m.Card.Id == "TIME_434t"), "Shadow fills the dying source's physical slot");
        ParkBoard(p); ParkBoard(p.Opponent);
        source = Summon(p, id);
        var dying = Summon(p.Opponent, "CS2_182"); dying.Damage = dying.BaseHealth + 1;
        int attacks = g.HistoryFacts.Query.AttacksStarted().Count();
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(g.HistoryFacts.Query.AttacksStarted().Count() == attacks
            && p.BoardZone.Any(m => m.Card.Id == "TIME_434t"), "pending-dead sole enemy cannot receive the Shadow's forced attack");
        g.DeathProcessingAndAuraUpdate();
        ParkBoard(p); source = Summon(p, id);
        dying = Summon(p.Opponent, "CS2_182"); dying.Damage = dying.BaseHealth + 1;
        var living = Summon(p.Opponent, "CS2_182");
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(living.Damage == 4, "Shadow selects living enemy before random choice during chained deaths");
        Core2026Proof.Check(g.HistoryFacts.EvidenceValid, "forced-attack evidence remains valid during chained deaths");
        ParkBoard(p); ParkBoard(p.Opponent);
        End(g); // Settle summon bookkeeping after direct kill, as batch-1 deathrattle proofs do.
    });
}
