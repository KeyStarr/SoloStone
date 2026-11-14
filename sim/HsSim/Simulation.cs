using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using System.Text.Json.Serialization;
#if SOURCE_ENGINE
using System.Security.Cryptography;
using System.Xml.Linq;
#endif

namespace HsSim;
using Game = SabberStoneCore.Model.Game;
using Card = SabberStoneCore.Model.Card;

public sealed class GameRecord
{
    public int? ReplaySchema { get; set; }
    public string PoolPolicyVersion { get; set; }
    public string PoolManifestHash { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string DarkGiftPolicyVersion { get; set; }
    public string Id { get; set; }
    public string Suite { get; set; }
    public string Player { get; set; }
    public string Opponent { get; set; }
    public int Seed { get; set; }
    public string Policy { get; set; } = "random";
    public List<int> Actions { get; set; } = new();
    public List<string> LegalShas { get; set; } = new();
    public int Turns { get; set; }
    public int Winner { get; set; }
    // Hash of the whole power history. Goldens no longer store it (read from older files, never written there): every
    // presentation fix moved it in most games, so any two engine changes conflicted on it. Goldens pin the option and choice
    // packets (DecisionsSha) and the final state instead; the client-tier transport checks look at the history itself.
    public string HistorySha { get; set; }
    public string DecisionsSha { get; set; }
    public string StateSha { get; set; }
    public List<int> ClonePoints { get; set; }
    public string Failure { get; set; }

    // The record as a golden file stores it: without the history hash.
    public GameRecord ForGolden()
    {
        var copy = (GameRecord)MemberwiseClone();
        copy.HistorySha = null;
        return copy;
    }
}

public sealed record DeckSpec(string Id, string Hero, string OpponentName, string[] Cards);
public static class Simulation
{
    public static readonly string[] ModernIds = { "proof-mage", "proof-warrior" };
    public static DeckSpec Deck(string id)
    {
#if SOURCE_ENGINE
        if (id.StartsWith("death-knight-v1-", StringComparison.Ordinal))
        {
            string common = "CORE_EDR_002 CORE_RLK_062 CORE_RLK_121 EDR_816 JAIL_440 RLK_708 RLK_958 TLC_443";
            string package = id switch {
                "death-knight-v1-rainbow" => "BE_284 BE_325 CATA_467 CATA_469 EDR_812 JAIL_441 JAIL_445 JAIL_454 TLC_439",
                "death-knight-v1-frost" => "TIME_611 TLC_401",
                "death-knight-v1-unholy" => "JAIL_450",
                _ => throw new ArgumentException("unknown Death Knight fixture: " + id)
            };
            string[] selected = (common + " " + package).Split(' ');
            string[] fillers = ("CS2_179 CS2_182 CS2_200 CS2_222 CS2_120 CS2_171 CS2_172 CS2_173 "
                + "EX1_015 EX1_019 EX1_096 EX1_066 EX1_007 EX1_556 EX1_284").Split(' ');
            string[] cards = selected.Concat(Enumerable.Range(0, 30 - selected.Length).Select(i => fillers[i % fillers.Length])).ToArray();
            var definitions = cards.Select(Cards.FromId).ToArray();
            if (definitions.Any(c => c == null || !c.Implemented || !c.Collectible
                    || !SabberStoneCore.Actions.CardClassRules.IsAllowedInDeck(c, CardClass.DEATHKNIGHT))
                || !SabberStoneCore.Actions.RuneRules.IsLegalForClass(SabberStoneCore.Actions.RuneRules.FromCards(definitions), CardClass.DEATHKNIGHT))
                throw new InvalidOperationException("invalid registered/rune-legal Death Knight fixture: " + id);
            return new(id, "HERO_11", "Death Knight v1", cards);
        }
        // Versioned fixtures: never derive pool goldens from the growing registry.
        if (id == "pool-pilot-mage-v1")
            return new(id, "HERO_08", "Pool pilot Mage v1", (
                "CATA_484 CATA_723 BE_854 JAIL_125 BE_220 EDR_530 TIME_040 JAIL_328 EDR_848 CS2_029 " +
                "CATA_484 CATA_723 BE_854 JAIL_125 BE_220 EDR_530 TIME_040 JAIL_328 EDR_848 CS2_029 " +
                "CATA_484 CATA_723 BE_854 JAIL_125 BE_220 EDR_530 TIME_040 JAIL_328 EDR_848 CS2_029").Split(' '));
        if (id == "pool-pilot-paladin-v1")
            return new(id, "HERO_04", "Pool pilot Paladin v1", (
                "JAIL_328 EDR_848 JAIL_125 BE_220 BE_854 CS2_089 " +
                "JAIL_328 EDR_848 JAIL_125 BE_220 BE_854 CS2_089 " +
                "JAIL_328 EDR_848 JAIL_125 BE_220 BE_854 CS2_089 " +
                "JAIL_328 EDR_848 JAIL_125 BE_220 BE_854 CS2_089 " +
                "JAIL_328 EDR_848 JAIL_125 BE_220 BE_854 CS2_089").Split(' '));
        if (id.StartsWith("standard-coverage-", StringComparison.Ordinal))
        {
            string cardId = id["standard-coverage-".Length..];
            if (!SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains(cardId)
                && !OfflinePoolPolicy.Load().Members.Any(member => member.CardId == cardId))
                throw new ArgumentException("unknown Standard coverage card: " + cardId);
            var cardClass = Cards.FromId(cardId).Class;
            // The legacy hero factory covers nine classes; Warrior also supports cross-class Heralds.
            var heroClass = cardClass is >= CardClass.DRUID and <= CardClass.WARRIOR ? cardClass : CardClass.WARRIOR;
            return new(id, Cards.HeroCard(heroClass).Id, "Standard coverage",
                new[] { cardId }.Concat(Enumerable.Repeat("CS2_182", 29)).ToArray());
        }
        if (id.StartsWith("standard-batch-1-", StringComparison.Ordinal))
        {
            int index = Array.IndexOf(DeckIds("standard-batch-1"), id);
            if (index < 0) throw new ArgumentException("unknown Standard batch deck: " + id);
            var batch = SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds;
            if (batch.Any(cardId => Cards.FromId(cardId).Type == CardType.LOCATION))
                // Move LOCATION cards after non-locations, preserving registry order within each group.
                batch = batch.Where(cardId => Cards.FromId(cardId).Type != CardType.LOCATION)
                    .Concat(batch.Where(cardId => Cards.FromId(cardId).Type == CardType.LOCATION)).ToArray();
            // Cross-class stress corpus, split into 30-card decks and cycled to fill the last deck.
            // Both stress heroes must support Herald even when registry growth moves cards between decks.
            return new(id, index % 2 == 0 ? "HERO_01" : "HERO_07", "Standard batch 1",
                Enumerable.Range(0, 30).Select(i => batch[(index * 30 + i) % batch.Count]).ToArray());
        }
        if (id == "standard-spike1-druid")
            // Standard card identities, deliberately repeated legendaries for engine stress (not a player deck).
            return new(id, "HERO_06", "Standard spike 1", Enumerable.Range(0, 6).SelectMany(_ => new[] {
                "EDR_209", "CORE_LOOT_137", "CORE_UNG_848", "CORE_EX1_043", "CORE_GIL_623"
            }).ToArray());
        if (id.StartsWith("core-2026-", StringComparison.Ordinal))
        {
            int index = int.Parse(id["core-2026-".Length..]) - 1;
            if (index == 3)
                return new(id, "HERO_03", "Core 2026 Tradeable", Enumerable.Range(0, 6)
                    .SelectMany(_ => new[] { "CORE_EX1_002", "CORE_EX1_005", "CS2_186", "CS2_179", "CS2_200" }).ToArray());
            if (index < 0 || index >= 3) throw new ArgumentException("unknown Core 2026 deck: " + id);
            string[] selected = SabberStoneCore.CardSets.Client.RebalancedCore2026.ByDbfId.Keys.Select(Cards.FromAssetId)
                // Keep accepted deck identities stable; new cards have their own stress deck.
                .Where(card => card.Id != "CORE_EX1_002" && card.Id != "CORE_EX1_005")
                .OrderBy(card => card.Id, StringComparer.Ordinal).Skip(index * 22).Take(22).Select(card => card.Id).ToArray();
            return new(id, "HERO_03", "Core 2026 rebalanced", selected.Concat(Enumerable.Repeat("CS2_082", 30 - selected.Length)).ToArray());
        }
        if (id is "core-reprints-1" or "core-reprints-2")
        {
            string[] reprints = SabberStoneCore.CardSets.Client.Reprints.ByDbfId.Keys.Select(Cards.FromAssetId)
                .Select(card => card.Id).Order(StringComparer.Ordinal).ToArray();
            if (reprints.Length != 57) throw new InvalidOperationException("Core suite requires all 57 registered reprints");
            string[] selected = reprints.Skip(id == "core-reprints-1" ? 0 : 29).Take(29).ToArray();
            // Deliberate cross-class engine stress decks; never exposed as player deck recipes.
            return new(id, "HERO_03", "Core reprints", selected.Concat(Enumerable.Repeat("CS2_082", 30 - selected.Length)).ToArray());
        }
#endif
        if (ModernIds.Contains(id))
        {
            ProofDeck deck = ProofDecks.Get(id);
            return new(deck.Id, deck.Hero, deck.Name, deck.CardIds);
        }
        LocalDeck local = LocalDecks.All.Single(deck => deck.Id == id);
        return new(local.Id, local.Hero, local.Opponent, local.CardIds.ToArray());
    }

