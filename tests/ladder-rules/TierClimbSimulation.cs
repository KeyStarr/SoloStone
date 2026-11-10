using System.Globalization;
using static LadderRules;

// Grind tier climb model (docs/design/2026-10-31-grind-tiers.md): the world climb of WorldClimbSimulation under a LadderPace,
// continued in Legend until the player first holds Legend #1 (1 + field NPCs above the hidden rating). Same synthetic world,
// matchmaker and Elo win model; reports games, which tools/grind_pace.py turns into hours with the minutes-per-game figure.
static class TierClimbSimulation
{
    const int MaximumGames = 4000;
    // Design-doc ladder figures: Legend for the ladder's reference player, Legend #1 for a player who beats the top of the field.
    public const double ReferenceStrength = WorldClimbSimulation.ReferenceStrength, TopStrength = 1900;
    // Legend games searched for #1 after arrival; a player who has not found it by then is counted stuck.
    public static int LegendBudget = 1500;

    public sealed record Result(int[] ToLegend, int[] ToLegendOne, int StuckLegend, int StuckLegendOne);

    public static Result Run(double strength, int climbs, LadderPace pace, int seed = 20261031)
    {
        WorldClimbSimulation.Invariant();
        var world = SyntheticNpcWorld.World();
        var matchmaker = new LadderMatchmaker(world, null, SyntheticNpcWorld.StubProfile);
        var toLegend = new int[climbs]; var toOne = new int[climbs];
        int stuckLegend = 0, stuckOne = 0, depth = matchmaker.Rules.HistoryDepth;
        for (int c = 0; c < climbs; c++)
        {
            long profileSeed = StableSeed.Derive(seed, c);
            var random = new Random(seed + c);
            var pos = Fresh();
            var history = new List<PastEncounter>();
            toLegend[c] = toOne[c] = MaximumGames;
            for (int game = 0; game < MaximumGames; game++)
            {
                int? number = pos.LegendReached ? matchmaker.Legend.LegendNumber(pos.Rating, game) : null;
                if (pos.LegendReached && toLegend[c] == MaximumGames) toLegend[c] = game;
                if (number == 1) { toOne[c] = game; break; }
                if (pos.LegendReached && game - toLegend[c] >= LegendBudget) break;
                var request = new MatchRequest(profileSeed, game, pos.StarIndex, pos.Rating, number, history.Skip(Math.Max(0, history.Count - depth)));
                var encounter = matchmaker.Next(request);
                bool win = random.NextDouble() < Expected(strength, encounter.Rating);
                pos = Apply(pos, win ? LadderOutcome.Win : LadderOutcome.Loss, encounter.Rating, pace).After;
                history.Add(new PastEncounter(encounter.Npc.Id, encounter.Deck.Id, encounter.Deck.Tags.Archetype, win ? "Won" : "Lost"));
            }
            if (toLegend[c] == MaximumGames) stuckLegend++;
            if (toOne[c] == MaximumGames) stuckOne++;
        }
        return new Result(toLegend, toOne, stuckLegend, stuckOne);
    }

    public static int Median(int[] values) { var s = values.OrderBy(v => v).ToArray(); return s[s.Length / 2]; }

    // --tier-report climbs bonus:top:k ... [@strength ...]: one row per (pace, strength), median/p10/p90 games, stuck counts.
    public static void Report(int climbs, IEnumerable<LadderPace> paces, IEnumerable<double> strengths)
    {
        WorldClimbSimulation.Invariant();
        Console.WriteLine("pace(bonus:top:k)  strength  toLegend med/p10/p90  stuck  toLegend#1 med/p10/p90  stuck");
        foreach (var pace in paces)
            foreach (double strength in strengths)
            {
                var r = Run(strength, climbs, pace);
                var l = r.ToLegend.OrderBy(v => v).ToArray(); var o = r.ToLegendOne.OrderBy(v => v).ToArray();
                Console.WriteLine($"{pace.BonusStarScale}:{pace.TopStarsPerWin}:{pace.LegendKScale,-10} {strength,-8}  " +
                    $"{l[l.Length / 2]}/{l[l.Length / 10]}/{l[l.Length * 9 / 10],-14} {r.StuckLegend,5}  {o[o.Length / 2]}/{o[o.Length / 10]}/{o[o.Length * 9 / 10],-16} {r.StuckLegendOne,5}");
            }
    }

    public static LadderPace Parse(string text)
    {
        var parts = text.Split(':');
        return new LadderPace(double.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture),
            double.Parse(parts[2], CultureInfo.InvariantCulture));
    }
}
