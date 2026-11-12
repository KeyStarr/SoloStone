using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using L = LocationProofs;

internal static class DemolitionRenovatorProof
{
    internal static void StandardCase() => StandardSetsProof.Case("CORE_REV_023", (game, p, id) => {
        var location = L.Location(p.Opponent);
        var card = Entity.FromCard(p, Cards.FromId(id), zone:p.HandZone);
        var task = p.Options().OfType<PlayCardTask>().First(t => t.Source == card && t.ActionTarget == location);
        L.Check(game.Process(task) && location.Zone == p.Opponent.GraveyardZone, "Renovator destroys enemy location");
        L.End(game);
    });
    internal static void Run()
    {
        for (int seed = 1; seed <= 3; seed++)
        foreach (bool available in new[] { false, true })
        {
            var game = L.Game(seed); var p = game.Player1; var friendly = L.Location(p);
            var enemy = available ? L.Location(p.Opponent) : null;
            if (enemy != null) { enemy.IsExhausted = true; enemy[GameTag.LOCATION_ACTION_COOLDOWN] = 1; enemy.Location.ConditionLocked = true; }
            L.Minion(p.Opponent);
            var card = Entity.FromCard(p, Cards.FromId("CORE_REV_023"), zone:p.HandZone);
            game.DeathProcessingAndAuraUpdate();
            foreach (var fork in new[] { game, game.CloneExact(history:true), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                var owner = fork.Player1; var source = fork.IdEntityDic[card.Id];
                var plays = owner.Options().OfType<PlayCardTask>().Where(t => t.Source == source).ToArray();
                L.Check(plays.Length == 2 && plays.All(t => t.ActionTarget?.Id == enemy?.Id), "only enemy locations targeted; absent target optional; every slot offered");
                var invalid = PlayCardTask.Any(owner, source, zonePosition:0); invalid.ActionTarget = fork.IdEntityDic[friendly.Id];
                L.Check(!fork.Process(invalid) && source.Zone == owner.HandZone, "friendly location rejected before payment");
                if (available) L.Check(!fork.Process(PlayCardTask.Any(owner, source, zonePosition:0)), "target required when available");
                var ordinary = Entity.FromCard(owner, Cards.FromId("CS2_182"), zone:owner.HandZone);
                var forged = PlayCardTask.Any(owner, ordinary, fork.IdEntityDic[friendly.Id], zonePosition:0, skipPrePhase:true);
                L.Check(!fork.Process(forged) && ordinary.Zone == owner.HandZone, "ordinary Battlecry cannot bypass location target declaration");
                L.Check(fork.Process(plays[0]), "Renovator plays with optional location target");
                if (available) L.Check(fork.IdEntityDic[enemy.Id].Zone == owner.Opponent.GraveyardZone, "closed locked location destroyed");
                L.Check(fork.DeadBoardObjects.Count == 0 && fork.NumMinionsKilledThisTurn == 0 && owner.Opponent.NumFriendlyMinionsThatDiedThisTurn == 0, "board-object death path has no minion counters");
            }
            var tradeGame = L.Game(seed); var trader = tradeGame.Player1;
            var tradeCard = Entity.FromCard(trader, Cards.FromId("CORE_REV_023"), zone:trader.HandZone);
            var trade = trader.Options().OfType<TradeCardTask>().Single(t => t.Source == tradeCard);
            L.Check(tradeGame.Process(trade) && trader.UsedMana == 1 && tradeCard.Zone == trader.DeckZone, "Tradeable remains functional");
        }
        Console.WriteLine("PASS Renovator seeds 1-3 enemy-only/optional/closed-locked/Tradeable/no minion deaths/clone/snapshot");
    }
}
