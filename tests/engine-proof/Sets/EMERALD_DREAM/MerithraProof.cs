using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static EmeraldDreamBatch3Proof;
using static StandardSetsProof;

internal static class MerithraProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_238", (g, p, id) => {
        var replay = g.CloneExact(history: true); var replayPlayer = replay.Player1;
        var deadShudderwock = Summon(replayPlayer, "GIL_820"); Kill(replay, deadShudderwock);
        var deadGiant = Summon(replayPlayer, "NEW1_038"); Kill(replay, deadGiant);
        var shudderwock = Summon(replayPlayer, "GIL_820");
        Cards.FromId(id).Power.PowerTask.Process(replay, replayPlayer, shudderwock, null, new TaskStack());
        replay.ProcessTasks(); replay.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(replayPlayer.BoardZone.Count == 2
            && replayPlayer.BoardZone.Count(m => m.Card.Id == "GIL_820") == 1
            && replayPlayer.BoardZone.Any(m => m.Card.Id == "NEW1_038" && m.Id != deadGiant.Id),
            "Merithra replay with Shudderwock source resurrects only the other eligible identity");
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 1, "empty death pool summons nothing"); Kill(g, source);
        var big = Summon(p, "EDR_486"); Kill(g, big); var duplicate = Summon(p, "EDR_486"); Kill(g, duplicate);
        var other = Summon(p, "EDR_471"); Kill(g, other);
        var eight = Summon(p, "NEW1_038"); Kill(g, eight);
        var seven = Summon(p, "CS2_186"); Kill(g, seven);
        var low = Summon(p, "CS2_182"); Kill(g, low);
        var discarded = Add(p, "EDR_527"); Generic.DiscardBlock(p, discarded);
        var burned = Entity.FromCard(p, Cards.FromId("FIR_778")); p.GraveyardZone.Add(burned);
        var enemy = Summon(p.Opponent, "FIR_778"); Kill(g, enemy);
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182"); source = (Minion)Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play");
        var raised = p.BoardZone.Where(m => m.Card.Id is "EDR_486" or "EDR_471" or "NEW1_038").ToArray();
        Core2026Proof.Check(raised.Length == 3 && raised.Select(m => m.Card.Id).Distinct().Count() == 3, "different friendly deaths only, cost at least 8");
        Core2026Proof.Check(raised.Single(m => m.Card.Id == "NEW1_038").Health == 7
            && p.BoardZone.All(m => m.Card.Id != "CS2_186"), "exactly 8 resurrected; exactly 7 excluded");
        Core2026Proof.Check(raised.All(m => m.Id != big.Id && m.Id != other.Id && m.Damage == 0 && !m.ToBeDestroyed), "fresh resurrected entities");
        Core2026Proof.Check(raised.Single(m => m.Card.Id == "EDR_486").Health == 9 && raised.Single(m => m.Card.Id == "EDR_471").Health == 30, "resurrected exact health");
        Core2026Proof.Check(raised.Single(m => m.Card.Id == "EDR_486").AttackDamage == 7 && raised.Single(m => m.Card.Id == "EDR_486").IsRush
            && raised.Single(m => m.Card.Id == "EDR_471").AttackDamage == 1 && raised.Single(m => m.Card.Id == "EDR_471").HasTaunt, "resurrected Attack and keywords");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] {left.Id, source.Id}.Concat(raised.OrderBy(m => m.Id).Select(m => m.Id)).Append(right.Id)), "creation order right of source");
        Core2026Proof.Check(big.Zone == p.GraveyardZone && duplicate.Zone == p.GraveyardZone && other.Zone == p.GraveyardZone, "death records retained");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182"); int entities = g.IdEntityDic.Count; Battlecry(g, p, source);
        Core2026Proof.Check(p.BoardZone.Count == 7 && g.IdEntityDic.Count == entities, "full board creates nothing");
        Kill(g, right); Battlecry(g, p, source); Core2026Proof.Check(p.BoardZone.Count == 7, "single board slot limits resurrection");
        ClearBoard(g, p); left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182"); Kill(g, source);
        Battlecry(g, p, source); Core2026Proof.Check(p.BoardZone.Take(2).Select(m => m.Id).SequenceEqual(new[] {left.Id, right.Id}), "gone source appends resurrection");
    });
}
