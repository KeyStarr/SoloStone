extern alias TransportFixtures;
using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

internal static partial class PoweredUpGlowTests
{
    // #194: the client takes an either-side minion's drop slot from the human's own board, also when the sub-option then
    // picks the enemy side. A slot past the end of a smaller enemy board was rejected as "no longer legal" on every resend,
    // so the card could not be played there. Every either-side card goes through the real SendOption path. Unpinned: no
    // retail trace shows where retail puts it; the slot is kept when it fits the enemy board, else it is the last slot.
    [GroupProof("either-side-enemy-slot-native", ProofKind.Native)]
    private static void Unpinned_EitherSideEnemySlot()
    {
        var ids = Cards.All.Where(card => card.Implemented && EitherSideRules.Supports(card)).Select(card => card.Id).ToArray();
        Check(ids.Contains("JAIL_455") && ids.Length >= 5, "either-side cards found: " + string.Join(",", ids));
        foreach (string id in ids)
        {
            // The report: own board 5 minions, enemy board a location only, client slot 4.
            EnemySlot(id, own: 5, enemyMinions: 0, enemyLocation: true, slot: 4, expected: 1);
            // Own board full: only the enemy side is offered. Engine-level case: live, the client does not let a full own board
            // take the drop at all (a retail enemy-board drop option is a separate issue), so no client sends this slot today.
            EnemySlot(id, own: 7, enemyMinions: 2, enemyLocation: false, slot: 8, expected: 2);
            EnemySlot(id, own: 4, enemyMinions: 0, enemyLocation: false, slot: 3, expected: 0);
            // A slot that fits the enemy board is kept, and the friendly side is unchanged.
            EnemySlot(id, own: 5, enemyMinions: 4, enemyLocation: false, slot: 2, expected: 1);
            EnemySlot(id, own: 5, enemyMinions: 1, enemyLocation: true, slot: 2, expected: 1);
            FriendlySlot(id, own: 5, slot: 3);
            PastBothBoards(id);
        }
        Console.WriteLine("PASS either-side enemy slot: " + ids.Length + " cards (#194)");
    }

    private static void EnemySlot(string id, int own, int enemyMinions, bool enemyLocation, int slot, int expected)
    {
        string scenario = $"{id} own={own} enemy={enemyMinions}{(enemyLocation ? "+location" : "")} slot={slot}";
        var f = new Fixture(); var p = f.Player; var enemy = p.Opponent;
        for (int i = 0; i < own; i++) f.Summon("CS2_182", p);
        if (enemyLocation) SabberStoneCore.Model.Entities.Entity.FromCard(enemy, Cards.FromId("JAIL_511"), zone: enemy.BoardZone);
        for (int i = 0; i < enemyMinions; i++) f.Summon("CS2_182", enemy);
        var card = f.Draw(id); f.Flush();
        var before = enemy.BoardZone.Slots.ToList();
        int sent = f.Packets.Count;
        Send(f, card, card.ChooseOnePlayables[1].Id, slot, scenario);
        Check(card.Controller == enemy && card.Zone == enemy.BoardZone, scenario + ": played on the enemy board");
        Placed(enemy.BoardZone, before, card, expected, scenario);
        ClientFollows(f, sent, card, slot, scenario);
    }

    // A slot past the human's own board as well is no drop slot the client can send: still refused, nothing paid.
    private static void PastBothBoards(string id)
    {
        var f = new Fixture(); var p = f.Player;
        for (int i = 0; i < 2; i++) f.Summon("CS2_182", p);
        var card = f.Draw(id); f.Flush();
        bool refused = false;
        try { Send(f, card, card.ChooseOnePlayables[1].Id, 5, id + " past both boards"); }
        catch (InvalidOperationException error) when ((error.InnerException ?? error) is ArgumentException rejected
            && rejected.Message.StartsWith("Selected board placement is no longer legal", StringComparison.Ordinal)) { refused = true; }
        Check(refused && card.Zone == p.HandZone && p.UsedMana == 0, id + ": slot past both boards refused before payment");
    }

