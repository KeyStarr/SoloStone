using System;
using System.Collections.Generic;
using System.Linq;

// Player-chosen NPC difficulty (owner 2026-11-15). Hard is today's NPCs, unchanged: every Hard path returns the encounter
// the matchmaker built, untouched. Easy and Medium are "the ladder, shifted down": the matchmaker serves the opponents a player
// ShiftStars stars lower would meet (deck band and rating target), with a Core step below Bronze (the class's Core starter
// deck) and a beginner boost on the NPC's play that fades as the player climbs. Extreme is shown but locked (no AI yet).
// NPCs are displayed at the player's rank (DisplayShift); every Elo update still uses the NPC's real rating.
// Naturalness floors: lethalMiss never applies under SkillCatalog.MinMissableLethalActions, mistakes stay the guarded
// SkilledPilot kinds (each one a real candidate within maxValueLoss), and the plan search never drops below MinPlanNodes.
public sealed class DifficultyBoost
{
    public double MistakeScale { get; }        // multiplies each tier mistake chance (capped at MaxChance)
    public double LossScale { get; }           // multiplies each mistake's maxValueLoss (absent kinds use the weakest tier's)
    public double AoeFloor { get; }            // ignoreAoeRisk chance at least this (plays into board clears)
    public double MulliganFloor { get; }       // greedyMulligan chance at least this (keeps expensive cards)
    public double TemperatureFloor { get; }    // softmax temperature at least this
    public int TopNFloor { get; }              // softmax pool at least this
    public double SoftmaxLossFloor { get; }    // softmax bound at least this (evaluator units)
    public double BudgetScale { get; }         // plan and turn node budgets (lethal budget unchanged)
    public DifficultyBoost(double mistakeScale, double lossScale, double aoeFloor, double mulliganFloor,
        double temperatureFloor, int topNFloor, double softmaxLossFloor, double budgetScale)
    { MistakeScale = mistakeScale; LossScale = lossScale; AoeFloor = aoeFloor; MulliganFloor = mulliganFloor;
      TemperatureFloor = temperatureFloor; TopNFloor = topNFloor; SoftmaxLossFloor = softmaxLossFloor; BudgetScale = budgetScale; }

    public static readonly DifficultyBoost None = new DifficultyBoost(1, 1, 0, 0, 0, 1, 0, 1);

    internal static DifficultyBoost Lerp(DifficultyBoost a, DifficultyBoost b, double t)
    {
        double L(double x, double y) => x + (y - x) * t;
        return new DifficultyBoost(L(a.MistakeScale, b.MistakeScale), L(a.LossScale, b.LossScale), L(a.AoeFloor, b.AoeFloor),
            L(a.MulliganFloor, b.MulliganFloor), L(a.TemperatureFloor, b.TemperatureFloor),
            (int)Math.Round(L(a.TopNFloor, b.TopNFloor)), L(a.SoftmaxLossFloor, b.SoftmaxLossFloor), L(a.BudgetScale, b.BudgetScale));
    }
}

public sealed class NpcDifficultyLevel
{
    public const double MaxChance = 0.6;
    public const int MinPlanNodes = 1750;
    public string Id { get; }
    public string Name { get; }
    public string Summary { get; }
    public bool Locked { get; }
    public int ShiftStars { get; }             // the ladder offset: Medium one league (30 stars), Easy two
    public double SoftKeep { get; }            // at the Core step: chance a soft picked deck (power tier >= 4, not Aggro) stays
    public DifficultyBoost Bottom { get; }     // boost at the Core step and Bronze (position <= 0)
    public DifficultyBoost Top { get; }        // boost at the level's top (position = LegendIndex - ShiftStars)
    // At the fresh rating, this share of Core matches uses the class's beginner deck instead of its player starter.
    // Easy always uses beginner decks. Medium blends both and increases the beginner share as rating falls.
    public double BeginnerShare { get; internal set; }
    // Casual keeps a share of its themed decks even at the bottom.
    public double CasualCoreShare { get; internal set; }
    internal NpcDifficultyLevel(string id, string name, string summary, bool locked, int shiftStars, double softKeep,
        DifficultyBoost bottom, DifficultyBoost top)
    { Id = id; Name = name; Summary = summary; Locked = locked; ShiftStars = shiftStars; SoftKeep = softKeep;
      Bottom = bottom; Top = top; }

