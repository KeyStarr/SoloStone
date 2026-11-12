using System.Reflection;
using System.Text;
using System.Text.Json;
using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;

internal sealed class TransportReplay
{
    public int Schema { get; set; } = 1;
    public GameRecord Game { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DeckSpec PlayerDeck { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DeckSpec OpponentDeck { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string EngineFingerprint { get; set; }
    public bool Focused { get; set; }
    public string FindingsSha { get; set; }
}

internal sealed record Example(string Replay, int Checkpoint, int Turn, int Entity, int Packets);
internal sealed class Bucket
{
    public string Card { get; init; }
    public string Tag { get; init; }
    public string Expected { get; init; }
    public string Shown { get; init; }
    public int Occurrences { get; set; }
    public HashSet<string> Games { get; } = new(StringComparer.Ordinal);
    public int GamesAffected => Games.Count;
    public Example Example { get; init; }
}

internal sealed class Runner
{
    internal readonly Dictionary<(string Card, string Tag, string Expected, string Shown), Bucket> Buckets = new();
    internal readonly Dictionary<string, int> EntryCounts = new();
    internal readonly Dictionary<string, int> Completed = new();
    internal int Checkpoints, Packets, Failures, CenariusPlayed, SuspendedAnswers, StandardBatchViolations;
    internal int LocationsPlayed, LocationActivations;
    internal int RewindOffers, RewindKeeps, Rewinds, FuzzRewindOffers, FuzzRewindKeeps, FuzzRewinds;
    internal const string Output = "sim/out/transport";
    internal bool LightspawnPlayed;
    internal readonly HashSet<string> CardsPlayed = new(StringComparer.Ordinal);
    // CHANGE_ENTITY packets by the step whose action produced them: (step, entity, new card).
    internal readonly List<(int Step, int Entity, string Card)> ChangeLog = new();
    internal readonly HashSet<string> StandardBatchPlayed = new(StringComparer.Ordinal);
    internal readonly HashSet<string> ReprintsPlayed = new(StringComparer.Ordinal);
    internal readonly HashSet<string> ReprintsTraded = new(StringComparer.Ordinal);
    internal bool LightspawnDetected => Buckets.Values.Any(b => b.Card == "EX1_335" && b.Tag == "ATK");
    internal int ViolationCount => Buckets.Values.Sum(b => b.Occurrences);

    internal HistoryShape Run(TransportReplay replay, bool verify = false, bool stopAfterForcedCard = false)
    {
        var record = replay.Game;
        string coverageCard = record.Player.StartsWith("standard-coverage-", StringComparison.Ordinal)
            ? Simulation.Deck(record.Player).Cards[0] : null;
        if (stopAfterForcedCard && (coverageCard == null || verify))
            throw new ArgumentException("Bounded pool evidence requires a fresh forced-card case.");
        bool coveragePlayed = false;
        var batchCards = record.Suite == "standard-batch-1" ? SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.ToHashSet(StringComparer.Ordinal) : null;
        if (record.Policy != "random") throw new ArgumentException("transport policy must be random");
        if (string.IsNullOrWhiteSpace(record.Id) || record.Id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
            throw new ArgumentException("transport game id must contain only ASCII letters, digits, '-' or '_'");
        if ((replay.PlayerDeck == null) != (replay.OpponentDeck == null))
            throw new ArgumentException("Transport replay requires both pinned decks or neither.");
        var game = Simulation.Create(record, replay: verify, playerDeck: replay.PlayerDeck, opponentDeck: replay.OpponentDeck);
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet));
        // Test seam: Start-equivalent factory + real Process/SendHistory, with both players seeded.
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        session = new(match);
        string path = $"{Output}/{record.Id}.json";
        session.History.Replay = path;
        session.History.RequireCorrelation = true;
        session.Flush();
        var random = new PolicyRandom(unchecked((ulong)(long)record.Seed) ^ 0x485353494DUL);
        var findings = new StringBuilder();
        bool newBucket = false;
        int step = 0, forcedEntity = 0;
        long lastRewindOffer = 0;
        void Observe()
        {
            Checkpoints++;
            foreach (var violation in Invariants.Compare(game, session.Client).Concat(Invariants.CompareMana(game, session.Client)))
            {
                if (record.Suite == "standard-batch-1") StandardBatchViolations++;
                findings.Append(step).Append(':').Append(JsonSerializer.Serialize(violation, Program.Json)).Append('\n');
                var key = (violation.Card, violation.Tag, violation.Expected, violation.Shown);
                if (!Buckets.TryGetValue(key, out var bucket))
                {
                    newBucket = true;
                    Buckets.Add(key, bucket = new() { Card = violation.Card, Tag = violation.Tag, Expected = violation.Expected,
                        Shown = violation.Shown, Example = new(path, step, game.Turn, violation.Entity, session.Packets.Count) });
                }
                bucket.Occurrences++;
                bucket.Games.Add(record.Id);
            }
        }
        while (game.State != State.COMPLETE)
        {
            session.History.Step = step;
            if (game.Turn > 200 || step >= 20000) throw new InvalidOperationException("transport game exceeded termination bounds: " + record.Id);
            if ((replay.Focused || coverageCard != null) && step == 2)
            {
                game.PowerHistory.Last.Clear();
                game.Player1.BaseMana = Math.Max(10, Cards.FromId(coverageCard ?? "EX1_335").Cost);
                game.Player1.UsedMana = 0;
                if (coverageCard != null)
                    foreach (var owner in new[] { game.Player1, game.Player2 })
                    // Damaged/undamaged, Legendary, Beast, Undead and Deathrattle targets satisfy the registered target restrictions.
                    foreach (string targetCard in new[] { "EX1_110", "CS2_172", "RLK_708", "CS2_182" })
                    {
                        var target = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId(targetCard));
                        SabberStoneCore.Actions.Generic.SummonBlock(game, target, -1, null);
                        target.Damage = targetCard == "CS2_182" ? 0 : 1;
                    }
                if (coverageCard == "EDR_804")
                {
                    var wisp = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1,Cards.FromId("CS2_231"));
                    SabberStoneCore.Actions.Generic.SummonBlock(game,wisp,-1,null);
                }
                game.DeathProcessingAndAuraUpdate();
                EvHooksTransport.Arrange(game, coverageCard);
                var definition = Cards.FromId(coverageCard ?? "EX1_335");
                if (definition.PlayRequirements.ContainsKey(PlayReq.REQ_WEAPON_EQUIPPED))
                    game.Player1.Hero.AddWeapon((Weapon)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("CS2_082")));
                forcedEntity = (definition.Shatter != null
                    ? SabberStoneCore.Actions.ShatterRules.GiveCombined(game.Player1, definition.Id, game.Player1.Hero)
                    : SabberStoneCore.Actions.Generic.DrawCard(game.Player1, definition)).Id;
                if (coverageCard == "CORE_RLK_567")
                {
                    // Shadow of Demise is unplayable until a legal spell gives it a held form.
                    var coin = SabberStoneCore.Actions.Generic.DrawCard(game.Player1, Cards.FromId("GAME_005"));
                    session.Flush();
                    session.Process(Simulation.Legal(game).Single(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == coin.Id));
                }
                var payment = SabberStoneCore.Actions.CostRules.Quote(game.Player1, (IPlayable)game.IdEntityDic[forcedEntity]);
                if (payment.Kind == SabberStoneCore.Model.Costs.CostPaymentKind.Armor)
                    game.Player1.Hero.Armor = Math.Max(game.Player1.Hero.Armor, payment.Amount);
                if (payment.Kind == SabberStoneCore.Model.Costs.CostPaymentKind.Health)
                    game.Player1.Hero.Health = Math.Max(game.Player1.Hero.Health, payment.Amount + 1);
                if (payment.Kind == SabberStoneCore.Model.Costs.CostPaymentKind.Corpses)
                    SabberStoneCore.Actions.CorpseRules.Gain(game.Player1, Math.Max(0, payment.Amount - SabberStoneCore.Actions.CorpseRules.Available(game.Player1)));
                session.Flush();
            }
            var legal = Simulation.Legal(game);
            if (legal.Count == 0) throw new InvalidOperationException("transport legal tasks empty: " + record.Id);
            bool human = legal[0].Controller == game.Player1;
            session.PublishDecision(legal[0].Controller);
            if (human) Observe();
            string hash = Canonical.Sha(Canonical.Value(legal.Select(Simulation.TaskKey).ToArray()));
            int index;
            if (verify)
            {
                if (step >= record.LegalShas.Count || hash != record.LegalShas[step])
                    throw new InvalidOperationException($"replay.legal: {record.Id} checkpoint {step}");
                if (step >= record.Actions.Count || record.Actions[step] < 0 || record.Actions[step] >= legal.Count)
                    throw new InvalidOperationException($"replay.actions: {record.Id} checkpoint {step}");
                index = record.Actions[step];
            }
            else
            {
                index = random.Index(legal.Count);
                if ((replay.Focused || coverageCard != null) && step < 2) index = 0;
                if ((replay.Focused || coverageCard != null) && step == 2)
                {
                    index = legal.FindIndex(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == forcedEntity);
                    if (index < 0) throw new InvalidOperationException($"focused {coverageCard ?? "Lightspawn"} has no legal play; hero={game.Player1.HeroClass}; mana={game.Player1.RemainingMana}");
                }
                record.Actions.Add(index);
                record.LegalShas.Add(hash);
            }
            var task = legal[index];
            string playedCard = task.PlayerTaskType == PlayerTaskType.PLAY_CARD ? task.Source.Card.Id : null;
            if (playedCard != null) { CardsPlayed.Add(playedCard); HistoryShapeReport.PlayedCard(playedCard); }
            if (task.PlayerTaskType == PlayerTaskType.PLAY_CARD && task.Source is SabberStoneCore.Model.Entities.BoardObject) LocationsPlayed++;
            if (task.PlayerTaskType == PlayerTaskType.ACTIVATE_LOCATION) LocationActivations++;
            if (task.PlayerTaskType == PlayerTaskType.PLAY_CARD && task.Source?.Card.Id == "EX1_335") LightspawnPlayed = true;
            if (task.PlayerTaskType == PlayerTaskType.PLAY_CARD && Simulation.IsCoreReprint(task.Source?.Card)) ReprintsPlayed.Add(task.Source.Card.Id);
            if (task.PlayerTaskType == PlayerTaskType.TRADE_CARD && Simulation.IsCoreReprint(task.Source?.Card)) ReprintsTraded.Add(task.Source.Card.Id);
            if (task.PlayerTaskType == PlayerTaskType.PLAY_CARD && task.Source?.Card.Id == "EDR_209") CenariusPlayed++;
            if (game.Resolution.HasSuspendedEffect) SuspendedAnswers++;
            if (task is ChooseTask answer && game.CurrentPlayer.Choice?.ChoiceAction == ChoiceAction.REWIND)
            {
                bool rewind = answer.Choices.Single() == game.Rewind.RewindEntityId;
                if (rewind) { Rewinds++; if (record.Suite == "fuzz") FuzzRewinds++; }
                else { RewindKeeps++; if (record.Suite == "fuzz") FuzzRewindKeeps++; }
            }
            int changedBefore = session.Changed.Count;
            try { session.Process(task); }
            catch (Exception error) { throw new InvalidOperationException($"transport action failed: {record.Id} step={step} task={Simulation.TaskKey(task)}", error); }
            for (int c = changedBefore; c < session.Changed.Count; c++) ChangeLog.Add((step, session.Changed[c].Entity, session.Changed[c].Card));
            if (game.Rewind?.Stage == SabberStoneCore.Model.Rewind.RewindStage.Offered && game.Rewind.OfferChoiceId != lastRewindOffer)
            {
                lastRewindOffer = game.Rewind.OfferChoiceId;
                RewindOffers++; if (record.Suite == "fuzz") FuzzRewindOffers++;
            }
            if (playedCard != null && batchCards?.Contains(playedCard) == true) StandardBatchPlayed.Add(playedCard);
            // A forced collectible can enter play as its exact exported hand-transform form.
            if (playedCard != null && coverageCard != null && task.Source.Id == forcedEntity
                && (Cards.FromId(coverageCard)[(GameTag)1452] == task.Source.Card.AssetId
                    || coverageCard == "CORE_RLK_567" && playedCard == "GAME_005"
                    || coverageCard == "TLC_452" && Cards.FromId(coverageCard).Entourage.Contains(playedCard)))
            {
                StandardBatchPlayed.Add(coverageCard);
                CardsPlayed.Add(coverageCard);
                HistoryShapeReport.PlayedCard(coverageCard);
            }
            step++;
            if (coverageCard != null && playedCard != null && task.Source.Id == forcedEntity)
                coveragePlayed = CardsPlayed.Contains(coverageCard);
            if (stopAfterForcedCard && coveragePlayed && !game.Resolution.HasSuspendedEffect
                && game.Player1.Choice == null && game.Player2.Choice == null) break;
        }
        if (stopAfterForcedCard && !coveragePlayed) throw new InvalidOperationException("Forced pool evidence card was not played.");
        // A normal game must keep its achievement evidence; fail-open recording must not hide recorder regressions.
        if (!game.HistoryFacts.EvidenceValid)
            throw new InvalidOperationException($"history.evidence-invalid: {record.Id}: {game.HistoryFacts.EvidenceFailure}");
        session.History.Step = step;
        Observe();
        newBucket |= HistoryShapeReport.Add(session.History);
        string findingsSha = Canonical.Sha(findings.ToString());
        if (verify && (step != record.Actions.Count || step != record.LegalShas.Count || findingsSha != replay.FindingsSha))
            throw new InvalidOperationException("replay.result: action length or transport findings changed");
        if (replay.EngineFingerprint != null)
        {
            string fingerprint = Canonical.Sha(Canonical.State(game) + Canonical.History(game)
                + Canonical.Value(game.Random.CaptureState()));
            // Renewal audit aid: HS_TRANSPORT_FP_PARTS=1 prints the three parts, so a drift can be shown to be state/history only (RNG identical).
            if (Environment.GetEnvironmentVariable("HS_TRANSPORT_FP_PARTS") == "1")
                Console.WriteLine($"FP_PARTS {record.Id} fingerprint={fingerprint} state={Canonical.Sha(Canonical.State(game))[..12]} history={Canonical.Sha(Canonical.History(game))[..12]} rng={Canonical.Sha(Canonical.Value(game.Random.CaptureState()))[..12]} actions={step}");
            if (verify && replay.EngineFingerprint != fingerprint)
                throw new InvalidOperationException("replay.engine: state/history/RNG fingerprint changed");
            replay.EngineFingerprint = fingerprint;
        }
        replay.FindingsSha = findingsSha;
        record.Turns = game.Turn;
        record.Winner = game.Player1.PlayState == PlayState.WON ? 1 : game.Player2.PlayState == PlayState.WON ? 2 : 0;
        Completed[record.Suite] = Completed.GetValueOrDefault(record.Suite) + 1;
        Packets += session.Packets.Count;
        foreach (var count in session.Client.EntryCounts) EntryCounts[count.Key] = EntryCounts.GetValueOrDefault(count.Key) + count.Value;
        // Keep every packet of representative games, not only the mismatching tag, so ordering can be inspected.
        if (!verify && (newBucket || replay.Focused))
        {
            Program.Save(path, replay);
            File.WriteAllLines(Path.ChangeExtension(path, ".packets.jsonl"), session.Packets.Select(p => JsonSerializer.Serialize(p, Program.Json)));
        }
        return session.History;
    }

    internal static IEnumerable<TransportReplay> Cases(string suite, int end)
    {
        foreach (string first in Simulation.DeckIds(suite))
        foreach (string second in Simulation.DeckIds(suite))
        for (int seed = 0; seed <= end; seed++)
            yield return new() { Game = new() { Id = $"{suite}-{first}-vs-{second}-s{seed}-random", Suite = suite,
                Player = first, Opponent = second, Seed = seed } };
    }

    internal static IEnumerable<TransportReplay> StandardCoverageCases(IEnumerable<string> cards)
    {
        foreach (string card in cards.Order(StringComparer.Ordinal))
            yield return new() { Game = new() { Id = "standard-coverage-" + card, Suite = "standard-batch-1",
                Player = "standard-coverage-" + card, Opponent = "warrior", Seed = 0 } };
    }

    internal static IEnumerable<TransportReplay> Fuzz()
    {
        var random = new PolicyRandom(1);
        var ids = Simulation.FuzzDeckIds();
        for (int i = 0; i < 200; i++)
            yield return new() { Game = new() { Id = $"fuzz-1-{i:D4}", Suite = "fuzz", Player = i < 12 ? "standard-coverage-" + (i % 2 == 0 ? "TIME_001" : "TIME_038") : ids[random.Index(ids.Length)],
                Opponent = ids[random.Index(ids.Length)], Seed = unchecked((int)random.Next()) } };
    }

    internal static IEnumerable<TransportReplay> RewindCases()
    {
        foreach (string card in new[] { "TIME_001", "TIME_038" })
        for (int seed = 0; seed < 8; seed++)
            yield return new() { Game = new() { Id = $"rewind-{card}-{seed}", Suite = "rewind",
                Player = "standard-coverage-" + card, Opponent = "warrior", Seed = seed } };
    }

    internal static TransportReplay Focused() => new() { Focused = true, Game = new() {
        Id = "focused-lightspawn", Suite = "focused", Player = "priest", Opponent = "mage", Seed = 0
    } };
}
