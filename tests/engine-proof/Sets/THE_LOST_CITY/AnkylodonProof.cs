using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class AnkylodonProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_422", Unpinned_Ankylodon);
    private static void Unpinned_Ankylodon(Game g, Controller p, string id)
    {
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, PoolPilotProof.Filter(id), Cards.FromId(id).AssetId);
        Core2026Proof.Check(candidates.Count >= 3 && candidates.All(c => c.Cost == 3 && c.IsRace(Race.BEAST)),
            "declared Standard any-class Beast pool meets admission threshold");
        var body = Summon(p, id); Core2026Proof.Check(body.HasTaunt, "pinned Taunt");
        PoolPilotProof.Resume(g, next => Kill(next, (Minion)next.IdEntityDic[body.Id]));
        var outputs = p.BoardZone.ToArray();
        Core2026Proof.Check(outputs.Length == 2 && outputs.All(m => m.Card.Cost == 3 && m.IsRace(Race.BEAST) && m.NumAttacksThisTurn == 0)
            && p.Opponent.Hero.Damage == outputs.Sum(m => m.AttackDamage),
            "Unpinned_AnkylodonSummonAttack: two admitted 3-Cost Beasts attack then-current enemy hero");
        ForcedCardProof.Clean(g);
        Batch9PoolProof.WithPools(g, id, 0, () => {
            body = Summon(p, id); var rng = g.Random.CaptureState(); Kill(g, body);
            Core2026Proof.Check(p.BoardZone.IsEmpty && rng.Equals(g.Random.CaptureState()), "empty Beast pool: no summons or attacks");
        });
        ForcedCardProof.Clean(g);
        var source = (Minion)null;
        var board = StandardSetsProof.DeathrattleSlotBoard(p, () => source = Summon(p, id));
        Kill(g, source);
        var beasts = p.BoardZone.Where(m => m.Id != board.Left.Id && m.Id != board.Right.Id).ToArray();
        StandardSetsProof.CheckDeathrattleSlot(p, board, beasts, "Ankylodon Beasts occupy former physical slot");
        ForcedCardProof.Clean(g); body = Summon(p, id);
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        int cursor = g.HistoryFacts.Count; Kill(g, body);
        Core2026Proof.Check(p.BoardZone.Count == 7 && g.HistoryFacts.Query.AttacksStarted().Count(a => a.Sequence > cursor) == 1,
            "Unpinned_AnkylodonSummonAttack: one initial slot creates only one attacker");
        ForcedCardProof.Clean(g); body = Summon(p, id); body.Silence(); Kill(g, body);
        Core2026Proof.Check(p.BoardZone.IsEmpty, "Silence removes Deathrattle");
        Unpinned_InitialCapacitySurvivesSummonDeaths(g, p, id);
    }

    private static void Unpinned_InitialCapacitySurvivesSummonDeaths(Game g, Controller p, string id)
    {
        ForcedCardProof.Clean(g);
        var body = Summon(p, id); var juggler = Summon(p, "NEW1_019");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        var abomination = Summon(p.Opponent, "EX1_097"); abomination.Damage = 3;
        g.SetRandomSeed(1);
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var owner = next.Player1;
            Kill(next, (Minion)next.IdEntityDic[body.Id]);
            Core2026Proof.Check(next.IdEntityDic[abomination.Id].Zone == owner.Opponent.GraveyardZone
                && next.IdEntityDic[juggler.Id].Zone == owner.GraveyardZone,
                "fixture: first Beast's knife triggers deaths that free board slots");
            var created = next.IdEntityDic.Values.Where(m => m[GameTag.DISPLAYED_CREATOR] == body.Id
                && m.Card.Type == CardType.MINION).ToArray();
            Core2026Proof.Check(created.Length == 1,
                "Unpinned_AnkylodonSummonAttack: summon-trigger deaths cannot enlarge initial one-slot capacity");
        }
    }
}
