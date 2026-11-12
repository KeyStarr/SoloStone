using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;
using Game = SabberStoneCore.Model.Game;
using Card = SabberStoneCore.Model.Card;
using Entity = SabberStoneCore.Model.Entities.Entity;

internal static class KeywordsCfaProof
{
    private static void Check(bool value, string message) => SelfChecks.Check(value, "CFA " + message);
    internal static void Run()
    {
        // Metadata-only Fabled deck: no unimplemented card effects are played or registered by this fixture.
        using var stream = new GZipStream(File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz"), CompressionMode.Decompress);
        using var export = JsonDocument.Parse(stream);
        var definitions = export.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(c => c.GetProperty("cardId").GetString());
        var ids = FabledRules.ExpandForDeckbuilding(new[] { "TIME_609" }).Concat(Enumerable.Repeat("CS2_182", 27)).ToArray();
        var deck = ids.Select(id => {
            if (id == "CS2_182") return Cards.FromId(id);
            var row = definitions[id];
            return new Card(id, row.GetProperty("dbfId").GetInt32(), row.GetProperty("tags").EnumerateObject()
                .Select(t => new Tag((GameTag)int.Parse(t.Name), t.Value.GetInt32())).ToArray(),
                new Dictionary<PlayReq, int>(), Array.Empty<string>(), Array.Empty<Tag>());
        }).ToList();
        Check(!FabledRules.DeckErrors(ids).Any() && deck.Count == 30, "bundle uses three normal deck slots");
        Check(deck.Take(3).All(c => c.ExcludeFromLegacyRandomPools), "Fabled metadata excluded from legacy generation");
        foreach (int actor in new[] { 1, 2 })
        {
            var game = new Game(new GameConfig { RandomSeed = 71, StartPlayer = actor, SkipMulligan = true,
                Shuffle = false, History = true, Logging = false, Player1Deck = deck,
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame();
            Check(game.Player1.HandZone.Concat(game.Player1.DeckZone).Count(e => ids.Take(3).Contains(e.Card.Id)) == 3,
                "companions are ordinary starting hand/deck entities");
            NativeSession session = null;
            var match = new NativeMatch((id, packet) => session.Receive(id, packet));
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            session = new NativeSession(match); session.Flush();
            void Observe() => Check(!Invariants.Compare(game, session.Client).Any(), "strict board/deck parity");
            Observe();
            var owner = game.CurrentPlayer; owner.BaseMana = 10;
            var body = (Minion)Generic.DrawCard(owner, Cards.FromId("CATA_432")); session.Flush();
            session.Process(PlayCardTask.Any(owner, body)); Observe();
            Check(ColossalRules.AppendagesOf(body).Count() == 4 && owner.BoardZone.SlotCount == 5, "body and all heads projected");
            var green = ColossalRules.AppendagesOf(body).Single(h => h.Card.Id == "CATA_432t1");
            Check(session.Client.Entities[body.Id].Get(GameTag.TAUNT) == 1, "body initially has native Taunt");
            green.ToBeDestroyed = true; game.DeathProcessingAndAuraUpdate(); session.Flush(); Observe();
            Check(!body.HasTaunt && session.Client.Entities[body.Id].Get(GameTag.TAUNT) == 0,
                "Green Head death removes body Taunt in native client");
            foreach (var head in ColossalRules.AppendagesOf(body).ToArray())
            {
                head.ToBeDestroyed = true; game.DeathProcessingAndAuraUpdate(); session.Flush(); Observe();
            }
            Check(!body.HasTaunt && !body.HasLifeSteal && !body.CantBeTargetedBySpells && !body.HasDivineShield, "Deathrattle keyword changes projected");
            var scaled = (Minion)Entity.FromCard(owner.Opponent, Cards.FromId("CATA_432t1"));
            ColossalRules.ConfigureAppendage(scaled, 2); Generic.SummonBlock(game, scaled, -1, null);
            game.DeathProcessingAndAuraUpdate(); session.Flush(); Observe();
            Check(session.Client.Entities[scaled.Id].Get(ColossalRules.RealizedFactor) == 0, "private scaling guard never sent");
        }
        Console.WriteLine("PASS CFA transport: Colossal body/heads/keyword removal/private guard, metadata-only Fabled 30-card deck; both seats; violations=0");
    }
}
