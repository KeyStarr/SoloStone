// Pure ladder rule tests and the seeded climb simulation (spec 2026-10-24-ladder-rules-design.md).
// `LadderRulesTests` runs the rule checks plus a short simulation band check; `--report [climbs]` prints the full table.
using System.Globalization;
using static LadderRules;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;   // reports and parsing never depend on the host locale
static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static void Reject(Action action, string message)
{
    try { action(); } catch (ArgumentException) { return; }
    throw new Exception("Expected rejection: " + message);
}
static LadderPosition At(int index, int multiplier = 1, int streak = 0, int? best = null, int placement = PlacementLength, double rating = StartRating) =>
    Validate(new LadderPosition { StarIndex = index, BestStarIndex = best ?? index, Multiplier = index >= DiamondFiveIndex ? 1 : multiplier,
        StreakWins = streak, PlacementGames = placement, Rating = rating, LegendReached = index == LegendIndex });
static LadderStep Play(LadderPosition p, LadderOutcome outcome) => Apply(p, outcome, p.Rating);

if (args.Length > 0 && args[0] == "--report")
{
    ClimbSimulation.Report(args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 20000);
    return;
}
if (args.Length > 0 && args[0] == "--tier-report")
{
    // --tier-report climbs bonus:top:k ... @strength ...: grind tier paces (docs/design/2026-10-31-grind-tiers.md).
    TierClimbSimulation.Report(int.Parse(args[1], CultureInfo.InvariantCulture),
        args.Skip(2).Where(a => !a.StartsWith('@')).Select(TierClimbSimulation.Parse).ToArray(),
        args.Skip(2).Where(a => a.StartsWith('@')).Select(a => double.Parse(a[1..], CultureInfo.InvariantCulture)).ToArray());
    return;
}
if (args.Length > 0 && args[0] == "--world-report")
{
    // --world-report [climbs] [strength ...] [bs=x gp=y d=z field=w]: tuning overrides for the matchmaker offsets.
    var tuned = QueueRules.Default;
    foreach (var arg in args.Skip(2).Where(a => a.Contains('=')))
    {
        double value = double.Parse(arg[(arg.IndexOf('=') + 1)..], CultureInfo.InvariantCulture);
        switch (arg[..arg.IndexOf('=')]) { case "bs": tuned.BronzeSilverOffset = value; break; case "gp": tuned.GoldPlatinumOffset = value; break;
            case "d": tuned.DiamondOffset = value; break; case "field": tuned.FieldOffset = value; break; default: throw new Exception("unknown override " + arg); }
    }
    WorldClimbSimulation.RulesOverride = tuned;
    var strengths = args.Skip(2).Where(a => !a.Contains('=')).Select(a => double.Parse(a, CultureInfo.InvariantCulture)).ToArray();
    if (strengths.Length > 0) WorldClimbSimulation.StrengthsToReport = strengths;
    WorldClimbSimulation.Report(args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 400);
    return;
}

// Floors and Bronze immunity: every index, exhaustively.
for (int index = 0; index < LegendIndex; index++)
{
    int expected = index < SilverTenIndex || index % FloorSpacing == 0 ? index : index - 1;
    var step = Play(At(index, best: index), LadderOutcome.Loss);
    Check(step.After.StarIndex == expected && step.StarsGained == expected - index, "loss at index " + index);
    Check(IsFloor(index) == (index > 0 && index % 15 == 0 && index <= 135), "floor flag " + index);
}
Check(Enumerable.Range(0, 151).Count(IsFloor) == 9, "nine floors from Bronze 5 to Diamond 5");
Check(RankName(15) == "Bronze 5" && RankName(30) == "Silver 10" && RankName(45) == "Silver 5" && RankName(75) == "Gold 5"
    && RankName(105) == "Platinum 5" && RankName(135) == "Diamond 5" && RankName(149) == "Diamond 1" && RankName(150) == "Legend", "rank names");

// Demotion at 0 stars: Silver 9 (0 stars) -> Silver 10 with 2 stars; a floor rank 5 at 0 stars holds.
var demoted = Play(At(33), LadderOutcome.Loss).After;
Check(demoted.StarIndex == 32 && RankName(32) == "Silver 10" && 32 % 3 == 2, "demotion to previous rank with 2 stars");
Check(Play(At(46, best: 50), LadderOutcome.Loss).After.StarIndex == 45, "Silver 5 floor reached holds");
Check(Play(At(45, best: 50), LadderOutcome.Loss).After.StarIndex == 45, "Silver 5 floor at 0 stars");
// Legacy folds (no Silver 5 floor) can leave the index below a floor its best passed; rules use the index's own floor.
Check(Play(At(44, best: 45), LadderOutcome.Loss).After.StarIndex == 43, "legacy position below a passed floor stays valid");

