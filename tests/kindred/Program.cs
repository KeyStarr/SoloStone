using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class Program
{
    private static int _tests;

    private static int Main()
    {
        try
        {
            DefinitionsAreExact();
            NoPreviousPlayDoesNotActivate();
            MatchingPreviousOwnTurnActivates();
            MismatchingPreviousOwnTurnDoesNotActivate();
            CurrentTurnPlayDoesNotActivate();
            OpponentPlayDoesNotActivate();
            AnyMatchingPreviousPlayActivates();
            AllMatchesTypedMinionsBothWays();
            AllDoesNotMatchTypelessMinions();
            CloneRolloverIsIsolated();
            SummonedCopyIsLiteralAndNonRecursive();
            BookkeeperDeathrattleDrawsOnlySpells();
            Console.WriteLine($"PASS kindred proof {_tests} tests");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL " + error.Message);
            return 1;
        }
    }

    private static Game Fresh(int seed = 101)
    {
        Game game = ProofDecks.CreateGame("proof-mage", "proof-warrior", seed);
        game.StartGame();
        Ready(game.Player1);
        Ready(game.Player2);
        return game;
    }

    private static void Ready(Controller controller)
    {
        controller.BaseMana = 10;
        controller.UsedMana = 0;
    }

    private static Minion AddMinion(Controller controller, string id) =>
        (Minion)Generic.DrawCard(controller, Cards.FromId(id));

    private static Minion PlayMinion(Game game, Controller controller, string id)
    {
        Ready(controller);
        Minion minion = AddMinion(controller, id);
        Check(game.Process(PlayCardTask.Minion(controller, minion)), $"could not play {id}");
        return minion;
    }

    private static void End(Game game, Controller controller)
    {
        Check(ReferenceEquals(game.CurrentPlayer, controller), "turn owner mismatch");
        Check(game.Process(EndTurnTask.Any(controller)), "could not end turn");
    }

    private static void ReturnToPlayerOne(Game game)
    {
        End(game, game.Player1);
        End(game, game.Player2);
        Ready(game.Player1);
    }

    private static int Count(Controller controller, string id) =>
        controller.BoardZone.Count(minion => minion.Card.Id == id);

    private static void DefinitionsAreExact()
    {
        Card bookkeeper = Cards.FromId("TLC_226");
        Card experiment = Cards.FromId("DINO_435");
        Check(bookkeeper != null && bookkeeper.AssetId == 117535 && bookkeeper.Cost == 3 &&
              bookkeeper.ATK == 2 && bookkeeper.Health == 2 && bookkeeper.Class == CardClass.MAGE &&
              bookkeeper.Type == CardType.MINION && bookkeeper.GetRawRace() == Race.ELEMENTAL &&
              bookkeeper.Deathrattle && (int)bookkeeper.Set == 1952,
            "TLC_226 metadata differs from frozen definition");
        Check(experiment != null && experiment.AssetId == 119695 && experiment.Cost == 5 &&
              experiment.ATK == 3 && experiment.Health == 4 && experiment.Class == CardClass.NEUTRAL &&
              experiment.Type == CardType.MINION && experiment.GetRawRace() == Race.ALL &&
              (int)experiment.Set == 1952,
            "DINO_435 metadata differs from frozen definition");
        Check(bookkeeper.Implemented && experiment.Implemented, "Kindred card powers are missing");
        Pass();
    }

    private static void NoPreviousPlayDoesNotActivate()
    {
        Game game = Fresh();
        PlayMinion(game, game.Player1, "TLC_226");
        Check(Count(game.Player1, "TLC_226") == 1, "empty previous-turn snapshot activated Kindred");
        Pass();
    }

    private static void MatchingPreviousOwnTurnActivates()
    {
        Game game = Fresh(103);
        PlayMinion(game, game.Player1, "TLC_226");
        ReturnToPlayerOne(game);
        PlayMinion(game, game.Player1, "TLC_226");
        Check(Count(game.Player1, "TLC_226") == 3, "matching Elemental from previous own turn did not activate");
        Pass();
    }

    private static void MismatchingPreviousOwnTurnDoesNotActivate()
    {
        Game game = Fresh(107);
        PlayMinion(game, game.Player1, "CORE_GVG_085");
        ReturnToPlayerOne(game);
        PlayMinion(game, game.Player1, "TLC_226");
        Check(Count(game.Player1, "TLC_226") == 1, "Mechanical previous play activated Elemental Kindred");
        Pass();
    }

    private static void CurrentTurnPlayDoesNotActivate()
    {
        Game game = Fresh(109);
        PlayMinion(game, game.Player1, "TLC_226");
        PlayMinion(game, game.Player1, "TLC_226");
        Check(Count(game.Player1, "TLC_226") == 2, "current-turn Elemental activated Kindred");
        Pass();
    }

    private static void OpponentPlayDoesNotActivate()
    {
        Game game = Fresh(113);
        End(game, game.Player1);
        PlayMinion(game, game.Player2, "TLC_226");
        End(game, game.Player2);
        PlayMinion(game, game.Player1, "TLC_226");
        Check(Count(game.Player1, "TLC_226") == 1, "opponent Elemental activated Kindred");
        Pass();
    }

    private static void AnyMatchingPreviousPlayActivates()
    {
        Game game = Fresh(127);
        PlayMinion(game, game.Player1, "Core_CS2_200");
        PlayMinion(game, game.Player1, "TLC_226");
        ReturnToPlayerOne(game);
        PlayMinion(game, game.Player1, "TLC_226");
        Check(Count(game.Player1, "TLC_226") == 3, "matching card among multiple previous plays was lost");
        Pass();
    }

    private static void AllMatchesTypedMinionsBothWays()
    {
        Game game = Fresh(131);
        PlayMinion(game, game.Player1, "TLC_226");
        ReturnToPlayerOne(game);
        Minion source = PlayMinion(game, game.Player1, "DINO_435");
        Minion[] experiments = game.Player1.BoardZone.Where(minion => minion.Card.Id == "DINO_435").ToArray();
        Check(experiments.Length == 2 && experiments.Select(minion => minion.Id).Distinct().Count() == 2 &&
              experiments.All(minion => minion.AttackDamage == 3 && minion.Health == 4) &&
              experiments.Count(minion => minion.Id == source.Id) == 1,
            "ALL Kindred did not summon a distinct literal 3/4 copy");

        game = Fresh(137);
        PlayMinion(game, game.Player1, "DINO_435");
        ReturnToPlayerOne(game);
        PlayMinion(game, game.Player1, "TLC_226");
        Check(Count(game.Player1, "TLC_226") == 2, "previous ALL minion did not match typed Kindred");
        Pass();
    }

    private static void AllDoesNotMatchTypelessMinions()
    {
        Game game = Fresh(139);
        PlayMinion(game, game.Player1, "Core_CS2_200");
        ReturnToPlayerOne(game);
        PlayMinion(game, game.Player1, "DINO_435");
        Check(Count(game.Player1, "DINO_435") == 1, "ALL Kindred matched a minion with no type");
        Pass();
    }

    private static void CloneRolloverIsIsolated()
    {
        Game original = Fresh(149);
        PlayMinion(original, original.Player1, "TLC_226");
        ReturnToPlayerOne(original);
        Game clone = original.Clone(resetRandomSeed: false, history: true);

        End(clone, clone.Player1);
        End(clone, clone.Player2);
        PlayMinion(clone, clone.Player1, "TLC_226");
        Check(Count(clone.Player1, "TLC_226") == 2, "clone did not replace previous snapshot after empty own turn");

        PlayMinion(original, original.Player1, "TLC_226");
        Check(Count(original.Player1, "TLC_226") == 3,
            "clone rollover mutated original previous-turn snapshot");
        Pass();
    }

    private static void SummonedCopyIsLiteralAndNonRecursive()
    {
        Game game = Fresh(151);
        PlayMinion(game, game.Player1, "TLC_226");
        ReturnToPlayerOne(game);
        int played = game.Player1.NumMinionsPlayedThisTurn;
        int[] before = game.Player1.BoardZone.Where(minion => minion.Card.Id == "TLC_226")
            .Select(minion => minion.Id).ToArray();
        Minion source = PlayMinion(game, game.Player1, "TLC_226");
        Minion[] activated = game.Player1.BoardZone.Where(minion => minion.Card.Id == "TLC_226").ToArray();
        Minion copy = activated.Single(minion => minion.Id != source.Id && !before.Contains(minion.Id));
        Check(activated.Length == 3 && activated.Select(minion => minion.Id).Distinct().Count() == 3,
            "Kindred copy recursed or reused an entity identity");
        Check(copy.AttackDamage == 2 && copy.Health == 2 && copy.Damage == 0 && copy.HasDeathrattle,
            "Kindred copy did not retain printed stats and Deathrattle");
        Check(game.Player1.NumMinionsPlayedThisTurn == played + 1,
            "summoned copy counted as a played minion");
        PowerHistoryFullEntity history = game.PowerHistory.Full.OfType<PowerHistoryFullEntity>()
            .Single(entry => entry.Entity.Id == copy.Id);
        Check(history.Entity.Name == "TLC_226" && history.Entity.Tags[GameTag.ATK] == 2 &&
              history.Entity.Tags[GameTag.HEALTH] == 2 && history.Entity.Tags[GameTag.ZONE] == (int)Zone.PLAY,
            "Kindred copy history lacks literal identity, stats, or board zone");
        Pass();
    }

    private static void BookkeeperDeathrattleDrawsOnlySpells()
    {
        Game game = Fresh(157);
        Controller player = game.Player1;
        while (player.DeckZone.Count > 0)
            player.DeckZone.Remove(0);
        IPlayable minion = Entity.FromCard(player, Cards.FromId("Core_CS2_200")) as IPlayable;
        IPlayable spell = Entity.FromCard(player, Cards.FromId("CORE_CS2_029")) as IPlayable;
        player.DeckZone.Add(minion);
        player.DeckZone.Add(spell);
        Minion bookkeeper = (Minion)Entity.FromCard(player, Cards.FromId("TLC_226"), zone: player.BoardZone);
        int draws = player.NumCardsDrawnThisTurn;
        Generic.DamageCharFunc(game.Player2.Hero, bookkeeper, 2, false);
        game.DeathProcessingAndAuraUpdate();
        Check(player.NumCardsDrawnThisTurn == draws + 1 && player.HandZone.Contains(spell) &&
              player.DeckZone.Contains(minion), "Bookkeeper Deathrattle did not filter draw to spells");

        game = Fresh(163);
        player = game.Player1;
        while (player.DeckZone.Count > 0)
            player.DeckZone.Remove(0);
        minion = Entity.FromCard(player, Cards.FromId("Core_CS2_200")) as IPlayable;
        player.DeckZone.Add(minion);
        bookkeeper = (Minion)Entity.FromCard(player, Cards.FromId("TLC_226"), zone: player.BoardZone);
        int hand = player.HandZone.Count;
        int fatigue = player.Hero.Fatigue;
        Generic.DamageCharFunc(game.Player2.Hero, bookkeeper, 2, false);
        game.DeathProcessingAndAuraUpdate();
        Check(player.HandZone.Count == hand && player.DeckZone.Contains(minion) && player.Hero.Fatigue == fatigue,
            "spell-filtered draw consumed a minion or caused fatigue when no spell existed");
        Pass();
    }

    internal static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Pass() => _tests++;
}
