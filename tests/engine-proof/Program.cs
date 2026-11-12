using System.Reflection;
using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

if (args.Contains("--ai-frontier-only")) { AiFrontierProof.Run(); return; }
if (args.Contains("--canonical-policy-only")) { CanonicalPolicyChecks.Run(); return; }
if (args.Contains("--shadow-trial-only")) { ShadowTrialProof.Run(); return; }
if (args.Contains("--card-facts-only")) { CardFactsProof.Run(); return; }
if (args.Contains("--attack-trigger-cards-only")) { AttackTriggerSweepProof.PositiveControls(); return; }
if (args.Contains("--attack-trigger-inventory-only")) { AttackTriggerSweepProof.Inventory(); return; }
if (args.Contains("--core-card-proofs-only")) { Core2026Proof.CardProofs(); return; }
if (args.Contains("--wrathspike-only")) { StandardSetsProof.Run(selectedProofs: new Action[] { WrathspikeBruteProof.Run }); return; }
if (GroupProofs.TryRun(args, proof => StandardSetsProof.Run(selectedProofs: new[] { proof }), allowUnmatched: true)) return;
if (args.Length == 2 && args[0] == "--colossal-regression") { ColossalDestinationRegressionProof.Run(args[1]); return; }
if (args.Contains("--yogg-only")) { B18TYoggProof.Run(); return; }
if (args.Contains("--facilities-only")) { GroupProofs.Run(ProofKind.Facility); return; }
if (args.Contains("--nozdormu-newest-only")) { StandardSetsProof.Run(selectedProofs: new Action[] { TimelordNozdormuProof.Run }); return; }
if (args.Contains("--release-meta-cards-only")) { StandardSetsProof.Run(selectedProofs: new Action[] {
    SparkOfLifeProof.Run, CremateProof.Run, BeastTripwireProof.Run, KindredSpellsProof.Run,
    SolitudeProof.Run, RunedOrbProof.Run }); return; }

if (args.Contains("--held-transform-history-only")) { HeldFormProof.DefinitionResetHistory(); return; }
if (args.Contains("--repeat-lost-target-only")) { RepeatLostTargetProof.Run(); return; }
if (args.Contains("--b13-tag-isolation-only")) { Batch13TagIsolationProof.Run(); return; }

if (args.Contains("--cthun-cards-only")) { StandardSetsProof.Run(selectedProofs: new Action[] {
    GargantuanSlithererProof.Run, DarkDeaconProof.Run, SpawnOfCthunProof.Run, TentacleLashProof.Run, WatchOverProof.Run }); return; }

if (args.FirstOrDefault() == "--discover-real-regressions-only") { DiscoverRealProof.RunRegressions(args.ElementAtOrDefault(1)); return; }
if (args.Contains("--discover-real-only")) { DiscoverRealProof.Run(); return; }
if (args.Contains("--discover-real-cards-only")) { StandardSetsProof.Run(selectedProofs: new Action[] { HellraiserProof.Run, FrameJobProof.Run, AncientAugurProof.Run, InspectorMurlocHolmesProof.Run, WaveshapingProof.Run, FuturisticForefatherProof.Run, CommanderGeddonProof.Run, ReloadProof.Run }); return; }

if (args.Contains("--murozond-only")) { StandardSetsProof.Run(selectedProofs: new Action[] { Unpinned_EndtimeMurozondProof.Run }); return; }
if (args.Contains("--turn-queue-only")) { TurnQueueProof.Run(); return; }
if (args.Contains("--cast-twice-cards-only")) { StandardSetsProof.Run(selectedProofs: new Action[] { Unpinned_TyrandeProof.Run, Unpinned_TherazanetheEarthmotherProof.Run, Unpinned_EfflorescenceProof.Run, Unpinned_NirioftheCraterProof.Run, Unpinned_SinestraProof.Run, Unpinned_BreakoutArchitectProof.Run, Unpinned_ElderWordForesightProof.Run, Unpinned_ElderWordManifestProof.Run, Unpinned_ElderWordFlayProof.Run }); return; }
if (args.Length == 2 && args[0] == "--pool-init-order-child") { PoolInitOrderProof.Child(args[1]); return; }
if (args.Contains("--costmemory-cards-only")) { StandardSetsProof.Run(selectedProofs: new Action[] {
    Unpinned_MarchToWarProof.Run, Unpinned_PicklockProof.Run, Unpinned_GronnGiantProof.Run, Unpinned_WizenedTruthseekerProof.Run,
    Unpinned_AlarashiProof.Run, Unpinned_FranticForgerProof.Run, Unpinned_CaliaMenethilProof.Run, Unpinned_UlfarProof.Run, Unpinned_VyranothProof.Run }); return; }
