using System;
using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

// Dungeon Run's active treasures (data/runs/loot-v1.json): the treasure cards a run adds to its deck.
internal static class DungeonRunActiveTreasuresProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "dungeon run active treasures: " + message);

    private static Game Fight(int seed = 11) => DungeonRunRound1Proof.Fight("LOOTA_BOSS_42h", seed);

    // A fight whose player deck holds the given cards and whose run has defeated the given bosses.
    private static Game RunFight(int bosses, params string[] cards)
    {
        var config = new GameConfig {
            StartPlayer = 1, Player1HeroClass = CardClass.MAGE, Player2HeroCard = Cards.FromId("LOOTA_BOSS_42h"), Player2HeroClass = CardClass.SHAMAN,
            Player1Deck = cards.Concat(Enumerable.Repeat("CS2_182", 10 - cards.Length)).Select(Cards.FromId).ToList(),
            Player2Deck = Enumerable.Repeat("CS2_120", 10).Select(Cards.FromId).ToList(),
            Shuffle = false, SkipMulligan = true, History = true, RandomSeed = 11, SkipCoin = true, Player1RunBossesDefeated = bosses,
        };
        var game = new Game(config);
        game.StartGame();
        return game;
    }

    private static void RunScaled()
    {
        // Loyal Sidekick: +1/+1 per boss defeated, kept by a snapshot; Shifting Hourglass: (1) less per boss.
        foreach (Game run in new[] { RunFight(4, "LOOTA_829", "LOOTA_830", "LOOTA_817"), Game.LoadSnapshot(RunFight(4, "LOOTA_829", "LOOTA_830", "LOOTA_817").SaveSnapshot()) })
        {
            var all = run.Player1.HandZone.Concat(run.Player1.DeckZone).ToList();
            var sidekick = all.Single(p => p.Card.Id == "LOOTA_829"); var hourglass = all.Single(p => p.Card.Id == "LOOTA_830");
            Check(sidekick[GameTag.ATK] == 6 && sidekick[GameTag.HEALTH] == 6 && sidekick[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4, "Loyal Sidekick: 6/6 after 4 bosses");
            Check(hourglass.Cost == 3, "Shifting Hourglass: costs (3) after 4 bosses");
        }
        Game fresh = RunFight(0, "LOOTA_829", "LOOTA_830");
        Check(fresh.Player1.HandZone.Concat(fresh.Player1.DeckZone).Single(p => p.Card.Id == "LOOTA_830").Cost == 7, "Shifting Hourglass: (7) before any boss");

        // Shifting Hourglass: an extra turn.
        Game hour = RunFight(4, "LOOTA_830");
        var glass = hour.Player1.HandZone.Single(p => p.Card.Id == "LOOTA_830");
        PlayEntity(hour, hour.Player1, glass);
        End(hour);
        Check(hour.CurrentPlayer == hour.Player1, "Shifting Hourglass: an extra turn");

        // Primordial Wand: Adapt the minion once per boss defeated (three choices after 3 bosses).
        Game wand = RunFight(3, "LOOTA_817");
        var target = Summon(wand.Player1, "CS2_182");
        var card = wand.Player1.HandZone.Single(p => p.Card.Id == "LOOTA_817");
        wand.Player1.BaseMana = 10; wand.Player1.UsedMana = 0;
        var play = wand.Player1.Options().First(o => o.PlayerTaskType == PlayerTaskType.PLAY_CARD && o.Source?.Id == card.Id && o.Target?.Id == target.Id);
        Check(wand.Process(play), "Primordial Wand: played");
        int adapts = 0;
        while (wand.Player1.Choice != null && adapts < 10)
        {
            Check(wand.Process(ChooseTask.Pick(wand.Player1, wand.Player1.Choice.Choices[0])), "Primordial Wand: an adaptation chosen");
            adapts++;
        }
        Check(adapts == 3, "Primordial Wand: three adaptations, got " + adapts);
    }

    private static void Combined()
    {
        // Quel'Delar: Blade and Hilt both in the deck at the start combine into Quel'Delar in hand; alone, each stays.
        foreach (Game forged in new[] { RunFight(0, "LOOTA_842a", "LOOTA_842b"), Game.LoadSnapshot(RunFight(0, "LOOTA_842a", "LOOTA_842b").SaveSnapshot()) })
        {
            var all = forged.Player1.HandZone.Concat(forged.Player1.DeckZone).ToList();
            Check(forged.Player1.HandZone.Count(p => p.Card.Id == "LOOTA_842") == 1 && all.All(p => p.Card.Id != "LOOTA_842a" && p.Card.Id != "LOOTA_842b"),
                "Quel'Delar: forged from Blade and Hilt");
        }
        Game blade = RunFight(0, "LOOTA_842a");
        Check(blade.Player1.HandZone.Concat(blade.Player1.DeckZone).Count(p => p.Card.Id == "LOOTA_842a") == 1
            && blade.Player1.HandZone.All(p => p.Card.Id != "LOOTA_842"), "Blade of Quel'Delar alone stays");
        Game hiltGame = Fight();
        var hiltTarget = Summon(hiltGame.Player1, "CS2_182");
        Play(hiltGame, hiltGame.Player1, "LOOTA_842b", hiltTarget);
        Check(hiltTarget.AttackDamage == 7 && hiltTarget.Health == 8, "Hilt of Quel'Delar: +3/+3");
        Game quel = Fight();
        Play(quel, quel.Player1, "LOOTA_842");
        var foe = Summon(quel.Player2, "CS2_182");
        var swing = quel.Player1.Options().First(o => o.PlayerTaskType == PlayerTaskType.HERO_ATTACK && o.Target?.Id == quel.Player2.Hero.Id);
        Check(quel.Process(swing) && quel.Player2.Hero.Damage == 12 && foe.Damage == 6, "Quel'Delar: 6 to all enemies after the attack");

        // Mask of Mimicry: the minions in hand become the chosen minion; spells stay.
        Game mask = Fight();
        var model = Summon(mask.Player2, "CS2_120");
        Add(mask.Player1, "CS2_024");
        Play(mask, mask.Player1, "LOOTA_847", model);
        Check(mask.Player1.HandZone.Where(p => p.Card.Type == CardType.MINION).All(p => p.Card.Id == "CS2_120")
            && mask.Player1.HandZone.Any(p => p.Card.Id == "CS2_024"), "Mask of Mimicry: minions in hand copied");

        // Scroll of Confusion: every minion ends on some side; over seeds, some change sides.
        bool moved = false;
        for (int seed = 0; seed < 6; seed++)
        {
            Game scroll = Fight(seed);
            for (int i = 0; i < 3; i++) { Summon(scroll.Player1, "CS2_182"); Summon(scroll.Player2, "CS2_120"); }
            Play(scroll, scroll.Player1, "LOOTA_839");
            Check(scroll.Player1.BoardZone.Count + scroll.Player2.BoardZone.Count == 6, "Scroll of Confusion: all minions kept");
            moved |= scroll.Player1.BoardZone.Any(m => m.Card.Id == "CS2_120") || scroll.Player2.BoardZone.Any(m => m.Card.Id == "CS2_182");
        }
        Check(moved, "Scroll of Confusion: minions change sides");

        // Aleatoric Cube: both decks shuffled together and dealt in half; the player's cards cost (2) less.
        Game cube = Fight();
        int total = cube.Player1.DeckZone.Count + cube.Player2.DeckZone.Count;
        Play(cube, cube.Player1, "LOOTA_820");
        Check(cube.Player1.DeckZone.Count + cube.Player2.DeckZone.Count == total && Math.Abs(cube.Player1.DeckZone.Count - cube.Player2.DeckZone.Count) <= 1
            && cube.Player1.DeckZone.All(p => p.Controller == cube.Player1) && cube.Player2.DeckZone.All(p => p.Controller == cube.Player2),
            "Aleatoric Cube: decks pooled and split");
        Check(cube.Player1.DeckZone.All(p => p.Cost == Math.Max(0, p.Card.Cost - 2)) && cube.Player2.DeckZone.All(p => p.Cost == p.Card.Cost),
            "Aleatoric Cube: the player's cards cost (2) less");

        // Rod of Roasting: Pyroblasts until a hero dies.
        Game rod = Fight();
        Play(rod, rod.Player1, "LOOTA_822");
        Check(rod.Player1.Hero.IsDead || rod.Player2.Hero.IsDead, "Rod of Roasting: a hero died");

        // Portable Forge: three Legendary weapons to Discover; the pick is equipped.
        Game forge = Fight();
        Play(forge, forge.Player1, "LOOTA_841");
        Check(forge.Player1.Choice != null && forge.Player1.Choice.Choices.All(id => forge.IdEntityDic[id].Card.Type == CardType.WEAPON
            && forge.IdEntityDic[id].Card.Rarity == Rarity.LEGENDARY), "Portable Forge: Legendary weapons offered: " + (forge.Player1.Choice == null ? "none" : string.Join(",", forge.Player1.Choice.Choices.Select(id => forge.IdEntityDic[id].Card.Id + ":" + forge.IdEntityDic[id].Card.Rarity))));
        int pick = forge.Player1.Choice.Choices[0];
        Check(forge.Process(ChooseTask.Pick(forge.Player1, pick)) && forge.Player1.Hero.Weapon?.Id == pick, "Portable Forge: the pick is equipped");

        // Magic Mirror / Amulet of Domination: the copy / the stolen minion, and its card recorded for the run deck.
        Game mirror = Fight();
        var original = Summon(mirror.Player2, "CS2_120");
        Play(mirror, mirror.Player1, "LOOTA_813", original);
        Check(mirror.Player1.BoardZone.Count(m => m.Card.Id == "CS2_120") == 1 && !original.IsDead
            && SabberStoneCore.Actions.RunDeckRules.Of(mirror.Player1).SequenceEqual(new[] { "CS2_120" }), "Magic Mirror: a copy, added to the Dungeon Deck");
        Game amulet = Fight();
        var stolen = Summon(amulet.Player2, "CS2_182");
        Play(amulet, amulet.Player1, "LOOTA_805", stolen);
        Check(stolen.Controller == amulet.Player1 && SabberStoneCore.Actions.RunDeckRules.Of(amulet.Player1).SequenceEqual(new[] { "CS2_182" })
            && SabberStoneCore.Actions.RunDeckRules.Of(amulet.Player2).Count == 0, "Amulet of Domination: taken, added to the Dungeon Deck");
    }

    [GroupProof("dungeon-run-active-treasures")]
    internal static void Run()
    {
        RunScaled();
        Combined();

        // Bag of Coins: a hand full of Coins.
        Game coins = Fight();
        Play(coins, coins.Player1, "LOOTA_836");
        Check(coins.Player1.HandZone.IsFull && coins.Player1.HandZone.Count(p => p.Card.Id == "GAME_005") == 10 - 4, "Bag of Coins: hand filled with Coins");

        // Bag of Stuffing: draws until the hand is full or the deck is empty.
        Game stuffing = Fight();
        Play(stuffing, stuffing.Player1, "LOOTA_823");
        Check(stuffing.Player1.HandZone.Count == 10 || stuffing.Player1.DeckZone.IsEmpty, "Bag of Stuffing: draws to a full hand");

        // Boots of Haste: minions in hand cost (0) this turn only.
        Game boots = Fight();
        var yeti = Add(boots.Player1, "CS2_182"); var bolt = Add(boots.Player1, "CS2_024");
        Play(boots, boots.Player1, "LOOTA_812");
        Check(yeti.Cost == 0 && bolt.Cost == 2, "Boots of Haste: minions cost (0)");
        End(boots);
        Check(yeti.Cost == 4, "Boots of Haste: this turn only");

        // THE CANDLE: 4 damage to each enemy minion, then a copy shuffled into the deck.
        Game candle = Fight();
        var croc = Summon(candle.Player2, "CS2_120"); var mine = Summon(candle.Player1, "CS2_182");
        int deck = candle.Player1.DeckZone.Count;
        Play(candle, candle.Player1, "LOOTA_843");
        Check(croc.IsDead && mine.Damage == 0 && candle.Player1.DeckZone.Count == deck + 1
            && candle.Player1.DeckZone.Any(p => p.Card.Id == "LOOTA_843"), "THE CANDLE: enemy minions hit, shuffled back");

        // Embers of Ragnaros: three 8-damage fireballs at random enemies.
        Game embers = Fight();
        Play(embers, embers.Player1, "LOOTA_827");
        Check(embers.Player2.Hero.Damage == 24, "Embers of Ragnaros: 24 damage to a lone hero");

        // Orb of Destruction: the opponent loses 2 Mana Crystals and discards 2 cards.
        Game orb = Fight();
        orb.Player2.BaseMana = 5; orb.Player2.UsedMana = 0;
        int hand = orb.Player2.HandZone.Count;
        Play(orb, orb.Player1, "LOOTA_811");
        Check(orb.Player2.BaseMana == 3 && orb.Player2.HandZone.Count == hand - 2, "Orb of Destruction: 2 crystals, 2 cards");

        // Wand of Disintegration: enemy minions silenced and destroyed; a Deathrattle does not fire.
        Game wand = Fight();
        Summon(wand.Player2, "EX1_096"); Summon(wand.Player2, "CS2_120");
        hand = wand.Player2.HandZone.Count;
        Play(wand, wand.Player1, "LOOTA_806");
        Check(wand.Player2.BoardZone.Count == 0 && wand.Player2.HandZone.Count == hand, "Wand of Disintegration: silenced, destroyed");

        // Wax Rager: dies and comes back once per death.
        Game wax = Fight();
        var rager = Summon(wax.Player1, "LOOTA_840");
        Check(rager.AttackDamage == 5 && rager.Health == 1, "Wax Rager: 5/1");
        Kill(wax, rager);
        Check(wax.Player1.BoardZone.Count(m => m.Card.Id == "LOOTA_840") == 1, "Wax Rager: resummoned");

        // Dr. Boom's Boombox: seven Boom Bots.
        Game boom = Fight();
        Play(boom, boom.Player1, "LOOTA_838");
        Check(boom.Player1.BoardZone.Count(m => m.Card.Id == "GVG_110t") == 7, "Dr. Boom's Boombox: 7 Boom Bots");

        // Vorpal Dagger: four hero attacks a turn, each Poisonous.
        Game vorpal = Fight();
        Play(vorpal, vorpal.Player1, "LOOTA_821");
        var targets = Enumerable.Range(0, 4).Select(_ => Summon(vorpal.Player2, "CS2_182")).ToList();
        foreach (var t in targets)
        {
            var attack = vorpal.Player1.Options().FirstOrDefault(o => o.PlayerTaskType == SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.HERO_ATTACK && o.Target?.Id == t.Id);
            Check(attack != null && vorpal.Process(attack), "Vorpal Dagger: a hero attack");
        }
        Check(targets.All(t => t.IsDead), "Vorpal Dagger: four Poisonous attacks");

        // Greedy Pickaxe: each hero attack adds an empty Mana Crystal.
        Game pick = Fight();
        Play(pick, pick.Player1, "LOOTA_835");
        pick.Player1.BaseMana = 5; pick.Player1.UsedMana = 2;
        int mana = pick.Player1.BaseMana, left = pick.Player1.RemainingMana;
        var heroAttack = pick.Player1.Options().First(o => o.PlayerTaskType == SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.HERO_ATTACK);
        pick.Process(heroAttack);
        Check(pick.Player1.BaseMana == mana + 1 && pick.Player1.RemainingMana == left, "Greedy Pickaxe: an empty crystal");

        // Portable Ice Wall: 3/15 Taunt that cannot attack and freezes what it damages.
        Game wall = Fight();
        var ice = Summon(wall.Player1, "LOOTA_826");
        Check(ice.AttackDamage == 3 && ice.Health == 15 && ice.HasTaunt && ice.CantAttack && ice.Card.Freeze, "Portable Ice Wall: keywords");

        // Party Portal: each spell its owner casts summons a random minion of the same Cost.
        Game party = Fight();
        Summon(party.Player1, "LOOTA_816");
        Play(party, party.Player1, "CS2_029", party.Player2.Hero);
        Check(party.Player1.BoardZone.Count == 2 && party.Player1.BoardZone[1].Card.Cost == 4, "Party Portal: a 4-Cost minion for Fireball");

        // Horn of Cenarius: three minions recruited from the deck.
        Game horn = Fight();
        deck = horn.Player1.DeckZone.Count;
        Play(horn, horn.Player1, "LOOTA_837");
        Check(horn.Player1.BoardZone.Count == 3 && horn.Player1.DeckZone.Count == deck - 3, "Horn of Cenarius: Recruit 3");

        // Archmage Staff: a random Mage spell at the start of each of its owner's turns.
        Game staff = Fight();
        Play(staff, staff.Player1, "LOOTA_819");
        End(staff); End(staff);
        Check(staff.Player1.HandZone.Any(p => p.Card.Class == CardClass.MAGE && p.Card.Type == CardType.SPELL && p.Card.Id != "CS2_029"), "Archmage Staff: a Mage spell");

        // Gloves of Mugging: three cards move from the opponent's hand to the player's.
        Game gloves = Fight();
        int theirs = gloves.Player2.HandZone.Count; hand = gloves.Player1.HandZone.Count;
        Play(gloves, gloves.Player1, "LOOTA_834");
        Check(gloves.Player2.HandZone.Count == theirs - 3 && gloves.Player1.HandZone.Count == hand + 3
            && gloves.Player1.HandZone.Count(p => p.Card.Id == "CS2_120") == 3 && gloves.Player1.HandZone.All(p => p.Controller == gloves.Player1),
            "Gloves of Mugging: 3 cards stolen");

        // Wish: a board of Legendary minions and a fully healed hero.
        Game wish = Fight();
        wish.Player1.Hero.Damage = 20;
        Play(wish, wish.Player1, "LOOTA_814");
        Check(wish.Player1.BoardZone.Count == 7 && wish.Player1.BoardZone.All(m => m.Card.Rarity == Rarity.LEGENDARY) && wish.Player1.Hero.Damage == 0,
            "Wish: seven Legendaries, full Health");
    }
}
