using System.Globalization;
using static LadderRules;

// Climb simulation against WORLD opponents (WP4): the real LadderMatchmaker picks every opponent from a synthetic NPC world
// (spec rating spread, see SyntheticNpcWorld). A reference player of fixed strength meets NPCs of their drifted rating:
// win probability = Elo expectation of (strength, opponent rating); the ladder updates with LadderRules.Apply against that rating.
// Targets (backlog #52 / NPC spec): the reference player reaches Legend in 150-250 games; win rates per band near the midpoints
// Bronze-Silver 67.5%, Gold-Platinum 57.5%, Diamond 52.5%, Legend 50% (within +-5 points).
static class WorldClimbSimulation
{
    public const double ReferenceStrength = 1575;   // 1550 (the NPC spec's figure) needs a median ~350 with the owner's harder D5+ field
    const int MaximumGames = 3000, LegendGames = 100;
    public static readonly string[] BandNames = { "Bronze-Silver", "Gold-Platinum", "Diamond 10-6", "Diamond 5-1", "Legend" };
    public static readonly double[] BandTargets = { 0.675, 0.575, 0.525, 0.525, 0.50 };
    public const double Tolerance = 0.05;
    public static QueueRules? RulesOverride;
    public static double[] StrengthsToReport = { 1450.0, 1550.0, 1575.0, 1650.0 };

    public sealed record Result(int[] ToLegend, int[] ToDiamond5, int[] ToSilver, int[] ToDiamond10, int Stuck, long[] Games, long[] Wins, double[] OpponentRating, double[] StarsPerGame, double[] HiddenRating, double PlacedMultiplier);

    public static double Invariant() { CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; return 0; }

    static int BandOf(int starIndex) => starIndex < 60 ? 0 : starIndex < 120 ? 1 : starIndex < DiamondFiveIndex ? 2 : starIndex < LegendIndex ? 3 : 4;

    public static Result Run(double strength, int climbs, int seed = 20261025, Func<NpcWorld>? worldFactory = null, QueueRules? rules = null)
    {
        Invariant();
        var world = (worldFactory ?? (() => SyntheticNpcWorld.World()))();
        var matchmaker = new LadderMatchmaker(world, rules ?? RulesOverride, SyntheticNpcWorld.StubProfile);
        var toLegend = new int[climbs]; var toDiamond5 = new int[climbs]; var toSilver = new int[climbs]; var toDiamond10 = new int[climbs];
        double placedSum = 0; var games = new long[5]; var wins = new long[5]; var opponentSum = new double[5]; var stars = new double[5]; var hiddenSum = new double[5];
        int stuck = 0, depth = matchmaker.Rules.HistoryDepth;
        for (int c = 0; c < climbs; c++)
        {
            long profileSeed = StableSeed.Derive(seed, c);
            var random = new Random(seed + c);
            var pos = Fresh();
            var history = new List<PastEncounter>();
            toLegend[c] = toDiamond5[c] = toSilver[c] = toDiamond10[c] = MaximumGames;
            int legendAt = -1;
            for (int game = 0; game < MaximumGames; game++)
            {
                if (pos.LegendReached && legendAt < 0) { legendAt = game; toLegend[c] = game; }
                if (legendAt >= 0 && game - legendAt >= LegendGames) break;
                int? number = pos.LegendReached ? matchmaker.Legend.LegendNumber(pos.Rating, game) : null;
                var request = new MatchRequest(profileSeed, game, pos.StarIndex, pos.Rating, number, history.Skip(Math.Max(0, history.Count - depth)));
                var encounter = matchmaker.Next(request);
                bool win = random.NextDouble() < Expected(strength, encounter.Rating);
                int band = BandOf(pos.StarIndex);
                games[band]++; opponentSum[band] += encounter.Rating; hiddenSum[band] += pos.Rating; if (win) wins[band]++;
                var step = Apply(pos, win ? LadderOutcome.Win : LadderOutcome.Loss, encounter.Rating);
                stars[band] += step.StarsGained;
                pos = step.After;
                if (game + 1 == PlacementLength) placedSum += StartMultiplier(pos.Rating);
                history.Add(new PastEncounter(encounter.Npc.Id, encounter.Deck.Id, encounter.Deck.Tags.Archetype, win ? "Won" : "Lost"));
                if (pos.StarIndex >= SilverTenIndex && toSilver[c] == MaximumGames) toSilver[c] = game + 1;
                if (pos.StarIndex >= 120 && toDiamond10[c] == MaximumGames) toDiamond10[c] = game + 1;
                if (pos.StarIndex >= DiamondFiveIndex && toDiamond5[c] == MaximumGames) toDiamond5[c] = game + 1;
            }
            if (legendAt < 0) stuck++;
        }
        for (int b = 0; b < 5; b++) if (games[b] > 0) { opponentSum[b] /= games[b]; hiddenSum[b] /= games[b]; }
        return new Result(toLegend, toDiamond5, toSilver, toDiamond10, stuck, games, wins, opponentSum, stars.Select((s, b) => games[b] == 0 ? 0 : s / games[b]).ToArray(), hiddenSum, placedSum / climbs);
    }

    static int Median(int[] values) { var s = values.OrderBy(v => v).ToArray(); return s[s.Length / 2]; }
    static double Rate(Result r, int band) => r.Games[band] == 0 ? double.NaN : (double)r.Wins[band] / r.Games[band];

