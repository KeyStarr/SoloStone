using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class BefuddledSummonerProof
{
    internal static void Run() => StandardSetsProof.Case("BE_196", Unpinned_BefuddledSummoner);
    private static void Unpinned_BefuddledSummoner(Game g, Controller p, string id)
    {
        var source = Summon(p, id); var target = Summon(p.Opponent, "CS2_182");
        target.Silence(); target.BaseHealth = 20; target.AttackDamage = 0; target.Damage = 2;
        PoolPilotProof.Resume(g, next => End(next));
        var copy = p.BoardZone.Single(m => m.Id != source.Id);
        var attack = g.HistoryFacts.Query.AttacksStarted().Single();
        Core2026Proof.Check(copy.Card.Id == target.Card.Id && copy.IsSilenced && copy.BaseHealth == 20 && copy.Damage == 2
            && copy.Controller == p && copy.NumAttacksThisTurn == 0 && copy.IsExhausted
            && attack.AttackerId == copy.Id && attack.TargetId == target.Id,
            "Unpinned_BefuddledCopyState: damaged/silenced zero-Attack copy attacks original without ordinary readiness");
        ForcedCardProof.Clean(g); source = Summon(p, id); source.Silence(); target = Summon(p.Opponent, "CS2_182"); End(g);
        Core2026Proof.Check(p.BoardZone.Count == 1 && target.Damage == 0, "Silence removes end-turn copy");
        ForcedCardProof.Clean(g); source = Summon(p, id); while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        target = Summon(p.Opponent, "CS2_182"); End(g);
        Core2026Proof.Check(p.BoardZone.Count == 7 && target.Damage == 0, "full board prevents copy and attack");
        ForcedCardProof.Clean(g); Summon(p, id); var rng = g.Random.DrawCount; End(g);
        Core2026Proof.Check(p.BoardZone.Count == 1 && g.Random.DrawCount == rng, "empty enemy board creates nothing, consumes no RNG");
    }
}
