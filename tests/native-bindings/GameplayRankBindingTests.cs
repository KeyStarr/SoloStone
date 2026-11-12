using System.Reflection;

namespace NativeGameplayRankBoundary;

internal static class GameplayRankBindingTests
{
    private const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;
    private static MethodInfo Hook(string name) => typeof(NativeGameplayRank).GetMethod(name, Hidden);
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception("gameplay rank: " + message);
    }

    private static bool Ranked(bool native)
    {
        object[] args = { native };
        Hook("ProjectRankedGame").Invoke(null, args);
        return (bool)args[0];
    }

    private static MedalInfoTranslator Rank(Player player, MedalInfoTranslator native)
    {
        object[] args = { player, native };
        Hook("ProjectPlayerRank").Invoke(null, args);
        return (MedalInfoTranslator)args[1];
    }

    internal static void Run()
    {
        Harmony.Patches.Clear();
        var log = new ManualLogSource();
        NativeGameplayRank.Initialize(new Harmony(), log);
        Check(Harmony.Patches.Count == 2
            && Harmony.Patches.Any(p => p.Original.Name == nameof(GameUtils.IsGameTypeRanked) && p.Postfix.Method.Name == "ProjectRankedGame")
            && Harmony.Patches.Any(p => p.Original.Name == nameof(Player.GetRank) && p.Postfix.Method.Name == "ProjectPlayerRank"),
            "registers exact ranked predicate and Player.GetRank postfixes");

        var profile = new LocalProfile {
            Pending = new LocalMatchRecord { Id = "match", NpcRating = 1825, GrindTier = "grinder" },
            Ladder = new LadderPosition { StarIndex = 150, BestStarIndex = 150, Rating = 1860.696, LegendReached = true },
            History = new List<MatchRow> { new() { Result = "Won" }, new() { Result = "Lost" } },
            LadderArrivedByWin = true
        };
        LocalLadder.Profile = profile;
        LocalLadder.Screen = "Starting";
        LocalLadder.MatchId = "match";
        ClientBridge.DiagnosticMatchId = "match";
        NativeUIProbe.WorldAvailable = true;
        NativeUIProbe.ShowOpponent = true;

        Check(Ranked(false) && Ranked(true), "owned Starting match enables ranked path without clearing native true");
        var native = new MedalInfoTranslator();
        var friendly = Rank(new Player(Player.Side.FRIENDLY), native);
        Check(!ReferenceEquals(friendly, native) && friendly.IsDisplayable()
            && friendly.GetCurrentMedal(FormatType.FT_STANDARD).legendIndex == 9
            && friendly.Packet.Rating == 1860.696 && friendly.Packet.Games == 2 && friendly.Packet.Wins == 1
            && friendly.Packet.PlayerLegendRating == null,
            "friendly Player.GetRank receives displayable durable Legend #9 translator");
        Check(ReferenceEquals(LadderRules.LastPace, GrindTiers.Find("grinder").Ladder) && !ReferenceEquals(LadderRules.LastPace, profile.GrindTier.Ladder),
            "in-match medal uses the pending match's stamped pace, not a later change");
        var opponent = Rank(new Player(Player.Side.OPPOSING), native);
        Check(!ReferenceEquals(opponent, native) && opponent.IsDisplayable()
            && opponent.GetCurrentMedal(FormatType.FT_STANDARD).legendIndex == 13
            && opponent.Packet.Rating == 1825 && opponent.Packet.Games == 2 && opponent.Packet.Wins == 0
            && opponent.Packet.PlayerLegendRating == 1860.696,
            "opponent Player.GetRank receives displayable pending-snapshot Legend #13 translator, numbered beside the Legend player");
        profile.Ladder.LegendReached = false;
        Check(Rank(new Player(Player.Side.OPPOSING), native).Packet.PlayerLegendRating == null,
            "a player outside Legend does not shift the opponent's number");
        profile.Ladder.LegendReached = true;

        NativeUIProbe.ShowOpponent = false;
        var hiddenByNative = Rank(new Player(Player.Side.FRIENDLY), native);
        Check(!hiddenByNative.GetCurrentMedal(FormatType.FT_STANDARD).RankConfig.ShowOpponentRankInGame,
            "translator preserves native false opponent-disclosure row");
        NativeUIProbe.ShowOpponent = true;
        Check(Rank(new Player(Player.Side.FRIENDLY), native).GetCurrentMedal(FormatType.FT_STANDARD).RankConfig.ShowOpponentRankInGame,
            "translator preserves native true opponent-disclosure row");

        profile.Pending.NpcRating = null;
        Check(ReferenceEquals(Rank(new Player(Player.Side.OPPOSING), native), native),
            "missing opponent rating keeps native translator");
        profile.Pending.NpcRating = 1300;
        var ordinary = Rank(new Player(Player.Side.OPPOSING), native);
        Check(ordinary.IsDisplayable() && ordinary.GetCurrentMedal(FormatType.FT_STANDARD).starLevel == 75
            && ordinary.GetCurrentMedal(FormatType.FT_STANDARD).legendIndex == 0,
            "non-Legend opponent receives ordinary native rank translator");
        profile.Pending.NpcRating = double.NaN;
        Check(ReferenceEquals(Rank(new Player(Player.Side.OPPOSING), native), native) && log.Warnings.Count == 1,
            "invalid opponent snapshot fails closed to native translator");
        profile.Pending.NpcRating = 1825;

        NativeUIProbe.WorldAvailable = false;
        Check(ReferenceEquals(Rank(new Player(Player.Side.FRIENDLY), native), native),
            "missing deterministic Legend field keeps native translator");
        NativeUIProbe.WorldAvailable = true;
        LocalLadder.Screen = "Menu";
        Check(!Ranked(false) && ReferenceEquals(Rank(new Player(Player.Side.FRIENDLY), native), native),
            "menu keeps native ranked result and translator");
        LocalLadder.Screen = "Playing";
        ClientBridge.DiagnosticMatchId = "stale";
        Check(!Ranked(false) && ReferenceEquals(Rank(new Player(Player.Side.FRIENDLY), native), native),
            "stale transport match keeps native results");
        ClientBridge.DiagnosticMatchId = "match";
        profile.Pending.Mode = "Casual";
        Check(!Ranked(false) && ReferenceEquals(Rank(new Player(Player.Side.FRIENDLY), native), native)
            && ReferenceEquals(Rank(new Player(Player.Side.OPPOSING), native), native),
            "Casual match keeps the native unranked result and translators");
        profile.Pending.Mode = null;
        Check(ReferenceEquals(Rank(new Player(Player.Side.INVALID), native), native),
            "unknown player side keeps native translator");
        Check(log.Infos.Any(line => line.Contains("side=FRIENDLY") && line.Contains("legend=9") && line.Contains("showOpponent=True"))
            && log.Infos.Any(line => line.Contains("side=OPPOSING") && line.Contains("legend=13")),
            "production hook emits both projected native rows");
        Console.WriteLine("PASS gameplay rank native binding: hooks, both Players, native disclosure, stale and missing data");
    }
}

