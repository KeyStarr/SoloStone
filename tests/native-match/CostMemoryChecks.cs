using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    private static void CostMemoryChecks()
    {
        ManaDefinitionChanges();
        RetainedDiscountLifetime();
        foreach (string id in new[] { "JAIL_501", "BE_142" })
        {
            var capture = new Capture(); var match = StartAtHumanTurn(capture);
            var game = match.Game; var player = game.Player1;
            foreach (var held in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(held));
            player.BaseMana = 6; player.UsedMana = 0; player.TemporaryMana = 0;
            var source = Generic.DrawCard(player, Cards.FromId(id));
            var hidden = Generic.DrawCard(game.Player2, Cards.FromId(id));
            var target = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("CS2_200"), zone: game.Player2.BoardZone);
            target.Health = 9;
            game.DeathProcessingAndAuraUpdate(); FlushHistory(match);
            var tags = ReplayNativeEntityTags(NativeHistory(capture), source.Id);
            Check(tags[(int)GameTag.COST] == 6 && tags[2] == 6, id + " native prepayment Cost/text");
            if (source is Minion) Check(tags[(int)GameTag.ATK] == 6 && tags[(int)GameTag.HEALTH] == 6, "native Picklock stats");
            Check(!ReplayNativeEntityTags(NativeHistory(capture), hidden.Id).ContainsKey(2), "opponent Mana numbers remain private");
            Check(game.Process(PlayCardTask.Any(player, source, id == "JAIL_501" ? target : null)), id + " dynamic play");
            FlushHistory(match);
            tags = ReplayNativeEntityTags(NativeHistory(capture), source.Id);
            Check(tags[(int)GameTag.TAG_LAST_KNOWN_COST_IN_HAND] == 6, id + " committed Cost projected");
            var result = player.BoardZone.Single();
            tags = ReplayNativeEntityTags(NativeHistory(capture), result.Id);
            Check(tags[(int)GameTag.ATK] == 6 && tags[(int)GameTag.HEALTH] == 6, id + " frozen board stats");
            Check(NativeHistory(capture).All(e => !e.HasTagChange || !CostRules.IsInternalTag((GameTag)e.TagChange.Tag)), "private memory tags absent from wire");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS cost memory native dynamic hand/display/payment/privacy");
    }
    private static void ManaDefinitionChanges()
    {
        var capture = new Capture(); var match = StartAtHumanTurn(capture);
        var game = match.Game; var player = game.Player1;
        player.BaseMana = 6; player.UsedMana = 0; player.TemporaryMana = 0;
        var source = Generic.DrawCard(player, Cards.FromId("JAIL_501"));
        game.AuraUpdate(); FlushHistory(match);
        source = Generic.ChangeEntityBlock(player, source, Cards.FromId("CS2_231"), true);
        game.AuraUpdate(); FlushHistory(match);
        Check(SentTextTag(capture, source.Id, 2) == 0, "transform clears old mana text on native script tag 2");
        player.BaseMana = 3;
        source = Generic.ChangeEntityBlock(player, source, Cards.FromId("JAIL_501"), true);
        game.AuraUpdate(); FlushHistory(match);
        var tags = ReplayNativeEntityTags(NativeHistory(capture), source.Id);
        Check(tags[(int)GameTag.COST] == 3 && tags[(int)GameTag.ATK] == 3
            && tags[(int)GameTag.HEALTH] == 3 && tags[2] == 3,
            "native round-trip has fresh mana Cost/stats/text");
        Check(NativeHistory(capture).All(e => !e.HasTagChange || !CostRules.IsInternalTag((GameTag)e.TagChange.Tag)),
            "definition resets never project private memory tags");
        capture.SerializeAll();
    }
    private static void RetainedDiscountLifetime()
    {
        var capture = new Capture(); var match = StartAtHumanTurn(capture);
        var game = match.Game; var player = game.Player1;
        foreach (var held in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(held));
        player.BaseMana = 10; player.UsedMana = 0; player.TemporaryMana = 0;
        var source = (Minion)Generic.DrawCard(player, Cards.FromId("JAIL_453"));
        Generic.AddEnchantmentBlock(game, Cards.FromId("JAIL_453e"), player.Hero, source, 20);
        int discount = source.AppliedEnchantments.Single(e => e.Card.Id == "JAIL_453e").Id;
        Check(game.Process(PlayCardTask.Any(player, source)), "discounted native Jailbird play");
        FlushHistory(match);
        Check(SentTextTag(capture, source.Id, (int)GameTag.COST) == 0, "retained board discount projected");
        int cursor = capture.Packets.Count;
        Generic.ReturnToHandBlock(player, source); game.AuraUpdate(); FlushHistory(match);
        Check(NativeHistory(capture, cursor).Any(e => e.HasHideEntity && e.HideEntity.Entity == discount),
            "expired discount actor hidden on backward move");
        Check(SentTextTag(capture, source.Id, (int)GameTag.COST) == 5, "native bounce restores printed Cost");
        Check(game.Process(PlayCardTask.Any(player, source)), "full-Cost native Jailbird replay");
        FlushHistory(match);
        var tags = ReplayNativeEntityTags(NativeHistory(capture), source.Id);
        Check(tags[(int)GameTag.COST] == 5 && tags[(int)GameTag.TAG_LAST_KNOWN_COST_IN_HAND] == 5,
            "native replay keeps paid and battlefield Cost consistent");
        capture.SerializeAll();
    }
}
