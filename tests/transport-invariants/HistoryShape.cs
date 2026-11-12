using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using NativeHistory = PegasusGame.PowerHistory;
using NativeMeta = PegasusGame.PowerHistoryMetaData;

namespace TransportInvariants;
using Zone = SabberStoneCore.Enums.Zone;
using Game = SabberStoneCore.Model.Game;

// Contract boundaries (evidence files are the three history-shape-*.md notes supplied with this task):
// - Observe outgoing native protobufs (wire presence bits), never engine history indices. Blocks and metadata survive packet boundaries.
// - Engine hooks record each character tag mutation in per-(entity,tag) FIFO order, including non-effects.
//   The matching wire value consumes that mutation; injected final-stat corrections do not manufacture hits.
//   A hit shared by ARMOR and DAMAGE consumes metadata once. Hook context excludes payments, direct Health
//   assignments, death/transform resets and location durability. Synthetic streams use literal tag deltas.
// - Only immediate-block metadata counts. TARGET occurrences authorize one hit per victim in each execution range.
//   Explicit SUB_SPELL targets also authorize effects while that sub-spell is current (retail O:4258-4302).
//   Header-target checks apply to POWER spells/powers/locations, or other POWERs actually hitting that target;
//   Trade's self-targeted bookkeeping POWER is exempt until it actually affects that target (retail F:7325-7351).
//   TRIGGER random/explicit task-selected hits require per-invocation TARGET ranges (native 4.5; retail O:2445).
//   Fixed-set IncludeTask/AoE and event-only header targets do not (Undying Disciple F:7704).
//   Record-only task hooks establish selection independently of emitted TARGET; Poisonous activations
//   come from resolved damage and pre-hit state, never from a keyworded wire header.
// - A source may be its enchantment host/creator, or the equipped weapon of an attacking hero; no controller-wide
//   equivalence. ATTACK permits either combatant. Fatigue has its own source/block rule.
// - Public identity is historical wire knowledge; HIDE clears it. Snapshots can perform zone moves too.
//   Private bot choice provenance includes SETASIDE offers and CHANGE identities; actual secrets stay private,
//   QUEST=1 in SECRET is public. Joust/REVEALED explicitly publish identity. Other private snapshots must resolve
//   through a same-identity public move/HIDE by their enclosing operation's end (or Finish if no block), across packets.
//   Ordinary enchantment names do not identify the hidden card they modify and remain excluded.
// - MILL_ORDER requires the named SHOW itself to move DECK -> GRAVEYARD (retail 34104:30153-30160).
//   A burn/discard SHOW into GRAVEYARD must not be followed by a redundant move or HIDE of that actor.
//   Automatic PLAY retirement is not a mill: its same-identity DECK SHOW may precede its own PLAY block.
// - Buckets are (rule, immediate block source card), falling back to affected card then <game>.
//   Engine identities are diagnostic only: they never satisfy wire reveal/privacy checks.
// - SPLIT_EFFECT_BLOCK: one finding per effect block/variant, allowing overlap, raised when the block closes.
//   Root continuations require adjacent blocks in the same action (Replay/Step), never separate player actions.
//   A sibling variant names the earlier sibling's type, including mixed POWER/TRIGGER pairs.
//   Actor keywords/negative effect indices never participate as activations, but they are transparent wrappers:
//   nesting compares with the nearest enclosing non-wrapper block, and DAMAGE directly inside a same-source
//   wrapper (enclosed by, or following as sibling/root, its activation) is a split hit unless it is Lifesteal
//   healing converted onto its own hero. Findings wait for the root block to close; a peer's extra metadata is
//   appended as peer=. Effect selectors, TRIGGER event targets and HAND/DECK-to-SETASIDE cast markers
//   separate invocations. Record-only hooks also distinguish complete registered effect invocations;
//   arbitrary queued subtasks remain candidates, and inline hit blocks retain their invocation identity.
//   Detail = "<variant> under=<immediate parent> header=same|differs
//   meta=<TARGET/DAMAGE/HEALING in the block>" ("keyword-hit-in-X block=<wrapper> under=... meta=..." for wrapper hits);
//   the baseline accepts reviewed details, not whole cards.
// - Baseline is a set of reviewed defect/accepted buckets, not occurrence ceilings (seeds vary). Stale
//   entries are informational, reported only for the complete corpus; all runs reject new error buckets.
//   SPLIT_EFFECT_BLOCK entries list their reviewed details ("shapes"): an unlisted shape is new, an unseen one stale.
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<HistoryShapeCondition>))]
internal enum HistoryShapeCondition
{
    FiniteInfinitySaturation, TorethShieldCorrelation, RetainedHandTransform,
    AutomaticDrawCast, ModularSandfury, ModularCorpseFlower, ModularBattlecryDeathrattle, ExhaustionDeathrattle, HealthLossReplacement, RewindRestoration
}
internal sealed record HistoryShapeFinding(string Rule, string Severity, string Card, string BlockCard,
    string ActualCard, string VictimCard, string Replay, int Step, int Packet, string Detail, HistoryShapeCondition[] Conditions = null);

internal sealed class HistoryShape
{
    private static readonly HashSet<string> SecretCards = Cards.All.Where(c => c[GameTag.SECRET] == 1)
        .Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
    internal static readonly HashSet<string> InfoRules = new(StringComparer.Ordinal) {
        "SHIELD_META", "HIT_TARGET", "ATTACK_RESET", "EFFECT_INDEX"
    };
    internal static readonly string[] Rules = { "BALANCE", "DAMAGE_META", "DAMAGE_META_AMOUNT", "SHIELD_META",
        "HEALING_META", "TARGET_META", "TARGET_DATA", "HIT_TARGET", "FOREIGN_SOURCE", "ATTACK_SHAPE", "ATTACK_PROPOSED", "ATTACK_RESET",
        "TRIGGER_KEYWORD", "TRIGGER_HIT_TARGET", "POWER_HIT_TARGET", "POISONOUS_KEYWORD", "HAND_BUFF_FX", "EFFECT_INDEX", "FATIGUE_BLOCK", "ANON_PUBLIC", "MILL_ORDER", "META_REF", "PRIVACY", "SPLIT_EFFECT_BLOCK", "FULL_ENTITY_ID" };