// Ties change nothing (stars, streak, multiplier), and do not break a streak.
var tied = Play(At(50, multiplier: 3, streak: 2), LadderOutcome.Tie);
Check(tied.StarsGained == 0 && tied.After.StreakWins == 2 && tied.After.Multiplier == 3 && tied.After.Rating == StartRating, "tie neutral");
var placementTie = Apply(Fresh(), LadderOutcome.Tie, 1800);
Check(placementTie.After.Rating == StartRating && placementTie.After.PlacementGames == 0 && placementTie.After.Multiplier == Fresh().Multiplier
    && placementTie.FloorsReached.Length == 0, "tie neutral in placement against a stronger opponent");
var p = At(50, multiplier: 2);
var w1 = Play(p, LadderOutcome.Win); var t = Play(w1.After, LadderOutcome.Tie); var w2 = Play(t.After, LadderOutcome.Win);
var w3 = Play(w2.After, LadderOutcome.Win);
Check(w1.StarsGained == 2 && !w1.StreakBonus && w2.StarsGained == 2 && !w2.StreakBonus, "first two wins single");
Check(w3.StreakBonus && w3.StarsGained == 4 && w3.After.StreakWins == 3, "third straight win (tie between) pays 2m");
var w4 = Play(w3.After, LadderOutcome.Win);
Check(w4.StreakBonus && w4.StarsGained == 4, "later streak wins keep paying 2m");
var lost = Play(w4.After, LadderOutcome.Loss);
Check(lost.After.StreakWins == 0 && lost.After.Multiplier == w4.After.Multiplier, "loss resets streak, keeps multiplier");
Check(!Play(Play(lost.After, LadderOutcome.Win).After, LadderOutcome.Win).StreakBonus, "streak restarts after a loss");

// Diamond 5: no streak bonus and x1 from there; a win below it stops at index 135.
var d5 = Play(At(135, streak: 5), LadderOutcome.Win);
Check(d5.StarsGained == 1 && !d5.StreakBonus && d5.After.Multiplier == 1, "no streaks from Diamond 5");
var stop = Play(At(130, multiplier: 3, streak: 2), LadderOutcome.Win);
Check(stop.After.StarIndex == 135 && stop.StoppedAtDiamondFive && stop.After.Multiplier == 1, "big win stops at Diamond 5");
var streakStop = Play(At(134, multiplier: 1, streak: 2, best: 134), LadderOutcome.Win);
Check(streakStop.StreakBonus && streakStop.After.StarIndex == 135 && streakStop.StoppedAtDiamondFive, "streak win stops at Diamond 5");
Check(!Play(At(132, multiplier: 1), LadderOutcome.Win).StoppedAtDiamondFive, "exact landing below Diamond 5 is not a stop");
Check(Play(At(134, multiplier: 1), LadderOutcome.Win).After.StarIndex == 135, "single star reaches Diamond 5");

// Multiplier decay: -1 per new floor, applied after the win; several floors in one win; losses do not decay.
var multi = Play(At(28, multiplier: 10, streak: 2), LadderOutcome.Win);
Check(multi.StarsGained == 20 && multi.After.StarIndex == 48 && multi.FloorsReached.SequenceEqual(new[] { 30, 45 })
    && multi.After.Multiplier == 8, "multi-floor decay after the win");
Check(Play(At(48, multiplier: 8, best: 52), LadderOutcome.Win).After.Multiplier == 8, "re-reaching an old floor does not decay");
// A x10 start (placement done) climbing one win at a time: x(10 - floors reached) after every win, x1 from Diamond 5.
var climb = At(0, multiplier: 10);
int floorsSeen = 0;
while (climb.StarIndex < DiamondFiveIndex)
{
    var step = Play(climb, LadderOutcome.Win);
    floorsSeen += step.FloorsReached.Length;
    Check(step.StarsGained == Math.Min(climb.Multiplier, DiamondFiveIndex - climb.StarIndex), "x10 climb stars at " + climb.StarIndex);
    Check(step.After.Multiplier == (step.After.StarIndex >= DiamondFiveIndex ? 1 : Math.Max(1, 10 - FloorsReached(step.After.BestStarIndex))),
        "x10 climb decay at " + step.After.StarIndex);
    climb = Play(step.After, LadderOutcome.Loss).After; // a loss between wins keeps streaks out of this check
}
Check(floorsSeen == 9 && climb.Multiplier == 1, "nine floors and x1 at Diamond 5");
Check(Multiplier(10, 120, 120) == 2 && Multiplier(11, 134, 134) == 3 && Multiplier(11, 135, 135) == 1, "multiplier formula");