    // Hard (and the locked Extreme, which never reaches a match) change nothing.
    public bool IsHard => ShiftStars == 0 && Bottom == null;
    public double ShiftRating => ShiftStars * LadderRules.RatingPerStar;
    public double DisplayShift => IsHard ? 0 : ShiftRating;
    public const double DisplaySpread = 60;   // half a league either side of the player's rank
    // The rating an NPC is shown at (owner 2026-11-15: at the player's rank): the player's own rank (Legend: the player's
    // rating, never below Legend) plus the NPC's shifted offset from it, clamped to DisplaySpread. Cosmetic only; Elo and every
    // stored record use the real rating. Hard shows the real rating.
    public double DisplayRating(double npcRating, int playerStarIndex, double playerRating)
    {
        if (IsHard) return npcRating;
        double anchor = playerStarIndex >= LadderRules.LegendIndex
            ? Math.Max(playerRating, LadderRules.RatingForIndex(LadderRules.LegendIndex)) : LadderRules.RatingForIndex(playerStarIndex);
        double offset = Math.Max(-DisplaySpread, Math.Min(DisplaySpread, npcRating + ShiftRating - anchor));
        double shown = anchor + offset;
        double legend = LadderRules.RatingForIndex(LadderRules.LegendIndex);
        // Only a Legend player is ever shown a Legend rating: below Legend the shown rating stays strictly under the Legend line.
        return playerStarIndex >= LadderRules.LegendIndex ? Math.Max(shown, legend) : Math.Min(shown, legend - 1);
    }

    // Where on the shifted ladder a match sits, in stars: negative below Bronze 10 (the Core step).
    public int LadderPosition(int starIndex) => Math.Min(starIndex, LadderRules.LegendIndex) - ShiftStars;
    public int RatingPosition(double rating) => (int)Math.Floor((rating - LadderRules.BronzeRating) / LadderRules.RatingPerStar) - ShiftStars;

    // Share of matches at the Core step: 0 at position 0, rising to 1 one league (30 stars) below.
    public double CoreChance(int position) => IsHard || position >= 0 ? 0 : Math.Min(1.0, -position / (double)LeagueStars);
    public const int LeagueStars = 30;

    public DifficultyBoost BoostAt(int position)
    {
        if (IsHard) return DifficultyBoost.None;
        int top = Math.Max(1, LadderRules.LegendIndex - ShiftStars);
        double t = Math.Max(0, Math.Min(1, position / (double)top));
        return DifficultyBoost.Lerp(Bottom, Top, t);
    }