    public static string[] DeckIds(string suite) => suite switch
    {
        "classic" => LocalDecks.All.Where(deck => !ModernIds.Contains(deck.Id)).Select(deck => deck.Id).ToArray(),
        "modern" => ModernIds,
#if SOURCE_ENGINE
        "death-knight-v1" => new[] { "death-knight-v1-rainbow", "death-knight-v1-frost", "death-knight-v1-unholy" },
        "offline-standard-pools-v1" => new[] { "pool-pilot-mage-v1", "pool-pilot-paladin-v1" },
        "standard-batch-1" => Enumerable.Range(0, Math.Max(2,
            (SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Count + 29) / 30))
            .Select(i => "standard-batch-1-" + (i + 1)).ToArray(),
        "standard-spike1" => new[] { "standard-spike1-druid" },
        "core-reprints" => new[] { "core-reprints-1", "core-reprints-2", "core-2026-1", "core-2026-2", "core-2026-3", "core-2026-4" },
        "core-2026" => new[] { "core-2026-1", "core-2026-2", "core-2026-3", "core-2026-4" },
#endif
        _ => throw new ArgumentException("suite must be classic, modern or core-reprints (source engine)")
    };

    public static string[] FuzzDeckIds() => DeckIds("classic")
#if SOURCE_ENGINE
        // Modern proof decks require definitions absent from the frozen vendor DLL.
        .Concat(DeckIds("modern"))
        .Concat(DeckIds("core-reprints")).Concat(DeckIds("standard-spike1")).Concat(DeckIds("standard-batch-1"))
#endif
        .ToArray();

