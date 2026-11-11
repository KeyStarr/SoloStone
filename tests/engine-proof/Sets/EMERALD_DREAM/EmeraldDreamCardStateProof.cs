using System.IO.Compression;
using System.Text.Json;
using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class EmeraldDreamCardStateProof
{
    internal const string HandLeans = "Unpinned_HandCastScope Unpinned_HandUpgradeTiming Unpinned_HandFresh Unpinned_HandUpgradeCost Unpinned_HandProjection";

    internal static void Token(string id, int dbfId)
    {
        using var file = File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz");
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var doc = JsonDocument.Parse(gzip);
        var row = doc.RootElement.GetProperty("cards").EnumerateArray().Single(r => r.GetProperty("cardId").GetString() == id);
        var card = Cards.FromId(id);
        Core2026Proof.Check(card.Implemented && card.AssetId == dbfId && card.AssetId == row.GetProperty("dbfId").GetInt32()
            && card.Type.ToString() == row.GetProperty("cardType").GetString()
            && card.Cost == row.GetProperty("cost").GetInt32() && card.ATK == row.GetProperty("atk").GetInt32()
            && card.Health == row.GetProperty("health").GetInt32() && card.Text == row.GetProperty("text").GetString(), "exported token identity/stats/text " + id);
        foreach (var tag in row.GetProperty("tags").EnumerateObject())
            Core2026Proof.Check(card.Tags.GetValueOrDefault((GameTag)int.Parse(tag.Name)) == tag.Value.GetInt32(), "exported token keyword/tag " + id + ":" + tag.Name);
    }

    // Missing enrollment, wrong side/cast scope, cost retention or an off-by-one upgrade must fail here.
    internal static IPlayable UpgradeBySpells(Game g, Controller p, string id, string upgradedId)
    {
        Play(g, p, "GAME_005");
        var held = (Playable)Add(p, id); int slot = held.ZonePosition;
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "Unpinned_HandFresh earlier spells excluded");
        Play(g, p, "CS2_182");
        End(g); Play(g, p.Opponent, "GAME_005"); End(g);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "Unpinned_HandCastScope minion/enemy casts excluded");
        Play(g, p, "GAME_005");
        Core2026Proof.Check(held.Card.Id == id && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "first own completed spell");
        Play(g, p, "GAME_005");
        Core2026Proof.Check(held.Card.Id == id && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "second own completed spell");
        Effects.ReduceCost(1).ApplyTo(held);
        PoolPilotProof.Resume(g, next => {
            Play(next, next.Player1, "GAME_005");
            var result = next.IdEntityDic[held.Id];
            Core2026Proof.Check(result.Card.Id == upgradedId && result.Id == held.Id && result.ZonePosition == slot
                && result[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && result.Cost == Cards.FromId(upgradedId).Cost,
                HandLeans + " third spell upgrades same identity/slot and resets local cost");
        });
        return g.IdEntityDic[held.Id];
    }

    internal static void HiddenUpgrade(string id, string upgradedId, bool turnEnds = false)
    {
        var g = HistoryFactsProof.Fixture(); var owner = g.Player2;
        var held = Entity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone);
        End(g);
        if (turnEnds) for (int i = 0; i < 5; i++) End(g);
        else for (int i = 0; i < 3; i++) Play(g, owner, "GAME_005");
        Core2026Proof.Check(g.IdEntityDic[held.Id].Card.Id == upgradedId, "hidden owner actually upgrades");
        var entries = new List<PegasusGame.PowerHistoryData>();
        var match = new NativeMatch((packetId, message) => {
            using var stream = new MemoryStream(); message.Serialize(stream);
            if (packetId != 19) return;
            stream.Position = 0; var history = new PegasusGame.PowerHistory(); history.Deserialize(stream);
            entries.AddRange(history.List);
        });
        typeof(NativeMatch).GetProperty("Game").SetValue(match, g);
        typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, new object[] { g.PowerHistory.Full });
        Core2026Proof.Check(entries.Any(e => e.HasChangeEntity && e.ChangeEntity.Entity == held.Id), "hidden transformation crosses production serialization");
        foreach (var entry in entries)
        {
            var actor = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
            if (actor?.Entity == held.Id)
                Core2026Proof.Check(actor.Name == "" && actor.Tags.All(t => t.Name == 53 || t.Name == 50 || t.Name == 49 || t.Name == 263), "Unpinned_HandProjection hidden identity/counter/keywords masked");
            if (entry.HasTagChange && entry.TagChange.Entity == held.Id)
                Core2026Proof.Check(new[] { 53, 50, 49, 263 }.Contains(entry.TagChange.Tag), "hidden tag changes structural only");
        }
        var fair = FairBotState.Create(g, 1, 42, DeckStyle.Control);
        var view = (Game)typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fair);
        var hidden = view.Player2.HandZone[g.IdEntityDic[held.Id].ZonePosition];
        Core2026Proof.Check(hidden is Unknown && hidden.Card.HandProgress == null
            && hidden[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "fair bot sees Unknown without held progress");
    }
}
