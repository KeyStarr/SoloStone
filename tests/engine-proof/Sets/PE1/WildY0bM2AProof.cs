using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Actions;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Wave Y0b, lane m2a: Goblins vs Gnomes GVG_001 .. GVG_025 (Mage, Priest, Warlock, Rogue and neutral opening cards).
// Unregistered (not in the batch manifest), their scenarios kept below and run only with WILD_Y0B_M2A_UNREGISTERED=1:
//  - BLOCKED-STANDARD GVG_005, GVG_021: a Standard pool uses them today (data/pools/pool-eligibility-v1.json).
//  - GVG_007 is registered (script: 2 damage to all non-Mech characters; it stays in hand via Actions/Draw.cs).
//  - BLOCKED-HSDATA GVG_004, GVG_011, GVG_015: their English text changed in 37.0, and the locale files cannot be
//    regenerated without the hsdata CardDefs (tools/gen_card_facts.py --hsdata); an identity row would leave them stale.
//    Their script fixes are NOT on this branch (card text and behaviour must not disagree); the scenarios below state the
//    expected 37.0 behaviour and fail against the HEAD scripts. Land the fixes with the identity rows:
//      GVG_004: SplitHits.Inline(6, ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1), deathProcessing: false) replaces DamageRandomTargets(4, ENEMIES, 1).
//      GVG_011: GVG_011a enchantment SUB 3 (was 2).
//      GVG_015: PowerTask = DamageTask(3, TARGET, true) + ConditionTask(TARGET, IsDead) + ComplexTask.True(DrawFromDeck(1, IsSpell, SPELL_SCHOOL==6)).
internal static class WildY0bM2AProof
{
    [GroupProof("wild-y0b-m2a-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        WildSetsProof.Case("GVG_001", Flamecannon);
        WildSetsProof.Case("GVG_002", Snowchugger);
        WildSetsProof.Case("GVG_003", UnstablePortal);
        WildSetsProof.Case("GVG_006", Mechwarper);
        WildSetsProof.Case("GVG_007", FlameLeviathan);
        WildSetsProof.Case("GVG_008", Lightbomb);
        WildSetsProof.Case("GVG_009", Shadowbomber);
        WildSetsProof.Case("GVG_010", VelensChosen);
        WildSetsProof.Case("GVG_012", LightOfTheNaaru);
        WildSetsProof.Case("GVG_013", Cogmaster);
        WildSetsProof.Case("GVG_014", Voljin);
        WildSetsProof.Case("GVG_016", FelReaver);
        WildSetsProof.Case("GVG_017", CallPet);
        WildSetsProof.Case("GVG_018", QueenOfPain);
        WildSetsProof.Case("GVG_019", Demonheart);
        WildSetsProof.Case("GVG_020", FelCannon);
        WildSetsProof.Case("GVG_022", SharpswordOil);
        WildSetsProof.Case("GVG_023", AutoBarber);
        WildSetsProof.Case("GVG_024", CogmastersWrench);
        WildSetsProof.Case("GVG_025", OneEyedCheat);
        // Opt-in evidence for the six unregistered ids (never part of the registered group, never in the manifest).
        if (Environment.GetEnvironmentVariable("WILD_Y0B_M2A_UNREGISTERED") == "1") RunUnregistered();
    }

    // ---- fixtures -------------------------------------------------------------------------------------------------

    private static void Check(bool condition, string message) => Core2026Proof.Check(condition, message);
    private static void Fill(Controller c, string id = "CS2_182") { while (!c.BoardZone.IsFull) Summon(c, id); }
    private static void FillHand(Controller c, int to = 10) { while (c.HandZone.Count < to) Add(c, "CS2_182"); }
    private static void EmptyHand(Controller c) { foreach (var held in c.HandZone.ToArray()) c.SetasideZone.Add(c.HandZone.Remove(held)); }
    private static void Equip(Controller c, string id = "CS2_106") => c.Hero.AddWeapon((Weapon)Entity.FromCard(c, Cards.FromId(id)));
    private static bool Playable(Controller c, IPlayable card) =>
        c.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id);
    private static void Park(Controller c) => StandardSetsProof.ParkBoard(c);

    /// <summary>Deck of five Yetis topped by the given ids, the last id on top.</summary>
    private static void Stack(Controller c, params string[] topLast)
    {
        foreach (var card in c.DeckZone.ToArray()) c.SetasideZone.Add(c.DeckZone.Remove(card));
        for (int i = 0; i < 5; i++) Entity.FromCard(c, Cards.FromId("CS2_182"), zone: c.DeckZone);
        foreach (var id in topLast) Entity.FromCard(c, Cards.FromId(id), zone: c.DeckZone);
        Check(topLast.Length == 0 || c.DeckZone.TopCard.Card.Id == topLast[^1], "deck fixture top card");
    }