    public static bool IsCoreReprint(Card card) => card != null
#if SOURCE_ENGINE
        && (SabberStoneCore.CardSets.Client.Reprints.ByDbfId.ContainsKey(card.AssetId)
            || SabberStoneCore.CardSets.Client.RebalancedCore2026.ByDbfId.ContainsKey(card.AssetId))
#else
        && false
#endif
        ;

    public static string[] Missing(DeckSpec deck) => deck.Cards.Append(deck.Hero)
        .Distinct(StringComparer.Ordinal).Where(id => Cards.FromId(id) == null).ToArray();

    public static Game Create(GameRecord record, bool replay = false
#if SOURCE_ENGINE
        , OfflinePoolManifest pools = null
#endif
        , DeckSpec playerDeck = null, DeckSpec opponentDeck = null
        )
    {
        if (replay && record.ReplaySchema is not (2 or 3))
            throw new InvariantException("replay.schema", "explicit schema 2 or 3 required");
        if (replay && record.ReplaySchema == 2 && (record.PoolPolicyVersion != null || record.PoolManifestHash != null))
            throw new InvariantException("replay.pool", "schema 2 cannot contain offline pool metadata");
#if SOURCE_ENGINE
        if (replay && record.ReplaySchema == 3)
        {
            var manifest = pools ?? PoolSnapshots.ForReplay(record.PoolManifestHash);
            if (record.PoolPolicyVersion != manifest.PolicyVersion || record.PoolManifestHash != manifest.Hash)
                throw new InvariantException("replay.pool", "offline pool policy/manifest mismatch");
            if (record.DarkGiftPolicyVersion != null && record.DarkGiftPolicyVersion != DarkGifts.PolicyVersion)
                throw new InvariantException("replay.dark-gift", "Dark Gift policy mismatch");
            pools = manifest;
        }
#else
        if (replay && record.ReplaySchema != 2)
            throw new InvariantException("replay.schema", "vendor engine supports only schema-2 legacy replays");
#endif
        DeckSpec first = playerDeck ?? Deck(record.Player), second = opponentDeck ?? Deck(record.Opponent);
        if (first.Id != record.Player || second.Id != record.Opponent)
            throw new InvariantException("replay.deck", "pinned deck identity mismatch");
        Card hero = Cards.FromId(first.Hero), botHero = Cards.FromId(second.Hero);
#if SOURCE_ENGINE
        Dictionary<string, string[]> Sideboards(DeckSpec deck) => deck.Cards
            .Where(SabberStoneCore.Actions.DeckbuildingRules.HasSideboard).Distinct().ToDictionary(source => source,
                source => SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Select(Cards.FromId)
                    .Where(card => SabberStoneCore.Actions.DeckbuildingRules.Eligible(source, card, FormatType.FT_STANDARD)
                        && !deck.Cards.Contains(card.Id)).OrderBy(card => card.Id, StringComparer.Ordinal)
                    .Take(SabberStoneCore.Actions.DeckbuildingRules.SlotCount(source)).Select(card => card.Id).ToArray());
#endif
        // Mirror NativeMatch.Start, including Wild, names, hero classes and the enabled mulligan.
        var game = new Game(new GameConfig
        {
            Player1Name = "Player", Player2Name = second.OpponentName,
            Player1HeroClass = hero.Class, Player1HeroCard = hero,
            Player1Deck = first.Cards.Select(Cards.FromId).ToList(),
            Player2HeroClass = botHero.Class, Player2HeroCard = botHero,
            Player2Deck = second.Cards.Select(Cards.FromId).ToList(),
            FormatType = record.Suite is "standard-spike1" or "standard-batch-1" or "offline-standard-pools-v1" ? FormatType.FT_STANDARD : FormatType.FT_WILD, StartPlayer = 1, Shuffle = true,
#if SOURCE_ENGINE
            EnableCorpses = true, // matches NativeMatch.Start: Corpses are tracked in every game (owner, 2026-10-21)
            LegacyPoolReplay = replay && record.ReplaySchema == 2,
            ReplayPools = pools,
            Player1Sideboards = Sideboards(first), Player2Sideboards = Sideboards(second),
#endif
            RandomSeed = record.Seed, SkipMulligan = false, History = true, Logging = false
        });
#if SOURCE_ENGINE
        game.UnversionedDarkGiftReplay = replay && record.DarkGiftPolicyVersion == null;
#endif
        game.StartGame();
#if SOURCE_ENGINE
        if (!replay)
        {
            record.ReplaySchema = 3;
            record.PoolPolicyVersion = game.OfflinePools.PolicyVersion;
            record.PoolManifestHash = game.OfflinePools.Hash;
            record.DarkGiftPolicyVersion = DarkGifts.PolicyVersion;
        }
#else
        if (!replay)
        {
            record.ReplaySchema = 2;
            record.PoolPolicyVersion = null;
            record.PoolManifestHash = null;
        }
#endif
        return game;
    }

