using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Dungeon Run round 1 (data/runs/loot-v1.json): the three bosses and their Hero Powers, the AI's must-play rule for an
// autocast power, and the two legacy cards round 1 needs (Power of the Wild in the Druid start deck, One-eyed Cheat in
// Bink's deck).
internal static class DungeonRunRound1Proof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "dungeon run round 1: " + message);

    // The player (Mage) goes first; the boss is player 2, as a run fight stages it.
    internal static Game Fight(string boss, int seed = 11, string[] board = null, string[] hand = null)
    {
        var config = new GameConfig {
            StartPlayer = 1, Player1HeroClass = CardClass.MAGE, Player2HeroCard = Cards.FromId(boss),
            Player2HeroClass = Cards.FromId(boss).Class,
            Player1Deck = Enumerable.Repeat("CS2_182", 10).Select(Cards.FromId).ToList(),
            Player2Deck = Enumerable.Repeat("CS2_120", 10).Select(Cards.FromId).ToList(),
            Shuffle = false, SkipMulligan = true, History = true, RandomSeed = seed, SkipCoin = true,
            Player2StartingBoard = board, Player1StartingHand = hand,
        };
        var game = new Game(config);
        game.StartGame();
        return game;
    }

    private static void UseHeroPower(Game game, Controller player, ICharacter target = null)
    {
        player.BaseMana = Math.Max(player.BaseMana, 2); player.UsedMana = 0;
        var task = player.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER && t.Target?.Id == target?.Id);
        Check(task != null && game.Process(task), "hero power accepted: " + player.Hero.HeroPower.Card.Id);
    }

    [GroupProof("dungeon-run-round1")]
    internal static void Run()
    {
        // Giant Rat: 10 Health; Rat Race summons two 1/1 Rats.
        Game rat = Fight("LOOTA_BOSS_18h");
        Check(rat.Player2.Hero.Health == 10 && rat.Player2.Hero.HeroPower.Card.Id == "LOOTA_BOSS_18p", "Giant Rat: 10 Health, Rat Race");
        End(rat);
        UseHeroPower(rat, rat.Player2);
        Check(rat.Player2.BoardZone.Count(m => m.Card.Id == "LOOTA_BOSS_18t" && m.AttackDamage == 1 && m.Health == 1) == 2, "Rat Race: two 1/1 Rats");

        // Wee Whelp: 10 Health; Baby Breath deals 2 damage to a target.
        Game whelp = Fight("LOOTA_BOSS_44h");
        Check(whelp.Player2.Hero.Health == 10 && whelp.Player2.Hero.HeroPower.Card.Id == "LOOTA_BOSS_44p", "Wee Whelp: 10 Health, Baby Breath");
        End(whelp);
        Check(whelp.Player2.Options().Where(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER).All(t => t.Target != null),
            "Baby Breath needs a target");
        int before = whelp.Player1.Hero.Health;
        UseHeroPower(whelp, whelp.Player2, whelp.Player1.Hero);
        Check(whelp.Player1.Hero.Health == before - 2, "Baby Breath: 2 damage");

        // Bink the Burglar: 10 Health; Coin gives 1 mana this turn only, and the AI must use it whenever it can (autocast).
        Game bink = Fight("LOOTA_BOSS_45h");
        Check(bink.Player2.Hero.Health == 10 && bink.Player2.Hero.HeroPower.Card[GameTag.AI_MUST_PLAY] == 1, "Bink: 10 Health, an autocast Coin");
        End(bink);
        var forced = BotDriver.Forced(bink, bink.Player2.Options(), out var trace);
        Check(forced != null && forced.PlayerTaskType == PlayerTaskType.HERO_POWER && trace != null, "the AI uses an autocast Hero Power first");
        int mana = bink.Player2.RemainingMana;
        Check(bink.Process(forced) && bink.Player2.RemainingMana == mana + 1, "Coin: one more mana this turn");
        End(bink); End(bink);
        Check(bink.Player2.TemporaryMana == 0, "the extra mana lasts one turn");
        Check(BotDriver.Forced(rat, rat.Player2.Options(), out _)?.PlayerTaskType != PlayerTaskType.HERO_POWER,
            "a Hero Power without the tag is never forced");

        // Power of the Wild (Choose One): +1/+1 to your minions, or a 3/2 Panther.
        Game wild = Fight("LOOTA_BOSS_18h");
        Summon(wild.Player1, "CS2_182");
        var spell = Add(wild.Player1, "EX1_160");
        wild.Player1.BaseMana = 10; wild.Player1.UsedMana = 0;
        var buff = wild.Player1.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id && t.ChooseOne == 2);
        Check(wild.Process(buff) && wild.Player1.BoardZone.Single().AttackDamage == 5 && wild.Player1.BoardZone.Single().Health == 6,
            "Power of the Wild: +1/+1");
        var second = Add(wild.Player1, "EX1_160");
        var panther = wild.Player1.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == second.Id && t.ChooseOne == 1);
        Check(wild.Process(panther) && wild.Player1.BoardZone.Any(m => m.Card.Id == "EX1_160t" && m.AttackDamage == 3 && m.Health == 2),
            "Power of the Wild: a 3/2 Panther");

        // One-eyed Cheat: another Pirate summoned gives it Stealth; summoning the Cheat itself does not.
        Game cheat = Fight("LOOTA_BOSS_45h");
        var oneEyed = (Minion)Play(cheat, cheat.Player1, "GVG_025");
        Check(!oneEyed.HasStealth, "the Cheat's own summon gives no Stealth");
        Play(cheat, cheat.Player1, "CS2_146");
        Check(oneEyed.HasStealth, "a Pirate summoned: the Cheat gains Stealth");

        // Snapshots keep a boss game and its powers.
        Game loaded = Game.LoadSnapshot(whelp.SaveSnapshot());
        Check(HsSim.Canonical.State(loaded) == HsSim.Canonical.State(whelp), "a boss game survives its snapshot");
    }
}
