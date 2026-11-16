using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Dungeon Run's final (round 8) bosses (data/runs/loot-v1.json) and their special starts.
internal static class DungeonRunFinalsProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "dungeon run finals: " + message);

    private static PlayerTask Power(Controller player) =>
        player.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER);

    private static void Use(Game game)
    {
        var task = Power(game.Player2);
        Check(task != null && game.Process(task), "hero power accepted: " + game.Player2.Hero.HeroPower.Card.Id);
    }

    private static void BossTurn(Game game)
    {
        End(game);
        game.Player2.BaseMana = 10; game.Player2.UsedMana = 0;
    }

    private static Game Reload(Game game) => Game.LoadSnapshot(game.SaveSnapshot());

    private static readonly string[] Chests = Enumerable.Repeat("LOOT_357l", 5).ToArray();
    private static readonly string[] Candles = Enumerable.Repeat("LOOTA_BOSS_49t2", 3).ToArray();

    [GroupProof("dungeon-run-finals")]
    internal static void Run()
    {
        foreach (string hero in new[] { "LOOTA_BOSS_34h", "LOOTA_BOSS_49h", "LOOTA_BOSS_25h", "LOOTA_BOSS_26h", "LOOTA_BOSS_99h" })
            Check(DungeonRunRound1Proof.Fight(hero).Player2.Hero.Health == 70, hero + ": 70 Health");

        // Azari, Devour: the top 2 cards of the player's deck go to the graveyard.
        Game azari = DungeonRunRound1Proof.Fight("LOOTA_BOSS_34h");
        BossTurn(azari);
        int deck = azari.Player1.DeckZone.Count, grave = azari.Player1.GraveyardZone.Count;
        Use(azari);
        Check(azari.Player1.DeckZone.Count == deck - 2 && azari.Player1.GraveyardZone.Count == grave + 2, "Devour: 2 cards removed");

        // The Darkness: the player holds three Luminous Candles from the start (after the opening hand, kept by a snapshot);
        // Encroaching Darkness summons a 5/5 Darkspawn; a Candle destroys every Darkspawn and nothing else.
        foreach (Game darkness in new[] { DungeonRunRound1Proof.Fight("LOOTA_BOSS_49h", hand: Candles),
                     Reload(DungeonRunRound1Proof.Fight("LOOTA_BOSS_49h", hand: Candles)) })
        {
            Check(darkness.Player1.HandZone.Count(p => p.Card.Id == "LOOTA_BOSS_49t2") == 3, "three Luminous Candles in hand");
            BossTurn(darkness);
            Use(darkness);
            var spawn = darkness.Player2.BoardZone.Single();
            Check(spawn.Card.Id == "LOOTA_BOSS_49t" && spawn.AttackDamage == 5 && spawn.Health == 5, "Encroaching Darkness: a 5/5 Darkspawn");
            var other = Summon(darkness.Player2, "CS2_120");
            End(darkness);
            var candle = darkness.Player1.HandZone.First(p => p.Card.Id == "LOOTA_BOSS_49t2");
            PlayEntity(darkness, darkness.Player1, candle);
            Check(spawn.IsDead && !other.IsDead, "Luminous Candle: Darkspawns destroyed");
        }

        // Vustrasz: five Master Chests in play; Vindictive Breath is idle while all stand and then hits every enemy once per
        // missing Chest; a broken Chest gives the player a treasure.
        foreach (Game vustrasz in new[] { DungeonRunRound1Proof.Fight("LOOTA_BOSS_25h", board: Chests),
                     Reload(DungeonRunRound1Proof.Fight("LOOTA_BOSS_25h", board: Chests)) })
        {
            Check(vustrasz.Player2.BoardZone.Count(m => m.Card.Id == "LOOT_357l") == 5, "five Master Chests");
            var yeti = Summon(vustrasz.Player1, "CS2_182");
            BossTurn(vustrasz);
            Check(Power(vustrasz.Player2) == null, "Vindictive Breath: idle with every Chest");
            int hand = vustrasz.Player1.HandZone.Count;
            Kill(vustrasz, vustrasz.Player2.BoardZone[0]);
            Kill(vustrasz, vustrasz.Player2.BoardZone[0]);
            Check(vustrasz.Player1.HandZone.Count == hand + 2 && vustrasz.Player1.HandZone.Skip(hand).All(p => p.Card.Id.StartsWith("LOOT_998")),
                "Master Chest: a treasure each");
            Use(vustrasz);
            Check(vustrasz.Player1.Hero.Damage == 2 && yeti.Damage == 2, "Vindictive Breath: 2 damage for 2 missing Chests");
        }
        // Chromatic Dragonkin: +2/+2 for each spell the opponent casts.
        Game dragon = DungeonRunRound1Proof.Fight("LOOTA_BOSS_25h");
        var kin = Summon(dragon.Player2, "BRMA12_8t");
        Play(dragon, dragon.Player1, "CS2_008", dragon.Player2.Hero);
        Check(kin.AttackDamage == 4 && kin.Health == 5, "Chromatic Dragonkin: +2/+2");

        // Xol, Beam Me Up!: a Beam in hand at the end of each of her turns, not the player's.
        foreach (Game xol in new[] { DungeonRunRound1Proof.Fight("LOOTA_BOSS_26h"), Reload(DungeonRunRound1Proof.Fight("LOOTA_BOSS_26h")) })
        {
            int hand = xol.Player2.HandZone.Count;
            End(xol);
            Check(xol.Player2.HandZone.Count == hand + 1, "the boss draws for its turn");
            End(xol);
            Check(xol.Player2.HandZone.Count == hand + 2 && xol.Player2.HandZone.Last().Card.Id.StartsWith("LOOTA_BOSS_26p"),
                "Beam Me Up!: a Beam at the end of her turn");
            Check(Power(xol.Player2) == null, "Beam Me Up! is never used");
        }
        Beams();
        Togwaggle();
    }

    private static readonly string[] TogwaggleTreasures = {
        "LOOTA_819", "LOOTA_836", "LOOTA_823", "LOOTA_812", "LOOTA_838", "LOOTA_827", "LOOT_998k", "LOOTA_835",
        "LOOTA_847", "LOOTA_816", "LOOTA_826", "LOOTA_839", "LOOTA_843", "LOOTA_821", "LOOTA_806", "LOOTA_842b"
    };

    // King Togwaggle, Magic Candle (3): a random treasure from the wiki's list of outcomes, into his hand.
    private static void Togwaggle()
    {
        var found = new System.Collections.Generic.HashSet<string>();
        for (int seed = 0; seed < 24; seed++)
        {
            Game king = DungeonRunRound1Proof.Fight("LOOTA_BOSS_99h", seed);
            if (seed % 2 == 1) king = Reload(king);
            BossTurn(king);
            Check(king.Player2.Hero.HeroPower.Cost == 3, "Magic Candle costs (3)");
            int hand = king.Player2.HandZone.Count;
            Use(king);
            var treasure = king.Player2.HandZone.Last();
            Check(king.Player2.HandZone.Count == hand + 1 && TogwaggleTreasures.Contains(treasure.Card.Id)
                && treasure[GameTag.DISPLAYED_CREATOR] == king.Player2.Hero.HeroPower.Id, "Magic Candle: a listed treasure in hand");
            found.Add(treasure.Card.Id);
        }
        Check(found.Count >= 8, "Magic Candle: the treasures vary (" + found.Count + ")");

        Game full = DungeonRunRound1Proof.Fight("LOOTA_BOSS_99h");
        BossTurn(full);
        while (!full.Player2.HandZone.IsFull) full.Player2.HandZone.Add(SabberStoneCore.Model.Entities.Entity.FromCard(full.Player2, Cards.FromId("CS2_120")));
        Check(Power(full.Player2) == null, "Magic Candle: not used with a full hand");

        // Each untargeted treasure he can find resolves from his side.
        foreach (string id in TogwaggleTreasures.Where(id => id != "LOOTA_847" && id != "LOOTA_842b"))
        {
            Game game = DungeonRunRound1Proof.Fight("LOOTA_BOSS_99h");
            Summon(game.Player1, "CS2_182");
            BossTurn(game);
            var played = Play(game, game.Player2, id);
            Check(played.Zone == null || played.Zone.Type != Zone.HAND, "the boss plays " + id);
        }
        Game hilt = DungeonRunRound1Proof.Fight("LOOTA_BOSS_99h");
        BossTurn(hilt);
        var ally = Summon(hilt.Player2, "CS2_120");
        Play(hilt, hilt.Player2, "LOOTA_842b", ally);
        Check(ally.AttackDamage == 5 && ally.Health == 6, "the boss plays Hilt of Quel'Delar: +3/+3");

        // Rakanishu: a hidden-stats spell (HIDE_STATS: never played from hand, the game casts it); 4 damage to a random enemy.
        Game raka = DungeonRunRound1Proof.Fight("LOOTA_BOSS_99h");
        BossTurn(raka);
        var rakanishu = (Spell)SabberStoneCore.Model.Entities.Entity.FromCard(raka.Player2, Cards.FromId("LOOTA_BOSS_99t"));
        Check(rakanishu.Card.HideStat, "Rakanishu is cast, not played");
        raka.TaskQueue.StartEvent();
        SabberStoneCore.Actions.Generic.CastSpell.Invoke(raka.Player2, raka, rakanishu, null, 0);
        raka.TaskQueue.EndEvent();
        raka.DeathProcessingAndAuraUpdate();
        Check(raka.Player1.Hero.Damage == 4, "Rakanishu: 4 damage to the only enemy");
    }

    private static Game XolTurn(params string[] playerMinions)
    {
        Game game = DungeonRunRound1Proof.Fight("LOOTA_BOSS_26h");
        foreach (string id in playerMinions) Summon(game.Player1, id);
        End(game);
        return game;
    }

    private static void Beams()
    {
        // Frost: a random enemy minion and its neighbours frozen.
        for (int seed = 0; seed < 4; seed++)
        {
            Game frost = DungeonRunRound1Proof.Fight("LOOTA_BOSS_26h", seed);
            Summon(frost.Player1, "CS2_182"); Summon(frost.Player1, "CS2_182"); Summon(frost.Player1, "CS2_182"); Summon(frost.Player1, "CS2_182");
            End(frost);
            Play(frost, frost.Player2, "LOOTA_BOSS_26p2");
            var frozen = frost.Player1.BoardZone.Select((m, i) => (m, i)).Where(x => x.m.IsFrozen).Select(x => x.i).ToArray();
            Check(frozen.Length >= 2 && frozen.Length <= 3 && frozen.Last() - frozen.First() == frozen.Length - 1, "Beam of Frost: neighbours frozen");
        }
        Game noTarget = XolTurn();
        var idle = Add(noTarget.Player2, "LOOTA_BOSS_26p2");
        noTarget.Player2.BaseMana = 10; noTarget.Player2.UsedMana = 0;
        Check(!noTarget.Player2.Options().Any(t => t.Source?.Id == idle.Id), "Beam of Frost: needs an enemy minion");

        // Death: damaged enemy minions die.
        Game death = XolTurn("CS2_182", "CS2_182");
        death.Player1.BoardZone[0].Damage = 1;
        Play(death, death.Player2, "LOOTA_BOSS_26p3");
        Check(death.Player1.BoardZone.Count == 1 && death.Player1.BoardZone[0].Damage == 0, "Beam of Death: the damaged one");

        // Confusion: an enemy minion changes sides.
        Game confusion = XolTurn("CS2_182");
        Play(confusion, confusion.Player2, "LOOTA_BOSS_26p4");
        Check(confusion.Player1.BoardZone.Count == 0 && confusion.Player2.BoardZone.Count == 1, "Beam of Confusion: taken");

        // Fear: an enemy minion back into its owner's deck.
        Game fear = XolTurn("CS2_182");
        int deck = fear.Player1.DeckZone.Count;
        Play(fear, fear.Player2, "LOOTA_BOSS_26p5");
        Check(fear.Player1.BoardZone.Count == 0 && fear.Player1.DeckZone.Count == deck + 1, "Beam of Fear: shuffled into the player's deck");

        // Fire: 2 damage to each enemy minion.
        Game fire = XolTurn("CS2_182", "CS2_120");
        Play(fire, fire.Player2, "LOOTA_BOSS_26p6");
        Check(fire.Player1.BoardZone.All(m => m.Damage == 2), "Beam of Fire: 2 each");

        // Decay: the player loses a Mana Crystal.
        Game decay = XolTurn();
        decay.Player1.BaseMana = 5; decay.Player1.UsedMana = 0;
        Play(decay, decay.Player2, "LOOTA_BOSS_26p7");
        Check(decay.Player1.BaseMana == 4, "Beam of Decay: a crystal destroyed");
    }
}