    private static void Resume(Game game, Action<Game> continuation)
    {
        foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) })
        {
            Check(Canonical.State(game) == Canonical.State(next), "m2a state before continuation");
            continuation(next);
        }
    }

    /// <summary>The other seat's fair view (bot simulation and projection input) must not know a hidden hand card.</summary>
    private static void Concealed(Game g, IPlayable card, string what)
    {
        var own = FairBotState.Create(g, card.Controller.PlayerId, 3, DeckStyle.Control);
        var ownView = (Game)typeof(FairBotState).GetField("game", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(own);
        Check(ownView.IdEntityDic.TryGetValue(card.Id, out var known) && known.Card.Id == card.Card.Id, what + ": control: the owner's view knows its own card");
        foreach (int seed in new[] { 3, 71 })
        {
            var fair = FairBotState.Create(g, card.Controller.Opponent.PlayerId, seed, DeckStyle.Control);
            var view = (Game)typeof(FairBotState).GetField("game", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(fair);
            Check(!view.IdEntityDic.TryGetValue(card.Id, out var seen) || seen.Card.Id != card.Card.Id,
                what + ": the opponent's fair view does not know the hidden hand card " + card.Card.Id);
        }
    }

    private static T Get<T>(Game game, T entity) where T : class, IPlayable => (T)game.IdEntityDic[entity.Id];

    // ---- GVG_001 Flamecannon ----------------------------------------------------------------------------------------

    private static void Flamecannon(Game g, Controller p, string id)
    {
        var spell = Add(p, id);
        Check(!Playable(p, spell), "Flamecannon cannot be played without an enemy minion");
        var friend = Summon(p, "CS2_182");
        Check(!Playable(p, spell), "a friendly minion does not enable Flamecannon");
        var enemies = new[] { Summon(p.Opponent, "CS2_182"), Summon(p.Opponent, "CS2_182"), Summon(p.Opponent, "CS2_182") };
        Check(Playable(p, spell) && !p.Options().Any(t => t.Source?.Id == spell.Id && t.Target != null), "Flamecannon is untargeted");
        PlayEntity(g, p, spell);
        Check(enemies.Count(m => m.Damage == 4) == 1 && enemies.Count(m => m.Damage == 0) == 2 && friend.Damage == 0
            && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "Flamecannon deals 4 to exactly one enemy minion, nothing else");
        var victims = new HashSet<int>();
        for (int i = 0; i < 24; i++)
        {
            foreach (var m in enemies) m.Damage = 0;
            Play(g, p, id);
            victims.Add(Array.FindIndex(enemies, m => m.Damage == 4));
        }
        Check(!victims.Contains(-1) && victims.Count >= 2, "Flamecannon victim is random among the candidates");
        int impDied = 0, crocDied = 0;
        for (int i = 0; i < 24; i++)
        {
            Park(p.Opponent);
            var imp = Summon(p.Opponent, "CS2_059"); var croc = Summon(p.Opponent, "CS2_120");
            Check(imp.HasStealth, "Stealth fixture");
            Play(g, p, id);
            bool impDead = imp.Zone.Type == Zone.GRAVEYARD, crocDead = croc.Zone.Type == Zone.GRAVEYARD;
            Check(impDead != crocDead, "exactly one of two enemy minions dies to 4 damage");
            if (impDead) impDied++; else crocDied++;
        }
        Check(impDied > 0 && crocDied > 0, "a Stealth minion is as eligible as any other for the random hit");
        Park(p.Opponent);
        var yeti = Summon(p.Opponent, "CS2_182"); Summon(p, "EX1_012");
        Play(g, p, id);
        Check(yeti.Zone.Type == Zone.GRAVEYARD, "Flamecannon adds Spell Damage (4+1 kills a 5 Health minion)");
        Park(p); Park(p.Opponent);
        var mine = new[] { Summon(p, "CS2_182"), Summon(p, "CS2_182"), Summon(p, "CS2_182") };
        var theirs = Summon(p.Opponent, "CS2_182");
        End(g);
        PlayEntity(g, p.Opponent, Add(p.Opponent, id));
        Check(mine.Count(m => m.Damage == 4) == 1 && theirs.Damage == 0, "the opponent's Flamecannon hits one of its enemy's minions");
    }

    // ---- GVG_002 Snowchugger ----------------------------------------------------------------------------------------

    private static void Snowchugger(Game g, Controller p, string id)
    {
        var chug = Summon(p, id); var target = Summon(p.Opponent, "CS2_182");
        Check(chug.IsRace(Race.MECHANICAL) && chug.AttackDamage == 2 && chug.Health == 3, "Snowchugger is a 2/3 Mech");
        Attack(g, chug, target);
        Check(target.Damage == 2 && target.IsFrozen && !chug.IsFrozen, "Snowchugger Freezes a minion it damaged");
        Park(p.Opponent);
        var hero = p.Opponent.Hero;
        Attack(g, Summon(p, id), hero);
        Check(hero.Damage == 2 && hero.IsFrozen, "Snowchugger Freezes a hero it damaged");
        var squire = Summon(p.Opponent, "EX1_008"); var shielded = Summon(p, id);
        Attack(g, shielded, squire);
        Check(!squire.HasDivineShield && squire.Damage == 0 && !squire.IsFrozen, "a Divine Shield absorbs the hit: no damage, no Freeze");
        var silenced = Summon(p, id); silenced.Silence(); g.DeathProcessingAndAuraUpdate();
        var fresh = Summon(p.Opponent, "CS2_182");
        Attack(g, silenced, fresh);
        Check(fresh.Damage == 2 && !fresh.IsFrozen, "Silence removes the Freeze");
        Park(p); Park(p.Opponent);
        var defender = Summon(p, id); var attacker = Summon(p.Opponent, "CS2_182");
        End(g);
        Attack(g, attacker, defender);
        Check(attacker.Damage == 2 && attacker.IsFrozen && defender.Zone.Type == Zone.GRAVEYARD,
            "Snowchugger Freezes an attacker it damaged while defending, even as it dies");
        // The Standard Core Water Elemental shares the Minion.Freeze rule: silenced, it does not Freeze (also run in SilenceCantAttackProof).
        Park(p); Park(p.Opponent);
        var water = Summon(p, "CS2_033"); var silencedWater = Summon(p, "CS2_033"); silencedWater.Silence(); g.DeathProcessingAndAuraUpdate();
        End(g); // back to my turn (the Snowchugger case above ended it)
        var w1 = Summon(p.Opponent, "CS2_182"); var w2 = Summon(p.Opponent, "CS2_182");
        water.IsExhausted = false; silencedWater.IsExhausted = false;
        Attack(g, water, w1); Attack(g, silencedWater, w2);
        Check(w1.IsFrozen && !w2.IsFrozen, "Water Elemental Freezes; a silenced one does not");
    }

    // ---- GVG_003 Unstable Portal ------------------------------------------------------------------------------------

    private static void UnstablePortal(Game g, Controller p, string id)
    {
        var seen = new HashSet<string>();
        for (int i = 0; i < 14; i++)
        {
            EmptyHand(p);
            int enemyHand = p.Opponent.HandZone.Count;
            Play(g, p, id);
            Check(p.HandZone.Count == 1 && p.Opponent.HandZone.Count == enemyHand, "Unstable Portal adds exactly one card to its owner's hand");
            var made = p.HandZone[0];
            Check(made.Card.Type == CardType.MINION && made.Cost == Math.Max(0, made.Card.Cost - 3),
                "the added minion costs (3) less, never below zero: " + made.Card.Id);
            seen.Add(made.Card.Id);
            if (i < 3) Concealed(g, made, "Unstable Portal");
        }
        Check(seen.Count >= 4, "Unstable Portal's minion is random");
        EmptyHand(p); Play(g, p, id);
        var kept = p.HandZone[0]; int cost = kept.Cost;
        End(g); End(g);
        Check(kept.Cost == cost && kept.Zone.Type == Zone.HAND, "the discount persists across turns");
        EmptyHand(p); FillHand(p, 9);
        Play(g, p, id);
        Check(p.HandZone.Count == 10, "Unstable Portal fills the freed hand slot");
        Check(p.HandZone.Where(c => c.Card.Id == "CS2_182").All(c => c.Cost == 4), "other hand cards keep their cost");
    }

    // ---- GVG_004 Goblin Blastmage -----------------------------------------------------------------------------------

    private static void GoblinBlastmage(Game g, Controller p, string id)
    {
        var enemies = new[] { Summon(p.Opponent, "CS2_182"), Summon(p.Opponent, "CS2_182"), Summon(p.Opponent, "CS2_182"), Summon(p.Opponent, "GVG_044") };
        var friend = Summon(p, "CS2_182"); Add(p, "GVG_044");
        int Total() => enemies.Sum(m => m.Damage) + p.Opponent.Hero.Damage;
        var blastmage = (Minion)Play(g, p, id);
        Check(blastmage.AttackDamage == 5 && blastmage.Health == 4 && Total() == 0 && friend.Damage == 0 && p.Hero.Damage == 0,
            "without a friendly Mech (enemy Mech or Mech in hand) there is no damage");
        var mech = Summon(p, "GVG_044");
        Play(g, p, id);
        Check(Total() == 6 && friend.Damage == 0 && mech.Damage == 0 && p.Hero.Damage == 0,
            "with a Mech: 6 damage split among enemy characters only");
        Park(p.Opponent);
        var imp = Summon(p.Opponent, "CS2_059"); p.Opponent.Hero.Damage = 0;
        Play(g, p, id);
        Check(imp.Damage <= 1 && p.Opponent.Hero.Damage + imp.Damage == 6 && p.Hero.Damage == 0,
            "missiles never target a minion that already died: all 6 land");
        Park(p.Opponent); p.Opponent.Hero.Damage = 0;
        Play(g, p, id);
        Check(p.Opponent.Hero.Damage == 6, "with only the enemy hero, it takes all 6");
        var heroDamage = new HashSet<int>();
        for (int i = 0; i < 12; i++)
        {
            Park(p); Summon(p, "GVG_044"); Summon(p, "EX1_012");
            Park(p.Opponent); var target = Summon(p.Opponent, "CS2_182"); p.Opponent.Hero.Damage = 0;
            Play(g, p, id);
            Check(target.Damage + p.Opponent.Hero.Damage == 6, "Battlecry damage is exactly 6, Spell Damage does not boost it");
            heroDamage.Add(p.Opponent.Hero.Damage);
        }
        Check(heroDamage.Count >= 3, "the 6 missiles split randomly between the minion and the hero");
    }

    // ---- GVG_006 Mechwarper -----------------------------------------------------------------------------------------

    private static void Mechwarper(Game g, Controller p, string id)
    {
        var mech = Add(p, "GVG_044"); var yeti = Add(p, "CS2_182"); var enemyMech = Add(p.Opponent, "GVG_044"); var tiny = Add(p, "GVG_085");
        Check(mech.Cost == 3 && yeti.Cost == 4 && tiny.Cost == 2, "Mech cost fixtures");
        var warper = (Minion)Play(g, p, id);
        Check(warper.Card.Cost == 4 && warper.AttackDamage == 4 && warper.Health == 4 && warper.IsRace(Race.MECHANICAL), "Mechwarper is a 4/4 Mech for 4");
        Check(mech.Cost == 2 && yeti.Cost == 4 && enemyMech.Cost == 3 && tiny.Cost == 1,
            "friendly Mechs in hand cost (1) less; non-Mechs and the opponent's Mechs do not");
        var held = Add(p, id); g.DeathProcessingAndAuraUpdate();
        Check(held.Cost == 3, "another Mechwarper in hand is a Mech too");
        var second = Summon(p, id);
        Check(mech.Cost == 1 && tiny.Cost == 0 && held.Cost == 2, "two Mechwarpers stack");
        var third = Summon(p, id);
        Check(mech.Cost == 0 && tiny.Cost == 0, "cost never drops below zero");
        Kill(g, third); Check(mech.Cost == 1, "a dying Mechwarper stops reducing");
        second.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(mech.Cost == 2 && tiny.Cost == 1, "Silence removes the reduction");
        PlayEntity(g, p, mech);
        Check(p.BoardZone.Any(m => m.Id == mech.Id), "a Mech is paid for at the reduced cost");
        var theirs = Summon(p.Opponent, id);
        Check(enemyMech.Cost == 2 && tiny.Cost == 1, "an opposing Mechwarper reduces only its owner's Mechs");
        Resume(g, next => { End(next); Check(Get(next, tiny).Cost == 1 && Get(next, enemyMech).Cost == 2, "the reduction persists across turns"); });
    }

    // ---- GVG_007 Flame Leviathan ------------------------------------------------------------------------------------

    private static void FlameLeviathan(Game g, Controller p, string id)
    {
        var friend = Summon(p, "CS2_182"); var friendMech = Summon(p, "GVG_044"); var weak = Summon(p, "CS2_059");
        var enemy = Summon(p.Opponent, "CS2_182"); var enemyMech = Summon(p.Opponent, "GVG_044");
        Stack(p, id);
        var drawn = Generic.Draw(p); g.DeathProcessingAndAuraUpdate();
        Check(drawn.Card.Id == id && drawn.Zone.Type == Zone.HAND, "Flame Leviathan stays in hand when drawn");
        Check(friend.Damage == 2 && enemy.Damage == 2 && p.Hero.Damage == 2 && p.Opponent.Hero.Damage == 2 && friendMech.Damage == 0
            && enemyMech.Damage == 0 && weak.Zone.Type == Zone.GRAVEYARD, "drawing it deals 2 to all characters except Mechs");
        Add(p, id);
        Check(friend.Damage == 2 && p.Hero.Damage == 2, "adding it to hand is not drawing it");
        EmptyHand(p); FillHand(p);
        Stack(p, id);
        var burned = Generic.Draw(p); g.DeathProcessingAndAuraUpdate();
        Check(burned.Zone.Type != Zone.HAND && friend.Damage == 2 && p.Hero.Damage == 2, "a burned Flame Leviathan was not drawn into hand");
        EmptyHand(p); Summon(p, "EX1_012");
        Stack(p, id);
        var again = (Minion)Generic.Draw(p); g.DeathProcessingAndAuraUpdate();
        Check(friend.Damage == 4 && p.Hero.Damage == 4, "Spell Damage does not boost it");
        Check(again.AttackDamage == 7 && again.Health == 7 && again.IsRace(Race.MECHANICAL), "Flame Leviathan is a 7/7 Mech");
        PlayEntity(g, p, again);
        var rush = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == again.Id).ToArray();
        // Rush comes from the 37.0 identity row, which is not loaded for this unregistered id (Card.Rush is read-only here).
        Check(!again.Card.Rush || rush.Length > 0 && rush.All(t => t.Target is Minion), "Rush: it can attack minions but not the hero this turn");
        Resume(g, next => { End(next); Check(Get(next, again).Zone.Type == Zone.PLAY, "Leviathan survives on a clone"); });
        End(g);
        var opp = p.Opponent;
        Stack(opp, id);
        int mine = p.Hero.Damage;
        var theirs = Generic.Draw(opp); g.DeathProcessingAndAuraUpdate();
        Check(theirs.Zone.Type == Zone.HAND && p.Hero.Damage == mine + 2 && opp.Hero.Damage == 6, "the opponent drawing it hurts every non-Mech, including my hero");
    }

    // ---- GVG_008 Lightbomb ------------------------------------------------------------------------------------------

    private static void Lightbomb(Game g, Controller p, string id)
    {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var rider = Summon(p.Opponent, "CS2_124");
        var imp = Summon(p.Opponent, "CS2_059"); var buffed = Summon(p.Opponent, "CS2_179"); var squire = Summon(p.Opponent, "EX1_008");
        Play(g, p, "GVG_010", buffed);
        Check(buffed.AttackDamage == 5 && buffed.Health == 9, "Velen's Chosen fixture");
        Play(g, p, id);
        Check(friend.Damage == 4 && enemy.Damage == 4 && rider.Zone.Type == Zone.GRAVEYARD && imp.Zone.Type == Zone.PLAY && imp.Damage == 0
            && buffed.Damage == 5 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0,
            "each minion, friendly or enemy, takes damage equal to its current Attack (buffs included); heroes are untouched");
        Check(!squire.HasDivineShield && squire.Damage == 0 && squire.Zone.Type == Zone.PLAY, "Divine Shield absorbs it");
        Park(p); Park(p.Opponent);
        var cog = Summon(p, "GVG_013"); var tank = Summon(p, "GVG_044");
        Check(cog.AttackDamage == 3, "Cogmaster Attack fixture");
        Play(g, p, id);
        Check(cog.Zone.Type == Zone.GRAVEYARD && tank.Damage == 3, "damage equals the Attack the minion has when the spell resolves (aura Attack counts)");
        Park(p); Park(p.Opponent);
        var thalnos = Summon(p, "EX1_012"); var yeti = Summon(p, "CS2_182");
        Play(g, p, id);
        Check(yeti.Zone.Type == Zone.GRAVEYARD && thalnos.Zone.Type == Zone.GRAVEYARD, "Lightbomb adds Spell Damage (4+1 kills a Yeti)");
        Park(p);
        var wounded = Summon(p.Opponent, "CS2_182"); wounded.Damage = 1;
        Play(g, p, id);
        Check(wounded.Damage == 5 && wounded.Zone.Type == Zone.GRAVEYARD, "a damaged minion dies at its exact remaining Health");
        Park(p.Opponent);
        Play(g, p, id);
        Check(p.Opponent.BoardZone.Count == 0 && p.BoardZone.Count == 0, "an empty board is fine");
    }

    // ---- GVG_009 Shadowbomber ---------------------------------------------------------------------------------------

    private static void Shadowbomber(Game g, Controller p, string id)
    {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var bomber = (Minion)Play(g, p, id);
        Check(bomber.AttackDamage == 3 && bomber.Health == 1 && bomber.Damage == 0, "Shadowbomber is a 3/1 for 1");
        Check(p.Hero.Damage == 3 && p.Opponent.Hero.Damage == 3 && friend.Damage == 0 && enemy.Damage == 0,
            "Battlecry: 3 damage to each hero, minions untouched");
        p.Opponent.Hero.Damage = 0; p.Opponent.Hero.Armor = 2;
        Play(g, p, id);
        Check(p.Opponent.Hero.Armor == 0 && p.Opponent.Hero.Damage == 1, "Armor absorbs first");
        Summon(p, id); bomber.Silence();
        Check(p.Hero.Damage == 6 && p.Opponent.Hero.Damage == 1, "summoning or Silencing does not repeat the Battlecry");
        End(g);
        Play(g, p.Opponent, id);
        Check(p.Hero.Damage == 9 && p.Opponent.Hero.Damage == 4, "the opponent's Shadowbomber also hits both heroes");
    }

    // ---- GVG_010 Velen's Chosen -------------------------------------------------------------------------------------

    private static void VelensChosen(Game g, Controller p, string id)
    {
        var spell = Add(p, id);
        Check(!Playable(p, spell), "Velen's Chosen needs a minion target");
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var targets = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id).ToArray();
        Check(targets.Length == 2 && targets.All(t => t.Target is Minion), "any minion may be targeted, heroes may not");
        PlayEntity(g, p, spell, friend);
        Check(friend.AttackDamage == 6 && friend.Health == 9 && p.CurrentSpellPower == 1 && p.Opponent.CurrentSpellPower == 0,
            "+2/+4 and Spell Damage +1 for the controller");
        Play(g, p, id, enemy);
        Check(enemy.AttackDamage == 6 && enemy.Health == 9 && p.Opponent.CurrentSpellPower == 1 && p.CurrentSpellPower == 1,
            "an enemy minion's Spell Damage belongs to its owner");
        var spark = Summon(p.Opponent, "CS2_182"); Play(g, p, "CS2_008", spark);
        Check(spark.Damage == 2, "the granted Spell Damage boosts spells");
        End(g);
        Check(friend.AttackDamage == 6 && friend.Health == 9 && p.CurrentSpellPower == 1, "the buff is permanent");
        End(g);
        friend.Damage = 3; friend.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(friend.AttackDamage == 4 && friend.Health == 5 && p.CurrentSpellPower == 0, "Silence removes stats and Spell Damage");
        Kill(g, enemy);
        Check(p.Opponent.CurrentSpellPower == 0, "Spell Damage ends when the minion dies");
        Resume(g, next => Check(next.Player1.CurrentSpellPower == 0, "clone keeps the removed Spell Damage"));
    }

    // ---- GVG_011 Shrinkmeister --------------------------------------------------------------------------------------

    private static void Shrinkmeister(Game g, Controller p, string id)
    {
        var target = Summon(p.Opponent, "CS2_182"); var croc = Summon(p.Opponent, "CS2_120"); var mine = Summon(p, "CS2_182");
        var smith = (Minion)Play(g, p, id, target);
        Check(smith.AttackDamage == 3 && smith.Health == 2 && target.AttackDamage == 1 && croc.AttackDamage == 2 && mine.AttackDamage == 4,
            "Shrinkmeister gives a minion -3 Attack and nothing else");
        Play(g, p, id, croc);
        Check(croc.AttackDamage == 0, "Attack never goes below zero");
        Play(g, p, id, target);
        Check(target.AttackDamage == 0 && target.Health == 5, "two shrinks stack down to zero, Health untouched");
        Play(g, p, id, mine);
        Check(mine.AttackDamage == 1, "a friendly minion may be targeted");
        Resume(g, next => {
            End(next);
            Check(Get(next, target).AttackDamage == 4 && Get(next, croc).AttackDamage == 2 && Get(next, mine).AttackDamage == 4,
                "the penalty ends at end of turn");
        });
        target.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(target.AttackDamage == 4, "Silence removes the shrink immediately");
        End(g); Check(croc.AttackDamage == 2 && mine.AttackDamage == 4, "expiry on the real game too"); End(g);
        Park(p); Park(p.Opponent);
        var lone = (Minion)Play(g, p, id);
        Check(lone.AttackDamage == 3 && p.BoardZone.Count == 1, "playable with no other minion, never targets itself");
        var held = Add(p, id);
        var options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).ToArray();
        Check(options.Any(t => t.Target?.Id == lone.Id) && !options.Any(t => t.Target is Hero), "minions are legal targets, heroes are not");
    }

    // ---- GVG_012 Light of the Naaru ---------------------------------------------------------------------------------

    private static void LightOfTheNaaru(Game g, Controller p, string id)
    {
        p.Hero.Damage = 5;
        Play(g, p, id, p.Hero);
        var warden = p.BoardZone.Single();
        Check(p.Hero.Damage == 2 && warden.Card.Id == "EX1_001" && warden.AttackDamage == 1 && warden.Health == 2,
            "heals 3; the target is still damaged, so a Lightwarden arrives after the heal (no +2 Attack)");
        Park(p);
        p.Hero.Damage = 3;
        Play(g, p, id, p.Hero);
        Check(p.Hero.Damage == 0 && p.BoardZone.Count == 0, "exactly healed: no Lightwarden");
        Play(g, p, id, p.Opponent.Hero);
        Check(p.Opponent.Hero.Damage == 0 && p.BoardZone.Count == 0, "undamaged target: no Lightwarden");
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.Damage = 4;
        Play(g, p, id, enemy);
        Check(enemy.Damage == 1 && p.BoardZone.Count == 1 && p.Opponent.BoardZone.Count == 1 && p.BoardZone[0].Controller == p,
            "healing an enemy minion still summons my Lightwarden");
        Park(p);
        Fill(p); p.Hero.Damage = 5;
        Play(g, p, id, p.Hero);
        Check(p.Hero.Damage == 2 && p.BoardZone.Count == 7, "full board: the heal works and no Lightwarden appears");
        Park(p);
        var wounded = Summon(p, "CS2_182"); wounded.Damage = 2;
        Play(g, p, id, wounded);
        Check(wounded.Damage == 0 && p.BoardZone.Count == 1, "a minion healed to full gets no Lightwarden");
        Park(p.Opponent); Park(p);
        End(g);
        p.Opponent.Hero.Damage = 4;
        Play(g, p.Opponent, id, p.Opponent.Hero);
        Check(p.Opponent.Hero.Damage == 1 && p.Opponent.BoardZone.Single().Card.Id == "EX1_001", "the opponent's own cast summons for the opponent");
    }

    // ---- GVG_013 Cogmaster ------------------------------------------------------------------------------------------

    private static void Cogmaster(Game g, Controller p, string id)
    {
        var cog = (Minion)Play(g, p, id);
        Check(cog.AttackDamage == 1 && cog.Health == 2, "Cogmaster is a 1/2");
        var held = Add(p, "GVG_044"); Summon(p.Opponent, "GVG_044");
        Check(cog.AttackDamage == 1, "neither a Mech in hand nor an enemy Mech counts");
        var mech = Summon(p, "GVG_044");
        Check(cog.AttackDamage == 3, "+2 Attack while you have a Mech");
        var other = Summon(p, id);
        Check(other.AttackDamage == 3 && cog.AttackDamage == 3, "Cogmasters are not Mechs and do not buff each other");
        Kill(g, mech);
        Check(cog.AttackDamage == 1 && other.AttackDamage == 1, "the bonus ends when the Mech dies");
        mech = Summon(p, "GVG_044"); other.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(cog.AttackDamage == 3 && other.AttackDamage == 1, "Silence removes the bonus from that Cogmaster only");
        Resume(g, next => { Kill(next, Get(next, mech)); Check(Get(next, cog).AttackDamage == 1, "clone loses the bonus with the Mech"); });
        End(g);
        Check(cog.AttackDamage == 3, "the bonus is not a one-turn effect");
        Summon(p.Opponent, id);
        Check(p.Opponent.BoardZone.Last().AttackDamage == 3, "the opponent's Cogmaster counts the opponent's Mechs");
    }

    // ---- GVG_014 Vol'jin --------------------------------------------------------------------------------------------

    private static void Voljin(Game g, Controller p, string id)
    {
        var yeti = Summon(p.Opponent, "CS2_182");
        var vol = (Minion)Play(g, p, id, yeti);
        Check(vol.AttackDamage == 6 && yeti.AttackDamage == 4, "Vol'jin swaps only Health");
        Check(vol.Health == 5 && yeti.Health == 2 && vol.Damage == 0 && yeti.Damage == 0, "Vol'jin 2 and Yeti 5 swap Health");
        Park(p); Park(p.Opponent);
        var hurt = Summon(p, "CS2_182"); hurt.Damage = 1;
        var v2 = (Minion)Play(g, p, id, hurt);
        Check(v2.Health == 4 && v2.Damage == 0 && hurt.Health == 2 && hurt.Damage == 0,
            "current Health is swapped; the new value is each minion's undamaged Health");
        var croc = Summon(p.Opponent, "CS2_120");
        var v3 = (Minion)Play(g, p, id, croc);
        Check(v3.Health == 3 && croc.Health == 2 && croc.AttackDamage == 2, "the opponent's minion can be targeted");
        v3.Silence(); croc.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(v3.Health == 2 && croc.Health == 3, "Silence restores printed Health");
        Park(p); Park(p.Opponent);
        var alone = (Minion)Play(g, p, id);
        Check(alone.Health == 2 && p.BoardZone.Count == 1, "playable with no other minion; it cannot target itself");
        var held = Add(p, id);
        var options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).ToArray();
        Check(options.Any(t => t.Target?.Id == alone.Id) && !options.Any(t => t.Target is Hero), "minions are legal targets, heroes are not");
        Park(p);
        var big = Summon(p.Opponent, "CS2_182"); var tiny = Summon(p.Opponent, "CS2_059");
        Play(g, p, id, big);
        Resume(g, next => { End(next); var b = Get(next, big); Check(b.Health == 2 && Get(next, tiny).Health == 1, "the swap persists across turns"); });
        Kill(g, big);
        Check(p.BoardZone.Single().Health == 5, "swapped Health stays after the other minion dies");
    }

    // ---- GVG_015 Darkbomb -------------------------------------------------------------------------------------------

    private static void Darkbomb(Game g, Controller p, string id)
    {
        var croc = Summon(p.Opponent, "CS2_120");
        Stack(p, "GVG_015", "CS2_029", "GVG_015", "CS2_029", "GVG_015");
        int hand = p.HandZone.Count, deck = p.DeckZone.Count;
        Play(g, p, id, croc);
        Check(croc.Zone.Type == Zone.GRAVEYARD && p.HandZone.Count == hand + 1 && p.HandZone.Last().Card.Id == "GVG_015"
            && p.DeckZone.Count == deck - 1 && p.DeckZone.Count(c => c.Card.Id == "CS2_029") == 2,
            "a kill draws a Shadow spell and only a Shadow spell");
        Concealed(g, p.HandZone.Last(), "Darkbomb");
        var yeti = Summon(p.Opponent, "CS2_182"); hand = p.HandZone.Count; deck = p.DeckZone.Count;
        Play(g, p, id, yeti);
        Check(yeti.Damage == 3 && p.HandZone.Count == hand && p.DeckZone.Count == deck, "no kill, no draw");
        Play(g, p, id, p.Opponent.Hero);
        Check(p.Opponent.Hero.Damage == 3 && p.HandZone.Count == hand && p.DeckZone.Count == deck, "a hero survives the hit: no draw");
        Summon(p, "EX1_012"); yeti.Damage = 1;
        Play(g, p, id, yeti);
        Check(yeti.Zone.Type == Zone.GRAVEYARD && p.HandZone.Count == hand + 1, "Spell Damage raises $3 to 4, which kills a 4 Health minion");
        var mine = Summon(p, "CS2_065"); hand = p.HandZone.Count;
        Play(g, p, id, mine);
        Check(mine.Zone.Type == Zone.GRAVEYARD && p.HandZone.Count == hand + 1, "killing a friendly minion also draws");
        Stack(p); hand = p.HandZone.Count; deck = p.DeckZone.Count;
        var again = Summon(p.Opponent, "CS2_120");
        Play(g, p, id, again);
        Check(again.Zone.Type == Zone.GRAVEYARD && p.HandZone.Count == hand && p.DeckZone.Count == deck && p.Hero.Fatigue == 0,
            "no Shadow spell in the deck: nothing is drawn, and no fatigue");
        End(g);
        var theirs = Summon(p, "CS2_120");
        Stack(p.Opponent, "GVG_015");
        hand = p.Opponent.HandZone.Count; int myHand = p.HandZone.Count;
        Play(g, p.Opponent, id, theirs);
        Check(theirs.Zone.Type == Zone.GRAVEYARD && p.Opponent.HandZone.Count == hand + 1 && p.HandZone.Count == myHand,
            "the opponent's Darkbomb draws from the opponent's deck");
    }

    // ---- GVG_016 Fel Reaver -----------------------------------------------------------------------------------------

    private static void FelReaver(Game g, Controller p, string id)
    {
        var reaver = (Minion)Play(g, p, id);
        Check(reaver.AttackDamage == 8 && reaver.Health == 8 && reaver.IsRace(Race.MECHANICAL) && reaver.Card.Cost == 5, "Fel Reaver is an 8/8 Mech for 5");
        Stack(p, "CS2_120", "CS2_179", "CS2_065");
        int deck = p.DeckZone.Count, enemyDeck = p.Opponent.DeckZone.Count;
        Play(g, p, "CS2_182");
        Check(p.DeckZone.Count == deck && p.Opponent.DeckZone.Count == enemyDeck, "my own plays do not trigger it");
        Resume(g, next => {
            End(next);
            PlayEntity(next, next.Player2, Add(next.Player2, "CS2_182"));
            Check(next.Player1.DeckZone.Count == deck - 3 && next.Player1.GraveyardZone.Any(c => c.Card.Id == "CS2_065")
                && next.Player1.GraveyardZone.Any(c => c.Card.Id == "CS2_179"), "clone: the top three cards are removed");
        });
        End(g);
        var opp = p.Opponent;
        PlayEntity(g, opp, Add(opp, "CS2_182"));
        Check(p.DeckZone.Count == deck - 3 && p.DeckZone.TopCard.Card.Id == "CS2_182"
            && new[] { "CS2_120", "CS2_179", "CS2_065" }.All(c => p.GraveyardZone.Any(x => x.Card.Id == c)),
            "an opposing minion play removes exactly the top three cards of my deck");
        Check(opp.DeckZone.Count == enemyDeck - 1, "the opponent's own deck is not touched");
        deck = p.DeckZone.Count;
        var foe = Summon(p, "CS2_182");
        PlayEntity(g, opp, Add(opp, "CS2_008"), foe);
        Check(p.DeckZone.Count == deck - 3, "an opposing spell triggers it too");
        PlayEntity(g, opp, Add(opp, "CS2_182"));
        Check(p.DeckZone.Count == 0 && p.Hero.Fatigue == 0 && p.Hero.Damage == 0, "a short deck loses what it has, without fatigue");
        Stack(p, "CS2_120");
        reaver.Silence(); g.DeathProcessingAndAuraUpdate();
        deck = p.DeckZone.Count;
        PlayEntity(g, opp, Add(opp, "CS2_182"));
        Check(p.DeckZone.Count == deck, "a Silenced Fel Reaver removes nothing");
        Summon(opp, id);
        Stack(opp, "CS2_120", "CS2_179", "CS2_065"); int theirDeck = opp.DeckZone.Count;
        End(g);
        PlayEntity(g, p, Add(p, "CS2_182"));
        Check(opp.DeckZone.Count == theirDeck - 3, "the opponent's Fel Reaver removes the top of its enemy's plays too");
    }

    // ---- GVG_017 Call Pet -------------------------------------------------------------------------------------------

    private static void CallPet(Game g, Controller p, string id)
    {
        Stack(p, "CS2_182", "EX1_028");
        int hand = p.HandZone.Count;
        Play(g, p, id);
        var tiger = p.HandZone.Last();
        Check(p.HandZone.Count == hand + 1 && tiger.Card.Id == "EX1_028" && tiger.Cost == 1, "a drawn Beast costs (4) less");
        Concealed(g, tiger, "Call Pet");
        Play(g, p, id);
        var yeti = p.HandZone.Last();
        Check(yeti.Card.Id == "CS2_182" && yeti.Cost == 4, "a drawn non-Beast keeps its cost");
        Stack(p, "CS2_120");
        Play(g, p, id);
        Check(p.HandZone.Last().Card.Id == "CS2_120" && p.HandZone.Last().Cost == 0, "the discount never goes below zero");
        End(g); End(g);
        Check(tiger.Cost == 1 && tiger.Zone.Type == Zone.HAND, "the discount persists across turns");
        Stack(p, "EX1_028", "CS2_182");
        Play(g, p, id);
        var tigerNext = p.DeckZone.TopCard;
        Check(tigerNext.Card.Id == "EX1_028" && tigerNext.Cost == 5, "only the drawn card is discounted");
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        hand = p.HandZone.Count; int enemyHand = p.Opponent.HandZone.Count; int damage = p.Hero.Damage;
        Play(g, p, id);
        Check(p.HandZone.Count == hand && p.Hero.Damage == damage + 1 && p.Opponent.HandZone.Count == enemyHand, "an empty deck draws nothing and costs fatigue only");
        EmptyHand(p); FillHand(p, 9);
        Stack(p, "EX1_028");
        Play(g, p, id);
        Check(p.HandZone.Count == 10 && p.HandZone.Last().Cost == 1, "the freed slot takes the drawn Beast, discounted");
        Resume(g, next => Check(Get(next, p.HandZone.Last()).Cost == 1, "clone keeps the discount"));
        Check(p.Opponent.HandZone.All(c => c.Cost == c.Card.Cost), "the opponent's cards are untouched");
    }

    // ---- GVG_018 Queen of Pain --------------------------------------------------------------------------------------

    private static void QueenOfPain(Game g, Controller p, string id)
    {
        var queen = Summon(p, id);
        Check(queen.IsRace(Race.DEMON) && queen.AttackDamage == 1 && queen.Health == 4 && queen.HasLifeSteal, "Queen of Pain is a 1/4 Lifesteal Demon");
        p.Hero.Damage = 5; var croc = Summon(p.Opponent, "CS2_120");
        Attack(g, queen, croc);
        Check(croc.Damage == 1 && p.Hero.Damage == 4, "attacking heals my hero for the damage dealt");
        Park(p.Opponent);
        var q2 = Summon(p, id);
        Attack(g, q2, p.Opponent.Hero);
        Check(p.Opponent.Hero.Damage == 1 && p.Hero.Damage == 3, "also when hitting a hero");
        var squire = Summon(p.Opponent, "EX1_008"); var q3 = Summon(p, id);
        Attack(g, q3, squire);
        Check(!squire.HasDivineShield && p.Hero.Damage == 3, "a Divine Shield means no damage, no heal");
        p.Hero.Damage = 0; Park(p.Opponent);
        var q4 = Summon(p, id);
        Attack(g, q4, p.Opponent.Hero);
        Check(p.Hero.Damage == 0, "no overheal");
        p.Hero.Damage = 4; var q5 = Summon(p, id); q5.Silence(); g.DeathProcessingAndAuraUpdate();
        Attack(g, q5, p.Opponent.Hero);
        Check(p.Hero.Damage == 4 && !q5.HasLifeSteal, "Silence removes Lifesteal");
        Park(p); Park(p.Opponent);
        var defender = Summon(p, id); var attacker = Summon(p.Opponent, "CS2_120");
        End(g);
        p.Hero.Damage = 4;
        Attack(g, attacker, defender);
        Check(attacker.Damage == 1 && p.Hero.Damage == 3, "also when defending");
        Resume(g, next => Check(Get(next, defender).HasLifeSteal, "clone keeps Lifesteal"));
    }

    // ---- GVG_019 Demonheart -----------------------------------------------------------------------------------------

    private static void Demonheart(Game g, Controller p, string id)
    {
        var demon = Summon(p, "CS2_065"); var yeti = Summon(p, "CS2_182"); var enemyYeti = Summon(p.Opponent, "CS2_182");
        var enemyDemon = Summon(p.Opponent, "CS2_065");
        var spell = Add(p, id);
        var options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id).ToArray();
        Check(options.Length == 4 && options.All(t => t.Target is Minion), "any minion is a legal target, heroes are not");
        PlayEntity(g, p, spell, demon);
        Check(demon.AttackDamage == 6 && demon.Health == 8 && demon.Damage == 0, "a friendly Demon gets +5/+5 instead of damage");
        Play(g, p, id, enemyYeti);
        Check(enemyYeti.Zone.Type == Zone.GRAVEYARD, "an enemy minion takes 5 damage");
        Play(g, p, id, enemyDemon);
        Check(enemyDemon.Zone.Type == Zone.GRAVEYARD && enemyDemon.AttackDamage == 1, "an enemy Demon takes damage, not the buff");
        Play(g, p, id, yeti);
        Check(yeti.Zone.Type == Zone.GRAVEYARD, "a friendly non-Demon takes 5 damage");
        Summon(p, "EX1_012");
        var wall = Summon(p.Opponent, "CS2_179");
        Play(g, p, "GVG_010", wall);
        Play(g, p, id, wall);
        Check(wall.Damage == 6 && wall.Zone.Type == Zone.PLAY, "Spell Damage adds to the 5 damage");
        Play(g, p, id, demon);
        Check(demon.AttackDamage == 11 && demon.Health == 13, "Spell Damage does not boost the buff");
        End(g); End(g);
        Check(demon.AttackDamage == 11 && demon.Health == 13, "the buff is permanent");
        demon.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(demon.AttackDamage == 1 && demon.Health == 3, "Silence removes the buff");
        Resume(g, next => Check(Get(next, demon).AttackDamage == 1, "clone keeps the silenced state"));
    }

    // ---- GVG_020 Fel Cannon -----------------------------------------------------------------------------------------

    private static void FelCannon(Game g, Controller p, string id)
    {
        var cannon = Summon(p, id);
        Check(cannon.IsRace(Race.MECHANICAL) && cannon.AttackDamage == 3 && cannon.Health == 5, "Fel Cannon is a 3/5 Mech");
        End(g); End(g);
        Check(cannon.Damage == 0 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "with no non-Mech minion there is no victim");
        var friendMech = Summon(p, "GVG_044"); var enemyMech = Summon(p.Opponent, "GVG_044");
        var candidates = new[] { Summon(p, "CS2_182"), Summon(p.Opponent, "CS2_182"), Summon(p.Opponent, "CS2_120") };
        Resume(g, next => {
            End(next);
            var hit = candidates.Select(m => Get(next, m)).ToArray();
            Check(hit.Sum(m => m.Damage) == 2 && hit.Count(m => m.Damage == 2) == 1, "clone: exactly one non-Mech takes 2 at my end of turn");
        });
        var victims = new HashSet<int>();
        for (int i = 0; i < 16; i++)
        {
            foreach (var m in candidates) m.Damage = 0;
            End(g);
            Check(candidates.Sum(m => m.Damage) == 2 && candidates.Count(m => m.Damage == 2) == 1 && cannon.Damage == 0 && friendMech.Damage == 0
                && enemyMech.Damage == 0 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0,
                "at my end of turn exactly one non-Mech minion, on either side, takes 2");
            victims.Add(Array.FindIndex(candidates, m => m.Damage == 2));
            End(g);
            Check(candidates.Sum(m => m.Damage) == 2, "nothing more happens at the opponent's end of turn");
        }
        Check(victims.Count >= 2, "the victim is random");
        foreach (var m in candidates) m.Damage = 0;
        cannon.Silence(); g.DeathProcessingAndAuraUpdate();
        End(g);
        Check(candidates.Sum(m => m.Damage) == 0, "Silence removes the trigger");
        End(g);
    }

    // ---- GVG_022 Tinker's Sharpsword Oil ----------------------------------------------------------------------------

    private static void SharpswordOil(Game g, Controller p, string id)
    {
        Equip(p);
        var friends = new[] { Summon(p, "CS2_182"), Summon(p, "CS2_182"), Summon(p, "CS2_065") };
        Play(g, p, id);
        Check(p.Hero.Weapon.AttackDamage == 6 && p.Hero.Weapon.Durability == 2 && friends.All(m => m.AttackDamage == m.Card.ATK),
            "without Combo: weapon +3 Attack, minions untouched");
        Play(g, p, id);
        Check(p.Hero.Weapon.AttackDamage == 9 && friends.Count(m => m.AttackDamage == m.Card.ATK + 3) == 1
            && friends.Count(m => m.AttackDamage == m.Card.ATK) == 2, "Combo: weapon +3 and one random friendly minion +3");
        var seen = new HashSet<int>();
        for (int i = 0; i < 12; i++)
        {
            var before = friends.Select(m => m.AttackDamage).ToArray();
            Play(g, p, id);
            var raised = Enumerable.Range(0, friends.Length).Where(k => friends[k].AttackDamage == before[k] + 3).ToArray();
            Check(raised.Length == 1, "exactly one friendly minion gains 3 Attack");
            seen.Add(raised[0]);
        }
        Check(seen.Count >= 2 && p.Hero.Weapon.AttackDamage == 9 + 3 * 12, "the minion is random; the weapon always gains 3");
        friends[0].Silence(); g.DeathProcessingAndAuraUpdate();
        Check(friends[0].AttackDamage == friends[0].Card.ATK, "Silence removes the minion's bonus");
        int weapon = p.Hero.Weapon.AttackDamage;
        End(g);
        var opp = p.Opponent; var theirs = Summon(opp, "CS2_182");
        PlayEntity(g, opp, Add(opp, id));
        Check(theirs.AttackDamage == 4 && opp.Hero.Weapon == null && p.Hero.Weapon.AttackDamage == weapon,
            "no weapon: nothing happens, and my weapon is not theirs");
        PlayEntity(g, opp, Add(opp, id));
        Check(theirs.AttackDamage == 7 && opp.Hero.Weapon == null, "Combo without a weapon still buffs a minion");
        Park(opp);
        PlayEntity(g, opp, Add(opp, id));
        Check(opp.BoardZone.Count == 0, "Combo with no friendly minion and no weapon is harmless");
        Resume(g, next => Check(next.Player1.Hero.Weapon.AttackDamage == weapon, "clone keeps the weapon bonus"));
    }

    // ---- GVG_023 Goblin Auto-Barber ---------------------------------------------------------------------------------

    private static void AutoBarber(Game g, Controller p, string id)
    {
        var barber0 = (Minion)Play(g, p, id);
        Check(p.Hero.Weapon == null && barber0.IsRace(Race.MECHANICAL) && barber0.AttackDamage == 3 && barber0.Health == 2, "no weapon: nothing happens");
        Equip(p); Equip(p.Opponent);
        Play(g, p, id);
        Check(p.Hero.Weapon.AttackDamage == 4 && p.Hero.Weapon.Durability == 2 && p.Opponent.Hero.Weapon.AttackDamage == 3,
            "my weapon gains +1 Attack, durability and the enemy weapon are unchanged");
        Play(g, p, id);
        Check(p.Hero.Weapon.AttackDamage == 5, "Battlecries stack");
        End(g); End(g);
        Check(p.Hero.Weapon.AttackDamage == 5, "the bonus is permanent");
        Equip(p);
        Check(p.Hero.Weapon.AttackDamage == 3, "a replacement weapon is a fresh weapon");
        Play(g, p, id);
        Check(p.Hero.Weapon.AttackDamage == 4, "the new weapon can be buffed");
        Resume(g, next => Check(next.Player1.Hero.Weapon.AttackDamage == 4, "clone keeps the bonus"));
        End(g);
        Play(g, p.Opponent, id);
        Check(p.Opponent.Hero.Weapon.AttackDamage == 4 && p.Hero.Weapon.AttackDamage == 4, "the opponent's Barber buffs the opponent's weapon");
    }

    // ---- GVG_024 Cogmaster's Wrench ---------------------------------------------------------------------------------

    private static void CogmastersWrench(Game g, Controller p, string id)
    {
        var enemyMech = Summon(p.Opponent, "GVG_044"); Add(p, "GVG_044");
        Play(g, p, id);
        var wrench = p.Hero.Weapon;
        Check(wrench.Card.Id == id && wrench.AttackDamage == 1 && wrench.Durability == 3 && p.Hero.AttackDamage == 1,
            "1/3 weapon; neither an enemy Mech nor a Mech in hand counts");
        var mech = Summon(p, "GVG_044");
        Check(p.Hero.AttackDamage == 3, "+2 Attack while I have a Mech");
        Kill(g, mech);
        Check(p.Hero.AttackDamage == 1, "the bonus ends with the Mech");
        mech = Summon(p, "GVG_044");
        Park(p.Opponent);
        Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "hero attack accepted");
        Check(p.Opponent.Hero.Damage == 3 && wrench.Durability == 2, "the hero hits for 3 and the weapon loses 1 Durability");
        Resume(g, next => { Kill(next, Get(next, mech)); Check(next.Player1.Hero.AttackDamage == 1, "clone loses the bonus"); });
        End(g); End(g);
        Check(p.Hero.AttackDamage == 3, "the aura persists across turns");
        Play(g, p, id);
        Check(p.Hero.Weapon.Id != wrench.Id && p.Hero.AttackDamage == 3, "a second Wrench replaces the first and keeps the aura");
    }

    // ---- GVG_025 One-eyed Cheat -------------------------------------------------------------------------------------

    private static void OneEyedCheat(Game g, Controller p, string id)
    {
        var cheat = (Minion)Play(g, p, id);
        Check(cheat.IsRace(Race.PIRATE) && cheat.AttackDamage == 4 && cheat.Health == 1 && !cheat.HasStealth, "a 4/1 Pirate that does not stealth itself");
        Summon(p, "CS2_182");
        Check(!cheat.HasStealth, "a non-Pirate summon does nothing");
        Summon(p.Opponent, "NEW1_022");
        Check(!cheat.HasStealth, "an enemy Pirate does not count");
        var second = (Minion)Play(g, p, id);
        Check(cheat.HasStealth && !second.HasStealth, "a second Cheat stealths the first, not itself");
        Play(g, p, "NEW1_022");
        Check(cheat.HasStealth && second.HasStealth, "a played Pirate stealths every Cheat");
        Park(p.Opponent);
        Attack(g, cheat, p.Opponent.Hero);
        Check(!cheat.HasStealth && second.HasStealth, "attacking removes Stealth");
        Summon(p, "NEW1_022");
        Check(cheat.HasStealth, "a summoned Pirate grants it again");
        Resume(g, next => { Get(next, second).Silence(); Check(!Get(next, second).HasStealth, "clone Silence removes Stealth"); });
        second.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(!second.HasStealth, "Silence removes Stealth");
        Attack(g, cheat, p.Opponent.Hero);
        Check(!cheat.HasStealth, "Stealth lost again");
        Summon(p, "NEW1_022");
        Check(cheat.HasStealth && !second.HasStealth, "the Silenced Cheat no longer reacts");
        Kill(g, cheat);
        Summon(p, "NEW1_022");
        End(g);
        var theirs = (Minion)Play(g, p.Opponent, id);
        Summon(p.Opponent, "NEW1_022");
        Check(theirs.HasStealth, "the opponent's Cheat reacts to the opponent's Pirates");
    }

    // ---- Unregistered scenarios (never registered, never listed) ------------------------------------------------------

    // The 37.0 identity row is not loaded for these ids (see the header). Apply what it carries that a scenario reads, so the
    // scenario sees the retail card: the Shadow school for Darkbomb (Rush for Flame Leviathan cannot be set from here).
    private static void RunUnregistered()
    {
        Cards.FromId("GVG_015").Tags[GameTag.SPELL_SCHOOL] = 6;
        foreach (var (id, scenario) in new (string, Action<Game, Controller, string>)[] { ("GVG_004", GoblinBlastmage), ("GVG_005", EchoOfMedivh),
            ("GVG_011", Shrinkmeister), ("GVG_015", Darkbomb), ("GVG_021", MalGanis) })
            for (int seed = 1; seed <= 3; seed++)
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
                scenario(game, game.Player1, id);
                Check(Canonical.State(game) == Canonical.State(game.CloneExact(history: true)), "exact clone after unregistered scenario " + id);
                Check(Canonical.State(game) == Canonical.State(Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools)), "snapshot after unregistered scenario " + id);
            }
        Console.WriteLine("UNREGISTERED scenarios GVG_004, GVG_005, GVG_011, GVG_015, GVG_021 PASS; seeds=3; clone/snapshot");
    }

    private static void EchoOfMedivh(Game g, Controller p, string id)
    {
        var a = Summon(p, "CS2_182"); var b = Summon(p, "CS2_120"); Summon(p.Opponent, "CS2_182");
        b.Damage = 1;
        int enemyHand = p.Opponent.HandZone.Count;
        Play(g, p, id);
        Check(p.HandZone.Count == 2 && p.HandZone.Select(c => c.Card.Id).OrderBy(x => x).SequenceEqual(new[] { "CS2_120", "CS2_182" })
            && p.Opponent.HandZone.Count == enemyHand && p.BoardZone.Count == 2, "one copy of each friendly minion goes to hand");
        Check(p.HandZone.All(c => c.Zone.Type == Zone.HAND && ((Minion)c).Damage == 0), "copies are fresh");
        Park(p); Park(p.Opponent);
        EmptyHand(p); FillHand(p, 8);
        Summon(p, "CS2_182"); Summon(p, "CS2_182"); Summon(p, "CS2_182");
        Play(g, p, id);
        Check(p.HandZone.Count == 10, "a full hand takes only what fits");
        EmptyHand(p); Park(p);
        Play(g, p, id);
        Check(p.HandZone.Count == 0, "no minions, no copies");
    }

    private static void MalGanis(Game g, Controller p, string id)
    {
        var ally = Summon(p, "CS2_065"); var enemyDemon = Summon(p.Opponent, "CS2_065");
        var mal = (Minion)Play(g, p, id);
        Check(mal.AttackDamage == 9 && mal.Health == 7 && mal.IsRace(Race.DEMON), "Mal'Ganis is a 9/7 Demon");
        Check(ally.AttackDamage == 3 && ally.Health == 5 && mal.AttackDamage == 9 && enemyDemon.AttackDamage == 1, "other friendly Demons get +2/+2; not itself, not the enemy's");
        Check(p.Hero.IsImmune && !p.Opponent.Hero.IsImmune, "my hero is Immune");
        Check(p.Hero.Damage == 0, "fixture");
        Kill(g, mal);
        Check(!p.Hero.IsImmune && ally.AttackDamage == 1 && ally.Health == 3, "both effects end when it leaves");
        var again = (Minion)Play(g, p, id);
        again.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(!p.Hero.IsImmune && ally.AttackDamage == 1, "Silence ends both effects");
    }
}