    // The NPC's own tier with the boost at this position. template (the weakest tier) supplies maxValueLoss for kinds the
    // tier lacks. Hard returns skill itself.
    public SkillProfile Apply(SkillProfile skill, SkillProfile template, int position)
    {
        if (skill == null) throw new ArgumentNullException(nameof(skill));
        if (IsHard) return skill;
        var boost = BoostAt(position);
        var kinds = new[] { MistakeKind.TradeTarget, MistakeKind.FaceBias, MistakeKind.HoldTooLong, MistakeKind.IgnoreAoeRisk,
            MistakeKind.GreedyMulligan, MistakeKind.LethalMiss };
        var mistakes = new List<MistakeRule>();
        foreach (var kind in kinds)
        {
            var own = skill.Rule(kind);
            var model = own ?? template?.Rule(kind);
            double chance = (own?.Chance ?? 0) * boost.MistakeScale;
            if (kind == MistakeKind.IgnoreAoeRisk) chance = Math.Max(chance, boost.AoeFloor);
            if (kind == MistakeKind.GreedyMulligan) chance = Math.Max(chance, boost.MulliganFloor);
            chance = Math.Min(MaxChance, chance);
            if (chance <= 0) continue;
            double loss = (model?.MaxValueLoss ?? 0) * boost.LossScale;
            int minLethal = Math.Max(SkillCatalog.MinMissableLethalActions, model?.MinLethalActions ?? 0);
            mistakes.Add(new MistakeRule(kind, chance, loss, kind == MistakeKind.LethalMiss ? minLethal : 0));
        }
        // Misorder and any future kind the tier carries stay as they are.
        mistakes.AddRange(skill.Mistakes.Where(m => !kinds.Contains(m.Kind)));
        var b = skill.Budget;
        int plan = Math.Max(Math.Min(b.PlanNodes, MinPlanNodes), (int)Math.Round(b.PlanNodes * boost.BudgetScale));
        int turn = Math.Max(plan, (int)Math.Round(b.TurnNodes * boost.BudgetScale));
        var budget = new Budget(turn, plan, b.LethalNodes, b.ReplyK, b.ReplyL, b.ReplyR, b.OwnBeamWidth, b.Dedupe,
            b.TurnSearchSeconds, b.TurnWallSeconds, b.Brain);
        return new SkillProfile(skill.Id + "+" + Id, skill.Tier, skill.RatingMin, skill.RatingMax, budget,
            Math.Max(skill.Temperature, boost.TemperatureFloor), Math.Max(skill.TopN, boost.TopNFloor), skill.ReplyBlendScale,
            mistakes, Math.Max(skill.SoftmaxMaxValueLoss, boost.SoftmaxLossFloor)) { PersonalityStyle = skill.PersonalityStyle };
    }
}

// The four levels. Rows are code (like GrindTiers); tune Easy and Medium here, never Hard. Starting values are the design's
// (private/docs/roadmap/2026-11-15-npc-difficulty.md) until the sim round replaces them.
public static class NpcDifficulties
{
    public const string EasyId = NpcDifficultyIds.Easy, MediumId = NpcDifficultyIds.Medium, HardId = NpcDifficultyIds.Hard,
        ExtremeId = NpcDifficultyIds.Extreme;

    public static readonly NpcDifficultyLevel Easy = new NpcDifficultyLevel(EasyId, NpcDifficultyIds.Name(EasyId),
        NpcDifficultyIds.Summary(EasyId), false, 60, 0,
        // Sim round 1 (2026-11-15): the strongest natural boost; no missed short lethal, fewer passes than Hard.
        bottom: new DifficultyBoost(3.0, 2.0, 1.0, 0.9, 3.0, 5, 14, 0.25),
        top: new DifficultyBoost(1.5, 1.2, 0.4, 0.4, 1.0, 3, 5, 0.5)) { BeginnerShare = 1, CasualCoreShare = .70 };
    public static readonly NpcDifficultyLevel Medium = new NpcDifficultyLevel(MediumId, NpcDifficultyIds.Name(MediumId),
        NpcDifficultyIds.Summary(MediumId), false, 30, 0,
        bottom: new DifficultyBoost(1.5, 1.2, 0.4, 0.45, 1.7, 4, 7, 0.75),
        top: DifficultyBoost.None) { BeginnerShare = .40, CasualCoreShare = .55 };
    public static readonly NpcDifficultyLevel Hard = new NpcDifficultyLevel(HardId, NpcDifficultyIds.Name(HardId),
        NpcDifficultyIds.Summary(HardId), false, 0, 0, null, null);
    public static readonly NpcDifficultyLevel Extreme = new NpcDifficultyLevel(ExtremeId, NpcDifficultyIds.Name(ExtremeId),
        NpcDifficultyIds.Summary(ExtremeId), NpcDifficultyIds.IsLocked(ExtremeId), 0, 0, null, null);