if (args.Contains("--costmemory-only")) { CostMemoryProof.Run(); return; }
if (args.Length == 2 && args[0] == "--costmemory-lifecycle") {
    foreach (bool history in new[] { true, false }) {
        if (args[1] == "transform") CostMemoryProof.DefinitionChanges(history);
        else if (args[1] == "backward") CostMemoryProof.BackwardMovement(history);
        else throw new ArgumentException("unknown cost memory lifecycle proof");
    }
    return;
}
if (args.Contains("--b13-only"))
{
    SpellHealingProof.Run();
    StandardSetsProof.Run(selectedProofs: new Action[] { ArchmageKalecProof.Run, BattlefieldBlasterProof.Run,
        EldritchTentaclesProof.Run, CleansingClericProof.Run, RubySanctumProof.Run, WiltedShadowProof.Run, LightcallerProof.Run });
    return;
}


if (args.Contains("--pool-init-order-only")) { PoolInitOrderProof.Run(); return; }

if (args.Contains("--lifesteal-unblocked-only"))
{
    StandardSetsProof.Run(lifestealUnblockedOnly: true);
    if (args.Length == 3 && args[1] == "--evidence") PoolProofEvidence.Write(args[2]);
    return;
}

if (args.Contains("--b13-secrets-only"))
{
    StandardSetsProof.Run(selectedProofs: new Action[] { Unpinned_VioletTreasuregillProof.Run,
        Unpinned_AstralMediatorProof.Run, Unpinned_TricksyImproviserProof.Run, Unpinned_FacelessEnigmaProof.Run });
    return;
}

if (args.Contains("--auto-cast-only")) { AutomaticCastProof.Run(); return; }
if (args.Contains("--auto-cast-repeat-only")) { AutomaticCastRepeatProof.Run(); return; }

if (args.Contains("--payloads-only")) { PayloadRulesProof.Run(); return; }
if (args.Contains("--payload-bashana-only")) { StandardSetsProof.Run(selectedProofs: new Action[] { BashanaRunetotemProof.Run }); return; }
if (args.Contains("--bound-cards-only")) { StandardSetsProof.Run(selectedProofs: new Action[] { BashanaRunetotemProof.Run, ClutchOfCorruptionProof.Run, EntomologistToruProof.Run, CataclysmProofs.Run, EmeraldDreamProofs.Run }); return; }
if (args.Contains("--payload-egg-only")) { StandardSetsProof.Run(selectedProofs: new Action[] { ClutchOfCorruptionProof.Run }); return; }
if (args.Contains("--payload-cards-only")) { StandardSetsProof.Run(selectedProofs: new Action[] { BashanaRunetotemProof.Run, ClutchOfCorruptionProof.Run, TimewayWardenProof.Run, IsorathProof.Run, TrickySatyrProof.Run, KeeperOfFlameProof.Run, TolvirCarverProof.Run, IridaSinseekerProof.Run, EntomologistToruProof.Run }); return; }

if (args.Contains("--companion-only")) { CompanionProof.Run(); BasicHeroPowerProof.Run(); return; }
if (args.Contains("--companion-cards-only")) { StandardSetsProof.Run(selectedProofs: new Action[] {
    Unpinned_TamePetProof.Run, Unpinned_MigratingElekkProof.Run, Unpinned_RoamFreeProof.Run,
    Unpinned_BroodchamberTunnelerProof.Run, Unpinned_AqirSwarmerProof.Run, Unpinned_IncubateProof.Run }); return; }

if (args.Contains("--moon-only")) { BlessingOfTheMoonProof.Run(); return; }
if (args.Contains("--pool-filter-only")) { OfflinePoolFilterProof.Run(); return; }