    internal sealed class Hit
    {
        internal int Source, Victim, Amount, ReplacedHealth;
        internal string SourceCard;
        internal bool Heal, Shield, Fatigue, Lifesteal, Selected, Observed, SaturatedDamage, CounterSaturated;
        internal readonly HashSet<int> Related = new();
    }
    internal sealed class GroundBlock
    {
        internal int Type, Source, Target;
        internal int Invocation;
        internal PowerActivation Activation;
        internal int DeathEventId;
        internal bool Cancelled;
    }
    internal sealed class Mutation(int value, Hit hit)
    {
        internal readonly int Value = value;
        internal Hit Hit = hit;
    }
    private sealed class Block(PowerHistoryStart start, GroundBlock ground, string card, int sourceType, int activation)
    {
        internal readonly PowerHistoryStart Start = start;
        internal readonly GroundBlock Ground = ground;
        internal readonly string Card = card;
        internal readonly int SourceType = sourceType;
        internal readonly int Activation = activation;
        internal readonly List<NativeMeta> Pending = new();
        internal readonly Dictionary<NativeMeta, bool> Targeted = new();
        internal readonly List<int> Targets = new();
        internal readonly HashSet<int> PoisonTargets = new();
        internal readonly HashSet<int> UntargetedHandBuffs = new();
        internal readonly Dictionary<int, int> ShieldZeros = new();
        internal readonly HashSet<int> ShieldCleared = new();
        internal bool Deathrattle;
        internal Block DrawWrapper;
        internal readonly Stack<PowerHistorySubSpellStart> SubSpells = new();
        internal readonly List<(int Entity, int Tag, int Value)> AttackTags = new();
        internal readonly List<Block> EffectChildren = new();
        internal readonly List<(string Variant, bool SameHeader, Block[] Peers)> SplitVariants = new();
        internal Block Wrapped; // Same-source activation enclosing or preceding a keyword/attribution wrapper.
        internal string WrappedVariant, Under = "root";
        internal int HitMeta; // TARGET=1, DAMAGE=2, HEALING=4 directly in this block.
        internal bool HitDamage; // DAMAGE other than Lifesteal healing converted onto its own hero.
        internal bool TargetRequired, TargetChecked, HasTargets, RangeHasEffects, Hidden, HeaderSubSpellTarget, PoisonDestroyed;
        internal int FatigueEntity, Attacker = ground?.Source ?? start.Source, Defender = ground?.Target ?? start.Target;
        internal int GameStep; // Game entity STEP when the block opened.
    }
    private readonly Game game;
    private readonly Dictionary<int, ClientEntity> entities = new();
    private readonly HashSet<int> known = new();
    private readonly HashSet<int> privateChoices = new();
    private readonly HashSet<int> burnDestinations = new();
    private readonly HashSet<int> deckCastShows = new();
    private readonly Dictionary<int, string> publicCards = new();
    private readonly List<(int Entity, string Card, Block Scope)> privateShows = new();
    internal void RecordPrivateChoice(int entity) => privateChoices.Add(entity);
    private readonly Dictionary<(int Entity, int Tag), Queue<Mutation>> mutations = new();
    private readonly Queue<GroundBlock> groundBlocks = new();
    private readonly List<Hit> hookHits = new();
    private readonly List<(int Source, int Victim)> poisonVictims = new();
    internal void RecordPoison(int source, int victim) => poisonVictims.Add((source, victim));
    internal bool RequireCorrelation;
    internal void RegisterHit(Hit hit) => hookHits.Add(hit);
    private readonly Stack<Block> blocks = new();
    private readonly Dictionary<int, int> castActivations = new();
    private readonly Dictionary<int, (int Attack, int Health, Block Scope)> retainedHandLayers = new();
    private readonly HashSet<int> exhaustedLocations = new();
    internal readonly List<HistoryShapeFinding> Findings = new();
    internal string Replay = "proof";
    internal int Step;
    private int packetNumber;
    private bool finished;
    private Block previousRoot;
    private int previousRootStep;
    private string previousRootReplay;
    private readonly Func<int, int> wireId, engineId;
    internal HistoryShape(Game game = null, Func<int, int> wireId = null, Func<int, int> engineId = null)
    {
        this.game = game; this.wireId = wireId ?? (id => id); this.engineId = engineId ?? (id => id);
        if (game != null) HistoryShapeHooks.Attach(game, this);
    }
    private Block Current => blocks.Count == 0 ? null : blocks.Peek();
    private ClientEntity Entity(int id)
    {
        if (!entities.TryGetValue(id, out var entity)) entities.Add(id, entity = new());
        return entity;
    }
    private string Card(int id)
    {
        if (id == 0) return "";
        if (entities.TryGetValue(id, out var entity) && !string.IsNullOrEmpty(entity.CardId)) return entity.CardId;
        int original = engineId(id);
        return original > 0 && game?.IdEntityDic.ContainsKey(original) == true ? game.IdEntityDic[original].Card?.Id ?? "" : "";
    }
    private int Type(int id)
    {
        var entity = Entity(id);
        int type = entity.Get(GameTag.CARDTYPE);
        if (type != 0) return type;
        return string.IsNullOrEmpty(entity.CardId) ? 0 : (int)Cards.FromId(entity.CardId).Type;
    }
    private void Flag(string rule, int victim = 0, Hit hit = null, Block block = null, string detail = "", string victimCard = null, HistoryShapeCondition[] conditions = null)
    {
        block ??= Current;
        string source = block?.Card ?? "", affected = victimCard ?? Card(victim);
        Findings.Add(new(rule, InfoRules.Contains(rule) ? "info" : "error",
            !string.IsNullOrEmpty(source) ? source : !string.IsNullOrEmpty(affected) ? affected : "<game>",
            source, hit?.SourceCard ?? "", affected, Replay, Step, packetNumber, detail, conditions));
    }
    internal Mutation RecordMutation(int entity, GameTag tag, int value, Hit hit = null)
    {
        var key = (entity, (int)tag);
        if (!mutations.TryGetValue(key, out var queue)) mutations.Add(key, queue = new());
        var mutation = new Mutation(value, hit); queue.Enqueue(mutation); return mutation;
    }
    internal void RecordBlock(GroundBlock block) => groundBlocks.Enqueue(block);
    private int nextInvocation;
    internal int NextInvocation() => ++nextInvocation;
    private Hit GroundHit(int entity, int tag, int value, out bool matched)
    {
        matched = false;
        if (!mutations.TryGetValue((entity, tag), out var queue) || queue.Count == 0 || queue.Peek().Value != value) return null;
        matched = true;
        return queue.Dequeue().Hit;
    }
    private GroundBlock GroundStart(PowerHistoryStart start)
    {
        if (groundBlocks.Count == 0) return null;
        // Fixtures can discard unsent engine history; match block signatures in emission order.
        while (groundBlocks.Count > 0)
        {
            var next = groundBlocks.Dequeue();
            // Route/type/target are the properties under test; provenance must survive malformed headers.
            if (next.Source == start.Source) return next;
        }
        return null;
    }
    private static bool EffectBlock(Block b) => b != null && (int)b.Start.Type is 1 or 3 or 5 or 8;
    private static bool SplitCandidate(Block b) => b != null && (int)b.Start.Type is 3 or 5
        && !(b.Start.HasEffectIndex && b.Start.EffectIndex == -1)
        // Deathrattle bodies can split; actor keywords (Lifesteal/Poisonous, etc.) are separate effects.
        && !((int)b.Start.Type == 5 && b.Start.HasTriggerKeyword && b.Start.TriggerKeyword != 0
            && b.Start.TriggerKeyword != (int)GameTag.DEATHRATTLE && b.Start.TriggerKeyword != (int)GameTag.TRIGGER_VISUAL);
    // Lifesteal/Poisonous keywords and EffectIndex=-1 attribution blocks wrap effects without being activations;
    // other keyworded TRIGGERs remain distinct activations.
    private static bool Wrapper(Block b) => b != null && (int)b.Start.Type is 3 or 5 && (b.Start.HasEffectIndex && b.Start.EffectIndex == -1
        || (int)b.Start.Type == 5 && b.Start.TriggerKeyword is (int)GameTag.LIFESTEAL or (int)GameTag.POISONOUS);
    private Block Enclosing()
    {
        foreach (var b in blocks) if (!Wrapper(b)) return b;
        return null;
    }
    private static bool SameHeader(Block earlier, Block next)
    {
        var a = earlier.Start; var b = next.Start;
        return a.Type == b.Type && a.Source == b.Source && a.Target == b.Target && a.HasEffectIndex == b.HasEffectIndex
            && a.EffectIndex == b.EffectIndex && (a.EffectCardId ?? "") == (b.EffectCardId ?? "") && a.TriggerKeyword == b.TriggerKeyword;
    }
    private static string Label(Block b)
    {
        if (b == null) return "root";
        int type = (int)b.Start.Type;
        string name = type switch { 1 => "ATTACK", 3 => "POWER", 5 => "TRIGGER", 6 => "DEATHS", 7 => "PLAY", 8 => "FATIGUE", _ => "BLOCK" + type };
        // TRIGGER_VISUAL only selects the actor bauble of the activation itself; buckets stay keyed as ordinary triggers.
        if (b.Start.TriggerKeyword != 0 && b.Start.TriggerKeyword != (int)GameTag.TRIGGER_VISUAL) name += "/kw" + b.Start.TriggerKeyword;
        return b.Start.HasEffectIndex && b.Start.EffectIndex < 0 ? name + "/ei" + b.Start.EffectIndex : name;
    }
    private static string MetaName(int bits) => string.Join('+', new[] { (1, "TARGET"), (2, "DAMAGE"), (4, "HEALING") }
        .Where(m => (bits & m.Item1) != 0).Select(m => m.Item2));
    private static string Kind(Block b) => (int)b.Start.Type == 3 ? "POWER" : "TRIGGER";
    private static bool SameSource(Block activation, Block wrapper) => SplitCandidate(activation)
        && activation.Start.Source == wrapper.Start.Source && activation.Activation == wrapper.Activation;
    private readonly List<Block> pendingSplits = new();
    // Flag once the whole root block has closed, so every participant's metadata is complete.
    private void FlushSplits()
    {
        foreach (var b in pendingSplits)
        {
            if (b.HitMeta != 0)
                foreach (var (variant, same, peers) in b.SplitVariants)
                {
                    // A peer's extra hit metadata (DAMAGE beside a TARGET-only continuation) is part of the shape.
                    int peer = peers.Aggregate(0, (bits, p) => bits | p.HitMeta);
                    Flag("SPLIT_EFFECT_BLOCK", block: b, detail: $"{variant} under={b.Under} header={(same ? "same" : "differs")} meta={MetaName(b.HitMeta)}"
                        + ((peer & ~b.HitMeta) != 0 ? $" peer={MetaName(peer)}" : ""), conditions: SplitConditions(b, variant));
                }
            // Lifesteal heals and Poisonous TARGET are the wrapper's own effect; DAMAGE is the activation's hit.
            if (b.Wrapped != null && b.HitDamage)
                Flag("SPLIT_EFFECT_BLOCK", block: b, detail: $"{b.WrappedVariant} block={Label(b)} under={b.Under} meta={MetaName(b.HitMeta)}");
        }
        pendingSplits.Clear();
    }
    private bool ActiveCard(string card, int owner = 0) => entities.Values.Any(e => e.CardId == card
        && e.Get(GameTag.ZONE) is (int)Zone.PLAY or (int)Zone.SECRET
        && e.Get(GameTag.SILENCED) == 0 && e.Get(GameTag.UNTOUCHABLE) == 0
        && (owner == 0 || e.Get(GameTag.CONTROLLER) == owner));
    private bool Parts(Block b, string first, string second)
    {
        var entity = Entity(b.Start.Source);
        var parts = new[] { entity.Get(GameTag.MODULAR_ENTITY_PART_1), entity.Get(GameTag.MODULAR_ENTITY_PART_2) };
        return parts.Contains(Cards.FromId(first).AssetId) && parts.Contains(Cards.FromId(second).AssetId);
    }
    private HistoryShapeCondition[] SplitConditions(Block b, string variant)
    {
        var result = new List<HistoryShapeCondition>();
        if (variant == "nested-in-TRIGGER" && b.DrawWrapper != null
            && (Entity(b.Start.Source).Get(GameTag.CASTSWHENDRAWN) == 1
                || !string.IsNullOrEmpty(b.Card) && Cards.FromId(b.Card)[GameTag.CASTSWHENDRAWN] == 1)
            && b.DrawWrapper.EffectChildren.Count(e => e.Start.Source == b.Start.Source && (int)e.Start.Type == 3) == 1)
            result.Add(HistoryShapeCondition.AutomaticDrawCast);
        // Sandfury doubles the Earthen Drake component's end-of-turn damage. Only reviewed partners without a damage
        // trigger of their own qualify, and only during MAIN_END, so another damaging partner's split stays unaccepted.
        if (variant == "sibling-TRIGGER" && b.GameStep == (int)SabberStoneCore.Enums.Step.MAIN_END
            && (Parts(b, "CATA_999", "CORE_EX1_012") || Parts(b, "CATA_999", "DINO_435"))
            && ActiveCard("CATA_480", Entity(b.Start.Source).Get(GameTag.CONTROLLER)))
            result.Add(HistoryShapeCondition.ModularSandfury);
        if (variant == "nested-in-POWER" && Parts(b, "TIME_713", "EDR_815")
            && Card(b.Start.Target) == "TIME_713t"
            && Entity(b.Start.Target).Get(GameTag.CONTROLLER) != Entity(b.Start.Source).Get(GameTag.CONTROLLER))
            result.Add(HistoryShapeCondition.ModularCorpseFlower);
        var entity = Entity(b.Start.Source);
        if (variant == "sibling-POWER" && b.Deathrattle && Parts(b, "TIME_004", "RLK_708")
            && entity.Get(GameTag.ZONE) == (int)Zone.GRAVEYARD)
            result.Add(HistoryShapeCondition.ModularBattlecryDeathrattle);
        if (b.Deathrattle && exhaustedLocations.Contains(b.Start.Source)
            && entity.Get(GameTag.ZONE) == (int)Zone.GRAVEYARD)
            result.Add(HistoryShapeCondition.ExhaustionDeathrattle);
        return result.ToArray();
    }
    private static bool SameActivation(Block earlier, Block next)
    {
        if (!SplitCandidate(earlier) || !SplitCandidate(next) || earlier.Start.Source != next.Start.Source
            || earlier.Activation != next.Activation) return false;
        if (earlier.Ground?.Invocation > 0 && next.Ground?.Invocation > 0
            && earlier.Ground.Invocation != next.Ground.Invocation) return false;
        var a = earlier.Start; var b = next.Start;
        if (a.HasEffectIndex && b.HasEffectIndex && a.EffectIndex != b.EffectIndex
            || (a.EffectCardId ?? "") != (b.EffectCardId ?? "")) return false;
        // TRIGGER targets are invocation inputs (event victim/granting enchantment), not hit recipients.
        // A queued POWER continuation may omit the enclosing Deathrattle keyword and choose a new hit target.
        return (int)a.Type != 5 || (int)b.Type != 5 || a.Target == b.Target && a.TriggerKeyword == b.TriggerKeyword;
    }
    private static bool IsPublic(int zone) => zone is (int)Zone.PLAY or (int)Zone.GRAVEYARD or (int)Zone.REMOVEDFROMGAME;
    private static bool IsHidden(int zone) => zone is (int)Zone.DECK or (int)Zone.HAND;
    // Active Sidequests/Auras/Objectives are public in SECRET; stale tags never reveal a real Secret.
    private bool PublicSecret(int id) => Entity(id).Get(GameTag.QUEST) == 1
        || Entity(id).Get(GameTag.CARDTYPE) == (int)CardType.SPELL
            && (Entity(id).Get((GameTag)1192) == 1 || Entity(id).Get((GameTag)3429) == 1 || Entity(id).Get((GameTag)2311) == 1)
            && Entity(id).Get(GameTag.SECRET) != 1 && !SecretCards.Contains(Entity(id).CardId);
    private bool Public(int id, int zone)
    {
        var entity = Entity(id);
        bool quest = entity.Get(GameTag.QUEST) == 1;
        bool secret = entity.Get(GameTag.SECRET) == 1 || SecretCards.Contains(entity.CardId);
        return zone == (int)Zone.SECRET ? PublicSecret(id) : IsPublic(zone)
            && (zone != (int)Zone.PLAY || quest || !secret);
    }
    private bool Private(int id)
    {
        var entity = Entity(id); int zone = entity.Get(GameTag.ZONE);
        if (entity.Get(GameTag.CONTROLLER) != 2) return false;
        if (zone == (int)Zone.SETASIDE && privateChoices.Contains(engineId(id))) return true;
        return entity.Get(GameTag.CARDTYPE) != (int)CardType.ENCHANTMENT
            && (IsHidden(zone) || zone == (int)Zone.SECRET && !PublicSecret(id));
    }
    private void ResolvePrivateShows(Block scope)
    {
        foreach (var show in privateShows.Where(s => s.Scope == scope).ToArray())
        {
            Flag("PRIVACY", show.Entity, block: scope, victimCard: show.Card,
                detail: "named bot private snapshot without same-identity public move/HIDE before operation end");
            privateShows.Remove(show);
        }
    }
    private static bool EffectTag(int tag) => tag is (int)GameTag.ATK or (int)GameTag.HEALTH or (int)GameTag.DAMAGE
        or (int)GameTag.ARMOR or (int)GameTag.DIVINE_SHIELD or (int)GameTag.ZONE or (int)GameTag.TAUNT or (int)GameTag.SILENCED
        or (int)GameTag.FROZEN or (int)GameTag.STEALTH or (int)GameTag.WINDFURY or (int)GameTag.CHARGE or (int)GameTag.LIFESTEAL
        or (int)GameTag.POISONOUS or (int)GameTag.RUSH or (int)GameTag.COST;
    private static bool TargetSource(int type) => type is (int)CardType.SPELL or (int)CardType.HERO_POWER or (int)CardType.LOCATION;