    // Numeric tuple order is independent of dictionary enumeration and task display strings.
    public static List<PlayerTask> Legal(Game game) => Legal(game, out _);

    private static List<PlayerTask> Legal(Game game, out List<PlayerTask> tasks)
    {
        // NativeMatch answers the opponent mulligan before the human mulligan.
        Controller pending = new[] { game.Player2, game.Player1 }
            .FirstOrDefault(player => player.Choice?.ChoiceType == ChoiceType.MULLIGAN);
        if (pending != null)
        {
            int[] cards = pending.Choice.Choices.OrderBy(id => id).ToArray();
            tasks = Enumerable.Range(0, 1 << cards.Length).Select(mask => (PlayerTask)ChooseTask.Mulligan(pending,
                cards.Where((_, bit) => (mask & (1 << bit)) != 0).ToList())).ToList();
        }
        // Forced attacks can open a choice for the player whose turn is not active.
        else if (game.CurrentPlayer.Choice == null && game.CurrentOpponent.Choice != null)
            tasks = game.CurrentOpponent.Choice.Choices.Select(id => (PlayerTask)ChooseTask.Pick(game.CurrentOpponent, id)).ToList();
        else tasks = game.CurrentPlayer.Options();
        return tasks.OrderBy(task => (int)task.PlayerTaskType)
            .ThenBy(task => task.Controller.Id)
            .ThenBy(task => task.Source?.Id ?? 0)
#if SOURCE_ENGINE
            .ThenBy(task => task.ActionTarget?.Id ?? 0)
#else
            .ThenBy(task => task.Target?.Id ?? 0)
#endif
            .ThenBy(task => task is PlayCardTask play ? play.ZonePosition : 0)
            .ThenBy(task => task.ChooseOne)
            .ThenBy(task => task is ChooseTask choice ? string.Join(",", choice.Choices.Select(id => id.ToString("D10"))) : "", StringComparer.Ordinal)
            .ToList();
    }