if (args.Length == 2 && args[0] == "--rewind-child") { RewindProof.Child(args[1]); return; }
if (args.Contains("--rewind-retry-discount-only")) { RewindProof.KarovClocksworthAfterExpiredTax(); RewindProof.ClocksworthCopiedRecipe(); return; }
if (args.Contains("--rewind-smoke-only")) { StandardSetsProof.Run(rewindSmokeOnly: true); return; }
if (args.Length >= 1 && args[0] == "--rewind-round-two") { RewindProof.RunRoundTwo(args.ElementAtOrDefault(1)); return; }
if (args.Contains("--rewind-only")) { RewindProof.Run(); return; }
if (args.Contains("--rewind-cards-only")) { StandardSetsProof.Run(rewindOnly: true); return; }
if (args.Contains("--clone-only")) { CloneProof.Run(); return; }
if (args.Contains("--time-travel-s2-only")) { StandardSetsProof.Run(timeTravelS2Only: true); return; }
if (args.Length > 0 && args[0] == "--split-effects-only") { SplitEffectProof.Run(args.Length > 1 ? args[1] : null); return; }

if (args.Contains("--pool-caches-only")) { PoolCacheIsolationProof.Run(); return; }
if (args.Length == 2 && args[0] == "--pool-criteria-child") { PoolCacheIsolationProof.CriteriaChild(int.Parse(args[1])); return; }
if (args.Contains("--b13-forced-cards-only")) { StandardSetsProof.Run(selectedProofs: new Action[] { MythicalTerrorProof.Run, RampagingHoundProof.Run, IllidariInquisitorProof.Run, WarmaulChallengerProof.Run, TREEEESProof.Run, HighCultistHerennProof.Run, HoundsOfFuryProof.Run, BefuddledSummonerProof.Run, AnkylodonProof.Run }); return; }
if (args.Contains("--enemy-board-copy-only")) { EnemyBoardCopyProof.Run(); return; }
if (args.Contains("--forced-attack-only")) { ForcedAttackBudgetProof.Run(); return; }
if (args.Contains("--poisonous-only")) { PoisonousDamageProof.Run(); return; }
if (args.Contains("--floop-taunt-only")) { FloopTauntProof.Run(); return; }
if (args.Contains("--weapon-keywords-only")) { WeaponKeywordProof.Run(); return; }
if (args.Contains("--weapon-damage-only")) { DamageWeaponTaskProof.Run(); return; }

if (args.Contains("--outcast-only")) { OutcastProof.Run(); return; }
if (args.Contains("--outcast-cards-only")) { StandardSetsProof.Run(outcastOnly: true); return; }
if (args.Length == 2 && args[0] == "--spell-decision-child") { DecisionSpellProof.Child(args[1]); return; }
if (args.Contains("--decisions-only")) { DecisionProof.Run(false); return; }
if (args.Contains("--spell-decisions-only")) { IndirectSpellProof.Run(); DecisionSpellProof.Run(); return; }
if (args.Contains("--terminal-spell-only")) { DecisionSpellProof.TerminalBetweenChoices(); return; }
if (args.Contains("--lethal-spell-only")) { DecisionSpellProof.LethalDeathCleanup(args.Contains("--discover"), args.Contains("--pending")); return; }
if (args.Contains("--full-hand-twinspell-only")) { DecisionSpellProof.FullHandTwinspell(); return; }
if (args.Contains("--selectors-only")) { StandardSetsProof.Run(selectorsOnly: true); return; }
if (args.Contains("--rewind-s1-only")) { StandardSetsProof.Run(rewindSliceOnly: true); return; }
if (args.Length == 2 && args[0] == "--history-display-child") { HistoryHandDisplayProof.Child(args[1]); return; }
if (args.Length == 2 && args[0] == "--history-child") { HistoryHandDisplayProof.Register(); HistorySnapshotProof.Child(args[1]); return; }
if (args.Contains("--history-only")) { HistoryFactsProof.Run(); return; }
if (args.Contains("--achievement-history-only")) { AchievementHistoryProof.Run(); return; }
if (args.Contains("--achievement-evaluator-only")) { AchievementEvaluatorProof.Run(); return; }
if (args.Contains("--cost-only")) { CostMemoryProof.Run();
CostRulesProof.Run(); CostPaymentProof.Run(); CostCopySnapshotProof.Run(); CostCompositionProof.Run(); return; }

