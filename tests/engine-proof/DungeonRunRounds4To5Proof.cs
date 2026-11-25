using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Dungeon Run round 4-5 bosses (data/runs/loot-v1.json), the Rank 2 forms of three that return in rounds 6-7, and the
// legacy cards their decks need.
internal static class DungeonRunRounds4To5Proof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "dungeon run rounds 4-5: " + message);

    private static PlayerTask Power(Controller player, ICharacter target = null) =>
        player.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER && t.Target?.Id == target?.Id);

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

    private static Game Reload(Game game) => Game.LoadSnapshot(game.SaveSnapshot());

    // A. F. Kay: no card of hers is playable before her sixth turn (snapshots keep the rule); then she plays as usual. Idle
    // does nothing.
    private static void AFKay()
    {
        foreach (bool reload in new[] { false, true })
        {
            Game kay = DungeonRunRound1Proof.Fight("LOOTA_BOSS_54h");
            if (reload) kay = Game.LoadSnapshot(kay.SaveSnapshot());
            Check(kay.Player2.Hero.Health == 40, "A. F. Kay: 40 Health");
            for (int turn = 1; turn <= 6; turn++)
            {
                End(kay);
                kay.Player2.BaseMana = 10; kay.Player2.UsedMana = 0;
                if (turn == 3) kay = Game.LoadSnapshot(kay.SaveSnapshot());
                bool plays = kay.Player2.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD);
                Check(plays == (turn == 6), "A. F. Kay: turn " + turn + (turn == 6 ? " plays cards" : " plays no card"));
                if (turn < 6) End(kay);
            }
        }
        Game idle = BossTurn("LOOTA_BOSS_54h");
        int hand = idle.Player2.HandZone.Count, board = idle.Player1.BoardZone.Count + idle.Player2.BoardZone.Count;
        Use(idle);
        Check(idle.Player2.HandZone.Count == hand && idle.Player1.BoardZone.Count + idle.Player2.BoardZone.Count == board
            && idle.Player1.Hero.Damage == 0, "Idle: nothing happens");
    }

    // Whompwhisker, Join the Fray: a minion from each player's deck onto each board. Mogor's Champion (Forgetful): about half
    // its attacks go to another enemy (at random, Stealth included) when there is one; with only the intended target there
    // is no wrong one.
    private static void Whompwhisker()
    {
        Game fray = BossTurn("LOOTA_BOSS_41h");
        Check(fray.Player2.Hero.Health == 40, "Whompwhisker: 40 Health");
        int bossDeck = fray.Player2.DeckZone.Count, playerDeck = fray.Player1.DeckZone.Count;
        Use(fray);
        Check(fray.Player2.BoardZone.Count == 1 && fray.Player2.BoardZone[0].Card.Id == "CS2_120" && fray.Player2.DeckZone.Count == bossDeck - 1
            && fray.Player1.BoardZone.Count == 1 && fray.Player1.BoardZone[0].Card.Id == "CS2_182" && fray.Player1.DeckZone.Count == playerDeck - 1,
            "Join the Fray: both players Recruit a minion");

        int wrong = 0, stealthHit = 0;
        const int trials = 60;
        for (int seed = 0; seed < trials; seed++)
        {
            Game game = DungeonRunRound1Proof.Fight("LOOTA_BOSS_41h", seed);
            var champion = Summon(game.Player1, "AT_088");
            var intended = Summon(game.Player2, "CS2_120");
            var hidden = Summon(game.Player2, "CS2_120");
            hidden.HasStealth = true;
            Attack(game, champion, intended);
            bool hitIntended = intended.Damage > 0 || intended.IsDead;
            bool hitOther = hidden.Damage > 0 || hidden.IsDead || game.Player2.Hero.Damage > 0;
            Check(hitIntended != hitOther, "Mogor's Champion: exactly one target is hit");
            if (hitOther) wrong++;
            if (hidden.Damage > 0 || hidden.IsDead) stealthHit++;
        }
        Check(wrong > trials / 4 && wrong < trials * 3 / 4, "Forgetful: about half the attacks go wrong (" + wrong + "/" + trials + ")");
        Check(stealthHit > 0, "Forgetful: a Stealthed minion can be the wrong target");
        for (int seed = 0; seed < 10; seed++)
        {
            Game alone = DungeonRunRound1Proof.Fight("LOOTA_BOSS_41h", seed);
            var champion = Summon(alone.Player1, "AT_088");
            Attack(alone, champion, alone.Player2.Hero);
            Check(alone.Player2.Hero.Damage == 8, "Forgetful: with no other enemy the hero is hit");
        }
    }

    private static readonly string[] Ingredients = { "CFM_621t2", "CFM_621t3", "CFM_621t4", "CFM_621t5", "CFM_621t6", "CFM_621t8",
        "CFM_621t9", "CFM_621t10", "CFM_621t37" };

    // Mushhuckster Max, Mushroom, Mushroom (2): a 1-Mana Mushroom Potion of two distinct random 1-Mana Kazakus effects, which
    // plays (also after a reload) with both effects.
    private static void Mushhuckster()
    {
        var seen = new System.Collections.Generic.HashSet<string>();
        for (int seed = 0; seed < 30; seed++)
        {
            Game max = BossTurn("LOOTA_BOSS_50h", seed);
            if (seed == 0) Check(max.Player2.Hero.Health == 40 && max.Player2.Hero.HeroPower.Cost == 2, "Mushhuckster Max: 40 Health, a 2-Mana power");
            int hand = max.Player2.HandZone.Count;
            Use(max);
            var potion = max.Player2.HandZone.Last();
            var parts = new[] { potion.Card[GameTag.MODULAR_ENTITY_PART_1], potion.Card[GameTag.MODULAR_ENTITY_PART_2] }
                .Select(id => Cards.FromAssetId(id).Id).ToArray();
            Check(max.Player2.HandZone.Count == hand + 1 && potion.Card.Id == "LOOTA_BOSS_50t" && potion.Cost == 1
                && parts[0] != parts[1] && parts.All(Ingredients.Contains), "Mushroom, Mushroom: a 1-Mana potion of two distinct effects");
            seen.UnionWith(parts);
        }
        Check(seen.Count == Ingredients.Length, "Mushroom Potions: all nine effects occur (" + seen.Count + ")");

        foreach (bool reload in new[] { false, true })
        {
            Game armor = BossTurn("LOOTA_BOSS_50h");
            var brew = SabberStoneCore.Actions.GeneratedDefinitionRules.Potion(Cards.FromId("CFM_621t3"), Cards.FromId("CFM_621t8"), "LOOTA_BOSS_50t");
            SabberStoneCore.Actions.Generic.DrawCard(armor.Player2, brew);
            if (reload) { armor = Game.LoadSnapshot(armor.SaveSnapshot()); armor.Player2.BaseMana = 10; armor.Player2.UsedMana = 0; }
            var held = armor.Player2.HandZone.Last();
            int cards = armor.Player2.HandZone.Count;
            PlayEntity(armor, armor.Player2, held);
            Check(armor.Player2.Hero.Armor == 4 && armor.Player2.HandZone.Count == cards, "Stonescale Oil + Kingsblood: 4 Armor and a card" + (reload ? " (after a reload)" : ""));
        }

        Game fire = BossTurn("LOOTA_BOSS_50h");
        var yeti = Summon(fire.Player1, "CS2_182");
        var mine = Summon(fire.Player2, "CS2_120");
        SabberStoneCore.Actions.Generic.DrawCard(fire.Player2,
            SabberStoneCore.Actions.GeneratedDefinitionRules.Potion(Cards.FromId("CFM_621t2"), Cards.FromId("CFM_621t4"), "LOOTA_BOSS_50t"));
        PlayEntity(fire, fire.Player2, fire.Player2.HandZone.Last(), fire.Player1.Hero);
        Check(fire.Player1.Hero.Damage == 3 && yeti.Damage == 2 && mine.Damage == 2, "Heart of Fire + Felbloom: 3 to the target, 2 to every minion");

        Game demons = BossTurn("LOOTA_BOSS_50h");
        SabberStoneCore.Actions.Generic.DrawCard(demons.Player2,
            SabberStoneCore.Actions.GeneratedDefinitionRules.Potion(Cards.FromId("CFM_621t9"), Cards.FromId("CFM_621t10"), "LOOTA_BOSS_50t"));
        int before = demons.Player2.HandZone.Count;
        PlayEntity(demons, demons.Player2, demons.Player2.HandZone.Last());
        Check(demons.Player2.BoardZone.Count == 1 && demons.Player2.HandZone.Count == before
            && demons.Player2.HandZone.Last().Card.IsRace(Race.DEMON), "Shadow Oil + Netherbloom: a Demon to hand, a 2/2 Demon in play (a Priest boss)");
    }

    // Gemstudded Golem (Kraxx's deck): "Can only attack if you have 5 or more Armor" is a switching aura on CANT_ATTACK. The
    // history must end on the value the engine holds, or the client shows the Golem free to attack (a playtest desync).
    private static void GolemHistory()
    {
        Game game = BossTurn("LOOTA_BOSS_39h");
        int before = game.PowerHistory.Full.Count;
        var golem = Play(game, game.Player2, "LOOT_365");
        int Logged() => game.PowerHistory.Full.Skip(before).OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>()
            .Where(t => t.EntityId == golem.Id && t.Tag == GameTag.CANT_ATTACK).Select(t => t.Value).DefaultIfEmpty(-1).Last();
        Check(golem[GameTag.CANT_ATTACK] == 1 && Logged() == 1, "Gemstudded Golem without Armor: CANT_ATTACK 1, and the history says so");
        before = game.PowerHistory.Full.Count;
        game.Player2.Hero.Armor = 5;
        game.DeathProcessingAndAuraUpdate();
        Check(golem[GameTag.CANT_ATTACK] == 0 && Logged() == 0, "Gemstudded Golem with 5 Armor: CANT_ATTACK 0, and the history says so");
    }

    [GroupProof("dungeon-run-rounds4-5")]
    internal static void Run()
    {
        AFKay();
        Whompwhisker();
        Mushhuckster();
        GolemHistory();
        foreach (var (hero, health) in new[] { ("LOOTA_BOSS_17h", 40), ("LOOTA_BOSS_16h", 40), ("LOOTA_BOSS_06h", 40), ("LOOTA_BOSS_29h", 60),
                     ("LOOTA_BOSS_42h", 30), ("LOOTA_BOSS_20h", 30), ("LOOTA_BOSS_46h", 30), ("LOOTA_BOSS_19h", 30), ("LOOTA_BOSS_33h", 50), ("LOOTA_BOSS_39h", 30),
                     ("LOOTA_BOSS_37h", 40), ("LOOTA_BOSS_51h", 30), ("LOOTA_BOSS_13h", 40), ("LOOTA_BOSS_04h", 30), ("LOOTA_BOSS_27h", 50) })
            Check(DungeonRunRound1Proof.Fight(hero).Player2.Hero.Health == health, hero + ": " + health + " Health");

        // Battlecrier Jin'zo, Battle Totem: every Battlecry triggers twice, for both players, from the first turn and after a
        // snapshot; the power is never used.
        foreach (Game jinzo in new[] { DungeonRunRound1Proof.Fight("LOOTA_BOSS_17h"), Reload(DungeonRunRound1Proof.Fight("LOOTA_BOSS_17h")) })
        {
            int hand = jinzo.Player1.HandZone.Count;
            Play(jinzo, jinzo.Player1, "EX1_015");
            Check(jinzo.Player1.HandZone.Count == hand + 2, "Battle Totem: the player's Novice Engineer draws twice");
            End(jinzo);
            jinzo.Player2.BaseMana = 10; jinzo.Player2.UsedMana = 0;
            Check(Power(jinzo.Player2) == null, "Battle Totem is never used");
            hand = jinzo.Player2.HandZone.Count;
            Play(jinzo, jinzo.Player2, "EX1_015");
            Check(jinzo.Player2.HandZone.Count == hand + 2, "Battle Totem: the boss's Novice Engineer draws twice");
        }

        // Spiritspeaker Azun, Totem of the Dead: every Deathrattle triggers twice, both players.
        foreach (Game azun in new[] { DungeonRunRound1Proof.Fight("LOOTA_BOSS_16h"), Reload(DungeonRunRound1Proof.Fight("LOOTA_BOSS_16h")) })
        {
            var hoarder = Summon(azun.Player1, "EX1_096");
            int hand = azun.Player1.HandZone.Count;
            Kill(azun, hoarder);
            Check(azun.Player1.HandZone.Count == hand + 2, "Totem of the Dead: the player's Loot Hoarder draws twice");
            var bossHoarder = Summon(azun.Player2, "EX1_096");
            hand = azun.Player2.HandZone.Count;
            Kill(azun, bossHoarder);
            Check(azun.Player2.HandZone.Count == hand + 2, "Totem of the Dead: the boss's Loot Hoarder draws twice");
        }

        // Blackseed, Evolvomancy: a minion becomes a random one costing (1) more, on its own side; Greater Evolution: (3) more.
        foreach (var (hero, change) in new[] { ("LOOTA_BOSS_06h", 1), ("LOOTA_BOSS_29h", 3) })
        for (int seed = 0; seed < 3; seed++)
        {
            Game black = BossTurn(hero, seed);
            var yeti = Summon(black.Player1, "CS2_182");
            Use(black, yeti);
            var changed = black.Player1.BoardZone.Single();
            Check(changed.Card.Id != "CS2_182" && changed.Card.Cost == 4 + change && changed.Controller == black.Player1,
                hero + ": a " + (4 + change) + "-cost minion for the same player");
        }

        // Elder Jari, Mystic Barrier: 3 Armor.
        Game jari = BossTurn("LOOTA_BOSS_42h");
        Use(jari);
        Check(jari.Player2.Hero.Armor == 3, "Mystic Barrier: 3 Armor");

        // Fungalmancer Flurgl, Fungal Infection: +1/+1 to the boss's minions only.
        Game flurgl = BossTurn("LOOTA_BOSS_46h");
        var own = Summon(flurgl.Player2, "CS2_182"); var enemy = Summon(flurgl.Player1, "CS2_182");
        Use(flurgl);
        Check(own.AttackDamage == 5 && own.Health == 6 && enemy.AttackDamage == 4 && enemy.Health == 5, "Fungal Infection: +1/+1, own side");

        // Gutmook, Digest Magic: the opponent's spell summons a Tunnel Trogg for the boss; Metabolized Magic: the boss draws a
        // card that costs (1). Gluttonous Trogg grows on the opponent's spells, not its owner's.
        foreach (Game gut in new[] { DungeonRunRound1Proof.Fight("LOOTA_BOSS_19h"), Reload(DungeonRunRound1Proof.Fight("LOOTA_BOSS_19h")) })
        {
            var glutton = (Minion)Summon(gut.Player2, "LOOTA_109");
            Play(gut, gut.Player1, "CS2_029", gut.Player2.Hero);
            Check(gut.Player2.BoardZone.Count(m => m.Card.Id == "LOE_018") == 1, "Digest Magic: a Tunnel Trogg");
            Check(glutton.AttackDamage == 4 && glutton.Health == 4, "Gluttonous Trogg: +1/+1 on the opponent's spell");
            End(gut);
            Play(gut, gut.Player2, "CS2_029", gut.Player1.Hero);
            Check(gut.Player2.BoardZone.Count(m => m.Card.Id == "LOE_018") == 1 && glutton.AttackDamage == 4, "the boss's own spell does nothing");
            Check(Power(gut.Player2) == null, "Digest Magic is never used");
        }
        Game meta = DungeonRunRound1Proof.Fight("LOOTA_BOSS_33h");
        int bossHand = meta.Player2.HandZone.Count;
        Play(meta, meta.Player1, "CS2_029", meta.Player2.Hero);
        var drawn = meta.Player2.HandZone.Last();
        Check(meta.Player2.HandZone.Count == bossHand + 1 && drawn.Cost == 1, "Metabolized Magic: a drawn card that costs (1)");

        // Kraxx, Giant Stomp: 1 damage to each enemy minion, never the boss's.
        Game kraxx = BossTurn("LOOTA_BOSS_39h");
        var a = Summon(kraxx.Player1, "CS2_182"); var b = Summon(kraxx.Player1, "CS2_120"); var mine = Summon(kraxx.Player2, "CS2_182");
        Use(kraxx);
        Check(a.Health == 4 && b.Health == 2 && mine.Health == 5 && kraxx.Player1.Hero.Damage == 0, "Giant Stomp: enemy minions only");

        // Lyris the Wild Mage, Arcane Infusion: Arcane Missiles into her hand.
        Game lyris = BossTurn("LOOTA_BOSS_37h");
        Use(lyris);
        Check(lyris.Player2.HandZone.Last().Card.Id == "EX1_277", "Arcane Infusion: Arcane Missiles");

        // Russell the Bard, Alluring Tune: control of an enemy minion with 2 or less Attack.
        Game russell = BossTurn("LOOTA_BOSS_51h");
        var small = Summon(russell.Player1, "CS2_120"); var big = Summon(russell.Player1, "CS2_182");
        Check(Power(russell.Player2, big) == null, "Alluring Tune: not a 4-Attack minion");
        Use(russell, small);
        Check(small.Controller == russell.Player2 && russell.Player2.BoardZone.Contains(small), "Alluring Tune: the boss controls it");

        // Thaddock the Thief, Tactical Retreat: a friendly minion back to hand; never an enemy's.
        Game thad = BossTurn("LOOTA_BOSS_13h");
        var hers = Summon(thad.Player2, "CS2_182"); var theirs = Summon(thad.Player1, "CS2_182");
        Check(Power(thad.Player2, theirs) == null, "Tactical Retreat: no enemy target");
        Use(thad, hers);
        Check(thad.Player2.BoardZone.Count == 0 && thad.Player2.HandZone.Last().Card.Id == "CS2_182", "Tactical Retreat: back to hand");

        // Waxmancer Sturmi, Sculpt Wax: a 1/1 copy of any minion on the boss's side; Harden Sculpture: a full copy.
        foreach (var (hero, attack, health) in new[] { ("LOOTA_BOSS_04h", 1, 1), ("LOOTA_BOSS_27h", 4, 5) })
        {
            Game wax = BossTurn(hero);
            var target = Summon(wax.Player1, "CS2_182");
            Use(wax, target);
            var copy = wax.Player2.BoardZone.Single();
            Check(copy.Card.Id == "CS2_182" && copy.AttackDamage == attack && copy.Health == health, hero + ": a " + attack + "/" + health + " copy");
            Game loaded = Reload(wax);
            Check(HsSim.Canonical.State(loaded) == HsSim.Canonical.State(wax), hero + ": the copy survives a snapshot");
        }

        // Legacy deck cards. Wrath: 3 damage, or 1 damage and a draw.
        Game wrath = DungeonRunRound1Proof.Fight("LOOTA_BOSS_42h");
        var victim = Summon(wrath.Player2, "CS2_182");
        foreach (var (choice, damage, draws) in new[] { (1, 3, 0), (2, 1, 1) })
        {
            var spell = Add(wrath.Player1, "EX1_154");
            wrath.Player1.BaseMana = 10; wrath.Player1.UsedMana = 0;
            int before = victim.Damage, hand = wrath.Player1.HandZone.Count;
            var task = wrath.Player1.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id
                && t.ChooseOne == choice && t.Target?.Id == victim.Id);
            Check(wrath.Process(task) && victim.Damage == before + damage && wrath.Player1.HandZone.Count == hand - 1 + draws,
                "Wrath choice " + choice);
        }
        // Huge Toad: its Deathrattle hits one enemy for 1.
        Game toad = DungeonRunRound1Proof.Fight("LOOTA_BOSS_42h");
        var huge = Summon(toad.Player1, "LOE_046"); var foe = Summon(toad.Player2, "CS2_182");
        Kill(toad, huge);
        Check(toad.Player2.Hero.Damage + foe.Damage == 1, "Huge Toad: 1 damage to an enemy");
        // Jeweled Scarab: Discover a 3-Cost card; Tomb Spider: Discover a Beast.
        Game scarab = DungeonRunRound1Proof.Fight("LOOTA_BOSS_42h");
        Play(scarab, scarab.Player1, "LOE_029");
        Check(scarab.Player1.Choice != null && scarab.Player1.Choice.Choices.All(id => scarab.IdEntityDic[id].Card.Cost == 3),
            "Jeweled Scarab: three 3-Cost cards");
        Game spider = DungeonRunRound1Proof.Fight("LOOTA_BOSS_42h");
        Play(spider, spider.Player1, "LOE_047");
        Check(spider.Player1.Choice != null && spider.Player1.Choice.Choices.All(id => spider.IdEntityDic[id].Card.IsRace(Race.BEAST)),
            "Tomb Spider: three Beasts");
        // Brimstone Warden: four Stone Golems (15/5, can't attack) in play from the start, kept by a snapshot; a dying Golem
        // costs the Warden a Mana Crystal (an empty one first); Dispel Ward silences his minions, which then can attack.
        var golems = new[] { "LOOTA_BOSS_20t", "LOOTA_BOSS_20t", "LOOTA_BOSS_20t", "LOOTA_BOSS_20t" };
        foreach (Game warden in new[] { DungeonRunRound1Proof.Fight("LOOTA_BOSS_20h", board: golems),
                     Reload(DungeonRunRound1Proof.Fight("LOOTA_BOSS_20h", board: golems)) })
        {
            Check(warden.Player2.Hero.Health == 30 && warden.Player2.BoardZone.Count == 4 && warden.Player2.BoardZone.All(m =>
                m.Card.Id == "LOOTA_BOSS_20t" && m.AttackDamage == 15 && m.Health == 5 && m.CantAttack), "Brimstone Warden: four Stone Golems");
            End(warden);
            warden.Player2.BaseMana = 5; warden.Player2.UsedMana = 2;
            Kill(warden, warden.Player2.BoardZone[0]);
            Check(warden.Player2.BaseMana == 4 && warden.Player2.RemainingMana == 3, "Stone Golem: an empty Mana Crystal lost");
            warden.Player2.BaseMana = 5; warden.Player2.UsedMana = 0;
            Use(warden);
            Check(warden.Player2.BoardZone.Count == 3 && warden.Player2.BoardZone.All(m => !m.CantAttack && m.IsSilenced),
                "Dispel Ward: the Golems wake");
            Kill(warden, warden.Player2.BoardZone[0]);
            Check(warden.Player2.BaseMana == 5, "a silenced Golem has no Deathrattle");
        }
        // The player's Silence wakes a Golem too (Silence removes a card's own "Can't attack": Ancient Watcher, Ragnaros).
        Game silenced = DungeonRunRound1Proof.Fight("LOOTA_BOSS_20h", board: golems);
        var golem = silenced.Player2.BoardZone[0];
        Play(silenced, silenced.Player1, "EX1_332", golem);
        Check(!golem.CantAttack && golem.IsSilenced, "Silence wakes a Stone Golem");
        var watcher = Summon(silenced.Player1, "EX1_045");
        Play(silenced, silenced.Player1, "EX1_332", watcher);
        Check(!watcher.CantAttack, "Silence lets Ancient Watcher attack");
        Game looming = DungeonRunRound1Proof.Fight("LOOTA_BOSS_20h");
        int cards = looming.Player1.HandZone.Count;
        Play(looming, looming.Player1, "LOEA_01");
        Check(looming.Player1.HandZone.Count == cards + 2 && looming.Player1.Hero.Armor == 4, "Looming Presence: 2 cards, 4 Armor");
        // Murloc Tinyfin: a 0-cost 1/1.
        Game fin = DungeonRunRound1Proof.Fight("LOOTA_BOSS_42h");
        var tiny = (Minion)Play(fin, fin.Player1, "LOEA10_3");
        Check(tiny.AttackDamage == 1 && tiny.Health == 1, "Murloc Tinyfin: 1/1");
    }
}
