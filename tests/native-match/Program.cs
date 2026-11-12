using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using PegasusGame;
using SabberStoneCore.Auras;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks;
using EngineGame = SabberStoneCore.Model.Game;
using EngineZone = SabberStoneCore.Enums.Zone;
using NativeOption = PegasusGame.Option;

internal static partial class Program
{
    private sealed class Capture
    {
        public readonly List<(int Id, IProtoBuf Message)> Packets = new List<(int, IProtoBuf)>();
        public readonly List<(int Id, byte[] Bytes)> WirePackets = new();
        public void Send(int id, IProtoBuf message)
        {
            using var stream = new MemoryStream();
            message.Serialize(stream);
            Check(stream.Length == message.GetSerializedSize(), "serialized size mismatch for packet " + id);
            if (id == 19 && message is PowerHistory history && history.List.Count > 0)
                Check(!history.List[history.List.Count - 1].HasSubSpellEnd, "packet ends on SUB_SPELL_END: PowerProcessor replays its prefab");
            WirePackets.Add((id, stream.ToArray()));
            Packets.Add((id, message));
        }
        public IEnumerable<PowerHistory> HistorySnapshots(int cursor = 0)
        {
            foreach (var packet in WirePackets.Skip(cursor).Where(p => p.Id == 19))
            {
                using var stream = new MemoryStream(packet.Bytes);
                var history = new PowerHistory();
                history.Deserialize(stream);
                Check(stream.Position == stream.Length, "history snapshot did not consume sent bytes");
                yield return history;
            }
        }
        public T Last<T>(int id) where T : class, IProtoBuf =>
            Packets.Last(p => p.Id == id && p.Message is T).Message as T;
        public void SerializeAll()
        {
            CheckPinnedClientIds(this);
            foreach (var packet in Packets)
                using (var stream = new MemoryStream())
                {
                    packet.Message.Serialize(stream);
                    Check(stream.Length == packet.Message.GetSerializedSize(),
                        "serialized size mismatch for packet " + packet.Id);
                }
        }
    }

    // Fail-open achievement recording must not hide recorder regressions: any evidence invalidation in a
    // normal native match (including bot search clones) fails the suite. Forging tests set the verdict directly.
    private static readonly System.Collections.Concurrent.ConcurrentQueue<string> evidenceInvalidations = new();
    private static void Main(string[] args)
    {
        typeof(SabberStoneCore.Model.HistoryFacts).GetField("EvidenceInvalidated",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .SetValue(null, (Action<string>)(reason => evidenceInvalidations.Enqueue(reason)));
        RunSuite(args);
        if (!evidenceInvalidations.IsEmpty)
            throw new InvalidOperationException("TEST: " + evidenceInvalidations.Count + " native match(es) invalidated achievement evidence: "
                + string.Join(" | ", evidenceInvalidations.Distinct().Take(5)));
        Console.WriteLine("PASS achievement evidence stayed valid in every native match");
    }

    private static void RunSuite(string[] args)
    {
        ControllerAdmissionInvariant.Install();
        if (args.Contains("--match-event-log-review-only")) { NativeMatch.ClockFreeBot = true; MatchEventLogReview(); return; }
        if (args.Contains("--match-event-log-only")) { NativeMatch.ClockFreeBot = true; MatchEventLogNative(); return; }
        if (args.Contains("--held-form-overlay-only")) { NativeMatch.ClockFreeBot = true; HeldFormOverlayHistory(); return; }
        if (args.Contains("--temporary-only")) { NativeMatch.ClockFreeBot = true; TemporaryVisualHistory(args.ElementAtOrDefault(1)); return; }
        if (args.Contains("--hand-target-choice-only")) { NativeMatch.ClockFreeBot = true; HandTargetChoiceHistory(args.ElementAtOrDefault(1)); return; }
        if (args.Contains("--choice-tools-only")) { NativeMatch.ClockFreeBot = true; ChoiceToolsNative(); return; }
        if (args.Contains("--shatter-fx-only")) { ShatterFxHistory(); return; }
        if (args.Contains("--imbue-fx-only")) { NativeMatch.ClockFreeBot = true; ImbueFxHistory(); return; }
        if (args.Contains("--shatter-opening-only")) { NativeMatch.ClockFreeBot = true; ShatterOpeningNative(args.ElementAtOrDefault(1)); return; }
        if (args.Contains("--agent-choice-only")) { NativeMatch.ClockFreeBot = true; AgentCoinChoiceHistory(); return; }
        if (args.Contains("--jade-golem-only")) { JadeGolemProjection(); return; }
        if (args.Contains("--location-damage-fx-only")) { NativeMatch.ClockFreeBot = true; LocationActivationFx(); return; }
        if (args.Contains("--location-fx-only")) { NativeMatch.ClockFreeBot = true; LocationActivationHistory(); return; }
        if (args.Contains("--locations-only")) { NativeMatch.ClockFreeBot = true; LocationActivationHistory(); LocationOptions(); return; }
        if (args.Contains("--plague-only")) { NativeMatch.ClockFreeBot = true; PlagueAttackShuffle(); FreshDeckShuffleHistory(); ArrivalCastsWhenDrawn(); return; }
        if (args.Contains("--shuffle-choice-only")) { NativeMatch.ClockFreeBot = true; DiscoveredShuffleCompletion(); return; }
        if (args.Contains("--shuffle-only")) { NativeMatch.ClockFreeBot = true; GeneratedShuffleHistory(); return; }
        if (args.Contains("--shuffle-batch-only")) { NativeMatch.ClockFreeBot = true; ShuffleBatchHistory(); return; }
        if (args.Contains("--hero-skin-only")) { HeroSkinPresentationTests(); return; }
        if (args.Contains("--board-identity-only")) { NativeMatch.ClockFreeBot = true; BoardIdentityHistory(); return; }
        if (args.Contains("--rewind-presentation-only")) { RewindPresentationTags(); return; }
        if (args.Contains("--vanguard-only")) { NativeMatch.ClockFreeBot = true; VanguardDrawHistory(); return; }
        if (args.Contains("--broxigar-only")) { NativeMatch.ClockFreeBot = true; BroxigarReturnHistory(); return; }
        if (args.Contains("--chillmaw-only")) {
            NativeMatch.ClockFreeBot = true;
            ChillmawHistory(args.Contains("--dump-chillmaw"), args.Contains("--require-no-empty-effect")); return;
        }
        if (args.Contains("--trigger-visual-only")) { NativeMatch.ClockFreeBot = true; TriggerVisualKeywordHistory(); return; }
        if (args.Contains("--wisp-fx-only")) { NativeMatch.ClockFreeBot = true; WispHeroPowerHistory(); return; }
        if (args.Contains("--godfrey-only")) { GodfreyPortalHistory(); return; }
        if (args.Contains("--coin-flip-only")) { CoinFlipTests(); return; }
        if (args.Contains("--hero-replacement-only")) { NativeMatch.ClockFreeBot = true; HeroReplacementHistory(args.Contains("--dump-hero-replacement")); return; }
        if (args.Length == 2 && args[0] == "--admission-card") { NativeMatch.ClockFreeBot = true; NativeAdmissionSweep(args[1]); return; }
        if (args.Contains("--admission-only")) { NativeMatch.ClockFreeBot = true; NativeAdmissionSweep(); return; }
        if (args.Contains("--empty-triggers-only")) { EmptyTriggerPresentation(); return; }
        if (args.Contains("--conditional-deathrattle-only")) { ConditionalDeathrattlePresentation(); return; }
        if (args.Contains("--generator-targets-only")) { NativeGeneratorTargetControls(); return; }
        if (args.Contains("--admission-controls-only")) { NativeAdmissionControls(); NativeAdmissionTransportControls(); return; }
        if (args.Contains("--chaos-strike-only")) { NativeMatch.ClockFreeBot = true; ChaosStrikeHistory(); return; }
        if (args.Contains("--faceless-only")) { NativeMatch.ClockFreeBot = true; FacelessEnigmaHistory(); return; }
        NativePackRevealChecks.Run();
        if (args.Contains("--pack-reveal-only")) return;
        if (args.Contains("--playtest-seed-only")) { PlaytestSeedNative(); return; }
        if (args.Contains("--match-journal-only")) { MatchJournalNative(); return; }
        if (args.Contains("--concede-only"))
        {
            NativeMatch.ClockFreeBot = true;
            ConcedeFinishesThroughEngine(); NpcConcedeForTestWinsThroughEngine(); LoseForTestIsAPlainLoss();
            Console.WriteLine("PASS 3 engine-ending tests: human concede, NPC concede, plain loss"); return;
        }
        if (args.Contains("--debug-scenario-only")) { NativeMatch.ClockFreeBot = true; DebugScenarioTests(); return; }
        if (args.Contains("--turn-timer-only")) { PlayerTurnTimerTests(); return; }
        if (args.Contains("--npc-rope-only")) { NpcRopeClock(); return; }
        if (args.Contains("--result-settlement-only")) { ResultSettlementTests(); return; }
        if (args.Contains("--ai-sequencing-only")) { AiSequencingTests(); return; }
        if (args.Contains("--ai-recovery-only")) { AiRecoveryTests(); return; }
        if (args.Contains("--achievement-toast-only")) { AchievementToastQueue(); return; }
        if (args.Contains("--achievement-evidence-only")) { AchievementEvidenceIsTerminalAndRetryStable(); return; }
        if (args.Contains("--coin-skins-only")) { CoinSkinTests.Run(); return; }
        if (args.Contains("--deck-cosmetics-only")) { DeckCosmeticsSaveTests.Run(); return; }
        NativeMatch.ClockFreeBot = true;
        if (args.Contains("--pet-reactions-only")) { PetReactions(); return; }
        if (args.Contains("--npc-pets-only")) { NpcPets_(); return; }
        if (args.Contains("--pet-likes-only")) { PetLikesAndTreats(); return; }
        PlayerTurnTimerTests();
        RewindPresentationTags();
        CoinFlipTests();
        DebugScenarioTests();
        HeroSkinPresentationTests();
        BoardIdentityHistory();
        CosmeticsRuntimeTests.Run();
        CosmeticsMatchTests.Run();
        CoinSkinTests.Run();
        PetReactions();
        NpcPets_();
        PetLikesAndTreats();
        DeckCosmeticsSaveTests.Run();
        if (args.Contains("--cosmetics-only")) return;
        if (args.Contains("--premium-only")) { PremiumDeckAndGoldenHeroHistory(); NpcPremiumTests(); return; }
        if (args.Contains("--npc-premium-only")) { NpcPremiumTests(); return; }
        if (args.Contains("--start-universe-only")) { StartUniverseAndTrap(); return; }
        if (args.Contains("--deck-discover-hand-only")) { DeckDiscoverHandActor(); return; }
        if (args.Contains("--choice-retire-only")) { ChoiceActorRetirement(); DeckDiscoverHandActor(); return; }
        if (args.Contains("--opposing-choice-only")) { OpposingChoiceBacks(); return; }
        if (args.Contains("--choose-one-only")) { ChooseOneSubOptionHistory(); return; }
        if (GroupProofs.TryRun(args, allowUnmatched: true)) return;
        if (args.Contains("--holy-bola-live-replay")) { HolyBolaLiveReplay(); return; }
        if (args.Contains("--holy-bola-only")) { HolyBolaHistory(); return; }
        if (args.Contains("--forced-attack-only")) { ForcedAttackReadinessIsPublished(); return; }
        if (args.Contains("--cthun-only")) { CthunNativeProof(); return; }
        if (args.Contains("--bonus-only")) { Unpinned_BonusEffectsHistory(); return; }
        if (args.Length == 2 && args[0] == "--spell-healing-review") { SpellHealingReviewProof(args[1]); return; }
        if (args.Contains("--payloads-only")) { StoredPayloadProjection(); return; }
        if (args.Contains("--npc-runner-only")) { NpcTurnRunnerTests.Run(); return; }
        if (args.Contains("--npc-settle-only")) { NpcPresentationTests.AnimationSettle(); return; }
        if (args.Contains("--npc-presentation-only")) { NpcPresentationTests.Run(); NpcWorldIntegrationTests.Run(); return; }
        if (args.Contains("--npc-world-only")) { NpcWorldIntegrationTests.Run(); return; }
        DiscoverRealNative();
        if (args.Contains("--discover-real-only")) return;
        if (args.Contains("--cast-twice-only")) { CastTwiceMarkHistory(); return; }
        if (args.Contains("--companion-only")) { CompanionAndLarvaProjection(); return; }
        if (args.Contains("--overload-only")) { OverloadManaParity(); return; }
        if (args.Contains("--transform-history-only")) { TransformChangeClearsStaleDamage(); return; }
        if (args.Contains("--quests-only")) { AutomaticQuestHistory(); QuestNativeProof.Run(); return; }
        if (args.Contains("--auto-cast-only")) { AutomaticQuestHistory(); AutomaticSecretHistory(); FacelessEnigmaHistory(); return; }
        if (args.Contains("--overdraw-only")) { OverdrawHistory(); return; }
        if (args.Contains("--draw-spell-only")) { DrawSpellHistory(); return; }
        if (args.Contains("--subspell-only")) { ModernSubSpellHistory(); return; }
        if (args.Contains("--spell-vfx-only")) { SpellVfxHistory(); return; }
        if (args.Contains("--defile-only")) { DefilePrivateTagsNeverReachWire(); return; }
        if (args.Contains("--powered-up-only")) { PoweredUpGlowTests.Run(); return; }
        if (args.Contains("--showcase-only")) { FairViewHiddenOfferProof.Run(); return; }
        if (args.Contains("--keywords-only"))
        {
            TransformKeywordsReconcileFinalEngineState();
            KeywordProjectionUsesEffectiveEntityValues();
            WeaponKeywordLossClearsHeroAndWeapon();
            return;
        }
        if (args.Contains("--enchant-visibility-only")) { NativeMatch.ClockFreeBot = true; EnchantmentVisibilityHistory(); return; }
        if (args.Contains("--board-position-only")) { NativeMatch.ClockFreeBot = true; BoardPositionContract(); return; }
        if (args.Contains("--hand-text-only")) { HandTextCounters(); return; }
        if (args.Contains("--terminal-spell-only")) { TerminalDiscoverSpellLifecycle(); return; }
        if (args.Contains("--discover-only")) { DiscoverSpellLifecycle(args.ElementAtOrDefault(1)); TerminalDiscoverSpellLifecycle(); return; }
        // These legacy slices intentionally continue through a prefix of the default suite.
        if (GroupProofs.TryRun(args.Except(new[] { "--hand-buff-only", "--printed-tags-only", "--history-fields-only",
                "--spell-power-only", "--reveal-animation-only" }).ToArray())) return;
        PremiumDeckAndGoldenHeroHistory();
        NpcPremiumTests();
        StartUniverseAndTrap();
        Unpinned_BonusEffectsHistory();
        DiscoverSpellLifecycle();
        AutomaticQuestHistory();
        AutomaticSecretHistory();
        FacelessEnigmaHistory();
        HeroReplacementHistory();
        EnchantmentVisibilityHistory();
        BoardPositionContract();
        DiscoverSpellLifecycle("CORE_WON_350",scheduledRepeat:true);
        CastTwiceMarkHistory();
        SpellVfxHistory();
        ChillmawHistory();
        TriggerVisualKeywordHistory();
        DrawSpellHistory();
        OverdrawHistory();
        ChaosStrikeHistory();
        CthunNativeProof();
        HolyBolaHistory();
        GeneratedShuffleHistory();
        ShuffleBatchHistory();
        ModernSubSpellHistory();
        ChooseOneSubOptionHistory();
        TerminalDiscoverSpellLifecycle();
        TemporaryVisualHistory();
        HeldFormOverlayHistory();
        AgentCoinChoiceHistory();
        ShatterOpeningNative();
        ShatterFxHistory();
        ImbueFxHistory();
        HandBuffHistory();
        VanguardDrawHistory();
        BroxigarReturnHistory();
        GroupProofs.Run(ProofKind.Native);
        if (args.Contains("--hand-buff-only")) return;
        OverloadManaParity();
        PrintedCardTags();
        JadeGolemProjection();
        if (args.Contains("--printed-tags-only")) return;
        if (args.Contains("--history-fields-only"))
        {
            HistoryFieldsAndPrivacy();
            return;
        }
        HistoryFieldsAndPrivacy();
        DefilePrivateTagsNeverReachWire();
        if (args.Contains("--spell-power-only"))
        {
            SpellPowerSummaryTracksAcceptedActions();
            return;
        }
        ForcedAttackReadinessIsPublished();
        TransformCopyReadinessIsPublished();
        BefuddledCopyStateIsPublished();
        if (args.Contains("--reveal-animation-only"))
        {
            RevealAnimationHistory();
            return;
        }
        RevealAnimationHistory();
        OutcastGlowTests.Run();
        ComboGlowTests.Run();
        PoweredUpGlowTests.Run();
        DeathKnightNativeChecks();
        AchievementToastQueue();
        QuestNativeProof.Run();
        FairViewHiddenOfferProof.Run();
        DeckDiscoverHandActor();
        ChoiceActorRetirement();
        OpposingChoiceBacks();
        LocationOptions();
        LocationActivationHistory();
        LocationActivationFx();
        CompanionAndLarvaProjection();
        HeroPowersUsePinnedClientIds();
        SourceTradeAndBotDecisions();
        SourcePrepareAction();
        NativeTextUsesBoundedEngineRawDescriptions();
        EngineStartEvidenceIsExactAndImmutable();
        CustomDecksAndRewardFacts();
        StartingHandIsRevealedBeforeMulligan();
        StartProducesNativeSerializableGameAndRealMulligan();
        BotMulliganAcknowledgesEngineKeeps();
        SpellPowerSummaryTracksAcceptedActions();
        InvalidAndStaleOptionsLeaveStateUnchanged();
        LegalPacketActionsFinishARealMatch();
        MatchJournalNative();
        HandTargetChoiceHistory();
        ChoiceToolsNative();
        NpcTurnRunnerTests.Run();
        NpcPresentationTests.Run();
        NpcWorldIntegrationTests.Run();
        ConcedeFinishesThroughEngine();
        NpcConcedeForTestWinsThroughEngine();
        LoseForTestIsAPlainLoss();
        PriestPowerHealsFriendlyCharacters();
        PaladinPowerAndAuxiliaryHistoriesSerialize();
        WeaponDurabilityUsesNativeHealth();
        VisibleTransportSnapshotUsesExactHistoryContract();
        HandTextCounters();
        StoredPayloadProjection();
        MatchEventLogNative();
        MatchEventLogReview();
        UnregisteredDefinitionPreservesHistorySnapshots();
        PlayedHistoryCostPrecedesPlayStart();
        BotBattlecryControlChangeIsRevealedBeforePlay();
        CatalogPairingsComplete();
        TestLoanerReachesDecisionPaths();
        StandardBatchLoanerTransport();
        AutomaticAdmissionFixtures();
        FinishAdmissionSweep();
        ExpandedCatalogTransportSmoke();
        ExpandedMechanismTransport();
        TransformChangeClearsStaleDamage();
        TransformKeywordsReconcileFinalEngineState();
        KeywordProjectionUsesEffectiveEntityValues();
        WeaponKeywordLossClearsHeroAndWeapon();
        NonMinionChangeDoesNotGainTransformResets();
        ExactCopyTaskBridgeIsPresent();
        ExactAuraRemovalBridgeIsPresent();
        CopyCompletionIsImmutableAndDestinationScoped();
        CopyCompletionIsBatchOwnedAndOneShot();
        MirrorSummonCopyPreservesNoCopyOutcomes();
        ExactTransformCopyTaskSeamCapturesFinalState();
        ExactSummonCopyTaskSeamUsesControllerBoardAndEventSource();
        MirrorEntityHistoryUsesDestinationIdentity();
        AuraOwnerRemovalRestoresSerializedHandCosts();
        AuraStackingChangedOwnerAndOpponentHandPrivacy();
        AuraBoardAndSwitchingRemovalSerializeActualTags();
        AuraQueuedCallerPathsRestoreCosts();
        AuraCorrectionOrderAndLifetime();
        AuraCorrectionsRespectReplayVisibility();
        Console.WriteLine("PASS native match contract");
    }

    private static void AchievementEvidenceIsTerminalAndRetryStable()
    {
        var match = new NativeMatch((_, __) => { }, "mage", "priest", 73);
        match.Start();
        var pending = new LocalAchievementPendingContext {
            MatchId = match.MatchId,
            EligibleWorldMatch = true,
            StartingHero = match.Game.Player1.Hero.Card.Id,
            StartingHeroClass = "MAGE",
            QueuedNpcId = "npc-proof",
            QueuedNpcBand = "Diamond"
        };
        bool rejected = false;
        try { match.AchievementEvidence(pending); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "achievement evidence escaped before terminal result");
        Check(match.Game.Process(ConcedeTask.Any(match.Game.Player2)), "opponent concession rejected");
        var first = match.AchievementEvidence(pending);
        var retry = match.AchievementEvidence(pending);
        Check(first != null && ReferenceEquals(first, retry) && first.Available && first.Result == "Won",
            "terminal achievement evidence was not immutable across save retry");
        Check(first.Facts.Last() is MatchTerminalFact && first.Facts.SequenceEqual(match.Game.HistoryFacts.Records),
            "achievement evidence did not freeze the final authoritative ledger");
        pending.MainDeck.Add(new LocalAchievementDeckCard { BaseCardId = "CS2_029", Copies = 1, CardType = "SPELL" });
        bool changed = false;
        try { match.AchievementEvidence(pending); } catch (InvalidOperationException) { changed = true; }
        Check(changed, "achievement retry accepted changed frozen pending context");

        string root = Path.Combine(Path.GetTempPath(), "achievement-native-settle-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            using var profile = new LocalProfile(Path.Combine(root, "profile.xml"));
            int seed = 1000;
            while (profile.Stars < LadderRules.LegendIndex - 1)
            {
                var ordinary = profile.Begin("mage", "priest", seed++);
                profile.Settle(ordinary.Id, "Won", 10, false);
                profile.AcknowledgeRewardPresentation(ordinary.Id);
            }
            string noCatchupPath = Path.Combine(root, "no-linked-catchup.xml");
            File.Copy(Path.Combine(root, "profile.xml"), noCatchupPath);
            var context = profile.AchievementContextFor(LocalDecks.Get("mage"), false, "npc-proof", 2500, "Diamond");
            NativeMatch integrated = null;
            var record = profile.BeginLaunch("mage", "priest", 74, "npc-proof", "priest", 2500, context, queued => {
                integrated = new NativeMatch((_, __) => { }, "mage", "priest", 74) { MatchId = queued.Id };
                integrated.Start();
            });
            profile.MarkStarted(record.Id);
            Check(integrated.Game.Process(ConcedeTask.Any(integrated.Game.Player2)), "integrated NPC concession rejected");
            var evidence = integrated.AchievementEvidence(profile.Pending.AchievementContext);
            byte[] beforeSettlement = File.ReadAllBytes(Path.Combine(root, "profile.xml"));
            LocalProfile.NpcLegendNumber = (_, __) => 1;
            LocalProfile.CommitFaultHook = phase => { if (phase == "replace") throw new IOException("achievement settlement fault"); };
            bool saveFailed = false;
            try { profile.Settle(record.Id, "Won", integrated.Game.Turn, false, integrated.RewardFacts, 2500, null, evidence); }
            catch (IOException) { saveFailed = true; }
            finally { LocalProfile.CommitFaultHook = null; LocalProfile.NpcLegendNumber = null; }
            Check(saveFailed && File.ReadAllBytes(Path.Combine(root, "profile.xml")).SequenceEqual(beforeSettlement)
                && profile.Pending?.Id == record.Id && profile.Achievements.Career.EligibleWins == 0
                && profile.Achievements.Grants.Count == 0, "failed achievement settlement mutated disk or memory");
            LocalMatchRecord settled;
            LocalProfile.NpcLegendNumber = (_, __) => 1;
            try { settled = profile.Settle(record.Id, "Won", integrated.Game.Turn, false, integrated.RewardFacts, 2500, null, evidence); }
            finally { LocalProfile.NpcLegendNumber = null; }
            string[] linked = { "welcome-to-legend", "among-the-hundred", "there-can-be-only-one" };
            Check(settled.AchievementEvidenceFingerprint == evidence.Fingerprint
                && profile.Achievements.Career.EligibleWins == 1
                && profile.Achievements.Career.ClassWins.Single(row => row.Hero == "MAGE").Wins == 1
                && settled.AchievementGrants.Where(row => linked.Contains(row.AchievementId))
                    .Select(row => row.AchievementId).SequenceEqual(linked)
                && settled.AchievementGrants.Where(row => linked.Contains(row.AchievementId))
                    .All(row => row.Gold == 0 && row.Dust == 0 && row.StandardPacks == 0
                    && row.CardBackId == 0 && row.HeroSkinId == null && row.LinkedMilestoneId != null),
                "native evidence did not settle into the V14 career transaction");
            int grants = profile.Achievements.Grants.Count;
            Check(profile.Settle(record.Id, "Won", integrated.Game.Turn, false, integrated.RewardFacts, 2500, null, evidence).Id == record.Id
                && profile.Achievements.Career.EligibleWins == 1 && profile.Achievements.Grants.Count == grants,
                "native evidence retry replayed career or grants");

            foreach (var corruption in new Action<LocalProfileData>[] {
                save => save.Achievements.Unlocks.Single(row => row.Id == "welcome-to-legend").Witness =
                    new LocalAchievementWitnessRecord { Kind = "Terminal", Sequence = 1 },
                save => save.Achievements.Unlocks.Single(row => row.Id == "welcome-to-legend").Witness.Identity = "legend:top-100",
                save => save.Matches.Single(row => row.Id == record.Id).AchievementContext.MainDeck[0].BaseCost = -1,
                save => save.Matches.Single(row => row.Id == record.Id).AchievementContext.StartingHeroClass = "WARRIOR",
                save => save.Matches.Single(row => row.Id == record.Id).AchievementContext.PersonalDeck = true,
                save => save.Matches.Single(row => row.Id == record.Id).AchievementContext.CraftedAndOwnedBaseCardIds.Add("CS2_029"),
                save => save.Achievements.Career.PlayedCollectibleBaseCardIds = new List<string> { "NOT A CARD" }
            })
            {
                var invalid = ReadProfile(Path.Combine(root, "profile.xml")); corruption(invalid);
                string invalidPath = Path.Combine(root, "invalid-achievement-" + Guid.NewGuid() + ".xml");
                WriteProfile(invalidPath, invalid); byte[] invalidBytes = File.ReadAllBytes(invalidPath);
                bool strictRejected = false;
                try { using var ignored = new LocalProfile(invalidPath); } catch (InvalidOperationException) { strictRejected = true; }
                Check(strictRejected && File.ReadAllBytes(invalidPath).SequenceEqual(invalidBytes),
                    "strict achievement context/witness corruption was accepted or rewritten");
            }

            // Frozen history keeps the card data of its day: a later cost/rarity/set fix or a retired card id still loads.
            Check(Cards.All.Where(card => card != null && card.Collectible).All(card => LocalProfile.ValidAchievementCardId(LocalCardCatalog.CopyGroup(card.Id))),
                "achievement card-id shape rejects a real collectible copy group");
            foreach (var retune in new Action<LocalProfileData>[] {
                save => save.Matches.Single(row => row.Id == record.Id).AchievementContext.MainDeck[0].BaseCost++,
                save => { var card = save.Matches.Single(row => row.Id == record.Id).AchievementContext.MainDeck[0];
                    card.Rarity = card.Rarity == "EPIC" ? "RARE" : "EPIC"; card.CardSet = "EXPERT1"; },
                save => save.Achievements.Career.PlayedCollectibleBaseCardIds = new List<string> { "RETIRED_CARD_001" }
            })
            {
                var tuned = ReadProfile(Path.Combine(root, "profile.xml")); retune(tuned);
                string tunedPath = Path.Combine(root, "retuned-achievement-" + Guid.NewGuid() + ".xml");
                WriteProfile(tunedPath, tuned);
                using (var reopened = new LocalProfile(tunedPath))
                    Check(reopened.Achievements.Career.EligibleWins == 1, "card-data drift in frozen achievement history blocked load");
            }

            using var noCatchup = new LocalProfile(noCatchupPath);
            LocalProfile.NpcLegendNumber = (_, __) => 1;
            LocalMatchRecord oldMilestone;
            try {
                var direct = noCatchup.Begin("mage", "priest", 75);
                oldMilestone = noCatchup.Settle(direct.Id, "Won", 10, false);
            } finally { LocalProfile.NpcLegendNumber = null; }
            Check(oldMilestone.TierRewards.Any(row => row.MilestoneId == "legend")
                && oldMilestone.AchievementGrants.Count == 0, "ineligible milestone fixture did not reach Legend without links");
            noCatchup.AcknowledgeRewardPresentation(oldMilestone.Id);
            var laterContext = noCatchup.AchievementContextFor(LocalDecks.Get("mage"), false, "npc-later", 2500, "Legend");
            NativeMatch later = null;
            var laterRecord = noCatchup.BeginLaunch("mage", "priest", 76, "npc-later", "priest", 2500, laterContext, queued => {
                later = new NativeMatch((_, __) => { }, "mage", "priest", 76) { MatchId = queued.Id };
                later.Start();
            });
            noCatchup.MarkStarted(laterRecord.Id);
            Check(later.Game.Process(ConcedeTask.Any(later.Game.Player2)), "later NPC concession rejected");
            var laterEvidence = later.AchievementEvidence(noCatchup.Pending.AchievementContext);
            var laterSettled = noCatchup.Settle(laterRecord.Id, "Won", later.Game.Turn, false, later.RewardFacts, 2500, null, laterEvidence);
            Check(!laterSettled.AchievementGrants.Any(row => linked.Contains(row.AchievementId)),
                "later eligible match caught up an earlier ineligible linked milestone");

            AchievementLossAtCareerCap(root);
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine("PASS achievement evidence: terminal-only immutable retry snapshot and V14 settlement");
    }

    private static void AchievementLossAtCareerCap(string root)
    {
        string path = Path.Combine(root, "loss-career-cap.xml");
        using (var profile = new LocalProfile(path))
        {
            profile.CommitChange(next => {
                next.Achievements.Career.CreditedEnemyMinionDeaths = 999;
                return true;
            });
            var context = profile.AchievementContextFor(LocalDecks.Get("mage"), false, "npc-career-cap", 2500, "Diamond");
            var capture = new Capture();
            NativeMatch match = null;
            var record = profile.BeginLaunch("mage", "priest", 77, "npc-career-cap", "priest", 2500, context, queued => {
                match = new NativeMatch(capture.Send, "mage", "priest", 77) { MatchId = queued.Id };
                match.Start();
            });
            profile.MarkStarted(record.Id);
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());

            var human = match.Game.Player1;
            var opponent = match.Game.Player2;
            human.BaseMana = 10;
            QuestNativeProof.Summon(match.Game, opponent, "CS2_231");
            var victim = opponent.BoardZone.Single(minion => minion.Card.Id == "CS2_231");
            var attacker = (Minion)QuestNativeProof.Play(match.Game, human, "CORE_SW_068");
            attacker.IsExhausted = false;
            Check(match.Game.Process(MinionAttackTask.Any(human, attacker, victim))
                && victim.Zone == opponent.GraveyardZone, "career-cap fixture did not record the credited enemy death");
            Check(match.Game.Process(EndTurnTask.Any(human)), "career-cap fixture could not enter opponent turn");
            opponent.BaseMana = 10;
            opponent.UsedMana = 0;
            human.Hero.Damage = 29;
            var lethal = QuestNativeProof.Add(opponent, "CORE_CS2_029");
            var lethalTask = opponent.Options().Single(task => task.Source?.Id == lethal.Id && task.Target?.Id == human.Hero.Id);
            Check(match.Game.Process(lethalTask) && match.Game.State == State.COMPLETE && human.PlayState == PlayState.LOST
                && match.Game.HistoryFacts.Query.Events<MatchTerminalFact>().Count() == 1,
                "career-cap fixture did not finish with an engine loss");

            var evidence = match.AchievementEvidence(profile.Pending.AchievementContext);
            var settled = profile.Settle(record.Id, "Lost", match.Game.Turn, false, match.RewardFacts, 2500, null, evidence);
            Check(profile.Achievements.Career.CreditedEnemyMinionDeaths == 1000
                && profile.Achievements.Career.PlayedCollectibleBaseCardIds.Contains("SW_068")
                && profile.Achievements.Unlocks.Count(row => row.Id == "the-board-is-a-workplace-hazard") == 1
                && settled.AchievementGrants.Any(row => row.AchievementId == "the-board-is-a-workplace-hazard"),
                "loss settlement did not cap career progress and save the earned achievement");
        }
        using (var restarted = new LocalProfile(path))
            Check(restarted.Pending == null && restarted.Achievements.Career.CreditedEnemyMinionDeaths == 1000
                && restarted.Achievements.Career.PlayedCollectibleBaseCardIds.Contains("SW_068")
                && restarted.Achievements.Unlocks.Count(row => row.Id == "the-board-is-a-workplace-hazard") == 1,
                "loss career settlement did not survive reopen");
    }

    private static LocalProfileData ReadProfile(string path)
    {
        using var stream = File.OpenRead(path);
        return (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(stream);
    }

    private static void WriteProfile(string path, LocalProfileData profile)
    {
        using var stream = File.Create(path);
        new XmlSerializer(typeof(LocalProfileData)).Serialize(stream, profile);
    }

    private static readonly Lazy<HashSet<string>> PinnedClientIds = new Lazy<HashSet<string>>(() => {
        using var file = File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz");
        using var gzip = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionMode.Decompress);
        using var export = System.Text.Json.JsonDocument.Parse(gzip);
        return export.RootElement.GetProperty("cards").EnumerateArray()
            .Select(card => card.GetProperty("cardId").GetString()).ToHashSet(StringComparer.Ordinal);
    });

    private static void CheckPinnedClientIds(Capture capture)
    {
        // Check wire identities literally; applying TransportCardId here would mask missing aliases.
        string[] missing = EmittedIds(History(capture, 0)).Where(id => !PinnedClientIds.Value.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        Check(missing.Length == 0, "emitted card IDs absent from pinned client export: " + string.Join(", ", missing));
    }

    private static void HeroPowersUsePinnedClientIds()
    {
        foreach (var heroClass in Cards.HeroClasses)
        {
            var hero = Cards.HeroCard(heroClass);
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, hero, Enumerable.Repeat(Cards.FromId("CS2_182"), 30), 1337);
            match.Start();
            CheckPinnedClientIds(capture);
            Check(EmittedIds(History(capture, 0)).Contains(hero.Id + "bp"),
                "default class hero power uses pinned client identity: " + heroClass);
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            match.Game.Player1.BaseMana = 10;
            var justicar = SabberStoneCore.Model.Entities.Entity.FromCard(match.Game.Player1,
                Cards.FromId("AT_132"), zone: match.Game.Player1.HandZone);
            Check(match.Game.Process(PlayCardTask.Any(match.Game.Player1, justicar)), "Justicar upgrade accepted: " + heroClass);
            FlushHistory(match);
            Check(match.Game.Player1.Hero.HeroPower.Card.Id == "AT_132_" + heroClass
                && NativeMatch.TransportCardId("AT_132_" + heroClass) == hero.Id + "bp2"
                && EmittedIds(History(capture, 0)).Contains(hero.Id + "bp2"),
                "upgraded hero power retains engine identity and emits pinned native alias: " + heroClass);
            capture.SerializeAll();
        }
        Check(NativeMatch.TransportCardId("AT_132_UNKNOWN") == "AT_132_UNKNOWN", "upgraded power alias mapping stays exact");
        Console.WriteLine("PASS all default and upgraded class hero powers use pinned client identities");
    }

    private static void SourceTradeAndBotDecisions()
    {
        var capture = new Capture();
        var match = StartAtHumanTurn(capture);
        var game = match.Game;
        game.Player1.BaseMana = 10;
        var card = SabberStoneCore.Actions.Generic.DrawCard(game.Player1, Cards.FromId("CORE_EX1_005"));
        typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, new object[] { game.PowerHistory.Last });
        typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
        var options = capture.Last<AllOptions>(14);
        int trade = options.Options.FindIndex(o => o.MainOption?.Id == card.Id && o.MainOption.PowerKeyword == 1720);
        Check(trade >= 0 && options.Options.Any(o => o.MainOption?.Id == card.Id && o.MainOption.PowerKeyword == 0),
            "production source build needs distinct play/Trade options");
        int mana = game.Player1.RemainingMana, deck = game.Player1.DeckZone.Count, hand = game.Player1.HandZone.Count;
        match.SendOption(options.Id, trade, 0, -1, 0);
        Check(game.Player1.RemainingMana == mana - 1 && game.Player1.HandZone.Count == hand && game.Player1.DeckZone.Count == deck
            && !game.Player1.HandZone.Contains(card), "production Trade did not spend one and replace hand card");
        Check(capture.Packets.Where(p => p.Id == 19).SelectMany(p => ((PegasusGame.PowerHistory)p.Message).List)
            .Any(e => e.HasTagChange && e.TagChange.Entity == card.Id && e.TagChange.Tag == 2045 && e.TagChange.Value == 0),
            "production Trade reset missing from native history");

        game.Process(EndTurnTask.Any(game.Player1));
        game.Player2.BaseMana = 10;
        var cenarius = SabberStoneCore.Actions.Generic.DrawCard(game.Player2, Cards.FromId("EDR_209"));
        game.Process(PlayCardTask.Any(game.Player2, cenarius));
        Check(game.Resolution.HasSuspendedEffect, "bot Cenarius did not suspend");
        typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, new object[] { game.PowerHistory.Last });
        typeof(NativeMatch).GetMethod("DriveUntilHumanInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
        Check(!game.Resolution.HasSuspendedEffect && game.Player2.Choice == null && game.CurrentPlayer == game.Player1,
            "production fallback bot did not answer all suspended prompts");
        capture.SerializeAll();
        Console.WriteLine("PASS production ENGINE_SOURCE Trade and three suspended bot decisions");
    }

