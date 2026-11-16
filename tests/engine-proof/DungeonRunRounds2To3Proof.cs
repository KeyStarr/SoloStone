using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Dungeon Run round 2-3 bosses (data/runs/loot-v1.json) and the Rank 2 forms of the three that return in rounds 6-7.
internal static class DungeonRunRounds2To3Proof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "dungeon run rounds 2-3: " + message);

    private static PlayerTask Power(Controller player, ICharacter target = null) =>
        player.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER && t.Target?.Id == target?.Id);

    // The boss (player 2) on its own turn, with mana to spare.
    private static Game BossTurn(string boss, int seed = 11)
    {
        Game game = DungeonRunRound1Proof.Fight(boss, seed);
        End(game);
        game.Player2.BaseMana = 10; game.Player2.UsedMana = 0;
        return game;
    }

    private static void Use(Game game, ICharacter target = null)
    {
        var task = Power(game.Player2, target);
        Check(task != null && game.Process(task), "hero power accepted: " + game.Player2.Hero.HeroPower.Card.Id);
    }

    [GroupProof("dungeon-run-rounds2-3")]
    internal static void Run()
    {
        foreach (var (hero, health) in new[] { ("LOOTA_BOSS_12h", 20), ("LOOTA_BOSS_35h", 20), ("LOOTA_BOSS_09h", 20), ("LOOTA_BOSS_11h", 20),
                     ("LOOTA_BOSS_05h", 20), ("LOOTA_BOSS_40h", 20), ("LOOTA_BOSS_31h", 60), ("LOOTA_BOSS_30h", 60), ("LOOTA_BOSS_28h", 60) })
            Check(DungeonRunRound1Proof.Fight(hero).Player2.Hero.Health == health, hero + ": " + health + " Health");

        // Candlebeard, Charge!: a minion summoned this turn can attack at once.
        Game candle = BossTurn("LOOTA_BOSS_12h");
        var raider = Summon(candle.Player2, "CS2_120");
        Check(!raider.CanAttack, "a fresh minion cannot attack");
        Use(candle, raider);
        Check(raider.HasCharge && raider.CanAttack, "Charge!: the minion can attack");

        // Charrrrrge! (Rank 2): passive; every minion the boss plays gets Charge; the opponent's do not.
        Game charge = DungeonRunRound1Proof.Fight("LOOTA_BOSS_31h");
        var played = (Minion)Play(charge, charge.Player1, "CS2_120");
        Check(!played.HasCharge, "the opponent's minions get no Charge");
        End(charge);
        charge.Player2.BaseMana = 10; charge.Player2.UsedMana = 0;
        Check(Power(charge.Player2) == null && !charge.Player2.Hero.HeroPower.IsPlayable, "Charrrrrge! is never used");
        var bossMinion = (Minion)Play(charge, charge.Player2, "CS2_182");
        Check(bossMinion.HasCharge && bossMinion.CanAttack, "Charrrrrge!: a played minion has Charge");
        var summoned = Summon(charge.Player2, "CS2_120");
        Check(!summoned.HasCharge, "Charrrrrge!: a summoned (not played) minion does not");
        Game chargeLoaded = Game.LoadSnapshot(charge.SaveSnapshot());
        var after = (Minion)Play(chargeLoaded, chargeLoaded.Player2, "CS2_182");
        Check(after.HasCharge, "Charrrrrge! survives a snapshot");
        var chargeClone = charge.CloneExact(history: true);
        Check(((Minion)Play(chargeClone, chargeClone.Player2, "CS2_182")).HasCharge, "Charrrrrge! survives a clone");

        // Elder Brandlemar, Dampen Magic: a Counterspell goes up; it counters the opponent's next spell; one at a time.
        Game brand = BossTurn("LOOTA_BOSS_35h");
        Use(brand);
        Check(brand.Player2.SecretZone.Count == 1 && brand.Player2.SecretZone[0].Card.Id == "EX1_287", "Dampen Magic: a Counterspell");
        brand.Player2.UsedMana = 0;
        Check(Power(brand.Player2) == null, "no second Counterspell while one is up");
        Game brandLoaded = Game.LoadSnapshot(brand.SaveSnapshot());
        Check(HsSim.Canonical.State(brandLoaded) == HsSim.Canonical.State(brand), "a Counterspell from the Hero Power survives a snapshot");
        End(brand);
        int bossHealth = brand.Player2.Hero.Health;
        Play(brand, brand.Player1, "CS2_029", brand.Player2.Hero);
        Check(brand.Player2.Hero.Health == bossHealth && brand.Player2.SecretZone.Count == 0, "the Counterspell counters a Fireball");
        brand.Player2.BaseMana = 10; brand.Player2.UsedMana = 0;
        End(brand);
        brand.Player2.BaseMana = 10; brand.Player2.UsedMana = 0;
        Check(Power(brand.Player2) != null, "Dampen Magic is usable again once the Secret is gone");

        // Frostfur, Freeze: Freeze a minion.
        Game frost = BossTurn("LOOTA_BOSS_09h");
        var yeti = Summon(frost.Player1, "CS2_182");
        Check(Power(frost.Player2, frost.Player1.Hero) == null, "Freeze takes only a minion");
        Use(frost, yeti);
        Check(yeti.IsFrozen, "Freeze: the minion is Frozen");

        // Graves the Cleric, Light's Will (0): Restore 2 Health to all minions, both sides. (Graves's wiki deck is not yet
        // playable, so he is not in the run; the power is.)
        Game graves = BossTurn("LOOTA_BOSS_10h");
        var mine = Summon(graves.Player2, "CS2_182"); var theirs = Summon(graves.Player1, "CS2_182");
        mine.Damage = 3; theirs.Damage = 1;
        Use(graves);
        Check(mine.Health == 4 && theirs.Health == 5, "Light's Will: 2 Health to every minion");

        // Overseer Mogark, Cruel Words: 1 damage and +2 Attack to a friendly minion; Searing Lash: +5.
        foreach (var (hero, attack) in new[] { ("LOOTA_BOSS_11h", 2), ("LOOTA_BOSS_30h", 5) })
        {
            Game mogark = BossTurn(hero);
            var own = Summon(mogark.Player2, "CS2_182"); var enemy = Summon(mogark.Player1, "CS2_182");
            Check(Power(mogark.Player2, enemy) == null, hero + ": no enemy target");
            Use(mogark, own);
            Check(own.Health == 4 && own.AttackDamage == 4 + attack, hero + ": 1 damage and +" + attack + " Attack");
        }

        // Pathmaker Hamm (autocast): 1 damage to two different random enemies; Rank 2: 2 damage to three.
        foreach (var (hero, hits, damage) in new[] { ("LOOTA_BOSS_05h", 2, 1), ("LOOTA_BOSS_28h", 3, 2) })
        for (int seed = 0; seed < 3; seed++)
        {
            Game hamm = BossTurn(hero, seed);
            for (int i = 0; i < 3; i++) Summon(hamm.Player1, "CS2_182");
            var forced = BotDriver.Forced(hamm, hamm.Player2.Options(), out _);
            Check(forced?.PlayerTaskType == PlayerTaskType.HERO_POWER, hero + ": the AI fires the autocast power");
            Check(hamm.Process(forced), hero + ": power accepted");
            var hit = hamm.Player1.BoardZone.Cast<ICharacter>().Append(hamm.Player1.Hero)
                .Where(c => c.Damage > 0).ToList();
            Check(hit.Count == hits && hit.All(c => c.Damage == damage), hero + ": " + hits + " different enemies, " + damage + " each");
            Check(hamm.Player2.BoardZone.Count == 0 && hamm.Player2.Hero.Damage == 0, hero + ": no friendly target");
        }

        // Seriona, Fading Light: -1 Attack, never below 0.
        Game seriona = BossTurn("LOOTA_BOSS_40h");
        var wisp = Summon(seriona.Player1, "CS2_231");
        var croc = Summon(seriona.Player1, "CS2_120");
        Use(seriona, croc);
        Check(croc.AttackDamage == 1, "Fading Light: -1 Attack");
        seriona.Player2.UsedMana = 0; seriona.Player2.Hero.HeroPower.IsExhausted = false;
        Use(seriona, wisp);
        Check(wisp.AttackDamage == 0, "Fading Light: Attack stays at 0");
    }
}