internal sealed class Harmony
{
    internal static readonly List<(MethodInfo Original, HarmonyMethod Postfix)> Patches = new();
    internal void Patch(MethodInfo original, HarmonyMethod prefix = null, HarmonyMethod postfix = null) => Patches.Add((original, postfix));
}
internal sealed class HarmonyMethod
{
    internal MethodInfo Method { get; }
    internal HarmonyMethod(Type type, string name) => Method = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
}
internal static class AccessTools
{
    internal static MethodInfo Method(Type type, string name, Type[] args) => type.GetMethod(name,
        BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null);
}
internal sealed class ManualLogSource
{
    internal readonly List<string> Infos = new();
    internal readonly List<string> Warnings = new();
    internal void LogInfo(string value) => Infos.Add(value);
    internal void LogWarning(string value) => Warnings.Add(value);
}
internal static class GameUtils { internal static bool IsGameTypeRanked() => false; }
internal sealed class Player
{
    internal enum Side { INVALID, FRIENDLY, OPPOSING }
    private readonly Side side;
    internal Player(Side value) => side = value;
    internal Side GetSide() => side;
    internal MedalInfoTranslator GetRank() => new();
}
internal static class LocalLadder
{
    internal static LocalProfile Profile;
    internal static string Screen;
    internal static string MatchId;
}
internal static class ClientBridge { internal static string DiagnosticMatchId; }
internal sealed class LocalProfile
{
    internal LocalMatchRecord Pending;
    internal LadderPosition Ladder;
    internal List<MatchRow> History = new();
    internal List<MatchRow> RankedHistory => History;
    internal bool LadderArrivedByWin;
    internal GrindTier GrindTier = new();
}
internal sealed class GrindTier { internal LadderPace Ladder = new(); }
internal sealed class LadderPace { }
internal sealed class LocalMatchRecord { internal string Id; internal double? NpcRating; internal string Mode; internal string GrindTier; }
internal static class GrindTiers
{
    private static readonly Dictionary<string, GrindTier> Rows = new();
    internal static GrindTier Find(string id) { id ??= "standard"; if (!Rows.TryGetValue(id, out var tier)) Rows[id] = tier = new GrindTier(); return tier; }
}
internal static class CasualRules { internal static bool IsCasual(string mode) => mode == "Casual"; }
internal sealed class MatchRow { internal string Result; }
internal sealed class LadderPosition { internal int StarIndex, BestStarIndex; internal double Rating; internal bool LegendReached; }
internal sealed class LadderRankRow { internal int StarLevel, Stars; internal bool Legend; }
internal sealed class LadderMedal { internal bool IsLegend; internal int LeagueId, StarLevel; }
internal static class LadderRules
{
    internal const int LegendIndex = 150;
    internal static int IndexForRating(double rating) => rating >= 1500 ? LegendIndex : Math.Max(0, Math.Min(LegendIndex, (int)((rating - 1000) / 4)));
    internal static LadderPace LastPace;
    internal static LadderMedal Medal(LadderPosition position, int league, IEnumerable<LadderRankRow> rows, bool arrivedByWin, LadderPace pace)
    {
        LastPace = pace;
        return new() { IsLegend = position.LegendReached, LeagueId = league, StarLevel = position.LegendReached ? rows.Max(row => row.StarLevel) : position.StarIndex };
    }
}
internal sealed class LeagueDbfRecord { internal int ID; internal List<LeagueRankDbfRecord> Ranks = new(); }
internal sealed class LeagueRankDbfRecord { internal int StarLevel, Stars; }
internal enum FormatType { FT_STANDARD, FT_WILD }
internal sealed class MedalPacket
{
    internal bool HasLegendRank;
    internal int LegendRank, LeagueId, StarLevel, Games, Wins;
    internal double Rating;
    internal double? PlayerLegendRating;
}
internal static class NativeUIProbe
{
    internal const FormatType LocalDeckFormat = FormatType.FT_STANDARD;
    internal static bool WorldAvailable = true, ShowOpponent = true;
    internal static bool OwnsGameplayRank(string screen, string active, string pending, string transport) =>
        (screen == "Starting" || screen == "Playing") && !string.IsNullOrEmpty(active) && active == pending && active == transport;
    internal static LadderPosition ProjectOpponentLadder(double rating)
    {
        if (double.IsNaN(rating) || double.IsInfinity(rating)) throw new ArgumentException();
        int index = LadderRules.IndexForRating(rating);
        return new LadderPosition { StarIndex = index, BestStarIndex = index, Rating = rating, LegendReached = index == LadderRules.LegendIndex };
    }
    internal static LeagueDbfRecord ResolveMedalLeague(LadderPosition ladder, out LadderMedal projected)
    {
        projected = null;
        return new LeagueDbfRecord { ID = 5, Ranks = new List<LeagueRankDbfRecord> {
            new() { StarLevel = 1, Stars = 3 }, new() { StarLevel = 51, Stars = 0 }
        } };
    }
    internal static MedalPacket ProjectMedal(LadderMedal medal, double? rating, int games, int wins, double? playerLegendRating = null) => new() {
        HasLegendRank = !medal.IsLegend || WorldAvailable,
        LegendRank = medal.IsLegend && WorldAvailable ? rating == 1825 ? 13 : 9 : 0,
        LeagueId = medal.LeagueId, StarLevel = medal.StarLevel, Rating = rating ?? 0, Games = games, Wins = wins,
        PlayerLegendRating = playerLegendRating
    };
    internal static bool CompleteGameplayMedal(LadderMedal medal, MedalPacket packet, FormatType format) =>
        medal != null && packet != null && (!medal.IsLegend || packet.HasLegendRank);
}
internal sealed class MedalInfoTranslator
{
    internal MedalPacket Packet { get; }
    private readonly TranslatedMedalInfo current;
    internal MedalInfoTranslator() { }
    internal MedalInfoTranslator(MedalPacket packet)
    {
        Packet = packet;
        current = new TranslatedMedalInfo { leagueId = packet.LeagueId, starLevel = packet.StarLevel,
            legendIndex = packet.LegendRank, RankConfig = new RankConfig { ShowOpponentRankInGame = NativeUIProbe.ShowOpponent } };
    }
    internal bool IsDisplayable() => current?.IsValid() == true;
    internal TranslatedMedalInfo GetCurrentMedal(FormatType format) => current;
}
internal sealed class TranslatedMedalInfo
{
    internal int leagueId, starLevel, legendIndex;
    internal RankConfig RankConfig;
    internal bool IsValid() => leagueId > 0 && starLevel > 0;
}
internal sealed class RankConfig { internal bool ShowOpponentRankInGame; }
