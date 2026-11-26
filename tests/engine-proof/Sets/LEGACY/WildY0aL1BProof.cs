using System.Reflection;
using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static WildY0aL1ALegacyProof;

// Wild wave Y0a, lane l1b: Legacy and Classic neutral minions. The group runs both set files (the group name is unique).
internal static class WildY0aL1BLegacyProof
{
    [GroupProof("wild-y0a-l1b-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        WildY0aL1BExpertProof.Run();
    }

    // Proven against their 37.0 identity row (all pass, 2026-11-15) but not registered: the Standard product uses them today
    // (Basic-card list, Standard data files or test loaners), so their identity rows and scripts wait for #87
    // (BLOCKED-STANDARD); DS1_055's row moves its set CORE to LEGACY, which the Standard check "unsupported set admitted"
    // (tests/cards/Program.cs) rejects; EX1_007 also has changed English text that needs the locale files (BLOCKED-HSDATA).
    // A landing branch that clears the block registers an entry with WildSetsProof.Case and lists its id in the manifest.
    internal static readonly (string Id, Action<Game, Controller, string> Scenario)[] Unregistered =
    {
        ("CS2_179", Taunt),
        ("CS2_182", (g, p, id) => Plain(g, p, id)),
        ("CS2_186", (g, p, id) => Plain(g, p, id)),
        ("CS2_187", Taunt),
        ("CS2_189", (g, p, id) => BattlecryDamage(g, p, id, 1)),
        ("CS2_196", RazorfenHunter),
        ("CS2_197", SpellDamage),
        ("CS2_200", (g, p, id) => Plain(g, p, id)),
        ("CS2_201", (g, p, id) => Plain(g, p, id, Race.ELEMENTAL, Race.BEAST)),
        ("CS2_213", Charge),
        ("CS2_222", StormwindChampion),
        ("CS2_226", FrostwolfWarlord),
        ("DS1_055", DarkscaleHealer),
        ("EX1_007", AcolyteOfPain),
    };

    // Both seats' fair views hold no identity of the other seat's hand or deck, and none of their own deck.
    internal static void FairViewHidesCards(Game g, string what)
    {
        foreach (int seat in new[] { 1, 2 })
        {
            var fair = FairBotState.Create(g, seat, 77, DeckStyle.Tempo);
            var view = (Game)typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fair);
            var mine = seat == 1 ? view.Player1 : view.Player2; var theirs = seat == 1 ? view.Player2 : view.Player1;
            Core2026Proof.Check(theirs.HandZone.All(c => c is Unknown) && theirs.DeckZone.All(c => c is Unknown) && mine.DeckZone.All(c => c is Unknown),
                what + ": the fair view of seat " + seat + " reveals no hidden hand or deck identity");
        }
    }