if (args.Contains("--locations-only")) { LocationProofs.Run(); return; }
if (args.Contains("--dark-gift-behaviour-only")) { DarkGiftBehaviourProof.Run(); return; }
if (args.Contains("--dark-gifts-only")) { DarkGiftProof.Run(); DarkGiftBehaviourProof.Run(); return; }
if (args.Contains("--pool-pilots-only")) { StandardSetsProof.Run(poolPilotsOnly: true); return; }
if (args.Contains("--pool-pilot-decks-only")) { PoolPilotDeckProof.Run(); return; }

if (args.Length == 1 && args[0] == "--quest-nonrepeatable") { QuestSnapshotProof.Nonrepeatable(); return; }
if (args.Length == 2 && args[0] == "--quest-nonrepeatable-child") { QuestSnapshotProof.NonrepeatableChild(args[1]); return; }
if (args.Length == 2 && args[0] == "--quest-child") { QuestSnapshotProof.Child(args[1]); return; }
if (args.Contains("--quests-only")) { QuestLifecycleProof.Run(); QuestOpeningProof.Run(); QuestSnapshotProof.Run(); return; }
if (args.Length == 2 && args[0] == "--pool-evidence")
{
    CoreReprintsProof.Run();
    Core2026Proof.CardProofs();
    StandardSetsProof.Run();
    ForestLordCenariusProof.Run();
    LegacyPoolEligibilityProof.Run();
    PoolProofEvidence.Write(args[1]);
    return;
}

if (args.Contains("--legacy-pools-cold-child"))
{
    OfflinePoolLegacyCompatibilityProof.ColdStartChild();
    return;
}

HistoryHandDisplayProof.Register();
AutomaticCastProof.Run();
AutomaticCastRepeatProof.Run();

if (args.Length == 2 && args[0] == "--card-state-round3") { CardStateRound3Proof.Run(args[1]); return; }

if (args.Length == 2 && args[0] == "--card-state-restore-child") { DormantRulesProof.RestoreChild(args[1]); return; }
if (args.Length == 2 && args[0] == "--card-state-child") { HandProgressRulesProof.Child(args[1]); return; }
if (args.Contains("--card-state-only")) { CardStateRound3Proof.Run(); DormantRulesProof.Run(); TemporaryRulesProof.Run(); ShatterRulesProof.Run(); HandProgressRulesProof.Run(); return; }
if (args.Contains("--shatter-draw-only")) { ShatterRulesProof.Run(); DrawEventProof.Run(); HolyBolaProof.Run(); return; }

if (args.Length == 2 && args[0] == "--offline-pool-snapshot-child")
{
    OfflinePoolSnapshotProof.Child(args[1]);
    return;
}

if (args.Contains("--offline-pool-persistence-only"))
{
    OfflinePoolSnapshotProof.Run();
    OfflinePoolReplayProof.Run();
    return;
}

if (args.Contains("--offline-pool-admission-only")) { OfflinePoolAdmissionProof.Run(); return; }
if (args.Contains("--window-admission-regressions-only")) { StandardSetsProof.Run(selectedProofs: new Action[] { KarovtheBrokenProof.Run }); B18Autoplay2Proof.Run(); return; }
if (args.Contains("--offline-pool-eligibility-only")) { OfflinePoolEligibilityProof.Run(); return; }

if (args.Contains("--offline-pool-decisions-only")) { OfflinePoolDecisionProof.Run(); return; }

if (args.Contains("--offline-pools-only"))
{
    PoolPilotDeckProof.Run();
    OfflinePoolAdmissionProof.Run();
    OfflinePoolEligibilityProof.Run();
    OfflinePoolFilterProof.Run();
    OfflinePoolSamplingProof.Run();
    OfflinePoolManifestProof.Run();
    OfflinePoolDecisionProof.Run();
    OfflinePoolSnapshotProof.Run();
    OfflinePoolReplayProof.Run();
    LegacyPoolEligibilityProof.Run();
    OfflinePoolLegacyCompatibilityProof.Run();
    return;
}

if (args.Contains("--multiaura-only"))
{
    MultiAuraCleanupProof.Run();
    return;
}

if (args.Contains("--hero-immune-only"))
{
    HeroImmuneTargetingProof.Run();
    return;
}