    public static readonly IReadOnlyList<NpcDifficultyLevel> All = Array.AsReadOnly(new[] { Easy, Medium, Hard, Extreme });
    // Saves without the field (every save made before this setting) stay Hard; the Create save form starts at Medium.
    public static NpcDifficultyLevel Default => Find(NpcDifficultyIds.Default);
    public static NpcDifficultyLevel NewSaveDefault => Find(NpcDifficultyIds.NewSaveDefault);

    public static bool IsKnown(string id) => id != null && All.Any(level => level.Id == id);
    public static bool IsSelectable(string id) => IsKnown(id) && !Find(id).Locked;
    // null = Default (Hard). An unknown id throws: a save naming one is refused, never silently changed.
    public static NpcDifficultyLevel Find(string id)
    {
        string known = NpcDifficultyIds.Resolve(id);
        return All.First(level => level.Id == known);
    }
}

// Core starter decks as NPC decks (admission "core": never generated, matched or inferred; served only to a Core-step
// encounter). One per class, built from LocalDecks.PlayerCoreStarterDecks so the two lists never drift.
public static class NpcCoreDecks
{
    public const string Admission = "core", Origin = "core-starter", IdPrefix = "npc-core-", PlayerPrefix = "player-core-";

    private static readonly Dictionary<string, string> ClassByHero = new Dictionary<string, string>(StringComparer.Ordinal) {
        ["HERO_01"] = "WARRIOR", ["HERO_02"] = "SHAMAN", ["HERO_03"] = "ROGUE", ["HERO_04"] = "PALADIN", ["HERO_05"] = "HUNTER",
        ["HERO_06"] = "DRUID", ["HERO_07"] = "WARLOCK", ["HERO_08"] = "MAGE", ["HERO_09"] = "PRIEST", ["HERO_10"] = "DEMONHUNTER",
        ["HERO_11"] = "DEATHKNIGHT" };

    private static DeckArchetype ArchetypeOf(DeckStyle style) =>
        style == DeckStyle.Tempo ? DeckArchetype.Tempo : style == DeckStyle.Control || style == DeckStyle.Frost ? DeckArchetype.Control
        : DeckArchetype.Midrange;

    public static string IdFor(string cardClass) => IdPrefix + (cardClass ?? "").ToLowerInvariant();

    public static IReadOnlyList<NpcDeckEntry> Entries()
    {
        var rows = new List<NpcDeckEntry>();
        foreach (var deck in LocalDecks.PlayerCoreStarterDecks)
        {
            if (!deck.Id.StartsWith(PlayerPrefix, StringComparison.Ordinal) || !ClassByHero.TryGetValue(deck.Hero, out string cardClass)) continue;
            var cards = deck.CardIds.GroupBy(id => id, StringComparer.Ordinal).Select(g => new NpcDeckCard(g.Key, g.Count()));
            rows.Add(new NpcDeckEntry(IdFor(cardClass), 1, deck.Name, cardClass, deck.Hero, Origin, Admission, null,
                new NpcDeckTags(ArchetypeOf(deck.Style), 5, 2, 0, 3, LadderBand.Bronze, LadderBand.Legend),
                new NpcDeckSource("solostone", null, null, null, "Core starter " + deck.Id), cards, new NpcDeckSideboard[0]));
        }
        return rows;
    }

    // The library with the Core decks added (ids already present win, so a hand-made entry overrides).
    public static NpcDeckLibrary Include(NpcDeckLibrary library)
    {
        if (library == null) return null;
        var extra = Entries().Concat(NpcBeginnerDecks.Entries()).Where(entry => library.Find(entry.Id) == null).ToList();
        return extra.Count == 0 ? library : new NpcDeckLibrary(library.Schema, library.LibraryVersion, library.ActiveMeta, library.Decks.Concat(extra));
    }

