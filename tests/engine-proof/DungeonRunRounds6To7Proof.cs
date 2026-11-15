using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Dungeon Run round 6-7 bosses (data/runs/loot-v1.json) and the cards their decks need.
internal static class DungeonRunRounds6To7Proof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "dungeon run rounds 6-7: " + message);

    private static PlayerTask Power(Controller player) =>
        player.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER);

    private static Game BossTurn(string boss, int seed = 11)
    {
        Game game = DungeonRunRound1Proof.Fight(boss, seed);
        End(game);
        game.Player2.BaseMana = 10; game.Player2.UsedMana = 0;
        return game;
    }

    private static void Use(Game game)
    {
        var task = Power(game.Player2);
        Check(task != null && game.Process(task), "hero power accepted: " + game.Player2.Hero.HeroPower.Card.Id);
    }

    private static Game Reload(Game game) => Game.LoadSnapshot(game.SaveSnapshot());

    [GroupProof("dungeon-run-rounds6-7")]
    internal static void Run()
    {
        foreach (var (hero, health) in new[] { ("LOOTA_BOSS_23h", 50), ("LOOTA_BOSS_53h", 50), ("LOOTA_BOSS_21h", 40), ("LOOTA_BOSS_36h", 20),
                     ("LOOTA_BOSS_43h", 40), ("LOOTA_BOSS_47h", 50), ("LOOTA_BOSS_24h", 50), ("LOOTA_BOSS_48h", 40), ("LOOTA_BOSS_38h", 50) })
            Check(DungeonRunRound1Proof.Fight(hero).Player2.Hero.Health == health, hero + ": " + health + " Health");

        // Bristlesnarl, Hunter's Call: every card in the boss's hand costs (1) less.
        Game bristle = BossTurn("LOOTA_BOSS_23h");
        var costs = bristle.Player2.HandZone.Select(p => p.Cost).ToArray();
        Use(bristle);
        Check(bristle.Player2.HandZone.Select(p => p.Cost).SequenceEqual(costs.Select(c => System.Math.Max(0, c - 1))),
            "Hunter's Call: the hand costs (1) less");

        // Chronomancer Inara, Stolen Time: 3 Mana Crystals gone, and the boss takes the next turn too (one more crystal).
        foreach (Game inara in new[] { BossTurn("LOOTA_BOSS_53h"), Reload(BossTurn("LOOTA_BOSS_53h")) })
        {
            Use(inara);
            Check(inara.Player2.BaseMana == 7 && inara.Player2.RemainingMana == 0, "Stolen Time: 3 crystals destroyed");
            End(inara);
            Check(inara.CurrentPlayer == inara.Player2 && inara.Player2.BaseMana == 8, "Stolen Time: an extra turn");
            End(inara);
            Check(inara.CurrentPlayer == inara.Player1, "after the extra turn the player's turn");
        }

        // Gnosh the Greatworm, Swallow Whole: the highest-Attack minion of either side dies; one of a tie at random;
        // swallowing a Poisonous minion kills Gnosh.
        Game gnosh = BossTurn("LOOTA_BOSS_21h");
        var yeti = Summon(gnosh.Player1, "CS2_182"); var raptor = Summon(gnosh.Player2, "CS2_120");
        Use(gnosh);
        Check(yeti.IsDead && !raptor.IsDead, "Swallow Whole: the 4-Attack Yeti");
        for (int seed = 0; seed < 4; seed++)
        {
            Game tie = BossTurn("LOOTA_BOSS_21h", seed);
            Summon(tie.Player1, "CS2_182"); Summon(tie.Player2, "CS2_182");
            Use(tie);
            Check(tie.Player1.BoardZone.Count + tie.Player2.BoardZone.Count == 1, "Swallow Whole: one of a tie");
        }
        Game cobra = BossTurn("LOOTA_BOSS_21h");
        Summon(cobra.Player1, "EX1_170");
        Use(cobra);
        Check(cobra.Player2.Hero.IsDead && cobra.Player1.PlayState == PlayState.WON, "Swallow Whole: a Poisonous minion kills Gnosh");
        Game empty = BossTurn("LOOTA_BOSS_21h");
        Check(Power(empty.Player2) == null, "Swallow Whole: not with no minion in play");

        // Ixlid, Sprouting Spore: a 1/1 Poisonous Deadly Spore that also destroys a hero it damages.
        foreach (Game ixlid in new[] { BossTurn("LOOTA_BOSS_36h"), Reload(BossTurn("LOOTA_BOSS_36h")) })
        {
            Use(ixlid);
            var spore = ixlid.Player2.BoardZone.Single();
            Check(spore.Card.Id == "LOOTA_105" && spore.Poisonous && spore.AttackDamage == 1, "Sprouting Spore: a Deadly Spore");
            var target = Summon(ixlid.Player1, "CS2_182");
            Attack(ixlid, spore, target);
            Check(target.IsDead, "Deadly Spore: Poisonous");
        }
        Game deadly = BossTurn("LOOTA_BOSS_36h");
        Use(deadly);
        Attack(deadly, deadly.Player2.BoardZone.Single(), deadly.Player1.Hero);
        Check(deadly.Player1.Hero.IsDead && deadly.Player1.PlayState == PlayState.LOST, "Deadly Spore: Poisonous to heroes");

        // Jeeru, Three Wee Wishes: each player draws 3.
        Game jeeru = BossTurn("LOOTA_BOSS_43h");
        int mine = jeeru.Player1.HandZone.Count, boss = jeeru.Player2.HandZone.Count;
        Use(jeeru);
        Check(jeeru.Player1.HandZone.Count == mine + 3 && jeeru.Player2.HandZone.Count == boss + 3, "Three Wee Wishes: 3 each");

        // Lava-Filled Chamber, The Floor is Lava: each minion played (either side) takes 2 damage; a summoned one does not.
        foreach (Game lava in new[] { DungeonRunRound1Proof.Fight("LOOTA_BOSS_47h"), Reload(DungeonRunRound1Proof.Fight("LOOTA_BOSS_47h")) })
        {
            var played = (Minion)Play(lava, lava.Player1, "CS2_182");
            var summoned = Summon(lava.Player1, "CS2_182");
            Check(played.Damage == 2 && summoned.Damage == 0, "The Floor is Lava: the player's played minion");
            End(lava);
            var own = (Minion)Play(lava, lava.Player2, "CS2_120");
            Check(own.Damage == 2, "The Floor is Lava: the boss's own minion");
            lava.Player2.UsedMana = 0;
            Check(Power(lava.Player2) == null, "The Floor is Lava is never used");
        }

        // The Mothergloop, Gloop: minions in the boss's hand get +1/+1.
        Game gloop = BossTurn("LOOTA_BOSS_24h");
        var raptors = gloop.Player2.HandZone.OfType<Minion>().ToList();
        Check(raptors.Count > 0, "the Mothergloop holds minions");
        Use(gloop);
        Check(raptors.All(m => m.AttackDamage == 3 && m.Health == 4), "Gloop: +1/+1 in hand");

        // Trapped Room, Alarm: a revealed Secret of the boss's summons a 3/3 Sawblade.
        foreach (Game trap in new[] { BossTurn("LOOTA_BOSS_48h"), Reload(BossTurn("LOOTA_BOSS_48h")) })
        {
            Play(trap, trap.Player2, "EX1_287");
            End(trap);
            Play(trap, trap.Player1, "CS2_029", trap.Player2.Hero);
            Check(trap.Player2.Hero.Damage == 0 && trap.Player2.BoardZone.Count(m => m.Card.Id == "LOOTA_BOSS_48t"
                && m.AttackDamage == 3 && m.Health == 3) == 1, "Alarm: Counterspell revealed, a Sawblade");
        }

        // Voodoomaster Vex, Totem of Chaos: Battlecries and Deathrattles trigger twice, both players.
        foreach (Game vex in new[] { DungeonRunRound1Proof.Fight("LOOTA_BOSS_38h"), Reload(DungeonRunRound1Proof.Fight("LOOTA_BOSS_38h")) })
        {
            int hand = vex.Player1.HandZone.Count;
            Play(vex, vex.Player1, "EX1_015");
            Check(vex.Player1.HandZone.Count == hand + 2, "Totem of Chaos: Battlecry twice");
            var hoarder = Summon(vex.Player2, "EX1_096");
            hand = vex.Player2.HandZone.Count;
            Kill(vex, hoarder);
            Check(vex.Player2.HandZone.Count == hand + 2, "Totem of Chaos: Deathrattle twice");
        }

        DeckCards();
    }

    // The cards the round 6-7 decks need: legacy (Wild) cards left empty, Karazhan and League of Explorers adventure cards,
    // and the Kobolds boss minions.
    private static void DeckCards()
    {
        // King of Beasts: +1 Attack per other friendly Beast.
        Game king = DungeonRunRound1Proof.Fight("LOOTA_BOSS_23h");
        Summon(king.Player1, "CS2_120"); Summon(king.Player1, "CS2_120"); Summon(king.Player1, "CS2_182");
        var beasts = (Minion)Play(king, king.Player1, "GVG_046");
        Check(beasts.AttackDamage == 4 && beasts.HasTaunt, "King of Beasts: +2 Attack for two Beasts");

        // Gahz'rilla: each time it takes damage its Attack doubles.
        Game gahz = DungeonRunRound1Proof.Fight("LOOTA_BOSS_23h");
        var rilla = Summon(gahz.Player1, "GVG_049");
        Play(gahz, gahz.Player1, "CS2_008", rilla);
        Play(gahz, gahz.Player1, "CS2_008", rilla);
        Check(rilla.AttackDamage == 24 && rilla.Health == 7, "Gahz'rilla: 6 -> 12 -> 24 Attack, got " + rilla.AttackDamage + "/" + rilla.Health);

        // Fel Reaver: each card the opponent plays removes the top 3 of its owner's deck (to the graveyard).
        Game reaver = DungeonRunRound1Proof.Fight("LOOTA_BOSS_43h");
        Summon(reaver.Player2, "GVG_016");
        int deck = reaver.Player2.DeckZone.Count, grave = reaver.Player2.GraveyardZone.Count;
        Play(reaver, reaver.Player1, "CS2_182");
        Check(reaver.Player2.DeckZone.Count == deck - 3 && reaver.Player2.GraveyardZone.Count == grave + 3, "Fel Reaver: 3 removed");
        int mine = reaver.Player1.DeckZone.Count;
        End(reaver);
        deck = reaver.Player2.DeckZone.Count;
        Play(reaver, reaver.Player2, "CS2_120");
        Check(reaver.Player2.DeckZone.Count == deck && reaver.Player1.DeckZone.Count == mine, "Fel Reaver: its owner's plays remove nothing");

        // Anubisath Sentinel: its Deathrattle gives another friendly minion +3/+3.
        Game anubis = DungeonRunRound1Proof.Fight("LOOTA_BOSS_21h");
        var sentinel = Summon(anubis.Player1, "LOE_061"); var yeti = Summon(anubis.Player1, "CS2_182");
        Kill(anubis, sentinel);
        Check(yeti.AttackDamage == 7 && yeti.Health == 8, "Anubisath Sentinel: +3/+3");

        // Animated Armor: its owner's hero takes at most 1 damage at a time.
        Game armor = DungeonRunRound1Proof.Fight("LOOTA_BOSS_53h");
        Summon(armor.Player1, "LOE_119");
        End(armor);
        Play(armor, armor.Player2, "CS2_029", armor.Player1.Hero);
        Check(armor.Player1.Hero.Damage == 1, "Animated Armor: Fireball deals 1");

        // Archmage's Insight: spells cost (0) for the rest of the turn.
        Game insight = DungeonRunRound1Proof.Fight("LOOTA_BOSS_53h");
        var fireball = Add(insight.Player1, "CS2_029");
        Play(insight, insight.Player1, "KARA_00_05");
        Check(fireball.Cost == 0, "Archmage's Insight: Fireball costs (0)");
        End(insight);
        Check(fireball.Cost == 4, "Archmage's Insight: until the end of the turn");

        // Astral Portal: a random Legendary minion.
        Game portal = DungeonRunRound1Proof.Fight("LOOTA_BOSS_53h");
        Play(portal, portal.Player1, "KARA_00_07");
        Check(portal.Player1.BoardZone.Count == 1 && portal.Player1.BoardZone[0].Card.Rarity == Rarity.LEGENDARY, "Astral Portal: a Legendary");

        // Archmage's Apprentice: each spell its owner casts is shuffled into the deck again.
        Game apprentice = DungeonRunRound1Proof.Fight("LOOTA_BOSS_53h");
        Summon(apprentice.Player1, "KARA_00_08");
        deck = apprentice.Player1.DeckZone.Count;
        Play(apprentice, apprentice.Player1, "CS2_029", apprentice.Player2.Hero);
        Check(apprentice.Player1.DeckZone.Count == deck + 1 && apprentice.Player1.DeckZone.Any(p => p.Card.Id == "CS2_029"),
            "Archmage's Apprentice: a Fireball shuffled in");

        // Looming Presence (heroic): 3 cards, 6 Armor.
        Game looming = DungeonRunRound1Proof.Fight("LOOTA_BOSS_53h");
        int hand = looming.Player1.HandZone.Count;
        Play(looming, looming.Player1, "LOEA_01H");
        Check(looming.Player1.HandZone.Count == hand + 3 && looming.Player1.Hero.Armor == 6, "Looming Presence (heroic): 3 cards, 6 Armor");

        // Growing Ooze: +1/+1 at the start of its owner's turns only.
        Game growing = DungeonRunRound1Proof.Fight("LOOTA_BOSS_24h");
        var ooze = Summon(growing.Player2, "ICCA07_004");
        End(growing);
        Check(ooze.AttackDamage == 2 && ooze.Health == 2, "Growing Ooze: +1/+1 at its owner's turn");
        End(growing);
        Check(ooze.AttackDamage == 2, "Growing Ooze: not at the opponent's turn");

        // Reverberating Ooze: surviving damage summons a copy; dying does not.
        Game reverb = DungeonRunRound1Proof.Fight("LOOTA_BOSS_24h");
        var reverberating = Summon(reverb.Player1, "LOOTA_104");
        Play(reverb, reverb.Player1, "CS2_008", reverberating);
        Check(reverb.Player1.BoardZone.Count(m => m.Card.Id == "LOOTA_104") == 2, "Reverberating Ooze: a copy after surviving");
        Play(reverb, reverb.Player1, "CS2_029", reverberating);
        Check(reverb.Player1.BoardZone.Count(m => m.Card.Id == "LOOTA_104") == 1, "Reverberating Ooze: no copy when it dies");

        // Shroomsayer: surviving damage destroys all minions, itself included.
        Game shroom = DungeonRunRound1Proof.Fight("LOOTA_BOSS_48h");
        var sayer = Summon(shroom.Player2, "LOOTA_107"); Summon(shroom.Player1, "CS2_182"); Summon(shroom.Player2, "CS2_120");
        Play(shroom, shroom.Player1, "CS2_008", sayer);
        Check(shroom.Player1.BoardZone.Count == 0 && shroom.Player2.BoardZone.Count == 0, "Shroomsayer: all minions destroyed");
    }
}
