using static LadderRules;

// Seeded climb simulation. Win rates per band (Bronze–Silver, Gold–Platinum, Diamond) are the player model;
// the opponent is rated at the player's hidden rating (v1 placeholder until NPC ratings exist, spec: NPCs near R).
static class ClimbSimulation
{
    public sealed record Player(string Name, double BronzeSilver, double GoldPlatinum, double Diamond);
    public sealed record Result(int[] ToSilver, int[] ToDiamond10, int[] ToDiamond5, int[] ToLegend, int[] PlacedMultiplier, int Stuck);

    public static readonly Player Reference = new("reference", 0.675, 0.575, 0.525); // midpoints of the NPC spec's targets
    public static readonly Player Strong = new("strong", 0.80, 0.70, 0.60);
    public static readonly Player Weak = new("weak", 0.45, 0.45, 0.45);
    const int MaximumGames = 3000;

    static double WinRate(Player player, int index) =>
        index < 60 ? player.BronzeSilver : index < 120 ? player.GoldPlatinum : player.Diamond;

    // startMultiplier null = product rules from a fresh profile (placement); a value = the spec's fixed-start table.
    public static Result Run(Player player, int climbs, int? startMultiplier, int seed = 20261024)
    {
        var silver = new int[climbs]; var d10 = new int[climbs]; var d5 = new int[climbs]; var legend = new int[climbs];
        var placed = new int[climbs]; int stuck = 0;
        for (int i = 0; i < climbs; i++)
        {
            var random = new Random(seed + i);
            var pos = startMultiplier is int m
                ? new LadderPosition { Rating = StartRating, Multiplier = m, PlacementGames = PlacementLength }
                : Fresh();
            silver[i] = d10[i] = d5[i] = legend[i] = MaximumGames;
            placed[i] = startMultiplier ?? 0;
            for (int game = 1; game <= MaximumGames && !pos.LegendReached; game++)
            {
                var outcome = random.NextDouble() < WinRate(player, pos.StarIndex) ? LadderOutcome.Win : LadderOutcome.Loss;
                pos = Apply(pos, outcome, pos.Rating).After;
                if (game == PlacementLength && startMultiplier == null) placed[i] = StartMultiplier(pos.Rating);
                if (pos.StarIndex >= SilverTenIndex) silver[i] = Math.Min(silver[i], game);
                if (pos.StarIndex >= 120) d10[i] = Math.Min(d10[i], game);
                if (pos.StarIndex >= DiamondFiveIndex) d5[i] = Math.Min(d5[i], game);
                if (pos.LegendReached) legend[i] = game;
            }
            if (!pos.LegendReached) stuck++;
        }
        return new Result(silver, d10, d5, legend, placed, stuck);
    }

    static double Mean(int[] values) => values.Average();
    static int Quantile(int[] values, double q) { var s = values.OrderBy(v => v).ToArray(); return s[(int)Math.Min(s.Length - 1, q * s.Length)]; }
    static string Stats(int[] values) => $"{Mean(values),6:F0} {Quantile(values, 0.5),6} {Quantile(values, 0.1),6} {Quantile(values, 0.9),6}";

    public static void Report(int climbs)
    {
        Console.WriteLine($"Ladder climb simulation, rule version {RuleVersion}, {climbs} climbs per row, seed 20261024");
        Console.WriteLine($"Rating: start {StartRating}, placement {PlacementLength} games K={PlacementK}, then K={SettledK}; opponent = player's rating");
        Console.WriteLine($"Games: mean median p10 p90. Capped at {MaximumGames}: unfinished climbs count as {MaximumGames}, so rows with");
        Console.WriteLine("'unfinished' > 0 are lower bounds (censored), not completion times.");
        Console.WriteLine("\nSpec cross-check (fixed start multiplier, reference win rates 67.5/57.5/52.5):");
        Console.WriteLine($"{"start",-10} {"to Diamond 5",-28} {"to Legend",-28}");
        foreach (int m in new[] { 10, 8, 5, 2 })
        {
            var r = Run(Reference, climbs, m);
            Console.WriteLine($"x{m,-9} {Stats(r.ToDiamond5),-28} {Stats(r.ToLegend),-28} unfinished {r.Stuck}");
        }
        Console.WriteLine("\nProduct rules from a fresh profile (placement sets the multiplier):");
        Console.WriteLine($"{"player",-10} {"rates",-16} {"leave Bronze",-28} {"to Diamond 10",-28} {"to Diamond 5",-28} {"to Legend",-28} {"placed x (mean)",-15} unfinished");
        foreach (var player in new[] { Strong, Reference, Weak })
        {
            var r = Run(player, climbs, null);
            Console.WriteLine($"{player.Name,-10} {player.BronzeSilver:P1}/{player.GoldPlatinum:P1}/{player.Diamond:P1} " +
                $"{Stats(r.ToSilver),-28} {Stats(r.ToDiamond10),-28} {Stats(r.ToDiamond5),-28} {Stats(r.ToLegend),-28} {r.PlacedMultiplier.Average(),-15:F1} {r.Stuck}");
        }
        Console.WriteLine("\nPlacement multiplier distribution (reference):");
        var reference = Run(Reference, climbs, null);
        foreach (var group in reference.PlacedMultiplier.GroupBy(v => v).OrderBy(g => g.Key))
            Console.WriteLine($"x{group.Key,-3} {group.Count() * 100.0 / climbs,5:F1}%  median to Legend {Quantile(reference.ToLegend.Where((_, i) => reference.PlacedMultiplier[i] == group.Key).ToArray(), 0.5)}");
    }

    // Spec targets: reference 150-250 games to Legend, strong well under 60 through Platinum, weak not stuck in Bronze.
    public static void CheckBands(int climbs)
    {
        var reference = Run(Reference, climbs, null);
        int median = Quantile(reference.ToLegend, 0.5);
        if (median < 150 || median > 250 || reference.Stuck > climbs / 100)
            throw new Exception($"reference median games to Legend {median} outside 150-250 (stuck {reference.Stuck})");
        var strong = Run(Strong, climbs, null);
        if (Mean(strong.ToDiamond10) >= 60) throw new Exception($"strong player mean games through Platinum {Mean(strong.ToDiamond10):F0} >= 60");
        var weak = Run(Weak, climbs, null);
        if (weak.ToSilver.Max() >= MaximumGames || Quantile(weak.ToSilver, 0.5) > 60)
            throw new Exception($"weak player stuck in Bronze (median {Quantile(weak.ToSilver, 0.5)}, max {weak.ToSilver.Max()})");
        Console.WriteLine($"SIMULATION reference median to Legend {median}, strong mean through Platinum {Mean(strong.ToDiamond10):F0}, " +
            $"weak median leaves Bronze {Quantile(weak.ToSilver, 0.5)}");
    }
}