    public static NpcWorldCatalogs Include(NpcWorldCatalogs catalogs) => catalogs == null || catalogs.Decks == null ? catalogs
        : new NpcWorldCatalogs(Include(catalogs.Decks), catalogs.Skills, catalogs.Personalities, catalogs.Notables, catalogs.Names, catalogs.Cosmetics);
}

// Shared deck selection and skill adjustment for Ranked and Casual difficulty.
public static class NpcDifficultyShaper
{
    public const int StreamCore = 11, StreamSoftKeep = 12, StreamBeginner = 13;

    // The queue has already chosen the served deck. Rematches keep their original deck.
    public static NpcEncounter Shape(NpcEncounter encounter, NpcDifficultyLevel level, int position, NpcWorld world, NpcProfileBuilder build)
    {
        if (encounter == null) throw new ArgumentNullException(nameof(encounter));
        if (level == null || level.IsHard) return encounter;
        if (level.Locked) throw new InvalidOperationException("NPC difficulty " + level.Id + " is locked.");
        var tiers = world.Catalogs.Skills.Tiers;
        var skill = level.Apply(encounter.Skill, tiers.Count > 0 ? tiers[0] : null, position);
        var profile = (build ?? NpcEncounterProfiles.Build)(encounter.Npc, encounter.Deck, encounter.Personality, skill);
        return new NpcEncounter(encounter.Npc, encounter.Deck, skill, encounter.Personality, profile, encounter.Rating, encounter.MatchSeed,
            encounter.Reason, level.Id, position);
    }

    // The Core decision of one match, rolled once (streams 11 and 12): whether this match is at the Core step and whether a soft
    // deck keeps its place. The matchmaker rolls it before it builds candidates, so the queue rules judge the decks actually served.
    public readonly struct CoreDecision
    {
        public readonly bool Core, SoftKeep, Beginner;
        public CoreDecision(bool core, bool softKeep, bool beginner = false) { Core = core; SoftKeep = softKeep; Beginner = beginner; }
    }

    public static CoreDecision Decide(NpcDifficultyLevel level, int position, int seed,
        double rating = LadderRules.StartRating, double coreShare = 1)
    {
        double chance = level == null ? 0 : level.CoreChance(position) * coreShare;
        if (chance <= 0 || new MatchRng(seed, StreamCore).NextDouble() >= chance) return default(CoreDecision);
        double beginner = level.BeginnerShare;
        // A level that mixes the two kinds of deck gives a struggling player more chances to learn.
        // At the fresh rating it uses the configured share. A 400 Elo drop raises that share by one.
        if (beginner > 0 && beginner < 1)
            beginner = Math.Max(0, Math.Min(1, beginner + (LadderRules.StartRating - rating) / 400));
        return new CoreDecision(true, new MatchRng(seed, StreamSoftKeep).NextDouble() < level.SoftKeep,
            beginner >= 1 || beginner > 0 && new MatchRng(seed, StreamBeginner).NextDouble() < beginner);
    }

    // The deck a picked deck becomes under the decision: itself, or the Core deck of its class.
    public static NpcDeckEntry Served(NpcDeckEntry picked, CoreDecision decision, NpcDeckLibrary library)
    {
        if (!decision.Core || picked == null || library == null) return picked;
        bool soft = picked.Tags.PowerTier >= 4 && picked.Tags.Archetype != DeckArchetype.Aggro;
        if (soft && decision.SoftKeep) return picked;
        return (decision.Beginner ? library.Find(NpcBeginnerDecks.IdFor(picked.Class)) : null)
            ?? library.Find(NpcCoreDecks.IdFor(picked.Class)) ?? picked;
    }

    // A Core deck stands in for a deck of its class: an NPC "owns" it when it owns any deck of that class.
    public static bool IsCoreDeck(NpcDeckEntry deck) => deck != null && deck.Admission == NpcCoreDecks.Admission;
}