if (args.Contains("--keywords-cfa-only"))
{
    ColossalProof.Run();
    FabledProof.Run();
    return;
}

if (args.Contains("--herald-only"))
{
    HeraldProof.Run();
    return;
}

if (args.Contains("--cross-class-only"))
{
    CrossClassPlayabilityProof.Run(args.Contains("--keywords"));
    return;
}

if (args.Contains("--prepare-only"))
{
    PrepareProof.Run();
    return;
}

if (args.Contains("--bronze-only")) { BlessingOfTheBronzeProof.Run(); return; }

if (args.Contains("--imbue-only"))
{
    ImbueProof.Run();
    return;
}

if (args.Contains("--hero-shield-only"))
{
    HeroDivineShieldProof.Run();
    return;
}
if (args.Contains("--lifesteal-only"))
{
    LifestealArmorProof.Run();
    SpellHealingProof.Run();
    return;
}
if (args.Contains("--silence-health-only"))
{
    SilenceAuraHealthProof.Run();
    return;
}

if (args.Length == 2 && args[0] == "--cenarius-child")
{
    ForestLordCenariusProof.Child(args[1]);
    return;
}

if (args.Length == 2 && args[0] == "--effects-child")
{
    EffectFrameProof.Child(args[1]);
    return;
}

if (args.Contains("--races-only"))
{
    MultipleMinionTypesProof.Run();
    return;
}

if (args.Contains("--copy-timing-only"))
{
    CopyInitializationProof.Run();
    return;
}

if (args.Contains("--cenarius-only"))
{
    ForestLordCenariusProof.Run();
PlayerDecisionProof.Run();
    return;
}

if (args.Contains("--effect-frames-only"))
{
    EffectFrameProof.Run();
    return;
}

if (args.Length == 2 && args[0] == "--player-decision-child") { PlayerDecisionProof.Child(args[1]); return; }

if (args.Contains("--player-decision-only")) { PlayerDecisionProof.Run(); return; }

if (args.Contains("--effect-context-only"))
{
    EffectContextProof.Run(args.Contains("--full"));
    return;
}

if (args.Contains("--resolution-only"))
{
    ResolutionProof.Run(args.Contains("--full"));
    return;
}

if (args.Contains("--events-only"))
{
    EventDispatchProof.Run();
    EventTraceProof.Run();
    return;
}

if (args.Contains("--silence-keywords-only"))
{
    SilenceValueProof.Run();
    SilenceKeywordsProof.Run();
    return;
}

if (args.Contains("--attack-options-only"))
{
    BrassknuckleBrawlerProof.Run();
    MantidPrecursorProof.Run();
    DevilsaurMaskProof.Run();
    HerbivoreAssistantProof.Run();
    ScorchingObserverProof.Run();
    MotherDuckProof.Run();
    EpochStalkerProof.Run();
    SoldieroftheInfiniteProof.Run();
    Console.WriteLine("PASS attack options: eight card proofs; fresh/older; silence; clone/snapshot");
    return;
}

if (args.Contains("--tokenmeta-cards-only"))
{
    StandardSetsProof.Run(selectedProofs: new Action[] { ArmyOfTheDeadProof.Run, BattlefieldNecromancerProof.Run, BoneguardCommanderProof.Run });
    return;
}

if (args.Contains("--tokenmeta-only"))
{
    TokenMetadataProof.Run();
    return;
}

if (args.Contains("--bonus-review-only")) { BonusCopyRegressionProof.Run(); return; }

if (args.Contains("--bonus-cards-only")) { StandardSetsProof.Run(selectedProofs: new Action[] { StoryOfGalvadonProof.Run, TyrannogillProof.Run, ShadowsOfYesterdayProof.Run, FrontlineIronbornProof.Run, DreamboundRaptorProof.Run, VioletPunisherProof.Run, StranglevineProof.Run, TwistedMonstrosityProof.Run }); return; }
if (args.Contains("--bonus-effects-only")) { BonusEffectsProof.Run(); return; }

if (args.Contains("--standard-sets-only"))
{
    StateTaskListDeterminismProof.Run();
    Batch13TagIsolationProof.Run();
    ForcedAttackBudgetProof.Run();
    BonusEffectsProof.Run();
    EnemyBoardCopyProof.Run();
    DarkGiftProof.Run();
    DarkGiftBehaviourProof.Run();
    StandardSetsProof.Run();
    if (args.Length == 3 && args[1] == "--evidence") PoolProofEvidence.Write(args[2]);
    return;
}