    private static bool SubSpellTarget(Block b, int victim) => b.SubSpells.TryPeek(out var sub)
        && !string.IsNullOrWhiteSpace(sub.SpellPrefabGuid) && sub.TargetEntityIds.Contains(victim);
    private static bool HasTarget(Block b, int victim) => b.Targets.Contains(victim) || SubSpellTarget(b, victim);

    // TARGET_META: retail O:4258-4302, O:4403-4417, F:1159-1176; native-client sections 1.6, 2.2 and 4.3.
    private void TargetEffect(int victim, Block b, bool hit = false)
    {
        if (b == null || (int)b.Start.Type != 3 || (!hit && b.TargetChecked)
            || b.Start.Target != victim && !(hit && b.HasTargets)) return;
        if (b.Start.Target == victim) { b.TargetRequired = true; b.TargetChecked = true; }
        if (!HasTarget(b, victim)) Flag("TARGET_META", victim, block: b, detail: "TARGET or explicit sub-spell target absent before effect range");
    }
    private NativeMeta Consume(Block b, HistoryMeta.Type type, int victim)
    {
        if (b == null) return null;
        int index = b.Pending.FindIndex(m => m.Type == type && m.Info.Count == 1 && m.Info[0] == victim);
        if (index < 0) return null;
        var meta = b.Pending[index]; b.Pending.RemoveAt(index); return meta;
    }
    private void HitTargets(int victim, Block b, bool metadata = false)
    {
        TargetEffect(victim, b, hit: true);
        if (metadata && b != null && (int)b.Start.Type == 3 && b.SourceType is (int)CardType.SPELL or (int)CardType.HERO_POWER
            && !HasTarget(b, victim)) Flag("HIT_TARGET", victim);
        if (b != null) { b.Targets.RemoveAll(id => id == victim); b.RangeHasEffects = true; }
    }
    private void Resolve(Hit hit)
    {
        if (hit.Observed) return;
        hit.Observed = true;
        var b = Current;
        var meta = Consume(b, hit.Heal ? HistoryMeta.Type.HEALING : HistoryMeta.Type.DAMAGE, hit.Victim);
        var conditions = new List<HistoryShapeCondition>();
        bool saturated = hit.CounterSaturated || Entity(hit.Victim).Get(GameTag.DAMAGE) > 0
            && mutations.TryGetValue((hit.Victim, (int)GameTag.DAMAGE), out var future)
            && future.Any(m => m.Value == int.MaxValue && ReferenceEquals(m.Hit, hit));
        if (saturated && meta?.Data == int.MaxValue && hit.Amount > 0 && hit.Amount < int.MaxValue)
            conditions.Add(HistoryShapeCondition.FiniteInfinitySaturation);
        if (b != null && b.ShieldZeros.GetValueOrDefault(hit.Victim) >= 2 && b.ShieldCleared.Contains(hit.Victim)
            && meta?.Data == 0 && hit.Amount > 0 && b.Pending.Any(m => m.Type == HistoryMeta.Type.DAMAGE
                && m.Data == hit.Amount && m.Info.SequenceEqual(new[] { hit.Victim }) && b.Targeted.GetValueOrDefault(m)))
            conditions.Add(HistoryShapeCondition.TorethShieldCorrelation);
        if (hit.ReplacedHealth > 0 && meta?.Data == (long)hit.Amount + hit.ReplacedHealth)
            conditions.Add(HistoryShapeCondition.HealthLossReplacement);
        bool targeted = meta != null ? b.Targeted.GetValueOrDefault(meta) : b != null && HasTarget(b, hit.Victim);
        // TRIGGER_HIT_TARGET: selected recipients and repeated ranges, not event headers or fixed-set AoE.
        if (b != null && (int)b.Start.Type == 5 && (hit.Selected || game == null && b.HasTargets) && !targeted)
            Flag("TRIGGER_HIT_TARGET", hit.Victim, hit, detail: $"resolved selected TRIGGER hit lacks preceding TARGET occurrence; selected={hit.Selected}; hasTargets={b.HasTargets}; victim={hit.Victim}; meta={meta?.Data}; pending={string.Join(',', b.Pending.Select(m => m.Type + ":" + m.Data))}", conditions: conditions.ToArray());
        // Spell.AddMultiplePowerTargets can disable tag fallback. Retail random missiles provide a TARGET
        // for each selected hit, even with header Target=0; hook selection survives stripping all metadata.
        if (!hit.Heal && hit.Selected && b != null && (int)b.Start.Type == 3
            && b.SourceType is (int)CardType.SPELL or (int)CardType.HERO_POWER && !targeted)
            Flag("POWER_HIT_TARGET", hit.Victim, hit, detail: "selected POWER hit lacks preceding TARGET occurrence or explicit sub-spell target", conditions: conditions.ToArray());
        if (meta != null) b.Targeted.Remove(meta);
        if (meta == null) HitTargets(hit.Victim, b);
        // DAMAGE_META/AMOUNT/SHIELD_META: retail 113/113 damage preludes, shield F:7682; armor E:3188-3193.
        // HEALING_META: retail all 16 actual-heal amounts, O:2466-2468; zero heal O:3085-3106 omitted.
        if (hit.Shield)
        {
            if (meta?.Data != 0) Flag("SHIELD_META", hit.Victim, hit, detail: "shield pop needs DAMAGE Data=0");
        }
        else if (hit.Heal)
        {
            if (meta == null || meta.Data != hit.Amount)
                Flag("HEALING_META", hit.Victim, hit, detail: $"actual heal={hit.Amount}; meta={meta?.Data.ToString() ?? "missing"}");
        }
        else
        {
            if (!EffectBlock(b) || meta == null) Flag("DAMAGE_META", hit.Victim, hit, detail: $"resolved damage={hit.Amount}");
            else if (meta.Data != hit.Amount) Flag("DAMAGE_META_AMOUNT", hit.Victim, hit, detail: $"resolved damage={hit.Amount}; meta={meta.Data}", conditions: conditions.ToArray());
        }
        // FOREIGN_SOURCE: native-client section 4.7; retail independent source TRIGGER O:2444-2460.
        bool enchantmentHost = b != null && entities.TryGetValue(b.Start.Source, out var owner)
            && owner.Get(GameTag.CARDTYPE) == (int)CardType.ENCHANTMENT
            && (owner.Get(GameTag.ATTACHED) == hit.Source || owner.Get(GameTag.CREATOR) == hit.Source);
        if (!hit.Heal && !hit.Fatigue && b != null && !enchantmentHost && !hit.Related.Contains(b.Start.Source)
            && hit.Source != b.Start.Source && !((int)b.Start.Type == 1 && (hit.Source == b.Attacker || hit.Source == b.Defender)))
            Flag("FOREIGN_SOURCE", hit.Victim, hit, detail: $"block source={b.Card}; actual source={hit.SourceCard}");
        // TRIGGER_KEYWORD: native-client sections 5.3-5.4; GameTag.DEATHRATTLE=217, LIFESTEAL=685.
        if (hit.Lifesteal && (b == null || (int)b.Start.Type != 5 || !b.Start.HasTriggerKeyword
            || b.Start.TriggerKeyword != (int)GameTag.LIFESTEAL))
            Flag("TRIGGER_KEYWORD", hit.Victim, hit, detail: "Lifesteal healing needs TRIGGER keyword 685");
        // FATIGUE_BLOCK: native-client section 7.2; hero source + immediate FATIGUE tag + DAMAGE meta (tag carrier may be controller).
        if (hit.Fatigue && (b == null || (int)b.Start.Type != 8 || b.Start.Source != hit.Victim || b.FatigueEntity == 0
            || meta == null || meta.Data != hit.Amount))
            Flag("FATIGUE_BLOCK", hit.Victim, hit, detail: "fatigue needs hero FATIGUE block/tag/DAMAGE");
    }
    private void Close(Block b)
    {
        ResolvePrivateShows(b);
        if (b.Start.HasTriggerKeyword && b.Start.TriggerKeyword == 363 && !b.PoisonDestroyed)
            Flag("POISONOUS_KEYWORD", block: b, detail: "Poisonous block closed without exactly one expected victim destroy tag");
        // BALANCE: retail all 977 matched blocks; native-client section 7.3 targeted reveal requires HIDE.
        if ((int)b.Start.Type == 10 && b.Start.Target != 0 && !b.Hidden)
            Flag("BALANCE", b.Start.Target, block: b, detail: "targeted REVEAL_CARD lacks matching HIDE");
        if (b.TargetRequired && !b.TargetChecked && !b.Targets.Contains(b.Start.Target) && !b.HeaderSubSpellTarget)
            Flag("TARGET_META", b.Start.Target, block: b, detail: "targeted POWER lacks TARGET");
        if ((int)b.Start.Type != 1 || b.Ground?.Cancelled == true) return;
        int attacker = b.Attacker, defender = b.Defender;
        bool Has(int entity, GameTag tag, int value) => b.AttackTags.Contains((entity, (int)tag, value));
        bool Proposed(GameTag tag, int value) => b.AttackTags.LastOrDefault(t => t.Tag == (int)tag && t.Value != 0).Value == value
            && b.AttackTags.Any(t => t.Tag == (int)tag) && b.AttackTags.Last(t => t.Tag == (int)tag).Value == 0;
        // ATTACK_SHAPE/RESET: retail all 58 ATTACKs; native-client sections 3.1-3.3. Engine-cancelled exempt.
        if (!Has(attacker, GameTag.ATTACKING, 1) || !Has(defender, GameTag.DEFENDING, 1))
            Flag("ATTACK_SHAPE", defender, block: b, detail: "missing actual participants' attacking/defending flags");
        if (!Proposed(GameTag.PROPOSED_ATTACKER, attacker) || !Proposed(GameTag.PROPOSED_DEFENDER, defender))
            Flag("ATTACK_PROPOSED", defender, block: b, detail: "missing/stale proposals or proposal resets");
        if (!Has(attacker, GameTag.ATTACKING, 0) || !Has(defender, GameTag.DEFENDING, 0))
            Flag("ATTACK_RESET", defender, block: b, detail: "missing attack participant resets");
    }
    internal void Observe(NativeHistory packet)
    {
        // Remap hook provenance after the transport allocates return aliases. Never rewrite wire evidence.
        foreach (var hit in hookHits)
        {
            hit.Source = wireId(hit.Source); hit.Victim = wireId(hit.Victim);
            var related = hit.Related.Select(wireId).ToArray();
            hit.Related.Clear(); hit.Related.UnionWith(related);
        }
        foreach (var block in groundBlocks) { block.Source = wireId(block.Source); block.Target = wireId(block.Target); }
        var recorded = mutations.ToArray(); mutations.Clear();
        foreach (var pair in recorded) mutations.Add((wireId(pair.Key.Entity), pair.Key.Tag), pair.Value);
        for (int i = 0; i < poisonVictims.Count; i++)
            poisonVictims[i] = (wireId(poisonVictims[i].Source), wireId(poisonVictims[i].Victim));

        packetNumber++;
        // PowerProcessor.BuildTaskList runs FULL's DoRealTimeTask before queued metadata effects execute.
        var packetFulls = packet.List.Where(e => e.HasFullEntity).Select(e => e.FullEntity.Entity).ToHashSet();
        // A Rewind reset packet first retires client entities absent from the restored game (73f2bc07): ZONE=REMOVEDFROMGAME
        // only queues their removal before RESET_GAME, so a hidden one needs no identity (the client never shows it).
        bool rewindRetirement = packet.List.Any(e => e.HasResetGame && e.ResetGame.SourceIsRewind);
        void Move(int id, int before, int after, bool destinationShow = false, bool zoneTag = false)
        {
            var entity = Entity(id);
            // A SETASIDE offer stays private until picked into a hand or play; retiring it (GRAVEYARD, REMOVEDFROMGAME)
            // never clears it. An offered card outside SETASIDE (a deck or hand choice) is an ordinary card once it moves,
            // so a later stop in SETASIDE (a drawn Casts When Drawn card resolving) is not an offer.
            bool offer = before == (int)Zone.SETASIDE && privateChoices.Contains(engineId(id));
            bool picked = offer && after is (int)Zone.HAND or (int)Zone.PLAY;
            if (before != after && before != (int)Zone.INVALID && (!offer || picked)) privateChoices.Remove(engineId(id));
            // A drawn/held card moves to SETASIDE before its cast POWER; opponent card type can stay private.
            if (IsHidden(before) && after == (int)Zone.SETASIDE)
                castActivations[id] = castActivations.GetValueOrDefault(id) + 1;
            // A retired offer named on the way out leaked; only a pick into play publishes it.
            if (Public(id, after) && (!offer || picked))
            {
                privateShows.RemoveAll(s => s.Entity == id && s.Card == entity.CardId);
                if (!string.IsNullOrEmpty(entity.CardId)) publicCards[id] = entity.CardId;
            }
            bool publicMove = IsHidden(before) && Public(id, after) && entity.Get(GameTag.CARDTYPE) != (int)CardType.ENCHANTMENT;
            // ANON_PUBLIC: native-client section 6.1; native actors cannot infer card IDs from zone tags.
            if (publicMove && string.IsNullOrEmpty(entity.CardId) && !(rewindRetirement && after == (int)Zone.REMOVEDFROMGAME))
                Flag("ANON_PUBLIC", id, detail: "hidden actor entered public zone without identity");
            // SHOW carries the destination; the bracketed DECK in retail logs is pre-task context.
            bool castRetirement = deckCastShows.Contains(id) && (int?)Current?.Start.Type == 7 && Current.Start.Source == id;
            if (before == (int)Zone.DECK && after == (int)Zone.GRAVEYARD && !destinationShow && !castRetirement
                && entity.Get(GameTag.CARDTYPE) != (int)CardType.ENCHANTMENT)
                Flag("MILL_ORDER", id, detail: "mill lacks named destination SHOW snapshot ZONE=GRAVEYARD");
            if (zoneTag && burnDestinations.Contains(id) && after == (int)Zone.GRAVEYARD)
                Flag("MILL_ORDER", id, detail: "redundant GRAVEYARD move after burn/discard SHOW");
            if (before != after) burnDestinations.Remove(id);
            if (before != after) deckCastShows.Remove(id);
            if (destinationShow && before is (int)Zone.DECK or (int)Zone.HAND) burnDestinations.Add(id);
        }
        foreach (var entry in packet.List)
        {
            if (blocks.Count == 0 && !entry.HasPowerStart && !entry.HasPowerEnd) previousRoot = null;
            if (entry.HasCreateGame)
            {
                known.Add(entry.CreateGame.GameEntity.Id);
                foreach (var player in entry.CreateGame.Players) known.Add(player.Entity.Id);
                foreach (var tag in entry.CreateGame.GameEntity.Tags) Entity(entry.CreateGame.GameEntity.Id).Tags[tag.Name] = tag.Value;
                foreach (var player in entry.CreateGame.Players)
                    foreach (var tag in player.Entity.Tags) Entity(player.Entity.Id).Tags[tag.Name] = tag.Value;
            }
            else if (entry.HasPowerStart)
            {
                var start = entry.PowerStart;
                // A secret's own activation publicly identifies it; PLAY alone must not do so.
                if ((int)start.Type is 3 or 5 && Entity(start.Source).Get(GameTag.ZONE) == (int)Zone.SECRET
                    && Entity(start.Source).Get(GameTag.QUEST) != 1)
                {
                    privateShows.RemoveAll(s => s.Entity == start.Source && s.Card == Entity(start.Source).CardId);
                    if (!string.IsNullOrEmpty(Entity(start.Source).CardId)) publicCards[start.Source] = Entity(start.Source).CardId;
                }
                var b = new Block(start, GroundStart(start), Card(start.Source), Type(start.Source), castActivations.GetValueOrDefault(start.Source)) {
                    GameStep = Entity(1).Get(GameTag.STEP),
                    TargetRequired = (int)start.Type == 3 && start.Target != 0 && TargetSource(Type(start.Source))
                        && !(start.Target == start.Source && Entity(start.Source).Get(GameTag.IS_USING_TRADE_OPTION) == 1)
                };
                // Compare wire entity sources within the immediate parent, looking through keyword wrappers
                // for the enclosing activation; retain state across packets.
                var parent = Current; var enclosing = Enclosing(); b.Under = Label(parent);
                if ((int)start.Type == 3 && (int?)parent?.Start.Type == 5 && parent.Start.Source == start.Source
                    && Entity(start.Source).Get(GameTag.ZONE) == (int)Zone.HAND
                    && !blocks.Any(p => (int)p.Start.Type == 7 && p.Start.Source == start.Source)) b.DrawWrapper = parent;
                b.Deathrattle = start.TriggerKeyword == (int)GameTag.DEATHRATTLE
                    || blocks.Any(p => p.Start.Source == start.Source && p.Start.TriggerKeyword == (int)GameTag.DEATHRATTLE);
                if (SplitCandidate(b) && parent != null)
                {
                    if (SameActivation(enclosing, b))
                        b.SplitVariants.Add(("nested-in-" + Kind(enclosing), SameHeader(enclosing, b), new[] { enclosing }));
                    foreach (int type in new[] { 3, 5 })
                    {
                        var earlier = parent.EffectChildren.Where(e => (int)e.Start.Type == type && SameActivation(e, b)).ToArray();
                        if (earlier.Length > 0)
                            b.SplitVariants.Add((type == 3 ? "sibling-POWER" : "sibling-TRIGGER", earlier.All(e => SameHeader(e, b)), earlier));
                    }
                    parent.EffectChildren.Add(b);
                }
                bool sameAction = previousRootStep == Step && previousRootReplay == Replay;
                if (Wrapper(b))
                {
                    // A hit wrapper inside, after a sibling of, or right after the root of its source's activation.
                    var prior = parent?.EffectChildren.LastOrDefault(e => SameSource(e, b));
                    if (SameSource(enclosing, b)) (b.Wrapped, b.WrappedVariant) = (enclosing, "keyword-hit-in-" + Kind(enclosing));
                    else if (prior != null) (b.Wrapped, b.WrappedVariant) = (prior, "keyword-hit-after-" + Kind(prior));
                    else if (parent == null && sameAction && SameSource(previousRoot, b))
                        (b.Wrapped, b.WrappedVariant) = (previousRoot, "keyword-hit-after-root-" + Kind(previousRoot));
                }
                if (parent == null && previousRoot != null && SameActivation(previousRoot, b) && sameAction)
                    b.SplitVariants.Add(("root-sibling-" + Kind(previousRoot), SameHeader(previousRoot, b), new[] { previousRoot }));
                if (parent == null) previousRoot = null;
                if (blocks.Count == 0)
                    for (int i = 0; i < privateShows.Count; i++)
                        if (privateShows[i].Scope == null) privateShows[i] = (privateShows[i].Entity, privateShows[i].Card, b);
                blocks.Push(b);
                // EFFECT_INDEX: retail explicit EffectIndex=0/1; native-client section 5.2 selector input.
                if ((int)start.Type is 3 or 5 && !start.HasEffectIndex) Flag("EFFECT_INDEX", block: b);
                if (b.Ground?.Activation == PowerActivation.DEATHRATTLE && ((int)start.Type != 5 || !start.HasTriggerKeyword
                    || start.TriggerKeyword != (int)GameTag.DEATHRATTLE))
                    Flag("TRIGGER_KEYWORD", block: b, detail: "deathrattle body needs TRIGGER keyword 217");
            }
            else if (entry.HasPowerEnd)
            {
                if (blocks.Count == 0) Flag("BALANCE", detail: "BLOCK_END without start");
                else
                {
                    var closed = blocks.Peek(); Close(closed); blocks.Pop();
                    if (closed.SplitVariants.Count > 0 || closed.Wrapped != null) pendingSplits.Add(closed);
                    if (blocks.Count == 0)
                    {
                        FlushSplits(); exhaustedLocations.Clear(); retainedHandLayers.Clear();
                        previousRoot = closed; previousRootStep = Step; previousRootReplay = Replay;
                    }
                }
            }
            else if (entry.HasSubSpellStart)
            {
                var b = Current;
                if (b != null)
                {
                    b.SubSpells.Push(entry.SubSpellStart);
                    if (b.Start.Target != 0 && SubSpellTarget(b, b.Start.Target)) b.HeaderSubSpellTarget = true;
                }
            }
            else if (entry.HasSubSpellEnd)
            {
                if (Current?.SubSpells.Count > 0) Current.SubSpells.Pop();
            }
            else if (entry.HasMetaData)
            {
                var meta = entry.MetaData; var b = Current;
                if (b != null)
                {
                    // Retail F:2107-2108 targets the hidden DECK card after creation; this is not a missile hit.
                    bool deckTarget = meta.Type == HistoryMeta.Type.TARGET && meta.Info.Count > 0
                        && meta.Info.All(id => Entity(id).Get(GameTag.ZONE) == (int)Zone.DECK);
                    b.HitMeta |= meta.Type switch { HistoryMeta.Type.TARGET when !deckTarget => 1,
                        HistoryMeta.Type.DAMAGE => 2, HistoryMeta.Type.HEALING => 4, _ => 0 };
                    // Auchenai-style conversion turns the Lifesteal heal into damage to the source controller's hero.
                    int controller = Entity(b.Start.Source).Get(GameTag.CONTROLLER);
                    b.HitDamage |= meta.Type == HistoryMeta.Type.DAMAGE && !(b.Start.TriggerKeyword == (int)GameTag.LIFESTEAL
                        && controller != 0 && meta.Info.Count > 0 && meta.Info.All(id => Type(id) == (int)CardType.HERO && Entity(id).Get(GameTag.CONTROLLER) == controller));
                }
                // META_REF: native-client section 2.1 metadata Info is an entity-reference list.
                // These two types use trailing Info slots as non-entity parameters (native-client 2.1).
                var references = meta.Type is HistoryMeta.Type.SHOW_BIG_CARD or HistoryMeta.Type.CONTROLLER_AND_ZONE_CHANGE ? meta.Info.Take(1) : meta.Info;
                foreach (int id in references) if (!known.Contains(id) && !packetFulls.Contains(id)) Flag("META_REF", id, detail: $"unknown metadata entity {id}");
                if (meta.Type == HistoryMeta.Type.TARGET)
                {
                    // TARGET_DATA: native-client sections 2.2 and 4.3 source binding; retail TARGET.Data=0.
                    if (meta.Data != 0 && meta.Data != (b?.Start.Source ?? 0)) Flag("TARGET_DATA", detail: $"TARGET.Data={meta.Data}");
                    if (b != null)
                    {
                        if (b.RangeHasEffects) b.Targets.Clear();
                        b.Targets.AddRange(meta.Info); b.HasTargets = true; b.RangeHasEffects = false;
                    }
                }
                else if ((int)meta.Type == 22 && b != null && meta.Data == 0 && meta.Info.Count == 1
                    && b.Targets.Contains(meta.Info[0])) b.PoisonTargets.Add(meta.Info[0]);
                else if (meta.Type is HistoryMeta.Type.DAMAGE or HistoryMeta.Type.HEALING)
                {
                    if (b != null) b.Targeted[meta] = meta.Info.All(victim => HasTarget(b, victim));
                    if (b != null && meta.Type == HistoryMeta.Type.DAMAGE && meta.Data == 0 && b.Targeted[meta])
                        foreach (int victim in meta.Info)
                            if (Entity(victim).Get(GameTag.DIVINE_SHIELD) == 1
                                && ActiveCard("EDR_258", Entity(victim).Get(GameTag.CONTROLLER)))
                                b.ShieldZeros[victim] = b.ShieldZeros.GetValueOrDefault(victim) + 1;
                    foreach (int victim in meta.Info) HitTargets(victim, b, metadata: true);
                    b?.Pending.Add(meta);
                    if (meta.Type == HistoryMeta.Type.DAMAGE && b != null && meta.Info.Count == 1)
                    {
                        // Preserve hit order: an earlier ordinary hit still waits for its DAMAGE tag.
                        var next = hookHits.FirstOrDefault(h => !h.Observed && !h.Heal && !h.Shield
                            && h.Source == b.Start.Source && h.Victim == meta.Info[0]);
                        if (next?.SaturatedDamage == true) Resolve(next);
                    }
                }
            }
            else if (entry.HasFullEntity || entry.HasShowEntity || entry.HasChangeEntity)
            {
                var snapshot = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.ChangeEntity;
                // Retired originals must never identify which public burn became the anonymous return.
                // Reject the packet itself: a later HIDE or filtered rewind FULL cannot undo the disclosure.
                if (!string.IsNullOrEmpty(snapshot.Name) && wireId(engineId(snapshot.Entity)) != snapshot.Entity)
                    Flag("PRIVACY", snapshot.Entity, victimCard: snapshot.Name, detail: "named retired return actor " + snapshot.Entity);
                if (entry.HasFullEntity && (snapshot.Tags.Count(t => t.Name == (int)GameTag.ENTITY_ID) != 1
                    || snapshot.Tags.First(t => t.Name == (int)GameTag.ENTITY_ID).Value != snapshot.Entity))
                    Flag("FULL_ENTITY_ID", snapshot.Entity, detail: "FULL must register exactly one ENTITY_ID matching outer ID " + snapshot.Entity);
                known.Add(snapshot.Entity);
                if (entry.HasChangeEntity && Current?.TargetRequired == true) TargetEffect(snapshot.Entity, Current);
                var entity = Entity(snapshot.Entity); int before = entity.Get(GameTag.ZONE);
                if (entity.CardId != snapshot.Name) deckCastShows.Remove(snapshot.Entity);
                if (entry.HasChangeEntity && entity.Get(GameTag.ZONE) == (int)Zone.HAND && !string.IsNullOrEmpty(entity.CardId))
                    retainedHandLayers[snapshot.Entity] = (entity.Get(GameTag.ATK) - Cards.FromId(entity.CardId).ATK,
                        entity.Get(GameTag.HEALTH) - Cards.FromId(entity.CardId).Health, blocks.LastOrDefault());
                entity.CardId = snapshot.Name ?? "";
                foreach (var tag in snapshot.Tags)
                {
                    entity.Tags[tag.Name] = tag.Value;
                    // A reset can be folded into SHOW/CHANGE. Consume only matching non-hit mutations;
                    // final-state snapshots must never steal the later tag belonging to an actual hit.
                    if (mutations.TryGetValue((snapshot.Entity, tag.Name), out var queue) && queue.Count > 0
                        && queue.Peek().Value == tag.Value && queue.Peek().Hit == null) queue.Dequeue();
                }
                int zone = entity.Get(GameTag.ZONE);
                if (entity.Get(GameTag.CARDTYPE) == (int)CardType.ENCHANTMENT && entity.Get(GameTag.ATTACHED) != 0)
                {
                    int attached = entity.Get(GameTag.ATTACHED);
                    var host = Entity(attached);
                    if (host.Get(GameTag.ZONE) == (int)Zone.HAND && !string.IsNullOrEmpty(host.CardId))
                    {
                        if (Current?.UntargetedHandBuffs.Remove(attached) == true)
                            Flag("HAND_BUFF_FX", attached, detail: "hand aura stats preceded attachment without explicit animation target");
                    }
                }
                if (entry.HasShowEntity && !string.IsNullOrEmpty(snapshot.Name) && zone == (int)Zone.DECK) deckCastShows.Add(snapshot.Entity);
                bool publicReveal = entity.Get(GameTag.REVEALED) == 1 || blocks.Any(b => (int)b.Start.Type == 2);
                if (!string.IsNullOrEmpty(snapshot.Name) && Private(snapshot.Entity) && !publicReveal
                    && publicCards.GetValueOrDefault(snapshot.Entity) != snapshot.Name)
                {
                    // A picked offer named just before its public move (NativeMatch identity-before-move) is no leak;
                    // any offer still nameless-public by the operation end is.
                    privateShows.Add((snapshot.Entity, snapshot.Name, blocks.LastOrDefault()));
                }
                if (publicReveal && !string.IsNullOrEmpty(snapshot.Name)) publicCards[snapshot.Entity] = snapshot.Name;
                Move(snapshot.Entity, before, zone, entry.HasShowEntity && !string.IsNullOrEmpty(snapshot.Name)
                    && snapshot.Tags.Any(t => t.Name == (int)GameTag.ZONE && t.Value == (int)Zone.GRAVEYARD));
            }
            else if (entry.HasHideEntity)
            {
                var hide = entry.HideEntity; var entity = Entity(hide.Entity);
                if (burnDestinations.Remove(hide.Entity))
                    Flag("MILL_ORDER", hide.Entity, detail: "HIDE after burn/discard SHOW moves the native mill actor again");
                deckCastShows.Remove(hide.Entity);
                privateShows.RemoveAll(s => s.Entity == hide.Entity && s.Card == entity.CardId);
                entity.CardId = ""; entity.Tags[(int)GameTag.ZONE] = hide.Zone; entity.Tags[(int)GameTag.DAMAGE] = 0;
                entity.Tags[(int)GameTag.REVEALED] = 0; publicCards.Remove(hide.Entity);
                foreach (var b in blocks) if ((int)b.Start.Type == 10 && b.Start.Target == hide.Entity) b.Hidden = true;
            }
            else if (entry.HasTagChange)
            {
                var tag = entry.TagChange; var entity = Entity(tag.Entity); int before = entity.Tags.GetValueOrDefault(tag.Tag);
                var b = Current;
                if (tag.Tag == (int)GameTag.DAMAGE && Type(tag.Entity) == (int)CardType.LOCATION
                    && entity.Get(GameTag.HEALTH) > 0 && tag.Value >= entity.Get(GameTag.HEALTH)) exhaustedLocations.Add(tag.Entity);
                // Retail O:3282-3307; SubSpellController.AttachTasklistToSubSpell binds explicit hand targets.
                // Creation in SETASIDE followed by SHOW is retail too; HISTORY_TARGET cannot animate it.
                if (tag.Tag is (int)GameTag.ATK or (int)GameTag.HEALTH && tag.Value > before
                    && entity.Get(GameTag.ZONE) == (int)Zone.HAND && !string.IsNullOrEmpty(entity.CardId)
                    // Trading removes hand-only reductions before ZONE changes; restoring base stats is not a buff.
                    && entity.Get(GameTag.IS_USING_TRADE_OPTION) != 1
                    // Summon-time scaling prepares the played actor before its HAND-to-PLAY move.
                    && !blocks.Any(parent => (int)parent.Start.Type == 7 && parent.Start.Source == tag.Entity)
                    && b != null && !HasTarget(b, tag.Entity))
                {
                    if (entities.Values.Any(e => !string.IsNullOrEmpty(e.CardId)
                        && e.Get(GameTag.CARDTYPE) == (int)CardType.ENCHANTMENT && e.Get(GameTag.ATTACHED) == tag.Entity
                        && e.Get(GameTag.ZONE) == (int)Zone.PLAY))
                        Flag("HAND_BUFF_FX", tag.Entity, detail: "visible hand enchantment buff lacks active explicit animation target",
                            conditions: entity.CardId == "CATA_615t" && retainedHandLayers.TryGetValue(tag.Entity, out var layer) && layer.Scope == blocks.LastOrDefault()
                                && (tag.Tag == (int)GameTag.ATK ? layer.Attack > 0 && tag.Value == Cards.FromId(entity.CardId).ATK + layer.Attack
                                    : layer.Health > 0 && tag.Value == Cards.FromId(entity.CardId).Health + layer.Health)
                                ? new[] { HistoryShapeCondition.RetainedHandTransform } : Array.Empty<HistoryShapeCondition>());
                    else b.UntargetedHandBuffs.Add(tag.Entity);
                }
                if (tag.Tag == (int)GameTag.TO_BE_DESTROYED && tag.Value == 1)
                {
                    int expected = poisonVictims.FindIndex(p => p.Victim == tag.Entity);
                    bool keyword = b?.Start.HasTriggerKeyword == true && b.Start.TriggerKeyword == 363;
                    if (expected >= 0)
                    {
                        int poisonSource = poisonVictims[expected].Source;
                        poisonVictims.RemoveAt(expected);
                        // Native 5.2-5.3: actor keyword 363/76, blank card ID and negative index avoid replaying card triggers.
                        if (b == null || b.PoisonDestroyed || (int)b.Start.Type != 5 || b.Start.Source != poisonSource
                            || !b.Start.HasTriggerKeyword || b.Start.TriggerKeyword != 363
                            || !b.Start.HasEffectIndex || b.Start.EffectIndex != -1 || b.Start.Target != 0 || !string.IsNullOrEmpty(b.Start.EffectCardId)
                            || !b.Targets.Contains(tag.Entity) || !b.PoisonTargets.Remove(tag.Entity))
                            Flag("POISONOUS_KEYWORD", tag.Entity, detail: "Poisonous destroy needs source TRIGGER keyword 363, effect index -1, victim TARGET and POISONOUS metadata");
                        if (b != null) b.PoisonDestroyed = true;
                        b?.Targets.Remove(tag.Entity);
                    }
                    else if (keyword)
                        Flag("POISONOUS_KEYWORD", tag.Entity, detail: "unexpected or duplicate Poisonous activation");
                }
                if (tag.Tag == (int)GameTag.FATIGUE && tag.Value != before)
                {
                    if (b == null || (int)b.Start.Type != 8 || Type(b.Start.Source) != (int)CardType.HERO)
                        Flag("FATIGUE_BLOCK", tag.Entity, detail: "FATIGUE mutation outside a hero fatigue block",
                            conditions: rewindRetirement && b != null && (int)b.Start.Type == 11
                                ? new[] { HistoryShapeCondition.RewindRestoration } : Array.Empty<HistoryShapeCondition>());
                    if (b != null) b.FatigueEntity = tag.Entity;
                }
                var attack = blocks.FirstOrDefault(x => (int)x.Start.Type == 1);
                if (attack != null)
                {
                    attack.AttackTags.Add((tag.Entity, tag.Tag, tag.Value));
                    if (attack.Ground == null)
                    {
                        if (tag.Value != 0 && tag.Tag == (int)GameTag.PROPOSED_ATTACKER) attack.Attacker = tag.Value;
                        if (tag.Value != 0 && tag.Tag == (int)GameTag.PROPOSED_DEFENDER) attack.Defender = tag.Value;
                    }
                }
                Hit hit = GroundHit(tag.Entity, tag.Tag, tag.Value, out bool matched);
                if (game == null && !matched && Type(tag.Entity) is (int)CardType.HERO or (int)CardType.MINION)
                {
                    if (tag.Tag == (int)GameTag.DAMAGE && tag.Value != before)
                        hit = new() { Source = b?.Start.Source ?? 0, Victim = tag.Entity, Amount = Math.Abs(tag.Value - before), Heal = tag.Value < before };
                    if (tag.Tag == (int)GameTag.ARMOR && tag.Value < before)
                        hit = new() { Source = b?.Start.Source ?? 0, Victim = tag.Entity, Amount = before - tag.Value };
                    if (tag.Tag == (int)GameTag.DIVINE_SHIELD && before > 0 && tag.Value == 0)
                        hit = new() { Source = b?.Start.Source ?? 0, Victim = tag.Entity, Shield = true };
                }
                if (tag.Tag == (int)GameTag.DIVINE_SHIELD && before > 0 && tag.Value == 0) b?.ShieldCleared.Add(tag.Entity);
                if (hit != null)
                {
                    hit.CounterSaturated = tag.Tag == (int)GameTag.DAMAGE && tag.Value == int.MaxValue && before > 0;
                    Resolve(hit);
                }
                entity.Tags[tag.Tag] = tag.Value;
                if (tag.Tag == (int)GameTag.ZONE) Move(tag.Entity, before, tag.Value, zoneTag: true);
                // A card handed to the human is the human's to see: NativeMatch names it before the CONTROLLER move.
                if (tag.Tag == (int)GameTag.CONTROLLER && tag.Value == 1 && before != 1)
                    privateShows.RemoveAll(s => s.Entity == tag.Entity && s.Card == entity.CardId);
                if (b?.TargetRequired == true && b.Start.Target == tag.Entity && tag.Value != before
                    && EffectTag(tag.Tag))
                    TargetEffect(tag.Entity, b);
            }
        }
        // Hidden/filtered engine tags have no wire counterpart in this packet.
        if (RequireCorrelation && hookHits.Any(h => !h.Observed))
            throw new InvalidOperationException($"history-shape unmatched hook hit {Replay}@{Step}: " +
                string.Join(',', hookHits.Where(h => !h.Observed).Select(h => $"{h.SourceCard}/{h.Victim}/{h.Amount}")));
        if (RequireCorrelation && poisonVictims.Count != 0)
            throw new InvalidOperationException("history-shape unmatched Poisonous activation");
        hookHits.Clear(); mutations.Clear(); groundBlocks.Clear();
    }
    internal void Finish()
    {
        if (finished) return;
        finished = true;
        foreach (var scope in privateShows.Select(s => s.Scope).Distinct().ToArray()) ResolvePrivateShows(scope);
        pendingSplits.AddRange(blocks.Where(b => b.SplitVariants.Count > 0 || b.Wrapped != null).Reverse());
        FlushSplits();
        foreach (var b in blocks) Flag("BALANCE", block: b, detail: "block left open at game end");
    }
}
