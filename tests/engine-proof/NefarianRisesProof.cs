using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client.Adventures.NefarianRises;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Nefarian Rises! (co-op Tavern Brawl): the shared team result (CoopRules), Nefarian's side switch, phases and schedule, his spells
// and the brawl's player cards (CardSets/Client/Adventures/NefarianRises*).
internal static class NefarianRisesProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "nefarian rises: " + message);

    private const GameTag HealthPhase = (GameTag)34712, AttackPhase = (GameTag)34713;

    // A co-op game: Player 1 (Priest) starts, Nefarian stands on Player 2's (Shaman's) side.
    internal static Game Brawl(int seed = 7, bool coop = true, bool boss = true, int health = 0)
    {
        var config = new GameConfig {
            StartPlayer = 1, Player1HeroClass = CardClass.PRIEST, Player2HeroClass = CardClass.SHAMAN,
            Player1Deck = Enumerable.Repeat("CS2_182", 20).Select(Cards.FromId).ToList(),
            Player2Deck = Enumerable.Repeat("CS2_120", 20).Select(Cards.FromId).ToList(),
            Shuffle = false, SkipMulligan = true, History = true, RandomSeed = seed, SkipCoin = true, Coop = coop,
            Player2StartingBoard = boss ? new[] { NefarianBoss.BossId } : null,
            Player1StartingHealth = health, Player2StartingHealth = health,
        };
        var game = new Game(config);
        game.StartGame();
        return game;
    }

    private static Minion Boss(Game game) => NefarianBoss.Find(game);

    private static Game Reload(Game game) => Game.LoadSnapshot(game.SaveSnapshot());

    // The spell played from hand by the player (his spells cost nothing and need no target).
    private static void Cast(Game game, Controller player, string id) => Play(game, player, id);

    private static List<string> Graveyards(Game game) =>
        game.Player1.GraveyardZone.Concat(game.Player2.GraveyardZone).Where(p => p.Card.Type == CardType.SPELL).OrderBy(p => p.Id)
            .Select(p => p.Card.Id).ToList();

    [GroupProof("nefarian-rises")]
    internal static void Run()
    {
        TeamResult();
        Board();
        Phases();
        Schedule();
        Spells();
        PlayerCards();
        Content();
        Console.WriteLine("PASS nefarian rises: team result, side switch, phases, schedule, spells, player cards, shipped decks");
    }

    private static void TeamResult()
    {
        // A hero falls: both players lose.
        Game fall = Brawl(boss: false);
        fall.Player1.Hero.Damage = 26;
        Cast(fall, fall.Player1, NefarianBoss.Immolate4);
        Check(fall.State == State.COMPLETE && fall.Player1.PlayState == PlayState.LOST && fall.Player2.PlayState == PlayState.LOST,
            "a defeated hero loses for both");

        // Either player concedes: both lose.
        Game concede = Brawl();
        Check(concede.Process(ConcedeTask.Any(concede.Player2)), "concede accepted");
        Check(concede.Player1.PlayState == PlayState.LOST && concede.Player2.PlayState == PlayState.LOST, "a concession loses for both");

        // Nefarian dies: both win (his last phase, 1 Health left).
        Game win = Brawl();
        var boss = Boss(win);
        boss[HealthPhase] = 2; boss[AttackPhase] = 2;
        boss.Damage = 199;
        Attack(win, Summon(win.Player1, "CS2_182"), boss);
        Check(win.State == State.COMPLETE && win.Player1.PlayState == PlayState.WON && win.Player2.PlayState == PlayState.WON,
            "Nefarian defeated wins for both (" + win.State + " " + win.Player1.PlayState + " " + win.Player2.PlayState + " dead=" + boss.IsDead
            + " zone=" + boss.Zone?.Type + " hp=" + boss.Health + " defeated=" + CoopRules.IsBossDefeated(win) + " deathrattle=" + boss.Card.Deathrattle + ")");
        Check(CoopRules.IsBossDefeated(win), "boss marked defeated");

        // Without Coop the same death of a hero is an ordinary win for the other player.
        Game plain = Brawl(coop: false, boss: false);
        plain.Player1.Hero.Damage = 26;
        Cast(plain, plain.Player1, NefarianBoss.Immolate4);
        Check(plain.Player1.PlayState == PlayState.LOST && plain.Player2.PlayState == PlayState.WON, "ordinary games unchanged");
        Check(!CoopRules.IsCoop(plain) && CoopRules.IsCoop(Reload(Brawl())), "co-op mark only when set, kept by a snapshot");
    }

    private static void Board()
    {
        Game game = Brawl(health: 300);
        var boss = Boss(game);
        Check(boss.Controller == game.Player2 && boss.AttackDamage == 0 && boss.Health == 200 && boss.HasTaunt, "0/200 Taunt on Player 2's side");
        Check(boss.CantBeTargetedBySpells, "can't be targeted by spells");
        Check(!boss.Card.Deathrattle && boss[GameTag.DEATHRATTLE] == 0, "no Deathrattle (the client would show its skull)");
        End(game);
        Check(boss.Controller == game.Player1, "after Player 1's turn he stands on Player 1's side");
        End(game);
        Check(boss.Controller == game.Player2, "after Player 2's turn back on Player 2's side");

        // A full side: every minion there is destroyed and he takes his place.
        Game full = Brawl(health: 300);
        for (int i = 0; i < 7; i++) Summon(full.Player1, "CS2_182");
        End(full);
        Check(Boss(full).Controller == full.Player1, "full side: he moved");
        // untouchable-ok: proof counts the whole board.
        Check(full.Player1.BoardZone.All(m => NefarianBoss.IsBoss(m) || m.Card.Id == NefarianBoss.RuneMinion), "full side: its minions destroyed");

        // Reloaded from a snapshot he keeps switching.
        Game reloaded = Reload(Brawl(health: 300));
        End(reloaded);
        Check(Boss(reloaded).Controller == reloaded.Player1, "snapshot: switch still runs");
    }

    private static void Phases()
    {
        // 160: the hit stops at 160, he is Immune for the rest of the turn, gains 5 Attack and casts Don't Push Me! and
        // Elemental Eruption.
        Game game = Brawl(health: 300);
        var boss = Boss(game);
        boss.Damage = 38;
        var striker = Summon(game.Player1, "CS2_182");
        Attack(game, striker, boss);
        Check(boss.Health == 160, "160: the hit stops at the threshold (" + boss.Health + ")");
        Check(boss.IsImmune && boss.AttackDamage == 5 && boss[HealthPhase] == 1 && boss[AttackPhase] == 1, "160: Immune, 5 Attack");
        var graves = Graveyards(game);
        Check(graves.Contains(NefarianBoss.DontPush) && graves.Contains(NefarianBoss.Eruption), "160: Don't Push Me! and Elemental Eruption");
        Cast(game, game.Player1, NefarianBoss.Missiles10);
        Check(boss.Health == 160, "160: Immune for the rest of the turn");
        End(game);
        Check(!boss.IsImmune, "160: Immune ends with the turn");

        // 60: Glorious Finale and Twisted Light (heals him 30, 30 damage to every other minion), 10 Attack.
        End(game);
        boss.Damage = boss.BaseHealth - 62;
        var third = Summon(game.Player1, "CS2_182");
        Attack(game, third, boss);
        Check(boss[HealthPhase] == 2 && boss.AttackDamage == 10, "60: second phase, 10 Attack (" + boss.AttackDamage + ")");
        Check(boss.Health == 90, "60: Twisted Light heals him to 90 (" + boss.Health + ")");
        Check(Graveyards(game).Contains(NefarianBoss.Glorious) && Graveyards(game).Contains(NefarianBoss.TwistedLight), "60: Glorious Finale, Twisted Light");
        // untouchable-ok: proof reads the whole board.
        Check(game.Player1.BoardZone.Concat(game.Player2.BoardZone).All(m => NefarianBoss.IsBoss(m)), "60: Twisted Light clears the board");

        // One hit can't skip a phase: from 200 straight past 160 stops at 160.
        Game big = Brawl(health: 300);
        var giant = Summon(big.Player1, "CS2_182");
        giant.AttackDamage = 100;
        Attack(big, giant, Boss(big));
        Check(Boss(big).Health == 160, "a 100-damage hit stops at 160");

        // Too slow: after AttackAdvanceTurns of his casts in one attack phase he gains 5 Attack on his own.
        Game slow = Brawl(health: 1000);
        for (int i = 0; i < NefarianBoss.AttackAdvanceTurns; i++) End(slow);
        Check(Boss(slow).AttackDamage == 5 && Boss(slow)[HealthPhase] == 0, "time advances the attack phase (" + Boss(slow).AttackDamage + ")");
    }

    private static void Schedule()
    {
        // Seeded: the same seed casts the same spells; every cast is from his pool or a phase spell; never the same choice twice
        // in a row.
        List<string> Run(int seed)
        {
            Game game = Brawl(seed, health: 1000);
            for (int i = 0; i < 10; i++) End(game);
            return Graveyards(game);
        }
        var a = Run(3); var b = Run(3);
        Check(a.SequenceEqual(b), "same seed, same schedule");
        var pool = new HashSet<string>(NefarianBoss.Pool(0, 0)) { NefarianBoss.DontPush };
        Check(a.All(id => pool.Contains(id)), "casts come from the first pool: " + string.Join(",", a));
        Check(a.Count >= 3, "he casts most turns (" + a.Count + " of 10)");
        Check(!a.Zip(a.Skip(1), (x, y) => x == y).Any(same => same), "no spell twice in a row");
        Check(NefarianBoss.Pool(2, 2).All(id => id == NefarianBoss.Cleave7 || id == NefarianBoss.Runes || id == NefarianBoss.Missiles10
            || id == NefarianBoss.Immolate7) && !NefarianBoss.Pool(0, 2).Contains(NefarianBoss.Bamboozle), "late pools");
    }

    private static void Spells()
    {
        // Bamboozle: the hands swap.
        Game swap = Brawl(boss: false);
        var first = swap.Player1.HandZone.Select(p => p.Id).ToList();
        var second = swap.Player2.HandZone.Select(p => p.Id).ToList();
        Cast(swap, swap.Player1, NefarianBoss.Bamboozle);
        Check(swap.Player2.HandZone.Select(p => p.Id).OrderBy(i => i).SequenceEqual(first.OrderBy(i => i))
            && swap.Player1.HandZone.Select(p => p.Id).OrderBy(i => i).SequenceEqual(second.OrderBy(i => i)), "Bamboozle swaps hands");
        Check(swap.Player2.HandZone.All(p => p.Controller == swap.Player2), "Bamboozle: new owners");

        // Cleave: 4 to a minion and its owner.
        Game cleave = Brawl(boss: false);
        var yeti = Summon(cleave.Player2, "CS2_182");
        Cast(cleave, cleave.Player1, NefarianBoss.Cleave4);
        Check(yeti.Damage == 4 && cleave.Player2.Hero.Damage == 4, "Cleave: minion and owner");

        // Immolate: each hero.
        Game immolate = Brawl(boss: false);
        Cast(immolate, immolate.Player1, NefarianBoss.Immolate7);
        Check(immolate.Player1.Hero.Damage == 7 && immolate.Player2.Hero.Damage == 7, "Immolate: 7 to each hero");

        // Explosive Rune: lands on the caster's side and explodes at the start of its owner's turn for 9.
        Game rune = Brawl(boss: false);
        Cast(rune, rune.Player1, NefarianBoss.Rune);
        // untouchable-ok: proof reads the board.
        var runeMinion = rune.Player1.BoardZone.Single(m => m.Card.Id == NefarianBoss.RuneMinion);
        Check(runeMinion.HasTaunt && runeMinion.Health == 3, "Explosive Rune: 0/3 Taunt on the caster's side");
        // Flame Missiles never hit it.
        Cast(rune, rune.Player1, NefarianBoss.Missiles10);
        Check(runeMinion.Damage == 0 && rune.Player1.Hero.Damage + rune.Player2.Hero.Damage == 10, "Flame Missiles: 10 on heroes, never a Rune");
        int before = rune.Player1.Hero.Damage;
        End(rune); End(rune);
        Check(rune.Player1.Hero.Damage == before + 9 && runeMinion.IsDead, "Explosive Rune: 9 to its owner at the start of the turn");

        // Explosive Runes: one per side.
        Game runes = Brawl(boss: false);
        Cast(runes, runes.Player1, NefarianBoss.Runes);
        // untouchable-ok: proof reads the boards.
        Check(runes.Player1.BoardZone.Count(m => m.Card.Id == NefarianBoss.RuneMinion) == 1
            && runes.Player2.BoardZone.Count(m => m.Card.Id == NefarianBoss.RuneMinion) == 1, "Explosive Runes: one per side");

        // Elemental Eruption: 4-6 to every other minion, not Nefarian.
        Game eruption = Brawl();
        var a = Summon(eruption.Player1, "CS2_182"); var b = Summon(eruption.Player2, "CS2_182");
        Cast(eruption, eruption.Player1, NefarianBoss.Eruption);
        Check(a.Damage >= 4 && a.Damage <= 6 && b.Damage >= 4 && b.Damage <= 6 && Boss(eruption).Damage == 0, "Elemental Eruption: 4-6, not Nefarian");
    }

    // data/coop/nefarian-rises-v1.json: both seats hold 30 cards, every card plays in this engine, the boss is Nefarian.
    private static void Content([System.Runtime.CompilerServices.CallerFilePath] string here = "")
    {
        string file = null;
        foreach (string start in new[] { System.IO.Path.GetDirectoryName(here), System.IO.Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var dir = string.IsNullOrEmpty(start) ? null : new System.IO.DirectoryInfo(start); dir != null && file == null; dir = dir.Parent)
            {
                string candidate = System.IO.Path.Combine(dir.FullName, "data", "coop", "nefarian-rises-v1.json");
                if (System.IO.File.Exists(candidate)) file = candidate;
            }
        Check(file != null, "data/coop/nefarian-rises-v1.json not found");
        using var json = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(file));
        var root = json.RootElement;
        Check(root.GetProperty("schema").GetString() == "solostone.coop/1", "content: schema");
        Check(root.GetProperty("boss").GetString() == NefarianBoss.BossId, "content: the boss is Nefarian");
        Check(root.GetProperty("client").GetProperty("scenario").GetInt32() == 1865, "content: scenario 1865 (TB_COOPV3)");
        var seats = root.GetProperty("seats").EnumerateArray().ToList();
        Check(seats.Count == 2, "content: two seats");
        foreach (var seat in seats)
        {
            string hero = seat.GetProperty("hero").GetString();
            Check(Cards.FromId(hero)?.Type == CardType.HERO, "content: hero " + hero);
            if (seat.TryGetProperty("skin", out var skin)) Check(Cards.FromId(skin.GetString()) != null, "content: skin " + skin.GetString());
            var deck = seat.GetProperty("deck").EnumerateArray().Select(id => id.GetString()).ToList();
            Check(deck.Count == 30, "content: " + hero + " deck has 30 cards (" + deck.Count + ")");
            var missing = deck.Where(id => { var card = Cards.FromId(id); return card == null || !card.Implemented; }).Distinct().ToList();
            Check(missing.Count == 0, "content: unimplemented cards in " + hero + "'s deck: " + string.Join(", ", missing));
        }
    }

    private static void PlayerCards()
    {
        // Main Tank: +2/+2 to every other minion on both sides, not the Boss.
        Game tank = Brawl();
        var mine = Summon(tank.Player1, "CS2_120"); var theirs = Summon(tank.Player2, "CS2_120");
        var played = (Minion)Play(tank, tank.Player1, "TB_Coopv3_104");
        Check(mine.AttackDamage == 4 && mine.Health == 5 && theirs.AttackDamage == 4 && theirs.Health == 5, "Main Tank: both sides +2/+2");
        Check(Boss(tank).AttackDamage == 0 && Boss(tank).Health == 200 && played.AttackDamage == 4, "Main Tank: not the Boss, not itself");

        // Raid Healer: healing your hero heals the teammate as much; two Raid Healers don't echo forever.
        Game raid = Brawl(boss: false);
        Summon(raid.Player1, "TB_Coopv3_105"); Summon(raid.Player2, "TB_Coopv3_105");
        raid.Player1.Hero.Damage = 10; raid.Player2.Hero.Damage = 10;
        Play(raid, raid.Player1, "CS2_007", raid.Player1.Hero);
        Check(raid.Player1.Hero.Damage == 2 && raid.Player2.Hero.Damage == 2, "Raid Healer: teammate healed 8, once ("
            + raid.Player1.Hero.Damage + "," + raid.Player2.Hero.Damage + ")");

        // Dragonscale Warrior: the player who targets it with a spell draws.
        Game scale = Brawl(boss: false);
        var warrior = Summon(scale.Player2, "TB_Coopv3_100");
        int hand = scale.Player1.HandZone.Count;
        Play(scale, scale.Player1, "CS2_236", warrior);
        Check(scale.Player1.HandZone.Count == hand + 1, "Dragonscale Warrior: the targeting player draws");

        // Freewheeling Skulker: switches sides at the end of its owner's turn and becomes a Team Player.
        Game skulk = Brawl(boss: false);
        var skulker = Summon(skulk.Player1, "TB_Coopv3_101");
        End(skulk);
        Check(skulker.Controller == skulk.Player2 && skulker[GameTag.IMMUNE_WHILE_ATTACKING] == 1, "Freewheeling Skulker: switched, Immune while attacking");

        // Intrepid Dragonstalker: +1/+1 whenever any player plays a card.
        Game stalk = Brawl(boss: false);
        var stalker = Summon(stalk.Player1, "TB_Coopv3_103");
        Play(stalk, stalk.Player1, "CS2_120");
        End(stalk);
        Play(stalk, stalk.Player2, "CS2_120");
        Check(stalker.AttackDamage == 5 && stalker.Health == 5, "Intrepid Dragonstalker: +1/+1 for each player's card ("
            + stalker.AttackDamage + "/" + stalker.Health + ")");
    }
}