// Legend: reached by winning past Diamond 1, permanent, cap 150.
var legend = Play(At(149), LadderOutcome.Win);
Check(legend.After.StarIndex == 150 && legend.LegendReachedNow && legend.After.LegendReached, "Legend promotion");
Check(Play(legend.After, LadderOutcome.Loss).After.StarIndex == 150 && Play(legend.After, LadderOutcome.Win).After.StarIndex == 150
    && !Play(legend.After, LadderOutcome.Win).LegendReachedNow, "Legend permanent and capped");
Reject(() => Validate(new LadderPosition { StarIndex = 151, BestStarIndex = 151, Multiplier = 1 }), "index above 150");
Reject(() => Validate(new LadderPosition { StarIndex = 150, BestStarIndex = 150, Multiplier = 1 }), "Legend flag missing");
Reject(() => Validate(new LadderPosition { StarIndex = 140, BestStarIndex = 140, Multiplier = 2, Rating = 1500 }), "multiplier above x1 from Diamond 5");
Reject(() => Validate(new LadderPosition { StarIndex = 10, BestStarIndex = 10, Multiplier = 0, Rating = 1500 }), "multiplier 0");
Reject(() => Validate(new LadderPosition { StarIndex = 10, BestStarIndex = 29, Multiplier = 2, Rating = 1500 }), "Bronze never drops");
Reject(() => Validate(new LadderPosition { StarIndex = 134, BestStarIndex = 134, Multiplier = 4, Rating = 1500 }), "multiplier above 11 - floors");
Check(Validate(new LadderPosition { StarIndex = 134, BestStarIndex = 134, Multiplier = 3, Rating = 1500 }) != null, "x11 start keeps x3 at Diamond 6");
Reject(() => Validate(new LadderPosition { StarIndex = 10, BestStarIndex = 10, Multiplier = 2, Rating = double.NaN }), "NaN rating");
Reject(() => Apply(At(10), LadderOutcome.Win, double.PositiveInfinity), "infinite opponent rating");

// Placement: rating moves by the large step; the multiplier follows it each game, then is fixed.
var fresh = Fresh();
Check(fresh.PlacementGames == 0 && fresh.Rating == StartRating && fresh.Multiplier == StartMultiplier(StartRating), "fresh profile");
var placed = fresh; int lastMultiplier = placed.Multiplier;
for (int i = 0; i < PlacementLength; i++)
{
    var step = Play(placed, LadderOutcome.Win);
    Check(step.Placement && step.After.Rating == placed.Rating + PlacementK / 2, "placement step " + i);
    Check(step.After.Multiplier == Multiplier(StartMultiplier(step.After.Rating), step.After.BestStarIndex, step.After.StarIndex), "placement follows rating " + i);
    placed = step.After;
}
Check(placed.PlacementGames == PlacementLength && StartMultiplier(placed.Rating) == MaximumMultiplier, "ten placement wins reach x11 rating");
var settled = Play(placed, LadderOutcome.Win);
Check(!settled.Placement && settled.After.Rating == placed.Rating + SettledK / 2 && settled.After.PlacementGames == PlacementLength, "settled step");
var risen = Play(Play(fresh, LadderOutcome.Win).After, LadderOutcome.Win).After;
Check(StartMultiplier(risen.Rating) > StartMultiplier(fresh.Rating), "multiplier rises during placement");
var fell = fresh; for (int i = 0; i < 4; i++) fell = Play(fell, LadderOutcome.Loss).After;
Check(fell.Multiplier == 2 && fell.StarIndex == 0, "placement losses drop to retail floor x2, no Bronze star loss");
Check(StartMultiplier(1000) == 2 && StartMultiplier(RatingForIndex(45)) == 3 && StartMultiplier(RatingForIndex(75)) == 5
    && StartMultiplier(RatingForIndex(135)) == 9 && StartMultiplier(RatingForIndex(150)) == 10 && StartMultiplier(VeryHighRating) == 11,
    "rating map follows retail minimums");