if (args.Contains("--core-reprints-only"))
{
    CoreReprintsProof.Run();
Core2026Proof.Run();
    return;
}

if (args.Length == 2 && args[0] == "--snapshot-child")
{
    SnapshotProof.Child(args[1]);
    return;
}

if (args.Contains("--snapshot-only"))
{
    SnapshotProof.Run(args.Contains("--full"));
    return;
}

if (args.Contains("--clone-benchmark"))
{
    CloneProof.Benchmark();
    return;
}

if (args.Contains("--dk-only"))
{
    ClassHeroesProof.Run();
    DeathKnightRulesProof.Run();
    CorpseProof.Run();
    return;
}

if (args.Contains("--class-heroes-only"))
{
    ClassHeroesProof.Run();
    return;
}
// Legacy handlers above own their flags; only this aggregate continues into the default suite.
if (GroupProofs.TryRun(args.Where(arg => arg != "--after-admission-only").ToArray())) return;
#if CLIENT_TRANSPORT
// The engine-proof-client suite runs only what needs your client (ClientTier.cs); the default suite below is engine-proof.
ClientTier.Run();
return;
#endif
if (!args.Contains("--after-admission-only"))
{
    CompanionProof.Run();
    BasicHeroPowerProof.Run();
    RewindProof.Run();
    SplitEffectProof.Run();
    CardStateRound3Proof.Run();
    DormantRulesProof.Run();
    TemporaryRulesProof.Run();
    CostMemoryProof.Run();
CostRulesProof.Run();
    CostPaymentProof.Run();
    CostCopySnapshotProof.Run();
    CostCompositionProof.Run();
    LocationProofs.Run();

    DarkGiftProof.Run();
    DarkGiftBehaviourProof.Run();
    PoolPilotDeckProof.Run();
    OfflinePoolAdmissionProof.Run();
}
OfflinePoolEligibilityProof.Run();
OfflinePoolFilterProof.Run();
OfflinePoolSamplingProof.Run();
OfflinePoolManifestProof.Run();
OfflinePoolDecisionProof.Run();
OfflinePoolSnapshotProof.Run();
OfflinePoolReplayProof.Run();
LegacyPoolEligibilityProof.Run();
OfflinePoolLegacyCompatibilityProof.Run();
PoolCacheIsolationProof.Run();
PoolInitOrderProof.Run();

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static Util.DeepCloneableRandom Rng(Game game) => (Util.DeepCloneableRandom)typeof(Game)
    .GetProperty("Random", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);

static long[] Draws(Util.DeepCloneableRandom random, int count = 32) =>
    Enumerable.Range(0, count).Select(_ => random.NextLong()).ToArray();

// Vectors recorded from the unchanged engine before adding state/count APIs.
var random = new Util.DeepCloneableRandom(1);
Check(Draws(random, 8).SequenceEqual(new long[] {
    5761717516557699368, -1151398839027255706, 1053790060648499002, 7492133939475596510,
    854510344979523816, 6732341938274694184, -4275295117218215325, -5368873365444457071
}), "primitive known-answer vector");
random = new Util.DeepCloneableRandom(1);
Check(Enumerable.Range(0, 10).ToArray().ChooseNElements(8, random)
    .SequenceEqual(new[] { 3, 9, 0, 4, 7, 8, 2, 1 }), "compound selection/rejection vector");
var shuffled = Enumerable.Range(0, 10).ToList();
shuffled.Shuffle(random);
Check(shuffled.SequenceEqual(new[] { 6, 3, 0, 2, 1, 8, 5, 9, 7, 4 }), "compound shuffle vector");
Check(random.NextLong() == -2076432308280158875, "compound consumption order");
Console.WriteLine("PASS known-answer vectors");

