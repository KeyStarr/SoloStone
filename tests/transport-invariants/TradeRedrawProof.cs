using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;
using Entity = SabberStoneCore.Model.Entities.Entity;

internal static class TradeRedrawProof
{
    internal static void Run()
    {
        var game = OfflinePoolTestRecipes.NewGame(); var player = game.Player1;
        foreach (var card in player.DeckZone.ToArray()) player.SetasideZone.Add(player.DeckZone.Remove(card));
        player.DeckZone.Add(Entity.FromCard(player, Cards.FromId("TLC_513t2")));
        var source = Generic.DrawCard(player, Cards.FromId("TLC_255"));
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new NativeSession(match); session.Flush(); session.PublishDecision(player);
        session.Process(TradeCardTask.Any(player, source));
        SelfChecks.Check(source.Zone == player.HandZone && player.DeckZone.Count == 0 && player.BoardZone.Single().Card.Id == "TLC_513t2"
            && player.UsedMana == 1, "Trade through Summoned When Drawn can redraw original source");
        SelfChecks.Check(!Invariants.Compare(game, session.Client).Any(), "redrawn Trade source transport");
        Console.WriteLine("PASS Trade trace: Summoned When Drawn replacement redraws traded source; violations=0");
    }
}
