using System;
using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// LethalBound (late-lethal pool gate): basic face damage, weapon, Charge from hand, and the opponent's armour.
public static class LethalBoundTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception("LethalBound: " + message); }

    // Empty hands and no mana on both sides; the current player is the bot.
    private static Game Board(int mana)
    {
        var deck = Enumerable.Repeat("CS2_182", 30).Select(Cards.FromId).ToList();
        var game = new Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR,
            Player1Deck = deck, Player2Deck = deck.ToList(), StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, RandomSeed = 7 });
        game.StartGame();
        foreach (var player in new[] { game.Player1, game.Player2 })
        {
            foreach (var card in player.HandZone.ToArray()) { player.HandZone.Remove(card); player.SetasideZone.Add(card); }
            player.BaseMana = 0; player.UsedMana = 0;
        }
        game.CurrentPlayer.BaseMana = mana;
        return game;
    }
    private static FairFacts Facts(Game game)
    {
        game.DeathProcessingAndAuraUpdate();
        return LethalTests.View(game).Facts;
    }

    public static void TestLethalBoundBasicCases()
    {
        // Face damage: a ready 4/5 is lethal on 4 health, not on 5; an exhausted or frozen one deals nothing.
        foreach (int health in new[] { 4, 5 })
        {
            var game = Board(0); var bot = game.CurrentPlayer;
            var yeti = (Minion)Entity.FromCard(bot, Cards.FromId("CS2_182"), zone: bot.BoardZone); yeti.IsExhausted = false;
            bot.Opponent.Hero.Damage = 30 - health;
            var facts = Facts(game);
            Check(LethalBound.UpperBound(facts) >= 4 && LethalBound.Plausible(facts) == (health == 4), "minion face damage, health " + health);
        }
        var tired = Board(0); var tiredBot = tired.CurrentPlayer;
        ((Minion)Entity.FromCard(tiredBot, Cards.FromId("CS2_182"), zone: tiredBot.BoardZone)).IsExhausted = true;
        tiredBot.Opponent.Hero.Damage = 26;
        Check(!LethalBound.Plausible(Facts(tired)), "exhausted minion counted");

        // Weapon: an equipped 3/2 is lethal on 3, and the opponent's armour counts as health.
        foreach (int armour in new[] { 0, 1 })
        {
            var game = Board(0); var bot = game.CurrentPlayer;
            var axe = Entity.FromCard(bot, Cards.FromId("CS2_106"), zone: bot.HandZone); axe.Cost = 0;
            Check(game.Process(PlayCardTask.Any(bot, axe)), "weapon fixture");
            bot.Opponent.Hero.Damage = 27; bot.Opponent.Hero.Armor = armour;
            var facts = Facts(game);
            Check(LethalBound.Plausible(facts) == (armour == 0), "weapon vs " + armour + " armour: bound " + LethalBound.UpperBound(facts));
        }

        // Charge from hand: castable Bluegill Warrior (2/1 Charge, 2 mana) reaches 2 health; without the mana it does not.
        foreach (int mana in new[] { 2, 1 })
        {
            var game = Board(mana); var bot = game.CurrentPlayer;
            Entity.FromCard(bot, Cards.FromId("CS2_173"), zone: bot.HandZone);
            bot.Opponent.Hero.Damage = 28;
            var facts = Facts(game);
            Check(LethalBound.Plausible(facts) == (mana == 2), "charge minion with " + mana + " mana: bound " + LethalBound.UpperBound(facts));
        }

        // Nothing to attack with: implausible even on 1 health.
        var empty = Board(0); empty.CurrentPlayer.Opponent.Hero.Damage = 29;
        Check(!LethalBound.Plausible(Facts(empty)), "empty board plausible");
    }
}
