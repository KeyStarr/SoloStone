using System.IO.Compression;
using System.Text.Json;
using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class LegacyPoolEligibilityProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, message);
    internal static readonly string[] Ids = { "EX1_028", "GVG_070", "AT_101" };

    internal static void Run()
    {
        var definitions = ClientExport.TryCards();
        foreach (string id in Ids)
        {
            var card = Cards.FromId(id);
            Check(card.Implemented && card.Collectible, id + " registered");
            if (definitions != null)
            {
                var data = definitions[id];
                Check(card.AssetId == data.GetProperty("dbfId").GetInt32(), id + " registered pinned identity");
                Check(card.Cost == data.GetProperty("cost").GetInt32() && card.ATK == data.GetProperty("atk").GetInt32()
                    && card.Health == data.GetProperty("health").GetInt32() && (card.Text ?? "") == data.GetProperty("text").GetString(), id + " current export stats/cost/text");
                Check(card.Races.Select(r => r.ToString()).Order().SequenceEqual(data.GetProperty("races").EnumerateArray().Select(r => r.GetString()).Order()), id + " current export races");
                foreach (var tag in new[] { GameTag.STEALTH, GameTag.TAUNT, GameTag.CHARGE, GameTag.WINDFURY, GameTag.DIVINE_SHIELD })
                    Check(card[tag] == (data.GetProperty("tags").TryGetProperty(((int)tag).ToString(), out var value) ? value.GetInt32() : 0), id + " current text-relevant tag " + tag);
            }
            else ClientExport.Skip("Legacy pool eligibility cards against the client export");
            foreach (bool summon in new[] { false, true })
            {
                var game = new Game(new GameConfig { RandomSeed = 27, StartPlayer = 1, SkipMulligan = true, Shuffle = false,
                    History = true, Logging = false, Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                    Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
                game.StartGame();
                var minion = summon ? Summon(game.Player1, id) : (Minion)Play(game, game.Player1, id);
                Check(minion.AttackDamage == card.ATK && minion.Health == card.Health && minion.Cost == 5 && minion.Zone.Type == Zone.PLAY, id + " play/summon stats");
                Check(minion.HasStealth == (id == "EX1_028"), id + " printed Stealth");
                Check(Canonical.State(game) == Canonical.State(game.CloneExact(history: true)), id + " clone before attack");
                Check(Canonical.State(game) == Canonical.State(Game.LoadSnapshot(game.SaveSnapshot())), id + " snapshot before attack");
                int health = game.Player2.Hero.Health;
                Attack(game, minion, game.Player2.Hero);
                Check(game.Player2.Hero.Health == health - card.ATK && !minion.HasStealth, id + " attack damage/removes Stealth");
                Check(Canonical.State(game) == Canonical.State(game.CloneExact(history: true)), id + " clone after attack");
            }
            PoolProofEvidence.Passed(id, "LegacyPoolEligibilityProof.Run:" + id);
            Console.WriteLine("PASS legacy pool eligibility " + id);
        }
    }
}
