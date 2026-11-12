using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class PrescientSlitherdrakeProof
{
    internal static void Run() => StandardSetsProof.Case("END_033", (g, p, id) => {
        var source = Add(p, id); Visible(source, 7);
        var opponentDragon = Add(p.Opponent, id); Visible(source, 7);
        var dragon = Add(p, id); g.AuraUpdate(); Visible(source, 4); Visible(dragon, 4);
        foreach (var resumed in new[] { g.CloneExact(history: false), Game.LoadSnapshot(g.SaveSnapshot()) }) {
            var restoredSource = resumed.IdEntityDic[source.Id];
            var restoredDragon = resumed.IdEntityDic[dragon.Id];
            Visible(restoredSource, 4); Visible(restoredDragon, 4);
            resumed.Player1.SetasideZone.Add(resumed.Player1.HandZone.Remove(restoredDragon)); resumed.AuraUpdate();
            Visible(restoredSource, 7); Visible(source, 4); Visible(dragon, 4);
        }
        p.SetasideZone.Add(p.HandZone.Remove(dragon)); g.AuraUpdate(); Visible(source, 7);
        var all = Add(p, "GIL_681"); Core2026Proof.Check(all.Card.IsRace(Race.DRAGON), "ALL race is valid Dragon"); g.AuraUpdate(); Visible(source, 4);
        Effects.SetCost(2).ApplyTo(source); Visible(source, 0);
        var copy = Generic.Copy(p.Opponent, p.Hero, source, Zone.HAND); g.AuraUpdate(); Visible(copy, 0);
        p.Opponent.SetasideZone.Add(p.Opponent.HandZone.Remove(opponentDragon)); g.AuraUpdate(); Visible(copy, 2);
        p.SetasideZone.Add(p.HandZone.Remove(all)); g.AuraUpdate(); Visible(source, 2);
        var deckCopy = Generic.Copy(p, p.Hero, source, Zone.DECK); Visible(deckCopy, 7);
        var minion = (Minion)Play(g, p, id);
        StandardSetsProof.Elusive(g, p, minion);
    });
}