Check(Math.Abs(Apply(At(10, rating: 1500), LadderOutcome.Win, 1700).After.Rating - (1500 + SettledK * (1 - Expected(1500, 1700)))) <= 0.0005,
    "Elo against a supplied opponent rating");

// V10 reservation seed: star index unchanged, placement from game count, streak from trailing wins.
var W = LadderOutcome.Win; var L = LadderOutcome.Loss; var T = LadderOutcome.Tie;
Check(Seed(0, 0, Array.Empty<LadderOutcome>()).Rating == StartRating, "empty history seeds a fresh profile");
var seed = Seed(62, 64, new[] { L, W, L, W, W, T, W, L, W, W, T, W });
Check(seed.StarIndex == 62 && seed.BestStarIndex == 64 && seed.PlacementGames == 10 && seed.StreakWins == 3
    && seed.Multiplier == Multiplier(StartMultiplier(seed.Rating), 64, 62) && seed.Rating > RatingForIndex(62), "seeded reservation");
var shortSeed = Seed(3, 3, new[] { W, W, W });
Check(shortSeed.PlacementGames == PlacementLength && shortSeed.StreakWins == 3, "migrated profiles have completed placement");
Check(Play(shortSeed, W).After.Rating == shortSeed.Rating + SettledK / 2, "migrated profile uses the settled step");
Check(Seed(150, 150, new[] { W }).LegendReached && Seed(140, 140, new[] { W }).Multiplier == 1, "seeded Legend and Diamond 5");
Check(Seed(44, 45, Enumerable.Repeat(W, 45).Append(L).ToArray()).BestStarIndex == 45, "legacy best above a new floor seeds");
Reject(() => Seed(10, 5, new[] { W }), "best below index");

// Native medal projection from 50 three-star DBF ranks plus Legend.
var rows = Enumerable.Range(1, 51).Select(level => new LadderRankRow { StarLevel = level, Stars = level == 51 ? 0 : 3, Legend = level == 51 })
    .Reverse().ToArray();
var medal = Medal(At(44, multiplier: 4, streak: 2, best: 44), 7, rows);
Check(medal.LeagueId == 7 && medal.StarLevel == 15 && medal.Stars == 2 && medal.BestStarLevel == 15 && medal.StarsPerWin == 4
    && medal.Streak == 2 && !medal.IsLegend, "medal Silver 6 with 2 stars");
// One position, two native forms (client: 0-3 stars per rank, promotion on the win after 3/3).
var landed = Medal(At(21, streak: 1), 7, rows, arrivedByWin: true);
Check(landed.StarLevel == 7 && landed.Stars == 3 && landed.BestStarLevel == 7, "after a win: previous rank 3/3");
var dropped = Medal(At(33, best: 34), 7, rows, arrivedByWin: false);
Check(dropped.StarLevel == 12 && dropped.Stars == 0 && dropped.BestStarLevel == 12, "after a loss: rank 0/3");
Check(Medal(At(22), 7, rows, arrivedByWin: true).Stars == 1 && Medal(At(0), 7, rows, arrivedByWin: true).StarLevel == 1
    && Medal(At(0), 7, rows, arrivedByWin: true).Stars == 0, "only multiples of 3 above 0 change form");
Check(Medal(At(30), 7, rows, arrivedByWin: true).StarLevel == 10 && Medal(At(30), 7, rows).StarLevel == 11, "Silver 10 floor forms");
var legendMedal = Medal(legend.After, 7, rows, arrivedByWin: true);
Check(legendMedal.StarLevel == 51 && legendMedal.Stars == 0 && legendMedal.IsLegend && legendMedal.StarsPerWin == 1 && legendMedal.Streak == 0, "Legend medal");
Check(Medal(At(140, streak: 4), 7, rows).Streak == 0, "no streak flame from Diamond 5");
Reject(() => Medal(At(0), 7, rows.Take(50)), "missing Legend row");
Reject(() => Medal(At(0), 0, rows), "league id 0");
Reject(() => Medal(At(0), 7, rows.Select(r => new LadderRankRow { StarLevel = r.StarLevel, Stars = 2, Legend = r.Legend })), "non-three-star rank");

