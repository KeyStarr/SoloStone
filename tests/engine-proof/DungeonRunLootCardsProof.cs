using System;
using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// The legacy cards Dungeon Run's loot buckets needed (engine/.../Sets/Legacy/LegacyLootCards.cs).
internal static class DungeonRunLootCardsProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "dungeon run loot cards: " + message);

    // The opening hand and first draw take the deck's first four cards.
    private const string Opening = "CS2_182";

    private static Game Fight(CardClass cls = CardClass.MAGE, int seed = 11, params string[] deck)
    {
        var config = new GameConfig {
            StartPlayer = 1, Player1HeroClass = cls, Player2HeroClass = CardClass.WARRIOR,
            Player1Deck = deck.Concat(Enumerable.Repeat("CS2_182", 20 - deck.Length)).Select(Cards.FromId).ToList(),
            Player2Deck = Enumerable.Repeat("CS2_120", 20).Select(Cards.FromId).ToList(),
            Shuffle = false, SkipMulligan = true, History = true, RandomSeed = seed, SkipCoin = true,
        };
        var game = new Game(config);
        game.StartGame();
        return game;
    }

    private static void Minions()
    {
        Game monkey = Fight();
        Check(Summon(monkey.Player1, "LOE_022").HasTaunt, "Fierce Monkey: Taunt");

        foreach (bool reload in new[] { false, true })
        {
            Game obsidian = Fight();
            Summon(obsidian.Player1, "LOE_009");
            if (reload) obsidian = Game.LoadSnapshot(obsidian.SaveSnapshot());
            End(obsidian);
            var scarab = obsidian.Player1.BoardZone.SingleOrDefault(m => m.Card.Id == "LOE_009t");
            Check(scarab != null && scarab.HasTaunt && scarab.AttackDamage == 1 && scarab.Health == 1, "Obsidian Destroyer: a 1/1 Taunt Scarab at end of turn");
            End(obsidian);
            Check(obsidian.Player1.BoardZone.Count(m => m.Card.Id == "LOE_009t") == 1, "Obsidian Destroyer: only on its owner's turn");
        }

        Game eadric = Fight();
        var big = Summon(eadric.Player2, "CS2_182");
        var mine = Summon(eadric.Player1, "CS2_120");
        Play(eadric, eadric.Player1, "AT_081");
        Check(big.AttackDamage == 1 && big.Health == 5 && mine.AttackDamage == 2, "Eadric the Pure: enemy minions' Attack becomes 1");

        Game challenger = Fight(CardClass.PALADIN, 11, Opening, Opening, Opening, Opening, "EX1_130", "EX1_130", "EX1_136", "EX1_132");
        Play(challenger, challenger.Player1, "AT_079");
        var secrets = challenger.Player1.SecretZone.Select(s => s.Card.Id).OrderBy(id => id).ToList();
        Check(secrets.SequenceEqual(new[] { "EX1_130", "EX1_132", "EX1_136" }) && challenger.Player1.DeckZone.Count(p => p.Card.Id == "EX1_130") == 1,
            "Mysterious Challenger: one of each Secret from the deck");

        Game voljin = Fight();
        var other = Summon(voljin.Player2, "CS2_182");
        other.Damage = 2;
        var troll = (Minion)Play(voljin, voljin.Player1, "GVG_014", other);
        Check(troll.Health == 3 && other.Health == 2 && other.Damage == 0, "Vol'jin: Health swapped (" + troll.Health + "/" + other.Health + ")");

        bool[] seen = new bool[3];
        for (int seed = 0; seed < 8; seed++)
        {
            Game mechano = Fight(CardClass.SHAMAN, seed);
            var friends = Enumerable.Range(0, 3).Select(i => Summon(mechano.Player1, "CS2_120")).ToList();
            var enemy = Summon(mechano.Player2, "CS2_120");
            var self = (Minion)Play(mechano, mechano.Player1, "GVG_107");
            foreach (Minion m in friends)
            {
                int keywords = (m.HasWindfury ? 1 : 0) + (m.HasTaunt ? 1 : 0) + (m.HasDivineShield ? 1 : 0);
                Check(keywords == 1, "Enhance-o Mechano: each other friendly minion gets one keyword");
                seen[0] |= m.HasWindfury; seen[1] |= m.HasTaunt; seen[2] |= m.HasDivineShield;
            }
            Check(!self.HasWindfury && !self.HasTaunt && !self.HasDivineShield && !enemy.HasTaunt && !enemy.HasDivineShield,
                "Enhance-o Mechano: not itself, not the enemy's");
        }
        Check(seen.All(s => s), "Enhance-o Mechano: all three keywords occur");

        Game bling = Fight(CardClass.HUNTER);
        Play(bling, bling.Player1, "GVG_119");
        Check(bling.Player1.Hero.Weapon != null && bling.Player2.Hero.Weapon != null, "Blingtron 3000: a weapon for each player");
    }

    private static void Inventions()
    {
        var found = new System.Collections.Generic.HashSet<string>();
        for (int seed = 0; seed < 16; seed++)
        {
            Game gelbin = Fight(CardClass.SHAMAN, seed);
            Play(gelbin, gelbin.Player1, "EX1_112");
            var invention = gelbin.Player1.BoardZone.Where(m => m.Card.Id != "EX1_112").ToList();
            Check(invention.Count == 1 && invention[0].Card.Id.StartsWith("Mekka", StringComparison.Ordinal), "Gelbin Mekkatorque: one invention");
            found.Add(invention[0].Card.Id);
        }
        Check(found.Count >= 3, "Gelbin Mekkatorque: the inventions vary");

        Game chicken = Fight();
        Summon(chicken.Player1, "Mekka1");
        End(chicken);
        int hand = chicken.Player1.HandZone.Count;
        End(chicken);
        Check(chicken.Player1.BoardZone.All(m => m.Card.Id != "Mekka1") && chicken.Player1.HandZone.Count == hand + 4,
            "Homing Chicken: destroyed at the start of its owner's turn, 3 cards drawn");

        Game repair = Fight();
        repair.Player1.Hero.Damage = 10;
        Summon(repair.Player1, "Mekka2");
        End(repair);
        Check(repair.Player1.Hero.Damage == 4, "Repair Bot: 6 Health to a damaged character");

        Game embolden = Fight();
        var bot = Summon(embolden.Player1, "Mekka3");
        End(embolden);
        Check(bot.AttackDamage == 1 && bot.Health == 5, "Emboldener 3000: +1/+1 to a random minion");

        Game poultry = Fight();
        Summon(poultry.Player1, "Mekka4");
        End(poultry); End(poultry);
        Check(poultry.Player1.BoardZone.Count == 1 && poultry.Player1.BoardZone[0].Card.Id == "Mekka4t", "Poultryizer: a random minion becomes a Chicken");
    }

    private static void Spells()
    {
        Game lone = Fight(CardClass.WARRIOR);
        var target = Summon(lone.Player2, "CS2_120");
        Play(lone, lone.Player1, "GVG_050");
        Check(target.IsDead && lone.Player2.BoardZone.Count == 0, "Bouncing Blade: repeats until the minion dies");
        for (int seed = 0; seed < 5; seed++)
        {
            Game pair = Fight(CardClass.WARRIOR, seed);
            var a = Summon(pair.Player1, "CS2_182"); var b = Summon(pair.Player2, "CS2_120");
            Play(pair, pair.Player1, "GVG_050");
            Check(a.IsDead != b.IsDead && a.Damage + b.Damage >= 3, "Bouncing Blade: stops when one minion dies");
        }

        Game rafaam = Fight(CardClass.WARRIOR);
        Play(rafaam, rafaam.Player1, "LOE_092");
        var choice = rafaam.Player1.Choice;
        Check(choice != null && choice.Choices.Select(id => rafaam.IdEntityDic[id].Card.Id).OrderBy(id => id)
            .SequenceEqual(new[] { "LOEA16_3", "LOEA16_4", "LOEA16_5" }), "Arch-Thief Rafaam: the three Artifacts");
        int mirror = choice.Choices.First(id => rafaam.IdEntityDic[id].Card.Id == "LOEA16_5");
        Check(rafaam.Process(ChooseTask.Pick(rafaam.Player1, mirror)) && rafaam.Player1.HandZone.Any(p => p.Card.Id == "LOEA16_5"), "Rafaam: the pick goes to hand");
        PlayEntity(rafaam, rafaam.Player1, rafaam.Player1.HandZone.First(p => p.Card.Id == "LOEA16_5"));
        Check(rafaam.Player1.BoardZone.Count == 7 && rafaam.Player1.BoardZone.Count(m => m.Card.Id == "LOEA16_5t") == 6, "Mirror of Doom: the board filled with Mummy Zombies");

        Game lantern = Fight();
        var lit = Summon(lantern.Player1, "CS2_120");
        Play(lantern, lantern.Player1, "LOEA16_3", lit);
        Check(lit.AttackDamage == 12 && lit.Health == 13, "Lantern of Power: +10/+10");

        Game timepiece = Fight();
        var foe = Summon(timepiece.Player2, "CS2_182");
        Play(timepiece, timepiece.Player1, "LOEA16_4");
        // A 4/5 that dies takes 5 of the 10 missiles; otherwise every missile lands on it or the hero.
        Check(foe.IsDead ? timepiece.Player2.Hero.Damage == 5 : timepiece.Player2.Hero.Damage + foe.Damage == 10,
            "Timepiece of Horror: 10 damage split among enemies");
        Game bare = Fight();
        Play(bare, bare.Player1, "LOEA16_4");
        Check(bare.Player2.Hero.Damage == 10, "Timepiece of Horror: all 10 to the hero alone");
    }

    private static void Reactions()
    {
        Game djinni = Fight(CardClass.PRIEST);
        var genie = Summon(djinni.Player1, "LOE_053");
        var friend = Summon(djinni.Player1, "CS2_182");
        Play(djinni, djinni.Player1, "CS2_004", friend);
        Check(friend.Health == 7 && genie.Health == 8, "Djinni of Zephyrs: a copy of the spell on the Djinni");
        Play(djinni, djinni.Player1, "CS2_004", genie);
        Check(genie.Health == 10 && friend.Health == 7, "Djinni of Zephyrs: a spell on the Djinni itself is not copied");
        Play(djinni, djinni.Player1, "CS1_130", djinni.Player2.Hero);
        Check(genie.Damage == 0, "Djinni of Zephyrs: a spell on an enemy is not copied");

        foreach (bool reload in new[] { false, true })
        {
            Game wilfred = Fight(CardClass.WARLOCK, 11, Opening, Opening, Opening, Opening, "CS2_186");
            Summon(wilfred.Player1, "AT_027");
            if (reload) wilfred = Game.LoadSnapshot(wilfred.SaveSnapshot());
            wilfred.Player1.BaseMana = 10; wilfred.Player1.UsedMana = 0;
            var power = wilfred.Player1.Options().First(o => o.PlayerTaskType == PlayerTaskType.HERO_POWER);
            Check(wilfred.Process(power), "Wilfred Fizzlebang: Life Tap used");
            var drawn = wilfred.IdEntityDic[wilfred.Player1.LastCardDrawn] as IPlayable;
            Check(drawn != null && drawn.Card.Id == "CS2_186" && drawn.Cost == 0, "Wilfred Fizzlebang: a card drawn by the Hero Power costs (0)");
            End(wilfred); End(wilfred);
            var normal = wilfred.IdEntityDic[wilfred.Player1.LastCardDrawn] as IPlayable;
            Check(normal.Cost == normal.Card.Cost, "Wilfred Fizzlebang: a normal draw keeps its cost");
        }
    }

    [GroupProof("dungeon-run-loot-cards")]
    internal static void Run()
    {
        Minions();
        Inventions();
        Spells();
        Reactions();
    }
}