    public static string TaskKey(PlayerTask task) => $"{(int)task.PlayerTaskType}:{task.Controller.Id}:{task.Source?.Id}:" +
#if SOURCE_ENGINE
        $"{task.ActionTarget?.Id}:" +
#else
        $"{task.Target?.Id}:" +
#endif
        $"{(task is PlayCardTask play ? play.ZonePosition : 0)}:{task.ChooseOne}:" +
        (task is ChooseTask choice ? string.Join(",", choice.Choices) : "");

    public static Game Run(GameRecord record, bool replay = false, Action<Game> started = null, Action<PlayerTask> processed = null
#if SOURCE_ENGINE
        , OfflinePoolManifest pools = null
#endif
        )
    {
        if (record.Policy is not ("random" or "first")) throw new ArgumentException("policy must be random or first");
        if (record.Actions == null) throw new ArgumentException("actions missing");
        Game game = Create(record, replay
#if SOURCE_ENGINE
            , pools
#endif
            );
        started?.Invoke(game);
        // The XOR salts (0x485353494D = "HSSIM" here, 0x434C4F4E45 = "CLONE" for fuzz clone points) derive separate PRNG streams from
        // the game seed, so the policy and the clone-point choice never consume the game's own randomness.
        var random = new PolicyRandom(unchecked((ulong)(long)record.Seed) ^ 0x485353494DUL);
        var clones = new List<(Game Game, int Until)>();
        int step = 0;
        while (game.State != State.COMPLETE)
        {
            // Safety stops, not game rules: passing either bound means the game cannot end, and is reported as an invariant failure.
            if (game.Turn > 200) throw new InvariantException("termination.turns", $"turn {game.Turn}");
            if (step >= 20000) throw new InvariantException("termination.actions", "20000 actions");
            // A clone probe runs the copy beside the original for up to 20 steps (step + 20 below), comparing state after every action.
            if (record.ClonePoints?.Contains(step) == true)
            {
                Game clone = game.Clone(logging: false, resetRandomSeed: false, history: true);
                EqualState(game, clone, "clone.initial", step);
                clones.Add((clone, step + 20));
            }
            List<PlayerTask> legal = Legal(game, out var nativeTasks);
            if (legal.Count == 0) throw new InvariantException("legal.empty", $"action {step}, turn {game.Turn}");
            string[] keys = legal.Select(TaskKey).ToArray();
            string legalSha = Canonical.Sha(Canonical.Value(keys));
            Canonical.CaptureDecision(game, step, legal[0].Controller, nativeTasks, legalSha);
            int index;
            if (replay)
            {
                if (step >= record.Actions.Count) throw new InvariantException("replay.truncated", $"action {step}");
                if (record.LegalShas == null || step >= record.LegalShas.Count || record.LegalShas[step] != legalSha)
                    throw new InvariantException("replay.legal", $"action {step}: ordered legal-task hash mismatch; recorded="
                        + $"{record.LegalShas?.ElementAtOrDefault(step) ?? "missing"}; current={legalSha}");
                index = record.Actions[step];
            }
            else
            {
                index = record.Policy == "first" ? 0 : random.Index(legal.Count);
#if SOURCE_ENGINE
                var selectorAnswer = record.Policy == "first" ? game.GetSelectorAnswer() : null;
                if (selectorAnswer != null)
                    index = legal.FindIndex(task => task is ChooseTask pick && pick.Choices.SequenceEqual(selectorAnswer.SelectedOptionIds));
#endif
                record.Actions.Add(index);
                record.LegalShas.Add(legalSha);
            }
            if (index < 0 || index >= legal.Count) throw new InvariantException("replay.illegal", $"action {step}, index {index}, legal {legal.Count}");
            // Resolve clone tasks before advancing the original: no task object crosses game boundaries.
            var cloneTasks = new List<(Game Game, PlayerTask Task)>();
            foreach (var probe in clones)
            {
                var options = Legal(probe.Game);
                if (!keys.SequenceEqual(options.Select(TaskKey)))
                    throw new InvariantException("clone.legal", $"action {step}");
                cloneTasks.Add((probe.Game, options[index]));
            }
            if (!Process(game, legal[index])) throw new InvariantException("legal.rejected", $"action {step}");
            processed?.Invoke(legal[index]);
            foreach (var probe in cloneTasks)
            {
                if (!Process(probe.Game, probe.Task)) throw new InvariantException("clone.rejected", $"action {step}");
                EqualState(game, probe.Game, "clone.advance", step);
            }
            step++;
            clones.RemoveAll(probe => probe.Until <= step);
        }
        if (game.Turn > 200) throw new InvariantException("termination.turns", $"turn {game.Turn}");
        if (replay && step != record.Actions.Count) throw new InvariantException("replay.extra", $"consumed {step}/{record.Actions.Count}");
        if (replay && step != record.LegalShas.Count) throw new InvariantException("replay.legal", $"consumed {step}/{record.LegalShas.Count} legal-task hashes");
        return game;
    }

