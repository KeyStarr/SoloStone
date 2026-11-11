using System.IO.Compression;
using System.Text.Json;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Loader;
using SabberStoneCore.CardSets.Client;
using System.Text.Json.Nodes;
using System.Reflection;

internal static class CoreReprintsProof
{
    // Current export behavior deliberately supersedes legacy identity for these batch-11 printings.
    private static readonly Dictionary<string, Action> StandardOverrides = new(StringComparer.Ordinal) {
        ["CORE_CS2_053"] = CoreLegacy.FarSightProof.Run,
        ["CORE_EX1_082"] = CoreLegacy.MadBomberProof.Run,
        ["CORE_EX1_100"] = CoreLegacy.LorewalkerChoProof.Run,
        ["CORE_EX1_103"] = CoreLegacy.ColdlightSeerProof.Run,
        ["CORE_GVG_103"] = CoreLegacy.MicroMachineProof.Run,
        ["CORE_LOOT_101"] = CoreLegacy.ExplosiveRunesProof.Run
    };

    private static void Check(bool condition, string message) => EventTraceProof.Check(condition, message);
    internal static void Run()
    {
        var aliases = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText("engine/SabberStoneCore/resources/Data/ReprintAliases.json"));
        var definitions = ClientExport.TryCards();
        Check(aliases.Count == 57, "explicit 57 Core 2026 reprints");
        LoaderBoundaries();
        AttackRegressions();
        GrizzlyHealthLoss();
        Check(Reprints.ByDbfId.Count == 57, "registry keyed by dbfId");
        var localOverrides = typeof(StandardCardAttribute).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .SelectMany(method => method.GetCustomAttributes<StandardCardAttribute>())
            .Select(spec => spec.Id).Where(aliases.ContainsKey).ToHashSet(StringComparer.Ordinal);
        var expectedOverrides = StandardOverrides.Keys.Concat(localOverrides).ToHashSet(StringComparer.Ordinal);
        Check(aliases.Where(pair => !ReferenceEquals(Cards.FromId(pair.Key).Power, Cards.FromId(pair.Value).Power))
            .Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal).SetEquals(expectedOverrides.Where(id => id != "CORE_DAL_575")), "exact declared Standard power overrides");
        Check(ReferenceEquals(Cards.FromId("CORE_DAL_575").Power, Cards.FromId("DAL_575").Power), "Khadgar deliberately shares its new summon facility with the legacy alias");
        StandardSetsProof.Run(selectedProofs: StandardOverrides.Values.Append((Action)(() => GroupProofs.Run(ProofKind.Cards))).ToArray());
        var failures = new List<string>();
        foreach (var (id, originalId) in aliases)
        {
            try
            {
                Check(Cards.All.Any(c => c.Id == id), id + " client definition missing");
                var card = Cards.FromId(id);
                var original = Cards.FromId(originalId);
                Check(ReferenceEquals(Cards.FromAssetId(card.AssetId), card), id + " dbfId lookup");
                if (definitions != null)
                {
                    var data = definitions[id];
                    Check(card.AssetId == data.GetProperty("dbfId").GetInt32(), id + " client dbfId");
                    Check(card.Name == data.GetProperty("name").GetString() && card.Text == data.GetProperty("text").GetString(), id + " client enUS strings");
                    Check(card.Cost == data.GetProperty("cost").GetInt32() && card.ATK == data.GetProperty("atk").GetInt32() && card.Health == data.GetProperty("health").GetInt32(), id + " client stats");
                }
                else ClientExport.Skip("Core reprint strings, stats and tags against the client export");
                Check(card.Collectible && (int)card.Set == 1637, id + " client collectible/Core set");
                Check(Reprints.ByDbfId[card.AssetId] == original.AssetId && Cards.IsFormatLegal(card, FormatType.FT_WILD)
                    && Cards.IsFormatLegal(card, FormatType.FT_STANDARD), id + " registry and deck legality");
                if (definitions != null)
                    foreach (var tag in definitions[id].GetProperty("tags").EnumerateObject())
                        Check(card.Tags.TryGetValue((GameTag)int.Parse(tag.Name), out int value) && value == tag.Value.GetInt32(), id + " client tag " + tag.Name);
                bool standardOverride = expectedOverrides.Contains(id);
                if (standardOverride)
                    Check(card.Implemented && StandardSets.CollectibleIds.Contains(id) && EffectModules.ByDbfId.GetValueOrDefault(card.AssetId) == id
                        && (StandardOverrides.TryGetValue(id, out var proof) ? PoolProofEvidence.HasPassed(id, proof) : PoolProofEvidence.HasPassed(id)),
                        id + " Standard override with passing per-card proof");
                else
                    Check(ReferenceEquals(card.Power, original.Power) && card.Implemented, id + " complete legacy power reused (native-only cards may have null Power)");
                Check(card.PlayRequirements.OrderBy(t => t.Key).SequenceEqual(original.PlayRequirements.OrderBy(t => t.Key)), id + " targeting requirements");
                for (int seed = 1; !standardOverride && seed <= 3; seed++)
                {
                    string expected = Scenario(original, seed);
                    string actual = NormalizePrinting(Scenario(card, seed), card, original);
                    Check(expected == actual, id + $" history differs seed={seed}: " + Difference(expected, actual));
                }
                if (!standardOverride) PoolProofEvidence.Passed(id, "CoreReprintsProof.Run:" + id);
                Console.WriteLine("PASS reprint " + id);
            }
            catch (Exception error) { failures.Add(id + ": " + error.GetBaseException().Message); }
        }
        Check(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine($"core-reprints: definitions=57; legacy equivalence={57 - expectedOverrides.Count}; Standard overrides={expectedOverrides.Count}; seeds=3; print-only rarity normalized where Basic/Core differ");
    }

    // Witchwood Grizzly whose Battlecry runs twice (Brann) against a 7-card hand loses 14 Health and must die,
    // as Dread Leviathan does; it once stayed on the board at -2 Health (transport standard-batch-1 27-vs-10 s8).
    private static void GrizzlyHealthLoss()
    {
        foreach (bool doubled in new[] { false, true })
        {
            var game = HistoryFactsProof.Fixture(); var player = game.Player1;
            if (doubled) Core2026Proof.Summon(player, "LOE_077");
            for (int i = 0; i < 7; i++) Generic.DrawCard(player.Opponent, Cards.FromId("CS2_182"));
            var grizzly = (Minion)Core2026Proof.Play(game, player, "CORE_GIL_623");
            Check(doubled ? grizzly.Zone.Type == Zone.GRAVEYARD : grizzly.Zone.Type == Zone.PLAY && grizzly.Health == 5,
                "CORE_GIL_623 " + (doubled ? "doubled Battlecry below 1 Health dies" : "single Battlecry keeps 5 Health")
                + " (" + grizzly.Zone.Type + "/" + grizzly.Health + ")");
        }
    }

    private static void AttackRegressions()
    {
        foreach (string id in new[] { "EX1_611", "CORE_EX1_611", "EX1_610", "CORE_EX1_610" })
        {
            var game = new Game(new GameConfig { RandomSeed = 1, StartPlayer = 1, SkipMulligan = true,
                History = true, Logging = false });
            game.StartGame();
            game.CurrentPlayer.BaseMana = 10;
            var secret = Generic.DrawCard(game.CurrentPlayer, Cards.FromId(id));
            Play(game, secret.Id);
            game.Process(EndTurnTask.Any(game.CurrentPlayer));
            var boar = (Minion)Entity.FromCard(game.CurrentPlayer, Cards.FromId("CS2_171"));
            Generic.SummonBlock.Invoke(game, boar, -1, null);
            var attack = game.CurrentPlayer.Options().Single(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK
                && t.Source.Id == boar.Id && t.Target == game.CurrentOpponent.Hero);
            Check(game.Process(attack), id + " legal attack canceled by secret must be accepted");
            Check(secret.Zone.Type == Zone.GRAVEYARD, id + " triggered secret spent");
            Check(boar.Zone.Type == (id.EndsWith("611") ? Zone.HAND : Zone.GRAVEYARD), id + " secret resolves before combat");
            if (id.EndsWith("611"))
                Check(boar.Cost == 3 && game.PowerHistory.Last.OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>()
                    .Any(change => change.EntityId == boar.Id && change.Tag == GameTag.COST && change.Value == 3), id + " bounced cost reaches history");
            Check(!game.Process(MinionAttackTask.Any(game.CurrentPlayer, boar, game.CurrentPlayer.Hero)), "invalid friendly attack remains rejected");
        }
        foreach (string id in new[] { "EX1_010", "CORE_EX1_010", "EX1_028", "CORE_EX1_028" })
        {
            var game = new Game(new GameConfig { RandomSeed = 1, StartPlayer = 1, SkipMulligan = true,
                History = true, Logging = false });
            game.StartGame();
            game.CurrentPlayer.BaseMana = 10;
            var source = Generic.DrawCard(game.CurrentPlayer, Cards.FromId(id));
            Play(game, source.Id);
            game.Process(EndTurnTask.Any(game.CurrentPlayer));
            game.Process(EndTurnTask.Any(game.CurrentPlayer));
            var attack = game.CurrentPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source.Id == source.Id);
            Check(game.Process(attack) && source[GameTag.STEALTH] == 0, id + " attack removes Stealth");
            Check(game.PowerHistory.Last.OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>()
                .Any(change => change.EntityId == source.Id && change.Tag == GameTag.STEALTH && change.Value == 0), id + " Stealth removal reaches history");
        }
        Console.WriteLine("PASS secret cancellation and Stealth transport regressions");
    }

    private static void LoaderBoundaries()
    {
        var original = Cards.FromId("AT_055");
        var row = new ClientOverlay.Definition {
            CardId = "PROOF_ADDITIVE", DbfId = 900001, Name = "Proof", Text = "",
            Tags = new() { ["202"] = 4, ["183"] = 1637, ["321"] = 0, ["190"] = 0 }
        };
        var document = new ClientOverlay.Document { SchemaVersion = 1, SourceBuild = "37.0.0.254477",
            ExportSha256 = new string('a', 64), Cards = new[] { row } };
        var result = ClientOverlay.AddTo(new[] { original }, document);
        Check(result.Length == 2 && ReferenceEquals(result[0], original) && !result[1].Collectible && !result[1].Taunt && result[1].Tags[GameTag.TAUNT] == 0,
            "additive loader preserves originals and zero-valued client tags");
        void Reject(string message)
        {
            bool rejected = false;
            try { ClientOverlay.AddTo(new[] { original }, document); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, message);
        }
        row.CardId = original.Id; Reject("XML cardId collision rejected");
        row.CardId = "PROOF_ADDITIVE"; row.DbfId = original.AssetId; Reject("XML dbfId collision rejected");
        row.DbfId = 900001; document.Cards = new[] { row, row }; Reject("duplicate overlay identity rejected");
        document.Cards = new[] { row }; document.SourceBuild = "untrusted"; Reject("wrong pinned build rejected");
        document.SourceBuild = "37.0.0.254477"; document.ExportSha256 = "invalid"; Reject("invalid provenance rejected");
        original = Cards.FromId("EX1_110t");
        document.ExportSha256 = new string('a', 64); row.CardId = original.Id; row.DbfId = original.AssetId; row.ScopedSnapshot = true;
        Check(ClientOverlay.AddTo(new[] { original }, document).Single() == original, "scoped token never replaces XML lookup");
        row.Tags["321"] = 1; Reject("collectible snapshot rejected"); row.Tags["321"] = 0;
        row.DbfId++; Reject("snapshot identity mismatch rejected"); row.DbfId--;
        document.Cards = new[] { row, row }; Reject("duplicate snapshots rejected");
        Console.WriteLine("PASS additive overlay boundaries");
    }

    // Basic-to-Core reprints changed rarity. Normalize only static printing metadata on this
    // card's entity definitions; never suppress gameplay tag changes or other entities.
    private static string NormalizePrinting(string history, Card card, Card original)
    {
        return string.Join('\n', history.Split('\n').Select(line => {
            if (!line.StartsWith('{')) return line;
            var node = JsonNode.Parse(line);
            if (node["type"]?.GetValue<string>() is "PowerHistoryFullEntity" or "PowerHistoryShowEntity"
                && node["Entity"]?["Name"]?.GetValue<string>() == card.Id)
            {
                node["Entity"]["Name"] = original.Id;
                node["Entity"]["Tags"]["203"] = original[GameTag.RARITY];
                return node.ToJsonString();
            }
            return line.Replace(card.Id, original.Id, StringComparison.Ordinal);
        }));
    }

    private static string Difference(string expected, string actual)
    {
        int at = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(i => expected[i] != actual[i], -1);
        return at < 0 ? $"length {expected.Length}/{actual.Length}" : $"at {at}, expected {expected.Substring(Math.Max(0, at - 40), Math.Min(100, expected.Length - Math.Max(0, at - 40)))}; actual {actual.Substring(Math.Max(0, at - 40), Math.Min(100, actual.Length - Math.Max(0, at - 40)))}";
    }

    private static string Scenario(Card card, int seed)
    {
        var game = new Game(new GameConfig {
            RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            Logging = false, History = true, FormatType = FormatType.FT_WILD,
            Player1HeroClass = CardClass.ROGUE, Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList()
        });
        game.StartGame();
        var player = game.CurrentPlayer;
        player.BaseMana = 10; player.UsedMana = 0;
        player.Hero.Damage = 10; player.Opponent.Hero.Damage = 8;
        // Boards cover targeting, auras, tribe buffs and random effects.
        foreach (var owner in new[] { player, player.Opponent })
        foreach (string id in new[] { "EX1_506a", "NEW1_018", "EX1_043" })
            Generic.SummonBlock.Invoke(game, (Minion)Entity.FromCard(owner, Cards.FromId(id)), -1, null);
        player.BoardZone[0].Damage = 1;
        Generic.DrawCard(player, Cards.FromId("EX1_043")); // Dragon for Chillmaw.
        var weapon = Generic.DrawCard(player, Cards.FromId("CS2_082"));
        Play(game, weapon.Id);
        player.UsedMana = 0;
        var source = Generic.DrawCard(player, card);
        Play(game, source.Id);
        // Follow-up spell/Murloc, both turns and attacks exercise delayed effects/secrets.
        foreach (string id in new[] { "CS2_024", "EX1_506", "CS2_231" })
        {
            player.UsedMana = 0;
            Play(game, Generic.DrawCard(player, Cards.FromId(id)).Id, required: false);
        }
        for (int turn = 0; turn < 4 && game.State != State.COMPLETE; turn++)
        {
            Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "end turn accepted");
            game.CurrentPlayer.BaseMana = 10; game.CurrentPlayer.UsedMana = 0;
            foreach (string id in new[] { "CS2_024", "EX1_506", "CS2_231" })
            {
                game.CurrentPlayer.UsedMana = 0;
                Play(game, Generic.DrawCard(game.CurrentPlayer, Cards.FromId(id)).Id, required: false);
            }
            for (int attacks = 0; attacks < 10; attacks++)
            {
                var attack = game.CurrentPlayer.Options().FirstOrDefault(t => t.PlayerTaskType is PlayerTaskType.MINION_ATTACK or PlayerTaskType.HERO_ATTACK);
                if (attack == null) break;
                game.Process(attack); // A secret can cancel an otherwise legal attack.
            }
        }
        if (game.State != State.COMPLETE)
        {
            game.CurrentPlayer.BaseMana = 10; game.CurrentPlayer.UsedMana = 0;
            Play(game, Generic.DrawCard(game.CurrentPlayer, Cards.FromId("EX1_312")).Id);
        }
        return Canonical.History(game);
    }

    private static void Play(Game game, int source, bool required = true)
    {
        var task = game.CurrentPlayer.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source)
            .OrderByDescending(t => t.Target?.Controller == game.CurrentPlayer.Opponent).ThenBy(t => t.Target?.Id ?? 0).FirstOrDefault();
        if (required) Check(task != null, "legal play missing: " + game.IdEntityDic[source].Card.Id);
        if (task != null)
        {
            bool accepted = game.Process(task);
            if (required) Check(accepted, "legal play rejected: " + game.IdEntityDic[source].Card.Id);
        }
    }
}