    public static void Report(int climbs)
    {
        Invariant();
        var rules = RulesOverride ?? QueueRules.Default;
        Console.WriteLine($"World climb simulation (synthetic NPC world, {climbs} climbs per row, matchmaker offsets Bronze-Silver {rules.BronzeSilverOffset:F1}," +
            $" Gold-Platinum {rules.GoldPlatinumOffset:F1}, Diamond {rules.DiamondOffset:F1}, D5+ field offset {rules.FieldOffset:F1})");
        Console.WriteLine($"{"strength",-9} {"to D5 (med)",-12} {"to Legend (med/p10/p90)",-26} stuck  win% BS / GP / D10-6 / D5-1 / L   mean opp BS / GP / D10-6 / D5-1 / L   mean hidden   placed x   stars/game BS / GP / D10-6 / D5-1 / L");
        foreach (double strength in StrengthsToReport)
        {
            var r = Run(strength, climbs);
            var sorted = r.ToLegend.OrderBy(v => v).ToArray();
            Console.WriteLine($"{strength,-9} S{Median(r.ToSilver)}/D10 {Median(r.ToDiamond10)}/D5 {Median(r.ToDiamond5),-12} {Median(r.ToLegend)}/{sorted[sorted.Length / 10]}/{sorted[sorted.Length * 9 / 10],-14} {r.Stuck,5}  " +
                string.Join(" / ", Enumerable.Range(0, 5).Select(b => (Rate(r, b) * 100).ToString("F1"))) + "   " +
                string.Join(" / ", r.OpponentRating.Select(o => o.ToString("F0"))) + "   " + string.Join(" / ", r.HiddenRating.Select(o => o.ToString("F0"))) + "   " + r.PlacedMultiplier.ToString("F1") + "   " + string.Join(" / ", r.StarsPerGame.Take(5).Select(o => o.ToString("F2"))));
        }
    }

    // Backlog #52 bands: Bronze-Silver, Gold-Platinum, Diamond (10-6 and 5-1 together), Legend; midpoint +- Tolerance.
    static readonly string[] CheckNames = { "Bronze-Silver", "Gold-Platinum", "Diamond", "Legend" };
    static readonly double[] CheckTargets = { 0.675, 0.575, 0.525, 0.50 };

    public static void Check(int climbs)
    {
        Invariant();
        var r = Run(ReferenceStrength, climbs);
        int median = Median(r.ToLegend);
        if (median < 150 || median > 250 || r.Stuck > climbs / 50)
            throw new Exception($"world reference median games to Legend {median} outside 150-250 (stuck {r.Stuck}/{climbs}, limit 2%)");
        var rates = new[] { Rate(r, 0), Rate(r, 1), (double)(r.Wins[2] + r.Wins[3]) / (r.Games[2] + r.Games[3]), Rate(r, 4) };
        for (int b = 0; b < 4; b++)
            if (double.IsNaN(rates[b]) || Math.Abs(rates[b] - CheckTargets[b]) > Tolerance)
                throw new Exception($"world reference {CheckNames[b]} win rate {rates[b]:P1} is not within {Tolerance * 100:F0} points of {CheckTargets[b]:P1}");
        // Owner rule: the fixed Diamond 5-1 field is never softer than Diamond 10-6 (mean opponent rating), reported separately.
        double d106 = r.OpponentRating[2], d51 = r.OpponentRating[3];
        if (d51 < d106)
            throw new Exception($"Diamond 5-1 field is softer than Diamond 10-6: mean opponent {d51:F0} < {d106:F0}");
        // 1550 (the NPC spec's figure) must still finish: stuck <= 2%.
        var slow = Run(1550, Math.Max(100, climbs / 2));
        if (slow.Stuck > slow.ToLegend.Length / 50) throw new Exception($"1550 player stuck at Legend: {slow.Stuck}/{slow.ToLegend.Length}");
        // Monotone in strength: a stronger player never needs more games (same seeds), a weaker one is not locked out of Bronze.
        var strong = Run(1650, Math.Max(40, climbs / 4)); var weak = Run(1450, Math.Max(40, climbs / 4));
        if (Median(strong.ToLegend) >= median) throw new Exception($"strong player median {Median(strong.ToLegend)} not below reference {median}");
        if (Median(weak.ToLegend) < 2 * median) throw new Exception($"Diamond should be a wall for a 1450 player (median {Median(weak.ToLegend)} vs reference {median})");
        if (Median(weak.ToSilver) > 60) throw new Exception("weak player stuck in Bronze (median " + Median(weak.ToSilver) + ")");
        Console.WriteLine($"WORLD_SIMULATION reference {ReferenceStrength} median to Legend {median} (stuck {r.Stuck}), win rates " +
            string.Join(" / ", Enumerable.Range(0, 4).Select(b => $"{CheckNames[b]} {rates[b]:P1}")) +
            $"; Diamond 10-6 win {Rate(r, 2):P1} opp {d106:F0}, Diamond 5-1 win {Rate(r, 3):P1} opp {d51:F0}" +
            $"; 1550 median {Median(slow.ToLegend)} (stuck {slow.Stuck}), 1650 median {Median(strong.ToLegend)}, 1450 median {Median(weak.ToLegend)} (stuck {weak.Stuck}/{weak.ToLegend.Length})");
    }
}
