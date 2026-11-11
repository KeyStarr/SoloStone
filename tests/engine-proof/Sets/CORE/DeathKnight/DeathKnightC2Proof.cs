using System.IO.Compression;
using System.Text.Json;
using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class DeathKnightC2Proof
{
    internal static void Case(string id, Action<Game, Controller, string> scenario,
        [System.Runtime.CompilerServices.CallerFilePath] string proofFile = "") =>
        StandardSetsProof.Case(id, (baseline, _, cardId) => {
            var game = new Game(new GameConfig { EnableCorpses = true, RandomSeed = baseline.Seed,
                StartPlayer = 1, Shuffle = false, SkipMulligan = true, History = true, Logging = false,
                Player1HeroClass = CardClass.DEATHKNIGHT, Player2HeroClass = CardClass.MAGE,
                FormatType = FormatType.FT_STANDARD,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame();
            foreach (var owner in new[] { game.Player1, game.Player2 })
            {
                foreach (var card in owner.HandZone.ToArray()) { owner.HandZone.Remove(card); owner.SetasideZone.Add(card); }
                owner.BaseMana = 10;
            }
            scenario(game, game.Player1, cardId);
            Core2026Proof.Check(Canonical.State(game) == Canonical.State(game.CloneExact(history: true)), "c2 final exact clone");
            Core2026Proof.Check(Canonical.State(game) == Canonical.State(Game.LoadSnapshot(game.SaveSnapshot())), "c2 final snapshot");
        }, proofFile);

    internal static void Continue(Game game, Action<Game> action)
    {
        var copies = new[] { game, game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()) };
        var histories = copies.Select(g => g.PowerHistory.Full.Count).ToArray();
        foreach (var copy in copies) action(copy);
        Core2026Proof.Check(copies.Select(g => Canonical.State(g)).Distinct().Count() == 1, "c2 continuation state matches clone/snapshot");
        Core2026Proof.Check(copies.Select(g => Canonical.Value(g.Random.CaptureState())).Distinct().Count() == 1, "c2 continuation RNG matches");
        Core2026Proof.Check(copies.Select((g, i) => string.Join("\n", g.PowerHistory.Full.Skip(histories[i]).Select(e => Canonical.Value(e)))).Distinct().Count() == 1,
            "c2 continuation history matches");
    }

    internal static void Dependency(string id, int dbfId)
    {
        var card = Cards.FromId(id);
        Core2026Proof.Check(card != null && card.Implemented && card.AssetId == dbfId, "registered c2 dependency " + id);
        if (ClientExport.TryCards() is not { } export) { ClientExport.Skip("Core death knight dependencies against the client export"); return; }
        var row = export[id];
        Core2026Proof.Check(dbfId == row.GetProperty("dbfId").GetInt32()
            && card.Cost == row.GetProperty("cost").GetInt32() && card.ATK == row.GetProperty("atk").GetInt32()
            && card.Health == row.GetProperty("health").GetInt32() && card.Text == row.GetProperty("text").GetString(), "exact c2 dependency " + id);
        foreach (var tag in row.GetProperty("tags").EnumerateObject())
            Core2026Proof.Check(card.Tags.GetValueOrDefault((GameTag)int.Parse(tag.Name)) == tag.Value.GetInt32(), "dependency tag " + id + ":" + tag.Name);
    }
}