// Golden: a fixed sequence yields the same receipts, pinned.
string Golden()
{
    var pos = Fresh(); var lines = new List<string>();
    foreach (char c in "WWWLWTWWWLLWWWWWWLWWWWWWLW")
    {
        var step = Play(pos, c == 'W' ? W : c == 'L' ? L : T);
        lines.Add(FormattableString.Invariant($"{step.Before.StarIndex}>{step.After.StarIndex} x{step.After.Multiplier} s{step.After.StreakWins} r{step.After.Rating:F1} f[{string.Join(",", step.FloorsReached)}]"));
        pos = step.After;
    }
    return string.Join(";", lines);
}
Check(Golden() == Golden(), "deterministic receipts");
string golden = Golden();
Console.WriteLine("GOLDEN " + golden);
Check(golden == GoldenPin.Receipts, "golden receipts changed (rule change needs a new RuleVersion): " + golden);

// Explicit Standard pace must preserve every field of the old step, across all valid combinations.
bool SamePosition(LadderPosition a, LadderPosition b) => a.StarIndex == b.StarIndex && a.BestStarIndex == b.BestStarIndex
    && a.Multiplier == b.Multiplier && a.StreakWins == b.StreakWins && a.PlacementGames == b.PlacementGames
    && a.Rating == b.Rating && a.LegendReached == b.LegendReached;
bool SameStep(LadderStep a, LadderStep b) => SamePosition(a.Before, b.Before) && SamePosition(a.After, b.After)
    && a.Outcome == b.Outcome && a.OpponentRating == b.OpponentRating && a.Placement == b.Placement
    && a.StreakBonus == b.StreakBonus && a.StoppedAtDiamondFive == b.StoppedAtDiamondFive
    && a.LegendReachedNow == b.LegendReachedNow && a.StarsGained == b.StarsGained && a.FloorsReached.SequenceEqual(b.FloorsReached);
for (int index = 0; index <= LegendIndex; index++)
    foreach (var outcome in Enum.GetValues<LadderOutcome>())
        foreach (int streak in new[] { 0, 1, 2, 3, 10 })
            for (int multiplier = 1; multiplier <= (index >= DiamondFiveIndex ? 1 : MaximumMultiplier - FloorsReached(index)); multiplier++)
                foreach (int placement in new[] { 0, 9, PlacementLength })
                {
                    var position = At(index, multiplier, streak, placement: placement);
                    Check(SameStep(Apply(position, outcome, 1700), Apply(position, outcome, 1700, LadderPace.Standard)), "Standard pace preserves old step");
                }
for (int m = 1; m <= MaximumMultiplier; m++)
{
    Check(new LadderPace(0, 1, 1).Stars(m) == 1 && LadderPace.Standard.Stars(m) == m, "bonus star scale 0/1");
    Check(new LadderPace(1.5, 1, 1).Stars(m) == Math.Min(11, 1 + (int)Math.Round((m - 1) * 1.5, MidpointRounding.AwayFromZero)), "1.5 bonus and x11 cap");
    Check(new LadderPace(.25, 1, 1).Stars(m) == 1 + (int)Math.Round((m - 1) * .25, MidpointRounding.AwayFromZero), "quarter bonus rounding");
}
var fastPace = new LadderPace(1, 2, 1.5);
Check(Apply(At(148), W, StartRating, fastPace).After.StarIndex == LegendIndex
    && Apply(At(149), W, StartRating, fastPace).StarsGained == 1, "two-star Diamond wins cap at Legend");
Check(Medal(At(135), 7, rows, pace: fastPace).StarsPerWin == 2
    && Medal(At(20, 4), 7, rows, pace: GrindTiers.Relaxed.Ladder).StarsPerWin == 6, "medal shows next-win pace");
foreach (int index in new[] { 100, 149, 150 })
    foreach (int placement in new[] { 0, PlacementLength })
        foreach (var outcome in new[] { W, L })
        {
            var position = At(index, placement: placement);
            var normal = Apply(position, outcome, position.Rating);
            var scaled = Apply(position, outcome, position.Rating, fastPace);
            double factor = index == 150 && placement == PlacementLength ? 1.5 : 1;
            Check(scaled.After.Rating - position.Rating == (normal.After.Rating - position.Rating) * factor,
                "Legend K scales only settled Legend, never placement or promotion");
        }
