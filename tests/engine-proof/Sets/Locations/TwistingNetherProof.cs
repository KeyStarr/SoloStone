using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using L = LocationProofs;

internal static class TwistingNetherProof
{
    internal static void StandardCase() => StandardSetsProof.Case("CORE_EX1_312", (game, p, id) => {
        L.Location(p); L.Location(p.Opponent); L.Minion(p); L.Minion(p.Opponent);
        var spell = Entity.FromCard(p, Cards.FromId(id), zone:p.HandZone);
        L.Check(game.Process(PlayCardTask.Any(p, spell)) && p.BoardZone.SlotCount == 0 && p.Opponent.BoardZone.SlotCount == 0, "Nether clears mixed boards");
        L.End(game);
    });
    internal static void Run()
    {
        for (int seed = 1; seed <= 3; seed++)
        foreach (bool locations in new[] { false, true })
        {
            var game = L.Game(seed); var p = game.Player1;
            var first = L.Minion(p);
            if (locations) L.Location(p.Opponent);
            var second = L.Minion(p.Opponent);
            if (locations) { var closed = L.Location(p); closed.IsExhausted = true; closed.Location.ConditionLocked = true; }
            var dormant = L.Minion(p.Opponent); dormant[GameTag.DORMANT] = 1;
            var spell = Entity.FromCard(p, Cards.FromId("CORE_EX1_312"), zone:p.HandZone);
            game.DeathProcessingAndAuraUpdate();
            L.Check(dormant.IsDormant, "fixture dormant minion");
            foreach (var fork in new[] { game, game.CloneExact(history:true), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                var expected = fork.Player1.BoardZone.Slots.Concat(fork.Player2.BoardZone.Slots).Where(e => e.Id != dormant.Id).OrderBy(e => e.OrderOfPlay).Select(e => e.Id).ToArray();
                L.Check(fork.Process(PlayCardTask.Any(fork.Player1, fork.IdEntityDic[spell.Id])), "cast Core Nether");
                L.Check(fork.Player1.BoardZone.SlotCount == 0 && fork.Player2.BoardZone.Slots.Select(e => e.Id).SequenceEqual(new[] { dormant.Id }), "destroy respects existing dormant semantics");
                L.Check(fork.NumMinionsKilledThisTurn == 2 && fork.Player1.NumFriendlyMinionsThatDiedThisTurn == 1 && fork.Player2.NumFriendlyMinionsThatDiedThisTurn == 1, "only minions counted");
                if (locations) PinnedRule_MixedDeathOrder(fork, expected);
                L.Check(fork.DeadBoardObjects.Count == 0 && fork.DeadMinions.Count == 0, "single death pass drained");
            }
        }
        Console.WriteLine("PASS Core Nether seeds 1-3 mixed/minion-only/both boards/dormant/counters/clone/snapshot; PinnedRule_MixedDeathOrder");
    }
    private static void PinnedRule_MixedDeathOrder(Game game, int[] expected)
    {
        var actual = game.PowerHistory.Last.OfType<PowerHistoryTagChange>().Where(t => t.Tag == GameTag.ZONE && t.Value == (int)Zone.GRAVEYARD && expected.Contains(t.EntityId)).Select(t => t.EntityId);
        L.Check(actual.SequenceEqual(expected), "Nether mixed deaths follow ORDER_OF_PLAY: expected=" + string.Join(",", expected) + " actual=" + string.Join(",", actual));
    }
}