    public static bool Process(Game game, PlayerTask task)
    {
#if SOURCE_ENGINE
        if (game.Resolution.HasSuspendedEffect)
        {
            var view = game.GetPendingDecisions().Single();
            return task is ChooseTask choice && game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(
                view.Id, task.Controller.Id, choice.Choices), out _);
        }
#endif
        bool accepted = game.Process(task);
        // The product calls MainBegin() explicitly once both mulligans are done (NativeMatch, after the last mulligan answer), and the
        // harness must do the same or its recorded games would start the first turn differently from real ones.
        if (game.Step == Step.BEGIN_MULLIGAN && game.Player1.MulliganState == Mulligan.DONE
            && game.Player2.MulliganState == Mulligan.DONE) game.MainBegin();
        return accepted;
    }

    private static void EqualState(Game original, Game clone, string invariant, int step)
    {
        string before = Canonical.State(original), after = Canonical.State(clone);
        if (before != after)
        {
            var error = new InvariantException(invariant, $"action {step}");
            error.Data["originalState"] = before;
            error.Data["cloneState"] = after;
            throw error;
        }
    }

    public static void Finish(GameRecord record, Game game)
    {
        record.Turns = game.Turn;
        record.Winner = game.Player1.PlayState == PlayState.WON ? 1 : game.Player2.PlayState == PlayState.WON ? 2 : 0;
        record.HistorySha = Canonical.Sha(Canonical.History(game));
        record.DecisionsSha = Canonical.Sha(Canonical.DecisionTrace(game));
        record.StateSha = Canonical.Sha(Canonical.State(game));
    }
}
