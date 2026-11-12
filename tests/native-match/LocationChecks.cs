using System;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    private static void StalePlacementOptions()
    {
        foreach (string cardId in new[] { "CORE_REV_990", "CS2_182" })
        foreach (string change in new[] { "shrink", "last-location-removal", "mana", "full" })
        {
            var capture = new Capture(); var match = StartAtHumanTurn(capture); var game = match.Game;
            var player = game.Player1;
            player.BaseMana = 10; player.UsedMana = 0;
            var location = (BoardObject)SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("JAIL_877"), zone:player.BoardZone);
            var removed = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("CS2_182"), zone:player.BoardZone);
            var source = Generic.DrawCard(player, Cards.FromId(cardId));
            FlushHistory(match);
            typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
            var options = capture.Last<AllOptions>(14);
            int index = options.Options.FindIndex(o => o.MainOption?.Id == source.Id);
            Check(index >= 0 && match.ValidateOption(options.Id, index, 0, -1, 3) is PlayCardTask,
                "stale placement fixture must initially allow position 3");
            if (change == "shrink") player.BoardZone.Remove(removed);
            else if (change == "last-location-removal") player.BoardZone.Remove(location);
            else if (change == "mana") player.UsedMana = 10;
            else while (!player.BoardZone.IsFull)
                SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("CS2_182"), zone:player.BoardZone);
            int mana = player.RemainingMana;
            var hand = player.HandZone.Select(p => p.Id).ToArray();
            var board = player.BoardZone.Slots.Select(p => p.Id).ToArray();
            AssertRejectedWithoutMutation(match, capture, () => match.ValidateOption(options.Id, index, 0, -1, 3));
            AssertRejectedWithoutMutation(match, capture, () => match.SendOption(options.Id, index, 0, -1, 3));
            Check(player.RemainingMana == mana && player.HandZone.Select(p => p.Id).SequenceEqual(hand)
                && player.BoardZone.Slots.Select(p => p.Id).SequenceEqual(board) && match.HumanOptionsId == options.Id,
                $"rejected {cardId} placement after {change} changed mana/hand/board/options");
            player.UsedMana = 0;
            while (player.BoardZone.IsFull) player.BoardZone.Remove((Minion)player.BoardZone.GetSlot(player.BoardZone.SlotCount - 1));
            Check(match.ValidateOption(options.Id, index, 0, -1, 2) is PlayCardTask,
                "pending placement options must remain answerable");
            match.SendOption(options.Id, index, 0, -1, 2);
            Check(player.BoardZone.GetSlot(1) == source, "pending valid placement did not succeed");
        }
        Console.WriteLine("PASS stale native placement: current slots/legal tasks, location/minion rejection without mutation, pending options usable");
    }

    private static void StaleLocationOptions()
    {
        foreach (bool bounce in new[] { false, true })
        {
            var capture = new Capture(); var match = StartAtHumanTurn(capture); var game = match.Game;
            var source = (BoardObject)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("JAIL_877"), zone:game.Player1.BoardZone);
            FlushHistory(match);
            typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
            var options = capture.Last<AllOptions>(14);
            int index = options.Options.FindIndex(o => o.MainOption?.Id == source.Id);
            if (bounce) { game.Player1.BoardZone.Remove(source); game.Player1.HandZone.Add(source); }
            else { source.Destroy(); game.DeathProcessingAndAuraUpdate(); }
            AssertRejectedWithoutMutation(match, capture, () => match.ValidateOption(options.Id, index, 0, -1, 0));
        }
    }

    private static void ExpiredLocationActivation()
    {
        foreach (string cardId in new[] { "CORE_REV_990", "JAIL_877" })
        {
            var capture = new Capture(); var match = StartAtHumanTurn(capture); var game = match.Game;
            var player = game.Player1;
            var source = (BoardObject)SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId(cardId), zone:player.BoardZone);
            var victim = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("CS2_182"), zone:game.Player2.BoardZone);
            var wisp = Generic.DrawCard(player, Cards.FromId("CS2_231"));
            int target = cardId == "CORE_REV_990" ? victim.Id : 0;
            FlushHistory(match);
            void Publish() => typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
            Publish(); var options = capture.Last<AllOptions>(14);
            int index = options.Options.FindIndex(o => o.MainOption?.Id == source.Id);
            Check(match.ValidateOption(options.Id, index, target, -1, 0) is ActivateLocationTask,
                "activation must initially be valid");
            Check(game.Process(player.Options().First(t => t is PlayCardTask && t.Source == wisp)),
                "intervening legal action failed");
            FlushHistory(match);
            Check(LocationRules.CanActivate(player, source, target == 0 ? null : victim),
                "intervening action must leave location ready");
            int durability = source.Durability, mana = player.RemainingMana;
            var hand = player.HandZone.Select(p => p.Id).ToArray();
            var board = player.BoardZone.Slots.Select(p => p.Id).ToArray();
            AssertRejectedWithoutMutation(match, capture, () => match.ValidateOption(options.Id, index, target, -1, 0));
            AssertRejectedWithoutMutation(match, capture, () => match.SendOption(options.Id, index, target, -1, 0));
            Check(source.Durability == durability && !source.IsExhausted && player.RemainingMana == mana
                && player.HandZone.Select(p => p.Id).SequenceEqual(hand) && player.BoardZone.Slots.Select(p => p.Id).SequenceEqual(board)
                && match.HumanOptionsId == options.Id, "expired activation changed state or cleared pending options");
            int end = options.Options.FindIndex(o => o.Type_ == PegasusGame.Option.Type.END_TURN);
            Check(match.ValidateOption(options.Id, end, 0, -1, 0) is EndTurnTask, "pending options left unavailable");
            Publish(); var fresh = capture.Last<AllOptions>(14);
            int freshIndex = fresh.Options.FindIndex(o => o.MainOption?.Id == source.Id);
            AssertRejectedWithoutMutation(match, capture, () => match.SendOption(options.Id, index, target, -1, 0));
            Check(match.HumanOptionsId == fresh.Id && match.ValidateOption(fresh.Id, freshIndex, target, -1, 0) is ActivateLocationTask,
                "stale input invalidated fresh options");
            match.SendOption(fresh.Id, freshIndex, target, -1, 0);
            Check(source.Durability == durability - 1 && source.IsExhausted, "fresh activation did not succeed");
        }
        Console.WriteLine("PASS expired native activation: decision identity, ready location rejected without mutation, pending/fresh options usable");
    }
    private static void GeneralizedLocationTarget()
    {
        foreach (bool stale in new[] { false, true })
        {
            var capture = new Capture(); var match = StartAtHumanTurn(capture); var game = match.Game;
            var player = game.Player1; player.BaseMana = 10; player.UsedMana = 0;
            var source = Generic.DrawCard(player, Cards.FromId("CORE_REV_023"));
            var target = (BoardObject)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("JAIL_877"), zone:game.Player2.BoardZone);
            var second = (BoardObject)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("CORE_REV_990"), zone:game.Player2.BoardZone);
            var friendly = SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("JAIL_877"), zone:player.BoardZone);
            var minion = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("CS2_182"), zone:game.Player2.BoardZone);
            FlushHistory(match);
            typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
            var options = capture.Last<AllOptions>(14);
            int index = options.Options.FindIndex(o => o.MainOption?.Id == source.Id);
            Check(index >= 0 && options.Options[index].MainOption.Targets.Select(t => t.Id).SequenceEqual(new[] { target.Id, second.Id }), "Renovator native targets must be enemy location ids only");
            var play = (PlayCardTask)match.ValidateOption(options.Id, index, target.Id, -1, 1);
            Check(play.ActionTarget == target && play.Target == null && play.Copy().ActionTarget == target, "native/Copy drops location Battlecry target");
            Check(BotAction.From(play).Target == target.Id && BotAction.From(play).Resolve(player.Options()).ActionTarget == target,
                "bot action dropped non-character target");
            foreach (int bad in new[] { 0, friendly.Id, minion.Id, player.Opponent.Hero.Id, 999999 })
                AssertRejectedWithoutMutation(match, capture, () => match.ValidateOption(options.Id, index, bad, -1, 1));
            if (stale)
            {
                target.Destroy(); game.DeathProcessingAndAuraUpdate();
                AssertRejectedWithoutMutation(match, capture, () => match.ValidateOption(options.Id, index, target.Id, -1, 1));
                AssertRejectedWithoutMutation(match, capture, () => match.SendOption(options.Id, index, target.Id, -1, 1));
                Check(match.ValidateOption(options.Id, index, second.Id, -1, 1) is PlayCardTask, "stale target rejection spoiled pending legal target");
            }
            else
            {
                match.SendOption(options.Id, index, target.Id, -1, 1);
                Check(target.Zone.Type == SabberStoneCore.Enums.Zone.GRAVEYARD && source.Zone == player.BoardZone && game.NumMinionsKilledThisTurn == 0, "native Renovator must destroy via location death path");
            }
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Renovator native location-only targets, target copy/bot roundtrip, stale rejection without mutation");
    }
    private static void LegacyPlacementParity()
    {
        var capture = new Capture(); var match = StartAtHumanTurn(capture); var game = match.Game;
        var player = game.Player1; player.BaseMana = 10; player.UsedMana = 0;
        foreach (var card in player.HandZone.ToArray()) player.HandZone.Remove(card);
        SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("CS2_182"), zone:player.BoardZone);
        var source = Generic.DrawCard(player, Cards.FromId("CS2_182"));
        FlushHistory(match);
        typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
        var options = capture.Last<AllOptions>(14);
        int index = options.Options.FindIndex(o => o.MainOption?.Id == source.Id);
        // Legacy validation matches published placement tasks even after mana changes.
        player.UsedMana = 10;
        for (int position = 1; position <= 2; position++)
            Check(match.ValidateOption(options.Id, index, 0, -1, position) is PlayCardTask play && play.ZonePosition == position - 1,
                "location-free placement must retain original published-task validation");
        AssertRejectedWithoutMutation(match, capture, () => match.ValidateOption(options.Id, index, 0, -1, 0));
        AssertRejectedWithoutMutation(match, capture, () => match.ValidateOption(options.Id, index, 0, -1, 3));
        Console.WriteLine("PASS location-free minion placement validation parity");
    }
    private static void LocationOptions()
    {
        var capture = new Capture();
        var match = StartAtHumanTurn(capture);
        var game = match.Game;
        var player = game.Player1;
        foreach (var old in player.HandZone.ToArray()) player.HandZone.Remove(old);
        player.BaseMana = 10; player.UsedMana = 0;
        var left = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("CS2_182"), zone: player.BoardZone);
        var right = SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("CS2_182"), zone: player.BoardZone);
        var victim = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("CS2_231"), zone: game.Player2.BoardZone);
        var card = (BoardObject)Generic.DrawCard(player, Cards.FromId("CORE_REV_990"));
        game.DeathProcessingAndAuraUpdate();
        FlushHistory(match);
        void Publish() => typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
        Publish();
        var options = capture.Last<AllOptions>(14);
        int index = options.Options.FindIndex(o => o.MainOption?.Id == card.Id);
        Check(index >= 0, "location hand play missing");
        for (int position = 1; position <= player.BoardZone.SlotCount + 1; position++)
        {
            var task = match.ValidateOption(options.Id, index, 0, -1, position);
            Check(task is PlayCardTask play && play.Source == card && play.ZonePosition == position - 1,
                "location insertion must match exact physical task");
        }
        void Reject(Action action)
        {
            string State() => string.Join("|", game.IdEntityDic.Values.OrderBy(e => e.Id).Select(e =>
                $"{e.Id}:{e.Card.Id}:" + string.Join(",", e.NativeTags.OrderBy(t => (int)t.Key))))
                + $"/{card.Location.UsesCompleted}/{game.PowerHistory.Last.Count}/{game.NextCloneIndex}";
            var rng = (Util.DeepCloneableRandom)typeof(SabberStoneCore.Model.Game)
                .GetProperty("Random", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
            var randomBefore = rng.CaptureState();
            string before = State(); int packets = capture.Packets.Count;
            AssertRejectedWithoutMutation(match, capture, action);
            Check(before == State() && randomBefore.Equals(rng.CaptureState()) && packets == capture.Packets.Count,
                "rejected location input changed state/RNG/history");
        }
        Reject(() => match.SendOption(options.Id, index, 0, -1, 0));
        Reject(() => match.SendOption(options.Id, index, 0, -1, player.BoardZone.SlotCount + 2));
        match.SendOption(options.Id, index, 0, -1, 2);
        Check(player.BoardZone.GetSlot(1) == card, "native location middle placement");
        Reject(() => match.SendOption(options.Id, index, 0, -1, 2));
        var tasks = player.Options();
        var kettle = PowerOptionsBuilder.AllOptions(game, tasks);
        var power = kettle.PowerOptionList.SingleOrDefault(o => o.MainOption?.EntityId == card.Id);
        Check(power != null && power.OptionType == OptionType.POWER && power.MainOption.PowerKeyword == 0
            && power.SubOptions.Count == 0 && power.MainOption.Targets.SequenceEqual(tasks.OfType<ActivateLocationTask>()
                .Select(t => t.ActionTarget.Id).Distinct()), "Kettle location POWER main option/targets");
        Check(kettle.PowerOptionList.Last() == power, "activation must follow every legacy option");
        options = capture.Last<AllOptions>(14);
        index = options.Options.FindIndex(o => o.MainOption?.Id == card.Id);
        Check(match.ValidateOption(options.Id, index, victim.Id, -1, 0) is ActivateLocationTask activation
            && activation.Source == card && activation.ActionTarget == victim, "exact activation task resolution");
        Reject(() => match.SendOption(options.Id + 1, index, victim.Id, -1, 0));
        Reject(() => match.SendOption(options.Id, index, game.Player2.Hero.Id, -1, 0));
        Reject(() => match.SendOption(options.Id, index, 999999, -1, 0));
        Reject(() => match.SendOption(options.Id, index, victim.Id, 0, 0));
        Reject(() => match.SendOption(options.Id, index, victim.Id, -1, 1));
        match.SendOption(options.Id, index, victim.Id, -1, 0);
        Check(card.Durability == 2 && card.IsExhausted, "native activation spent once");
        Reject(() => match.SendOption(options.Id, index, victim.Id, -1, 0));
        var network = Generic.DrawCard(player, Cards.FromId("JAIL_877"));
        FlushHistory(match); Publish(); options = capture.Last<AllOptions>(14);
        index = options.Options.FindIndex(o => o.MainOption?.Id == network.Id);
        for (int position = 1; position <= player.BoardZone.SlotCount + 1; position++)
            Check(match.ValidateOption(options.Id, index, 0, -1, position) is PlayCardTask placed
                && placed.ZonePosition == position - 1, "mixed-board insertion positions include location slot");
        match.SendOption(options.Id, index, 0, -1, player.BoardZone.SlotCount + 1);
        options = capture.Last<AllOptions>(14); index = options.Options.FindIndex(o => o.MainOption?.Id == network.Id);
        Check(options.Options[index].MainOption.Targets.Count == 0 && options.Options[index].SubOptions.Count == 0
            && match.ValidateOption(options.Id, index, 0, -1, 0) is ActivateLocationTask untargeted && untargeted.ActionTarget == null,
            "untargeted Network main POWER option");
        Reject(() => match.SendOption(options.Id, index, victim.Id, -1, 0));
        Reject(() => match.SendOption(options.Id, index, 0, 0, 0));
        match.SendOption(options.Id, index, 0, -1, 0);
        Check(player.BoardZone.Any(m => m.Card.Id == "JAIL_877t"), "native untargeted Network activation");
        game.Process(EndTurnTask.Any(player)); FlushHistory(match);
        Reject(() => match.SendOption(options.Id, index, victim.Id, -1, 0));
        Check(card.IsExhausted && card[GameTag.LOCATION_ACTION_COOLDOWN] == 1,
            "native location stays closed on opponent turn");
        game.Process(EndTurnTask.Any(player.Opponent)); FlushHistory(match); Publish();
        options = capture.Last<AllOptions>(14);
        Check(card.IsExhausted && card[GameTag.LOCATION_ACTION_COOLDOWN] == 0 &&
            options.Options.All(o => o.MainOption?.Id != card.Id && o.MainOption?.Id != network.Id),
            "native POWER options absent on skipped owner turn");
        game.Process(EndTurnTask.Any(player)); game.Process(EndTurnTask.Any(player.Opponent));
        FlushHistory(match); Publish(); options = capture.Last<AllOptions>(14);
        index = options.Options.FindIndex(o => o.MainOption?.Id == network.Id);
        Check(!card.IsExhausted && card[GameTag.LOCATION_ACTION_COOLDOWN] == 0 && index >= 0 &&
            match.ValidateOption(options.Id, index, 0, -1, 0) is ActivateLocationTask,
            "native POWER option returns on following owner turn");
        match.SendOption(options.Id, index, 0, -1, 0);
        Check(network.Zone == player.GraveyardZone, "native reopened location final charge resolves");
        capture.SerializeAll();
        LegacyPlacementParity();
        StalePlacementOptions();
        ExpiredLocationActivation();
        StaleLocationOptions();
        GeneralizedLocationTarget();

        // Literal legacy ordering: end, hand sources, minion attacks, hero attacks, hero power.
        var legacy = new Capture(); var legacyMatch = StartAtHumanTurn(legacy);
        var g = legacyMatch.Game; g.Player1.BaseMana = 10;
        var attacker = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(g.Player1, Cards.FromId("CS2_182"), zone:g.Player1.BoardZone);
        attacker.IsExhausted = false;
        var legal = g.Player1.Options();
        var expected = new[] { 0 }.Concat(legal.Where(t => t is PlayCardTask).Select(t => t.Source.Id).Distinct())
            .Concat(legal.Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK).Select(t => t.Source.Id).Distinct())
            .Concat(legal.Any(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK) ? new[] { g.Player1.Hero.Id } : Array.Empty<int>())
            .Concat(legal.Any(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER) ? new[] { g.Player1.Hero.HeroPower.Id } : Array.Empty<int>());
        Check(PowerOptionsBuilder.AllOptions(g, legal).PowerOptionList.Select(o => o.MainOption?.EntityId ?? 0).SequenceEqual(expected),
            "location-free option order changed");
        Console.WriteLine("PASS location native options: placement, POWER targets/order, exact match, invalid/stale/duplicate/foreign/opponent rejection");
    }
}