var state = random.CaptureState();
var expected = Draws(random);
random.RestoreState(state);
Check(random.CaptureState().Equals(state), "restore must include draw count and both state words");
Check(Draws(random).SequenceEqual(expected), "capture/restore future draws");
Check(random.Clone().CaptureState().Equals(random.CaptureState()), "RNG clone retains draw count");
foreach (var invalid in new[] {
    new RngState(state.State0, state.State1, state.DrawCount, 999),
    new RngState(0, 0, 0), new RngState(1, 2, -1)
})
{
    var before = random.CaptureState();
    bool rejected = false;
    try { random.RestoreState(invalid); }
    catch (ArgumentException) { rejected = true; }
    Check(rejected && random.CaptureState().Equals(before), "invalid state must fail without mutation");
}
Console.WriteLine("PASS capture/restore and validation");

random.SetSeed(1);
Check(random.DrawCount == 0, "reseed resets stream cursor");
Action[] wrappers = { () => random.NextLong(), () => random.Next(), () => random.Next(0),
    () => random.Next(-2, 3), () => random.NextLong(0), () => random.NextLong(-3, 9),
    () => random.NextDouble(), () => random.NextFloat(), () => random.NextBoolean() };
foreach (var draw in wrappers)
{
    long before = random.DrawCount;
    draw();
    Check(random.DrawCount == before + 1, "each scalar wrapper consumes exactly one primitive draw");
}
random.SetSeed(1, 2);
Check(random.DrawCount == 0, "two-state reseed resets stream cursor");
long previous = 0;
for (int i = 0; i < 100; i++)
{
    Enumerable.Range(0, 10).ToArray().ChooseNElements(8, random);
    Check(random.DrawCount >= previous + 8, "compound draws count rejection attempts monotonically");
    previous = random.DrawCount;
}
Console.WriteLine("PASS draw count");

static GameConfig Config(long? seed) => new GameConfig {
    RandomSeed = seed, StartPlayer = -1, Shuffle = true, SkipMulligan = true,
    Logging = false, History = true, FormatType = FormatType.FT_WILD,
    Player1Deck = Enumerable.Range(0, 30).Select(i => Cards.FromId(i % 2 == 0 ? "CS2_231" : "CS2_182")).ToList(),
    Player2Deck = Enumerable.Range(0, 30).Select(i => Cards.FromId(i % 2 == 0 ? "CS2_182" : "CS2_231")).ToList()
};
var game = new Game(Config(123));
game.StartGame();
Draws(Rng(game), 7);
state = Rng(game).CaptureState();
var exact = game.CloneExact(history: true);
Check(exact.Seed == game.Seed && exact.PowerHistory != null, "exact clone root seed/history");
Check(Rng(game).CaptureState().Equals(state), "cloning never consumes source RNG");
Check(Rng(exact).CaptureState().Equals(state), "exact clone copies full RNG state");
Check(Draws(Rng(exact)).SequenceEqual(Draws(Rng(game).Clone())), "exact clone next draws");
Check(Rng(game).CaptureState().Equals(state), "drawing from clone never advances source");
var first = game.CloneForSimulation(456, history: true);
var second = game.CloneForSimulation(456);
var different = game.CloneForSimulation(457);
Check(first.Seed == 456 && first.PowerHistory != null, "simulation clone root seed/history");
Check(Rng(first).CaptureState().Equals(new Util.DeepCloneableRandom(456).CaptureState()), "simulation starts explicit stream");
expected = Draws(Rng(first));
Check(expected.SequenceEqual(Draws(Rng(second))), "same simulation seed reproduces");
Check(!expected.SequenceEqual(Draws(Rng(different))), "different simulation seeds diverge");
Check(Rng(game).CaptureState().Equals(state), "simulation clone and draws leave source unchanged");
#pragma warning disable CS0618 // Prove legacy API remains compatible.
var legacyExact = game.Clone(resetRandomSeed: false);
var legacyReset = game.Clone();
#pragma warning restore CS0618
Check(Rng(legacyExact).CaptureState().Equals(state), "legacy exact clone compatibility");
Check(Rng(legacyReset).DrawCount == 0 && Rng(game).CaptureState().Equals(state), "legacy reset/source isolation");
game.SetRandomSeed(789);
Check(Draws(Rng(game)).SequenceEqual(Draws(new Util.DeepCloneableRandom(789))), "legacy SetRandomSeed stream compatibility");
Console.WriteLine("PASS exact/simulation/legacy clone isolation");

