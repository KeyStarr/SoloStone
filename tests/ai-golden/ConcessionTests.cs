using System;
using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

public static class ConcessionTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static Game Dead(int turn = 12)
    {
        var deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList();
        var game = new Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR,
            Player1Deck = deck, Player2Deck = deck.ToList(), StartPlayer = 2, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, RandomSeed = 8 });
        game.StartGame(); game.Turn = turn;
        game.Player2.Hero.Damage = 26;
        Entity.FromCard(game.Player1, Cards.FromId("CS2_182"), zone: game.Player1.BoardZone);
        return game;
    }
    private static string Willing(NpcProfile profile) => Enumerable.Range(0, 1000).Select(i => "concede-" + i)
        .First(id => MatchRecorder.PersonalityRoll(profile.Version, id, 17, "concede") < profile.Personality.ConcessionWillingness);

    public static void TestAllPersonalityFloorsWillingnessAndUsefulSequence()
    {
        foreach (var profile in NpcCatalog.All)
        {
            string id = Willing(profile);
            var game = Dead(profile.Personality.ConcedeFromTurn - 1);
            Check(!NpcConcession.ShouldConcede(game, 2, profile, id, 17, true), profile.Id + " conceded early");
            game.Turn++;
            Check(NpcConcession.ShouldConcede(game, 2, profile, id, 17, true), profile.Id + " failed dead proof");
            Check(!NpcConcession.ShouldConcede(game, 2, profile, id, 17, false), "conceded before useful sequence");
            Check(!NpcConcession.ShouldConcede(game, 2, profile, id, 17, true, 0), "incomplete proof conceded");
            Check(NpcConcession.ShouldConcede(game, 2, profile, id, 17, true), "polling rerolled willingness");
            if (profile.Personality.ConcessionWillingness < 1)
            {
                string unwilling = Enumerable.Range(0, 1000).Select(i => "stay-" + i).First(key =>
                    MatchRecorder.PersonalityRoll(profile.Version, key, 17, "concede") >= profile.Personality.ConcessionWillingness);
                Check(!NpcConcession.ShouldConcede(game, 2, profile, unwilling, 17, true), "willingness ignored");
            }
        }
    }
    public static void TestProofRejectsNearMissHiddenLethalSecretsAndDefenses()
    {
        var game = Dead();
        Check(NpcConcession.ProvesPublicBoardLethal(game, 2), "plain public board lethal rejected");
        string before = MatchRecorder.Fingerprint(game);
        Check(NpcConcession.ProvesPublicBoardLethal(game, 2) && before == MatchRecorder.Fingerprint(game), "proof mutated live state");
        game.Player1.BoardZone[0][GameTag.OUTGOING_DAMAGE_ADJUSTMENT] = -4;
        Check(!NpcConcession.ProvesPublicBoardLethal(game, 2), "unmodelled signed outgoing modifier accepted");
        game.Player1.BoardZone[0][GameTag.OUTGOING_DAMAGE_ADJUSTMENT] = 0;
        game.Player2.Hero.Armor = 1;
        Check(!NpcConcession.ProvesPublicBoardLethal(game, 2), "armor near miss accepted");
        game.Player2.Hero.Armor = 0;
        game.Player1.BoardZone[0].IsFrozen = true;
        Entity.FromCard(game.Player1, Cards.FromId("CS2_029"), zone: game.Player1.HandZone);
        Check(!NpcConcession.ProvesPublicBoardLethal(game, 2), "hidden hand lethal accepted");
        game.Player1.BoardZone[0].IsFrozen = false;
        var secret = Entity.FromCard(game.Player1, Cards.FromId("EX1_287"), zone: game.Player1.SecretZone);
        Check(!NpcConcession.ProvesPublicBoardLethal(game, 2), "human secret accepted");
        game.Player1.SecretZone.Remove((Spell)secret);
        var defender = Entity.FromCard(game.Player2, Cards.FromId("CS2_179"), zone: game.Player2.BoardZone);
        Check(!NpcConcession.ProvesPublicBoardLethal(game, 2), "unmodelled taunt accepted");
        game.Player2.BoardZone.Remove((Minion)defender);
        game.Player2.Hero.IsImmune = true;
        Check(!NpcConcession.ProvesPublicBoardLethal(game, 2), "immune hero accepted");
        game.Player2.Hero.IsImmune = false;
        game.Player2.Hero[GameTag.HEALTH_MINIMUM] = 1;
        Check(!NpcConcession.ProvesPublicBoardLethal(game, 2), "health floor accepted");
        game.Player2.Hero[GameTag.HEALTH_MINIMUM] = 0;
        Entity.FromCard(game.Player1, Cards.FromId("EX1_095"), zone: game.Player1.BoardZone);
        Check(!NpcConcession.ProvesPublicBoardLethal(game, 2), "unmodelled attack trigger accepted");
    }
    public static void TestProofRejectsHumanFatigueBeforeBoardLethal()
    {
        var game = Dead();
        foreach (var card in game.Player1.DeckZone.ToArray())
        { game.Player1.DeckZone.Remove(card); game.Player1.SetasideZone.Add(card); }
        game.Player1.Hero.Damage = game.Player1.Hero.BaseHealth - 1;
        Check(!NpcConcession.ProvesPublicBoardLethal(game, 2), "conceded a win before human fatigue");
    }

    public static void TestBuiltEngineCarriesSourceIdentity()
    {
        var stamp = typeof(Game).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>().SingleOrDefault(a => a.Key == "EngineSourceIdentity")?.Value;
        Check(stamp != null && stamp.Length == 64 && stamp == PlaytestRecord.EngineSourceIdentity,
            "real engine output has no matching source identity stamp");
        string otherTarget = Environment.GetEnvironmentVariable("NPC_ENGINE_NETSTANDARD_OUTPUT");
        if (otherTarget != null)
        {
            var assembly = System.Reflection.Assembly.LoadFile(System.IO.Path.GetFullPath(otherTarget));
            var other = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
                .Cast<System.Reflection.AssemblyMetadataAttribute>().Single(a => a.Key == "EngineSourceIdentity").Value;
            Check(other == stamp, "net10.0/netstandard2.1 real build source stamps differ");
        }
    }
    public static void TestEngineConcessionOutcomeAndLegacyResults()
    {
        foreach (var profile in NpcCatalog.All)
        {
            var game = Dead(profile.Personality.ConcedeFromTurn);
            Check(game.Process(ConcedeTask.Any(game.Player2)), "engine rejected bot concession");
            var outcome = MatchOutcome.From(game, false);
            Check(game.State == State.COMPLETE && outcome.Result == "Won" && !outcome.Conceded
                && outcome.Turns == game.Turn, profile.Id + " bot concession became human loss/concession");
        }
        var legacy = Dead();
        foreach (var state in new[] { PlayState.WON, PlayState.LOST, PlayState.TIED, PlayState.PLAYING })
        {
            legacy.Player1.PlayState = state;
            string expected = state == PlayState.WON ? "Won" : state == PlayState.LOST ? "Lost" : state == PlayState.TIED ? "Tied" : null;
            foreach (bool conceded in new[] { false, true })
            {
                var outcome = MatchOutcome.From(legacy, conceded);
                Check(outcome.Result == expected && outcome.Conceded == conceded && outcome.Turns == legacy.Turn,
                    "legacy outcome changed");
            }
        }
        legacy = Dead(); legacy.Process(ConcedeTask.Any(legacy.Player1));
        Check(MatchOutcome.From(legacy, true).Result == "Lost" && MatchOutcome.From(legacy, true).Conceded, "human concession changed");
    }
}
