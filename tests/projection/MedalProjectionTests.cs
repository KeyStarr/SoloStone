// Executes the production DTO builder; native DTO presence semantics and world lookup are isolated boundaries.
internal static class MedalProjectionTests
{
    internal static void Run()
    {
        var calls = new List<(double Rating, int Games)>();
        int? rank = 42;
        NpcWorldRuntime.Lookup = (rating, games) => { calls.Add((rating, games)); return rank; };
        var numbers = new LadderMedal { IsLegend = true, LeagueId = 7, StarLevel = 151, BestStarLevel = 151,
            Stars = 0, StarsPerWin = 1, Streak = 2 };
        NetCache.NetCacheMedalInfo Verify(double? rating, int games, int wins, int? expected)
        {
            var packet = NativeUIProbe.ProjectMedal(numbers, rating, games, wins);
            Require(packet.MedalData.Count == 2, "both native formats receive medals");
            foreach (var entry in packet.MedalData) {
                var data = entry.Value;
                Require(data.FormatType == entry.Key && data.LeagueId == numbers.LeagueId && data.StarLevel == numbers.StarLevel
                    && data.BestStarLevel == numbers.BestStarLevel && data.Stars == numbers.Stars && data.StarsPerWin == numbers.StarsPerWin
                    && data.Streak == numbers.Streak && data.SeasonGames == games && data.SeasonWins == wins,
                    "shared medal builder preserves rank, stars and history counters");
                Require(data.HasLegendRank == expected.HasValue && (!expected.HasValue || data.LegendRank == expected.Value),
                    "native Legend rank presence and value match the world projection");
            }
            return packet;
        }
        var completeLegend = Verify(1800, 20, 12, 42); // Ordinary Legend, before settlement.
        Require(NativeUIProbe.CompleteGameplayMedal(numbers, completeLegend, FormatType.FT_STANDARD),
            "gameplay Legend medal accepts a deterministic number");
        rank = 47; Verify(1790, 21, 12, 47); // Receipt after a loss uses its own rating/count.
        rank = 1; Verify(5000, 0, 0, 1); // Seeded Legend with zero games is still displayable.
        Require(calls.SequenceEqual(new[] { (1800d, 20), (1790d, 21), (5000d, 0) }),
            "Legend lookup uses each supplied receipt rating and match count once");
        Verify(null, 2, 1, null); // Legacy receipt has no trustworthy rating.
        numbers.IsLegend = false; var ordinary = Verify(5000, 0, 0, null);
        Require(NativeUIProbe.CompleteGameplayMedal(numbers, ordinary, FormatType.FT_STANDARD),
            "non-Legend gameplay medal does not require a Legend number");
        Require(calls.Count == 3, "non-Legend and missing-rating medals do not query world ranking");
        numbers.IsLegend = true; rank = null; var incompleteLegend = Verify(1800, 20, 12, null);
        Require(!NativeUIProbe.CompleteGameplayMedal(numbers, incompleteLegend, FormatType.FT_STANDARD),
            "missing world ranking fails a gameplay Legend medal closed");
        Require(NativeUIProbe.OwnsGameplayRank("Starting", "match", "match", "match")
            && NativeUIProbe.OwnsGameplayRank("Playing", "match", "match", "match"),
            "owned starting and playing match projects gameplay rank");
        Require(!NativeUIProbe.OwnsGameplayRank("Menu", "match", "match", "match")
            && !NativeUIProbe.OwnsGameplayRank("Result", "match", "match", "match")
            && !NativeUIProbe.OwnsGameplayRank("Playing", "", "", "")
            && !NativeUIProbe.OwnsGameplayRank("Playing", "match", "stale", "match")
            && !NativeUIProbe.OwnsGameplayRank("Playing", "match", "match", "stale"),
            "non-gameplay empty and stale matches keep native rank");
        var legendOpponent = NativeUIProbe.ProjectOpponentLadder(1825);
        Require(legendOpponent.StarIndex == LadderRules.LegendIndex && legendOpponent.BestStarIndex == LadderRules.LegendIndex
            && legendOpponent.LegendReached && legendOpponent.Rating == 1825,
            "Legend opponent derives from frozen pending rating");
        var goldOpponent = NativeUIProbe.ProjectOpponentLadder(1300);
        Require(goldOpponent.StarIndex == 75 && goldOpponent.BestStarIndex == 75 && !goldOpponent.LegendReached,
            "non-Legend opponent derives retail rank index from frozen pending rating");
        try { NativeUIProbe.ProjectOpponentLadder(double.NaN); throw new Exception("invalid opponent rating accepted"); }
        catch (ArgumentException) { }
        // NPC opponent beside a Legend player: the world numbers it with the player counted (NpcLegendField.OpponentLegendNumber;
        // real-field cases in tests/npc-world MatchmakerSuite.LegendNumbersNeverCollide).
        var opponentCalls = new List<(double Rating, int Games, double? Player)>();
        NpcWorldRuntime.OpponentLookup = (rating, games, player) => { opponentCalls.Add((rating, games, player)); return 30; };
        rank = 29; calls.Clear();
        int? Opp(double npc, double? player) {
            var data = NativeUIProbe.ProjectMedal(numbers, npc, 5, 3, player).MedalData[FormatType.FT_STANDARD];
            return data.HasLegendRank ? data.LegendRank : (int?)null;
        }
        Require(Opp(1745, 1750) == 30 && opponentCalls.SequenceEqual(new (double, int, double?)[] { (1745, 5, 1750) }) && calls.Count == 0,
            "opponent beside a Legend player takes the world's opponent number");
        Require(Opp(1745, null) == 29 && opponentCalls.Count == 1 && calls.SequenceEqual(new[] { (1745d, 5) }),
            "player outside Legend: plain Legend number");
        NpcWorldRuntime.OpponentLookup = null;
        NpcWorldRuntime.Lookup = null;
        Console.WriteLine("PASS native gameplay medal projection: ranks, match ownership, opponent snapshot, both formats");
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}

internal enum FormatType { FT_STANDARD, FT_WILD }
internal static class NetCache
{
    internal sealed class NetCacheMedalInfo { internal readonly Dictionary<FormatType, PegasusUtil.MedalInfoData> MedalData = new(); }
}
internal static partial class PegasusUtil
{
    internal sealed class MedalInfoData
    {
        internal FormatType FormatType;
        internal int LeagueId, StarLevel, BestStarLevel, Stars, StarsPerWin, Streak, SeasonId, SeasonGames, SeasonWins;
        internal bool HasLegendRank;
        private int legendRank;
        // Pinned blizzard.bgsclient MedalInfoData contract.
        internal int LegendRank { get => legendRank; set { legendRank = value; HasLegendRank = true; } }
    }
}
internal static class NpcWorldRuntime
{
    internal static Func<double, int, int?> Lookup;
    internal static int? LegendNumber(double rating, int games) => Lookup(rating, games);
    internal static Func<double, int, double?, int?> OpponentLookup;
    internal static int? OpponentLegendNumber(double rating, int games, double? player) => OpponentLookup(rating, games, player);
}