// Replay identical actions through shuffled draws until fatigue ends the game.
for (int trial = 0; trial < 3; trial++)
{
    var unseeded = new Game(Config(null));
    var replay = new Game(Config(unseeded.Seed));
    unseeded.StartGame();
    replay.StartGame();
    int turns = 0;
    while (unseeded.State != State.COMPLETE && turns++ < 200)
    {
        Check(unseeded.Process(EndTurnTask.Any(unseeded.CurrentPlayer)), "original end turn accepted");
        Check(replay.Process(EndTurnTask.Any(replay.CurrentPlayer)), "replay end turn accepted");
    }
    Check(unseeded.State == State.COMPLETE && replay.State == State.COMPLETE, "replay terminates");
    Check(Canonical.History(unseeded) == Canonical.History(replay), "unseeded root seed replays full canonical PowerHistory");
    Check(Rng(unseeded).CaptureState().Equals(Rng(replay).CaptureState()), "replay final RNG state");
}
Console.WriteLine("PASS unseeded full-history replay");

// Interleaving seeded games must not introduce thread/process RNG dependencies.
long[] seeds = { long.MinValue, long.MaxValue };
var references = seeds.Select(seed => new Game(Config(seed))).ToArray();
foreach (var reference in references) reference.StartGame();
var parallel = new Game[8];
Parallel.For(0, parallel.Length, i => {
    parallel[i] = new Game(Config(seeds[i % seeds.Length]));
    parallel[i].StartGame();
});
for (int i = 0; i < parallel.Length; i++)
{
    var reference = references[i % seeds.Length];
    Check(parallel[i].Seed == reference.Seed, "explicit signed 64-bit root seed retained");
    Check(Canonical.History(parallel[i]) == Canonical.History(reference), "parallel games keep independent seeded histories");
    Check(Rng(parallel[i]).CaptureState().Equals(Rng(reference).CaptureState()), "parallel games keep independent RNG state");
}
Console.WriteLine("PASS parallel seeded game isolation");
DeathAuraTimingProof.Run();
PoisonousDamageProof.Run();
DamageWeaponTaskProof.Run();
WeaponKeywordProof.Run();
MultiAuraCleanupProof.Run();
DrawEventProof.Run();
DiscoverRealProof.Run();
ClassHeroesProof.Run();
DeathKnightRulesProof.Run();
CorpseProof.Run();
TokenMetadataProof.Run();
CardFactsProof.RunWithoutClient();
CoreReprintsProof.Run();
Core2026Proof.Run();
ForcedAttackBudgetProof.Run();
BonusEffectsProof.Run();
EnemyBoardCopyProof.Run();
StandardSetsProof.Run();
QuestLifecycleProof.Run();
QuestOpeningProof.Run();
QuestSnapshotProof.Run();
CopyInitializationProof.Run();
PayloadRulesProof.Run();
ImbueProof.Run();
SilenceValueProof.Run();
SilenceKeywordsProof.Run();
FloopTauntProof.Run();
CloneProof.Run();
EventTraceProof.Run();
EventDispatchProof.Run();
DecisionProof.Run(args.Contains("--full"));
SnapshotProof.Run(args.Contains("--full"));
ResolutionProof.Run(args.Contains("--full"));
EffectFrameProof.Run();
IndirectSpellProof.Run();
DecisionSpellProof.Run();
TurnQueueProof.Run();
ForestLordCenariusProof.Run();
PlayerDecisionProof.Run();
EffectContextProof.Run(args.Contains("--full"));
HeroDivineShieldProof.Run();
LifestealArmorProof.Run();
SilenceAuraHealthProof.Run();
HeroImmuneTargetingProof.Run();
MultipleMinionTypesProof.Run();
ShatterRulesProof.Run();
HandProgressRulesProof.Run();
SplitEffectProof.Run("hand-progress");
SplitEffectProof.Run("weapon-battlecry");
HeraldProof.Run();
CrossClassPlayabilityProof.Run(keywordOnly: false); // ~10 s for every implemented collectible: no sample mode
ColossalProof.Run();
FabledProof.Run();
PrepareProof.Run();
OutcastProof.Run();
StateTaskListDeterminismProof.Run();
HistoryFactsProof.Run();
CanonicalPolicyChecks.Run();
ShadowTrialProof.Run();
AiFrontierProof.Run();
ClientExport.PrintSkips();
Console.WriteLine("engine-proof: PASS");
