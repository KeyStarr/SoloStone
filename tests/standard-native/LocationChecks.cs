using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

static partial class Program
{
    static void LocationProjection()
    {
        foreach (string id in new[] { "CORE_REV_990", "JAIL_877" })
        {
            var definition = Cards.FromId(id);
            foreach (var tag in new[] { GameTag.CARDTYPE, GameTag.HEALTH, GameTag.COST, GameTag.LOCATION_ACTION_COST, GameTag.LOCATION_ACTION_COOLDOWN })
            {
                Check(NativeUIProjection.TryGetDefinitionTag(true, true, true, id, (int)tag, out int value)
                    && value == definition[tag], "location definition tag: " + id + ":" + tag);
                Check(!NativeUIProjection.TryGetDefinitionTag(false, true, true, id, (int)tag, out _), "definition context escaped");
            }
        }
        foreach (int tag in new[] { 2352, 2353 })
            Check(!NativeUIProjection.TryGetDefinitionTag(true, true, true, "CS2_182", tag, out _), "location tag admitted on minion");
        var packets = new List<(int Id, IProtoBuf Message)>();
        var match = new NativeMatch((id, message) => packets.Add((id, message)));
        match.Start();
        var mulligan = (EntityChoices)packets.Last(p => p.Id == 17).Message;
        match.SendChoices(mulligan.Id, new List<int>());
        var game = match.Game; var player = game.Player1;
        var location = (BoardObject)SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("JAIL_877"), zone:player.BoardZone);
        typeof(NativeMatch).GetMethod("SendHistory", Private).Invoke(match, new object[] { game.PowerHistory.Last });
        var show = SabberStoneCore.Kettle.PowerHistoryBuilder.ShowEntity(location);
        var convert = typeof(NativeMatch).GetMethod("ToNativeHistory", Private);
        location[GameTag.DAMAGE] = location[GameTag.HEALTH] - 1; location.IsExhausted = true; location[GameTag.LOCATION_ACTION_COOLDOWN] = 1;
        var historical = ((PowerHistoryData)convert.Invoke(match, new object[] { show })).ShowEntity;
        int Tag(PowerHistoryEntity entity, GameTag tag) => entity.Tags.FirstOrDefault(t => t.Name == (int)tag)?.Value ?? 0;
        Check(Tag(historical, GameTag.HEALTH) == 2 && Tag(historical, GameTag.LOCATION_ACTION_COOLDOWN) == 0,
            "location history read later live state");
        var current = ((PowerHistoryData)convert.Invoke(match, new object[] { SabberStoneCore.Kettle.PowerHistoryBuilder.ShowEntity(location) })).ShowEntity;
        Check(Tag(current, GameTag.CARDTYPE) == 39 && Tag(current, GameTag.HEALTH) == 2 && Tag(current, GameTag.COST) == 2
            && Tag(current, GameTag.EXHAUSTED) == 1 && Tag(current, GameTag.LOCATION_ACTION_COOLDOWN) == 1 && Tag(current, GameTag.DAMAGE) == 1,
            "location current durability/readiness in history");
        foreach (var tag in new[] { GameTag.LOCATION_ACTION_COST, GameTag.LOCATION_ACTION_COOLDOWN })
        {
            var change = (PowerHistoryData)convert.Invoke(match, new object[] { SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(location.Id, tag, 1) });
            Check(change.HasTagChange && change.TagChange.Tag == (int)tag && change.TagChange.Value == 1, "location tag filtered outbound");
        }
        var changed = new SabberStoneCore.Kettle.PowerHistoryChangeEntity { CardId = location.Card.Id,
            Entity = new SabberStoneCore.Kettle.PowerHistoryEntity { Id = location.Id, Name = location.Card.Id,
                Tags = new Dictionary<GameTag, int> { [GameTag.CARDTYPE] = 39, [GameTag.HEALTH] = 2, [GameTag.DAMAGE] = 1,
                    [GameTag.EXHAUSTED] = 1, [GameTag.LOCATION_ACTION_COOLDOWN] = 1 } } };
        var advanced = ((PowerHistoryData)convert.Invoke(match, new object[] { changed })).ChangeEntity;
        Check(Tag(advanced, GameTag.HEALTH) == 2 && Tag(advanced, GameTag.DAMAGE) == 1 && Tag(advanced, GameTag.LOCATION_ACTION_COOLDOWN) == 1
            && !advanced.Tags.Any(t => t.Name == (int)GameTag.NUM_ATTACKS_THIS_TURN || t.Name == (int)GameTag.TAUNT),
            "location CHANGE_ENTITY acquired minion resets");
        Console.WriteLine("PASS location projection: narrow definition tags; historical/current durability/cooldown; outbound tags; CHANGE_ENTITY preserves state");
    }
}
