using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Actions;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Wave Y0b, lane m2c: Goblins vs Gnomes GVG_051 .. GVG_075 (Warrior, Paladin, Shaman, Priest, Hunter and neutral cards).
// Unregistered (not in the batch manifest), their scenarios kept below and run only with WILD_Y0B_M2C_UNREGISTERED=1:
//  - BLOCKED-STANDARD GVG_058, GVG_062 (data/pools/pool-eligibility-v1.json), GVG_059, GVG_061 (plugin/LocalCardCatalog.cs CoreUnlock rows
//    and data/progression/core-2026-unlocks.json copyGroup), GVG_070 (data/pools/pool-eligibility-v1.json, plugin/Npc/TriggerMeasure.cs): Standard
//    data uses them today. Their scripts are not changed here; GVG_061's script already matches the 37.0 text.
// New mechanic: FORGETFUL ("50% chance to attack the wrong enemy": GVG_054 Ogre Warmaul, GVG_065 Ogre Brute, GVG_066 Dunemaul Shaman) is
// SpecificTask.Forgetful on a TARGET trigger. Rule (hearthstone.wiki.gg/wiki/Forgetful): a fair coin; on a miss the attack goes to a uniformly
// random OTHER enemy character (hero included, Taunt / Stealth / Immune ignored); with no other enemy the attack stays on its target.
internal static class WildY0bM2CProof
{
    [GroupProof("wild-y0b-m2c-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        WildSetsProof.Case("GVG_051", Warbot);
        WildSetsProof.Case("GVG_052", Crush);
        WildSetsProof.Case("GVG_053", Shieldmaiden);
        WildSetsProof.Case("GVG_054", OgreWarmaul);
        WildSetsProof.Case("GVG_055", ScrewjankClunker);
        WildSetsProof.Held("GVG_056", "held: generated-shuffle FX scope", IronJuggernaut);
        WildSetsProof.Case("GVG_057", SealOfLight);
        WildSetsProof.Case("GVG_060", Quartermaster);
        WildSetsProof.Case("GVG_063", BolvarFordragon);
        WildSetsProof.Case("GVG_064", Puddlestomper);
        WildSetsProof.Case("GVG_065", OgreBrute);
        WildSetsProof.Case("GVG_066", DunemaulShaman);
        WildSetsProof.Case("GVG_067", StonesplinterTrogg);
        WildSetsProof.Case("GVG_068", BurlyRockjawTrogg);
        WildSetsProof.Case("GVG_069", AntiqueHealbot);
        WildSetsProof.Case("GVG_071", LostTallstrider);
        WildSetsProof.Case("GVG_072", Shadowboxer);
        WildSetsProof.Case("GVG_073", CobraShot);
        WildSetsProof.Case("GVG_074", KezanMystic);
        WildSetsProof.Case("GVG_075", ShipsCannon);
        if (Environment.GetEnvironmentVariable("WILD_Y0B_M2C_UNREGISTERED") == "1") RunUnregistered();
    }

    // ---- fixtures -------------------------------------------------------------------------------------------------

    private static void Check(bool condition, string message) => Core2026Proof.Check(condition, message);
    private static void Fill(Controller c, string id = "CS2_182") { while (!c.BoardZone.IsFull) Summon(c, id); }
    private static void EmptyHand(Controller c) { foreach (var held in c.HandZone.ToArray()) c.SetasideZone.Add(c.HandZone.Remove(held)); }
    private static void Equip(Controller c, string id = "CS2_106") => c.Hero.AddWeapon((Weapon)Entity.FromCard(c, Cards.FromId(id)));
    private static bool Playable(Controller c, IPlayable card) =>
        c.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id);
    private static bool Targetable(Controller c, IPlayable card, ICharacter target) =>
        c.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id && t.Target?.Id == target.Id);
    private static void Park(Controller c) => StandardSetsProof.ParkBoard(c);
    private static T Get<T>(Game game, T entity) where T : class, IPlayable => (T)game.IdEntityDic[entity.Id];
    private static Controller Get(Game game, Controller c) => c.PlayerId == game.Player1.PlayerId ? game.Player1 : game.Player2;

    private static void Resume(Game game, Action<Game> continuation)
    {
        foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) })
        {
            Check(Canonical.State(game) == Canonical.State(next), "m2c state before continuation");
            continuation(next);
        }
    }

    private static Game Fresh(int seed)
    {
        var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            Player1HeroClass = CardClass.ROGUE, Player2HeroClass = CardClass.MAGE, History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), FormatType = FormatType.FT_WILD });
        game.StartGame();
        foreach (var owner in new[] { game.Player1, game.Player2 })
        {
            foreach (var opening in owner.HandZone.ToArray()) { owner.HandZone.Remove(opening); owner.SetasideZone.Add(opening); }
            owner.BaseMana = 10;
        }
        return game;
    }

    /// <summary>The other seat's fair view (bot simulation and projection input) must not know a hidden hand card.</summary>
    private static void Concealed(Game g, IPlayable card, string what)
    {
        var field = typeof(FairBotState).GetField("game", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var own = FairBotState.Create(g, card.Controller.PlayerId, 3, DeckStyle.Control);
        var ownView = (Game)field.GetValue(own);
        Check(ownView.IdEntityDic.TryGetValue(card.Id, out var known) && known.Card.Id == card.Card.Id, what + ": control: the owner's view knows its own card");
        foreach (int seed in new[] { 3, 71 })
        {
            var fair = FairBotState.Create(g, card.Controller.Opponent.PlayerId, seed, DeckStyle.Control);
            var view = (Game)field.GetValue(fair);
            Check(!view.IdEntityDic.TryGetValue(card.Id, out var seen) || seen.Card.Id != card.Card.Id,
                what + ": the opponent's fair view does not know the hidden hand card " + card.Card.Id);
        }
    }

    private static void Swing(Game g, Controller p, ICharacter target)
    {
        p.Hero.IsExhausted = false; p.Hero.NumAttacksThisTurn = 0;
        var task = p.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == target.Id);
        Check(task != null && g.Process(task), "hero attack accepted");
    }

    private static void Strike(Game g, Minion attacker, ICharacter target)
    {
        attacker.NumAttacksThisTurn = 0;
        Attack(g, attacker, target);
    }

    /// <summary>No character may keep a combat flag once an attack is over (a redirected attack must not leave the intended target "defending").</summary>
    private static void NoCombatFlags(Game g, string what)
    {
        foreach (var entity in g.IdEntityDic.Values.OfType<ICharacter>())
            Check(!entity.IsAttacking && !entity.IsDefending, what + ": no stale combat flag on " + entity.Card.Id);
    }

    /// <summary>Runs <paramref name="n"/> attacks; returns how often the intended target and each other candidate lost health.</summary>
    private static (int Intended, int[] Others) Trials(Game g, int n, ICharacter intended, ICharacter[] others, Action swing)
    {
        int hitIntended = 0; var hitOthers = new int[others.Length];
        for (int i = 0; i < n; i++)
        {
            intended.Damage = 0; foreach (var o in others) o.Damage = 0;
            swing();
            Check(intended.Damage > 0 ^ others.Any(o => o.Damage > 0), "an attack lands on exactly one of the candidates");
            if (intended.Damage > 0) hitIntended++;
            for (int k = 0; k < others.Length; k++) if (others[k].Damage > 0) hitOthers[k]++;
            NoCombatFlags(g, "after attack " + i);
        }
        intended.Damage = 0; foreach (var o in others) o.Damage = 0;
        return (hitIntended, hitOthers);
    }

    private static void Hit(Game g, ICharacter target, IPlayable source, int n) { target.TakeDamage(source, n); g.DeathProcessingAndAuraUpdate(); }
    private static void Heal(Game g, ICharacter target, IPlayable source, int n) { target.TakeHeal(source, n); g.DeathProcessingAndAuraUpdate(); }
    private static void SetDamage(Game g, ICharacter target, int n) { target.Damage = n; g.DeathProcessingAndAuraUpdate(); }
    private static Minion Dummy(Controller c, string id = "CS2_182") { var m = Summon(c, id); m[GameTag.ATK] = 0; m[GameTag.HEALTH] = 20; return m; }

    // ---- GVG_051 Warbot ---------------------------------------------------------------------------------------------

    private static void Warbot(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var bot = (Minion)Play(g, p, id);
        Check(bot.Card.Cost == 1 && bot.AttackDamage == 1 && bot.Health == 3 && bot.IsRace(Race.MECHANICAL), "Warbot is a 1/3 Mech for 1");
        Check(bot.AttackDamage == 1, "undamaged: no Attack bonus");
        Hit(g, bot, Summon(opp, "CS2_182"), 1);
        Check(bot.AttackDamage == 2, "damaged: +1 Attack");
        Hit(g, bot, opp.BoardZone[0], 1);
        Check(bot.AttackDamage == 2 && bot.Health == 1, "still +1, not +1 per damage");
        Heal(g, bot, Add(p, "EX1_011"), 1);
        Check(bot.AttackDamage == 2, "still damaged after a partial heal");
        Heal(g, bot, Add(p, "EX1_011"), 5);
        Check(bot.AttackDamage == 1 && bot.Damage == 0, "healed to full: bonus gone");
        Play(g, p, "CS2_087", bot);
        Check(bot.AttackDamage == 4, "Blessing of Might: 4");
        Hit(g, bot, opp.BoardZone[0], 1);
        Check(bot.AttackDamage == 5, "enrage sits on top of other buffs");
        Resume(g, next => Check(Get(next, bot).AttackDamage == 5, "clone / snapshot keep the enraged Attack"));
        bot.Silence();
        Check(bot.AttackDamage == 1, "Silence removes the buff and the enrage");
        Hit(g, bot, opp.BoardZone[0], 1);
        Check(bot.AttackDamage == 1, "a Silenced Warbot no longer enrages");
        End(g);
        var theirs = (Minion)Play(g, opp, id);
        Hit(g, theirs, p.BoardZone[0], 1);
        Check(theirs.AttackDamage == 2 && bot.AttackDamage == 1, "the opponent's Warbot enrages on its own");
        Kill(g, theirs);
        Check(theirs.Zone.Type == Zone.GRAVEYARD, "it can die as usual");
    }

    // ---- GVG_052 Crush ----------------------------------------------------------------------------------------------

    private static void Crush(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var spell = Add(p, id);
        Check(!Playable(p, spell), "Crush needs a minion target");
        var enemy = Summon(opp, "CS2_182"); var friend = Summon(p, "CS2_182");
        Check(spell.Cost == 7 && Playable(p, spell), "7 mana with no damaged friendly minion");
        Check(Targetable(p, spell, enemy) && Targetable(p, spell, friend) && !Targetable(p, spell, opp.Hero), "targets any minion, no hero");
        SetDamage(g, enemy, 2);
        Check(spell.Cost == 7, "a damaged ENEMY minion does not discount");
        SetDamage(g, p.Hero, 5);
        Check(spell.Cost == 7, "a damaged hero does not discount");
        SetDamage(g, friend, 1);
        Check(spell.Cost == 3, "a damaged friendly minion makes it cost 4 less: " + spell.Cost);
        SetDamage(g, friend, 0);
        Check(spell.Cost == 7, "back to 7 when healed");
        SetDamage(g, friend, 1);
        PlayEntity(g, p, spell, enemy);
        Check(enemy.Zone.Type == Zone.GRAVEYARD && friend.Zone.Type == Zone.PLAY && p.UsedMana == 3, "destroys the target for 3");
        var second = Add(p, id);
        Check(second.Cost == 3, "the discount applies to every copy in hand");
        var shielded = Summon(opp, "CS2_182"); shielded.HasDivineShield = true;
        PlayEntity(g, p, second, shielded);
        Check(shielded.Zone.Type == Zone.GRAVEYARD, "Divine Shield does not stop a destroy effect");
        var third = Add(p, id);
        Kill(g, friend);
        Check(third.Cost == 7, "no damaged friendly minion left: full price");
        var victim = Summon(p, "CS2_182");
        PlayEntity(g, p, third, victim);
        Check(victim.Zone.Type == Zone.GRAVEYARD, "may destroy its own minion");
        Resume(g, next => Check(Get(next, third).Cost == 7, "clone keeps the cost rule"));
        End(g);
        var mine = Summon(p, "CS2_182"); var theirs = Summon(opp, "CS2_182"); SetDamage(g, theirs, 3);
        var oppSpell = Add(opp, id);
        Check(oppSpell.Cost == 3 && Add(p, id).Cost == 7, "the discount follows the caster's own board");
        PlayEntity(g, opp, oppSpell, mine);
        Check(mine.Zone.Type == Zone.GRAVEYARD, "the opponent's Crush works");
    }

    // ---- GVG_053 Shieldmaiden ---------------------------------------------------------------------------------------

    private static void Shieldmaiden(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 5, "Shieldmaiden costs 5 in 37.0");
        PlayEntity(g, p, card);
        var maiden = (Minion)card;
        Check(maiden.AttackDamage == 5 && maiden.Health == 5 && p.Hero.Armor == 5 && opp.Hero.Armor == 0, "a 5/5 that gains 5 Armor for its hero only");
        Play(g, p, id);
        Check(p.Hero.Armor == 10, "Armor stacks");
        Fill(p);
        Park(p); Fill(p, "CS2_182");
        Check(p.BoardZone.IsFull, "full board fixture");
        var hand = Add(p, id);
        Check(!Playable(p, hand), "not playable on a full board");
        Park(p);
        maiden = (Minion)Play(g, p, id);
        maiden.Silence();
        Check(p.Hero.Armor == 15, "Silence does not remove gained Armor");
        End(g);
        Play(g, opp, id);
        Check(opp.Hero.Armor == 5 && p.Hero.Armor == 15, "the opponent gains its own Armor");
        Resume(g, next => Check(Get(next, opp).Hero.Armor == 5, "clone keeps Armor"));
    }

    // ---- GVG_054 Ogre Warmaul ---------------------------------------------------------------------------------------

    private static void OgreWarmaul(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var weapon = (Weapon)Add(p, id);
        Check(weapon.Cost == 3 && weapon.AttackDamage == 4 && weapon.Durability == 2, "Ogre Warmaul is a 4/2 weapon for 3");
        PlayEntity(g, p, weapon);
        Check(p.Hero.Weapon == weapon && p.Hero.AttackDamage == 4, "equipped, hero Attack 4");
        // empty enemy board: the hero is the only enemy, nothing to mix up
        for (int i = 0; i < 20; i++) { opp.Hero.Damage = 0; weapon.Damage = 0; Swing(g, p, opp.Hero); Check(opp.Hero.Damage == 4, "the only enemy is the hero: the attack stays"); }
        var a = Dummy(opp); var b = Dummy(opp); var c = Dummy(opp);
        var (intended, others) = Trials(g, 100, a, new ICharacter[] { b, c, opp.Hero }, () => { weapon.Damage = 0; Swing(g, p, a); });
        Check(intended >= 30 && intended <= 70, "about half the swings reach the intended target: " + intended);
        Check(others.All(x => x >= 3), "every other enemy, hero included, is a candidate: " + string.Join("/", others));
        // Taunt is bypassed, Stealth ignored
        Park(opp);
        var taunt = Dummy(opp, "CS2_179"); var plain = Dummy(opp); var sneaky = Dummy(opp, "EX1_010"); sneaky[GameTag.ATK] = 0;
        Check(taunt.HasTaunt && sneaky.HasStealth, "fixture");
        Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == plain.Id), "Taunt still restricts the choice of the player");
        var (tIntended, tOthers) = Trials(g, 100, taunt, new ICharacter[] { plain, sneaky, opp.Hero }, () => { weapon.Damage = 0; Swing(g, p, taunt); });
        Check(tIntended >= 30 && tIntended <= 70 && tOthers.All(x => x >= 3), "the wrong target ignores Taunt and Stealth: " + tIntended + "/" + string.Join("/", tOthers));
        // durability and the end of the weapon
        weapon.Damage = 0; Park(opp);
        Swing(g, p, opp.Hero);
        Check(weapon.Durability == 1, "an attack costs one durability: " + weapon.Durability);
        Swing(g, p, opp.Hero);
        Check(p.Hero.Weapon == null && weapon.Zone.Type != Zone.PLAY, "destroyed after two swings");
        Check(opp.Hero.Damage == 8, "both swings landed (hero was the only enemy)");
        // a replacement weapon is not forgetful
        Equip(p); Dummy(opp); Dummy(opp);
        var target = opp.BoardZone[0];
        for (int i = 0; i < 30; i++)
        {
            p.Hero.Weapon.Damage = 0; target.Damage = 0;
            Swing(g, p, target);
            Check(target.Damage == 3 && opp.Hero.Damage == 8 && opp.BoardZone[1].Damage == 0, "Fiery War Axe always hits the intended target");
        }
        // minions of the owner are not affected by the weapon
        PlayEntity(g, p, (Weapon)Add(p, id)); var friend = Summon(p, "CS2_182");
        for (int i = 0; i < 30; i++) { target.Damage = 0; opp.BoardZone[1].Damage = 0; Strike(g, friend, target); Check(target.Damage == 4 && opp.BoardZone[1].Damage == 0, "a friendly minion attack is never redirected by the weapon"); friend.Damage = 0; }
        // the opponent's Warmaul
        End(g);
        Park(p); Park(opp);
        var mine1 = Dummy(p); var mine2 = Dummy(p);
        var theirs = (Weapon)Add(opp, id); PlayEntity(g, opp, theirs);
        var (oi, oo) = Trials(g, 100, mine1, new ICharacter[] { mine2, p.Hero }, () => { theirs.Damage = 0; Swing(g, opp, mine1); });
        Check(oi >= 30 && oi <= 70 && oo.All(x => x >= 10), "the opponent's Warmaul misdirects against my side: " + oi + "/" + string.Join("/", oo));
        // spells and hero powers are untouched
        var spell = Add(opp, "CS2_029");
        PlayEntity(g, opp, spell, mine1);
        Check(mine1.Damage >= 6 && mine2.Damage == 0, "a spell keeps its target");
        Resume(g, next => Check(Get(next, opp).Hero.Weapon?.Card.Id == id, "clone keeps the weapon"));
    }

    // ---- GVG_055 Screwjank Clunker ----------------------------------------------------------------------------------

    private static void ScrewjankClunker(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var clunker = Add(p, id);
        Check(clunker.Cost == 4 && ((Minion)clunker).AttackDamage == 2 && ((Minion)clunker).Health == 5 && ((Minion)clunker).IsRace(Race.MECHANICAL), "a 2/5 Mech for 4");
        Check(Playable(p, clunker), "playable with no Mech on the board");
        var yeti = Summon(p, "CS2_182"); var enemyMech = Summon(opp, "GVG_085");
        Check(Playable(p, clunker) && !Targetable(p, clunker, yeti) && !Targetable(p, clunker, enemyMech), "neither a friendly non-Mech nor an enemy Mech is a target");
        PlayEntity(g, p, clunker);
        Check(yeti.AttackDamage == 4 && enemyMech.AttackDamage == 1 && ((Minion)clunker).AttackDamage == 2, "nothing got a buff");
        var mech = Summon(p, "GVG_085");
        var second = Add(p, id);
        Check(Targetable(p, second, mech) && Targetable(p, second, (Minion)clunker), "a friendly Mech is a target (the first Clunker is a Mech too)");
        PlayEntity(g, p, second, mech);
        Check(mech.AttackDamage == 3 && mech.Health == 4, "+2/+2 on the target");
        Resume(g, next => Check(Get(next, mech).AttackDamage == 3, "clone keeps the buff"));
        mech.Silence();
        Check(mech.AttackDamage == 1 && mech.Health == 2, "Silence removes it");
        Fill(p); Park(p); var only = Summon(p, "GVG_085");
        Check(only.Zone.Type == Zone.PLAY, "fixture");
        End(g);
        var theirs = Summon(opp, "GVG_085");
        PlayEntity(g, opp, Add(opp, id), theirs);
        Check(theirs.AttackDamage == 3 && only.AttackDamage == 1, "the opponent buffs its own Mech");
    }

    // ---- GVG_056 Iron Juggernaut ------------------------------------------------------------------------------------

    private static int Mines(Controller c) => c.DeckZone.Count(x => x.Card.Id == "GVG_056t");

    private static void IronJuggernaut(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 6 && ((Minion)card).AttackDamage == 6 && ((Minion)card).Health == 5 && ((Minion)card).IsRace(Race.MECHANICAL), "a 6/5 Mech for 6");
        Check(((Minion)card).HasDeathrattle, "37.0: Battlecry AND Deathrattle");
        int own = p.DeckZone.Count, theirs = opp.DeckZone.Count;
        PlayEntity(g, p, card);
        Check(Mines(opp) == 1 && Mines(p) == 0 && opp.DeckZone.Count == theirs + 1 && p.DeckZone.Count == own, "Battlecry: a Mine into the OPPONENT's deck only");
        var juggernaut = (Minion)card;
        Resume(g, next => Check(Mines(Get(next, opp)) == 1 && Mines(Get(next, p)) == 0, "clone keeps the Mine"));
        Kill(g, juggernaut);
        Check(juggernaut.Zone.Type == Zone.GRAVEYARD && Mines(opp) == 2 && Mines(p) == 0, "Deathrattle: a second Mine");
        // silenced: no Deathrattle
        var silenced = (Minion)Play(g, p, id);
        Check(Mines(opp) == 3, "each play adds one");
        silenced.Silence(); Kill(g, silenced);
        Check(Mines(opp) == 3, "a Silenced Juggernaut leaves no Mine behind");
        // full opposing deck
        while (!opp.DeckZone.IsFull) Entity.FromCard(opp, Cards.FromId("CS2_182"), zone: opp.DeckZone);
        int before = opp.DeckZone.Count;
        Play(g, p, id);
        Check(opp.DeckZone.Count == before && Mines(opp) == 3, "a full deck takes no Mine");
        foreach (var extra in opp.DeckZone.Where(x => x.Card.Id == "GVG_056t").Skip(1).ToArray()) opp.SetasideZone.Add(opp.DeckZone.Remove(extra));
        Check(Mines(opp) == 1 && opp.Hero.Damage == 0, "fixture: one Mine left");
        int guard = 0;
        while (Mines(opp) > 0 && guard++ < 100) { EmptyHand(opp); Generic.Draw(opp); }
        Check(Mines(opp) == 0 && opp.Hero.Damage == 10, "a drawn Mine explodes for 10 on its drawer: " + opp.Hero.Damage);
        Check(!p.Hero.IsDead && p.Hero.Damage == 0, "the Juggernaut's owner takes nothing");
        // the opponent's Juggernaut puts the Mine into MY deck
        var g2 = Fresh(5); var a = g2.Player1; var b = g2.Player2;
        End(g2);
        int mine = Mines(a);
        Play(g2, b, id);
        Check(Mines(a) == mine + 1 && Mines(b) == 0, "the opponent's Juggernaut shuffles the Mine into my deck");
        var hp = a.Hero.Damage;
        foreach (var extra in a.DeckZone.Where(x => x.Card.Id == "GVG_056t").Skip(1).ToArray()) a.SetasideZone.Add(a.DeckZone.Remove(extra));
        while (Mines(a) > 0) { EmptyHand(a); Generic.Draw(a); }
        Check(a.Hero.Damage == hp + 10, "and I take the 10");
        // random placement: the Mine does not always sit in the same slot
        var slots = new HashSet<int>();
        for (int seed = 1; seed <= 12; seed++)
        {
            var f = Fresh(seed); End(f);
            Play(f, f.Player2, id);
            slots.Add(f.Player1.DeckZone.ToList().FindIndex(x => x.Card.Id == "GVG_056t"));
        }
        Check(slots.Count >= 3 && !slots.Contains(-1), "the Mine lands at a random position: " + string.Join(",", slots.OrderBy(x => x)));
    }

    // ---- GVG_057 Seal of Light --------------------------------------------------------------------------------------

    private static void SealOfLight(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var spell = Add(p, id);
        Check(spell.Cost == 2 && Playable(p, spell), "2 mana, no target");
        p.Hero.Damage = 10; opp.Hero.Damage = 10;
        PlayEntity(g, p, spell);
        Check(p.Hero.Damage == 6 && opp.Hero.Damage == 10, "restores 4 to my hero only");
        Check(p.Hero.AttackDamage == 2, "+2 Attack this turn");
        Play(g, p, id);
        Check(p.Hero.AttackDamage == 4, "stacks to +4");
        Equip(p);
        Check(p.Hero.AttackDamage == 7, "adds to a weapon (3 + 4)");
        p.Hero.Damage = 1;
        Play(g, p, id);
        Check(p.Hero.Damage == 0, "healing stops at full health");
        p.Hero.Armor = 3;
        Resume(g, next => Check(Get(next, p).Hero.AttackDamage == 9 && Get(next, p).Hero.Armor == 3, "clone keeps the temporary Attack"));
        Swing(g, p, opp.Hero);
        Check(opp.Hero.Damage == 19, "the bonus hits for real: 10 + 9");
        End(g);
        Check(opp.Hero.AttackDamage == 0, "the opponent never had the bonus");
        End(g);
        Check(p.Hero.AttackDamage == 3, "the bonus ended with my turn: " + p.Hero.AttackDamage + " weapon " + p.Hero.Weapon?.Card.Id + " " + p.Hero.Weapon?.Durability + " temp " + p.Hero[GameTag.ATK]);
        End(g);
        opp.Hero.Damage = 12;
        Play(g, opp, id);
        Check(opp.Hero.Damage == 8 && opp.Hero.AttackDamage == 2 && p.Hero[GameTag.ATK] == 0, "the opponent's Seal of Light is its own");
    }

    // ---- GVG_060 Quartermaster --------------------------------------------------------------------------------------

    private static void Quartermaster(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 5 && ((Minion)card).AttackDamage == 2 && ((Minion)card).Health == 5, "a 2/5 for 5");
        Check(Playable(p, card) && !p.Options().Any(t => t.Source?.Id == card.Id && t.Target != null), "playable with no Recruits; no target");
        var yeti = Summon(p, "CS2_182");
        PlayEntity(g, p, card);
        Check(yeti.AttackDamage == 4 && ((Minion)card).AttackDamage == 2, "with no Recruits it does nothing");
        var r1 = Summon(p, "CS2_101t"); var r2 = Summon(p, "CS2_101t"); var enemyRecruit = Summon(opp, "CS2_101t");
        var handRecruit = Add(p, "CS2_101t");
        Play(g, p, id);
        Check(r1.AttackDamage == 3 && r1.Health == 3 && r2.AttackDamage == 3 && r2.Health == 3, "friendly Silver Hand Recruits get +2/+2");
        Check(enemyRecruit.AttackDamage == 1 && yeti.AttackDamage == 4 && ((Minion)handRecruit).AttackDamage == 1, "enemy Recruits, other minions and the hand are untouched");
        var later = Summon(p, "CS2_101t");
        Check(later.AttackDamage == 1, "a Recruit summoned later is not buffed");
        Play(g, p, id);
        Check(r1.AttackDamage == 5 && later.AttackDamage == 3, "a second copy buffs every Recruit again");
        Resume(g, next => Check(Get(next, r1).AttackDamage == 5 && Get(next, r1).Health == 5, "clone keeps the buff"));
        r2.Silence();
        Check(r2.AttackDamage == 1 && r2.Health == 1, "Silence removes it");
        r1.Damage = 4; Kill(g, r2);
        Check(r1.Health == 1 && r2.Zone.Type == Zone.GRAVEYARD, "buffed health is real");
        End(g);
        var mine = Summon(p, "CS2_101t"); var theirs = Summon(opp, "CS2_101t");
        Play(g, opp, id);
        Check(theirs.AttackDamage == 3 && mine.AttackDamage == 1, "the opponent buffs its own Recruits");
    }

    // ---- GVG_063 Bolvar Fordragon -----------------------------------------------------------------------------------

    private static void BolvarFordragon(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var bolvar = (Minion)Add(p, id);
        Check(bolvar.Card.Cost == 5 && bolvar.AttackDamage == 1 && bolvar.Health == 7, "a 1/7 for 5");
        Concealed(g, bolvar, "Bolvar in hand");
        var a = Summon(p, "CS2_182"); var b = Summon(p, "CS2_182"); var enemy = Summon(opp, "CS2_182");
        Kill(g, enemy);
        Check(bolvar.AttackDamage == 1, "an enemy death does nothing");
        Kill(g, a);
        Check(bolvar.AttackDamage == 2, "a friendly death in hand: +1");
        a = Summon(p, "CS2_182");
        a.ToBeDestroyed = true; b.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
        Check(bolvar.AttackDamage == 4, "two simultaneous deaths: +2");
        Resume(g, next => Check(Get(next, bolvar).AttackDamage == 4, "clone keeps the hand buff"));
        var deckBolvar = Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
        var c1 = Summon(p, "CS2_182"); Kill(g, c1);
        Check(((Minion)deckBolvar).AttackDamage == 1 && bolvar.AttackDamage == 5, "a Bolvar in the deck gets nothing");
        PlayEntity(g, p, bolvar);
        Check(bolvar.AttackDamage == 5 && bolvar.Zone.Type == Zone.PLAY, "the buff stays when played");
        var d = Summon(p, "CS2_182"); Kill(g, d);
        Check(bolvar.AttackDamage == 5, "on the board it no longer grows");
        bolvar.Silence();
        Check(bolvar.AttackDamage == 1, "Silence removes the gained Attack");
        // a Bolvar discarded or returned
        End(g);
        var theirs = (Minion)Add(opp, id);
        var mine = Summon(p, "CS2_182"); Kill(g, mine);
        Check(theirs.AttackDamage == 1, "my minions' deaths do not feed the opponent's Bolvar");
        var own = Summon(opp, "CS2_182"); Kill(g, own);
        Check(theirs.AttackDamage == 2, "the opponent's Bolvar grows from its own side");
    }

    // ---- GVG_064 Puddlestomper / GVG_071 Lost Tallstrider -----------------------------------------------------------

    private static void Puddlestomper(Game g, Controller p, string id)
    {
        var m = (Minion)Play(g, p, id);
        Check(m.Card.Cost == 2 && m.AttackDamage == 3 && m.Health == 2 && m.IsRace(Race.MURLOC) && string.IsNullOrEmpty(m.Card.Text), "a vanilla 3/2 Murloc for 2");
        Check(!m.HasTaunt && !m.HasDivineShield && !m.HasCharge && !m.HasStealth && !m.HasDeathrattle, "no keywords");
        var enemy = Summon(p.Opponent, "CS2_182");
        End(g); End(g);
        Strike(g, m, enemy);
        Check(enemy.Damage == 3 && m.Zone.Type == Zone.GRAVEYARD, "fights as a 3/2 and dies to a Yeti");
        Resume(g, next => Check(Get(next, enemy).Damage == 3, "clone keeps the fight"));
    }

    private static void LostTallstrider(Game g, Controller p, string id)
    {
        var m = (Minion)Play(g, p, id);
        Check(m.Card.Cost == 4 && m.AttackDamage == 5 && m.Health == 4 && m.IsRace(Race.BEAST) && string.IsNullOrEmpty(m.Card.Text), "a vanilla 5/4 Beast for 4");
        Check(!m.HasTaunt && !m.HasDivineShield && !m.HasCharge && !m.HasStealth && !m.HasDeathrattle, "no keywords");
        var enemy = Summon(p.Opponent, "CS2_182");
        End(g); End(g);
        Strike(g, m, enemy);
        Check(enemy.Zone.Type == Zone.GRAVEYARD && m.Damage == 4 && m.Zone.Type == Zone.GRAVEYARD, "trades with a Yeti as a 5/4");
    }

    // ---- GVG_065 Ogre Brute -----------------------------------------------------------------------------------------

    private static void OgreBrute(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var brute = (Minion)Play(g, p, id);
        Check(brute.Card.Cost == 3 && brute.AttackDamage == 4 && brute.Health == 4, "a 4/4 for 3");
        brute[GameTag.ATK] = 4;
        // only the enemy hero: nothing to confuse
        for (int i = 0; i < 20; i++) { opp.Hero.Damage = 0; Strike(g, brute, opp.Hero); Check(opp.Hero.Damage == 4, "the hero is the only enemy: the attack stays"); brute.Damage = 0; }
        var a = Dummy(opp); var b = Dummy(opp); var c = Dummy(opp);
        var (intended, others) = Trials(g, 100, a, new ICharacter[] { b, c, opp.Hero }, () => Strike(g, brute, a));
        Check(intended >= 30 && intended <= 70, "about half the attacks reach the intended target: " + intended);
        Check(others.All(x => x >= 3), "every other enemy, hero included, is a candidate: " + string.Join("/", others));
        // attacking the hero with minions on the other side: the minions are the wrong targets
        var (hi, ho) = Trials(g, 100, opp.Hero, new ICharacter[] { a, b, c }, () => Strike(g, brute, opp.Hero));
        Check(hi >= 30 && hi <= 70 && ho.All(x => x >= 3), "hero as intended target: " + hi + "/" + string.Join("/", ho));
        // Taunt and Stealth are ignored by the wrong target
        Park(opp);
        var taunt = Dummy(opp, "CS2_179"); var plain = Dummy(opp); var sneaky = Dummy(opp, "EX1_010"); sneaky[GameTag.ATK] = 0;
        var (ti, to) = Trials(g, 100, taunt, new ICharacter[] { plain, sneaky, opp.Hero }, () => Strike(g, brute, taunt));
        Check(ti >= 30 && ti <= 70 && to.All(x => x >= 3), "Taunt and Stealth do not limit the wrong target: " + ti + "/" + string.Join("/", to));
        // a wrong target that dies, a Divine Shield that pops
        Park(opp);
        var weak = Summon(opp, "CS2_231"); var shielded = Dummy(opp); shielded.HasDivineShield = true;
        int weakDied = 0, popped = 0;
        for (int i = 0; i < 60; i++)
        {
            if (weak.Zone.Type != Zone.PLAY) { weak = Summon(opp, "CS2_231"); }
            shielded.HasDivineShield = true; shielded.Damage = 0; opp.Hero.Damage = 0; brute.Damage = 0;
            if (weak.Zone.Type == Zone.PLAY) { Strike(g, brute, shielded); }
            if (weak.Zone.Type != Zone.PLAY) weakDied++;
            if (!shielded.HasDivineShield) popped++;
        }
        Check(weakDied >= 3 && popped >= 20, "a misdirected attack resolves as a normal attack on its new target: " + weakDied + "/" + popped);
        // Silence
        Park(opp);
        var x1 = Dummy(opp); var x2 = Dummy(opp);
        brute.Silence();
        for (int i = 0; i < 40; i++) { x1.Damage = 0; x2.Damage = 0; brute.Damage = 0; Strike(g, brute, x1); Check(x1.Damage == 4 && x2.Damage == 0 && opp.Hero.Damage == 0, "a Silenced Ogre Brute attacks what it is told"); }
        // the opponent's Brute
        End(g);
        Park(p); Park(opp);
        var theirs = (Minion)Play(g, opp, id);
        var m1 = Dummy(p); var m2 = Dummy(p);
        var (oi, oo) = Trials(g, 100, m1, new ICharacter[] { m2, p.Hero }, () => Strike(g, theirs, m1));
        Check(oi >= 30 && oi <= 70 && oo.All(x => x >= 10), "the opponent's Brute misdirects against my side: " + oi + "/" + string.Join("/", oo));
        Resume(g, next => Check(Get(next, theirs).Zone.Type == Zone.PLAY, "clone keeps the Ogre"));
    }

    // ---- GVG_066 Dunemaul Shaman ------------------------------------------------------------------------------------

    private static void DunemaulShaman(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 4 && ((Minion)card).AttackDamage == 5 && ((Minion)card).Health == 4 && ((Minion)card).HasWindfury, "a 5/4 Windfury for 4");
        PlayEntity(g, p, card);
        var shaman = (Minion)card;
        Check(p.OverloadOwed == 1, "Overload (1) owed");
        End(g); End(g);
        Check(p.OverloadLocked == 1 && p.RemainingMana == p.BaseMana - 1, "one crystal locked next turn");
        // two attacks per turn (Windfury), each with its own coin
        var a = Dummy(opp); var b = Dummy(opp);
        shaman.IsExhausted = false; shaman.NumAttacksThisTurn = 0;
        Attack(g, shaman, a);
        Check(shaman.NumAttacksThisTurn == 1 && !shaman.IsExhausted, "Windfury: not exhausted after the first attack");
        var task = p.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == shaman.Id);
        Check(task != null && g.Process(task), "a second attack is offered");
        Check(shaman.IsExhausted, "and the third is not");
        a.Damage = 0; b.Damage = 0; shaman.Damage = 0;
        var (intended, others) = Trials(g, 100, a, new ICharacter[] { b, opp.Hero }, () => Strike(g, shaman, a));
        Check(intended >= 30 && intended <= 70 && others.All(x => x >= 5), "each Windfury attack is a fresh 50% coin: " + intended + "/" + string.Join("/", others));
        // a Windfury shaman can mix up both attacks
        var kinds = new HashSet<string>();
        for (int i = 0; i < 40; i++)
        {
            a.Damage = 0; b.Damage = 0; opp.Hero.Damage = 0; shaman.Damage = 0; shaman.NumAttacksThisTurn = 0; shaman.IsExhausted = false;
            Attack(g, shaman, a);
            var second = p.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == shaman.Id && t.Target?.Id == a.Id);
            Check(second != null && g.Process(second), "second attack on the same target");
            kinds.Add((a.Damage > 0 ? "A" : "-") + (b.Damage > 0 ? "B" : "-") + (opp.Hero.Damage > 0 ? "H" : "-"));
        }
        Check(kinds.Count >= 4, "two attacks give several combinations of victims: " + string.Join(",", kinds.OrderBy(k => k)));
        // Silence removes Windfury and the mix-up, not the Overload already owed
        shaman.Silence();
        Check(!shaman.HasWindfury, "Silence removes Windfury");
        for (int i = 0; i < 30; i++) { a.Damage = 0; b.Damage = 0; opp.Hero.Damage = 0; shaman.Damage = 0; Strike(g, shaman, a); Check(a.Damage == 5 && b.Damage == 0 && opp.Hero.Damage == 0, "Silenced: no more mix-ups"); }
        // the opponent's shaman
        End(g);
        Park(p); Park(opp);
        var theirs = (Minion)Play(g, opp, id);
        Check(opp.OverloadOwed == 1 && p.OverloadOwed == 0, "Overload belongs to its caster");
        var m1 = Dummy(p); var m2 = Dummy(p);
        var (oi, oo) = Trials(g, 100, m1, new ICharacter[] { m2, p.Hero }, () => Strike(g, theirs, m1));
        Check(oi >= 30 && oi <= 70 && oo.All(x => x >= 10), "the opponent's Shaman misdirects against my side: " + oi + "/" + string.Join("/", oo));
        Resume(g, next => Check(Get(next, opp).OverloadOwed == 1, "clone keeps the Overload"));
    }

    // ---- GVG_067 / GVG_068 Troggs -----------------------------------------------------------------------------------

    private static void Trogg(Game g, Controller p, string id, int step, int cost, int atk, int hp)
    {
        var opp = p.Opponent;
        var trogg = (Minion)Play(g, p, id);
        Check(trogg.Card.Cost == cost && trogg.AttackDamage == atk && trogg.Health == hp, "stats " + atk + "/" + hp + " for " + cost);
        var yeti = Summon(p, "CS2_182");
        Play(g, p, "CS2_029", opp.Hero);
        Check(trogg.AttackDamage == atk, "my own spell does nothing");
        Play(g, p, "CS2_087", yeti);
        Check(trogg.AttackDamage == atk, "a friendly buff spell does nothing");
        Play(g, p, "CS2_231");
        Check(trogg.AttackDamage == atk, "my minion play does nothing");
        End(g);
        Play(g, opp, "CS2_029", p.Hero);
        Check(trogg.AttackDamage == atk + step, "an enemy spell: +" + step);
        Play(g, opp, "CS2_029", yeti);
        Check(trogg.AttackDamage == atk + 2 * step, "each enemy spell stacks");
        Play(g, opp, "CS2_182");
        Check(trogg.AttackDamage == atk + 2 * step, "an enemy minion play does nothing");
        var weapon = Add(opp, "CS2_106"); PlayEntity(g, opp, weapon);
        Check(trogg.AttackDamage == atk + 2 * step, "an enemy weapon does nothing");
        Play(g, opp, "CS2_029", Summon(p, "CS2_182"));
        Resume(g, next => Check(Get(next, trogg).AttackDamage == atk + 3 * step, "clone keeps the gained Attack"));
        Play(g, opp, "CS2_087", opp.BoardZone[0]);
        Check(trogg.AttackDamage == atk + 4 * step, "an enemy spell on its own minion still counts");
        // two Troggs both react, Silence stops one
        var twin = Summon(p, id);
        trogg.Silence();
        int silencedAtk = trogg.AttackDamage;
        Play(g, opp, "CS2_029", p.Hero);
        Check(trogg.AttackDamage == silencedAtk && twin.AttackDamage == atk + step, "Silence stops the Trogg, a fresh one reacts");
        Kill(g, twin);
        var theirs = (Minion)Play(g, opp, id);
        End(g);
        Play(g, p, "CS2_029", opp.Hero);
        Check(theirs.AttackDamage == atk + step, "the opponent's Trogg reacts to MY spells");
        End(g);
        Play(g, opp, "CS2_029", p.Hero);
        Check(theirs.AttackDamage == atk + step, "and not to its own side's");
    }

    private static void StonesplinterTrogg(Game g, Controller p, string id) => Trogg(g, p, id, 1, 2, 2, 3);
    private static void BurlyRockjawTrogg(Game g, Controller p, string id) => Trogg(g, p, id, 2, 4, 3, 5);

    // ---- GVG_069 Antique Healbot ------------------------------------------------------------------------------------

    private static void AntiqueHealbot(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 5 && ((Minion)card).AttackDamage == 3 && ((Minion)card).Health == 3 && ((Minion)card).IsRace(Race.MECHANICAL), "a 3/3 Mech for 5");
        Check(Playable(p, card) && !p.Options().Any(t => t.Source?.Id == card.Id && t.Target != null), "untargeted");
        PlayEntity(g, p, card);
        Check(p.Hero.Damage == 0, "at full health nothing happens");
        p.Hero.Damage = 20; opp.Hero.Damage = 20;
        var damaged = Summon(p, "CS2_182"); damaged.Damage = 3;
        Play(g, p, id);
        Check(p.Hero.Damage == 12 && opp.Hero.Damage == 20 && damaged.Damage == 3, "restores 8 to my hero only");
        p.Hero.Damage = 3;
        Play(g, p, id);
        Check(p.Hero.Damage == 0, "no overheal");
        p.Hero.Damage = 8; p.Hero.Armor = 4;
        Play(g, p, id);
        Check(p.Hero.Damage == 0 && p.Hero.Armor == 4, "Armor is not touched");
        Resume(g, next => Check(Get(next, p).Hero.Damage == 0, "clone keeps the state"));
        End(g);
        opp.Hero.Damage = 20;
        Play(g, opp, id);
        Check(opp.Hero.Damage == 12 && p.Hero.Damage == 0, "the opponent heals its own hero");
    }

    // ---- GVG_072 Shadowboxer ----------------------------------------------------------------------------------------

    private static void Shadowboxer(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var box = (Minion)Play(g, p, id);
        Check(box.Card.Cost == 2 && box.AttackDamage == 2 && box.Health == 3 && box.IsRace(Race.MECHANICAL), "a 2/3 Mech for 2");
        var healer = Add(p, "EX1_011");
        var a = Summon(opp, "CS2_182"); var b = Summon(opp, "CS2_182"); var c = Summon(opp, "CS2_182");
        var friend = Summon(p, "CS2_182"); var enemyHurt = Summon(opp, "CS2_182");
        friend.Damage = 2;
        friend.TakeHeal(healer, 2);
        Check(friend.Damage == 0 && a.Damage + b.Damage + c.Damage + enemyHurt.Damage + opp.Hero.Damage == 1, "a friendly minion healed: 1 damage to a random enemy");
        foreach (var m in new[] { a, b, c, enemyHurt }) m.Damage = 0; opp.Hero.Damage = 0;
        friend.TakeHeal(healer, 2);
        Check(opp.Hero.Damage == 0 && a.Damage + b.Damage + c.Damage + enemyHurt.Damage == 0, "no healing, no trigger (undamaged minion)");
        p.Hero.Damage = 5; p.Hero.TakeHeal(healer, 3);
        opp.Hero.Damage = 5; opp.Hero.TakeHeal(healer, 3);
        Check(a.Damage + b.Damage + c.Damage + enemyHurt.Damage == 0 && opp.Hero.Damage == 2 && p.Hero.Damage == 2, "heroes are not minions: no trigger");
        opp.Hero.Damage = 0;
        // an ENEMY minion healed also triggers it
        var victim = Summon(opp, "CS2_182"); victim.Damage = 3;
        var before = opp.BoardZone.Sum(m => m.Damage) + opp.Hero.Damage + p.Hero.Damage + p.BoardZone.Sum(m => m.Damage);
        victim.TakeHeal(healer, 2);
        var after = opp.BoardZone.Sum(m => m.Damage) + opp.Hero.Damage + p.Hero.Damage + p.BoardZone.Sum(m => m.Damage);
        Check(after == before - 2 + 1, "'Whenever a minion is healed': an enemy minion's heal triggers too: " + before + " > " + after);
        Check(p.Hero.Damage == 2 && p.BoardZone.Sum(m => m.Damage) == 0, "only enemies of the Shadowboxer are hit");
        foreach (var m in opp.BoardZone) m.Damage = 0; opp.Hero.Damage = 0; p.Hero.Damage = 0;
        // randomness among three or more candidates (3 enemy minions + hero)
        Park(opp);
        var e1 = Summon(opp, "CS2_182"); var e2 = Summon(opp, "CS2_182"); var e3 = Summon(opp, "CS2_182");
        var hits = new int[4];
        for (int i = 0; i < 80; i++)
        {
            friend.Damage = 1; friend.TakeHeal(healer, 1);
            var victims = new ICharacter[] { e1, e2, e3, opp.Hero };
            for (int k = 0; k < 4; k++) if (victims[k].Damage > 0) { hits[k]++; victims[k].Damage = 0; }
        }
        Check(hits.All(x => x >= 8) && hits.Sum() == 80, "every enemy character is a candidate, one hit per heal: " + string.Join("/", hits));
        // a lethal ping
        Park(opp);
        var one = Summon(opp, "CS2_231");
        friend.Damage = 1; friend.TakeHeal(healer, 1);
        for (int i = 0; i < 20 && one.Zone.Type == Zone.PLAY; i++) { friend.Damage = 1; friend.TakeHeal(healer, 1); g.DeathProcessingAndAuraUpdate(); }
        Check(one.Zone.Type == Zone.GRAVEYARD, "a 1-health enemy minion dies to the ping");
        opp.Hero.Damage = 0;
        // two Shadowboxers: two pings per heal
        Park(opp);
        var twin = Summon(p, id);
        friend.Damage = 1;
        friend.TakeHeal(healer, 1);
        Check(opp.Hero.Damage == 2, "each Shadowboxer pings once (only the hero to hit): " + opp.Hero.Damage);
        opp.Hero.Damage = 0;
        // Silence and its own heal
        twin.Silence(); box.Silence();
        friend.Damage = 1; friend.TakeHeal(healer, 1);
        Check(opp.Hero.Damage == 0, "Silenced Shadowboxers do nothing");
        Resume(g, next => Check(Get(next, box).Zone.Type == Zone.PLAY, "clone keeps the board"));
        // the opponent's Shadowboxer pings MY side
        End(g);
        Park(p); Park(opp);
        var theirs = Summon(opp, id); var mine = Summon(p, "CS2_182"); var their2 = Summon(opp, "CS2_182");
        their2.Damage = 2;
        their2.TakeHeal(Add(opp, "EX1_011"), 2);
        Check(mine.Damage + p.Hero.Damage == 1 && opp.Hero.Damage == 0 && theirs.Damage == 0, "the opponent's Shadowboxer hits my side only");
    }

    // ---- GVG_073 Cobra Shot -----------------------------------------------------------------------------------------

    private static void CobraShot(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var spell = Add(p, id);
        Check(spell.Cost == 4, "Cobra Shot costs 4 in 37.0");
        Check(!Playable(p, spell), "needs a minion target");
        var enemy = Summon(opp, "CS2_182"); var friend = Summon(p, "CS2_182");
        Check(Targetable(p, spell, enemy) && Targetable(p, spell, friend) && !Targetable(p, spell, opp.Hero) && !Targetable(p, spell, p.Hero), "any minion, never a hero");
        PlayEntity(g, p, spell, enemy);
        Check(enemy.Damage == 3 && opp.Hero.Damage == 3 && p.Hero.Damage == 0, "3 to the minion and 3 to the enemy hero");
        var second = Add(p, id);
        PlayEntity(g, p, second, friend);
        Check(friend.Damage == 3 && opp.Hero.Damage == 6 && p.Hero.Damage == 0, "aimed at a friendly minion it still hits the ENEMY hero");
        var shielded = Summon(opp, "CS2_182"); shielded.HasDivineShield = true;
        var third = Add(p, id); Check(Targetable(p, third, shielded), "shielded minion is a target");
        PlayEntity(g, p, third, shielded);
        Check(!shielded.HasDivineShield && shielded.Damage == 0 && opp.Hero.Damage == 9, "a Divine Shield eats the minion's damage, the hero still takes 3");
        var geomancer = Summon(p, "CS2_142");
        var fourth = Add(p, id); Check(Targetable(p, fourth, enemy), "enemy is a target: dmg " + enemy.Damage + " zone " + enemy.Zone.Type);
        PlayEntity(g, p, fourth, enemy);
        Check(enemy.Zone.Type == Zone.GRAVEYARD && opp.Hero.Damage == 13, "Spell Damage +1 raises both hits to 4; a dying target does not cancel the hero hit");
        Kill(g, geomancer);
        Resume(g, next => Check(Get(next, opp).Hero.Damage == 13, "clone keeps the damage"));
        End(g);
        var mine = Summon(p, "CS2_182");
        PlayEntity(g, opp, Add(opp, id), mine);
        Check(mine.Damage == 3 && p.Hero.Damage == 3 && opp.Hero.Damage == 13, "the opponent's Cobra Shot hits MY hero");
    }

    // ---- GVG_074 Kezan Mystic ---------------------------------------------------------------------------------------

    private static Spell Secret(Game g, Controller owner, string id)
    {
        var secret = (Spell)Add(owner, id);
        PlayEntity(g, owner, secret);
        Check(secret.Zone.Type == Zone.SECRET && owner.SecretZone.Contains(secret), "secret fixture " + id + " zone " + secret.Zone?.Type + " count " + owner.SecretZone.Count + " hand " + owner.HandZone.Count);
        return secret;
    }

    private static void KezanMystic(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 4 && ((Minion)card).AttackDamage == 4 && ((Minion)card).Health == 3, "a 4/3 for 4");
        Check(Playable(p, card), "playable with no enemy Secret");
        PlayEntity(g, p, card);
        Check(p.SecretZone.Count == 0 && opp.SecretZone.Count == 0, "no Secret, no effect");
        // take the only Secret; it then works for me
        End(g);
        var trap = Secret(g, opp, "EX1_610");
        End(g);
        var mystic = (Minion)Play(g, p, id);
        Check(opp.SecretZone.Count == 0 && p.SecretZone.Count == 1 && p.SecretZone.First().Card.Id == "EX1_610" && p.SecretZone.First().Controller == p
            && opp.Hero.Damage == 0, "the enemy Secret is now mine");
        Check(p.SecretZone.First().Zone == p.SecretZone, "its zone is my Secret zone");
        var stolen = p.SecretZone.First();
        Resume(g, next => Check(Get(next, p).SecretZone.Count == 1 && Get(next, opp).SecretZone.Count == 0, "clone keeps the transfer"));
        // the old owner no longer has it: my attack is not punished
        var ownMinion = Summon(p, "CS2_182"); var enemyMinion = Summon(opp, "CS2_182");
        Strike(g, ownMinion, opp.Hero);
        Check(ownMinion.Damage == 0 && mystic.Damage == 0 && opp.Hero.Damage == 4 && p.Hero.Damage == 0, "the former owner has no trap any more: " + ownMinion.Damage + "/" + mystic.Damage + "/" + opp.Hero.Damage + "/" + p.Hero.Damage + " dead=" + (ownMinion.Zone?.Type));
        End(g);
        // the stolen Explosive Trap fires for me when the enemy attacks my hero
        enemyMinion.IsExhausted = false;
        var opp2 = Summon(opp, "CS2_182");
        Attack(g, enemyMinion, p.Hero);
        Check(stolen.Zone.Type != Zone.SECRET && p.SecretZone.Count == 0, "the stolen Secret triggered for its new owner");
        Check(opp.Hero.Damage == 6 && opp2.Damage == 2 && enemyMinion.Damage == 2 && p.Hero.Damage == 4, "Explosive Trap hit the enemy side: hero " + opp.Hero.Damage + " minion " + opp2.Damage);
        Check(mystic.Damage == 0 && ownMinion.Damage == 0, "my side was not hit by my own trap");
        // randomness among three Secrets, each stolen at some seed; duplicates and a full area are skipped
        var stolenIds = new HashSet<string>();
        for (int seed = 1; seed <= 24; seed++)
        {
            var f = Fresh(seed); End(f);
            Secret(f, f.Player2, "EX1_610"); Secret(f, f.Player2, "EX1_289"); Secret(f, f.Player2, "EX1_287");
            End(f);
            Play(f, f.Player1, id);
            Check(f.Player1.SecretZone.Count == 1 && f.Player2.SecretZone.Count == 2, "exactly one of three Secrets moved");
            stolenIds.Add(f.Player1.SecretZone.First().Card.Id);
        }
        Check(stolenIds.Count == 3, "each of the three Secrets can be taken: " + string.Join(",", stolenIds.OrderBy(x => x)));
        {
            var f = Fresh(9); Secret(f, f.Player1, "EX1_289"); End(f);
            Secret(f, f.Player2, "EX1_289"); End(f);
            Play(f, f.Player1, id);
            Check(f.Player1.SecretZone.Count == 1 && f.Player2.SecretZone.Count == 1, "a Secret I already hold cannot be taken twice");
            var f2 = Fresh(9); Secret(f2, f2.Player1, "EX1_289"); End(f2);
            Secret(f2, f2.Player2, "EX1_289"); Secret(f2, f2.Player2, "EX1_610"); End(f2);
            Play(f2, f2.Player1, id);
            Check(f2.Player1.SecretZone.Count == 2 && f2.Player1.SecretZone.Any(s => s.Card.Id == "EX1_610") && f2.Player2.SecretZone.Single().Card.Id == "EX1_289",
                "with a duplicate and a different Secret the different one is taken");
        }
        {
            var f = Fresh(4);
            foreach (var sid in new[] { "EX1_610", "EX1_289", "EX1_132", "EX1_554", "EX1_594" }) { Secret(f, f.Player1, sid); }
            End(f);
            Secret(f, f.Player2, "EX1_611"); End(f);
            Check(f.Player1.SecretZone.IsFull, "full Secret area fixture");
            Play(f, f.Player1, id);
            Check(f.Player1.SecretZone.Count == 5 && f.Player2.SecretZone.Count == 1, "no room: the enemy keeps its Secret");
        }
        // the opponent's Kezan Mystic takes MY Secret
        End(g);
        var mine = Secret(g, p, "EX1_289");
        End(g);
        Play(g, opp, id);
        Check(p.SecretZone.Count == 0 && opp.SecretZone.Count == 1 && opp.SecretZone.First().Card.Id == "EX1_289", "the opponent's Kezan Mystic takes my Secret");
    }

    // ---- GVG_075 Ship's Cannon --------------------------------------------------------------------------------------

    private static void ShipsCannon(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var cannon = (Minion)Play(g, p, id);
        Check(cannon.Card.Cost == 2 && cannon.AttackDamage == 2 && cannon.Health == 3, "a 2/3 for 2");
        // only the hero as a candidate
        Play(g, p, "CS2_146");
        Check(opp.Hero.Damage == 2 && p.Hero.Damage == 0, "a Pirate played: 2 damage to the only enemy, the hero");
        opp.Hero.Damage = 0;
        Play(g, p, "CS2_182");
        Summon(p, "CS2_231");
        Check(opp.Hero.Damage == 0, "non-Pirates do nothing");
        Summon(p, "CS2_146");
        Check(opp.Hero.Damage == 2, "a summoned Pirate triggers too");
        opp.Hero.Damage = 0;
        Summon(opp, "CS2_146");
        Check(opp.Hero.Damage == 0 && p.Hero.Damage == 0, "an enemy Pirate does nothing");
        Summon(p, id);
        Check(opp.Hero.Damage == 0, "another Ship's Cannon is not a Pirate");
        Park(p); Park(opp);
        var c1 = Summon(p, id); var c2 = Summon(p, id);
        Play(g, p, "CS2_146");
        Check(opp.Hero.Damage == 4, "two Cannons: 2 damage each: " + opp.Hero.Damage);
        opp.Hero.Damage = 0;
        // randomness among three enemy minions and the hero
        Park(p); Park(opp);
        var cannon2 = Summon(p, id);
        var e1 = Summon(opp, "CS2_182"); var e2 = Summon(opp, "CS2_182"); var e3 = Summon(opp, "CS2_182");
        var hits = new int[4];
        for (int i = 0; i < 80; i++)
        {
            Summon(p, "CS2_146"); Park2(p, cannon2);
            var victims = new ICharacter[] { e1, e2, e3, opp.Hero };
            Check(victims.Sum(v => v.Damage) == 2, "exactly one enemy takes 2");
            for (int k = 0; k < 4; k++) if (victims[k].Damage > 0) { hits[k]++; victims[k].Damage = 0; }
        }
        Check(hits.All(x => x >= 8) && hits.Sum() == 80, "every enemy character is a candidate: " + string.Join("/", hits));
        // lethal on a small minion, and a full board of mine
        Park(opp);
        var imp = Summon(opp, "CS2_231");
        for (int i = 0; i < 12 && imp.Zone.Type == Zone.PLAY; i++) { Summon(p, "CS2_146"); Park2(p, cannon2); }
        Check(imp.Zone.Type == Zone.GRAVEYARD, "the Cannon can kill an enemy minion");
        Fill(p);
        Check(p.BoardZone.IsFull, "full board fixture");
        opp.Hero.Damage = 0; Park(opp);
        var extra = Add(p, "CS2_146");
        Check(!Playable(p, extra) && opp.Hero.Damage == 0, "no room, no Pirate, no trigger");
        Park(p);
        // Silence
        var silenced = Summon(p, id); silenced.Silence();
        var third = Summon(p, id);
        third.Silence();
        Park(p); var lone = Summon(p, id); lone.Silence();
        Summon(p, "CS2_146");
        Check(opp.Hero.Damage == 0, "a Silenced Cannon does nothing");
        Resume(g, next => Check(Get(next, lone).Zone.Type == Zone.PLAY, "clone keeps the board"));
        // the opponent's Cannon
        End(g);
        Park(p); Park(opp);
        var theirs = Summon(opp, id);
        Play(g, opp, "CS2_146");
        Check(p.Hero.Damage == 2 && opp.Hero.Damage == 0, "the opponent's Cannon hits my side");
        p.Hero.Damage = 0;
        Summon(p, "CS2_146");
        Check(p.Hero.Damage == 0, "and does not react to my Pirates");
    }

    /// <summary>Removes the freshly summoned Pirates (everything but the Cannon) so the board never fills during the loops.</summary>
    private static void Park2(Controller c, Minion keep)
    {
        foreach (var m in c.BoardZone.ToArray()) if (m != keep && m.Card.Id != "GVG_075") c.SetasideZone.Add(c.BoardZone.Remove(m));
        c.Game.DeathProcessingAndAuraUpdate();
    }

    // ---- unregistered scenarios (BLOCKED-STANDARD) ------------------------------------------------------------------

    private static void RunUnregistered()
    {
        foreach (var (id, scenario) in new (string, Action<Game, Controller, string>)[] {
            ("GVG_058", ShieldedMinibot), ("GVG_059", Coghammer), ("GVG_061", MusterForBattle), ("GVG_062", CobaltGuardian), ("GVG_070", SaltyDog) })
            for (int seed = 1; seed <= 3; seed++)
            {
                var game = Fresh(seed);
                scenario(game, game.Player1, id);
                Check(Canonical.State(game) == Canonical.State(game.CloneExact(history: true)), "exact clone after unregistered scenario " + id);
                Check(Canonical.State(game) == Canonical.State(Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools)), "snapshot after unregistered scenario " + id);
            }
        Console.WriteLine("UNREGISTERED scenarios GVG_058, GVG_059, GVG_061, GVG_062, GVG_070 PASS; seeds=3; clone/snapshot");
    }

    private static void ShieldedMinibot(Game g, Controller p, string id)
    {
        var m = (Minion)Play(g, p, id);
        Check(m.Card.Cost == 2 && m.AttackDamage == 2 && m.Health == 2 && m.IsRace(Race.MECHANICAL) && m.HasDivineShield, "a 2/2 Mech with Divine Shield for 2");
        var enemy = Summon(p.Opponent, "CS2_182");
        m.TakeDamage(enemy, 4);
        Check(!m.HasDivineShield && m.Damage == 0, "the shield absorbs one hit");
        m.TakeDamage(enemy, 1);
        Check(m.Damage == 1, "then it takes damage");
        var other = Summon(p, id); other.Silence();
        Check(!other.HasDivineShield, "Silence removes the shield");
    }

    private static void Coghammer(Game g, Controller p, string id)
    {
        var enemy = Summon(p.Opponent, "CS2_182");
        var weapon = (Weapon)Play(g, p, id);
        Check(weapon.Card.Cost == 3 && weapon.AttackDamage == 2 && weapon.Durability == 3 && !enemy.HasTaunt && !enemy.HasDivineShield, "a 2/3 weapon; with no friendly minion nothing else happens");
        var a = Summon(p, "CS2_231"); var b = Summon(p, "CS2_231"); var c = Summon(p, "CS2_231");
        var chosen = new HashSet<int>();
        for (int seed = 1; seed <= 12; seed++)
        {
            var f = Fresh(seed);
            var x = Summon(f.Player1, "CS2_231"); var y = Summon(f.Player1, "CS2_231"); var z = Summon(f.Player1, "CS2_231");
            Play(f, f.Player1, id);
            var all = new[] { x, y, z };
            Check(all.Count(m => m.HasTaunt && m.HasDivineShield) == 1 && all.Count(m => !m.HasTaunt && !m.HasDivineShield) == 2, "one random friendly minion gets both");
            chosen.Add(Array.FindIndex(all, m => m.HasTaunt));
        }
        Check(chosen.Count == 3, "each of three friendly minions can be chosen");
        Play(g, p, id);
        var lucky = new[] { a, b, c }.Single(m => m.HasTaunt);
        lucky.Silence();
        Check(!lucky.HasTaunt && !lucky.HasDivineShield, "Silence removes both");
    }

    private static void MusterForBattle(Game g, Controller p, string id)
    {
        var spell = Add(p, id);
        Check(spell.Cost == 3, "costs 3");
        PlayEntity(g, p, spell);
        Check(p.BoardZone.Count == 3 && p.BoardZone.All(m => m.Card.Id == "CS2_101t" && m.AttackDamage == 1 && m.Health == 1), "three 1/1 Silver Hand Recruits");
        Check(p.Hero.Weapon != null && p.Hero.Weapon.AttackDamage == 1 && p.Hero.Weapon.Durability == 4, "and a 1/4 weapon");
        Equip(p);
        Play(g, p, id);
        Check(p.BoardZone.Count == 6 && p.Hero.Weapon.AttackDamage == 1 && p.Hero.Weapon.Durability == 4, "an old weapon is replaced");
        Play(g, p, id);
        Check(p.BoardZone.Count == 7, "only one more fits on the board; the weapon is still equipped");
        Park(p);
        Fill(p);
        Play(g, p, id);
        Check(p.BoardZone.Count == 7 && p.Hero.Weapon.Durability == 4, "a full board still equips the weapon");
    }

    private static void CobaltGuardian(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var guardian = (Minion)Play(g, p, id);
        Check(guardian.Card.Cost == 5 && guardian.AttackDamage == 6 && guardian.Health == 3 && guardian.IsRace(Race.MECHANICAL) && !guardian.HasDivineShield, "a 6/3 Mech for 5");
        Play(g, p, "CS2_182");
        Check(!guardian.HasDivineShield, "a non-Mech does nothing");
        Summon(opp, "GVG_085");
        Check(!guardian.HasDivineShield, "an enemy Mech does nothing");
        Play(g, p, "GVG_085");
        Check(guardian.HasDivineShield, "a friendly Mech played: Divine Shield");
        guardian.TakeDamage(opp.BoardZone[0], 1);
        Check(!guardian.HasDivineShield && guardian.Damage == 0, "the shield pops");
        Summon(p, "GVG_085");
        Check(guardian.HasDivineShield, "a summoned Mech renews it");
        guardian.Silence();
        guardian.HasDivineShield = false;
        Summon(p, "GVG_085");
        Check(!guardian.HasDivineShield, "Silence removes the trigger");
        var second = (Minion)Play(g, p, id);
        Check(!second.HasDivineShield, "a Cobalt Guardian does not shield itself when it is summoned");
    }

    private static void SaltyDog(Game g, Controller p, string id)
    {
        var m = (Minion)Play(g, p, id);
        Check(m.Card.Cost == 5 && m.AttackDamage == 7 && m.Health == 4 && m.IsRace(Race.PIRATE) && string.IsNullOrEmpty(m.Card.Text), "a vanilla 7/4 Pirate for 5");
        var enemy = Summon(p.Opponent, "CS2_182");
        End(g); End(g);
        Strike(g, m, enemy);
        Check(enemy.Zone.Type == Zone.GRAVEYARD && m.Damage == 4 && m.Zone.Type == Zone.GRAVEYARD, "trades with a Yeti as a 7/4");
    }
}
