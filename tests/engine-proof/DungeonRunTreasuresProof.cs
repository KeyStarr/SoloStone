using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

// Dungeon Run's passive treasures (data/runs/loot-v1.json): each resolves as Start of Game from the loadout, keeps working
// after a snapshot, and changes only what the treasure says.
internal static class DungeonRunTreasuresProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "dungeon run treasures: " + message);

    private static Game Fight(string treasure, string[] deck = null, int health = 0, CardClass hero = CardClass.MAGE)
    {
        var config = new GameConfig {
            StartPlayer = 1, Player1HeroClass = hero, Player2HeroCard = Cards.FromId("LOOTA_BOSS_42h"), Player2HeroClass = CardClass.SHAMAN,
            Player1Deck = (deck ?? Enumerable.Repeat("CS2_182", 10).ToArray()).Select(Cards.FromId).ToList(),
            Player2Deck = Enumerable.Repeat("CS2_120", 10).Select(Cards.FromId).ToList(),
            Shuffle = false, SkipMulligan = true, History = true, RandomSeed = 11, SkipCoin = true,
            Player1StartingHealth = health, Player1Loadout = treasure == null ? null : new[] { treasure },
        };
        var game = new Game(config);
        game.StartGame();
        return game;
    }

    private static Game Reload(Game game) => Game.LoadSnapshot(game.SaveSnapshot());

    private static Game[] Both(string treasure, string[] deck = null) => new[] { Fight(treasure, deck), Reload(Fight(treasure, deck)) };

    [GroupProof("dungeon-run-treasures")]
    internal static void Run()
    {
        Game plain = Fight(null);

        // Battle Totem / Totem of the Dead: the player's Battlecries / Deathrattles trigger twice; the boss's do not.
        foreach (Game totem in Both("LOOTA_846"))
        {
            int hand = totem.Player1.HandZone.Count;
            Play(totem, totem.Player1, "EX1_015");
            Check(totem.Player1.HandZone.Count == hand + 2, "Battle Totem: Battlecry twice");
            End(totem);
            hand = totem.Player2.HandZone.Count;
            Play(totem, totem.Player2, "EX1_015");
            Check(totem.Player2.HandZone.Count == hand + 1, "Battle Totem: only the player's");
        }
        foreach (Game dead in Both("LOOTA_845"))
        {
            var hoarder = Summon(dead.Player1, "EX1_096");
            int hand = dead.Player1.HandZone.Count;
            Kill(dead, hoarder);
            Check(dead.Player1.HandZone.Count == hand + 2, "Totem of the Dead: Deathrattle twice");
        }

        // Captured Flag: the player's minions have +1/+1, the boss's do not.
        foreach (Game flag in Both("LOOTA_828"))
        {
            var yeti = Summon(flag.Player1, "CS2_182"); var croc = Summon(flag.Player2, "CS2_120");
            Check(yeti.AttackDamage == 5 && yeti.Health == 6 && croc.AttackDamage == 2, "Captured Flag: +1/+1");
            Check(flag.Player1.AppliedEnchantments?.Any(e => e.Card.Id == "LOOTA_828e2") == true, "Captured Flag: the player enchantment");
        }

        // Cloak of Invisibility: the player's minions are stealthed and stay so after attacking.
        foreach (Game cloak in Both("LOOTA_832"))
        {
            var yeti = Summon(cloak.Player1, "CS2_182");
            Check(yeti.HasStealth, "Cloak of Invisibility: Stealth");
            Attack(cloak, yeti, cloak.Player2.Hero);
            Check(yeti.HasStealth && cloak.Player2.Hero.Damage == 4, "Cloak of Invisibility: permanent Stealth");
        }
        var bare = Summon(plain.Player1, "CS2_182");
        Check(!bare.HasStealth, "no treasure, no Stealth");

        // Crystal Gem: one more Mana Crystal than usual on the first turn.
        Check(Fight("LOOTA_801").Player1.BaseMana == plain.Player1.BaseMana + 1, "Crystal Gem: an extra Mana Crystal");

        // Glyph of Warding: the boss's minions cost (1) more.
        Game glyph = Fight("LOOTA_831");
        Check(glyph.Player2.HandZone.Concat(glyph.Player2.DeckZone).All(p => p.Cost == 3), "Glyph of Warding: enemy minions cost (1) more");

        // Grommash's Armguards: weapons cost (1); Khadgar's Scrying Orb: spells cost (1) less; Scepter of Summoning: minions
        // costing (5) or more cost (5). Only cards present at the start: a card added later keeps its cost.
        string[] mixed = { "CS2_106", "CS2_106", "CS2_029", "CS2_029", "EX1_620", "EX1_620", "CS2_182", "CS2_182", "CS2_200", "CS2_200" };
        Game arms = Fight("LOOTA_818", mixed);
        Check(arms.Player1.HandZone.Concat(arms.Player1.DeckZone).Where(p => p is Weapon).All(p => p.Cost == 1), "Grommash's Armguards: weapons cost (1)");
        Game orb = Fight("LOOTA_824", mixed);
        Check(orb.Player1.HandZone.Concat(orb.Player1.DeckZone).Where(p => p.Card.Id == "CS2_029").All(p => p.Cost == 3), "Khadgar's Scrying Orb: (1) less");
        Check(Add(orb.Player1, "CS2_029").Cost == 4, "Khadgar's Scrying Orb: a Fireball added later costs (4)");
        Game scepter = Fight("LOOTA_803", mixed);
        var all = scepter.Player1.HandZone.Concat(scepter.Player1.DeckZone).ToList();
        Check(all.Where(p => p.Card.Id == "EX1_620" || p.Card.Id == "CS2_200").All(p => p.Cost == 5) && all.Where(p => p.Card.Id == "CS2_182").All(p => p.Cost == 4),
            "Scepter of Summoning: big minions cost (5)");

        // Justicar's Ring: the Hero Power is the upgraded one and costs (1).
        foreach (var (cls, upgraded) in new[] { (CardClass.MAGE, "AT_132_MAGE"), (CardClass.WARRIOR, "AT_132_WARRIOR"), (CardClass.PRIEST, "AT_132_PRIEST") })
        {
            Game ring = Fight("LOOTA_802", hero: cls);
            Check(ring.Player1.Hero.HeroPower.Card.Id == upgraded && ring.Player1.Hero.HeroPower.Cost == 1, "Justicar's Ring: " + upgraded + " for (1)");
        }

        // Mysterious Tome: three different Secrets in play.
        foreach (Game tome in Both("LOOTA_833"))
            Check(tome.Player1.SecretZone.Count == 3 && tome.Player1.SecretZone.Select(s => s.Card.Id).Distinct().Count() == 3, "Mysterious Tome: 3 Secrets");

        // Potion of Vitality: double the starting Health, a run's too.
        Check(Fight("LOOTA_800").Player1.Hero.Health == 60, "Potion of Vitality: 60 Health");
        Game potion = Fight("LOOTA_800", health: 25);
        Check(potion.Player1.Hero.Health == 50 && potion.Player1.Hero.BaseHealth == 50, "Potion of Vitality: a run's 25 becomes 50");

        // Robe of the Magi: Spell Damage +3.
        Game robe = Fight("LOOTA_825");
        Play(robe, robe.Player1, "CS2_008", robe.Player2.Hero);
        Check(robe.Player2.Hero.Damage == 4, "Robe of the Magi: Moonfire deals 4");

        // Small Backpacks: two more cards at the start.
        Check(Fight("LOOTA_804").Player1.HandZone.Count == plain.Player1.HandZone.Count + 2, "Small Backpacks: 2 cards");
    }
}