    // A vanilla body: no text, exactly the printed races, no keyword.
    internal static void Plain(Game g, Controller p, string id, params Race[] races)
    {
        var m = (Minion)Play(g, p, id);
        Only(m, id);
        Core2026Proof.Check(string.IsNullOrEmpty(m.Card.Text), id + " has no text");
        Core2026Proof.Check(m.Card.Races.OrderBy(r => r).SequenceEqual(races.OrderBy(r => r)), id + " printed races");
        Core2026Proof.Check(m.AttackDamage == m.Card.ATK && m.Health == m.Card.Health && AttackTargets(p, m).Count == 0,
            id + " vanilla body, summoning sick");
        Silence(g, m);
        Core2026Proof.Check(m.IsSilenced && m.AttackDamage == m.Card.ATK && m.Health == m.Card.Health, id + " Silence changes nothing on a vanilla body");
        NextOwnTurn(g);
        Core2026Proof.Check(AttackTargets(p, m).SetEquals(new[] { p.Opponent.Hero.Id }), id + " attacks the lone enemy hero next turn");
        Attack(g, m, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == m.Card.ATK, id + " deals exactly its Attack");
        var spare = (Minion)Play(g, p, id);
        if (spare.Health >= 2)
        {
            spare.Damage = spare.Health - 2; Play(g, p, "CS2_008", spare);
            Core2026Proof.Check(spare.Zone.Type == Zone.PLAY && spare.Health == 1, id + " survives one point short of lethal");
        }
        else spare.Damage = 0;
        Play(g, p, "CS2_008", spare);
        Core2026Proof.Check(spare.Zone.Type == Zone.GRAVEYARD, id + " dies at exact Health");
        var enemy = Summon(p.Opponent, id);
        Kill(g, enemy);
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 0 && p.BoardZone.Count == 1, id + " opponent copy leaves no trace");
    }

    private static void RazorfenHunter(Game g, Controller p, string id)
    {
        var enemy = Summon(p.Opponent, "CS2_182");
        var hunter = (Minion)Play(g, p, id);
        Only(hunter, id, GameTag.BATTLECRY);
        Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone[hunter.ZonePosition + 1].Card.Id == "CS2_boar", id + " summons a Boar to its right");
        var boar = p.BoardZone[hunter.ZonePosition + 1];
        Core2026Proof.Check(boar.AttackDamage == 1 && boar.Health == 1 && boar.Card.Races.Contains(Race.BEAST) && !boar.HasCharge
            && AttackTargets(p, boar).Count == 0, "the Boar is a plain 1/1 Beast, summoning sick");
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 1 && enemy.Damage == 0 && p.Hero.Damage == 0, id + " touches nothing else");
        var held = Add(p, id);
        Core2026Proof.Check(g.Process(new PlayCardTask(p, held, null, 0)), id + " played at the left edge");
        Core2026Proof.Check(p.BoardZone[0] == held && p.BoardZone[1].Card.Id == "CS2_boar" && p.BoardZone.Count == 4,
            id + " Boar lands immediately right of the Hunter");
        int count = p.BoardZone.Count;
        Silence(g, (Minion)held); Kill(g, (Minion)held);
        Core2026Proof.Check(p.BoardZone.Count == count - 1, id + " Silence and death summon nothing more");
        StandardSetsProof.ParkBoard(p);
        for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "CS2_boar") == 1, id + " with room for exactly one more summons the Boar");
        StandardSetsProof.ParkBoard(p);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.All(m => m.Card.Id != "CS2_boar"), id + " on a board of six finds no room for a Boar");
        End(g);
        int mine = p.BoardZone.Count;
        var theirs = (Minion)Play(g, p.Opponent, id);
        Core2026Proof.Check(p.BoardZone.Count == mine && p.Opponent.BoardZone.Count(m => m.Card.Id == "CS2_boar") == 1
            && p.Opponent.BoardZone[theirs.ZonePosition + 1].Card.Id == "CS2_boar", id + " opponent copy summons the Boar on its own side");
        End(g);
    }

    private static void StormwindChampion(Game g, Controller p, string id)
    {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var held = (Minion)Add(p, "CS2_182");
        var champ = (Minion)Play(g, p, id);
        Only(champ, id, GameTag.AURA);
        Core2026Proof.Check(champ.AttackDamage == 7 && champ.Health == 7, id + " does not buff itself");
        Core2026Proof.Check(friend.AttackDamage == 5 && friend.Health == 6 && enemy.AttackDamage == 4 && enemy.Health == 5
            && held.AttackDamage == 4 && held.Health == 5, id + " buffs my other board minions only");
        var late = Summon(p, "CS2_182");
        Core2026Proof.Check(late.AttackDamage == 5 && late.Health == 6, id + " buffs minions that arrive later");
        Silence(g, friend);
        Core2026Proof.Check(friend.AttackDamage == 5 && friend.Health == 6, id + " aura survives Silence on the recipient");
        var second = (Minion)Play(g, p, id);
        Core2026Proof.Check(champ.AttackDamage == 8 && second.AttackDamage == 8 && champ.Health == 8 && friend.AttackDamage == 6 && friend.Health == 7,
            id + " two champions buff each other and stack on others");
        Kill(g, second);
        Core2026Proof.Check(champ.AttackDamage == 7 && friend.AttackDamage == 5 && late.Health == 6, id + " the aura leaves with its source");
        Resume(g, next => {
            var c = (Minion)next.IdEntityDic[champ.Id];
            Core2026Proof.Check(((Minion)next.IdEntityDic[late.Id]).AttackDamage == 5, id + " aura survives clone and snapshot");
            Silence(next, c);
            Core2026Proof.Check(((Minion)next.IdEntityDic[late.Id]).AttackDamage == 4, id + " Silence removes the aura after a restore");
        });
        var frail = Summon(p, "CS2_182");
        for (int i = 0; i < 5; i++) Play(g, p, "CS2_008", frail);
        Core2026Proof.Check(frail.Health == 1 && frail.AttackDamage == 5, "a buffed minion carries damage 5 at Health 1");
        Silence(g, champ);
        Core2026Proof.Check(late.AttackDamage == 4 && late.Health == 5 && enemy.AttackDamage == 4, id + " Silence removes the aura");
        var again = (Minion)Play(g, p, id);
        Core2026Proof.Check(late.AttackDamage == 5 && late.Health == 6, id + " a fresh champion buffs again");
        Play(g, p, "CS1_113", enemy);
        Core2026Proof.Check(enemy.Controller == p && enemy.AttackDamage == 5 && enemy.Health == 6, id + " stolen minions are buffed");
        End(g);
        var theirs = (Minion)Play(g, p.Opponent, id);
        Core2026Proof.Check(theirs.AttackDamage == 7 && again.AttackDamage == 7 && late.AttackDamage == 5 && enemy.AttackDamage == 5,
            id + " opponent copy buffs only the opponent's board");
        Play(g, p.Opponent, "CS1_113", again);
        Core2026Proof.Check(again.Controller == p.Opponent && late.AttackDamage == 4 && again.AttackDamage == 8 && theirs.AttackDamage == 8,
            id + " control change moves the aura with the champion");
        End(g);
    }

    private static void FrostwolfWarlord(Game g, Controller p, string id)
    {
        var alone = (Minion)Play(g, p, id);
        Only(alone, id, GameTag.BATTLECRY);
        Core2026Proof.Check(alone.AttackDamage == 4 && alone.Health == 4 && alone.Card.Cost == 5, id + " alone gains nothing");
        Kill(g, alone);
        var a = Summon(p, "CS2_182"); var b = Summon(p, "CS2_182");
        var e1 = Summon(p.Opponent, "CS2_182"); var e2 = Summon(p.Opponent, "CS2_182"); var e3 = Summon(p.Opponent, "CS2_182");
        var inHand = (Minion)Add(p, "CS2_182");
        var warlord = (Minion)Play(g, p, id);
        Core2026Proof.Check(warlord.AttackDamage == 6 && warlord.Health == 6 && warlord.Damage == 0,
            id + " gains +1/+1 for each OTHER friendly minion, not enemies, not itself, not hand");
        Core2026Proof.Check(a.AttackDamage == 4 && b.Health == 5 && e1.AttackDamage == 4 && inHand.AttackDamage == 4, id + " buffs only itself");
        var late = Summon(p, "CS2_182");
        Core2026Proof.Check(warlord.AttackDamage == 6, id + " counts once, at play time");
        Kill(g, a); Kill(g, late);
        Core2026Proof.Check(warlord.AttackDamage == 6 && warlord.Health == 6, id + " keeps the gain when the counted minions die");
        Resume(g, next => Core2026Proof.Check(((Minion)next.IdEntityDic[warlord.Id]).AttackDamage == 6, id + " gain survives clone and snapshot"));
        Silence(g, warlord);
        Core2026Proof.Check(warlord.AttackDamage == 4 && warlord.Health == 4, id + " Silence removes the gain");
        StandardSetsProof.ParkBoard(p);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        var big = (Minion)Play(g, p, id);
        Core2026Proof.Check(big.AttackDamage == 10 && big.Health == 10 && p.BoardZone.Count == 7, id + " counts six others for +6/+6");
        Kill(g, big);
        End(g);
        var theirs = (Minion)Play(g, p.Opponent, id);
        Core2026Proof.Check(theirs.AttackDamage == 7 && theirs.Health == 7 && p.BoardZone.Count == 6,
            id + " opponent copy counts the opponent's three others");
        End(g);
    }

    private static void DarkscaleHealer(Game g, Controller p, string id)
    {
        var friend = Summon(p, "CS2_182"); var other = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var spare = Summon(p, "CS2_182");
        var mine = Summon(p, "EX1_001"); var theirLw = Summon(p.Opponent, "EX1_001");
        p.Hero.Damage = 5; friend.Damage = 3; other.Damage = 1; enemy.Damage = 2; p.Opponent.Hero.Damage = 5;
        int hand = p.HandZone.Count, deck = p.DeckZone.Count, theirHand = p.Opponent.HandZone.Count;
        var healer = (Minion)Play(g, p, id);
        Only(healer, id, GameTag.BATTLECRY);
        Core2026Proof.Check(healer.Card.Races.Contains(Race.NAGA) && healer.Damage == 0, id + " is a Naga");
        Core2026Proof.Check(p.Hero.Damage == 3 && friend.Damage == 1 && other.Damage == 0 && spare.Damage == 0,
            id + " restores exactly 2 to every friendly character, capped at maximum Health");
        Core2026Proof.Check(enemy.Damage == 2 && p.Opponent.Hero.Damage == 5, id + " leaves enemies alone");
        Core2026Proof.Check(mine.AttackDamage == 7 && theirLw.AttackDamage == 7,
            id + " every healed character triggers Lightwarden once (hero, two minions) and undamaged ones do not, on both sides");
        Core2026Proof.Check(p.HandZone.Count == hand && p.DeckZone.Count == deck && p.Opponent.HandZone.Count == theirHand,
            id + " moves no cards");
        friend.Damage = 3; p.Hero.Damage = 1;
        Silence(g, healer); Kill(g, healer);
        Core2026Proof.Check(friend.Damage == 3 && p.Hero.Damage == 1, id + " Silence and death do not repeat the Battlecry");
        var second = (Minion)Play(g, p, id);
        Core2026Proof.Check(friend.Damage == 1 && p.Hero.Damage == 0 && mine.AttackDamage == 11, id + " a second Healer restores again and the hero caps at 0");
        Kill(g, second);
        var third = (Minion)Play(g, p, id);
        Core2026Proof.Check(friend.Damage == 0 && mine.AttackDamage == 13, id + " a third Healer heals the last point only");
        Kill(g, third);
        Kill(g, (Minion)Play(g, p, id));
        Core2026Proof.Check(mine.AttackDamage == 13, id + " with nothing damaged heals nothing and triggers nothing");
        friend.Damage = 4;
        var velen = Summon(p, "EX1_350");
        Kill(g, (Minion)Play(g, p, id));
        Core2026Proof.Check(friend.Damage == 2, id + " Battlecry healing is not doubled by Prophet Velen (spells and Hero Powers only) " + friend.Damage + " " + p.BoardZone.Count);
        Kill(g, velen);
        End(g);
        enemy.Damage = 3; p.Opponent.Hero.Damage = 4; p.Hero.Damage = 4;
        Resume(g, next => {
            Play(next, next.Player2, id);
            Core2026Proof.Check(next.Player2.Hero.Damage == 2 && ((Minion)next.IdEntityDic[enemy.Id]).Damage == 1
                && next.Player1.Hero.Damage == 4, id + " an opponent copy restores the opponent's side after a restore");
        });
        Play(g, p.Opponent, id);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 2 && enemy.Damage == 1 && p.Hero.Damage == 4, id + " opponent copy heals only its own side");
        End(g);
    }

    private static void AcolyteOfPain(Game g, Controller p, string id)
    {
        var acolyte = (Minion)Play(g, p, id);
        Only(acolyte, id);
        int hand = p.HandZone.Count, deck = p.DeckZone.Count, theirHand = p.Opponent.HandZone.Count, theirDeck = p.Opponent.DeckZone.Count;
        Play(g, p, "CS2_008", acolyte);
        Core2026Proof.Check(acolyte.Damage == 1 && p.HandZone.Count == hand + 1 && p.DeckZone.Count == deck - 1
            && p.Opponent.HandZone.Count == theirHand && p.Opponent.DeckZone.Count == theirDeck,
            id + " draws exactly one card for its owner when damaged");
        hand = p.HandZone.Count;
        var other = Summon(p, "CS2_182"); Play(g, p, "CS2_008", other); Play(g, p, "CS2_008", p.Hero); Play(g, p, "CS2_008", p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == hand, id + " ignores damage to other characters");
        hand = p.HandZone.Count;
        var bomb = Add(p, "CS2_029");
        PlayEntity(g, p, bomb, acolyte);
        Core2026Proof.Check(acolyte.Zone.Type == Zone.GRAVEYARD && p.HandZone.Count == hand + 1,
            id + " Fireball kills it at exact Health and it still draws exactly once");
        var wide = (Minion)Play(g, p, id);
        hand = p.HandZone.Count;
        Play(g, p, "CS2_150", wide);
        Core2026Proof.Check(wide.Damage == 2 && p.HandZone.Count == hand + 1, id + " a two-point hit draws one card, not two");
        var silenced = (Minion)Play(g, p, id);
        Silence(g, silenced);
        hand = p.HandZone.Count;
        Play(g, p, "CS2_008", silenced);
        Core2026Proof.Check(silenced.Damage == 1 && p.HandZone.Count == hand, id + " Silence removes the trigger");
        var drained = (Minion)Play(g, p, id);
        foreach (var c in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(c));
        int dmg = p.Hero.Damage;
        Play(g, p, "CS2_008", drained);
        Core2026Proof.Check(p.Hero.Damage == dmg + 1, id + " with an empty deck takes the first fatigue damage");
        var full = (Minion)Play(g, p, id);
        Resume(g, next => {
            var c = (Minion)next.IdEntityDic[full.Id];
            int h = next.Player1.HandZone.Count;
            Play(next, next.Player1, "CS2_008", c);
            Core2026Proof.Check(next.Player1.Hero.Damage == dmg + 3, id + " the trigger survives clone and snapshot (the second fatigue costs 2)");
        });
        End(g);
        var theirs = (Minion)Play(g, p.Opponent, id);
        int mineNow = p.HandZone.Count, mineDeck = p.DeckZone.Count, theirHandNow = p.Opponent.HandZone.Count;
        Play(g, p.Opponent, "CS2_008", theirs);
        Core2026Proof.Check(p.Opponent.HandZone.Count == theirHandNow + 1 && p.HandZone.Count == mineNow && p.DeckZone.Count == mineDeck,
            id + " opponent copy draws for the opponent only");
        FairViewHidesCards(g, id + " after both seats drew");
        End(g);
    }
}