    private static void FriendlySlot(string id, int own, int slot)
    {
        string scenario = $"{id} friendly own={own} slot={slot}";
        var f = new Fixture(); var p = f.Player;
        for (int i = 0; i < own; i++) f.Summon("CS2_182", p);
        SabberStoneCore.Model.Entities.Entity.FromCard(p.Opponent, Cards.FromId("JAIL_511"), zone: p.Opponent.BoardZone);
        var card = f.Draw(id); f.Flush();
        var before = p.BoardZone.Slots.ToList();
        int sent = f.Packets.Count;
        Send(f, card, card.ChooseOnePlayables[0].Id, slot, scenario);
        Check(card.Controller == p && card.Zone == p.BoardZone, scenario + ": played on the friendly board");
        Placed(p.BoardZone, before, card, slot - 1, scenario);
        ClientFollows(f, sent, card, slot, scenario);
    }

    // Disguised Executioner then destroys a random neighbour, so check the order of the survivors around the card:
    // the cards that were left of the expected slot are still left of it, the others right of it.
    private static void Placed(SabberStoneCore.Model.Zones.BoardZone board, System.Collections.Generic.List<IPlayable> before,
        IPlayable card, int expected, string scenario)
    {
        var after = board.Slots.ToList(); int at = after.IndexOf(card);
        Check(at >= 0 && at <= expected, scenario + $": slot {expected}, got {at}");
        for (int i = 0; i < before.Count; i++)
        {
            int j = after.IndexOf(before[i]);
            if (j >= 0) Check(i < expected == j < at, scenario + $": slot {expected}, got {at} of [{string.Join(",", after.Select(e => e.Card.Id))}]");
        }
    }

    // A dragged play: the client has already put the card at the drop slot of the human's own board before it sends the option,
    // and it moves that card again only once the play's history pairs with that prediction. On the client model of that, the card
    // must end where the engine put it, on either board, with both boards in the engine's order. Before the fix an enemy-side
    // play at another slot left the prediction unpaired and the card on the human's board (live check).
    private static void ClientFollows(Fixture f, int sent, IPlayable card, int slot, string scenario)
    {
        var view = TransportFixtures::TransportInvariants.BoardActorInvariant.DroppedCard(f.Packets.Take(sent), card.Id, slot, 1,
            f.Packets.Skip(sent));
        int[] Board(Controller side) => side.BoardZone.Slots.Select(e => e.Id).ToArray();
        string boards = $"client [{string.Join(",", view.Board1)}] [{string.Join(",", view.Board2)}], engine [{string.Join(",", Board(f.Player))}] [{string.Join(",", Board(f.Player.Opponent))}]";
        Check(view.Resolved, scenario + ": the client pairs its drop prediction with the play; " + boards);
        Check(view.Zone == (int)SabberStoneCore.Enums.Zone.PLAY && view.Controller == card.Controller.PlayerId,
            scenario + $": the client shows the card on the board of player {card.Controller.PlayerId}, not {view.Controller}; " + boards);
        Check(view.Board1.SequenceEqual(Board(f.Player)) && view.Board2.SequenceEqual(Board(f.Player.Opponent)),
            scenario + ": the client's boards match the engine's; " + boards);
    }

    private static void Send(Fixture f, IPlayable card, int sideEntity, int slot, string scenario)
    {
        typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f.Match, null);
        var options = (PowerAllOptions)typeof(NativeMatch).GetField("humanOptions", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(f.Match);
        int index = options.PowerOptionList.FindIndex(o => o.MainOption?.EntityId == card.Id);
        Check(index >= 0, scenario + ": play option offered");
        int sub = options.PowerOptionList[index].SubOptions.FindIndex(o => o.EntityId == sideEntity);
        Check(sub >= 0, scenario + ": side sub-option offered");
        try { f.Match.SendOption(options.Index, index, 0, sub, slot); }
        catch (Exception error)
        {
            throw new InvalidOperationException("TEST PoweredUp: " + scenario + ": SendOption rejected: "
                + (error.InnerException ?? error).Message, error);
        }
    }
}
