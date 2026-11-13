using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TransportInvariants;

internal static class Program
{
    internal static readonly JsonSerializerOptions Json = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    internal static void Save<T>(string path, T data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        File.WriteAllText(path, JsonSerializer.Serialize(data, Json) + "\n");
    }

    internal static int ResultCode(Runner runner, bool strict, bool historyShapePassed) =>
        runner.Failures > 0 ? 2 : strict && (runner.ViolationCount > 0 || !historyShapePassed) ? 1 : 0;

    internal static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        try
        {
            if (args is ["--hand-card-state-only"]) { TransportHooks.Install(); HandCardStateChecks.Run(); return 0; }
            // One corpus game by id: the full inner exception, without the proofs or the corpus of about 50 minutes.
            if (args is ["--game", var gameId])
            {
                TransportHooks.Install();
                string suite = gameId.Split("-vs-")[0].Contains("standard-batch-1") ? "standard-batch-1" : gameId.Split('-')[0];
                new Runner().Run(Runner.Cases(suite, 9).Single(c => c.Game.Id == gameId), verify: false);
                Console.WriteLine("PASS transport game " + gameId);
                return 0;
            }
            if (args is ["--card-state-only"]) { TransportHooks.Install(); CardStateChecks.Run(); return 0; }
            if (args is ["--deathrattle-replay-only"])
            {
                TransportHooks.Install();
                DeathrattleReplayProof.Run();
                return HistoryShapeReport.Write(false) ? 0 : 1;
            }
            if (args is ["--private-return-only"])
            {
                TransportHooks.Install(); PrivateReturnProof.Run();
                return HistoryShapeReport.Write(false) ? 0 : 1;
            }
            if (args is ["--private-return-corpus"] or ["--private-return-corpus", "--sample"]
                or ["--private-return-corpus", "--deck31"])
            {
                TransportHooks.Install(); HsSim.PoolSnapshots.Save();
                return PrivateReturnCorpus.Run(args.Contains("--sample"), args.Contains("--deck31"));
            }
            if (args is ["--harness-only"])
            {
                TransportHooks.Install();
                HsSim.PoolSnapshots.Save();
                TransportHarnessChecks.Focused();
                return 0;
            }
            if (args is ["--shatter-state-only"]) { TransportHooks.Install(); HandCardStateChecks.Run(shatterOnly: true); return 0; }
            if (args is ["--hand-card-state-only"]) { TransportHooks.Install(); HandCardStateChecks.Run(); return 0; }
            if (args is ["--godfrey-only"]) { TransportHooks.Install(); GodfreyHistoryProof.Run(); return 0; }
            if (args is ["--burned-card-only"]) { TransportHooks.Install(); HistoryShapeProof.BurnedCards(); return 0; }
            if (args is ["--pastvar-minions-only"])
            {
                TransportHooks.Install();
                LegacyPoolEligibilityProof.Run();
                return 0;
            }
            if (args.Length >= 1 && args[0] == "--transport-debt-only")
            {
                TransportHooks.Install();
                TransportDebtProof.Run(args.ElementAtOrDefault(1));
                return 0;
            }
            if (args.Length >= 1 && args[0] == "--meta-pre-be-only")
            {
                TransportHooks.Install();
                return MetaPreBeProof.Run(args.ElementAtOrDefault(1));
            }
            if (args is ["--classify-history", var rawPath])
                return HistoryShapeReport.Reclassify(rawPath) ? 0 : 1;
            if (args is ["--b13-state-only"]) { TransportHooks.Install(); Batch13PrivateStateProof.Run(); return 0; }
            if (args.Length >= 1 && args[0] == "--b14-privacy-only") { TransportHooks.Install(); B14PrivacyProof.Run(args.ElementAtOrDefault(1)); return 0; }
            if (args is ["--b14-presentation-only"]) { TransportHooks.Install(); HistoryShapeProof.B14Presentation(); return 0; }
            if (args is ["--imbue-only"]) { TransportHooks.Install(); ImbueProof.Run(); return 0; }
            if (args is ["--result-settlement-only"]) { TransportHooks.Install(); ResultSettlementObserverProof.Run(); return 0; }
            if (args is ["--prepare-only"]) { TransportHooks.Install(); PrepareProof.Run(); return 0; }
            if (args is ["--colossal-only"]) { TransportHooks.Install(); ColossalAppendageProof.Run(); return 0; }
            if (args is ["--auto-cast-card", var autoCastCard]) { TransportHooks.Install(); AutoCastProof.CoverOne(autoCastCard); return 0; }
            if (args is ["--auto-cast-identity-only"]) { TransportHooks.Install(); AutoCastProof.IdentityRegressions(); return 0; }
            if (args is ["--auto-cast-only"]) { TransportHooks.Install(); AutoCastProof.Run(); return 0; }
            if (args is ["--board-transit-only"])
            {
                TransportHooks.Install();
                DrawRevealProof.Run();
                CorpseTransportProof.Run();
                RewindTransportProof.Run();
                DiscoverRealProof.Run();
                TradeRedrawProof.Run();
                B14PrivacyProof.Run();
                return 0;
            }
            if (args is ["--location-only"]) { TransportHooks.Install(); LocationProof.Run(); return 0; }
            if (args is ["--draw-reveal-only"]) { TransportHooks.Install(); DrawRevealProof.Run(); return 0; }
            if (args is ["--metadata-full-entity-only"]) { HistoryShapeProof.FutureFullMetadata(); return 0; }
            bool strict = false, focused = false, selfTest = false, rewindOnly = false, overloadOnly = false, poolEvidenceOnly = false;
            string replayPath = null;
            var seen = new HashSet<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (!seen.Add(args[i])) throw new ArgumentException("duplicate option: " + args[i]);
                switch (args[i])
                {
                    case "--strict": strict = true; break;
                    case "--focused": focused = true; break;
                    case "--self-test": selfTest = true; break;
                    case "--rewind-only": rewindOnly = true; break;
                    case "--overload-only": overloadOnly = true; break;
                    case "--pool-evidence-only": poolEvidenceOnly = true; break;
                    case "--replay" when i + 1 < args.Length: replayPath = args[++i]; break;
                    default: throw new ArgumentException("usage: dotnet TransportInvariants.dll [--strict] [--focused|--self-test|--rewind-only|--overload-only|--pool-evidence-only|--replay file] or --classify-history raw-file");
                }
            }
            if ((focused ? 1 : 0) + (selfTest ? 1 : 0) + (rewindOnly ? 1 : 0) + (overloadOnly ? 1 : 0) + (poolEvidenceOnly ? 1 : 0) + (replayPath != null ? 1 : 0) > 1)
                throw new ArgumentException("focused, self-test, rewind-only, overload-only, pool-evidence-only and replay are mutually exclusive");
            var watch = Stopwatch.StartNew();
            if (replayPath == null) HsSim.PoolSnapshots.Save();
            TransportHooks.Install();
            if (args is ["--self-test"]) Batch13PrivateStateProof.Run();
            if (overloadOnly) { OverloadManaProof.Run(); return 0; }
            if (rewindOnly) RewindTransportProof.Run();
            // A serialized replay checks its own trace; broad proofs belong to corpus runs.
            else if (replayPath == null)
            {
                HistoryShapeProof.Run();
                B14PrivacyProof.Run();
                SelfChecks.Run();
                // Per-card admission omits cross-product replays; focused/full transport still checks them.
                if (!poolEvidenceOnly)
                {
                    TransportDebtProof.Run();
                    SelfChecks.Check(MetaPreBeProof.Run() == 0, "pre-BE regression replays");
                }
                CorpseTransportProof.Run();
                DrawRevealProof.Run();
                GodfreyHistoryProof.Run();
                PrivateReturnProof.Run();
                DeathrattleReplayProof.Run();
                AutoCastProof.Run();
                CenariusProof.Run();
                ResultSettlementObserverProof.Run();
                RewindTransportProof.Run();
                PlayerDecisionProof.Run();
                CopyInitializationProof.Run();
                SummonCopyReadinessProof.Run();
                SilenceValueProof.Run();
                SecretPositionProof.Run();
                BoardPositionProof.Run();
                KeywordsCfaProof.Run();
                PrepareProof.Run();
                OverloadManaProof.Run();
                QuestProof.Run();
                LocationProof.Run();
                LegacyPoolEligibilityProof.Run();
                OfflinePoolProof.Run();
                DiscoverRealProof.Run();
                if (!selfTest) Batch13PrivateStateProof.Run();
                DarkGiftProof.Run();
                ColossalAppendageProof.Run();
                TradeRedrawProof.Run();
                StandardCoverageChecks.Run(includeCorpus: !poolEvidenceOnly);
            }
            if (selfTest) { TransportHarnessChecks.Focused(); return 0; }
            var runner = new Runner();
            var poolEvidenceCards = SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds
                .Concat(SabberStoneCore.Model.OfflinePoolPolicy.Load().Members.Select(m => m.CardId)).Distinct().ToArray();
            if (replayPath != null)
            {
                var replay = JsonSerializer.Deserialize<TransportReplay>(File.ReadAllText(replayPath), Json);
                if (replay?.Schema != 1 || replay.Game == null || replay.Game.Actions == null || replay.Game.LegalShas == null)
                    throw new ArgumentException("invalid transport replay");
                runner.Run(replay, verify: true);
            }
            else
            {
                var cases = poolEvidenceOnly ? Runner.StandardCoverageCases(poolEvidenceCards)
                    : focused ? new[] { Runner.Focused() } : rewindOnly ? Runner.RewindCases()
                    : Runner.Cases("classic", 4).Concat(Runner.Cases("modern", 9))
                        .Concat(Runner.Cases("death-knight-v1", 2)).Concat(Runner.Cases("core-reprints", 2)).Concat(Runner.Cases("standard-spike1", 9)).Concat(Runner.Cases("standard-batch-1", 9)).Concat(Runner.RewindCases()).Concat(Runner.Fuzz());
                int count = 0;
                foreach (var replay in cases)
                {
                    try
                    {
                        runner.Run(replay, stopAfterForcedCard: poolEvidenceOnly);
                        if (rewindOnly)
                        {
                            Save($"{Runner.Output}/{replay.Game.Id}.json", replay);
                            runner.Run(replay, verify: true);
                        }
                    }
                    catch (Exception error)
                    {
                        runner.Failures++;
                        Save($"{Runner.Output}/{replay.Game.Id}.failure.json", new { replay, error = error.ToString() });
                        Console.Error.WriteLine($"transport ERROR {replay.Game.Id}: {error.Message}");
                    }
                    if (++count % 25 == 0) Console.Error.WriteLine($"transport progress={count}; violations={runner.ViolationCount}; failures={runner.Failures}");
                }
                if (!poolEvidenceOnly && !focused && !rewindOnly && !runner.LightspawnPlayed) runner.Run(Runner.Focused());
            }
            if (poolEvidenceOnly && poolEvidenceCards.Except(runner.CardsPlayed).Any())
                throw new InvalidOperationException("Pool evidence must play every registered Standard card and admitted pool member.");
            if (!poolEvidenceOnly && !focused && !rewindOnly && replayPath == null)
            {
                if (runner.RewindOffers == 0 || runner.RewindKeeps == 0 || runner.Rewinds == 0 ||
                    runner.FuzzRewindOffers == 0 || runner.FuzzRewindKeeps == 0 || runner.FuzzRewinds == 0)
                    throw new InvalidOperationException("strict corpus and fuzz must exercise Rewind offers and both answers");
                if (runner.LocationsPlayed == 0 || runner.LocationActivations == 0)
                    throw new InvalidOperationException("transport corpus must play and activate locations");
                var expected = SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds;
                // Random cross-class games cannot guarantee draws, legal Herald profiles or action selection.
                foreach (var replay in Runner.StandardCoverageCases(expected.Except(runner.StandardBatchPlayed, StringComparer.Ordinal).ToArray()))
                    runner.Run(replay);
                var missing = expected.Except(runner.StandardBatchPlayed, StringComparer.Ordinal).ToArray();
                if (missing.Length > 0) throw new InvalidOperationException("Standard batch cards never played: " + string.Join(", ", missing));
                Console.WriteLine($"transport standard-batch-1: games={runner.Completed.GetValueOrDefault("standard-batch-1")}; cardsPlayed={runner.StandardBatchPlayed.Count}; violations={runner.StandardBatchViolations}");
            }
            var buckets = runner.Buckets.Values.OrderByDescending(b => b.Occurrences).ThenBy(b => b.Card, StringComparer.Ordinal)
                .ThenBy(b => b.Tag, StringComparer.Ordinal).ThenBy(b => b.Expected, StringComparer.Ordinal).ThenBy(b => b.Shown, StringComparer.Ordinal).ToArray();
            string reportPath = $"{Runner.Output}/{(focused ? "focused-report" : rewindOnly ? "rewind-report" : replayPath != null ? "replay-report" : "report")}.json";
            double seconds = watch.Elapsed.TotalSeconds;
            Save(reportPath, new { schema = 1, strict, poolEvidenceOnly,
                cardsPlayed = runner.CardsPlayed.Concat(LegacyPoolEligibilityProof.Played).Append("EDR_209").Distinct().Order(StringComparer.Ordinal).ToArray(),
                engineSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(SabberStoneCore.Model.Game).Assembly.Location))),
                completed = runner.Completed, failures = runner.Failures, checkpoints = runner.Checkpoints, packets = runner.Packets,
                violations = runner.ViolationCount, lightspawnPlayed = runner.LightspawnPlayed, lightspawnDetected = runner.LightspawnDetected,
                cenariusPlayed = runner.CenariusPlayed, suspendedAnswers = runner.SuspendedAnswers,
                standardBatchViolations = runner.StandardBatchViolations,
                standardBatchPlayed = runner.StandardBatchPlayed.Order(StringComparer.Ordinal).ToArray(),
                rewindOffers = runner.RewindOffers, rewindKeeps = runner.RewindKeeps, rewinds = runner.Rewinds,
                fuzzRewindOffers = runner.FuzzRewindOffers, fuzzRewindKeeps = runner.FuzzRewindKeeps, fuzzRewinds = runner.FuzzRewinds,
                locationsPlayed = runner.LocationsPlayed, locationActivations = runner.LocationActivations,
                reprintsPlayed = runner.ReprintsPlayed.Order(StringComparer.Ordinal).ToArray(),
                reprintsTraded = runner.ReprintsTraded.Order(StringComparer.Ordinal).ToArray(),
                seconds, entryCounts = runner.EntryCounts, buckets });
            Console.WriteLine("card\ttag\tengine\tclient\toccurrences\tgames\treplay@checkpoint");
            foreach (var bucket in buckets)
                Console.WriteLine($"{bucket.Card}\t{bucket.Tag}\t{bucket.Expected}\t{bucket.Shown}\t{bucket.Occurrences}\t{bucket.GamesAffected}\t{bucket.Example.Replay}@{bucket.Example.Checkpoint}");
            Console.WriteLine($"transport: games={runner.Completed.Values.Sum()}; violations={runner.ViolationCount}; buckets={buckets.Length}; failures={runner.Failures}; cenariusPlayed={runner.CenariusPlayed}; suspendedAnswers={runner.SuspendedAnswers}; locationsPlayed={runner.LocationsPlayed}; locationActivations={runner.LocationActivations}; reprintsPlayed={runner.ReprintsPlayed.Count}; reprintsTraded={JsonSerializer.Serialize(runner.ReprintsTraded.Order(StringComparer.Ordinal))}; rewindOffers={runner.RewindOffers}; rewindKeeps={runner.RewindKeeps}; rewinds={runner.Rewinds}; fuzzRewindOffers={runner.FuzzRewindOffers}; fuzzRewindKeeps={runner.FuzzRewindKeeps}; fuzzRewinds={runner.FuzzRewinds}; LightspawnDetected={runner.LightspawnDetected.ToString().ToLowerInvariant()}; wall={seconds:F3}s; report={reportPath}");
            bool historyShapePassed = HistoryShapeReport.Write(!poolEvidenceOnly && !focused && !rewindOnly && replayPath == null);
            if (!poolEvidenceOnly && !focused && !rewindOnly && replayPath == null && runner.Failures == 0)
            {
                TransportHarnessChecks.Corpus(runner);
                TransportHarnessChecks.Focused();
            }
            return ResultCode(runner, strict, historyShapePassed);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 2;
        }
    }
}