foreach (double bad in new[] { -1d, double.NaN, double.NegativeInfinity, double.PositiveInfinity, 4.1 })
{
    Reject(() => new LadderPace(bad, 1, 1), "invalid bonus scale");
    Reject(() => new LadderPace(1, 1, bad), "invalid Legend scale");
}
Reject(() => new LadderPace(1, 1, 0), "zero Legend K");
Reject(() => new LadderPace(1, 0, 1), "zero top stars");
Reject(() => new LadderPace(1, 4, 1), "too many top stars");

// Table promises are tied to the shared synthetic-world model, with modest samples and bounded Legend search.
int oldBudget = TierClimbSimulation.LegendBudget;
TierClimbSimulation.LegendBudget = 250;
var legendHours = new List<double>(); var oneHours = new List<double>();
var paceErrors = new List<string>();
try
{
    // Pinned design-doc games, independent of collection display data. Retail shares Standard ladder pace.
    var legendGames = new[] { 101, 217, 440, 217 };
    var oneGames = new[] { 45, 70, 297, 70 };
    for (int i = 0; i < GrindTiers.Selectable.Count; i++)
    {
        var tier = GrindTiers.Selectable[i];
        double hours = TierClimbSimulation.Median(TierClimbSimulation.Run(TierClimbSimulation.ReferenceStrength, 60, tier.Ladder).ToLegend) * GrindTiers.MinutesPerGame / 60d;
        double one = TierClimbSimulation.Median(TierClimbSimulation.Run(TierClimbSimulation.TopStrength, 60, tier.Ladder).ToLegendOne) * GrindTiers.MinutesPerGame / 60d;
        if (Math.Abs(hours / (legendGames[i] * GrindTiers.MinutesPerGame / 60d) - 1) > .25) paceErrors.Add(tier.Name + " Legend hours " + hours + ", table " + (legendGames[i] * GrindTiers.MinutesPerGame / 60d));
        if (Math.Abs(one / (oneGames[i] * GrindTiers.MinutesPerGame / 60d) - 1) > .25) paceErrors.Add(tier.Name + " Legend #1 hours " + one + ", table " + (oneGames[i] * GrindTiers.MinutesPerGame / 60d));
        legendHours.Add(hours); oneHours.Add(one);
        Console.WriteLine($"PACE {tier.Id}: Legend {hours:F1} h; Legend #1 {one:F1} h");
    }
}
finally { TierClimbSimulation.LegendBudget = oldBudget; }
Check(legendHours[0] < legendHours[1] && legendHours[1] < legendHours[2]
    && oneHours[0] < oneHours[1] && oneHours[1] < oneHours[2], "tier hours ordered Relaxed < Standard < Grinder");

Check(ReferenceEquals(GrindTiers.Retail.Ladder, LadderPace.Standard)
    && legendHours[3] == legendHours[1] && oneHours[3] == oneHours[1], "Retail matches Standard ladder");

// Simulation bands (short seeded run; `--report` prints the full table).
ClimbSimulation.CheckBands(4000);
WorldClimbSimulation.Check(200);
Check(paceErrors.Count == 0, "Tier hours outside +/-25%: " + string.Join("; ", paceErrors));
Console.WriteLine("LADDER_RULES_TESTS PASS");

static class GoldenPin
{
    public const string Receipts =
        "0>6 x8 s1 r1480.0 f[];6>14 x9 s2 r1560.0 f[];14>32 x8 s3 r1640.0 f[15,30];32>31 x7 s0 r1560.0 f[];31>38 x8 s1 r1640.0 f[];38>38 x8 s1 r1640.0 f[];38>46 x7 s2 r1720.0 f[45];46>60 x7 s3 r1800.0 f[60];60>74 x7 s4 r1880.0 f[];74>73 x7 s0 r1800.0 f[];73>72 x6 s0 r1720.0 f[];72>78 x5 s1 r1732.0 f[75];78>83 x5 s2 r1744.0 f[];83>93 x4 s3 r1756.0 f[90];93>101 x4 s4 r1768.0 f[];101>109 x3 s5 r1780.0 f[105];109>115 x3 s6 r1792.0 f[];115>114 x3 s0 r1780.0 f[];114>117 x3 s1 r1792.0 f[];117>120 x2 s2 r1804.0 f[120];120>124 x2 s3 r1816.0 f[];124>128 x2 s4 r1828.0 f[];128>132 x2 s5 r1840.0 f[];132>135 x1 s6 r1852.0 f[135];135>135 x1 s0 r1840.0 f[];135>136 x1 s1 r1852.0 f[]";
}