    private static void SourcePrepareAction()
    {
        var capture = new Capture();
        var match = StartAtHumanTurn(capture);
        var game = match.Game;
        var player = game.Player1;
        player.BaseMana = 10; player.UsedMana = 0;
        var card = SabberStoneCore.Actions.Generic.DrawCard(player, Cards.FromId("CS2_182"));
        SabberStoneCore.Actions.PrepareRules.Grant(card);
        typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, new object[] { game.PowerHistory.Last });
        typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
        var options = capture.Last<AllOptions>(14);
        int prepare = options.Options.FindIndex(o => o.MainOption?.Id == card.Id && o.MainOption.PowerKeyword == 4354);
        Check(prepare >= 0 && options.Options.Any(o => o.MainOption?.Id == card.Id && o.MainOption.PowerKeyword == 0), "distinct play/Prepare options");
        AssertRejectedWithoutMutation(match, capture, () => match.SendOption(options.Id, prepare, game.Player2.Hero.Id, -1, 0));
        AssertRejectedWithoutMutation(match, capture, () => match.SendOption(options.Id, prepare, 0, 0, 0));
        AssertRejectedWithoutMutation(match, capture, () => match.SendOption(options.Id, prepare, 0, -1, 1));
        int hand = player.HandZone.Count, deck = player.DeckZone.Count, position = card.ZonePosition;
        match.SendOption(options.Id, prepare, 0, -1, 0);
        Check(card.Zone == player.HandZone && card.ZonePosition == position && player.HandZone.Count == hand
            && player.DeckZone.Count == deck && player.RemainingMana == 7 && card.Cost == 0, "native Prepare payment and hand identity");
        Check(!capture.Last<AllOptions>(14).Options.Any(o => o.MainOption?.Id == card.Id), "prepared card locked in options");
        AssertRejectedWithoutMutation(match, capture, () => match.SendOption(options.Id, prepare, 0, -1, 0));
        capture.SerializeAll();
        Console.WriteLine("PASS production ENGINE_SOURCE Prepare: keyword routing, parameter validation, payment, lock and stale packet rejection");
    }

    private static void NativeTextUsesBoundedEngineRawDescriptions()
    {
        var nameOverride = typeof(LocalNativeText).GetMethod("TryGetNameOverride",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Check(nameOverride != null, "bounded enchantment name policy is implemented");
        int nameTransforms = 0;
        Func<string, string> transformName = raw => { nameTransforms++; return "rendered:" + raw; };
        object[] chargeName = { true, "EX1_084e", transformName, null };
        Check((bool)nameOverride.Invoke(null, chargeName) && (string)chargeName[3] == "rendered:Charge"
            && nameTransforms == 1, "active Warsong enchantment renders transformed engine Charge name");
        foreach (var input in new[] {
            (false, "EX1_084e"), (true, "CURRENT_ONLY"), (true, "EX1_084"), (true, "ex1_084e"),
            (true, "EX1_577"), (true, "EX1_614"), (true, "EX1_614t"), (true, "EX1_finkle"), (true, (string)null)
        })
        {
            object[] unchangedName = { input.Item1, input.Item2, transformName, null };
            Check(!(bool)nameOverride.Invoke(null, unchangedName) && unchangedName[3] == null
                && nameTransforms == 1, "inactive or non-Warsong name preserves native lookup: " + input.Item2);
        }
        object[] noNameTransform = { true, "EX1_084e", null, null };
        Check(!(bool)nameOverride.Invoke(null, noNameTransform) && noNameTransform[3] == null,
            "missing name transformer preserves native lookup");

        string text;
        Check(!LocalNativeText.TryGetOverride(false, "CS2_089", value => value, out text),
            "unstaged native text was replaced");
        Check(!LocalNativeText.TryGetOverride(true, "CURRENT_ONLY", value => value, out text),
            "unknown native text was replaced");
        Check(!LocalNativeText.TryGetOverride(true, "CS2_tk1", value => value, out text),
            "empty engine text was replaced");
        Check(!LocalNativeText.TryGetOverride(true, null, value => value, out text),
            "null card ID was replaced");

        Check(LocalNativeText.TryGetOverride(true, "CS2_089", value => value, out text) &&
              text == "Restore #6 Health.", "Holy Light did not use pinned engine text");
        Check(LocalNativeText.TryGetOverride(true, "HERO_04bp", value => value, out text) &&
              text == "<b>Hero Power</b>\nSummon a 1/1 Silver Hand Recruit.",
            "native Paladin power text was: " + text);
        Check(LocalNativeText.TryGetOverride(true, "GAME_005", value => value, out text) &&
              text == "Gain 1 Mana Crystal this turn only.", "Coin text missing");
        Check(LocalNativeText.TryGetOverride(true, "CS2_mirror", value => value, out text) &&
              text == "<b>Taunt</b>", "Mirror Image token text missing");

        var classicIds = new HashSet<string>(LocalCardCatalog.Current.Select(card => card.Id), StringComparer.Ordinal);
        Check(LocalCardCatalog.Current.Count == 252 && classicIds.Count == 252,
            "native text fixture no longer has current 252-card Classic catalog");
        using var schedule = System.Text.Json.JsonDocument.Parse(File.ReadAllText("data/progression/core-2026-unlocks.json"));
        var coreIds = new HashSet<string>(schedule.RootElement.GetProperty("cards").EnumerateArray()
            .Select(card => card.GetProperty("cardId").GetString()), StringComparer.Ordinal);
        Check(coreIds.Count == 289 && coreIds.SetEquals(LocalCoreProgression.Unlocks.Select(card => card.Id)),
            "native text fixture differs from generated V8 Core unlock set (289 cards)");
        var expectedCatalogIds = new HashSet<string>(classicIds, StringComparer.Ordinal);
        expectedCatalogIds.UnionWith(Cards.All.Where(card => coreIds.Contains(card.Id)
            && card.Implemented && card.Collectible).Select(card => card.Id));
        Check(LocalEconomy.Catalog.Count == expectedCatalogIds.Count
            && expectedCatalogIds.SetEquals(LocalEconomy.Catalog.Select(card => card.Id)),
            "native text catalog must contain all Classic cards and exactly the engine-supported Core unlocks");
        foreach (var card in LocalEconomy.Catalog)
            if (string.IsNullOrEmpty(card.Text))
                Check(!LocalNativeText.TryGetOverride(true, card.Id, value => value, out text),
                    "empty catalog text replaced client text for " + card.Id);
            else
                Check(LocalNativeText.TryGetOverride(true, card.Id, value => value, out text) &&
                      !string.IsNullOrEmpty(text) && text == LocalNativeText.NormalizeRawText(card.Text),
                    "nonempty catalog raw text missing or changed for " + card.Id);

        foreach (var (id, expectedRaw) in new[] {
            ("CORE_BAR_541", "Deal $2 damage. <b>Discover</b> a spell."),
            ("CORE_BT_321", "<b>Battlecry:</b>& <b>Discover</b> a Demon."),
            ("CORE_BT_921", "<b>Lifesteal</b>"),
            ("CORE_DRG_024", "<b>Battlecry:</b>& Add a random Pirate to your hand."),
            ("CORE_EDR_001", "<b>Battlecry:</b>& Add 2 random Mage spells to your hand."),
            ("CORE_EDR_004_2026", "[x]<b>Rewind</b>\n<b>Battlecry:</b> <b>Discover</b> a Beast\nwith a <b>Dark Gift</b>. <b>Kindred:</b>\nIt costs (1) less."),
            ("CORE_ONY_022", "<b>Battlecry:</b>& <b>Discover</b> a\nHoly spell."),
            ("CORE_RLK_745", "[x]<b>Reborn</b>\nAt the end of your turn,\nspend 4 <b>Corpses</b> to summon\na copy of this minion."),
            ("CORE_SW_442", "<b>Lifesteal</b>\nDeal $4 damage."),
            ("CORE_ULD_178", "[x]<b>Battlecry:</b> Gain 2 of <b>Rush</b>,\n<b>Taunt</b>, <b>Divine Shield</b>, or\n<b>Windfury</b> <i>(your choice).</i>"),
            ("CORE_WON_350", "<b>Discover</b> a <b>Taunt</b> minion. Give it +1/+2."),
            ("CORE_YOP_001", "<b>Discover</b> an <b>Outcast</b> card. Your next one costs (1) less.")
        })
            Check(coreIds.Contains(id) && LocalEconomy.Catalog.Any(card => card.Id == id && card.Text == expectedRaw)
                && LocalNativeText.TryGetOverride(true, id, value => value, out text) && text == expectedRaw,
                "Core V5 unlock engine/native raw description changed: " + id);

        string[] generated = {
            "CS2_tk1", "skele21", "CS2_mirror", "EX1_110t", "CS2_101t",
            "NEW1_012o", "EX1_019e", "EX1_603e", "CS2_222o", "CS2_004e", "EX1_561e",
            "EX1_411e", "EX1_414e", "CS1_129e", "CS2_236e", "CS2_087e", "CS2_092e",
            "EX1_360e", "EX1_382e", "EX1_619e", "EX1_366e", "EX1_355e"
        };
        var expectedIds = new HashSet<string>(LocalEconomy.Catalog.Select(card => card.Id), StringComparer.Ordinal);
        foreach (string hero in LocalDecks.PlayerHeroes)
        {
            expectedIds.Add(hero);
            string power = Cards.FromAssetId(Cards.FromId(hero)[GameTag.HERO_POWER]).Id;
            string nativePower = NativeMatch.TransportCardId(power);
            expectedIds.Add(nativePower);
            Check(LocalNativeText.SupportedIds.Contains(hero) && LocalNativeText.SupportedIds.Contains(nativePower),
                "player hero and native power text scope " + hero);
            Check(LocalNativeText.TryGetOverride(true, nativePower, value => value, out text)
                && text == LocalNativeText.NormalizeRawText(Cards.FromId(power).Text), "power native text " + hero);
        }
        Check(LocalNativeText.SupportedIds.Contains("HERO_11") && LocalNativeText.SupportedIds.Contains("HERO_11bp"),
            "Death Knight native text authority missing");
        expectedIds.Add("GAME_005");
        foreach (string id in generated)
        {
            expectedIds.Add(id);
            Check(LocalNativeText.SupportedIds.Contains(id), "generated ID missing from native text scope: " + id);
        }
        foreach (string id in LocalCardCatalog.SupplementalGeneratedIds)
        {
            expectedIds.Add(NativeMatch.TransportCardId(id));
            Check(LocalNativeText.SupportedIds.Contains(NativeMatch.TransportCardId(id)),
                "supplemental ID missing from native text scope: " + id);
        }
        expectedIds.UnionWith(LocalDecks.TestStandardCardIds.Select(NativeMatch.TransportCardId));
        expectedIds.UnionWith(LocalDecks.TestLoaners.SelectMany(deck => deck.CardIds).Select(NativeMatch.TransportCardId));
        expectedIds.UnionWith(new[] { "TIME_609t1", "TIME_609t2" });
        expectedIds.UnionWith(LocalCardCatalog.CardStateGeneratedIds.Select(NativeMatch.TransportCardId));
        Check(expectedIds.SetEquals(LocalNativeText.SupportedIds), "native text supported-ID scope widened or narrowed");
        foreach (string id in new[] { "EX1_577", "EX1_614", "EX1_614t", "EX1_finkle" })
            Check(!LocalNativeText.SupportedIds.Contains(id)
                && !LocalNativeText.TryGetOverride(true, id, value => value, out text),
                "quarantined identity must not receive supported native text: " + id);
        foreach (string id in new[] { "DREAM_01", "DREAM_02", "DREAM_03", "DREAM_04", "DREAM_05" })
            Check(LocalNativeText.SupportedIds.Contains(id), "Dream card missing from native text scope: " + id);

        foreach (string id in new[] {
            "CS2_024", "CS2_029", "CS2_032", "CS2_114", "EX1_400", "CS1_130", "CS1_112",
            "CS2_031", "CS2_028", "EX1_279", "EX1_410", "EX1_391", "CS2_093", "CS2_094"
        })
        {
            string raw = Cards.FromId(id).Text;
            Check(raw.EndsWith(" @spelldmg", StringComparison.Ordinal), "marker fixture changed for " + id);
            Check(LocalNativeText.TryGetOverride(true, id, value => value, out text) &&
                  text == raw.Substring(0, raw.Length - " @spelldmg".Length),
                "terminal spell-damage marker not removed for " + id);
        }
        string fireblastRaw = Cards.FromId("CS2_034").Text;
        Check(fireblastRaw.EndsWith(" @spelldmg", StringComparison.Ordinal) &&
              LocalNativeText.TryGetOverride(true, "HERO_08bp", value => value, out text) &&
              text == fireblastRaw.Substring(0, fireblastRaw.Length - " @spelldmg".Length),
            "mapped Fireblast marker not removed");

        foreach (string unchanged in new[] {
            "@spelldmg", "Text @spelldmg ", "Text @spelldmg\n", "Text @SpellDmg", "Text @spelldmg after",
            "[x] Deal $3_damage and restore #4. <b>Bold</b> @builder"
        }) Check(LocalNativeText.NormalizeRawText(unchanged) == unchanged,
            "non-terminal marker or native markup changed: " + unchanged.Replace("\n", "\\n"));
        Check(LocalNativeText.NormalizeRawText(null) == null, "null raw text changed");

        string variantInput = null;
        Check(LocalNativeText.TryGetOverride(true, "CS2_089", value => {
            variantInput = value;
            return "[x] Deal $3_damage and restore #4. <b>Bold</b> @builder";
        }, out text) && variantInput == "Restore #6 Health." &&
              text == "[x] Deal $3_damage and restore #4. <b>Bold</b> @builder",
            "default variant selection or native macros/markup were changed");
        Check(LocalNativeText.TryGetOverride(true, "CS2_089", value => "Variant @spelldmg", out text) &&
              text == "Variant", "obsolete suffix was not removed after default variant selection");
        Check(!LocalNativeText.TryGetOverride(true, "CS2_089", value => throw new InvalidOperationException(), out text),
            "native text transform failure did not fall back");
        Console.WriteLine("PASS bounded native engine text");
    }

    private static void EngineStartEvidenceIsExactAndImmutable()
    {
        // Replace both Mana Wyrms: this multiset is distinct from every loaner.
        string[] ids = LocalDecks.Get("mage").CardIds.Select(id => id == "NEW1_012" ? "EX1_066" : id).ToArray();
        string expected = string.Join(",", ids.OrderBy(id => id, StringComparer.Ordinal));
        Check(!LocalDecks.All.Any(deck => string.Join(",", deck.CardIds.OrderBy(id => id, StringComparer.Ordinal)) == expected),
            "engine evidence fixture must be a unique personal deck");
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, LocalDecks.Custom("Exact personal", "HERO_08", ids), "warrior", 8427);
        match.Start();
        Check(match.EngineStartHero == "HERO_08", "engine start hero differs from selected personal hero");
        Check(match.EngineStartCount == 30 && match.EngineStartCards == expected,
            "engine start multiset differs from exact selected 30 cards");
        Check(match.EngineStartCards == string.Join(",", match.Game.Player1.DeckZone.Concat(match.Game.Player1.HandZone)
            .Select(card => card.Card.Id).OrderBy(id => id, StringComparer.Ordinal)),
            "start evidence differs from actual engine deck and hand");
        match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
        SabberStoneCore.Model.Entities.Entity.FromCard(match.Game.Player1, Cards.FromId("GAME_005"), zone: match.Game.Player1.HandZone);
        Check(match.Game.Player1.DeckZone.Concat(match.Game.Player1.HandZone).Any(card => card.Card.Id == "GAME_005"),
            "engine mutation fixture did not change live cards");
        Check(match.EngineStartHero == "HERO_08" && match.EngineStartCount == 30 && match.EngineStartCards == expected,
            "engine start evidence changed after mulligan or live card mutation");
        Console.WriteLine("PASS exact immutable personal engine-start evidence");
    }

    private static void CustomDecksAndRewardFacts()
    {
        foreach (string id in new[] { "mage", "warrior", "priest", "paladin" })
        {
            LocalDeck source = LocalDecks.Get(id);
            var cards = source.CardIds;
            LocalDeck custom = LocalDecks.Custom("Test " + id, source.Hero, cards);
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, custom, "mage", 8427);
            cards[0] = "GAME_005";
            custom.CardIds[0] = "GAME_005";
            match.Start();
            Check(match.Game.Player1.Hero.Card.Id == source.Hero, "custom hero snapshot changed for " + id);
            Check(match.Game.Player1.DeckZone.Concat(match.Game.Player1.HandZone)
                .All(card => card.Card.Id != "GAME_005"), "custom card snapshot changed for " + id);
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            DriveToTerminal(match, capture);
            Check(match.Game.State == State.COMPLETE, "custom match did not finish for " + id);
            capture.SerializeAll();
        }

        bool unsupported = false;
        try
        {
            var invalid = new LocalDeck("Invalid", "HERO_05", LocalDecks.Get("mage").CardIds.ToArray());
            new NativeMatch((_, __) => { }, invalid, "mage", 1);
        }
        catch (InvalidOperationException) { unsupported = true; }
        Check(unsupported, "unsupported custom hero accepted");

        var factsCapture = new Capture();
        var factsMatch = StartAtHumanTurn(factsCapture, LocalDecks.Custom(
            "Facts", "HERO_08", LocalDecks.Get("mage").CardIds), "mage", 17);
        LocalRewardFacts initial = factsMatch.RewardFacts;
        initial.DistinctCardsPlayed = 999;
        Check(factsMatch.RewardFacts.DistinctCardsPlayed == 0, "reward facts expose mutable alias");

        var game = factsMatch.Game;
        game.Player1.BaseMana = 10;
        game.Player1.UsedMana = 0;
        var firstWisp = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("CS2_231"), zone: game.Player1.HandZone);
        var playable = game.Player1.Options().First(task => task.Source?.Id == firstWisp.Id);
        int beforeDistinct = factsMatch.RewardFacts.DistinctCardsPlayed;
        InvokeProcess(factsMatch, playable);
        Check(factsMatch.RewardFacts.DistinctCardsPlayed == beforeDistinct + 1,
            "accepted human play not counted");

        var secondWisp = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("CS2_231"), zone: game.Player1.HandZone);
        InvokeProcess(factsMatch, game.Player1.Options().First(task => task.Source?.Id == secondWisp.Id));
        Check(factsMatch.RewardFacts.DistinctCardsPlayed == beforeDistinct + 1,
            "duplicate card ID counted twice");

        var coin = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("GAME_005"), zone: game.Player1.HandZone);
        InvokeProcess(factsMatch, game.Player1.Options().First(task => task.Source?.Id == coin.Id));
        Check(factsMatch.RewardFacts.DistinctCardsPlayed == beforeDistinct + 1, "Coin counted as distinct card");

        var enemyWisp = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player2, Cards.FromId("CS2_231"), zone: game.Player2.HandZone);
        InvokeProcess(factsMatch, game.Player1.Options().Single(task =>
            task.PlayerTaskType == SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.END_TURN));
        game.Player2.BaseMana = 10;
        game.Player2.UsedMana = 0;
        InvokeProcess(factsMatch, game.Player2.Options().First(task => task.Source?.Id == enemyWisp.Id));
        InvokeProcess(factsMatch, game.Player2.Options().Single(task =>
            task.PlayerTaskType == SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.END_TURN));
        var archer = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("CS2_189"), zone: game.Player1.HandZone);
        InvokeProcess(factsMatch, game.Player1.Options().First(task =>
            task.Source?.Id == archer.Id && task.Target?.Id == enemyWisp.Id));
        Check(factsMatch.RewardFacts.EnemyMinionsDefeated == 1, "enemy board death not counted");

        InvokeProcess(factsMatch, game.Player1.Options().Single(task =>
            task.PlayerTaskType == SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.END_TURN));
        foreach (var card in game.Player2.HandZone.ToArray()) game.Player2.HandZone.Remove(card);
        foreach (var card in game.Player2.DeckZone.ToArray()) game.Player2.DeckZone.Remove(card);
        var arcaneIntellect = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player2, Cards.FromId("CS2_023"), zone: game.Player2.HandZone);
        for (int i = 0; i < 9; i++) SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player2, Cards.FromId("CS2_231"), zone: game.Player2.HandZone);
        var drawCards = new[] {
            SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("CS2_182"), zone: game.Player2.DeckZone),
            SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("CS2_231"), zone: game.Player2.DeckZone)
        };
        game.Player2.BaseMana = 10;
        game.Player2.UsedMana = 0;
        int beforeBurnDefeats = factsMatch.RewardFacts.EnemyMinionsDefeated;
        InvokeProcess(factsMatch, game.Player2.Options().First(task => task.Source?.Id == arcaneIntellect.Id));
        var burned = drawCards.Single(card => card.Zone?.Type == EngineZone.GRAVEYARD);
        Check(game.PowerHistory.Last.Any(entry => entry is SabberStoneCore.Kettle.PowerHistoryTagChange tag &&
                tag.EntityId == burned.Id && tag.Tag == GameTag.ZONE && tag.Value == (int)EngineZone.GRAVEYARD),
            "real full-hand draw lacks burn history");
        Check(factsMatch.RewardFacts.EnemyMinionsDefeated == beforeBurnDefeats,
            "real hand burn counted as board death");

        game.Player1.Hero.Damage = 23;
        InvokeProcess(factsMatch, game.Player2.Options().Single(task =>
            task.PlayerTaskType == SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.END_TURN));
        Check(factsMatch.RewardFacts.MinimumHealth == 7, "real action boundary health not observed");

        LocalRewardFacts beforeSearch = factsMatch.RewardFacts;
        var state = FairBotState.Create(game, game.CurrentPlayer.PlayerId, 123, DeckStyle.Tempo);
        BotSearch.Choose(state, BotBudget.ForStars(0));
        LocalRewardFacts afterSearch = factsMatch.RewardFacts;
        Check(beforeSearch.DistinctCardsPlayed == afterSearch.DistinctCardsPlayed &&
              beforeSearch.EnemyMinionsDefeated == afterSearch.EnemyMinionsDefeated &&
              beforeSearch.MinimumHealth == afterSearch.MinimumHealth,
            "BotSearch clone changed reward facts");
        Console.WriteLine("PASS custom native decks and real reward facts");
    }

    private static void InvokeProcess(NativeMatch match, object task)
    {
        var process = typeof(NativeMatch).GetMethod("Process",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        process.Invoke(match, new[] { task });
    }

    private static void DriveToTerminal(NativeMatch match, Capture capture)
    {
        int count = 0;
        while (match.Game.State != State.COMPLETE && count++ < 1500)
        {
            if (match.Game.Player1.Choice != null)
            {
                var choice = capture.Last<EntityChoices>(17);
                match.SendChoices(choice.Id, choice.Entities.Take(choice.CountMin).ToList());
            }
            else PlayBestPacketAction(match, capture.Last<AllOptions>(14));
        }
    }

    private static void BotBattlecryControlChangeIsRevealedBeforePlay()
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, "warrior-control", "warrior-control", 8427);
        match.Start();
        match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
        var game = match.Game;
        var sendHistory = typeof(NativeMatch).GetMethod("SendHistory",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        void Flush() => sendHistory.Invoke(match, new object[] { game.PowerHistory.Last });

        // Build the position, then use real card actions for buff, battlecry and deathrattle.
        game.PowerHistory.Last.Clear();
        game.Player1.BaseMana = 10;
        game.Player1.UsedMana = 0;
        var sylvanas = (SabberStoneCore.Model.Entities.Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("EX1_016"), zone: game.Player1.BoardZone);
        var taskmaster = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("EX1_603"), zone: game.Player1.HandZone);
        var hunter = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player2, Cards.FromId("EX1_005"), zone: game.Player2.HandZone);
        Flush();
        Check(!capture.Packets.Where(p => p.Id == 19).SelectMany(p => ((PowerHistory)p.Message).List)
            .Any(p => (p.HasShowEntity && p.ShowEntity.Entity == hunter.Id && p.ShowEntity.Name.Length > 0) ||
                      (p.HasFullEntity && p.FullEntity.Entity == hunter.Id && p.FullEntity.Name.Length > 0)),
            "Fixture exposed Hunter while still in bot hand");

        Check(game.Process(game.Player1.Options().First(t => t.Source?.Id == taskmaster.Id && t.Target?.Id == sylvanas.Id)),
            "Taskmaster buff rejected");
        Flush();
        Check(sylvanas.AttackDamage == 7, "Fixture Sylvanas must have seven attack");
        Check(game.Process(game.Player1.Options().Single(t =>
            t.PlayerTaskType == SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.END_TURN)), "End turn rejected");
        Flush();
        game.PowerHistory.Last.Clear();
        game.Player2.BaseMana = 10;
        game.Player2.UsedMana = 0;
        Flush();
        Check(game.Player2.BoardZone.Count == 0, "Hunter must be Sylvanas's only steal target");
        int cursor = capture.Packets.Count;
        Check(game.Process(game.Player2.Options().First(t => t.Source?.Id == hunter.Id && t.Target?.Id == sylvanas.Id)),
            "Hunter battlecry rejected");
        Check(hunter.Controller == game.Player1 && game.Player1.BoardZone.Contains(hunter),
            "Real Sylvanas deathrattle must steal the just-played Hunter");
        Flush();

        var data = capture.Packets.Skip(cursor).Where(p => p.Id == 19)
            .SelectMany(p => ((PowerHistory)p.Message).List).ToList();
        int play = data.FindIndex(p => p.HasPowerStart && p.PowerStart.Source == hunter.Id &&
            (int)p.PowerStart.Type == (int)BlockType.PLAY);
        int reveal = data.FindIndex(p => p.HasShowEntity && p.ShowEntity.Entity == hunter.Id &&
            p.ShowEntity.Name == "EX1_005");
        Check(play >= 0, "Fixture lacks Hunter PLAY block");
        Check(reveal >= 0 && reveal < play, "Bot Hunter stolen during its battlecry reached PLAY before its reveal");
        Check(TagValue(data[reveal].ShowEntity.Tags, GameTag.CONTROLLER) == 2,
            "Pre-PLAY reveal prematurely applies Hunter's final owner");
        Check(TagValue(data[reveal].ShowEntity.Tags, GameTag.ZONE) == (int)EngineZone.HAND,
            "Pre-PLAY reveal prematurely moves Hunter out of hand");
        Check(data.Skip(play + 1).Any(p => p.HasTagChange && p.TagChange.Entity == hunter.Id &&
            p.TagChange.Tag == (int)GameTag.CONTROLLER && p.TagChange.Value == 1),
            "History lost Sylvanas's subsequent control change");
        capture.SerializeAll();
        Console.WriteLine("PASS bot battlecry ownership change reveals before PLAY");
    }

    private static void PriestPowerHealsFriendlyCharacters()
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, "mage", "priest", 17);
        match.Start();
        var bot = match.Game.Player2;
        bot.Hero.Damage = 5;
        match.Game.Player1.Hero.Damage = 5;
        var healFriend = SabberStoneCore.Tasks.PlayerTasks.HeroPowerTask.Any(bot, bot.Hero);
        var healEnemy = SabberStoneCore.Tasks.PlayerTasks.HeroPowerTask.Any(bot, match.Game.Player1.Hero);
        Check(LocalBot.Score(healFriend) > LocalBot.Score(healEnemy), "Priest prefers healing enemy");
        bot.Hero.Damage = 0;
        Check(LocalBot.Score(healFriend) < -10000, "Priest wastes healing at full health");
    }

    private static void PaladinPowerAndAuxiliaryHistoriesSerialize()
    {
        var powerCapture = new Capture();
        var powerMatch = StartAtHumanTurn(powerCapture, LocalDecks.Get("paladin"), "mage", 41);
        var initial = NativeHistory(powerCapture);
        Check(Names(initial).Contains("HERO_04bp"), "Paladin hero power was not mapped to the native asset");
        Check(!Names(initial).Contains("CS2_101") && initial.All(data => !data.HasPowerStart || data.PowerStart.EffectCardId != "CS2_101"),
            "legacy Paladin hero power leaked into native transport");
        powerMatch.Game.Player1.BaseMana = 10;
        powerMatch.Game.Player1.UsedMana = 0;
        int cursor = powerCapture.Packets.Count;
        var power = powerMatch.Game.Player1.Options().Single(task =>
            task.PlayerTaskType == SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.HERO_POWER);
        InvokeProcess(powerMatch, power);
        FlushHistory(powerMatch);
        var powerData = NativeHistory(powerCapture, cursor);
        Check(Names(powerData).Contains("CS2_101t"), "Reinforce did not emit a named Recruit");
        Check(!Names(powerData).Contains("CS2_101") && powerData.All(data => !data.HasPowerStart || data.PowerStart.EffectCardId != "CS2_101"),
            "Reinforce action emitted legacy Paladin power ID");
        powerCapture.SerializeAll();

        AssertTargetedPaladinHistory("CS2_087", "CS2_087e", false);
        AssertTargetedPaladinHistory("CS2_092", "CS2_092e", false);
        AssertTargetedPaladinHistory("EX1_355", "EX1_355e", false);
        AssertTargetedPaladinHistory("EX1_360", "EX1_360e", true);
        AssertTargetedPaladinHistory("EX1_382", "EX1_382e", true);

        var equalityCapture = new Capture();
        var equalityMatch = StartAtHumanTurn(equalityCapture, LocalDecks.Get("paladin"), "mage", 47);
        var equalityGame = equalityMatch.Game;
        equalityGame.Player1.BaseMana = 10;
        equalityGame.Player1.UsedMana = 0;
        var equalityFriend = (SabberStoneCore.Model.Entities.Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            equalityGame.Player1, Cards.FromId("CS2_231"), zone: equalityGame.Player1.BoardZone);
        var equalityEnemy = (SabberStoneCore.Model.Entities.Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            equalityGame.Player2, Cards.FromId("CS2_231"), zone: equalityGame.Player2.BoardZone);
        var equality = SabberStoneCore.Model.Entities.Entity.FromCard(
            equalityGame.Player1, Cards.FromId("EX1_619"), zone: equalityGame.Player1.HandZone);
        FlushHistory(equalityMatch);
        cursor = equalityCapture.Packets.Count;
        InvokeProcess(equalityMatch, equalityGame.Player1.Options().Single(task => task.Source?.Id == equality.Id));
        FlushHistory(equalityMatch);
        var equalityData = NativeHistory(equalityCapture, cursor);
        Check(equalityFriend.Health == 1 && equalityEnemy.Health == 1, "Equality fixture did not affect both sides");
        Check(Names(equalityData).Count(name => name == "EX1_619e") >= 2,
            "Equality did not serialize named auxiliary entities for both sides");
        equalityCapture.SerializeAll();

        var swordCapture = new Capture();
        var swordMatch = StartAtHumanTurn(swordCapture, LocalDecks.Get("paladin"), "mage", 53);
        var swordGame = swordMatch.Game;
        swordGame.Player1.BaseMana = 10;
        swordGame.Player1.UsedMana = 0;
        var sword = SabberStoneCore.Model.Entities.Entity.FromCard(
            swordGame.Player1, Cards.FromId("EX1_366"), zone: swordGame.Player1.HandZone);
        FlushHistory(swordMatch);
        InvokeProcess(swordMatch, swordGame.Player1.Options().Single(task => task.Source?.Id == sword.Id));
        FlushHistory(swordMatch);
        swordGame.Player1.UsedMana = 0;
        var recruit = (SabberStoneCore.Model.Entities.Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            swordGame.Player1, Cards.FromId("CS2_231"), zone: swordGame.Player1.HandZone);
        FlushHistory(swordMatch);
        cursor = swordCapture.Packets.Count;
        InvokeProcess(swordMatch, swordGame.Player1.Options().First(task => task.Source?.Id == recruit.Id));
        FlushHistory(swordMatch);
        var swordData = NativeHistory(swordCapture, cursor);
        Check(recruit.AttackDamage == 2 && recruit.Health == 2, "Sword of Justice fixture did not buff summoned minion");
        Check(Names(swordData).Contains("EX1_366e"), "Sword trigger did not serialize named auxiliary entity");
        Check(swordData.Any(data => data.HasPowerStart && data.PowerStart.Target == recruit.Id),
            "Sword trigger history points away from summoned friendly minion");
        swordCapture.SerializeAll();
    }

    private static void AssertTargetedPaladinHistory(string cardId, string auxiliaryId, bool enemyTarget)
    {
        var capture = new Capture();
        var match = StartAtHumanTurn(capture, LocalDecks.Get("paladin"), "mage", 59);
        var game = match.Game;
        game.Player1.BaseMana = 10;
        game.Player1.UsedMana = 0;
        var target = SabberStoneCore.Model.Entities.Entity.FromCard(
            enemyTarget ? game.Player2 : game.Player1, Cards.FromId("CS2_231"),
            zone: enemyTarget ? game.Player2.BoardZone : game.Player1.BoardZone);
        var card = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId(cardId), zone: game.Player1.HandZone);
        FlushHistory(match);
        int cursor = capture.Packets.Count;
        var task = game.Player1.Options().First(option => option.Source?.Id == card.Id && option.Target?.Id == target.Id);
        InvokeProcess(match, task);
        FlushHistory(match);
        var data = NativeHistory(capture, cursor);
        Check(data.Any(entry => entry.HasPowerStart && entry.PowerStart.Source == card.Id && entry.PowerStart.Target == target.Id),
            cardId + " history target direction changed");
        Check(Names(data).Contains(auxiliaryId), cardId + " did not serialize named " + auxiliaryId);
        capture.SerializeAll();
    }

    private static void FlushHistory(NativeMatch match)
    {
        var sendHistory = typeof(NativeMatch).GetMethod("SendHistory",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        sendHistory.Invoke(match, new object[] { match.Game.PowerHistory.Last });
    }

    private static void SendHistoryEntries(NativeMatch match,
        List<SabberStoneCore.Kettle.IPowerHistoryEntry> entries)
    {
        var sendHistory = typeof(NativeMatch).GetMethod("SendHistory",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        sendHistory.Invoke(match, new object[] { entries });
    }

    private static List<PowerHistoryData> NativeHistory(Capture capture, int cursor = 0) => capture.Packets.Skip(cursor)
        .Where(packet => packet.Id == 19).SelectMany(packet => ((PowerHistory)packet.Message).List).ToList();

    private static Dictionary<int, int> ReplayNativeEntityTags(IEnumerable<PowerHistoryData> history, int entityId)
    {
        var tags = new Dictionary<int, int>();
        foreach (PowerHistoryData entry in history)
        {
            if (entry.HasHideEntity && entry.HideEntity.Entity == entityId)
                tags.Clear();
            var snapshot = entry.HasFullEntity ? entry.FullEntity
                : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
            if (snapshot != null && snapshot.Entity == entityId)
            {
                if (!entry.HasChangeEntity) tags.Clear();
                foreach (PegasusGame.Tag tag in snapshot.Tags) tags[tag.Name] = tag.Value;
            }
            if (entry.HasTagChange && entry.TagChange.Entity == entityId)
                tags[entry.TagChange.Tag] = entry.TagChange.Value;
        }
        return tags;
    }

    private static bool StrictTransportTagMatches(IReadOnlyDictionary<int, int> native,
        IReadOnlyDictionary<int, int> expected, GameTag tag)
    {
        bool nativePresent = native.TryGetValue((int)tag, out int nativeValue);
        bool expectedPresent = expected.TryGetValue((int)tag, out int expectedValue);
        return nativePresent == expectedPresent && (!expectedPresent || nativeValue == expectedValue);
    }

    private static List<string> Names(IEnumerable<PowerHistoryData> data) => data.Select(entry =>
        entry.HasFullEntity ? entry.FullEntity.Name : entry.HasShowEntity ? entry.ShowEntity.Name :
        entry.HasChangeEntity ? entry.ChangeEntity.Name : "").Where(name => name.Length > 0).ToList();

    private static void WeaponDurabilityUsesNativeHealth()
    {
        for (int seed = 0; seed < 30; seed++)
        {
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, "warrior", "mage", seed);
            match.Start();
            var weapon = match.Game.Player1.HandZone.OfType<SabberStoneCore.Model.Entities.Weapon>().FirstOrDefault();
            if (weapon == null) continue;
            var reveal = capture.Packets.Where(p => p.Id == 19).SelectMany(p => ((PowerHistory)p.Message).List)
                .Last(x => x.HasShowEntity && x.ShowEntity.Entity == weapon.Id).ShowEntity;
            Check(TagValue(reveal.Tags, GameTag.HEALTH) == weapon[GameTag.DURABILITY] && weapon[GameTag.DURABILITY] > 0,
                "weapon native HEALTH does not contain base durability");
            var convert = typeof(NativeMatch).GetMethod("ToNativeHistory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var durability = (PowerHistoryData)convert.Invoke(match, new object[] {
                SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(weapon.Id, GameTag.DURABILITY, 4) });
            Check(durability.TagChange.Tag == (int)GameTag.HEALTH && durability.TagChange.Value == 4,
                "weapon durability change not mapped");
            var damage = (PowerHistoryData)convert.Invoke(match, new object[] {
                SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(weapon.Id, GameTag.DAMAGE, 1) });
            Check(damage.TagChange.Tag == (int)GameTag.DAMAGE && damage.TagChange.Value == 1,
                "weapon damage changed or double-subtracted");
            foreach (var hidden in capture.Packets.Where(p => p.Id == 19).SelectMany(p => ((PowerHistory)p.Message).List)
                         .Where(x => x.HasFullEntity && x.FullEntity.Name == ""))
                Check(HasOnlyHiddenTags(hidden.FullEntity.Tags), "weapon conversion exposed hidden stats");
            return;
        }
        throw new Exception("Weapon fixture not found");
    }

    private static void VisibleTransportSnapshotUsesExactHistoryContract()
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, "warrior", "mage", 9);
        match.Start();
        EngineGame game = match.Game;
        IPlayable aura = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("CS2_222"), zone: game.Player1.HandZone);
        IPlayable charge = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("CS2_124"), zone: game.Player1.HandZone);
        var modified = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("CS2_120"), zone: game.Player1.BoardZone);
        IPlayable weapon = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("CS2_106"), zone: game.Player1.HandZone);
        FlushHistory(match);
        IReadOnlyDictionary<int,int> auraTags = match.SnapshotVisibleTransportTags(aura);
        Check(!auraTags.ContainsKey((int)GameTag.AURA)
            && !auraTags.ContainsKey((int)GameTag.COLLECTIBLE),
            "non-allowlisted definition AURA and COLLECTIBLE stay omitted from transport");
        Check(auraTags[(int)GameTag.CLASS] == aura.Card[GameTag.CLASS],
            "allowlisted printed CLASS reaches EntityBase.GetClasses");

        IReadOnlyDictionary<int,int> chargeTags = match.SnapshotVisibleTransportTags(charge);
        Check(chargeTags.TryGetValue((int)GameTag.CHARGE, out int chargeValue) && chargeValue == 1,
            "flattened CHARGE remains present in visible transport");

        aura.NativeTags[GameTag.AURA] = 0;
        IReadOnlyDictionary<int,int> zeroOverrideTags = match.SnapshotVisibleTransportTags(aura);
        Check(zeroOverrideTags.TryGetValue((int)GameTag.AURA, out int zeroAura) && zeroAura == 0,
            "explicit zero instance override remains present in transport");
        aura.NativeTags[GameTag.AURA] = 1;
        Check(match.SnapshotVisibleTransportTags(aura)[(int)GameTag.AURA] == 1,
            "explicit positive instance override remains present in transport");

        modified.NativeTags[GameTag.ATK] = 7;
        int printedHealth = modified[GameTag.HEALTH];
        modified.BaseHealth = 5;
        modified.Damage = 2;
        IReadOnlyDictionary<int,int> modifiedTags = match.SnapshotVisibleTransportTags(modified);
        Check(modified[GameTag.HEALTH] == 5
            && modified[GameTag.HEALTH] != printedHealth
            && modified.Damage > 0
            && modified[GameTag.HEALTH] != modified.Health,
            "damaged health-buffed character distinguishes maximum, printed, and remaining health");
        Check(modifiedTags[(int)GameTag.ATK] == modified.AttackDamage
            && modifiedTags[(int)GameTag.HEALTH] == modified[GameTag.HEALTH]
            && modifiedTags[(int)GameTag.DAMAGE] == modified.Damage
            && modifiedTags[(int)GameTag.HEALTH] - modifiedTags[(int)GameTag.DAMAGE] == modified.Health,
            "character audit snapshot keeps maximum HEALTH and separate DAMAGE");

        game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(modified.Id, GameTag.ATK, 5));
        game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(
            modified.Id, GameTag.ZONE, (int)EngineZone.GRAVEYARD));
        FlushHistory(match);
        modified.Zone = game.Player1.GraveyardZone;
        modified.NativeTags[GameTag.ATK] = 4;
        Check(match.SnapshotVisibleTransportTags(modified)[(int)GameTag.ATK] == 5,
            "graveyard transport keeps last emitted buffed ATK after engine reset");

        IReadOnlyDictionary<int,int> weaponTags = match.SnapshotVisibleTransportTags(weapon);
        Check(weaponTags[(int)GameTag.HEALTH] == weapon[GameTag.DURABILITY],
            "visible weapon maps DURABILITY to native HEALTH");

        var target = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("CS2_120"), zone: game.Player1.BoardZone);
        SabberStoneCore.Model.Card enchantmentCard = Cards.FromId("CS2_103e2");
        SabberStoneCore.Model.Entities.Enchantment enchantment =
            SabberStoneCore.Model.Entities.Enchantment.GetInstance(game.Player1, target, target, enchantmentCard);
        FlushHistory(match);
        IReadOnlyDictionary<int,int> enchantmentTags = match.SnapshotVisibleTransportTags(enchantment);
        Check(enchantmentTags.TryGetValue((int)GameTag.CARDTYPE, out int enchantmentType)
            && enchantmentType == (int)CardType.ENCHANTMENT,
            "visible enchantment CARDTYPE follows outbound history conversion");
    }

    private static void UnregisteredDefinitionPreservesHistorySnapshots()
    {
        string unregistered = PinnedClientIds.Value.Except(Cards.All.Select(card => card.Id))
            .OrderBy(id => id, StringComparer.Ordinal).First();
        Check(PinnedClientIds.Value.Contains(unregistered) && !Cards.All.Any(card => card.Id == unregistered),
            "unregistered snapshot fixture must exist in the pinned client only");
        var capture = new Capture();
        var match = StartAtHumanTurn(capture);
        foreach (int controller in new[] { 1, 2 })
        {
            var player = controller == 1 ? match.Game.Player1 : match.Game.Player2;
            var spell = SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("CS2_024"), zone: player.HandZone);
            // Pinned event spell has no engine definition.
            var snapshot = new SabberStoneCore.Kettle.PowerHistoryEntity {
                Id = spell.Id, Name = unregistered,
                Tags = new Dictionary<GameTag, int> {
                    [GameTag.CONTROLLER] = controller, [GameTag.ZONE] = (int)EngineZone.HAND,
                    [GameTag.CARDTYPE] = (int)CardType.SPELL, [GameTag.COST] = 2, [(GameTag)1635] = 6
                }
            };
            int cursor = capture.Packets.Count;
            SendHistoryEntries(match, new List<SabberStoneCore.Kettle.IPowerHistoryEntry> {
                new SabberStoneCore.Kettle.PowerHistoryFullEntity { Entity = snapshot },
                new SabberStoneCore.Kettle.PowerHistoryShowEntity { Entity = snapshot },
                new SabberStoneCore.Kettle.PowerHistoryChangeEntity { Entity = snapshot, CardId = snapshot.Name }
            });
            var history = NativeHistory(capture, cursor);
            Check(history.Count == 3 && history[0].HasFullEntity && history[1].HasShowEntity && history[2].HasChangeEntity,
                "unregistered definition lost full/show/change history");
            foreach (var entry in history)
            {
                var native = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.ChangeEntity;
                Check(native.Entity == snapshot.Id, "unregistered definition lost entity identity");
                if (controller == 1)
                    Check(native.Name == unregistered && native.Tags.Count == snapshot.Tags.Count
                        && native.Tags.All(tag => snapshot.Tags.TryGetValue((GameTag)tag.Name, out int value) && value == tag.Value),
                        "optional race projection changed unregistered definition's snapshot");
                else
                    Check(native.Name == "" && HasOnlyHiddenTags(native.Tags),
                        "unregistered definition leaked opponent hand identity or tags");
            }
        }
        capture.SerializeAll();
        Console.WriteLine("PASS unregistered definition preserves full/show/change snapshots and hidden privacy");
    }

    private static void PlayedHistoryCostPrecedesPlayStart()
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, "warrior", "mage", 11);
        match.Start();
        EngineGame game = match.Game;
        IPlayable source = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("EX1_084"), zone: game.Player1.HandZone);
        FlushHistory(match);

        int cursor = capture.Packets.Count;
        SendHistoryEntries(match, new List<SabberStoneCore.Kettle.IPowerHistoryEntry> {
            SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.PLAY, source.Id, "", 0, 0),
            new SabberStoneCore.Kettle.PowerHistoryBlockEnd()
        });
        List<PowerHistoryData> normal = NativeHistory(capture, cursor);
        int normalPlay = normal.FindIndex(entry => entry.HasPowerStart
            && entry.PowerStart.Source == source.Id && (int)entry.PowerStart.Type == (int)BlockType.PLAY);
        Check(normalPlay > 0 && normal[normalPlay - 1].HasTagChange
            && normal[normalPlay - 1].TagChange.Entity == source.Id
            && normal[normalPlay - 1].TagChange.Tag == (int)GameTag.TAG_LAST_KNOWN_COST_IN_HAND
            && normal[normalPlay - 1].TagChange.Value == 3,
            "normal visible hand cost precedes PLAY");

        cursor = capture.Packets.Count;
        SendHistoryEntries(match, new List<SabberStoneCore.Kettle.IPowerHistoryEntry> {
            SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.PLAY, source.Id, "", 0, 0),
            SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(source.Id, GameTag.TAG_LAST_KNOWN_COST_IN_HAND, 2),
            SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(game.Player1.Hero.Id, GameTag.DAMAGE, 1),
            new SabberStoneCore.Kettle.PowerHistoryBlockEnd()
        });
        List<PowerHistoryData> discounted = NativeHistory(capture, cursor);
        int discountedCost = discounted.FindIndex(entry => entry.HasTagChange
            && entry.TagChange.Entity == source.Id
            && entry.TagChange.Tag == (int)GameTag.TAG_LAST_KNOWN_COST_IN_HAND);
        int discountedPlay = discounted.FindIndex(entry => entry.HasPowerStart
            && entry.PowerStart.Source == source.Id && (int)entry.PowerStart.Type == (int)BlockType.PLAY);
        int unrelated = discounted.FindIndex(entry => entry.HasTagChange
            && entry.TagChange.Entity == game.Player1.Hero.Id && entry.TagChange.Tag == (int)GameTag.DAMAGE);
        Check(discountedCost >= 0 && discountedCost < discountedPlay
            && discounted[discountedCost].TagChange.Value == 2,
            "discounted paid cost precedes PLAY without using printed cost");
        Check(unrelated > discountedPlay, "moving paid cost reordered unrelated tag change");

        SendHistoryEntries(match, new List<SabberStoneCore.Kettle.IPowerHistoryEntry> {
            SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(source.Id, GameTag.COST, 0),
            SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(source.Id, GameTag.TAG_LAST_KNOWN_COST_IN_HAND, 3)
        });
        cursor = capture.Packets.Count;
        SendHistoryEntries(match, new List<SabberStoneCore.Kettle.IPowerHistoryEntry> {
            SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, game.Player1.Hero.Id, "", 0, 0),
            SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(game.Player1.Hero.Id, GameTag.DAMAGE, 2),
            SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.PLAY, source.Id, "", 0, 0),
            SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, source.Id, "", 0, 0),
            SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(game.Player1.Hero.Id, GameTag.DAMAGE, 3),
            new SabberStoneCore.Kettle.PowerHistoryBlockEnd(),
            new SabberStoneCore.Kettle.PowerHistoryBlockEnd(),
            new SabberStoneCore.Kettle.PowerHistoryBlockEnd()
        });
        List<PowerHistoryData> free = NativeHistory(capture, cursor);
        int freePlay = free.FindIndex(entry => entry.HasPowerStart && entry.PowerStart.Source == source.Id
            && (int)entry.PowerStart.Type == (int)BlockType.PLAY);
        int freeCost = free.FindIndex(entry => entry.HasTagChange && entry.TagChange.Entity == source.Id
            && entry.TagChange.Tag == (int)GameTag.TAG_LAST_KNOWN_COST_IN_HAND);
        Check(freeCost == freePlay - 1 && free[freeCost].TagChange.Value == 0,
            "free replay clears stale last-known cost before PLAY");
        Check(free.Take(freeCost).Count(entry => entry.HasPowerStart) == 1
            && free.Skip(freePlay + 1).First(entry => entry.HasPowerStart).PowerStart.Type
                == (HistoryBlock.Type)(int)BlockType.TRIGGER,
            "cost insertion preserves outer and nested block order");
    }

    private static void JadeGolemProjection()
    {
        for (int size = 1; size <= 30; size++)
        {
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, "mage", "warrior", 1);
            match.Start(); match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            string id = "CFM_712_t" + size.ToString("D2");
            var golem = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(match.Game.Player1, Cards.FromId(id));
            SabberStoneCore.Actions.Generic.SummonBlock(match.Game, golem, -1, null);
            FlushHistory(match);
            var row = History(capture, 0).Last(entry => entry.HasFullEntity && entry.FullEntity.Entity == golem.Id).FullEntity;
            string expected = size >= 20 ? "CFM_712_t20" : size >= 7 ? "CFM_712_t07" : size >= 4 ? "CFM_712_t04" : "CFM_712_t01";
            Check(row.Name == expected, "Jade Golem uses a pinned native art identity: " + id);
            Check(row.Tags.Any(tag => tag.Name == (int)GameTag.ATK && tag.Value == size)
                && row.Tags.Any(tag => tag.Name == (int)GameTag.HEALTH && tag.Value == size)
                && row.Tags.Any(tag => tag.Name == (int)GameTag.COST && tag.Value == Math.Min(size, 10)), "Jade Golem transport preserves exact engine stats: " + id);
            var wire = capture.HistorySnapshots().SelectMany(packet => packet.List)
                .Last(entry => entry.HasFullEntity && entry.FullEntity.Entity == golem.Id).FullEntity;
            foreach (var tag in new[] { GameTag.ATK, GameTag.HEALTH, GameTag.COST })
                Check((wire.DefTags.SingleOrDefault(value => value.Name == (int)tag)?.Value
                    ?? Cards.FromId(wire.Name)[tag]) == golem.Card[tag],
                    "Jade Golem wire definition keeps printed stats, not art-tier stats: " + id + " " + tag);
            capture.SerializeAll();
        }
        JadeCoinSequence();
        Check(NativeMatch.TransportCardId("CFM_712_t31") == "CFM_712_t31" && NativeMatch.TransportCardId(null) == "", "Jade mapping is bounded to legacy identities");
        Console.WriteLine("PASS Jade Golem projection: 30 legacy sizes, four pinned native identities, exact printed and current stats");
    }

    private static void JadeCoinSequence()
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, "mage", "warrior", 1);
        match.Start(); match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
        var game = match.Game;
        var player = game.Player1;
        game.PowerHistory.Last.Clear();
        for (int size = 1; size <= 5; size++)
        {
            player.BaseMana = 10; player.UsedMana = 0; player.TemporaryMana = 0;
            var coin = SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("JAIL_504t"), zone: player.HandZone);
            Check(game.Process(PlayCardTask.Any(player, coin)), "Jade Coin accepted");
            var golem = player.BoardZone.Last();
            string id = "CFM_712_t" + size.ToString("D2");
            Check(golem.Card.Id == id && golem.Card.ATK == size && golem.Card.Health == size
                && golem.AttackDamage == size && golem.Health == size && (golem.AppliedEnchantments?.Count ?? 0) == 0,
                "Jade Coin summons exact unbuffed engine identity: " + id);
            FlushHistory(match);
        }
        game.PowerHistory.Last.Clear();
        var second = player.BoardZone[1];
        new AddEnchantmentTask("CS2_009e", EntityType.TARGET).Process(game, player, player.Hero, second);
        var buffed = SabberStoneCore.Kettle.PowerHistoryBuilder.ShowEntity(second);
        FlushHistory(match);
        SendHistoryEntries(match, new List<SabberStoneCore.Kettle.IPowerHistoryEntry> { buffed });
        var wire = capture.HistorySnapshots().SelectMany(packet => packet.List).ToList();
        foreach (var golem in player.BoardZone)
        {
            var row = wire.First(entry => entry.HasFullEntity && entry.FullEntity.Entity == golem.Id).FullEntity;
            Check(row.Name == NativeMatch.TransportCardId(golem.Card.Id), "Jade Coin wire uses available native art");
            Check(row.DefTags.Single(tag => tag.Name == (int)GameTag.ATK).Value == golem.Card.ATK
                && row.DefTags.Single(tag => tag.Name == (int)GameTag.HEALTH).Value == golem.Card.Health,
                "Jade Coin wire retains per-size printed base");
        }
        var shown = wire.Last(entry => entry.HasShowEntity && entry.ShowEntity.Entity == second.Id).ShowEntity;
        Check(shown.DefTags.Single(tag => tag.Name == (int)GameTag.ATK).Value == 2
            && shown.DefTags.Single(tag => tag.Name == (int)GameTag.HEALTH).Value == 2
            && shown.Tags.Single(tag => tag.Name == (int)GameTag.ATK).Value == 4
            && shown.Tags.Single(tag => tag.Name == (int)GameTag.HEALTH).Value == 4,
            "real Jade buff remains above printed base (green)");
        capture.SerializeAll();
        Console.WriteLine("PASS Jade Coins 1..5: exact engine tokens; serialized native art + per-size DefTags; real buff stays green");
    }

    private static void StandardBatchLoanerTransport(bool admissionOnly = false, string admissionCard = null)
    {
        using var input = File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz");
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var export = JsonDocument.Parse(gzip);
        var definitions = export.RootElement.GetProperty("cards").EnumerateArray()
            .ToDictionary(card => card.GetProperty("cardId").GetString());
        string[] dualIds = "END_001 END_002 END_004 END_007 END_008 END_014 END_017 END_023 END_025 END_026 END_028 END_031 END_032".Split(' ');
        var nativeClassRule = typeof(DeckRule_IsClassCardOrNeutral).GetMethod("CardIsClassCardOrNeutral", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (string id in dualIds)
        {
            var definition = new EntityDef();
            foreach (var tag in definitions[id].GetProperty("tags").EnumerateObject())
                definition.SetTag(int.Parse(tag.Name), tag.Value.GetInt32());
            definition.SetTag((int)GAME_TAG.CARD_SET, definitions[id].GetProperty("cardSet").GetProperty("id").GetInt32());
            var expectedClasses = definitions[id].GetProperty("cardClasses").EnumerateArray()
                .Select(cls => Enum.Parse<TAG_CLASS>(cls.GetString())).ToHashSet();
            var nativeClasses = new List<TAG_CLASS>();
            definition.GetClasses(nativeClasses);
            Check(nativeClasses.ToHashSet().SetEquals(expectedClasses), "native dual-class tags match export: " + id);
            foreach (string hero in LocalDecks.PlayerHeroes)
            {
                var heroClass = (TAG_CLASS)(int)Cards.FromId(hero).Class;
                var classes = new List<TAG_CLASS> { heroClass };
                bool accepted = (bool)nativeClassRule.Invoke(null, new object[] { definition, classes, classes });
                Check(accepted == expectedClasses.Contains(heroClass), "native deck tray class rule: " + id + "/" + hero);
            }
        }
        Check(NativeMatch.TransportCardId("DS1h_292") == "HERO_05bp", "Hunter hero power uses the native identity");
        var standardLoaners = LocalDecks.TestLoaners.ToArray();
        Check(standardLoaners.Length == 105, "all 105 test loaners reach native transport");
        var transportIds = standardLoaners.SelectMany(deck => deck.CardIds).Distinct(StringComparer.Ordinal).ToArray();
        // Preserve the three pre-existing historical compatibility seats; new Past modules stay generated-only.
        var generatedOnly = OfflinePoolPolicy.Load().BuildManifest().Past.Select(m => m.CardId)
            .Except(new[] { "CORE_FP1_011", "YOG_514", "CORE_EDR_004" }).ToHashSet(StringComparer.Ordinal);
        var waitingWindow = new[] { "BE_036", "BE_EVENT_100", "BE_EVENT_101", "BE_EVENT_102" }
            .Where(id => !LocalCardCatalog.InActiveMeta(Cards.FromId(id))).ToHashSet(StringComparer.Ordinal);
        Check(LocalDecks.TestStandardCardIds.SequenceEqual(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds
                .Where(id => !waitingWindow.Contains(id))
                .Where(id => !generatedOnly.Contains(id)))
            && LocalDecks.TestStandardCardIds.All(transportIds.Contains),
            "every covered Standard registration has a player-hand transport fixture");
        Check(dualIds.All(id => LocalDecks.TestStandardCardIds.Contains(id)), "all thirteen dual-class cards enter native per-card fixtures");
        var fixtureIds = transportIds.Where(id => admissionCard == null || admissionCard.Split(',').Contains(id, StringComparer.Ordinal)).ToArray();
        foreach (string id in fixtureIds)
        foreach (bool bot in new[] { false, true })
        {
            var capture = new Capture();
            // Each card uses an export-legal player recipe and its stable fixed opponent.
            var loaner = standardLoaners.First(deck => deck.CardIds.Contains(id));
            var match = new NativeMatch(capture.Send, bot ? LocalDecks.OpponentFor(loaner.Id, Array.Empty<string>()).Id : loaner.Id,
                bot ? loaner.Id : LocalDecks.OpponentFor(loaner.Id, Array.Empty<string>()).Id, 1337);
            match.Start();
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            var game = match.Game;
            if (bot) { InvokeProcess(match, EndTurnTask.Any(game.Player1)); FlushFieldHistory(match); }
            var owner = game.CurrentPlayer;
            owner.BaseMana = 10; owner.UsedMana = 0; owner.Hero.Armor = 10;
            // Healthy minions and damaged legendary Beasts satisfy damage, race and rarity targets on either side.
            foreach (var player in new[] { owner, owner.Opponent })
            {
                SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("CS2_182"), zone: player.BoardZone);
                var beast = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("EX1_543"), zone: player.BoardZone);
                beast.Damage = 1;
            }
            if (loaner.Hero == "HERO_11")
                SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("HERO_11bpt"), zone: owner.BoardZone);
            var source = Cards.FromId(id).Shatter != null
                ? SabberStoneCore.Actions.ShatterRules.GiveCombined(owner, id, owner.Hero)
                : SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone);
            SeatCardContext(game, id, owner);
            if (id == "CORE_RLK_567")
            {
                FlushHistory(match); game.PowerHistory.Last.Clear();
                var coin = SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("GAME_005"), zone: owner.HandZone);
                Check(game.Process(PlayCardTask.Any(owner, coin)) && source.Card.Id == "GAME_005",
                    "Shadow of Demise becomes playable only after copying a spell");
            }
            if (dualIds.Contains(id))
            {
                Check(source.Card.Class == CardClass.INVALID && owner.HeroClass == Cards.FromId(loaner.Hero).Class,
                    "INVALID card class leaves controller hero intact: " + id + "/" + loaner.Hero);
                Check(!owner.Wild.Contains(source.Card),
                    "dual-class loaner does not alter frozen legacy generation: " + id);
                var discoverGame = game.CloneExact();
                var discoverOwner = discoverGame.CurrentPlayer;
                Check(new DiscoverTask().Process(discoverGame, discoverOwner, discoverGame.IdEntityDic[source.Id], null) == TaskState.COMPLETE
                    && discoverOwner.Choice?.Choices.Count > 0
                    && discoverOwner.Choice.Choices.All(entity => discoverOwner.Wild.Contains(discoverGame.IdEntityDic[entity].Card)),
                    "INVALID source Discover resolves controller class: " + id + "/" + loaner.Hero);
                // Quest spells are deliberately absent from random pools; admitted dual-class cards use export membership.
                bool admitted = game.OfflinePools.Standard.Any(member => member.CardId == id);
                Check(admitted == (id != "END_017"), "dual-class random-pool admission stays explicit: " + id);
                if (admitted)
                foreach (string hero in LocalDecks.PlayerHeroes)
                {
                    var heroClass = Cards.FromId(hero).Class;
                    var candidates = OfflineCardPools.Candidates(game, heroClass,
                        new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: heroClass), 0);
                    bool legalClass = definitions[id].GetProperty("cardClasses").EnumerateArray()
                        .Any(cls => cls.GetString() == heroClass.ToString());
                    Check(candidates.Contains(source.Card) == legalClass, "dual-class offline class filter: " + id + "/" + hero);
                }
            }
            // Above-ten costs need a discount in a ten-mana fixture, just as in a real game.
            if (source.Cost > owner.RemainingMana)
            {
                Check(!owner.Options().OfType<PlayCardTask>().Any(task => task.Source?.Id == source.Id),
                    "unaffordable Standard card offered before discount: " + id);
                SabberStoneCore.Actions.CostRules.AddModifier((Playable)source, SabberStoneCore.Enchants.EffectOperator.SET, owner.RemainingMana);
            }
            var payment = SabberStoneCore.Actions.CostRules.Quote(owner, (IPlayable)source);
            if (payment.Kind == SabberStoneCore.Model.Costs.CostPaymentKind.Corpses)
                SabberStoneCore.Actions.CorpseRules.Gain(owner, Math.Max(0, payment.Amount - SabberStoneCore.Actions.CorpseRules.Available(owner)));
            if (payment.Kind == SabberStoneCore.Model.Costs.CostPaymentKind.Health)
                owner.Hero.Health = Math.Max(owner.Hero.Health, payment.Amount + 1);
            if (payment.Kind == SabberStoneCore.Model.Costs.CostPaymentKind.Armor)
                owner.Hero.Armor = Math.Max(owner.Hero.Armor, payment.Amount);
            FlushHistory(match);
            var legal = owner.Options().OfType<PlayCardTask>().Where(task => task.Source?.Id == source.Id).ToArray();
            Check(legal.Length > 0, "Standard card has no legal engine task: " + id + "/" + owner.PlayerId);
            if (bot)
            {
                InvokeProcess(match, legal[0]); FlushFieldHistory(match);
                for (int decisions = 0; owner.Choice != null && decisions < 10; decisions++)
                {
                    int pick = owner.Choice.ChoiceAction == SabberStoneCore.Model.ChoiceAction.REWIND
                        ? owner.Choice.Choices.First(c => c != game.Rewind.RewindEntityId) : owner.Choice.Choices[0];
                    InvokeProcess(match, ChooseTask.Pick(owner, pick)); FlushFieldHistory(match);
                }
            }
            else
            {
                typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
                var options = capture.Last<AllOptions>(14);
                Check(legal.Length > 0, "Standard card has no legal engine task: " + id + "; mana=" + owner.BaseMana);
                var option = options.Options.FirstOrDefault(entry => entry.Type_ == NativeOption.Type.POWER && entry.MainOption?.Id == source.Id
                    && entry.MainOption.PowerKeyword == 0);
                Check(option != null, "Standard card missing native option: " + id);
                Check(option.MainOption.Targets.Select(target => target.Id).ToHashSet()
                    .SetEquals(legal.Where(task => task.ChooseOne <= 0 && task.ActionTarget != null).Select(task => task.ActionTarget.Id)), "Standard card legal native targets: " + id);
                if (source.ChooseOne && !owner.ChooseBoth)
                {
                    var branches = legal.Select(task => task.ChooseOne).Where(branch => branch > 0).Distinct().OrderBy(branch => branch).ToArray();
                    Check(option.SubOptions.Count == branches.Length, "Standard Choose One native branches: " + id);
                    for (int branch = 0; branch < option.SubOptions.Count; branch++)
                        Check(option.SubOptions[branch].Id == source.ChooseOnePlayables[branches[branch] - 1].Id
                            && option.SubOptions[branch].Targets.Select(target => target.Id).ToHashSet()
                                .SetEquals(legal.Where(task => task.ChooseOne == branches[branch] && task.ActionTarget != null).Select(task => task.ActionTarget.Id)),
                            "Standard Choose One branch targets: " + id + "/" + branch);
                }
                string expectedText = LocalNativeText.NormalizeRawText(Cards.FromId(id).Text);
                bool hasText = LocalNativeText.TryGetOverride(true, id, text => text, out string raw);
                Check(string.IsNullOrEmpty(expectedText) ? !hasText : hasText && raw == expectedText, "test loaner native text: " + id);
                int index = options.Options.IndexOf(option);
                var chosen = legal[0];
                int position = chosen is PlayCardTask play && (source is Minion || source is BoardObject) ? play.ZonePosition + 1 : 0;
                int subIndex = chosen.ChooseOne > 0 ? option.SubOptions.FindIndex(entry => entry.Id == source.ChooseOnePlayables[chosen.ChooseOne - 1].Id) : -1;
                try { match.SendOption(options.Id, index, chosen.ActionTarget?.Id ?? 0, subIndex, position); }
                catch (Exception error) { throw new InvalidOperationException("Standard native play failed: " + id, error); }
                for (int decisions = 0; owner.Choice != null && decisions < 10; decisions++)
                {
                    var choice = capture.Last<EntityChoices>(17);
                    match.SendChoices(choice.Id, choice.Entities.Take(Math.Max(1, choice.CountMin)).ToList());
                }
            }
            Check(owner.Choice == null && !game.Resolution.HasSuspendedEffect, "Standard play resolves pending choices: " + id);
            Check(!owner.HandZone.Contains(source), "Standard option accepted by engine: " + id);
            if (dualIds.Contains(id))
                Check(EmittedIds(History(capture, 0)).Contains(NativeMatch.TransportCardId(id)),
                    "dual-class play reveals pinned native identity: " + id);
            AdmitFixture("standard", id, owner.PlayerId, capture.HistorySnapshots(), game);
            capture.SerializeAll();
        }
        if (admissionOnly) { Console.WriteLine($"PASS admission Standard fixtures: {fixtureIds.Length} cards x both seats"); return; }
        OmittedChooseOneBranchMapsByEntity();
        foreach (var loaner in standardLoaners)
        for (int seed = 1; seed <= 3; seed++)
        {
            var capture = new Capture();
            var opponent = LocalDecks.OpponentFor(loaner.Id, Array.Empty<string>());
            Check(Cards.FromId(loaner.Hero)?.Type == CardType.HERO && Cards.FromId(opponent.Hero)?.Type == CardType.HERO,
                "Standard player and opponent heroes exist: " + loaner.Id);
            var match = new NativeMatch(capture.Send, loaner.Id, opponent.Id, seed);
            match.Start();
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            var emitted = EmittedIds(History(capture, 0));
            Check(match.Game.Player1.Hero.Card.Id == loaner.Hero && match.Game.Player2.Hero.Card.Id == opponent.Hero
                && emitted.Contains(opponent.Hero)
                && emitted.Contains(opponent.Hero + "bp"),
                "fixed opponent reveals native hero and hero power: " + opponent.Id);
            for (int actions = 0; match.Game.State != State.COMPLETE && actions < 1500; actions++)
            {
                if (match.Game.Player1.Choice != null)
                {
                    var choice = capture.Last<EntityChoices>(17);
                    match.SendChoices(choice.Id, choice.Entities.Take(Math.Max(1, choice.CountMin)).ToList());
                }
                else PlayBestPacketAction(match, capture.Last<AllOptions>(14));
            }
            Check(match.Game.State == State.COMPLETE, "Standard loaner match finishes: " + loaner.Id + "/" + seed);
            capture.SerializeAll();
        }
        Console.WriteLine($"PASS Standard batch native transport {transportIds.Length} cards; {standardLoaners.Length * 3} fixed-opponent games");
    }

    // An unplayable Choose One branch is omitted from native SubOptions; index 0 must then select the playable branch.
    private static void OmittedChooseOneBranchMapsByEntity()
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, "test-standard-more-priest", "test-standard-batch-2-opp-rogue", 1337);
        match.Start();
        match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
        var game = match.Game;
        game.Player1.BaseMana = 10; game.Player1.UsedMana = 0;
        Check(game.Player1.BoardZone.Count == 0 && game.Player2.BoardZone.Count == 0, "omitted-branch fixture starts with empty boards");
        SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("CS2_182"), zone: game.Player2.BoardZone);
        var source = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("EDR_463"), zone: game.Player1.HandZone);
        FlushHistory(match);
        typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
        var options = capture.Last<AllOptions>(14);
        int index = options.Options.FindIndex(entry => entry.Type_ == NativeOption.Type.POWER && entry.MainOption?.Id == source.Id
            && entry.MainOption.PowerKeyword == 0);
        Check(index >= 0 && options.Options[index].SubOptions.Count == 1
            && options.Options[index].SubOptions[0].Id == source.ChooseOnePlayables[1].Id,
            "unplayable first Choose One branch is omitted from native sub-options");
        int friendly = game.Player1.BoardZone.Count;
        match.SendOption(options.Id, index, 0, 0, 0);
        Check(!game.Player1.HandZone.Contains(source) && game.Player2.BoardZone.Count == 1 && game.Player1.BoardZone.Count == friendly + 1,
            "native sub-option 0 plays the remaining second Choose One branch");
        capture.SerializeAll();
        Console.WriteLine("PASS omitted Choose One branch maps native sub-option by entity");
    }

    // Extra context for seat-loaner cards whose every native branch needs a condition the
    // generic fixture lacks. It only adds ordinary entities; legality still comes from the engine.
    private static void SeatCardContext(SabberStoneCore.Model.Game game, string id, Controller owner = null)
    {
        owner ??= game.Player1;
        if (Cards.FromId(id).PlayRequirements.ContainsKey(PlayReq.REQ_WEAPON_EQUIPPED))
            owner.Hero.AddWeapon((Weapon)SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CS2_082")));
        switch (id)
        {
            case "TIME_EVENT_997": // Welcome Home! needs a closed friendly location.
                var location = (BoardObject)SabberStoneCore.Model.Entities.Entity.FromCard(owner,
                    Cards.FromId("CORE_REV_990"), zone: owner.BoardZone);
                location.IsExhausted = true; location[GameTag.EXHAUSTED] = 1;
                break;
            case "EDR_804": // Divination needs a friendly Wisp.
                SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CS2_231"), zone: owner.BoardZone);
                break;
            case "EDR_463": // Twilight Influence: the destroy branch needs a minion with 3 or less Attack.
                SabberStoneCore.Model.Entities.Entity.FromCard(owner.Opponent, Cards.FromId("CS2_168"), zone: owner.Opponent.BoardZone);
                break;
        }
    }

    // The test loaner exists to reach Discover, Trade and the bot's suspended Cenarius prompts live.
    private static void TestLoanerReachesDecisionPaths()
    {
        Check(NativeMatch.TransportCardId("CS2_017") == "HERO_06bp", "Druid hero power uses the native identity");
        int discovers = 0, trades = 0, cenarius = 0, games = 0;
        for (int seed = 1; seed <= 12; seed++)
        {
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, LocalDecks.TestDecisionsId, LocalDecks.TestCenariusId, seed);
            match.Start();
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            for (int count = 0; match.Game.State != State.COMPLETE && count < 1500; count++)
            {
                if (match.Game.Player1.Choice != null)
                {
                    var choice = capture.Last<EntityChoices>(17);
                    if (choice.ChoiceType == (int)ChoiceType.GENERAL) discovers++;
                    match.SendChoices(choice.Id, choice.Entities.Take(Math.Max(1, choice.CountMin)).ToList());
                    continue;
                }
                var options = capture.Last<AllOptions>(14);
                int trade = options.Options.FindIndex(o => o.MainOption?.PowerKeyword == 1720);
                if (trade >= 0 && trades < 3 * (games + 1)) { trades++; match.SendOption(options.Id, trade, 0, -1, 0); }
                else PlayBestPacketAction(match, options);
            }
            Check(match.Game.State == State.COMPLETE, "test loaner match did not finish, seed " + seed);
            Check(!match.Game.Resolution.HasSuspendedEffect, "test match ended with a suspended effect, seed " + seed);
            // A played Cenarius (not one burned from hand) offers both branches at each of its three suspended prompts.
            var history = capture.Packets.Where(p => p.Id == 19).SelectMany(p => ((PowerHistory)p.Message).List).ToList();
            var bot = match.Game.IdEntityDic.Values.Where(e => e.Card.Id == "EDR_209" && e.Controller == match.Game.Player2)
                .Select(e => e.Id).ToHashSet();
            int plays = history.Count(d => d.HasPowerStart && (int)d.PowerStart.Type == (int)BlockType.PLAY && bot.Contains(d.PowerStart.Source));
            var growthOffers = match.Game.IdEntityDic.Values.Where(e => e.Card.Id == "EDR_209a" && e.Controller == match.Game.Player2)
                .Select(e => e.Id).ToHashSet();
            var ancientOffers = match.Game.IdEntityDic.Values.Where(e => e.Card.Id == "EDR_209b" && e.Controller == match.Game.Player2)
                .Select(e => e.Id).ToHashSet();
            int growth = history.Count(d => d.HasFullEntity && growthOffers.Contains(d.FullEntity.Entity));
            int ancients = history.Count(d => d.HasFullEntity && ancientOffers.Contains(d.FullEntity.Entity));
            Check(growth == 3 * plays && ancients == 3 * plays, "each bot Cenarius play offers three suspended prompts, seed " + seed);
            Check(history.Where(d => d.HasFullEntity && (growthOffers.Contains(d.FullEntity.Entity) || ancientOffers.Contains(d.FullEntity.Entity)))
                .All(d => string.IsNullOrEmpty(d.FullEntity.Name)), "bot Cenarius choice offers remain anonymous");
            cenarius += plays;
            capture.SerializeAll();
            games++;
        }
        Console.WriteLine("test loaner: games=" + games + " discovers=" + discovers + " trades=" + trades + " cenarius=" + cenarius);
        Check(discovers > 0 && trades > 0 && cenarius > 0, "test loaner reaches Discover, Trade and bot Cenarius prompts");
    }

    private static void CatalogPairingsComplete()
    {
        Check(LocalDecks.All.Count == 8, "need eight distinct starter archetypes");
        Check(LocalDecks.Get("mage").Build().Any(c => c.Name == "Mana Wyrm"), "Mage lacks spell-tempo starter");
        string profileRoot = Path.Combine(Path.GetTempPath(), "hs-pairings-" + Guid.NewGuid());
        using var profile = new LocalProfile(Path.Combine(profileRoot, "profile.xml"));
        int expectedGold = 0, wins = 0;
        LadderPosition expectedLadder = null;
        foreach (var human in LocalDecks.All)
        foreach (var bot in LocalDecks.All)
        {
            var deck = human.Build();
            Check(deck.Count == 30 && deck.GroupBy(c => c.Id).All(g => g.Count() <= (g.First().Rarity == Rarity.LEGENDARY ? 1 : 2)), "illegal deck size or duplicates");
            var hero = Cards.FromId(human.Hero);
            Check(deck.All(c => (c.Set == CardSet.CORE || c.Set == CardSet.EXPERT1 || new[] { "EX1_284", "EX1_298", "EX1_016", "CS2_031", "EX1_295" }.Contains(c.Id)) &&
                (c.Class == CardClass.NEUTRAL || c.Class == hero.Class)), "wrong class or expansion: " + string.Join(",", deck.Where(c => (c.Set != CardSet.CORE && c.Set != CardSet.EXPERT1 && !new[] { "EX1_284", "EX1_298", "EX1_016", "CS2_031", "EX1_295" }.Contains(c.Id)) || (c.Class != CardClass.NEUTRAL && c.Class != hero.Class)).Select(c => c.Id + ":" + c.Name + ":" + c.Set + ":" + c.Class)));
            var capture = new Capture();
            var pending = profile.Begin(human.Id, bot.Id, 8427);
            var match = new NativeMatch(capture.Send, human.Id, bot.Id, 8427);
            var errors = new List<string>();
            match.BotTrace = line => { if (line.StartsWith("fallback=")) errors.Add(line); };
            match.Start();
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            int count = 0;
            while (match.Game.State != State.COMPLETE && count++ < 1500)
            {
                if (match.Game.Player1.Choice != null)
                {
                    var choice = capture.Last<EntityChoices>(17);
                    match.SendChoices(choice.Id, choice.Entities.Take(choice.CountMin).ToList());
                }
                else PlayBestPacketAction(match, capture.Last<AllOptions>(14));
            }
            Check(match.Game.State == State.COMPLETE, "pairing did not finish " + human.Id + "/" + bot.Id);
            Check(errors.Count == 0, "Search exceptions in " + human.Id + "/" + bot.Id + ": " + string.Join(";",errors));
            string outcome = match.Game.Player1.PlayState == PlayState.WON ? "Won" : match.Game.Player1.PlayState == PlayState.LOST ? "Lost" : "Tied";
            // V6: a completed loss/draw pays a rounded third of the same win roll.
            expectedGold += profile.GrindTier.Gold(LocalEconomy.RewardV6(outcome, match.Game.Turn, false,
                LocalEconomy.RewardRollSeed(pending.Id, pending.Seed)).Gold);
            if (outcome == "Won") wins++;
            var priorLadder = expectedLadder ?? profile.Ladder;
            expectedLadder = LadderRules.Apply(priorLadder, LadderRules.Outcome(outcome), priorLadder.Rating).After;
            profile.Settle(pending.Id, outcome, match.Game.Turn, false);
            profile.Settle(pending.Id, outcome, match.Game.Turn, false);
            Check(profile.Gold == expectedGold && profile.Stars == expectedLadder.StarIndex, "engine result settlement mismatch");
            Check(profile.AcknowledgeRewardPresentation(pending.Id), "engine result reward presentation acknowledgement");
            var known = new HashSet<int>();
            var owners = new Dictionary<int,int>();
            foreach (var power in capture.Packets.Where(p => p.Id == 19).SelectMany(p => ((PowerHistory)p.Message).List))
            {
                if (power.HasFullEntity && power.FullEntity.Name.Length > 0) known.Add(power.FullEntity.Entity);
                if (power.HasShowEntity && power.ShowEntity.Name.Length > 0) known.Add(power.ShowEntity.Entity);
                if (power.HasChangeEntity && power.ChangeEntity.Name.Length > 0) known.Add(power.ChangeEntity.Entity);
                if (power.HasHideEntity) known.Remove(power.HideEntity.Entity);
                var snapshot = power.HasFullEntity ? power.FullEntity : power.HasShowEntity ? power.ShowEntity : power.HasChangeEntity ? power.ChangeEntity : null;
                if (snapshot != null && snapshot.Tags.Any(t=>t.Name==(int)GameTag.CONTROLLER)) owners[snapshot.Entity]=TagValue(snapshot.Tags,GameTag.CONTROLLER);
                if (power.HasTagChange && power.TagChange.Tag==(int)GameTag.CONTROLLER) owners[power.TagChange.Entity]=power.TagChange.Value;
                if (power.HasPowerStart && (int)power.PowerStart.Type == (int)BlockType.PLAY &&
                    owners.TryGetValue(power.PowerStart.Source,out int owner) && owner==2)
                    Check(known.Contains(power.PowerStart.Source), "Bot public play precedes reveal: " + power.PowerStart.Source);
            }
            capture.SerializeAll();
            Console.WriteLine("PASS pairing " + human.Id + "/" + bot.Id + " turn=" + match.Game.Turn + " result=" + match.Game.Player1.PlayState);
        }
        Check(profile.History.Count == 64 && wins > 0, "pairings failed to cover wins and losses");
        profile.Dispose();
        Directory.Delete(profileRoot, true);
    }

    private static void ExpandedCatalogTransportSmoke()
    {
        var legacy = new HashSet<string>(LocalCardCatalog.LegacyV4.Select(card => card.Id), StringComparer.Ordinal);
        var admitted = LocalCardCatalog.Current.Where(card => !legacy.Contains(card.Id)).ToArray();
        var allowed = new HashSet<string>(LocalCardCatalog.Current.Select(card => card.Id)
            .Concat(LocalCardCatalog.SupplementalGeneratedIds).Concat(new[] {
                "CS2_182", "CS2_168", "GAME_005", "HERO_08", "HERO_08bp", "HERO_01", "HERO_01bp",
                "HERO_04", "HERO_04bp", "HERO_09", "HERO_09bp", "CS1h_001", "CS2_034", "CS2_101"
            }), StringComparer.Ordinal);
        foreach (var card in admitted)
        {
            var capture = new Capture();
            string heroId = card.Class == CardClass.WARRIOR ? "HERO_01" : card.Class == CardClass.PALADIN ? "HERO_04"
                : card.Class == CardClass.PRIEST ? "HERO_09" : "HERO_08";
            var match = new NativeMatch(capture.Send, Cards.FromId(heroId), Enumerable.Repeat(card, 30), 1337);
            match.Start();
            EngineGame game = match.Game;
            game.Player1.BaseMana = 10; game.Player1.UsedMana = 0; game.Player1.Hero.Damage = 8; game.Player2.Hero.Damage = 8;
            var friendly = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("CS2_168"), zone: game.Player1.BoardZone); friendly.IsExhausted = false;
            var hurtFriendly = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("CS2_182"), zone: game.Player1.BoardZone); hurtFriendly.Damage = 1; hurtFriendly.IsExhausted = false;
            var enemy = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("CS2_168"), zone: game.Player2.BoardZone); enemy.IsExhausted = false;
            var hurtEnemy = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("CS2_182"), zone: game.Player2.BoardZone); hurtEnemy.Damage = 1; hurtEnemy.IsExhausted = false;
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            var source = game.Player1.HandZone.First(entity => entity.Card.Id == card.Id);
            var legal = game.Player1.Options().Where(task => task.Source?.Id == source.Id).ToList();
            Check(legal.Count > 0, "native transport has no legal task: " + card.Id);
            var chosen = legal.FirstOrDefault(task => task.Target?.Controller == game.Player2 && task.Target is Minion)
                ?? legal.FirstOrDefault(task => task.Target != null) ?? legal[0];
            AllOptions options = capture.Last<AllOptions>(14);
            int optionIndex = options.Options.FindIndex(option => option.Type_ == NativeOption.Type.POWER && option.MainOption?.Id == source.Id);
            Check(optionIndex >= 0, "native option absent: " + card.Id);
            var expectedTargets = legal.Where(task => task.ChooseOne <= 0 && task.Target != null).Select(task => task.Target.Id).ToHashSet();
            var nativeTargets = options.Options[optionIndex].MainOption.Targets.Select(targetOption => targetOption.Id).ToHashSet();
            Check(nativeTargets.SetEquals(expectedTargets), "native target list mismatch: " + card.Id);
            Check(options.Options[optionIndex].SubOptions.Count == 0, "unexpected native suboption: " + card.Id);
            int cursor = capture.Packets.Count;
            int target = chosen.Target?.Id ?? 0;
            int position = chosen is PlayCardTask play && source is Minion ? play.ZonePosition + 1 : 0;
            match.SendOption(options.Id, optionIndex, target, -1, position);
            foreach (var packet in capture.Packets.Skip(cursor))
                using (var stream = new MemoryStream())
                {
                    packet.Message.Serialize(stream);
                    Check(stream.Length == packet.Message.GetSerializedSize(), "expanded transport size mismatch: " + card.Id);
                }
            var history = capture.Packets.Skip(cursor).Where(packet => packet.Id == 19)
                .SelectMany(packet => ((PowerHistory)packet.Message).List).ToArray();
            var allHistory = capture.Packets.Where(packet => packet.Id == 19)
                .SelectMany(packet => ((PowerHistory)packet.Message).List).ToArray();
            string[] emitted = history.SelectMany(item => new[] {
                    item.HasFullEntity ? item.FullEntity.Name : "", item.HasShowEntity ? item.ShowEntity.Name : "",
                    item.HasChangeEntity ? item.ChangeEntity.Name : "", item.HasPowerStart ? item.PowerStart.EffectCardId : ""
                }).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
            Check(emitted.All(allowed.Contains), "expanded transport emitted unknown ID for " + card.Id + ": "
                + string.Join(",", emitted.Where(id => !allowed.Contains(id))));
            Check(allHistory.Any(item => (item.HasFullEntity && item.FullEntity.Entity == source.Id && item.FullEntity.Name == card.Id)
                || (item.HasShowEntity && item.ShowEntity.Entity == source.Id && item.ShowEntity.Name == card.Id)
                || (item.HasChangeEntity && item.ChangeEntity.Entity == source.Id && item.ChangeEntity.Name == card.Id)),
                "expanded transport did not reveal played source: " + card.Id);
        }
        Console.WriteLine($"PASS expanded catalog native transport {admitted.Length}/{admitted.Length}");
    }

    private static void ExpandedMechanismTransport()
    {
        var controlCapture = new Capture();
        Minion controlTarget = null;
        var control = StartFixture(controlCapture, "CS1_113", game => {
            controlTarget = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("CS2_168"), zone: game.Player2.BoardZone);
        });
        int controlCursor = controlCapture.Packets.Count;
        SendFixtureCard(control, controlCapture, "CS1_113", controlTarget.Id);
        var controlHistory = History(controlCapture, controlCursor);
        Check(control.Game.Player1.BoardZone.Any(minion => minion.Id == controlTarget.Id), "native control target did not move");
        Check(controlHistory.Any(entry => entry.HasTagChange && entry.TagChange.Entity == controlTarget.Id
            && entry.TagChange.Tag == (int)GameTag.CONTROLLER && entry.TagChange.Value == 1), "native control history lacks controller transition");
        Check(controlTarget.Zone.Type == EngineZone.PLAY, "native control target left play zone");
        controlCapture.SerializeAll();

        var transformCapture = new Capture();
        Minion transformTarget = null;
        var transform = StartFixture(transformCapture, "EX1_564", game => {
            transformTarget = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("CS2_200"), zone: game.Player2.BoardZone);
        });
        var transformSource = transform.Game.Player1.HandZone.First(card => card.Card.Id == "EX1_564");
        int transformCursor = transformCapture.Packets.Count;
        SendFixtureCard(transform, transformCapture, "EX1_564", transformTarget.Id);
        Check(History(transformCapture, transformCursor).Any(entry => entry.HasChangeEntity
            && entry.ChangeEntity.Entity == transformSource.Id && entry.ChangeEntity.Name == "CS2_200"), "native Faceless CHANGE_ENTITY missing");
        transformCapture.SerializeAll();

        var chargeCapture = new Capture();
        Minion chargeTarget = null;
        var charge = StartFixture(chargeCapture, "CS2_103", game => {
            chargeTarget = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("CS2_182"), zone: game.Player1.BoardZone);
        });
        int chargeCursor = chargeCapture.Packets.Count;
        SendFixtureCard(charge, chargeCapture, "CS2_103", chargeTarget.Id);
        Check(chargeTarget[GameTag.CHARGE] == 1, "native Charge state missing");
        Check(History(chargeCapture, chargeCursor).Any(entry => entry.HasTagChange && entry.TagChange.Entity == chargeTarget.Id
            && entry.TagChange.Tag == (int)GameTag.CHARGE && entry.TagChange.Value == 1), "native Charge tag missing");

        var silenceCapture = new Capture();
        Minion silenceTarget = null;
        var silence = StartFixture(silenceCapture, "EX1_332", game => {
            silenceTarget = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("CS2_121"), zone: game.Player2.BoardZone);
        });
        int silenceCursor = silenceCapture.Packets.Count;
        SendFixtureCard(silence, silenceCapture, "EX1_332", silenceTarget.Id);
        var silenceHistory = History(silenceCapture, silenceCursor);
        Check(silenceHistory.Any(entry => entry.HasTagChange && entry.TagChange.Entity == silenceTarget.Id
            && entry.TagChange.Tag == (int)GameTag.SILENCED && entry.TagChange.Value == 1), "native silence tag missing");

        var weaponCapture = new Capture();
        var weapon = StartFixture(weaponCapture, "EX1_398", null);
        int weaponCursor = weaponCapture.Packets.Count;
        SendFixtureCard(weapon, weaponCapture, "EX1_398", 0);
        var weaponHistory = History(weaponCapture, weaponCursor);
        Check(weaponHistory.Any(entry => SnapshotName(entry) == "EX1_398t" && SnapshotTag(entry, GameTag.HEALTH) == 2),
            "generated weapon lacks native HEALTH durability");
        Check(EmittedIds(weaponHistory).Contains("EX1_398t"), "generated weapon ID missing");

        var delayedCapture = new Capture();
        var delayed = StartFixture(delayedCapture, "EX1_572", null);
        SendFixtureCard(delayed, delayedCapture, "EX1_572", 0);
        int delayedCursor = delayedCapture.Packets.Count;
        SendEndTurn(delayed, delayedCapture);
        var delayedHistory = History(delayedCapture, delayedCursor);
        string dream = EmittedIds(delayedHistory).SingleOrDefault(id => id.StartsWith("DREAM_", StringComparison.Ordinal));
        Check(dream != null && delayed.Game.Player1.HandZone.Any(card => card.Card.Id == dream), "delayed Dream native history missing");
        int dreamEntity = delayedHistory.Where(entry => SnapshotName(entry) == dream).Select(SnapshotEntity).First();
        Check(delayedHistory.Any(entry => SnapshotEntity(entry) == dreamEntity && SnapshotTag(entry, GameTag.CONTROLLER) == 1)
            || delayedHistory.Any(entry => entry.HasTagChange && entry.TagChange.Entity == dreamEntity
                && entry.TagChange.Tag == (int)GameTag.CONTROLLER && entry.TagChange.Value == 1), "delayed Dream controller tag missing");
        Check(delayedHistory.Any(entry => SnapshotEntity(entry) == dreamEntity && SnapshotTag(entry, GameTag.ZONE) == (int)EngineZone.HAND)
            || delayedHistory.Any(entry => entry.HasTagChange && entry.TagChange.Entity == dreamEntity
                && entry.TagChange.Tag == (int)GameTag.ZONE && entry.TagChange.Value == (int)EngineZone.HAND), "delayed Dream hand-zone tag missing");

        var secretCapture = new Capture();
        int secretEntity = 0;
        var secret = StartFixture(secretCapture, "CS2_029", game => {
            foreach (var card in game.Player2.HandZone.ToArray()) game.Player2.HandZone.Remove(card);
            var hidden = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("EX1_287"), zone: game.Player2.HandZone);
            secretEntity = hidden.Id;
            game.Player2.BaseMana = 10; game.Player2.UsedMana = 0;
        });
        int botCursor = secretCapture.Packets.Count;
        SendEndTurn(secret, secretCapture);
        var botHistory = History(secretCapture, botCursor);
        Check(secret.Game.Player2.SecretZone.Any(card => card.Id == secretEntity), "bot did not play Counterspell fixture");
        Check(!EmittedIds(botHistory).Contains("EX1_287"), "hidden opponent secret identity leaked before activation");
        Check(botHistory.Where(entry => SnapshotEntity(entry) == secretEntity).All(entry => string.IsNullOrEmpty(SnapshotName(entry))),
            "hidden opponent secret snapshot leaked before activation");
        int activationCursor = secretCapture.Packets.Count;
        SendFixtureCard(secret, secretCapture, "CS2_029", secret.Game.Player2.Hero.Id);
        var activationHistory = History(secretCapture, activationCursor);
        Check(secret.Game.Player2.SecretZone.All(card => card.Id != secretEntity), "Counterspell did not activate");
        Check(EmittedIds(activationHistory).Contains("EX1_287"), "activated opponent secret was not revealed");
        Check(activationHistory.Any(entry => SnapshotEntity(entry) == secretEntity && SnapshotName(entry) == "EX1_287"),
            "activated opponent secret lacks named reveal");
        secretCapture.SerializeAll();

        Console.WriteLine("PASS expanded mechanism native transport targets, tags, delayed, generated, control, secret privacy");
    }

    private static void TransformChangeClearsStaleDamage()
    {
        var capture = new Capture();
        Minion target = null;
        var match = StartFixture(capture, "CS2_022", game => {
            target = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
                game.Player1, Cards.FromId("EX1_608"), zone: game.Player1.BoardZone);
            target.Damage = 1;
            target[GameTag.NUM_ATTACKS_THIS_TURN] = 1;
            target[GameTag.STEALTH] = 1;
            target[GameTag.TAUNT] = 1;
        });
        var source = match.Game.Player1.HandZone.First(card => card.Card.Id == "CS2_022");
        var task = match.Game.Player1.Options().First(option =>
            option.Source?.Id == source.Id && option.Target?.Id == target.Id);
        target[GameTag.CANT_BE_TARGETED_BY_SPELLS] = 1;
        target[GameTag.CANT_BE_TARGETED_BY_HERO_POWERS] = 1;
        int cursor = capture.Packets.Count;
        InvokeProcess(match, task);
        FlushHistory(match);
        var change = History(capture, cursor).Single(entry => entry.HasChangeEntity
            && entry.ChangeEntity.Entity == target.Id && entry.ChangeEntity.Name == "CS2_tk1");
        Check(change.ChangeEntity.Tags.Any(tag => tag.Name == (int)GameTag.DAMAGE && tag.Value == 0),
            "ordinary transform CHANGE_ENTITY did not explicitly clear stale DAMAGE");
        foreach (GameTag tag in new[] {
            GameTag.NUM_ATTACKS_THIS_TURN, GameTag.STEALTH, GameTag.TAUNT,
            GameTag.CANT_BE_TARGETED_BY_SPELLS, GameTag.CANT_BE_TARGETED_BY_HERO_POWERS
        })
            Check(change.ChangeEntity.Tags.Any(value => value.Name == (int)tag && value.Value == 0),
                "ordinary transform CHANGE_ENTITY did not explicitly reset " + tag);
        Check(change.ChangeEntity.Tags.All(tag => tag.Name != (int)GameTag.IMMUNE || tag.Value != 0),
            "ordinary transform invented an IMMUNE reset that engine ResetAttributes does not perform");

        Dictionary<int, int> native = ReplayNativeEntityTags(NativeHistory(capture), target.Id);
        IReadOnlyDictionary<int, int> expected = match.SnapshotVisibleTransportTags(target);
        foreach (GameTag tag in new[] {
            GameTag.DAMAGE, GameTag.CANT_BE_TARGETED_BY_SPELLS,
            GameTag.CANT_BE_TARGETED_BY_HERO_POWERS, GameTag.AURA,
            GameTag.CLASS, GameTag.COLLECTIBLE
        })
            Check(StrictTransportTagMatches(native, expected, tag),
                "ordinary transform audit diverged from merged native presence: " + tag);
        var wrongReset = new Dictionary<int, int>(native) { [(int)GameTag.DAMAGE] = 1 };
        Check(!StrictTransportTagMatches(wrongReset, expected, GameTag.DAMAGE),
            "ordinary transform audit accepted wrong explicit DAMAGE reset");
        var missingReset = new Dictionary<int, int>(native);
        missingReset.Remove((int)GameTag.CANT_BE_TARGETED_BY_SPELLS);
        Check(!StrictTransportTagMatches(missingReset, expected, GameTag.CANT_BE_TARGETED_BY_SPELLS),
            "ordinary transform audit accepted missing explicit targeting reset");

        target.Damage = 1;
        IReadOnlyDictionary<int, int> changed = match.SnapshotVisibleTransportTags(target);
        Check(changed[(int)GameTag.DAMAGE] == 1 && native[(int)GameTag.DAMAGE] == 0,
            "ordinary transform audit reused cached DAMAGE after authoritative state changed");
        target.Damage = 0;
        capture.SerializeAll();
    }

    private static void TransformKeywordsReconcileFinalEngineState()
    {
        // Missing final keyword reconciliation must fail against replayed client packets.
        foreach (var fixture in new[] {
            (GameTag.DIVINE_SHIELD, "EX1_008"), (GameTag.WINDFURY, "EX1_021"),
            (GameTag.RUSH, "GIL_803"), (GameTag.CHARGE, "CS2_171"),
            (GameTag.POISONOUS, "EX1_170"), (GameTag.LIFESTEAL, "ICC_905"),
            (GameTag.STEALTH, "EX1_010")
        })
        {
            var capture = new Capture();
            var match = StartFixture(capture, "CS2_231", null);
            var game = match.Game;
            var controller = game.Player1;
            IEntity source = controller.Hero;
            new SummonTask("CS2_231").Process(game, controller, source, null);
            Minion target = controller.BoardZone.Last();
            FlushHistory(match);
            foreach (string cardId in new[] { fixture.Item2, "CS2_231" })
            {
                int want = cardId == fixture.Item2 ? 1 : 0;
                var result = new TransformTask(cardId, EntityType.TARGET, clearNativeKeywords: true)
                    .Process(game, controller, source, target);
                Check(result == TaskState.COMPLETE && target[fixture.Item1] == want,
                    "keyword transform engine fixture: " + fixture.Item1 + "/" + want);
                FlushHistory(match);
                var native = ReplayNativeEntityTags(NativeHistory(capture), target.Id);
                native.TryGetValue((int)fixture.Item1, out int actual);
                Check(actual == want, "keyword transform client diverged: " + fixture.Item1
                    + " expected=" + want + " actual=" + actual);
                Check(StrictTransportTagMatches(native, match.SnapshotVisibleTransportTags(target), fixture.Item1),
                    "keyword transform audit diverged: " + fixture.Item1);
            }
            if (fixture.Item1 != GameTag.STEALTH)
            {
                // Clearing transforms discard old instance suppression and expose the new printed keyword.
                target[fixture.Item1] = 0;
                new TransformTask(fixture.Item2, EntityType.TARGET).Process(game, controller, source, target);
                Check(target[fixture.Item1] == 1, "cleared keyword suppression fixture: " + fixture.Item1);
                FlushHistory(match);
                var native = ReplayNativeEntityTags(NativeHistory(capture), target.Id);
                native.TryGetValue((int)fixture.Item1, out int actual);
                Check(actual == 1, "transform cleared suppression client diverged: " + fixture.Item1);
            }
            capture.SerializeAll();
        }
        Console.WriteLine("PASS transformed keyword gain/loss native projection");
    }

    private static void KeywordProjectionUsesEffectiveEntityValues()
    {
        var capture = new Capture();
        var match = StartFixture(capture, "CS2_231", null);
        var player = match.Game.Player1;
        var project = typeof(NativeMatch).GetMethod("VisibleStats", BindingFlags.Static | BindingFlags.NonPublic);
        Dictionary<GameTag, int> Tags(IPlayable entity) =>
            ((IEnumerable<KeyValuePair<GameTag, int>>)project.Invoke(null, new object[] { entity }))
                .GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.Last().Value);
        var minion = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("CS2_231"));
        minion[GameTag.WINDFURY] = 2;
        minion[GameTag.MEGA_WINDFURY] = 1;
        new SabberStoneCore.Enchants.Effect(GameTag.CHARGE, SabberStoneCore.Enchants.EffectOperator.SET, 1).ApplyAuraTo(minion);
        minion.AuraEffects.Rush = true;
        minion.AuraEffects.Lifesteal = true;
        minion.IsExhausted = true;
        var tags = Tags(minion);
        Check(tags[GameTag.WINDFURY] == 2, "projection collapsed numeric Windfury or conflated Mega-Windfury");
        Check(tags[GameTag.CHARGE] == 1 && tags[GameTag.RUSH] == 1 && tags[GameTag.LIFESTEAL] == 1,
            "projection lost aura keywords or masked them with exhaustion");
        minion.IsImmune = true;
        Check(Tags(minion)[GameTag.STEALTH] == 0, "projection conflated Immune with Stealth");

        var weapon = (Weapon)SabberStoneCore.Model.Entities.Entity.FromCard(player, Cards.FromId("EX1_567"));
        player.Hero.AddWeapon(weapon);
        weapon[GameTag.WINDFURY] = 2;
        weapon[GameTag.POISONOUS] = 1;
        weapon[GameTag.LIFESTEAL] = 1;
        tags = Tags(weapon);
        Check(tags[GameTag.WINDFURY] == 2 && tags[GameTag.POISONOUS] == 1 && tags[GameTag.LIFESTEAL] == 1,
            "weapon projection lost instance keyword tags");
        Check(Tags(player.Hero)[GameTag.WINDFURY] == 1 && Tags(player.Hero)[GameTag.LIFESTEAL] == 1,
            "hero projection lost effective weapon keywords");
        weapon[GameTag.WINDFURY] = 0;
        weapon[GameTag.POISONOUS] = 0;
        weapon[GameTag.LIFESTEAL] = 0;
        tags = Tags(weapon);
        Check(tags[GameTag.WINDFURY] == 0 && tags[GameTag.POISONOUS] == 0 && tags[GameTag.LIFESTEAL] == 0,
            "weapon projection used printed or presence-only getters instead of instance keyword values");
        player.Hero.RemoveWeapon();
        player.Hero.HasStealth = true;
        tags = Tags(player.Hero);
        Check(tags[GameTag.WINDFURY] == 0 && tags[GameTag.LIFESTEAL] == 0 && tags[GameTag.STEALTH] == 1,
            "hero projection retained removed weapon keywords or lost Stealth");
        Console.WriteLine("PASS keyword projection aura, exhaustion, weapon and hero values");
    }

    private static void DefilePrivateTagsNeverReachWire()
    {
        var capture = new Capture();
        var match = StartFixture(capture, "CS2_231", game => {
            for (int health = 1; health <= 2; health++)
                SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("CS2_182"),
                    zone: game.Player2.BoardZone)[GameTag.HEALTH] = health;
        });
        var source = SabberStoneCore.Model.Entities.Entity.FromCard(match.Game.Player1, Cards.FromId("ICC_041"),
            zone: match.Game.Player1.HandZone);
        FlushFieldHistory(match);
        int snapshots = 0;
        SabberStoneCore.Model.TriggerManager.TriggerHandler duringWave = entity => {
            if (source[(GameTag)31060] == 0) return;
            var frozen = SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(source).Entity;
            Check(frozen.Tags[(GameTag)31060] > 0, "Defile snapshot must capture an active wave budget");
            // Exercise every snapshot route plus tag changes with the reserved private range.
            var privateTags = Enumerable.Range(31060, 10).Concat(new[] { 31006, 31010 }).ToArray();
            foreach (int tag in privateTags) frozen.Tags[(GameTag)tag] = 1;
            var entries = new List<SabberStoneCore.Kettle.IPowerHistoryEntry> {
                new SabberStoneCore.Kettle.PowerHistoryFullEntity { Entity = frozen },
                new SabberStoneCore.Kettle.PowerHistoryShowEntity { Entity = frozen },
                new SabberStoneCore.Kettle.PowerHistoryChangeEntity { Entity = frozen, CardId = source.Card.Id }
            };
            entries.AddRange(privateTags.Select(tag => SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(source.Id, (GameTag)tag, 1)));
            int cursor = capture.Packets.Count;
            SendHistoryEntries(match, entries);
            var wire = NativeHistory(capture, cursor);
            Check(wire.Count(e => e.HasFullEntity || e.HasShowEntity || e.HasChangeEntity) == 3,
                "Defile privacy fixture must emit all three snapshot routes");
            Check(wire.All(e => !e.HasTagChange || !privateTags.Contains(e.TagChange.Tag))
                && wire.SelectMany(e => e.HasFullEntity ? e.FullEntity.Tags : e.HasShowEntity ? e.ShowEntity.Tags
                    : e.HasChangeEntity ? e.ChangeEntity.Tags : new List<PegasusGame.Tag>())
                    .All(tag => !privateTags.Contains(tag.Name)), "Defile private wave state leaked onto the wire");
            snapshots++;
        };
        match.Game.TriggerManager.DeathTrigger += duringWave;
        try { SendCardOption(match, capture, source.Id); }
        finally { match.Game.TriggerManager.DeathTrigger -= duringWave; }
        Check(snapshots > 0 && source[(GameTag)31060] == 0 && match.Game.Player2.BoardZone.Count == 0,
            "Defile must run multiple waves and clear its private budget");
        capture.SerializeAll();
        Console.WriteLine("PASS Defile active-wave private tags excluded from snapshots and tag changes");
    }

    private static void WeaponKeywordLossClearsHeroAndWeapon()
    {
        var capture = new Capture();
        var match = StartFixture(capture, "CS2_231", null);
        var card = SabberStoneCore.Model.Entities.Entity.FromCard(match.Game.Player1, Cards.FromId("EX1_567"),
            zone: match.Game.Player1.HandZone);
        FlushFieldHistory(match);
        SendCardOption(match, capture, card.Id);
        var hero = match.Game.Player1.Hero;
        var weapon = hero.Weapon;
        var keywords = new[] { GameTag.WINDFURY, GameTag.LIFESTEAL };
        foreach (var keyword in keywords) weapon[keyword] = 1;
        FlushFieldHistory(match);
        foreach (var entity in new IPlayable[] { weapon, hero })
        foreach (var keyword in keywords)
            Check(ReplayNativeEntityTags(NativeHistory(capture), entity.Id)[(int)keyword] == 1,
                "equipped weapon keyword must reach weapon and hero: " + keyword);

        int cursor = capture.Packets.Count;
        foreach (var keyword in keywords) weapon[keyword] = 0;
        FlushFieldHistory(match);
        var wire = NativeHistory(capture, cursor);
        var missing = new List<string>();
        foreach (var entity in new IPlayable[] { weapon, hero })
        foreach (var keyword in keywords)
            if (!wire.Any(e => e.HasTagChange && e.TagChange.Entity == entity.Id
                && e.TagChange.Tag == (int)keyword && e.TagChange.Value == 0)
                || ReplayNativeEntityTags(NativeHistory(capture), entity.Id)[(int)keyword] != 0)
                missing.Add(entity.Card.Id + ":" + keyword);
        Check(missing.Count == 0, "weapon keyword loss must emit zero clears: " + string.Join(", ", missing));
        Check(!weapon.IsWindfury && !weapon.HasLifeSteal && !hero.HasWindfury && !hero.HasLifeSteal,
            "wire keyword clears must agree with engine weapon and hero getters");
        hero.HasWindfury = true;
        hero[GameTag.LIFESTEAL] = 1;
        FlushFieldHistory(match);
        foreach (var keyword in keywords)
        {
            Check(ReplayNativeEntityTags(NativeHistory(capture), hero.Id)[(int)keyword] == 1,
                "direct hero keyword grant must reach wire: " + keyword);
            Check(ReplayNativeEntityTags(NativeHistory(capture), weapon.Id)[(int)keyword] == 0,
                "direct hero keyword grant must not modify weapon: " + keyword);
        }
        Check(hero.HasWindfury && hero.HasLifeSteal && !weapon.IsWindfury && !weapon.HasLifeSteal,
            "direct hero keyword grants must agree with engine without changing weapon");
        capture.SerializeAll();
        Console.WriteLine("PASS weapon WINDFURY/LIFESTEAL 1-to-0 clears weapon and hero on wire");
    }

    private static void NonMinionChangeDoesNotGainTransformResets()
    {
        var capture = new Capture();
        var match = StartFixture(capture, "EX1_625", null);
        var entry = new SabberStoneCore.Kettle.PowerHistoryChangeEntity {
            CardId = "EX1_625t",
            Entity = SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(match.Game.Player1.Hero.HeroPower).Entity
        };
        var convert = typeof(NativeMatch).GetMethod("ToNativeHistory",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var change = (PowerHistoryData)convert.Invoke(match, new object[] { entry });
        foreach (GameTag tag in new[] {
            GameTag.DAMAGE, GameTag.NUM_ATTACKS_THIS_TURN, GameTag.STEALTH, GameTag.TAUNT,
            GameTag.CANT_BE_TARGETED_BY_SPELLS, GameTag.CANT_BE_TARGETED_BY_HERO_POWERS
        })
            Check(change.ChangeEntity.Tags.Any(value => value.Name == (int)tag)
                    == entry.Entity.Tags.ContainsKey(tag),
                "non-minion CHANGE_ENTITY changed minion reset presence: " + tag);
    }

    private static void ExactCopyTaskBridgeIsPresent()
    {
        System.Type bridge = typeof(NativeMatch).Assembly.GetType("CopyTransportBridge");
        Check(bridge != null,
            "exact copy-task transport bridge is absent");
        Check(typeof(NativeMatch).GetMethod("RecordCopyCompletion",
                BindingFlags.Instance | BindingFlags.NonPublic) != null,
            "NativeMatch immutable copy-completion recorder is absent");
        Check(bridge.GetMethod("Enter", BindingFlags.Static | BindingFlags.NonPublic) != null
                && bridge.GetMethod("Exit", BindingFlags.Static | BindingFlags.NonPublic) != null,
            "copy-task active-game scope is absent");
        var required = bridge.GetMethod("RequiredProcess", BindingFlags.Static | BindingFlags.NonPublic);
        Check(required != null, "exact copy-task Process resolver is absent");
        foreach (System.Type taskType in new[] { typeof(SummonCopyTask), typeof(TransformCopyTask) })
        {
            var process = (MethodInfo)required.Invoke(null, new object[] { taskType });
            Check(process.DeclaringType == taskType && process.ReturnType == typeof(TaskState)
                    && process.GetParameters().Length == 5
                    && process.GetParameters().All(parameter => parameter.ParameterType.IsByRef),
                "copy-task Process resolver accepted a non-pinned signature: " + taskType.Name);
        }
        foreach (string method in new[] {
            "BeforeSummonCopy", "AfterSummonCopy", "BeforeTransformCopy", "AfterTransformCopy"
        })
            Check(bridge.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic) != null,
                "copy-task Harmony seam is absent: " + method);
    }

    private static void ExactAuraRemovalBridgeIsPresent()
    {
        System.Type bridge = typeof(NativeMatch).Assembly.GetType("AuraTransportBridge");
        Check(bridge != null, "exact aura-removal transport bridge is absent");
        MethodInfo required = bridge.GetMethod("RequiredRemoveInternal",
            BindingFlags.Static | BindingFlags.NonPublic);
        Check(required != null, "exact aura RemoveInternal resolver is absent");
        foreach (System.Type auraType in new[] { typeof(Aura), typeof(SwitchingAura) })
        {
            var method = (MethodInfo)required.Invoke(null, new object[] { auraType });
            Check(method.DeclaringType == auraType && method.ReturnType == typeof(void)
                && method.GetParameters().Length == 0,
                "aura RemoveInternal resolver accepted a non-pinned signature: " + auraType.Name);
        }
    }

    private static void CopyCompletionIsImmutableAndDestinationScoped()
    {
        var capture = new Capture();
        var match = StartFixture(capture, "CS2_231", null);
        var game = match.Game;
        _ = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("CS2_222"), zone: game.Player1.BoardZone);
        var copy = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("NEW1_023"), zone: game.Player1.BoardZone);
        var batch = new List<SabberStoneCore.Kettle.IPowerHistoryEntry> {
            SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(copy)
        };
        copy[GameTag.ATK] = 5;
        copy[GameTag.HEALTH] = 7;
        copy.Damage = 3;
        copy[GameTag.DIVINE_SHIELD] = 0;
        copy[GameTag.FROZEN] = 1;
        copy[GameTag.SILENCED] = 1;
        copy[GameTag.STEALTH] = 1;
        copy[GameTag.TAUNT] = 1;
        copy[GameTag.WINDFURY] = 1;
        copy[GameTag.CHARGE] = 1;
        copy[GameTag.RUSH] = 1;
        copy[GameTag.EXHAUSTED] = 1;
        match.RecordCopyCompletion(batch, batch.Count, copy);

        copy[GameTag.ATK] = 1;
        copy[GameTag.HEALTH] = 2;
        copy.Damage = 0;
        copy[GameTag.FROZEN] = 0;
        int cursor = capture.Packets.Count;
        SendHistoryEntries(match, batch);
        var changes = History(capture, cursor).Where(entry => entry.HasTagChange
            && entry.TagChange.Entity == copy.Id).ToDictionary(entry => (GameTag)entry.TagChange.Tag,
                entry => entry.TagChange.Value);
        foreach (var expected in new[] {
            (GameTag.ATK, 6), (GameTag.HEALTH, 8), (GameTag.DAMAGE, 3),
            (GameTag.FROZEN, 1), (GameTag.SILENCED, 1), (GameTag.STEALTH, 1),
            (GameTag.TAUNT, 1), (GameTag.WINDFURY, 1), (GameTag.CHARGE, 1),
            (GameTag.RUSH, 1), (GameTag.EXHAUSTED, 1),
            (GameTag.CANT_BE_TARGETED_BY_SPELLS, 1),
            (GameTag.CANT_BE_TARGETED_BY_HERO_POWERS, 1)
        })
            Check(changes.TryGetValue(expected.Item1, out int value) && value == expected.Item2,
                "copy completion lost immutable destination " + expected.Item1 + "=" + expected.Item2
                + "; actual=" + (changes.TryGetValue(expected.Item1, out int actual) ? actual.ToString() : "missing"));
        Check(changes.TryGetValue(GameTag.DIVINE_SHIELD, out int shield) && shield == 0,
            "copy completion did not retain spent Divine Shield presence");
        capture.SerializeAll();
    }

    private static void CopyCompletionIsBatchOwnedAndOneShot()
    {
        var capture = new Capture();
        var match = StartFixture(capture, "CS2_231", null);
        var game = match.Game;
        var source = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("CS2_231"), zone: game.Player1.HandZone);
        var copy = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("CS2_120"), zone: game.Player1.BoardZone);
        FlushHistory(match);
        int cursor = capture.Packets.Count;
        var batch = new List<SabberStoneCore.Kettle.IPowerHistoryEntry> {
            SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.PLAY, source.Id, "", 0, 0),
            SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(
                source.Id, GameTag.TAG_LAST_KNOWN_COST_IN_HAND, 1),
            new SabberStoneCore.Kettle.PowerHistoryBlockEnd()
        };
        match.RecordCopyCompletion(batch, 1, copy);
        match.RecordTransportCorrection(batch, 1, copy.Id,
            new Dictionary<GameTag, int> { [GameTag.COST] = 2 }, true);

        SendHistoryEntries(match, new List<SabberStoneCore.Kettle.IPowerHistoryEntry>(batch));
        Check(History(capture, cursor).All(entry => !entry.HasTagChange
                || entry.TagChange.Entity != copy.Id || entry.TagChange.Tag != (int)GameTag.DAMAGE),
            "copy completion escaped to an equal but foreign history batch");

        cursor = capture.Packets.Count;
        SendHistoryEntries(match, batch);
        List<PowerHistoryData> completionHistory = History(capture, cursor);
        Check(completionHistory.Count(entry => entry.HasTagChange
            && entry.TagChange.Entity == copy.Id && entry.TagChange.Tag == (int)GameTag.DAMAGE) == 1,
            "copy completion at paid-cost boundary was skipped or duplicated");
        int paidCost = completionHistory.FindIndex(entry => entry.HasTagChange
            && entry.TagChange.Entity == source.Id
            && entry.TagChange.Tag == (int)GameTag.TAG_LAST_KNOWN_COST_IN_HAND);
        int play = completionHistory.FindIndex(entry => entry.HasPowerStart
            && entry.PowerStart.Source == source.Id && (int)entry.PowerStart.Type == (int)BlockType.PLAY);
        int restoredCost = completionHistory.FindIndex(entry => entry.HasTagChange
            && entry.TagChange.Entity == copy.Id && entry.TagChange.Tag == (int)GameTag.COST);
        Check(paidCost >= 0 && paidCost < play && restoredCost > play
            && completionHistory[paidCost].TagChange.Value == 1,
            "aura correction changed or reordered the just-played paid cost");
        IReadOnlyDictionary<int, int> presence = match.SnapshotVisibleTransportTags(copy);
        Check(presence.ContainsKey((int)GameTag.IMMUNE)
            && presence.ContainsKey((int)GameTag.CANT_BE_TARGETED_BY_SPELLS)
            && presence.TryGetValue((int)GameTag.COST, out int correctedCost) && correctedCost == 2,
            "COST-only aura correction erased copied explicit presence");

        cursor = capture.Packets.Count;
        SendHistoryEntries(match, batch);
        Check(History(capture, cursor).All(entry => !entry.HasTagChange
                || entry.TagChange.Entity != copy.Id || entry.TagChange.Tag != (int)GameTag.DAMAGE),
            "consumed copy completion replayed on duplicate history send");
        capture.SerializeAll();
    }

    private static void MirrorSummonCopyPreservesNoCopyOutcomes()
    {
        var missingCapture = new Capture();
        var missingMatch = StartFixture(missingCapture, "EX1_294", null);
        EngineGame missingGame = missingMatch.Game;
        var missingMirror = SabberStoneCore.Model.Entities.Entity.FromCard(
            missingGame.Player1, Cards.FromId("EX1_294"), zone: missingGame.Player1.HandZone);
        missingGame.PowerHistory.Last.Clear();
        Controller missingController = missingGame.Player1;
        IEntity missingSource = missingMirror;
        IPlayable missingTarget = null;
        SabberStoneCore.Model.TaskStack missingStack = null;
        PropertyInfo eventProperty = typeof(EngineGame).GetProperty("CurrentEventData",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        object priorEvent = eventProperty.GetValue(missingGame);
        eventProperty.SetValue(missingGame, null);
        CopyTransportBridge.Enter(missingMatch, missingGame);
        CopyTransportBridge.CopyTaskState missingState;
        TaskState missingResult;
        try
        {
            CopyTransportBridge.BeforeSummonCopy(ref missingGame, ref missingController,
                ref missingSource, ref missingTarget, out missingState);
            missingResult = new SummonCopyTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.EVENT_SOURCE)
                .Process(in missingGame, in missingController, in missingSource, in missingTarget, in missingStack);
            CopyTransportBridge.AfterSummonCopy(missingResult, missingState);
        }
        finally
        {
            CopyTransportBridge.Exit();
            eventProperty.SetValue(missingGame, priorEvent);
        }
        Check(missingResult == TaskState.STOP && PendingCopyCompletionCount(missingMatch) == 0,
            "missing Mirror EVENT_SOURCE did not preserve pinned STOP/no-copy behavior");

        var printedCapture = new Capture();
        var printedMatch = StartFixture(printedCapture, "EX1_294", null);
        EngineGame printedGame = printedMatch.Game;
        var printedMirror = SabberStoneCore.Model.Entities.Entity.FromCard(
            printedGame.Player1, Cards.FromId("EX1_294"), zone: printedGame.Player1.HandZone);
        var handSource = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            printedGame.Player2, Cards.FromId("CS2_120"), zone: printedGame.Player2.HandZone);
        handSource[GameTag.ATK] = 9;
        handSource[GameTag.HEALTH] = 9;
        handSource.Damage = 4;
        printedGame.PowerHistory.Last.Clear();
        Controller printedController = printedGame.Player1;
        IEntity printedSource = printedMirror;
        IPlayable printedTarget = handSource;
        SabberStoneCore.Model.TaskStack printedStack = null;
        priorEvent = eventProperty.GetValue(printedGame);
        object handEvent = Activator.CreateInstance(eventProperty.PropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { handSource, handSource, 0 }, null);
        eventProperty.SetValue(printedGame, handEvent);
        CopyTransportBridge.Enter(printedMatch, printedGame);
        CopyTransportBridge.CopyTaskState printedState;
        TaskState printedResult;
        try
        {
            CopyTransportBridge.BeforeSummonCopy(ref printedGame, ref printedController,
                ref printedSource, ref printedTarget, out printedState);
            printedResult = new SummonCopyTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.EVENT_SOURCE)
                .Process(in printedGame, in printedController, in printedSource, in printedTarget, in printedStack);
            CopyTransportBridge.AfterSummonCopy(printedResult, printedState);
        }
        finally
        {
            CopyTransportBridge.Exit();
            eventProperty.SetValue(printedGame, priorEvent);
        }
        Minion printedCopy = printedGame.Player1.BoardZone.Single(minion => minion.Card.Id == "CS2_120");
        Check(printedResult == TaskState.COMPLETE && PendingCopyCompletionCount(printedMatch) == 0
            && printedCopy[GameTag.ATK] == 2 && printedCopy[GameTag.HEALTH] == 3 && printedCopy.Damage == 0,
            "non-PLAY Mirror source did not preserve pinned fresh-printed COMPLETE behavior");

        var fullCapture = new Capture();
        var fullMatch = StartFixture(fullCapture, "EX1_294", null);
        EngineGame fullGame = fullMatch.Game;
        while (!fullGame.Player1.BoardZone.IsFull)
            _ = SabberStoneCore.Model.Entities.Entity.FromCard(
                fullGame.Player1, Cards.FromId("CS2_120"), zone: fullGame.Player1.BoardZone);
        var fullMirror = SabberStoneCore.Model.Entities.Entity.FromCard(
            fullGame.Player1, Cards.FromId("EX1_294"), zone: fullGame.Player1.HandZone);
        var fullTarget = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            fullGame.Player2, Cards.FromId("CS2_120"), zone: fullGame.Player2.BoardZone);
        fullGame.PowerHistory.Last.Clear();
        Controller fullController = fullGame.Player1;
        IEntity fullSource = fullMirror;
        IPlayable fullPlayable = fullTarget;
        SabberStoneCore.Model.TaskStack fullStack = null;
        priorEvent = eventProperty.GetValue(fullGame);
        object fullEvent = Activator.CreateInstance(eventProperty.PropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { fullTarget, fullTarget, 0 }, null);
        eventProperty.SetValue(fullGame, fullEvent);
        CopyTransportBridge.Enter(fullMatch, fullGame);
        CopyTransportBridge.CopyTaskState fullState;
        TaskState fullResult;
        try
        {
            CopyTransportBridge.BeforeSummonCopy(ref fullGame, ref fullController,
                ref fullSource, ref fullPlayable, out fullState);
            fullResult = new SummonCopyTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.EVENT_SOURCE)
                .Process(in fullGame, in fullController, in fullSource, in fullPlayable, in fullStack);
            CopyTransportBridge.AfterSummonCopy(fullResult, fullState);
        }
        finally
        {
            CopyTransportBridge.Exit();
            eventProperty.SetValue(fullGame, priorEvent);
        }
        Check(fullResult == TaskState.STOP && PendingCopyCompletionCount(fullMatch) == 0,
            "full-board Mirror did not preserve pinned STOP/no-copy behavior");

        EngineGame clone = fullGame.Clone(history: true);
        Controller cloneController = clone.Player1;
        IEntity cloneSource = clone.IdEntityDic[fullMirror.Id];
        IPlayable cloneTarget = clone.IdEntityDic[fullTarget.Id];
        CopyTransportBridge.Enter(fullMatch, fullGame);
        try
        {
            CopyTransportBridge.BeforeSummonCopy(ref clone, ref cloneController,
                ref cloneSource, ref cloneTarget, out CopyTransportBridge.CopyTaskState cloneState);
            Check(cloneState == null, "foreign cloned game armed Mirror copy capture");
        }
        finally
        {
            CopyTransportBridge.Exit();
        }
        SendHistoryEntries(printedMatch, printedGame.PowerHistory.Last);
        missingCapture.SerializeAll();
        printedCapture.SerializeAll();
        fullCapture.SerializeAll();
    }

    private static int PendingCopyCompletionCount(NativeMatch match) =>
        ((System.Collections.ICollection)typeof(NativeMatch).GetField("copyCompletions",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match)).Count;

    private static void ExactTransformCopyTaskSeamCapturesFinalState()
    {
        var capture = new Capture();
        var match = StartFixture(capture, "EX1_564", null);
        EngineGame game = match.Game;
        var sourceMinion = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("EX1_564"), zone: game.Player1.BoardZone);
        var targetMinion = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player2, Cards.FromId("EX1_008"), zone: game.Player2.BoardZone);
        targetMinion[GameTag.ATK] = 4;
        targetMinion[GameTag.HEALTH] = 5;
        targetMinion.Damage = 2;
        targetMinion[GameTag.DIVINE_SHIELD] = 0;
        targetMinion[GameTag.FROZEN] = 1;
        targetMinion[GameTag.SILENCED] = 1;
        targetMinion[GameTag.STEALTH] = 1;
        game.PowerHistory.Last.Clear();

        Controller controller = game.Player1;
        IEntity source = sourceMinion;
        IPlayable target = targetMinion;
        SabberStoneCore.Model.TaskStack stack = null;
        PropertyInfo eventProperty = typeof(EngineGame).GetProperty("CurrentEventData",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        object priorEvent = eventProperty.GetValue(game);
        object copyEvent = Activator.CreateInstance(eventProperty.PropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { sourceMinion, targetMinion, 0 }, null);
        eventProperty.SetValue(game, copyEvent);
        CopyTransportBridge.Enter(match, game);
        CopyTransportBridge.CopyTaskState state;
        TaskState result;
        try
        {
            CopyTransportBridge.BeforeTransformCopy(ref game, ref controller, ref source, ref target, out state);
            result = new TransformCopyTask().Process(in game, in controller, in source, in target, in stack);
            CopyTransportBridge.AfterTransformCopy(result, state);
        }
        finally
        {
            CopyTransportBridge.Exit();
            eventProperty.SetValue(game, priorEvent);
        }

        Check(result == TaskState.COMPLETE, "exact TransformCopyTask seam did not complete");
        var copy = (Minion)game.IdEntityDic[sourceMinion.Id];
        int cursor = capture.Packets.Count;
        SendHistoryEntries(match, game.PowerHistory.Last);
        var completion = History(capture, cursor).Where(entry => entry.HasTagChange
            && entry.TagChange.Entity == copy.Id).GroupBy(entry => (GameTag)entry.TagChange.Tag)
            .ToDictionary(group => group.Key, group => group.Last().TagChange.Value);
        foreach (GameTag tag in new[] {
            GameTag.ATK, GameTag.HEALTH, GameTag.DAMAGE, GameTag.DIVINE_SHIELD,
            GameTag.FROZEN, GameTag.SILENCED, GameTag.STEALTH, GameTag.TAUNT,
            GameTag.CANT_BE_TARGETED_BY_SPELLS, GameTag.CANT_BE_TARGETED_BY_HERO_POWERS,
            GameTag.WINDFURY, GameTag.CHARGE, GameTag.RUSH, GameTag.POISONOUS,
            GameTag.CANT_ATTACK, GameTag.CANNOT_ATTACK_HEROES
        })
            Check(completion.TryGetValue(tag, out int value) && value == copy[tag],
                "exact TransformCopyTask completion lost final destination " + tag);
        IReadOnlyDictionary<int, int> audit = match.SnapshotVisibleTransportTags(copy);
        foreach (GameTag tag in new[] {
            GameTag.ATK, GameTag.HEALTH, GameTag.DAMAGE, GameTag.DIVINE_SHIELD,
            GameTag.FROZEN, GameTag.SILENCED, GameTag.STEALTH, GameTag.TAUNT,
            GameTag.CANT_BE_TARGETED_BY_SPELLS, GameTag.CANT_BE_TARGETED_BY_HERO_POWERS
        })
            Check(audit.TryGetValue((int)tag, out int value) && value == copy[tag],
                "copy audit snapshot presence diverged from completion for " + tag);
        Dictionary<int, int> native = ReplayNativeEntityTags(NativeHistory(capture), copy.Id);
        foreach (GameTag tag in new[] { GameTag.AURA, GameTag.CLASS, GameTag.COLLECTIBLE })
            Check(StrictTransportTagMatches(native, audit, tag),
                "copy transform audit diverged from merged native presence: " + tag);
        int emittedDamage = native[(int)GameTag.DAMAGE];
        copy.Damage = emittedDamage == 0 ? 1 : emittedDamage - 1;
        Check(match.SnapshotVisibleTransportTags(copy)[(int)GameTag.DAMAGE] == copy.Damage
                && copy.Damage != emittedDamage,
            "copy transform audit reused cached DAMAGE after authoritative state changed");
        copy.Damage = emittedDamage;
        capture.SerializeAll();
    }

    private static void ExactSummonCopyTaskSeamUsesControllerBoardAndEventSource()
    {
        var capture = new Capture();
        var match = StartFixture(capture, "EX1_294", null);
        EngineGame game = match.Game;
        var mirror = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("EX1_294"), zone: game.Player1.HandZone);
        var targetMinion = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player2, Cards.FromId("CS2_181"), zone: game.Player2.BoardZone);
        targetMinion.Damage = 4;
        game.PowerHistory.Last.Clear();

        Controller controller = game.Player1;
        IEntity source = mirror;
        IPlayable target = targetMinion;
        SabberStoneCore.Model.TaskStack stack = null;
        PropertyInfo eventProperty = typeof(EngineGame).GetProperty("CurrentEventData",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        object priorEvent = eventProperty.GetValue(game);
        object copyEvent = Activator.CreateInstance(eventProperty.PropertyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { targetMinion, targetMinion, 0 }, null);
        eventProperty.SetValue(game, copyEvent);
        CopyTransportBridge.Enter(match, game);
        CopyTransportBridge.CopyTaskState state;
        TaskState result;
        try
        {
            CopyTransportBridge.BeforeSummonCopy(ref game, ref controller, ref source, ref target, out state);
            result = new SummonCopyTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.EVENT_SOURCE)
                .Process(in game, in controller, in source, in target, in stack);
            CopyTransportBridge.AfterSummonCopy(result, state);
        }
        finally
        {
            CopyTransportBridge.Exit();
            eventProperty.SetValue(game, priorEvent);
        }

        Check(result == TaskState.COMPLETE, "exact SummonCopyTask seam did not complete");
        Minion copy = game.Player1.BoardZone.Single(minion => minion.Card.Id == "CS2_181");
        int cursor = capture.Packets.Count;
        SendHistoryEntries(match, game.PowerHistory.Last);
        var completion = History(capture, cursor).Where(entry => entry.HasTagChange
            && entry.TagChange.Entity == copy.Id).GroupBy(entry => (GameTag)entry.TagChange.Tag)
            .ToDictionary(group => group.Key, group => group.Last().TagChange.Value);
        Check(completion[GameTag.ATK] == copy[GameTag.ATK]
            && completion[GameTag.HEALTH] == copy[GameTag.HEALTH]
            && completion[GameTag.DAMAGE] == copy.Damage && copy.Damage == 4,
            "exact SummonCopyTask completion lost EVENT_SOURCE final state");
        capture.SerializeAll();
    }

    private static void AuraOwnerRemovalRestoresSerializedHandCosts()
    {
        var capture = new Capture();
        var match = StartFixture(capture, "EX1_608", null);
        EngineGame game = match.Game;
        IPlayable sorcererHand = game.Player1.HandZone.First(card => card.Card.Id == "EX1_608");
        InvokeProcess(match, game.Player1.Options().First(task => task.Source?.Id == sorcererHand.Id));
        FlushHistory(match);
        Minion sorcerer = game.Player1.BoardZone.Single(minion => minion.Card.Id == "EX1_608");
        IPlayable polymorph = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("CS2_022"), zone: game.Player1.HandZone);
        IPlayable mirror = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("EX1_294"), zone: game.Player1.HandZone);
        game.AuraUpdate();
        FlushHistory(match);
        Check(polymorph[GameTag.COST] == 3 && mirror[GameTag.COST] == 2,
            "Sorcerer fixture did not apply hand discount");
        Aura aura = (Aura)game.Auras.Single(candidate => candidate.Owner.Id == sorcerer.Id);
        MethodInfo remove = AuraTransportBridge.RequiredRemoveInternal(typeof(Aura));
        game.PowerHistory.Last.Clear();
        int cursor = capture.Packets.Count;
        CopyTransportBridge.Enter(match, game);
        try
        {
            AuraTransportBridge.BeforeRemove(aura, out AuraTransportBridge.RemovalState state);
            remove.Invoke(aura, null);
            AuraTransportBridge.AfterRemove(state);
        }
        finally
        {
            CopyTransportBridge.Exit();
        }
        Check(polymorph[GameTag.COST] == 4 && mirror[GameTag.COST] == 3,
            "pinned Aura.RemoveInternal did not restore effective costs");
        FlushHistory(match);
        var changes = History(capture, cursor).Where(entry => entry.HasTagChange
            && entry.TagChange.Tag == (int)GameTag.COST)
            .GroupBy(entry => entry.TagChange.Entity)
            .ToDictionary(group => group.Key, group => group.Last().TagChange.Value);
        Check(changes.TryGetValue(polymorph.Id, out int polymorphCost) && polymorphCost == 4
            && changes.TryGetValue(mirror.Id, out int mirrorCost) && mirrorCost == 3,
            "owner-wide aura removal did not serialize restored hand costs");
        CopyTransportBridge.Enter(match, game);
        try
        {
            AuraTransportBridge.BeforeRemove(aura, out AuraTransportBridge.RemovalState repeated);
            Check(repeated == null, "already-removed base aura armed duplicate correction");
        }
        finally
        {
            CopyTransportBridge.Exit();
        }
        capture.SerializeAll();
    }

    private static void AuraBoardAndSwitchingRemovalSerializeActualTags()
    {
        var boardCapture = new Capture();
        var boardMatch = StartFixture(boardCapture, "CS2_222", null);
        EngineGame boardGame = boardMatch.Game;
        var beneficiary = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            boardGame.Player1, Cards.FromId("CS2_120"), zone: boardGame.Player1.BoardZone);
        FlushHistory(boardMatch);
        IPlayable championHand = boardGame.Player1.HandZone.First(card => card.Card.Id == "CS2_222");
        InvokeProcess(boardMatch, boardGame.Player1.Options().First(task => task.Source?.Id == championHand.Id));
        FlushHistory(boardMatch);
        Minion champion = boardGame.Player1.BoardZone.Single(minion => minion.Card.Id == "CS2_222");
        Check(beneficiary[GameTag.ATK] == 3 && beneficiary[GameTag.HEALTH] == 4,
            "Stormwind fixture did not apply board stats");
        int[] auraEnchantments = beneficiary.AppliedEnchantments
            .Where(enchantment => enchantment.Creator.Id == champion.Id)
            .Select(enchantment => enchantment.Id).ToArray();
        Check(auraEnchantments.Length > 0,
            "Stormwind fixture lacks enchantment-backed aura history");
        beneficiary.Damage = 1;
        FlushHistory(boardMatch);
        boardGame.PowerHistory.Last.Clear();
        int boardCursor = boardCapture.Packets.Count;
        RemoveAuraThroughSeam(boardMatch,
            (Aura)boardGame.Auras.Single(candidate => candidate.Owner.Id == champion.Id));
        Check(beneficiary[GameTag.ATK] == 2 && beneficiary[GameTag.HEALTH] == 3
            && beneficiary.Damage == 0,
            "Stormwind removal did not preserve max-health and damage semantics");
        FlushHistory(boardMatch);
        List<PowerHistoryData> boardHistory = History(boardCapture, boardCursor);
        var stats = boardHistory.Where(entry => entry.HasTagChange
            && entry.TagChange.Entity == beneficiary.Id)
            .GroupBy(entry => (GameTag)entry.TagChange.Tag)
            .ToDictionary(group => group.Key, group => group.Last().TagChange.Value);
        Check(stats.TryGetValue(GameTag.ATK, out int attack) && attack == 2
            && stats.TryGetValue(GameTag.HEALTH, out int health) && health == 3
            && stats.TryGetValue(GameTag.DAMAGE, out int damage) && damage == 0,
            "board aura removal did not serialize actual ATK/max HEALTH: "
            + string.Join(",", stats.Select(pair => pair.Key + "=" + pair.Value)) + ";history="
            + string.Join("|", boardHistory.Select(entry => entry.HasTagChange
                ? "TAG:" + entry.TagChange.Entity + ":" + (GameTag)entry.TagChange.Tag + "=" + entry.TagChange.Value
                : entry.HasHideEntity ? "HIDE:" + entry.HideEntity.Entity : "OTHER")));
        int damageIndex = boardHistory.FindLastIndex(entry => entry.HasTagChange
            && entry.TagChange.Entity == beneficiary.Id && entry.TagChange.Tag == (int)GameTag.DAMAGE);
        int healthIndex = boardHistory.FindLastIndex(entry => entry.HasTagChange
            && entry.TagChange.Entity == beneficiary.Id && entry.TagChange.Tag == (int)GameTag.HEALTH);
        Check(damageIndex >= 0 && healthIndex > damageIndex,
            "aura correction reordered genuine DAMAGE emitted during removal");
        int hideIndex = boardHistory.FindIndex(entry => entry.HasHideEntity
            && auraEnchantments.Contains(entry.HideEntity.Entity));
        int enchantmentZone = boardHistory.FindIndex(entry => entry.HasTagChange
            && auraEnchantments.Contains(entry.TagChange.Entity)
            && entry.TagChange.Tag == (int)GameTag.ZONE
            && entry.TagChange.Value == (int)EngineZone.REMOVEDFROMGAME);
        int attackIndex = boardHistory.FindLastIndex(entry => entry.HasTagChange
            && entry.TagChange.Entity == beneficiary.Id && entry.TagChange.Tag == (int)GameTag.ATK);
        Check(hideIndex >= 0 && enchantmentZone > hideIndex && attackIndex > enchantmentZone,
            "aura correction reordered genuine enchantment HIDE/zone history: hide=" + hideIndex
            + " zone=" + enchantmentZone + " attack=" + attackIndex + "; "
            + string.Join("|", boardHistory.Select(entry => entry.HasHideEntity
                ? "HIDE:" + entry.HideEntity.Entity
                : entry.HasTagChange ? "TAG:" + entry.TagChange.Entity + ":"
                    + (GameTag)entry.TagChange.Tag + "=" + entry.TagChange.Value : "OTHER")));

        var switchingCapture = new Capture();
        var switchingMatch = StartFixture(switchingCapture, "CS2_231", null);
        EngineGame switchingGame = switchingMatch.Game;
        var summoner = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            switchingGame.Player1, Cards.FromId("EX1_076"), zone: switchingGame.Player1.BoardZone);
        switchingGame.AuraUpdate();
        FlushHistory(switchingMatch);
        IPlayable discounted = SabberStoneCore.Model.Entities.Entity.FromCard(
            switchingGame.Player1, Cards.FromId("CS2_120"), zone: switchingGame.Player1.HandZone);
        Aura activeSwitchingAura = (Aura)switchingGame.Auras.Single(candidate => candidate.Owner.Id == summoner.Id);
        typeof(Aura).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(activeSwitchingAura, new object[] { discounted });
        FlushHistory(switchingMatch);
        Check(discounted[GameTag.COST] == 1, "Pint-Sized fixture did not apply hand discount");
        switchingGame.PowerHistory.Last.Clear();
        int switchingCursor = switchingCapture.Packets.Count;
        Aura switchingAura = (Aura)switchingGame.Auras.Single(candidate => candidate.Owner.Id == summoner.Id);
        Check(switchingAura is SwitchingAura, "Pint-Sized fixture did not create SwitchingAura");
        RemoveAuraThroughSeam(switchingMatch, switchingAura);
        Check(discounted[GameTag.COST] == 2, "SwitchingAura removal did not restore hand cost");
        FlushHistory(switchingMatch);
        Check(History(switchingCapture, switchingCursor).Any(entry => entry.HasTagChange
            && entry.TagChange.Entity == discounted.Id && entry.TagChange.Tag == (int)GameTag.COST
            && entry.TagChange.Value == 2),
            "SwitchingAura override did not serialize restored COST");
        boardCapture.SerializeAll();
        switchingCapture.SerializeAll();
    }

    private static void AuraStackingChangedOwnerAndOpponentHandPrivacy()
    {
        var stackedCapture = new Capture();
        var stackedMatch = StartFixture(stackedCapture, "EX1_608", null);
        EngineGame stackedGame = stackedMatch.Game;
        for (int i = 0; i < 2; i++)
        {
            IPlayable source = stackedGame.Player1.HandZone.First(card => card.Card.Id == "EX1_608");
            InvokeProcess(stackedMatch,
                stackedGame.Player1.Options().First(task => task.Source?.Id == source.Id));
            FlushHistory(stackedMatch);
        }
        IPlayable spell = SabberStoneCore.Model.Entities.Entity.FromCard(
            stackedGame.Player1, Cards.FromId("CS2_022"), zone: stackedGame.Player1.HandZone);
        stackedGame.AuraUpdate();
        FlushHistory(stackedMatch);
        Check(spell[GameTag.COST] == 2, "stacked Sorcerers did not apply both discounts");
        Minion changedOwner = stackedGame.Player1.BoardZone.First(minion => minion.Card.Id == "EX1_608");
        Aura changedAura = (Aura)stackedGame.Auras.Single(candidate => candidate.Owner.Id == changedOwner.Id);
        changedOwner.Card = Cards.FromId("CS2_tk1");
        stackedGame.PowerHistory.Last.Clear();
        int stackedCursor = stackedCapture.Packets.Count;
        RemoveAuraThroughSeam(stackedMatch, changedAura);
        Check(spell[GameTag.COST] == 3,
            "one stacked aura removal did not retain the remaining discount");
        FlushHistory(stackedMatch);
        Check(History(stackedCapture, stackedCursor).Any(entry => entry.HasTagChange
            && entry.TagChange.Entity == spell.Id && entry.TagChange.Tag == (int)GameTag.COST
            && entry.TagChange.Value == 3),
            "changed aura-owner card identity suppressed stacked correction");

        var privacyCapture = new Capture();
        var privacyMatch = StartFixture(privacyCapture, "EX1_616", null);
        EngineGame privacyGame = privacyMatch.Game;
        IPlayable wraithHand = privacyGame.Player1.HandZone.First(card => card.Card.Id == "EX1_616");
        InvokeProcess(privacyMatch,
            privacyGame.Player1.Options().First(task => task.Source?.Id == wraithHand.Id));
        FlushHistory(privacyMatch);
        Minion wraith = privacyGame.Player1.BoardZone.Single(minion => minion.Card.Id == "EX1_616");
        IPlayable human = SabberStoneCore.Model.Entities.Entity.FromCard(
            privacyGame.Player1, Cards.FromId("CS2_120"), zone: privacyGame.Player1.HandZone);
        IPlayable hidden = SabberStoneCore.Model.Entities.Entity.FromCard(
            privacyGame.Player2, Cards.FromId("CS2_120"), zone: privacyGame.Player2.HandZone);
        privacyGame.AuraUpdate();
        FlushHistory(privacyMatch);
        Check(human[GameTag.COST] == 3 && hidden[GameTag.COST] == 3,
            "Mana Wraith fixture did not affect both hands");
        privacyGame.PowerHistory.Last.Clear();
        int privacyCursor = privacyCapture.Packets.Count;
        RemoveAuraThroughSeam(privacyMatch,
            (Aura)privacyGame.Auras.Single(candidate => candidate.Owner.Id == wraith.Id));
        Check(human[GameTag.COST] == 2 && hidden[GameTag.COST] == 2,
            "Mana Wraith removal did not restore both engine costs");
        FlushHistory(privacyMatch);
        List<PowerHistoryData> privacyHistory = History(privacyCapture, privacyCursor);
        Check(privacyHistory.Any(entry => entry.HasTagChange
            && entry.TagChange.Entity == human.Id && entry.TagChange.Tag == (int)GameTag.COST
            && entry.TagChange.Value == 2),
            "Mana Wraith did not correct public human hand");
        Check(privacyHistory.All(entry => !entry.HasTagChange
            || entry.TagChange.Entity != hidden.Id || entry.TagChange.Tag != (int)GameTag.COST),
            "Mana Wraith correction leaked opponent hand identity");
        stackedCapture.SerializeAll();
        privacyCapture.SerializeAll();
    }

    private static void AuraQueuedCallerPathsRestoreCosts()
    {
        var death = StartSorcererRemovalFixture();
        death.Game.PowerHistory.Last.Clear();
        int deathCursor = death.Capture.Packets.Count;
        death.Owner.Damage = death.Owner.Health;
        death.Game.GraveYard();
        Check(death.Owner.Zone?.Type == EngineZone.GRAVEYARD
            && death.Game.Auras.Contains(death.Aura) && death.Spell[GameTag.COST] == 3,
            "real graveyard caller did not queue Sorcerer owner removal");
        CompleteQueuedAuraRemoval(death.Match, death.Aura);
        FlushHistory(death.Match);
        List<PowerHistoryData> deathHistory = History(death.Capture, deathCursor);
        int ownerZone = deathHistory.FindIndex(entry => entry.HasTagChange
            && entry.TagChange.Entity == death.Owner.Id && entry.TagChange.Tag == (int)GameTag.ZONE
            && entry.TagChange.Value == (int)EngineZone.GRAVEYARD);
        int deathCost = deathHistory.FindIndex(entry => entry.HasTagChange
            && entry.TagChange.Entity == death.Spell.Id && entry.TagChange.Tag == (int)GameTag.COST
            && entry.TagChange.Value == 4);
        Check(death.Spell[GameTag.COST] == 4 && ownerZone >= 0 && deathCost > ownerZone,
            "Sorcerer death caller did not restore COST after genuine owner zone history");

        var silence = StartSorcererRemovalFixture();
        silence.Game.PowerHistory.Last.Clear();
        Controller silenceController = silence.Game.Player1;
        IEntity silenceSource = silence.Owner;
        IPlayable silenceTarget = silence.Owner;
        SabberStoneCore.Model.TaskStack silenceStack = null;
        TaskState silenceResult = new SilenceTask(EntityType.TARGET).Process(
            in silence.Game, in silenceController, in silenceSource, in silenceTarget, in silenceStack);
        Check(silenceResult == TaskState.COMPLETE && silence.Owner.IsSilenced
            && silence.Game.Auras.Contains(silence.Aura) && silence.Spell[GameTag.COST] == 3,
            "pinned SilenceTask did not queue Sorcerer removal before aura update");
        CompleteQueuedAuraRemoval(silence.Match, silence.Aura);
        int silenceCursor = silence.Capture.Packets.Count;
        FlushHistory(silence.Match);
        Check(silence.Spell[GameTag.COST] == 4 && History(silence.Capture, silenceCursor).Any(entry =>
                entry.HasTagChange && entry.TagChange.Entity == silence.Spell.Id
                && entry.TagChange.Tag == (int)GameTag.COST && entry.TagChange.Value == 4),
            "silenced Sorcerer removal did not serialize restored COST");

        var transform = StartSorcererRemovalFixture();
        transform.Game.PowerHistory.Last.Clear();
        Controller transformController = transform.Game.Player1;
        IEntity transformSource = transform.Owner;
        IPlayable transformTarget = transform.Owner;
        SabberStoneCore.Model.TaskStack transformStack = null;
        TaskState transformResult = new TransformTask("CS2_tk1", EntityType.TARGET).Process(
            in transform.Game, in transformController, in transformSource, in transformTarget, in transformStack);
        Check(transformResult == TaskState.COMPLETE && transform.Owner.Card.Id == "CS2_tk1"
            && transform.Game.Auras.Contains(transform.Aura) && transform.Spell[GameTag.COST] == 3,
            "pinned TransformTask did not queue changed-owner Sorcerer removal");
        CompleteQueuedAuraRemoval(transform.Match, transform.Aura);
        int transformCursor = transform.Capture.Packets.Count;
        FlushHistory(transform.Match);
        Check(transform.Spell[GameTag.COST] == 4 && History(transform.Capture, transformCursor).Any(entry =>
                entry.HasTagChange && entry.TagChange.Entity == transform.Spell.Id
                && entry.TagChange.Tag == (int)GameTag.COST && entry.TagChange.Value == 4),
            "transformed Sorcerer removal depended on owner card identity");

        var ventureCapture = new Capture();
        var ventureMatch = StartFixture(ventureCapture, "CS2_227", null);
        EngineGame ventureGame = ventureMatch.Game;
        IPlayable ventureHand = ventureGame.Player1.HandZone.First(card => card.Card.Id == "CS2_227");
        InvokeProcess(ventureMatch, ventureGame.Player1.Options().First(task => task.Source?.Id == ventureHand.Id));
        FlushHistory(ventureMatch);
        Minion venture = ventureGame.Player1.BoardZone.Single(minion => minion.Card.Id == "CS2_227");
        IPlayable ventureTarget = SabberStoneCore.Model.Entities.Entity.FromCard(
            ventureGame.Player1, Cards.FromId("CS2_120"), zone: ventureGame.Player1.HandZone);
        ventureGame.AuraUpdate();
        FlushHistory(ventureMatch);
        Check(ventureTarget[GameTag.COST] == 5, "Venture Co. fixture did not add three COST");
        ventureGame.PowerHistory.Last.Clear();
        int ventureCursor = ventureCapture.Packets.Count;
        RemoveAuraThroughCaller(ventureMatch,
            (Aura)ventureGame.Auras.Single(candidate => candidate.Owner.Id == venture.Id));
        FlushHistory(ventureMatch);
        Check(ventureTarget[GameTag.COST] == 2 && History(ventureCapture, ventureCursor).Any(entry =>
                entry.HasTagChange && entry.TagChange.Entity == ventureTarget.Id
                && entry.TagChange.Tag == (int)GameTag.COST && entry.TagChange.Value == 2),
            "Venture Co. caller removal did not serialize restored minion COST");

        death.Capture.SerializeAll();
        silence.Capture.SerializeAll();
        transform.Capture.SerializeAll();
        ventureCapture.SerializeAll();
        Console.WriteLine("PASS queued aura callers: death, silence, transform, Venture Co.");
    }

    private static void AuraCorrectionOrderAndLifetime()
    {
        var stackedCapture = new Capture();
        var stackedMatch = StartFixture(stackedCapture, "EX1_608", null);
        EngineGame stackedGame = stackedMatch.Game;
        for (int i = 0; i < 2; i++)
        {
            IPlayable source = stackedGame.Player1.HandZone.First(card => card.Card.Id == "EX1_608");
            InvokeProcess(stackedMatch, stackedGame.Player1.Options().First(task => task.Source?.Id == source.Id));
            FlushHistory(stackedMatch);
        }
        IPlayable spell = SabberStoneCore.Model.Entities.Entity.FromCard(
            stackedGame.Player1, Cards.FromId("CS2_022"), zone: stackedGame.Player1.HandZone);
        stackedGame.AuraUpdate();
        FlushHistory(stackedMatch);
        Aura[] stackedAuras = stackedGame.Auras.OfType<Aura>()
            .Where(aura => aura.Owner.Card.Id == "EX1_608").ToArray();
        Check(stackedAuras.Length == 2 && spell[GameTag.COST] == 2,
            "same-index aura fixture did not apply two Sorcerer discounts");
        stackedGame.PowerHistory.Last.Clear();
        int stackedCursor = stackedCapture.Packets.Count;
        foreach (Aura aura in stackedAuras) aura.Remove();
        foreach (Aura aura in stackedAuras) CompleteQueuedAuraRemoval(stackedMatch, aura);
        Check(stackedGame.PowerHistory.Last.Count == 0 && spell[GameTag.COST] == 4,
            "two queued aura removals emitted unexpected engine history or wrong final COST");
        FlushHistory(stackedMatch);
        int[] stackedCosts = History(stackedCapture, stackedCursor).Where(entry => entry.HasTagChange
            && entry.TagChange.Entity == spell.Id && entry.TagChange.Tag == (int)GameTag.COST)
            .Select(entry => entry.TagChange.Value).ToArray();
        Check(stackedCosts.SequenceEqual(new[] { 3, 4 }),
            "two actual aura corrections at one index lost causal order");

        var nestedCapture = new Capture();
        var nestedMatch = StartFixture(nestedCapture, "CS2_222", null);
        EngineGame nestedGame = nestedMatch.Game;
        var nestedTarget = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            nestedGame.Player1, Cards.FromId("CS2_120"), zone: nestedGame.Player1.BoardZone);
        FlushHistory(nestedMatch);
        IPlayable nestedChampionHand = nestedGame.Player1.HandZone.First(card => card.Card.Id == "CS2_222");
        InvokeProcess(nestedMatch, nestedGame.Player1.Options().First(task => task.Source?.Id == nestedChampionHand.Id));
        FlushHistory(nestedMatch);
        Minion nestedChampion = nestedGame.Player1.BoardZone.Single(minion => minion.Card.Id == "CS2_222");
        Aura nestedAura = (Aura)nestedGame.Auras.Single(candidate => candidate.Owner.Id == nestedChampion.Id);
        nestedGame.PowerHistory.Last.Clear();
        nestedMatch.RecordCopyCompletion(nestedGame.PowerHistory.Last, 0, nestedTarget);
        RemoveAuraThroughCaller(nestedMatch, nestedAura);
        int nestedCursor = nestedCapture.Packets.Count;
        FlushHistory(nestedMatch);
        int[] nestedAttack = History(nestedCapture, nestedCursor).Where(entry => entry.HasTagChange
            && entry.TagChange.Entity == nestedTarget.Id && entry.TagChange.Tag == (int)GameTag.ATK)
            .Select(entry => entry.TagChange.Value).ToArray();
        Check(nestedAttack.SequenceEqual(new[] { 3, 2 }),
            "nested copy then aura records at one index did not leave removal authoritative");
        IReadOnlyDictionary<int, int> nestedPresence = nestedMatch.SnapshotVisibleTransportTags(nestedTarget);
        Check(nestedPresence.ContainsKey((int)GameTag.IMMUNE)
            && nestedPresence.TryGetValue((int)GameTag.ATK, out int nestedFinalAttack) && nestedFinalAttack == 2,
            "nested copy/aura presence union lost copy tags or final aura value");

        var switchingCapture = new Capture();
        var switchingMatch = StartFixture(switchingCapture, "CS2_231", null);
        EngineGame switchingGame = switchingMatch.Game;
        IPlayable discounted = SabberStoneCore.Model.Entities.Entity.FromCard(
            switchingGame.Player1, Cards.FromId("CS2_120"), zone: switchingGame.Player1.HandZone);
        var summoner = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            switchingGame.Player1, Cards.FromId("EX1_076"), zone: switchingGame.Player1.BoardZone);
        switchingGame.AuraUpdate();
        FlushHistory(switchingMatch);
        Aura switchingAura = (Aura)switchingGame.Auras.Single(candidate => candidate.Owner.Id == summoner.Id);
        Check(switchingAura is SwitchingAura && discounted[GameTag.COST] == 1,
            "Pint-Sized fixture did not apply normal first-minion discount");
        var played = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            switchingGame.Player1, Cards.FromId("CS2_231"), zone: switchingGame.Player1.BoardZone);
        MethodInfo playMinionTrigger = typeof(TriggerManager).GetMethod("OnPlayMinionTrigger",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo turnStartTrigger = typeof(TriggerManager).GetMethod("OnTurnStartTrigger",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Check(playMinionTrigger != null && turnStartTrigger != null, "pinned SwitchingAura trigger callers changed");
        switchingGame.PowerHistory.Last.Clear();
        playMinionTrigger.Invoke(switchingGame.TriggerManager, new object[] { played });
        CompleteQueuedAuraRemoval(switchingMatch, switchingAura);
        Check(discounted[GameTag.COST] == 2 && switchingGame.Auras.Contains(switchingAura),
            "normal first-minion turn-off removed Pint-Sized aura or left stale COST");
        turnStartTrigger.Invoke(switchingGame.TriggerManager, new object[] { switchingGame.Player1 });
        switchingAura.Update();
        Check(discounted[GameTag.COST] == 1,
            "normal turn-start caller did not reapply Pint-Sized discount");
        int switchingCursor = switchingCapture.Packets.Count;
        FlushHistory(switchingMatch);
        int[] switchingCosts = History(switchingCapture, switchingCursor).Where(entry => entry.HasTagChange
            && entry.TagChange.Entity == discounted.Id && entry.TagChange.Tag == (int)GameTag.COST)
            .Select(entry => entry.TagChange.Value).ToArray();
        Check(switchingCosts.SequenceEqual(new[] { 2, 1 }),
            "later genuine Pint-Sized reapply did not remain authoritative after correction");

        switchingGame.PowerHistory.Last.Clear();
        int ownerRemovalCursor = switchingCapture.Packets.Count;
        RemoveAuraThroughCaller(switchingMatch, switchingAura);
        FlushHistory(switchingMatch);
        Check(discounted[GameTag.COST] == 2 && !switchingGame.Auras.Contains(switchingAura)
            && History(switchingCapture, ownerRemovalCursor).Any(entry => entry.HasTagChange
                && entry.TagChange.Entity == discounted.Id && entry.TagChange.Tag == (int)GameTag.COST
                && entry.TagChange.Value == 2),
            "Pint-Sized owner-removal caller did not run SwitchingAura override correction");

        var emptyCapture = new Capture();
        var emptyMatch = StartFixture(emptyCapture, "EX1_608", null);
        EngineGame emptyGame = emptyMatch.Game;
        IPlayable emptyOwnerHand = emptyGame.Player1.HandZone.First(card => card.Card.Id == "EX1_608");
        InvokeProcess(emptyMatch, emptyGame.Player1.Options().First(task => task.Source?.Id == emptyOwnerHand.Id));
        FlushHistory(emptyMatch);
        Aura emptyAura = (Aura)emptyGame.Auras.Single(candidate => candidate.Owner.Id == emptyOwnerHand.Id);
        Check(emptyAura.AppliedEntities.Count == 0, "empty aura fixture unexpectedly has affected entities");
        emptyGame.PowerHistory.Last.Clear();
        int emptyCursor = emptyCapture.Packets.Count;
        RemoveAuraThroughCaller(emptyMatch, emptyAura);
        FlushHistory(emptyMatch);
        Check(emptyCapture.Packets.Count == emptyCursor,
            "empty applied set created or retained a transport correction");

        stackedCapture.SerializeAll();
        nestedCapture.SerializeAll();
        switchingCapture.SerializeAll();
        emptyCapture.SerializeAll();
        Console.WriteLine("PASS aura correction order, reapply, nested copy, and lifetime");
    }

    private static (Capture Capture, NativeMatch Match, EngineGame Game, Minion Owner, Aura Aura, IPlayable Spell)
        StartSorcererRemovalFixture()
    {
        var capture = new Capture();
        var match = StartFixture(capture, "EX1_608", null);
        EngineGame game = match.Game;
        IPlayable ownerHand = game.Player1.HandZone.First(card => card.Card.Id == "EX1_608");
        InvokeProcess(match, game.Player1.Options().First(task => task.Source?.Id == ownerHand.Id));
        FlushHistory(match);
        Minion owner = game.Player1.BoardZone.Single(minion => minion.Id == ownerHand.Id);
        IPlayable spell = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("CS2_022"), zone: game.Player1.HandZone);
        game.AuraUpdate();
        FlushHistory(match);
        Aura aura = (Aura)game.Auras.Single(candidate => candidate.Owner.Id == owner.Id);
        Check(spell[GameTag.COST] == 3 && aura.AppliedEntities.Any(entity => entity.Id == spell.Id),
            "Sorcerer caller fixture did not apply exact hand discount");
        return (capture, match, game, owner, aura, spell);
    }

    private static void AuraCorrectionsRespectReplayVisibility()
    {
        var capture = new Capture();
        var match = StartFixture(capture, "CS2_231", null);
        EngineGame game = match.Game;
        IPlayable human = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("CS2_120"), zone: game.Player1.HandZone);
        var humanBatch = new List<SabberStoneCore.Kettle.IPowerHistoryEntry> {
            SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(human)
        };
        match.RecordTransportCorrection(humanBatch, 1, human.Id,
            new Dictionary<GameTag, int> { [GameTag.COST] = 7 }, true);
        int cursor = capture.Packets.Count;
        SendHistoryEntries(match, humanBatch);
        Check(History(capture, cursor).Any(entry => entry.HasTagChange
            && entry.TagChange.Entity == human.Id && entry.TagChange.Tag == (int)GameTag.COST
            && entry.TagChange.Value == 7),
            "same-batch human hand reveal did not admit aura correction");

        IPlayable hidden = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player2, Cards.FromId("CS2_120"), zone: game.Player2.HandZone);
        var hiddenBatch = new List<SabberStoneCore.Kettle.IPowerHistoryEntry> {
            SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(hidden)
        };
        match.RecordTransportCorrection(hiddenBatch, 1, hidden.Id,
            new Dictionary<GameTag, int> { [GameTag.COST] = 9 }, true);
        cursor = capture.Packets.Count;
        SendHistoryEntries(match, hiddenBatch);
        Check(History(capture, cursor).All(entry => !entry.HasTagChange
            || entry.TagChange.Entity != hidden.Id || entry.TagChange.Tag != (int)GameTag.COST),
            "opponent hand aura correction leaked hidden identity");
        bool hiddenRejected = false;
        try { match.SnapshotVisibleTransportTags(hidden); }
        catch (InvalidOperationException) { hiddenRejected = true; }
        Check(hiddenRejected, "hidden identity entered shared transport-presence audit lifecycle");
        game.Player2.HandZone.Remove(hidden);
        game.Player2.BoardZone.Add((Minion)hidden);
        hiddenBatch.Clear();
        hiddenBatch.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(hidden));
        int revealedReplayCursor = capture.Packets.Count;
        SendHistoryEntries(match, hiddenBatch);
        List<PowerHistoryData> revealedReplay = History(capture, revealedReplayCursor);
        Check(revealedReplay.Any(entry => entry.HasFullEntity && entry.FullEntity.Entity == hidden.Id
                && entry.FullEntity.Name == "CS2_120")
            && revealedReplay.All(entry => !entry.HasTagChange
                || entry.TagChange.Entity != hidden.Id || entry.TagChange.Tag != (int)GameTag.COST),
            "consumed hidden aura correction replayed after entity became public");

        var auraOwner = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("EX1_608"), zone: game.Player1.BoardZone);
        game.AuraUpdate();
        EngineGame clone = game.Clone(history: true);
        Aura cloneAura = clone.Auras.OfType<Aura>().FirstOrDefault();
        Check(cloneAura != null && auraOwner != null, "clone isolation fixture lacks aura");
        CopyTransportBridge.Enter(match, game);
        try
        {
            AuraTransportBridge.BeforeRemove(cloneAura, out AuraTransportBridge.RemovalState state);
            Check(state == null, "foreign cloned game armed aura removal capture");
        }
        finally
        {
            CopyTransportBridge.Exit();
        }
        capture.SerializeAll();
    }

    private static void RemoveAuraThroughSeam(NativeMatch match, Aura aura)
    {
        MethodInfo remove = AuraTransportBridge.RequiredRemoveInternal(
            aura is SwitchingAura ? typeof(SwitchingAura) : typeof(Aura));
        CopyTransportBridge.Enter(match, match.Game);
        try
        {
            AuraTransportBridge.BeforeRemove(aura, out AuraTransportBridge.RemovalState state);
            remove.Invoke(aura, null);
            AuraTransportBridge.AfterRemove(state);
        }
        finally
        {
            CopyTransportBridge.Exit();
        }
    }

    private static void CompleteQueuedAuraRemoval(NativeMatch match, Aura aura)
    {
        // Local net10 cannot detour old Harmony; bracket the exact Aura.Update caller boundary instead.
        CopyTransportBridge.Enter(match, match.Game);
        try
        {
            AuraTransportBridge.BeforeRemove(aura, out AuraTransportBridge.RemovalState state);
            aura.Update();
            AuraTransportBridge.AfterRemove(state);
        }
        finally
        {
            CopyTransportBridge.Exit();
        }
    }

    private static void RemoveAuraThroughCaller(NativeMatch match, Aura aura)
    {
        aura.Remove();
        CompleteQueuedAuraRemoval(match, aura);
    }

    private static void MirrorEntityHistoryUsesDestinationIdentity()
    {
        var capture = new Capture();
        var match = StartFixture(capture, "EX1_294", null);
        EngineGame game = match.Game;

        IPlayable mirror = game.Player1.HandZone.First(card => card.Card.Id == "EX1_294");
        InvokeProcess(match, game.Player1.Options().First(task => task.Source?.Id == mirror.Id));
        FlushHistory(match);
        InvokeProcess(match, game.Player1.Options().Single(task => task.PlayerTaskType == PlayerTaskType.END_TURN));
        FlushHistory(match);

        game.Player2.BaseMana = 10;
        game.Player2.UsedMana = 0;
        IPlayable sword = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player2, Cards.FromId("EX1_366"), zone: game.Player2.HandZone);
        IPlayable boulder = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player2, Cards.FromId("CS2_200"), zone: game.Player2.HandZone);
        FlushHistory(match);
        InvokeProcess(match, game.Player2.Options().First(task => task.Source?.Id == sword.Id));
        FlushHistory(match);

        int cursor = capture.Packets.Count;
        InvokeProcess(match, game.Player2.Options().First(task => task.Source?.Id == boulder.Id));
        List<SabberStoneCore.Kettle.IPowerHistoryEntry> engineHistory = game.PowerHistory.Last.ToList();
        Minion source = game.Player2.BoardZone.Single(minion => minion.Card.Id == "CS2_200");
        Minion copy = game.Player1.BoardZone.Single(minion => minion.Card.Id == "CS2_200");
        FlushHistory(match);

        var engineFull = engineHistory.OfType<SabberStoneCore.Kettle.PowerHistoryFullEntity>()
            .Single(entry => entry.Entity.Id == copy.Id);
        Check(engineFull.Entity.Tags.TryGetValue(GameTag.ENTITY_ID, out int engineIdentity)
            && engineIdentity == source.Id && engineIdentity != copy.Id,
            "Mirror fixture no longer carries the pinned source identity defect");
        List<PowerHistoryData> native = History(capture, cursor);
        PowerHistoryData full = native.Single(entry => entry.HasFullEntity && entry.FullEntity.Entity == copy.Id);
        PowerHistoryData show = native.Single(entry => entry.HasShowEntity && entry.ShowEntity.Entity == copy.Id);
        Check(full.FullEntity.Name == "CS2_200" && SnapshotTag(full, GameTag.ENTITY_ID) == copy.Id,
            "Mirror FULL did not normalize embedded identity to its destination");
        Check(show.ShowEntity.Name == "CS2_200" && SnapshotTag(show, GameTag.ENTITY_ID) == copy.Id,
            "Mirror SHOW did not normalize embedded identity to its destination");
        foreach (PowerHistoryData entry in native)
        {
            var snapshot = entry.HasFullEntity ? entry.FullEntity
                : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
            if (snapshot != null && snapshot.Tags.Any(tag => tag.Name == (int)GameTag.ENTITY_ID))
                Check(TagValue(snapshot.Tags, GameTag.ENTITY_ID) == snapshot.Entity,
                    "serialized history contains contradictory outer and embedded identity");
        }
        bool rejected = false;
        try
        {
            NativeMatch.ValidateHistoryEntityIdentity(new PegasusGame.PowerHistoryEntity
            {
                Entity = copy.Id,
                Tags = new List<PegasusGame.Tag> {
                    new PegasusGame.Tag { Name = (int)GameTag.ENTITY_ID, Value = source.Id }
                }
            });
        }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "fatal history audit accepted contradictory embedded identity");
        NativeMatch.ValidateHistoryEntityIdentity(new PegasusGame.PowerHistoryEntity
        {
            Entity = copy.Id,
            Tags = new List<PegasusGame.Tag>()
        });
        Check(copy.Id != source.Id && copy.Controller == game.Player1 && copy.Zone.Type == EngineZone.PLAY
            && copy.AttackDamage == 6 && copy[GameTag.HEALTH] == 7,
            "Mirror did not create a distinct visible 6/7 friendly actor");
        Check(source.AttackDamage == 7 && source[GameTag.HEALTH] == 8,
            "Sword did not apply after Mirror captured the 6/7 source");

        game.Player2.BaseMana = 10;
        game.Player2.UsedMana = 0;
        IPlayable polymorph = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player2, Cards.FromId("CS2_022"), zone: game.Player2.HandZone);
        FlushHistory(match);
        InvokeProcess(match, game.Player2.Options().First(task =>
            task.Source?.Id == polymorph.Id && task.Target?.Id == copy.Id));
        FlushHistory(match);
        Check(copy.Card.Id == "CS2_tk1", "later legal targeting did not transform the Mirror actor");
        game.Player2.UsedMana = 0;
        IPlayable fireball = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player2, Cards.FromId("CS2_029"), zone: game.Player2.HandZone);
        FlushHistory(match);
        InvokeProcess(match, game.Player2.Options().First(task =>
            task.Source?.Id == fireball.Id && task.Target?.Id == copy.Id));
        FlushHistory(match);
        List<PowerHistoryData> later = History(capture, cursor);
        Check(later.Any(entry => entry.HasPowerStart && entry.PowerStart.Target == copy.Id)
            && later.Any(entry => entry.HasChangeEntity && entry.ChangeEntity.Entity == copy.Id
                && entry.ChangeEntity.Name == "CS2_tk1")
            && later.Any(entry => entry.HasTagChange && entry.TagChange.Entity == copy.Id
                && entry.TagChange.Tag == (int)GameTag.ZONE
                && entry.TagChange.Value == (int)EngineZone.GRAVEYARD),
            "later target/change/zone history lost the normalized Mirror actor");
        capture.SerializeAll();
    }

    private static NativeMatch StartFixture(Capture capture, string cardId, Action<EngineGame> arrange)
    {
        var card = Cards.FromId(cardId);
        string heroId = card.Class == CardClass.WARRIOR ? "HERO_01" : card.Class == CardClass.PALADIN ? "HERO_04"
            : card.Class == CardClass.PRIEST ? "HERO_09" : "HERO_08";
        var match = new NativeMatch(capture.Send, Cards.FromId(heroId), Enumerable.Repeat(card, 30), 1337);
        match.Start();
        match.Game.Player1.BaseMana = 10; match.Game.Player1.UsedMana = 0;
        arrange?.Invoke(match.Game);
        match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
        return match;
    }

    private static void SendFixtureCard(NativeMatch match, Capture capture, string cardId, int targetId)
    {
        var source = match.Game.Player1.HandZone.First(card => card.Card.Id == cardId);
        var legal = match.Game.Player1.Options().Where(task => task.Source?.Id == source.Id && (task.Target?.Id ?? 0) == targetId).ToList();
        Check(legal.Count > 0, "fixture card has no exact engine task: " + cardId + "/" + targetId);
        var selected = legal[0];
        var options = capture.Last<AllOptions>(14);
        int index = options.Options.FindIndex(option => option.Type_ == NativeOption.Type.POWER && option.MainOption?.Id == source.Id);
        Check(index >= 0 && options.Options[index].MainOption.Targets.Select(target => target.Id).Contains(targetId) == (targetId != 0),
            "fixture native target mismatch: " + cardId);
        int position = selected is PlayCardTask play && source is Minion ? play.ZonePosition + 1 : 0;
        match.SendOption(options.Id, index, targetId, -1, position);
    }

    private static void SendEndTurn(NativeMatch match, Capture capture)
    {
        var options = capture.Last<AllOptions>(14);
        int index = options.Options.FindIndex(option => option.Type_ == NativeOption.Type.END_TURN);
        Check(index >= 0, "native end turn missing");
        match.SendOption(options.Id, index, 0, -1, 0);
    }

    private static List<PowerHistoryData> History(Capture capture, int cursor) => capture.Packets.Skip(cursor)
        .Where(packet => packet.Id == 19).SelectMany(packet => ((PowerHistory)packet.Message).List).ToList();

    private static HashSet<string> EmittedIds(IEnumerable<PowerHistoryData> history) => history.SelectMany(entry => new[] {
        SnapshotName(entry), entry.HasPowerStart ? entry.PowerStart.EffectCardId : ""
    }).Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.Ordinal);

    private static string SnapshotName(PowerHistoryData entry) => entry.HasFullEntity ? entry.FullEntity.Name
        : entry.HasShowEntity ? entry.ShowEntity.Name : entry.HasChangeEntity ? entry.ChangeEntity.Name : "";

    private static int SnapshotEntity(PowerHistoryData entry) => entry.HasFullEntity ? entry.FullEntity.Entity
        : entry.HasShowEntity ? entry.ShowEntity.Entity : entry.HasChangeEntity ? entry.ChangeEntity.Entity : 0;

    private static int SnapshotTag(PowerHistoryData entry, GameTag tag)
    {
        var snapshot = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
        return snapshot == null ? 0 : TagValue(snapshot.Tags, tag);
    }

    private static void StartingHandIsRevealedBeforeMulligan()
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send);
        match.Start();
        var mulligan = capture.Last<EntityChoices>(17);
        var history = capture.Packets.Where(p => p.Id == 19)
            .SelectMany(p => ((PowerHistory)p.Message).List).ToList();
        Check(mulligan.Entities.Count == 3, "fixture lacks three human starting cards");
        foreach (int id in mulligan.Entities)
        {
            var engineHistory = match.Game.PowerHistory.Full;
            int showIndex = engineHistory.FindIndex(x =>
                x is SabberStoneCore.Kettle.PowerHistoryShowEntity show && show.Entity.Id == id);
            int handIndex = engineHistory.FindIndex(x =>
                x is SabberStoneCore.Kettle.PowerHistoryTagChange tag && tag.EntityId == id &&
                tag.Tag == GameTag.ZONE && tag.Value == (int)EngineZone.HAND);
            Check(showIndex >= 0, "engine starting card lacks SHOW");
            var engineShow = (SabberStoneCore.Kettle.PowerHistoryShowEntity)engineHistory[showIndex];
            Console.WriteLine("Starting card " + id + ": SHOW zone=" +
                (EngineZone)engineShow.Entity.Tags[GameTag.ZONE] + " index=" + showIndex +
                ", HAND index=" + handIndex);
            Check(handIndex > showIndex &&
                  engineShow.Entity.Tags[GameTag.ZONE] == (int)EngineZone.HAND,
                "starting draw SHOW must carry destination HAND before zone tags");
            var reveal = history.FirstOrDefault(x => x.HasShowEntity && x.ShowEntity.Entity == id)?.ShowEntity;
            Check(reveal != null && reveal.Name == match.Game.IdEntityDic[id].Card.Id,
                "human starting card " + id + " has no named native SHOW before mulligan");
            Check(TagValue(reveal.Tags, GameTag.ZONE) == (int)EngineZone.HAND &&
                  TagValue(reveal.Tags, GameTag.CARDTYPE) == (int)CardType.MINION &&
                  TagValue(reveal.Tags, GameTag.HEALTH) > 0,
                "human starting card " + id + " lacks actor initialization tags");
        }
    }

    private static void BotMulliganAcknowledgesEngineKeeps()
    {
        bool sawKeep = false, sawReplace = false;
        foreach (string deckId in new[] { "npc-dragon-warrior", LocalDecks.All[0].Id })
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var capture = new Capture();
                SabberStoneCore.Model.Game opening = null;
                NativeMatch match = null;
                match = new NativeMatch((id, packet) => {
                    capture.Send(id, packet);
                    if (id == 17 && packet is EntityChoices choice && choice.PlayerId == 2)
                        opening = match.Game.CloneExact();
                }, LocalDecks.All[0].Id, deckId, seed);
                match.Start();
                var offer = capture.Packets.Where(p => p.Id == 17).Select(p => (EntityChoices)p.Message)
                    .Single(c => c.PlayerId == 2 && c.ChoiceType == (int)ChoiceType.MULLIGAN);
                var acknowledged = capture.Packets.Where(p => p.Id == 13).Select(p => (EntitiesChosen)p.Message)
                    .Single(c => c.PlayerId == 2 && c.ChoiceType == (int)ChoiceType.MULLIGAN);
                var profile = NpcCatalog.ForDeck(deckId);
                var expected = profile == null ? new int[0] : BotDriver.Mulligan(new MulliganView(
                    offer.Entities.Select(id => new MulliganCard(id, opening.IdEntityDic[id].Card.Id,
                        opening.IdEntityDic[id].Card.Cost)), true, LocalDecks.Get(deckId).CardIds,
                    match.Game.Player1.Hero.Card.Class), new DeckPlanMulligan(DeckPlans.ForId(profile.PlanId))).ToArray();
                Check(acknowledged.ChooseEntities.Id == offer.Id && acknowledged.ChooseEntities.Entities.SequenceEqual(expected),
                    "native bot mulligan acknowledgement differs from policy keeps");
                var decision = opening.GetPendingDecisions().Single(d => d.ControllerId == opening.Player2.Id);
                Check(opening.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(
                    decision.Id, decision.ControllerId, expected), out var error), "reference mulligan rejected: " + error);
                // Replacements can redraw an earlier discarded entity; compare the complete engine result.
                Check(opening.Player2.HandZone.Select(c => c.Id).SequenceEqual(match.Game.Player2.HandZone.Select(c => c.Id)),
                    "engine mulligan result differs from acknowledged keep list");
                if (profile != null) { sawKeep |= expected.Length > 0; sawReplace |= expected.Length < offer.Entities.Count; }
                capture.SerializeAll();
            }
        }
        Check(sawKeep && sawReplace, "NPC mulligan fixture must exercise keeps and replacements");
    }

    private static void StartProducesNativeSerializableGameAndRealMulligan()
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send);
        match.Start();

        Check(capture.Packets.All(p => p.Id == 19 || p.Id == 17 || p.Id == 13 || p.Id == 14),
            "unexpected packet ID");
        var initialHistory = capture.Packets.Where(p => p.Id == 19).Select(p => (PowerHistory)p.Message).ToList();
        var historyData = initialHistory.SelectMany(p => p.List).ToList();
        var create = historyData.Single(x => x.HasCreateGame).CreateGame;
        Check(create.GameEntity != null && create.Players.Count == 2, "missing create-game records");
        Check(create.Players[0].Id == 1 && create.Players[1].Id == 2, "wrong player IDs");
        Check(create.Players[0].GameAccountId.Hi == 0 && create.Players[0].GameAccountId.Lo == 1,
            "player 1 account is not synthetic");
        Check(create.Players[1].GameAccountId.Hi == 0 && create.Players[1].GameAccountId.Lo == 2,
            "player 2 account is not synthetic");
        Check(create.Players.All(p => p.Entity != null), "missing player entity");
        foreach (var player in create.Players)
        {
            var spellPower = player.Entity.Tags.Where(tag => tag.Name == 291).ToList();
            Check(spellPower.Count == 1 && spellPower[0].Value == 0,
                "initial player spell-power summary is not exactly one numeric 291=0");
        }
        Check(TagValue(create.Players[0].Entity.Tags, GameTag.TEAM_ID) == 1 &&
              TagValue(create.Players[1].Entity.Tags, GameTag.TEAM_ID) == 2,
            "current client TEAM_ID tags are missing");
        Check(historyData.Any(x =>
                (x.HasFullEntity && x.FullEntity.Name == "HERO_08bp") ||
                (x.HasShowEntity && x.ShowEntity.Name == "HERO_08bp") ||
                (x.HasChangeEntity && x.ChangeEntity.Name == "HERO_08bp")),
            "legacy Mage hero power was not mapped to the current client asset");
        Check(historyData.All(x =>
                (!x.HasFullEntity || x.FullEntity.Name != "CS2_034") &&
                (!x.HasShowEntity || x.ShowEntity.Name != "CS2_034") &&
                (!x.HasChangeEntity || x.ChangeEntity.Name != "CS2_034") &&
                (!x.HasPowerStart || x.PowerStart.EffectCardId != "CS2_034")),
            "legacy Mage hero power leaked into native transport");

        foreach (var data in historyData.Where(x => x.HasFullEntity))
        {
            int zone = TagValue(data.FullEntity.Tags, GameTag.ZONE);
            if (zone == (int)EngineZone.DECK)
            {
                Check(data.FullEntity.Name == "", "deck identity leaked");
                Check(HasOnlyHiddenTags(data.FullEntity.Tags), "hidden deck tags leaked identifying data");
            }
        }
        foreach (var data in historyData.Where(x => x.HasShowEntity))
        {
            int controller = TagValue(data.ShowEntity.Tags, GameTag.CONTROLLER);
            if (controller == 2)
            {
                Check(data.ShowEntity.Name == "", "opponent hand identity leaked");
                Check(HasOnlyHiddenTags(data.ShowEntity.Tags), "opponent hand tags leaked identifying data");
            }
        }
        var hiddenIds = new HashSet<int>(historyData.Where(x => x.HasFullEntity &&
                (TagValue(x.FullEntity.Tags, GameTag.ZONE) == (int)EngineZone.DECK ||
                 (TagValue(x.FullEntity.Tags, GameTag.CONTROLLER) == 2 &&
                  TagValue(x.FullEntity.Tags, GameTag.ZONE) == (int)EngineZone.HAND)))
            .Select(x => x.FullEntity.Entity));
        foreach (var change in historyData.Where(x => x.HasTagChange && hiddenIds.Contains(x.TagChange.Entity)))
            Check(IsPublicHiddenTag(change.TagChange.Tag), "hidden entity TagChange leaked identifying data");

        var openingChoices = capture.Packets.Where(p => p.Id == 17).Select(p => (EntityChoices)p.Message).ToList();
        Check(openingChoices.Any(x => x.PlayerId == 2 && x.ChoiceType == (int)ChoiceType.MULLIGAN),
            "client did not receive bot mulligan state");
        Check(capture.Packets.Where(p => p.Id == 13).Select(p => (EntitiesChosen)p.Message)
                .Any(x => x.PlayerId == 2 && x.ChoiceType == (int)ChoiceType.MULLIGAN),
            "client did not receive bot mulligan result");
        var mulligan = capture.Last<EntityChoices>(17);
        Check(mulligan.PlayerId == 1 && mulligan.ChoiceType == (int)ChoiceType.MULLIGAN,
            "client did not receive human mulligan");
        match.SendChoices(mulligan.Id, new List<int>());
        Check(match.Game.Player1.MulliganState == Mulligan.DONE && match.Game.Player2.MulliganState == Mulligan.DONE,
            "both mulligans did not resolve");
        Check(match.Game.Step == Step.MAIN_ACTION && match.Game.CurrentPlayer.PlayerId == 1,
            "human first turn did not begin");
        var chosen = capture.Last<EntitiesChosen>(13);
        Check(chosen.ChooseEntities.Id == mulligan.Id && chosen.PlayerId == 1,
            "mulligan acknowledgement is invalid");
        Check(capture.Last<AllOptions>(14).Options.Any(o => o.Type_ == NativeOption.Type.END_TURN),
            "human did not receive legal options");
        capture.SerializeAll();
    }

    private static void InvalidAndStaleOptionsLeaveStateUnchanged()
    {
        var capture = new Capture();
        var match = StartAtHumanTurn(capture);
        var options = capture.Last<AllOptions>(14);
        AssertRejectedWithoutMutation(match, capture, () => match.SendOption(options.Id + 1000, 0, 0, 0, 0));

        int end = options.Options.FindIndex(o => o.Type_ == NativeOption.Type.END_TURN);
        int oldTurn = match.Game.Turn;
        match.SendOption(options.Id, end, 0, -1, 0);
        Check(match.Game.Turn > oldTurn, "legal end-turn did not drive game state");
        AssertRejectedWithoutMutation(match, capture, () => match.SendOption(options.Id, end, 0, -1, 0));

        var nextOptions = capture.Last<AllOptions>(14);
        int nonMinion = nextOptions.Options.FindIndex(o =>
            o.Type_ == NativeOption.Type.POWER && o.MainOption != null &&
            (!match.Game.IdEntityDic.ContainsKey(o.MainOption.Id) ||
             !(match.Game.IdEntityDic[o.MainOption.Id] is SabberStoneCore.Model.Entities.Minion minion) ||
             minion.Zone?.Type != EngineZone.HAND));
        Check(nonMinion >= 0, "fixture lacks a non-minion option");
        int nonMinionTarget = nextOptions.Options[nonMinion].MainOption.Targets.FirstOrDefault()?.Id ?? 0;
        AssertRejectedWithoutMutation(match, capture,
            () => match.SendOption(nextOptions.Id, nonMinion, nonMinionTarget, -1, 1));
        AssertRejectedWithoutMutation(match, capture,
            () => match.SendOption(nextOptions.Id, nonMinion, nonMinionTarget, -1, -1));
        capture.SerializeAll();
    }

    private static void SpellPowerSummaryTracksAcceptedActions()
    {
        var capture = new Capture();
        var match = StartAtHumanTurn(capture);
        var game = match.Game;
        bool player1HadRawSummary = game.Player1.NativeTags.ContainsKey((GameTag)291);
        int player1RawSummary = player1HadRawSummary ? game.Player1.NativeTags[(GameTag)291] : 0;
        game.Player1.BaseMana = 10;
        game.Player1.UsedMana = 0;
        var azure = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("EX1_284"), zone: game.Player1.HandZone);
        var thalnos = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("EX1_012"), zone: game.Player1.HandZone);
        var lethalFireball = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("CS2_029"), zone: game.Player1.HandZone);
        var unchangedFireball = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("CS2_029"), zone: game.Player1.HandZone);
        var polymorphAzure = SabberStoneCore.Model.Entities.Entity.FromCard(
            game.Player1, Cards.FromId("CS2_022"), zone: game.Player1.HandZone);
        int fixtureCursor = capture.Packets.Count;
        FlushHistory(match);
        Check(SpellPowerChanges(capture, fixtureCursor).Count == 0,
            "unchanged fixture history emitted spell-power summary");

        SendCardOption(match, capture, azure.Id);
        AssertLastSpellPowerAction(capture, game.Player1.Id, 1);
        Check(game.Player1.CurrentSpellPower == 1, "accepted Azure Drake did not establish spell power 1");

        game.Player1.UsedMana = 0;
        SendCardOption(match, capture, thalnos.Id);
        AssertLastSpellPowerAction(capture, game.Player1.Id, 2);
        Check(game.Player1.CurrentSpellPower == 2, "accepted Bloodmage Thalnos did not establish spell power 2");

        game.Player1.UsedMana = 0;
        SendCardOption(match, capture, lethalFireball.Id, thalnos.Id);
        AssertLastSpellPowerAction(capture, game.Player1.Id, 1);
        Check(thalnos.Zone?.Type == EngineZone.GRAVEYARD && game.Player1.CurrentSpellPower == 1,
            "accepted lethal damage did not remove Thalnos spell power");

        game.Player1.UsedMana = 0;
        int unchangedCursor = capture.Packets.Count;
        SendCardOption(match, capture, unchangedFireball.Id, game.Player2.Hero.Id);
        Check(SpellPowerChanges(capture, unchangedCursor).Count == 0,
            "post-death unchanged spell-power total emitted duplicate summary");
        Check(game.Player2.Hero.Health == 23, "accepted Fireball did not use remaining engine spell power 1");

        game.Player1.UsedMana = 0;
        SendCardOption(match, capture, polymorphAzure.Id, azure.Id);
        AssertLastSpellPowerAction(capture, game.Player1.Id, 0);
        Check(game.Player1.CurrentSpellPower == 0, "accepted Azure transform did not reduce spell power to 0");
        Check(game.Player1.NativeTags.ContainsKey((GameTag)291) == player1HadRawSummary &&
              (!player1HadRawSummary || game.Player1.NativeTags[(GameTag)291] == player1RawSummary),
            "native summary mutated engine controller tag 291");
        capture.SerializeAll();

        var transferCapture = new Capture();
        var transferMatch = StartAtHumanTurn(transferCapture, LocalDecks.Get("priest"), "priest", 91);
        var transferGame = transferMatch.Game;
        bool transferPlayer1HadRawSummary = transferGame.Player1.NativeTags.ContainsKey((GameTag)291);
        bool transferPlayer2HadRawSummary = transferGame.Player2.NativeTags.ContainsKey((GameTag)291);
        int transferPlayer1RawSummary = transferPlayer1HadRawSummary ? transferGame.Player1.NativeTags[(GameTag)291] : 0;
        int transferPlayer2RawSummary = transferPlayer2HadRawSummary ? transferGame.Player2.NativeTags[(GameTag)291] : 0;
        transferGame.Player1.BaseMana = 10;
        transferGame.Player1.UsedMana = 0;
        var stolenThalnos = SabberStoneCore.Model.Entities.Entity.FromCard(
            transferGame.Player1, Cards.FromId("EX1_012"), zone: transferGame.Player1.HandZone);
        FlushHistory(transferMatch);
        InvokeProcess(transferMatch, transferGame.Player1.Options().First(task => task.Source?.Id == stolenThalnos.Id));
        FlushHistory(transferMatch);
        Check(transferGame.Player1.CurrentSpellPower == 1 && transferGame.Player2.CurrentSpellPower == 0,
            "control-transfer fixture did not start 1/0");
        InvokeProcess(transferMatch, transferGame.Player1.Options().Single(task =>
            task.PlayerTaskType == SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.END_TURN));
        FlushHistory(transferMatch);
        transferGame.Player2.BaseMana = 10;
        transferGame.Player2.UsedMana = 0;
        var cabal = SabberStoneCore.Model.Entities.Entity.FromCard(
            transferGame.Player2, Cards.FromId("EX1_091"), zone: transferGame.Player2.HandZone);
        FlushHistory(transferMatch);
        int transferCursor = transferCapture.Packets.Count;
        InvokeProcess(transferMatch, transferGame.Player2.Options().First(task =>
            task.Source?.Id == cabal.Id && task.Target?.Id == stolenThalnos.Id));
        FlushHistory(transferMatch);
        var transferChanges = SpellPowerChanges(transferCapture, transferCursor);
        Check(transferGame.Player1.CurrentSpellPower == 0 && transferGame.Player2.CurrentSpellPower == 1,
            "accepted Cabal transfer did not move spell power across controllers");
        Check(transferChanges.Count == 2 &&
              transferChanges[0].TagChange.Entity == transferGame.Player1.Id && transferChanges[0].TagChange.Value == 0 &&
              transferChanges[1].TagChange.Entity == transferGame.Player2.Id && transferChanges[1].TagChange.Value == 1,
            "control transfer did not emit ordered summaries for both controllers");
        Check(transferGame.Player1.NativeTags.ContainsKey((GameTag)291) == transferPlayer1HadRawSummary &&
              transferGame.Player2.NativeTags.ContainsKey((GameTag)291) == transferPlayer2HadRawSummary &&
              (!transferPlayer1HadRawSummary || transferGame.Player1.NativeTags[(GameTag)291] == transferPlayer1RawSummary) &&
              (!transferPlayer2HadRawSummary || transferGame.Player2.NativeTags[(GameTag)291] == transferPlayer2RawSummary),
            "control-transfer summary mutated engine controller tag 291");
        var transferHistory = NativeHistory(transferCapture, transferCursor);
        int lastBlockEnd = transferHistory.FindLastIndex(entry => entry.HasPowerEnd);
        int firstSummary = transferHistory.FindIndex(entry => entry.HasTagChange && entry.TagChange.Tag == 291);
        Check(lastBlockEnd >= 0 && firstSummary > lastBlockEnd && transferHistory.Skip(firstSummary).All(entry =>
            entry.HasTagChange && entry.TagChange.Tag == 291),
            "control-transfer summaries were not appended after normal block history");
        transferCapture.SerializeAll();
        Console.WriteLine("PASS native spell-power summary lifecycle");
    }

    private static void SendCardOption(NativeMatch match, Capture capture, int sourceId, int targetId = 0)
    {
        var sendOptions = typeof(NativeMatch).GetMethod("SendHumanOptions",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        sendOptions.Invoke(match, null);
        var options = capture.Last<AllOptions>(14);
        int index = options.Options.FindIndex(option => option.Type_ == NativeOption.Type.POWER &&
            option.MainOption?.Id == sourceId && option.MainOption.Targets.Any(target => target.Id == targetId));
        if (targetId == 0)
            index = options.Options.FindIndex(option => option.Type_ == NativeOption.Type.POWER &&
                option.MainOption?.Id == sourceId);
        Check(index >= 0, "accepted-action fixture lacks option for " + sourceId + " target " + targetId);
        int cursor = capture.Packets.Count;
        int position = match.Game.IdEntityDic[sourceId] is SabberStoneCore.Model.Entities.Minion ? 1 : 0;
        match.SendOption(options.Id, index, targetId, -1, position);
        int historyPacket = capture.Packets.FindIndex(cursor, packet => packet.Id == 19);
        int optionsPacket = capture.Packets.FindIndex(cursor, packet => packet.Id == 14);
        Check(historyPacket >= cursor && optionsPacket > historyPacket,
            "spell-power action history was not sent before next options");
    }

    private static List<PowerHistoryData> SpellPowerChanges(Capture capture, int cursor) =>
        NativeHistory(capture, cursor).Where(entry => entry.HasTagChange && entry.TagChange.Tag == 291).ToList();

    private static void AssertLastSpellPowerAction(Capture capture, int entity, int value)
    {
        var packet = (PowerHistory)capture.Packets.Last(item => item.Id == 19).Message;
        int lastBlockEnd = packet.List.FindLastIndex(entry => entry.HasPowerEnd);
        int summary = packet.List.FindIndex(entry => entry.HasTagChange && entry.TagChange.Tag == 291);
        Check(summary > lastBlockEnd && summary == packet.List.Count - 1 &&
              packet.List[summary].TagChange.Entity == entity && packet.List[summary].TagChange.Value == value,
            "spell-power summary " + entity + "=" + value + " was missing, duplicated, or not final after block");
    }

    private static void LegalPacketActionsFinishARealMatch()
    {
        var capture = new Capture();
        var match = StartAtHumanTurn(capture);
        int actions = 0;
        int packetCursor = capture.Packets.Count;

        while (match.Game.State != State.COMPLETE && actions++ < 700)
        {
            var choicePacket = capture.Packets.Skip(packetCursor)
                .LastOrDefault(p => p.Id == 17 && p.Message is EntityChoices);
            var optionPacket = capture.Packets.Skip(packetCursor)
                .LastOrDefault(p => p.Id == 14 && p.Message is AllOptions);
            if (choicePacket.Message is EntityChoices choice)
            {
                packetCursor = capture.Packets.Count;
                var picks = choice.CountMin == 0 ? new List<int>() : choice.Entities.Take(choice.CountMin).ToList();
                match.SendChoices(choice.Id, picks);
                continue;
            }
            if (!(optionPacket.Message is AllOptions options))
                options = capture.Last<AllOptions>(14);
            AssertHumanHandHasNativeIdentity(match, capture);
            packetCursor = capture.Packets.Count;
            PlayBestPacketAction(match, options);
        }

        Check(match.Game.State == State.COMPLETE, "legal packet driver did not finish match");
        Check(new[] { match.Game.Player1.PlayState, match.Game.Player2.PlayState }.Count(x => x == PlayState.WON) == 1,
            "match lacks one engine winner");
        Check(new[] { match.Game.Player1.PlayState, match.Game.Player2.PlayState }.Count(x => x == PlayState.LOST) == 1,
            "match lacks one engine loser");
        var data = capture.Packets.Where(p => p.Id == 19).SelectMany(p => ((PowerHistory)p.Message).List).ToList();
        Check(data.Any(x => x.HasTagChange && x.TagChange.Entity == match.Game.Player1.Id &&
                            x.TagChange.Tag == (int)GameTag.PLAYSTATE && x.TagChange.Value == (int)PlayState.WON) ||
              data.Any(x => x.HasTagChange && x.TagChange.Entity == match.Game.Player2.Id &&
                            x.TagChange.Tag == (int)GameTag.PLAYSTATE && x.TagChange.Value == (int)PlayState.WON),
            "native history lacks winner PLAYSTATE");
        Check(data.Any(x => x.HasTagChange && x.TagChange.Entity == match.Game.Player1.Id &&
                            x.TagChange.Tag == (int)GameTag.PLAYSTATE && x.TagChange.Value == (int)PlayState.LOST) ||
              data.Any(x => x.HasTagChange && x.TagChange.Entity == match.Game.Player2.Id &&
                            x.TagChange.Tag == (int)GameTag.PLAYSTATE && x.TagChange.Value == (int)PlayState.LOST),
            "native history lacks loser PLAYSTATE");
        Check(data.Any(x => x.HasTagChange && x.TagChange.Entity == match.Game.Id &&
                            x.TagChange.Tag == (int)GameTag.STATE && x.TagChange.Value == (int)State.COMPLETE),
            "native history lacks complete game STATE");
        foreach (int source in data.Where(x => x.HasPowerStart && x.PowerStart.Type == HistoryBlock.Type.PLAY)
                     .Select(x => x.PowerStart.Source).Distinct())
        {
            if (match.Game.IdEntityDic.ContainsKey(source) && match.Game.IdEntityDic[source].Controller.PlayerId == 2)
                Check(data.Any(x => (x.HasShowEntity && x.ShowEntity.Entity == source && x.ShowEntity.Name.Length > 0) ||
                                    (x.HasFullEntity && x.FullEntity.Entity == source && x.FullEntity.Name.Length > 0)),
                    "bot play never revealed hidden card " + source);
        }
        capture.SerializeAll();
    }

    private static void AssertHumanHandHasNativeIdentity(NativeMatch match, Capture capture)
    {
        var history = capture.Packets.Where(p => p.Id == 19)
            .SelectMany(p => ((PowerHistory)p.Message).List).ToList();
        foreach (var card in match.Game.Player1.HandZone)
        {
            var reveal = history.LastOrDefault(x => x.HasShowEntity && x.ShowEntity.Entity == card.Id)?.ShowEntity;
            Check(reveal != null && reveal.Name == card.Card.Id,
                "human hand card " + card.Id + " has no native identity before legal options");
            Check(TagValue(reveal.Tags, GameTag.CARDTYPE) == (int)card.Card.Type,
                "human hand card " + card.Id + " lacks actor card type");
        }
    }

    private static void ConcedeFinishesThroughEngine()
    {
        var capture = new Capture();
        var match = StartAtHumanTurn(capture);
        int packetCursor = capture.Packets.Count;
        match.Concede();
        Check(match.Game.State == State.COMPLETE, "concede did not complete engine game");
        Check(match.Game.Player1.PlayState == PlayState.LOST && match.Game.Player2.PlayState == PlayState.WON,
            "concede did not assign winner through engine");
        var data = capture.Packets.Skip(packetCursor).Where(p => p.Id == 19)
            .SelectMany(p => ((PowerHistory)p.Message).List).ToList();
        Check(data.Any(x => x.HasTagChange && x.TagChange.Entity == match.Game.Player1.Id &&
                            x.TagChange.Tag == (int)GameTag.PLAYSTATE && x.TagChange.Value == (int)PlayState.LOST),
            "concede history lacks loser PLAYSTATE");
        Check(data.Any(x => x.HasTagChange && x.TagChange.Entity == match.Game.Player2.Id &&
                            x.TagChange.Tag == (int)GameTag.PLAYSTATE && x.TagChange.Value == (int)PlayState.WON),
            "concede history lacks winner PLAYSTATE");
        Check(data.Any(x => x.HasTagChange && x.TagChange.Entity == match.Game.Id &&
                            x.TagChange.Tag == (int)GameTag.STATE && x.TagChange.Value == (int)State.COMPLETE),
            "concede history lacks complete game STATE");
        Check(!capture.Packets.Skip(packetCursor).Any(p => p.Id == 14),
            "concede emitted options after completion");
        capture.SerializeAll();
    }

    // Test control's debug instant win: the NPC concedes on the human's turn, recorded as its own concession.
    private static void NpcConcedeForTestWinsThroughEngine()
    {
        var capture = new Capture();
        var match = StartAtHumanTurn(capture);
        int packetCursor = capture.Packets.Count;
        Check(match.Game.CurrentPlayer == match.Game.Player1, "fixture: human turn");
        match.ConcedeNpcForTest();
        Check(match.Game.State == State.COMPLETE && match.Game.Player1.PlayState == PlayState.WON
            && match.Game.Player2.PlayState == PlayState.LOST, "NPC concede did not make the human win");
        Check(match.OpponentConceded && !match.HumanConceded, "NPC concede not recorded as its own concession");
        var data = capture.Packets.Skip(packetCursor).Where(p => p.Id == 19)
            .SelectMany(p => ((PowerHistory)p.Message).List).ToList();
        Check(data.Any(x => x.HasTagChange && x.TagChange.Entity == match.Game.Player1.Id &&
                            x.TagChange.Tag == (int)GameTag.PLAYSTATE && x.TagChange.Value == (int)PlayState.WON),
            "NPC concede history lacks winner PLAYSTATE");
        bool threw = false;
        try { match.ConcedeNpcForTest(); } catch (InvalidOperationException) { threw = true; }
        Check(threw, "a finished match refuses another NPC concede");
        capture.SerializeAll();
    }

    // Test control's debug instant loss: a plain loss, recorded as no concession on either side.
    private static void LoseForTestIsAPlainLoss()
    {
        var capture = new Capture();
        var match = StartAtHumanTurn(capture);
        match.LoseForTest();
        Check(match.Game.State == State.COMPLETE && match.Game.Player1.PlayState == PlayState.LOST
            && match.Game.Player2.PlayState == PlayState.WON, "debug defeat did not make the human lose");
        Check(!match.HumanConceded && !match.OpponentConceded, "debug defeat recorded a concession");
        var outcome = MatchOutcome.From(match.Game, match.HumanConceded);
        Check(outcome.Result == "Lost" && !outcome.Conceded, "debug defeat does not settle as a plain loss");
        capture.SerializeAll();
    }

    private static NativeMatch StartAtHumanTurn(Capture capture)
    {
        var match = new NativeMatch(capture.Send);
        match.Start();
        var mulligan = capture.Last<EntityChoices>(17);
        match.SendChoices(mulligan.Id, new List<int>());
        return match;
    }

    private static NativeMatch StartAtHumanTurn(Capture capture, LocalDeck player, string opponent, int seed)
    {
        var match = new NativeMatch(capture.Send, player, opponent, seed);
        match.Start();
        var mulligan = capture.Last<EntityChoices>(17);
        match.SendChoices(mulligan.Id, new List<int>());
        return match;
    }

    private static void PlayBestPacketAction(NativeMatch match, AllOptions options)
    {
        int enemyHero = match.Game.Player2.Hero.Id;
        int selected = -1;
        int target = 0;
        for (int i = 0; i < options.Options.Count; i++)
        {
            var option = options.Options[i];
            if (option.Type_ != NativeOption.Type.POWER || option.MainOption == null)
                continue;
            var enemy = option.MainOption.Targets.FirstOrDefault(t => t.Id == enemyHero);
            if (enemy != null && IsAttackSource(match.Game, option.MainOption.Id))
            {
                selected = i;
                target = enemy.Id;
                break;
            }
        }
        if (selected < 0)
        {
            selected = options.Options.FindIndex(o => o.Type_ == NativeOption.Type.POWER && o.MainOption.PowerKeyword == 0);
            if (selected >= 0)
                target = options.Options[selected].MainOption.Targets.FirstOrDefault()?.Id ?? 0;
        }
        if (selected < 0)
            selected = options.Options.FindIndex(o => o.Type_ == NativeOption.Type.END_TURN);
        Check(selected >= 0, "no legal packet action");
        int position = 0;
        if (options.Options[selected].Type_ == NativeOption.Type.POWER &&
            match.Game.IdEntityDic.ContainsKey(options.Options[selected].MainOption.Id) &&
            match.Game.IdEntityDic.TryGetValue(options.Options[selected].MainOption.Id, out var source) &&
            (source is Minion || source is BoardObject) && source.Zone?.Type == EngineZone.HAND)
            position = 1;
        int sub = -1;
        if (options.Options[selected].SubOptions.Count > 0)
        {
            sub = 0;
            target = options.Options[selected].SubOptions[sub].Targets.FirstOrDefault()?.Id ?? 0;
        }
        try { match.SendOption(options.Id, selected, target, sub, position); }
        catch {
            Console.WriteLine("FAILED option source=" + options.Options[selected].MainOption?.Id + " target=" + target + " pos=" + position);
            Console.WriteLine(string.Join("\n", match.Game.Player1.Options().Select(t => t.FullPrint())));
            throw;
        }
    }

    private static bool IsAttackSource(EngineGame game, int id)
    {
        if (id == game.Player1.Hero.Id)
            return true;
        return game.IdEntityDic.ContainsKey(id) && game.IdEntityDic[id].Zone?.Type == EngineZone.PLAY;
    }

    private static void AssertRejectedWithoutMutation(NativeMatch match, Capture capture, Action action)
    {
        int turn = match.Game.Turn;
        Step step = match.Game.Step;
        State state = match.Game.State;
        int current = match.Game.CurrentPlayer.PlayerId;
        int p1Health = match.Game.Player1.Hero.Health;
        int p2Health = match.Game.Player2.Hero.Health;
        int packets = capture.Packets.Count;
        bool rejected = false;
        try { action(); }
        catch (InvalidOperationException) { rejected = true; }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "invalid/stale action was accepted");
        Check(match.Game.Turn == turn && match.Game.Step == step && match.Game.State == state &&
              match.Game.CurrentPlayer.PlayerId == current && match.Game.Player1.Hero.Health == p1Health &&
              match.Game.Player2.Hero.Health == p2Health && capture.Packets.Count == packets,
            "rejected action mutated state");
    }

    private static int TagValue(List<PegasusGame.Tag> tags, GameTag tag) =>
        tags.FirstOrDefault(x => x.Name == (int)tag)?.Value ?? 0;

    private static bool HasOnlyHiddenTags(List<PegasusGame.Tag> tags) =>
        tags.All(x => x.Name == (int)GameTag.ENTITY_ID || x.Name == (int)GameTag.CONTROLLER ||
                      x.Name == (int)GameTag.ZONE || x.Name == (int)GameTag.ZONE_POSITION);

    private static bool IsPublicHiddenTag(int tag) =>
        tag == (int)GameTag.ENTITY_ID || tag == (int)GameTag.CONTROLLER ||
        tag == (int)GameTag.ZONE || tag == (int)GameTag.ZONE_POSITION;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("TEST: " + message);
    }
}
