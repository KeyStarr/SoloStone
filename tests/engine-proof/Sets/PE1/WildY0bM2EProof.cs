using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Actions;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

// Wave Y0b, lane m2e: Goblins vs Gnomes GVG_101 .. GVG_123 (Scarlet Purifier, Tinkertown Technician, Hobgoblin, Piloted Sky Golem, Junkbot,
// Enhance-o Mechano, Recombobulator, Mini-Mage, Dr. Boom, Mimiron's Head, Mogor, Foe Reaper 4000, Toshley, Thermaplugg, Gazlowe, Troggzor,
// Blingtron 3000, Hemet Nesingwary, Clockwork Giant, Wee Spellstopper, Soot Spewer).
// Unregistered (not in the batch manifest), their scenarios kept below and run only with WILD_Y0B_M2E_UNREGISTERED=1:
//  - BLOCKED-STANDARD GVG_103 Micro Machine and GVG_114 Sneed's Old Shredder (plugin/LocalCardCatalog.cs CoreUnlock rows and
//    data/progression/core-2026-unlocks.json copy groups): Standard data uses them today. Their scripts are not changed here.
//  - HELD GVG_112 Mogor the Ogre: its Standard golden games reach it through the legacy random pools, and scripting it (WildGvgTasks.Mogor, the
//    Forgetful coin on every attack) changes core-2026 game "core-2026-2-vs-core-2026-3-s0-random" (legal-task hash at action 52). The script
//    stays at base (no effect); the scenario is kept unregistered and FAILS until the card is released with a Standard golden audit.
//    GVG_103 FAILS its scenario: the legacy script triggers only at the start of its owner's turn (no EitherTurn); the text says each turn.
// AMOUNT TRAP: the client stores SPELLPOWER as 1; Mini-Mage and Soot Spewer really have Spell Damage +1 (the base value).
internal static class WildY0bM2EProof
{
    [GroupProof("wild-y0b-m2e-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        WildSetsProof.Case("GVG_101", ScarletPurifier);
        WildSetsProof.Case("GVG_102", TinkertownTechnician);
        WildSetsProof.Case("GVG_104", Hobgoblin);
        WildSetsProof.Case("GVG_105", PilotedSkyGolem);
        WildSetsProof.Case("GVG_106", Junkbot);
        WildSetsProof.Case("GVG_107", EnhanceOMechano);
        WildSetsProof.Case("GVG_108", Recombobulator);
        WildSetsProof.Case("GVG_109", MiniMage);
        WildSetsProof.Case("GVG_110", DrBoom);
        WildSetsProof.Case("GVG_111", MimironsHead);
        WildSetsProof.Case("GVG_113", FoeReaper4000);
        WildSetsProof.Case("GVG_115", Toshley);
        WildSetsProof.Case("GVG_116", MekgineerThermaplugg);
        WildSetsProof.Case("GVG_117", Gazlowe);
        WildSetsProof.Case("GVG_118", Troggzor);
        WildSetsProof.Case("GVG_119", Blingtron3000);
        WildSetsProof.Case("GVG_120", HemetNesingwary);
        WildSetsProof.Case("GVG_121", ClockworkGiant);
        WildSetsProof.Case("GVG_122", WeeSpellstopper);
        WildSetsProof.Case("GVG_123", SootSpewer);
        if (Environment.GetEnvironmentVariable("WILD_Y0B_M2E_UNREGISTERED") == "1") RunUnregistered();
    }

    // ---- fixtures -------------------------------------------------------------------------------------------------

    private static void Check(bool condition, string message) => Core2026Proof.Check(condition, message);
    private static void Fill(Controller c, string id = "CS2_182") { while (!c.BoardZone.IsFull) Summon(c, id); }
    private static void EmptyHand(Controller c) { foreach (var held in c.HandZone.ToArray()) c.SetasideZone.Add(c.HandZone.Remove(held)); }
    private static void FillHand(Controller c, string id = "CS2_182") { while (c.HandZone.Count < 10) Add(c, id); }
    private static bool Playable(Controller c, IPlayable card) =>
        c.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id);
    private static bool Targetable(Controller c, IPlayable card, ICharacter target) =>
        c.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id && t.Target?.Id == target.Id);
    private static void Park(Controller c) => StandardSetsProof.ParkBoard(c);
    private static T Get<T>(Game game, T entity) where T : class, IPlayable => (T)game.IdEntityDic[entity.Id];
    private static Controller Get(Game game, Controller c) => c.PlayerId == game.Player1.PlayerId ? game.Player1 : game.Player2;
    private static IEnumerable<PlayerTask> PowerOptions(Controller c) => c.Options().Where(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER);
    private static void Equip(Controller c, string id = "CS2_106") => c.Hero.AddWeapon((Weapon)Entity.FromCard(c, Cards.FromId(id)));

    private static void Resume(Game game, Action<Game> continuation)
    {
        foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) })
        {
            Check(Canonical.State(game) == Canonical.State(next), "m2e state before continuation");
            continuation(next);
        }
    }

    private static Game Fresh(int seed, CardClass first = CardClass.ROGUE, CardClass second = CardClass.MAGE)
    {
        var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            Player1HeroClass = first, Player2HeroClass = second, History = true, Logging = false,
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

    private static void Strike(Game g, Minion attacker, ICharacter target)
    {
        attacker.NumAttacksThisTurn = 0;
        Attack(g, attacker, target);
    }

    private static void NoCombatFlags(Game g, string what)
    {
        foreach (var entity in g.IdEntityDic.Values.OfType<ICharacter>())
            Check(!entity.IsAttacking && !entity.IsDefending, what + ": no stale combat flag on " + entity.Card.Id);
    }

    private static (int Intended, int[] Others) Trials(Game g, int n, ICharacter intended, ICharacter[] others, Action swing)
    {
        int hitIntended = 0; var hitOthers = new int[others.Length];
        for (int i = 0; i < n; i++)
        {
            intended.Damage = 0; foreach (var o in others) o.Damage = 0;
            swing();
            Check(intended.Damage > 0 ^ others.Any(o => o.Damage > 0), "an attack lands on exactly one of the candidates: " + intended.Card.Id + "=" + intended.Damage + " others " + string.Join(",", others.Select(o => o.Card.Id + "=" + o.Damage)) + " trial " + i);
            if (intended.Damage > 0) hitIntended++;
            for (int k = 0; k < others.Length; k++) if (others[k].Damage > 0) hitOthers[k]++;
            NoCombatFlags(g, "after attack " + i);
        }
        intended.Damage = 0; foreach (var o in others) o.Damage = 0;
        return (hitIntended, hitOthers);
    }

    private static void Hit(Game g, ICharacter target, IPlayable source, int n) { target.TakeDamage(source, n); g.DeathProcessingAndAuraUpdate(); }
    private static Minion Dummy(Controller c, string id = "CS2_182") { var m = Summon(c, id); m[GameTag.ATK] = 0; m[GameTag.HEALTH] = 20; return m; }
    private static void KillSettle(Game g, Minion m) { Kill(g, m); g.DeathProcessingAndAuraUpdate(); }
    private static void Silence(Game g, Minion m) { m.Silence(); g.DeathProcessingAndAuraUpdate(); }
    private static int Parts(Controller c) => c.HandZone.Count(x => x.Card.Id.StartsWith("PART_"));
    private static int Count(Controller c, string id) => c.BoardZone.Count(m => m.Card.Id == id);
    private static Minion First(Controller c, string id) => c.BoardZone.FirstOrDefault(m => m.Card.Id == id);

    private static Spell Secret(Game g, Controller owner, string id)
    {
        var secret = (Spell)Add(owner, id);
        PlayEntity(g, owner, secret);
        Check(secret.Zone.Type == Zone.SECRET && owner.SecretZone.Contains(secret), "secret fixture " + id);
        return secret;
    }

    /// <summary>A Deathrattle minion with 20 Health and no Attack, a target that survives two damage.</summary>
    private static Minion Rattler(Controller c, int health = 20) { var m = Summon(c, "EX1_556"); m[GameTag.ATK] = 0; m[GameTag.HEALTH] = health; return m; }

    /// <summary>A Priest versus a Mage game: Mind Control (10 Mana) steals a minion from the Mage on the Priest's turn.</summary>
    private static Game PriestGame(int seed = 6) => Fresh(seed, CardClass.PRIEST, CardClass.MAGE);
    private static void MindControl(Game g, Controller by, Minion target)
    {
        PlayEntity(g, by, Add(by, "CS1_113"), target);
        Check(target.Controller == by && target.Zone.Type == Zone.PLAY, "Mind Control fixture");
    }

    // ---- GVG_101 Scarlet Purifier -----------------------------------------------------------------------------------

    private static void ScarletPurifier(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        var pur = (Minion)card;
        Check(card.Cost == 3 && pur.AttackDamage == 4 && pur.Health == 3, "a 4/3 for 3");
        Check(Playable(p, card), "playable with no Deathrattle minion in play");
        PlayEntity(g, p, card);
        Check(pur.Zone.Type == Zone.PLAY && pur.Damage == 0 && Count(opp, "EX1_556") == 0, "no Deathrattle minion: nothing happens");
        Park(p); Park(opp);
        var theirs = Rattler(opp); var mine = Rattler(p); var plain = Dummy(opp); var plainMine = Dummy(p);
        var shielded = Rattler(opp); shielded.HasDivineShield = true;
        var quiet = Rattler(opp); Silence(g, quiet);
        var hoarder = Summon(opp, "EX1_096");
        var magi = Summon(p, "CS2_197");
        Check(magi.SpellPower == 1, "fixture: spell damage");
        var inHand = Add(opp, "EX1_556");
        int oppHand = opp.HandZone.Count;
        var second = Add(p, id);
        PlayEntity(g, p, second);
        Check(theirs.Damage == 2 && mine.Damage == 2, "2 damage to every Deathrattle minion, mine and theirs, spell damage adds nothing: " + theirs.Damage + "/" + mine.Damage);
        Check(plain.Damage == 0 && plainMine.Damage == 0 && magi.Damage == 0, "minions without Deathrattle are untouched");
        Check(shielded.Damage == 0 && !shielded.HasDivineShield, "Divine Shield absorbs the blast");
        Check(quiet.Damage == 0, "a Silenced Deathrattle minion does not count");
        Check(hoarder.Zone.Type == Zone.GRAVEYARD && opp.HandZone.Count == oppHand + 1, "a 1-Health Deathrattle minion dies and its Deathrattle runs");
        Check(((Minion)second).Damage == 0 && p.Hero.Damage == 0 && opp.Hero.Damage == 0, "neither the Purifier nor a hero is hit");
        Check(inHand.Zone.Type == Zone.HAND, "a Deathrattle minion in hand is not affected");
        // a Deathrattle that a spell granted counts
        var f = Fresh(5, CardClass.DRUID, CardClass.MAGE);
        var vanilla = Dummy(f.Player1); var vanillaFoe = Dummy(f.Player2);
        Play(f, f.Player1, "EX1_158");
        Play(f, f.Player1, id);
        Check(vanilla.Damage == 2, "a Deathrattle granted by Soul of the Forest counts: " + vanilla.Damage);
        Check(vanillaFoe.Damage == 0, "the enemy minion has none");
        // a dead Deathrattle minion's result and clone / snapshot
        var dying = Rattler(opp, 2);
        Resume(g, next =>
        {
            var again = Add(Get(next, p), id);
            PlayEntity(next, Get(next, p), again);
            Check(Get(next, dying).Zone.Type == Zone.GRAVEYARD && Count(Get(next, opp), "skele21") == 1, "clone: the blast kills it and its Deathrattle runs");
        });
        // the opponent's Purifier hits my Deathrattle minions too
        End(g);
        var fresh = Rattler(p);
        var theirPurifier = Add(opp, id);
        PlayEntity(g, opp, theirPurifier);
        Check(fresh.Damage == 2 && mine.Damage == 4, "the opponent's Purifier hits my Deathrattle minions: " + fresh.Damage + "/" + mine.Damage);
    }

    // ---- GVG_102 Tinkertown Technician ------------------------------------------------------------------------------

    private static void TinkertownTechnician(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        var tech = (Minion)card;
        Check(card.Cost == 3 && tech.AttackDamage == 3 && tech.Health == 3 && !tech.IsRace(Race.MECHANICAL), "a 3/3 for 3");
        PlayEntity(g, p, card);
        Check(tech.AttackDamage == 3 && tech.Health == 3 && Parts(p) == 0, "no Mech: no buff, no Spare Part");
        // an enemy Mech and a Mech in hand do not count
        Summon(opp, "GVG_093"); Add(p, "GVG_093");
        var t2 = (Minion)Play(g, p, id);
        Check(t2.AttackDamage == 3 && t2.Health == 3 && Parts(p) == 0, "enemy Mech and hand Mech do not count");
        // a friendly Mech does; a Silenced Mech is still a Mech
        var mech = Summon(p, "GVG_093"); Silence(g, mech);
        var t3 = (Minion)Play(g, p, id);
        Check(t3.AttackDamage == 4 && t3.Health == 4 && Parts(p) == 1, "with a Mech: +1/+1 and one Spare Part: " + t3.AttackDamage + "/" + t3.Health + " parts " + Parts(p));
        var part = p.HandZone.First(x => x.Card.Id.StartsWith("PART_"));
        Check(new[] { "PART_001", "PART_002", "PART_003", "PART_004", "PART_005", "PART_006", "PART_007" }.Contains(part.Card.Id), "a real Spare Part: " + part.Card.Id);
        Concealed(g, part, "the Spare Part");
        Check(opp.HandZone.All(x => !x.Card.Id.StartsWith("PART_")), "nothing for the opponent");
        Resume(g, next =>
        {
            var c2 = Get(next, p);
            var t = (Minion)Play(next, c2, id);
            Check(t.AttackDamage == 4 && Parts(c2) == 2, "clone / snapshot: the battlecry still sees the Mech");
        });
        // the Technician is not a Mech, so it never feeds the next one
        Park(p);
        var lone = (Minion)Play(g, p, id);
        var next2 = (Minion)Play(g, p, id);
        Check(lone.AttackDamage == 3 && next2.AttackDamage == 3, "a Technician is not a Mech");
        // Silence removes the buff only
        Silence(g, t3);
        Check(t3.AttackDamage == 3 && t3.Health == 3, "Silence removes the +1/+1");
        // hand room: the battlecry adds one card even with a nearly full hand
        EmptyHand(p);
        Summon(p, "GVG_093");
        var t4 = Add(p, id); FillHand(p);
        PlayEntity(g, p, t4);
        Check(p.HandZone.Count == 10 && Parts(p) == 1, "nine cards in hand, the Spare Part fills the tenth");
        // the opponent's Technician looks at the opponent's Mechs
        End(g);
        EmptyHand(opp); Park(opp);
        Summon(p, "GVG_093");
        var theirs = (Minion)Play(g, opp, id);
        Check(theirs.AttackDamage == 3 && Parts(opp) == 0, "my Mech does not help the opponent");
        Summon(opp, "GVG_093");
        var theirs2 = (Minion)Play(g, opp, id);
        Check(theirs2.AttackDamage == 4 && Parts(opp) == 1 && Parts(p) == 1, "its own Mech does");
        // the Spare Part is random across seeds
        var seen = new HashSet<string>();
        for (int seed = 1; seed <= 40; seed++)
        {
            var fr = Fresh(seed); Summon(fr.Player1, "GVG_093");
            Play(fr, fr.Player1, id);
            Check(Parts(fr.Player1) == 1, "exactly one Spare Part");
            seen.Add(fr.Player1.HandZone.First(x => x.Card.Id.StartsWith("PART_")).Card.Id);
        }
        Check(seen.Count >= 4, "at least four different Spare Parts across seeds: " + seen.Count);
    }

    // ---- GVG_104 Hobgoblin ------------------------------------------------------------------------------------------

    private static void Hobgoblin(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var hob = (Minion)Play(g, p, id);
        Check(hob.Card.Cost == 3 && hob.AttackDamage == 2 && hob.Health == 3, "a 2/3 for 3");
        var wisp = (Minion)Play(g, p, "CS2_231");
        Check(wisp.AttackDamage == 3 && wisp.Health == 3, "a played 1-Attack minion gets +2/+2: " + wisp.AttackDamage + "/" + wisp.Health);
        var raider = (Minion)Play(g, p, "CS2_168");
        Check(raider.AttackDamage == 2 && raider.Health == 1, "a 2-Attack minion is untouched");
        var yeti = (Minion)Play(g, p, "CS2_182");
        Check(yeti.AttackDamage == 4, "a 4-Attack minion is untouched");
        var summoned = Summon(p, "CS2_231");
        Check(summoned.AttackDamage == 1 && summoned.Health == 1, "a summoned (not played) 1-Attack minion is untouched");
        var charger = (Minion)Play(g, p, "CS2_171");
        Check(charger.AttackDamage == 3 && charger.Health == 3 && charger.HasCharge, "a Charge 1-Attack minion is buffed too");
        // current Attack counts, not the printed one
        Park(p);
        hob = (Minion)Play(g, p, id);
        var buffedInHand = (Minion)Add(p, "CS2_231"); buffedInHand[GameTag.ATK] = 2;
        PlayEntity(g, p, buffedInHand);
        Check(buffedInHand.AttackDamage == 2 && buffedInHand.Health == 1, "a Wisp that has 2 Attack in hand does not qualify");
        var dummyInHand = (Minion)Add(p, "GVG_093"); dummyInHand[GameTag.ATK] = 1;
        PlayEntity(g, p, dummyInHand);
        Check(dummyInHand.AttackDamage == 3 && dummyInHand.Health == 4, "a 0-Attack minion that has 1 Attack in hand qualifies");
        // a Battlecry 1-Attack minion
        var squire = (Minion)Play(g, p, "EX1_008");
        Check(squire.AttackDamage == 3 && squire.Health == 3 && squire.HasDivineShield, "Argent Squire becomes 3/3");
        // two Hobgoblins stack
        Park(p);
        var h1 = (Minion)Play(g, p, id); var h2 = (Minion)Play(g, p, id);
        Check(h1.AttackDamage == 2 && h2.AttackDamage == 2, "a Hobgoblin is a 2-Attack minion and buffs neither");
        var stack = (Minion)Play(g, p, "CS2_231");
        Check(stack.AttackDamage == 5 && stack.Health == 5, "two Hobgoblins give +4/+4: " + stack.AttackDamage + "/" + stack.Health);
        Resume(g, next =>
        {
            var w = (Minion)Play(next, Get(next, p), "CS2_231");
            Check(w.AttackDamage == 5 && w.Health == 5, "clone / snapshot: both Hobgoblins still listen");
        });
        // Silence one
        Silence(g, h1);
        var half = (Minion)Play(g, p, "CS2_231");
        Check(half.AttackDamage == 3, "one Silenced Hobgoblin: only +2/+2");
        Silence(g, h2);
        var none = (Minion)Play(g, p, "CS2_231");
        Check(none.AttackDamage == 1 && none.Health == 1, "no Hobgoblin left");
        // the enemy side: my Hobgoblin does not buff the opponent's minions
        Park(p); Park(opp);
        var mine = (Minion)Play(g, p, id);
        End(g);
        Park(opp);
        var theirWisp = (Minion)Play(g, opp, "CS2_231");
        Check(theirWisp.AttackDamage == 1, "the opponent's played minion is not buffed by my Hobgoblin");
        var theirHob = (Minion)Play(g, opp, id);
        var theirWisp2 = (Minion)Play(g, opp, "CS2_231");
        Check(theirWisp2.AttackDamage == 3 && theirWisp.AttackDamage == 1, "the opponent's own Hobgoblin buffs its next 1-Attack minion only");
        // control change: a stolen Hobgoblin serves its new owner
        {
            var f = PriestGame();
            End(f);
            var stolen = (Minion)Play(f, f.Player2, id);
            End(f);
            MindControl(f, f.Player1, stolen);
            var w = (Minion)Play(f, f.Player1, "CS2_231");
            Check(w.AttackDamage == 3, "a stolen Hobgoblin buffs its new owner's 1-Attack minion");
            End(f);
            var w2 = (Minion)Play(f, f.Player2, "CS2_231");
            Check(w2.AttackDamage == 1, "and no longer its old owner's");
        }
    }

    // ---- GVG_105 Piloted Sky Golem ----------------------------------------------------------------------------------

    private static void PilotedSkyGolem(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var golem = (Minion)Play(g, p, id);
        Check(golem.Card.Cost == 6 && golem.AttackDamage == 6 && golem.Health == 4 && golem.IsRace(Race.MECHANICAL), "a 6/4 Mech for 6");
        KillSettle(g, golem);
        Check(p.BoardZone.Count == 1 && p.BoardZone[0].Card.Cost == 4 && p.BoardZone[0].Card.Type == CardType.MINION && p.BoardZone[0].Card.Collectible && opp.BoardZone.Count == 0,
            "a 4-cost collectible minion on my side");
        var seen = new HashSet<string>();
        var cost4 = SabberStoneCore.CardSets.Client.Sets.WildGvgTasks.MinionsCosting(FormatType.FT_WILD, 4);
        Check(cost4.Count >= 30 && cost4.All(c => c.Cost == 4 && c.Type == CardType.MINION && c.Collectible), "every candidate is a collectible 4-cost minion");
        Check(cost4.All(c => c.Id != "GVG_109") && Cards.CostMinionCards(FormatType.FT_WILD)[3].Any(c => c.Id == "GVG_109"),
            "Mini-Mage (now 3 Mana) moved from the Cost/4 bucket to Cost/3 (Cards.cs regroups the frozen buckets by current Cost)");
        for (int seed = 1; seed <= 400; seed++)
        {
            var f = Fresh(seed);
            var s = (Minion)Play(f, f.Player1, id);
            KillSettle(f, s);
            Check(f.Player1.BoardZone.Count == 1 && f.Player2.BoardZone.Count == 0, "exactly one minion appears");
            var m = f.Player1.BoardZone[0];
            Check(m.Card.Cost == 4 && m.Card.Type == CardType.MINION && (m.Card.Collectible || m.Card.Id == "LOOT_526d"), "a collectible 4-cost minion (The Darkness candle may already show as LOOT_526d): " + m.Card.Id);
            seen.Add(m.Card.Id);
        }
        Check(seen.Count >= 8, "a wide pool of 4-cost minions: " + seen.Count);
        Park(p);
        var last = (Minion)Play(g, p, id); Fill(p);
        Check(p.BoardZone.IsFull, "full board");
        KillSettle(g, last);
        Check(p.BoardZone.Count == 7, "the dead Golem's slot takes the new minion");
        Park(p);
        var quiet = (Minion)Play(g, p, id); Silence(g, quiet); KillSettle(g, quiet);
        Check(p.BoardZone.Count == 0, "a Silenced Golem summons nothing");
        var live = (Minion)Play(g, p, id);
        Resume(g, next => { KillSettle(next, Get(next, live)); Check(Get(next, p).BoardZone.Count == 1 && Get(next, p).BoardZone[0].Card.Cost == 4, "clone: the Deathrattle works"); });
        // killed in combat
        Park(p); Park(opp);
        var attacker = (Minion)Play(g, p, id);
        var wall = Summon(opp, "CS2_182"); wall[GameTag.ATK] = 9;
        attacker.IsExhausted = false; Attack(g, attacker, wall);
        Check(attacker.Zone.Type == Zone.GRAVEYARD && p.BoardZone.Count == 1 && p.BoardZone[0].Card.Cost == 4, "a combat death still summons");
        End(g);
        Park(opp);
        var theirs = (Minion)Play(g, opp, id);
        KillSettle(g, theirs);
        Check(opp.BoardZone.Count == 1 && opp.BoardZone[0].Card.Cost == 4, "the opponent's Golem summons on its own side");
    }

    // ---- GVG_106 Junkbot --------------------------------------------------------------------------------------------

    private static void Junkbot(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var junk = (Minion)Play(g, p, id);
        Check(junk.Card.Cost == 5 && junk.AttackDamage == 1 && junk.Health == 5 && junk.IsRace(Race.MECHANICAL), "a 1/5 Mech for 5");
        var mech = Summon(p, "GVG_093"); var plain = Summon(p, "CS2_182"); var foe = Summon(opp, "GVG_093"); var foePlain = Summon(opp, "CS2_182");
        Kill(g, plain);
        Check(junk.AttackDamage == 1 && junk.Health == 5, "a friendly non-Mech dying does nothing");
        Kill(g, foe);
        Check(junk.AttackDamage == 1 && junk.Health == 5, "an enemy Mech dying does nothing");
        Kill(g, mech);
        Check(junk.AttackDamage == 3 && junk.Health == 7, "a friendly Mech dying: +2/+2: " + junk.AttackDamage + "/" + junk.Health);
        // two Mechs at once
        var m1 = Summon(p, "GVG_093"); var m2 = Summon(p, "GVG_093"); var m3 = Summon(p, "GVG_093");
        m1.ToBeDestroyed = true; m2.ToBeDestroyed = true; m3.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
        Check(junk.AttackDamage == 9 && junk.Health == 13, "three Mechs dying together: +6/+6: " + junk.AttackDamage + "/" + junk.Health);
        // damage stays through the buff layers
        Hit(g, junk, foePlain, 4);
        Check(junk.Health == 9 && junk.Damage == 4, "buffed Health keeps its damage");
        var m4 = Summon(p, "GVG_093"); Kill(g, m4);
        Check(junk.AttackDamage == 11 && junk.Health == 11 && junk.Damage == 4, "a further +2/+2 stacks on the damaged Junkbot");
        // a Mech token and a combat death
        Park(p); Park(opp);
        var junk2 = (Minion)Play(g, p, id);
        var sheep = Summon(p, "GVG_044"); var biter = Summon(opp, "CS2_182");
        Attack(g, sheep, biter);
        Check(sheep.Zone.Type == Zone.GRAVEYARD && junk2.AttackDamage == 3 && junk2.Health == 7, "a Mech that dies in combat counts");
        // another Junkbot dying counts for the first
        var junk3 = (Minion)Play(g, p, id);
        Kill(g, junk3);
        Check(junk2.AttackDamage == 5 && junk2.Health == 9, "a friendly Junkbot is a Mech too");
        Resume(g, next =>
        {
            var m = Summon(Get(next, p), "GVG_093"); Kill(next, m);
            Check(Get(next, junk2).AttackDamage == 7 && Get(next, junk2).Health == 11, "clone / snapshot keeps listening");
        });
        // Silence removes the buff and the listening
        Silence(g, junk2);
        Check(junk2.AttackDamage == 1 && junk2.Health == 5, "Silence removes the stats");
        var m5 = Summon(p, "GVG_093"); Kill(g, m5);
        Check(junk2.AttackDamage == 1 && junk2.Health == 5, "a Silenced Junkbot no longer grows");
        // opponent's Junkbot reacts to its own Mechs only
        End(g);
        var theirJunk = (Minion)Play(g, opp, id);
        Kill(g, Summon(opp, "GVG_093"));
        Check(theirJunk.AttackDamage == 3, "the opponent's Junkbot grows from its own Mech");
        Kill(g, Summon(p, "GVG_093"));
        Check(theirJunk.AttackDamage == 3, "and not from mine");
        // control change: a stolen Junkbot grows from its new owner's Mechs
        {
            var f = PriestGame();
            End(f);
            var stolen = (Minion)Play(f, f.Player2, id);
            End(f);
            MindControl(f, f.Player1, stolen);
            var mine2 = Summon(f.Player1, "GVG_093"); var foe2 = Summon(f.Player2, "GVG_093");
            KillSettle(f, foe2);
            Check(stolen.AttackDamage == 1, "the old owner's Mech dying no longer counts");
            KillSettle(f, mine2);
            Check(stolen.AttackDamage == 3 && stolen.Health == 7, "the new owner's Mech dying does");
        }
    }

    // ---- GVG_107 Enhance-o Mechano ----------------------------------------------------------------------------------

    private static int Keywords(Minion m) => (m.HasWindfury ? 1 : 0) + (m.HasTaunt ? 1 : 0) + (m.HasDivineShield ? 1 : 0);

    private static void EnhanceOMechano(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        var mechano = (Minion)card;
        Check(card.Cost == 4 && mechano.AttackDamage == 3 && mechano.Health == 2 && mechano.IsRace(Race.MECHANICAL), "a 3/2 Mech for 4");
        PlayEntity(g, p, card);
        Check(Keywords(mechano) == 0, "alone: nothing, and never itself");
        Park(p); Park(opp);
        var a = Summon(p, "CS2_182"); var b = Summon(p, "CS2_182"); var c3 = Summon(p, "CS2_182"); var foe = Summon(opp, "CS2_182");
        var inHand = (Minion)Add(p, "CS2_182");
        var played = (Minion)Play(g, p, id);
        Check(Keywords(a) == 1 && Keywords(b) == 1 && Keywords(c3) == 1, "every other friendly minion gets exactly one of the three: " + Keywords(a) + Keywords(b) + Keywords(c3));
        Check(Keywords(played) == 0 && Keywords(mechano) == 0, "the Mechano itself and the earlier one get nothing");
        Check(Keywords(foe) == 0 && Keywords(inHand) == 0, "enemy minions and hand cards get nothing");
        // each of the three keywords exists across seeds, each minion rolls on its own
        int windfury = 0, taunt = 0, shield = 0, pairsEqual = 0, pairsDiffer = 0;
        var firstRun = new List<string>();
        for (int run = 0; run < 2; run++)
            for (int seed = 1; seed <= 40; seed++)
            {
                var f = Fresh(seed);
                var x = Summon(f.Player1, "CS2_182"); var y = Summon(f.Player1, "CS2_182"); var z = Summon(f.Player1, "CS2_182");
                Play(f, f.Player1, id);
                var signature = string.Concat(new[] { x, y, z }.Select(m => (m.HasWindfury ? "W" : "") + (m.HasTaunt ? "T" : "") + (m.HasDivineShield ? "D" : "") + "."));
                Check(new[] { x, y, z }.All(m => Keywords(m) == 1), "exactly one keyword each: " + signature);
                if (run == 0)
                {
                    firstRun.Add(signature);
                    foreach (var m in new[] { x, y, z }) { if (m.HasWindfury) windfury++; if (m.HasTaunt) taunt++; if (m.HasDivineShield) shield++; }
                    if (signature.Split('.')[0] == signature.Split('.')[1]) pairsEqual++; else pairsDiffer++;
                }
                else Check(firstRun[seed - 1] == signature, "same seed, same keywords");
            }
        Check(windfury >= 20 && taunt >= 20 && shield >= 20, "all three keywords appear, about a third each: " + windfury + "/" + taunt + "/" + shield);
        Check(pairsEqual >= 5 && pairsDiffer >= 5, "neighbours roll independently: equal " + pairsEqual + ", different " + pairsDiffer);
        // a minion that already has one of them still ends up with at least one; a Windfury grant works in combat
        Park(p); Park(opp);
        var taunter = Summon(p, "CS2_179");
        var victim = Dummy(opp);
        for (int seed = 1; seed <= 12; seed++)
        {
            var f = Fresh(seed);
            var t = Summon(f.Player1, "CS2_179");
            Play(f, f.Player1, id);
            Check(t.HasTaunt || t.HasWindfury || t.HasDivineShield, "an existing keyword is kept or joined");
        }
        // Windfury granted by the battlecry really allows a second attack
        bool checkedWindfury = false;
        for (int seed = 1; seed <= 40 && !checkedWindfury; seed++)
        {
            var f = Fresh(seed);
            var w = Summon(f.Player1, "CS2_182"); var wall = Dummy(f.Player2);
            Play(f, f.Player1, id);
            if (!w.HasWindfury) continue;
            End(f); End(f);
            Strike(f, w, wall); w.IsExhausted = false;
            Check(f.Player1.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == w.Id), "a second attack is available");
            checkedWindfury = true;
        }
        Check(checkedWindfury, "a Windfury roll occurred");
        // Silence removes the granted keyword
        for (int seed = 1; seed <= 6; seed++)
        {
            var f = Fresh(seed);
            var s = Summon(f.Player1, "CS2_182");
            Play(f, f.Player1, id);
            Silence(f, s);
            Check(Keywords(s) == 0, "Silence removes the granted keyword");
        }
        Resume(g, next =>
        {
            var q = Summon(Get(next, p), "CS2_182"); Play(next, Get(next, p), id);
            Check(Keywords(q) == 1, "clone / snapshot: the battlecry still works");
        });
        // the opponent's Mechano buffs the opponent's minions only
        End(g);
        Park(p); Park(opp);
        var mine = Summon(p, "CS2_182"); var theirs = Summon(opp, "CS2_182");
        Play(g, opp, id);
        Check(Keywords(theirs) == 1 && Keywords(mine) == 0, "the opponent's Mechano buffs its own minions: " + Keywords(theirs) + Keywords(mine));
    }

    // ---- GVG_108 Recombobulator -------------------------------------------------------------------------------------

    private static void Recombobulator(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 2 && ((Minion)card).AttackDamage == 3 && ((Minion)card).Health == 2, "a 3/2 for 2");
        Check(Playable(p, card), "playable with no other minion: the battlecry fizzles");
        var yeti = Summon(p, "CS2_182"); var foe = Summon(opp, "CS2_182");
        Check(Targetable(p, card, yeti) && !Targetable(p, card, foe) && !Targetable(p, card, p.Hero) && !Targetable(p, card, opp.Hero),
            "only a friendly minion is a target");
        PlayEntity(g, p, card, yeti);
        Check(yeti.Zone.Type != Zone.PLAY || yeti.Card.Id != "CS2_182" || p.BoardZone.All(m => m.Id != yeti.Id), "the Yeti is gone");
        var made = p.BoardZone.FirstOrDefault(m => m.Id != card.Id);
        Check(made != null && made.Card.Cost == 4 && made.Card.Type == CardType.MINION && made.Card.Collectible, "a 4-cost collectible minion took its place: " + made?.Card.Id);
        Check(p.BoardZone.Count == 2 && made.Id == yeti.Id && p.BoardZone[1] == made, "transformed in place, nothing extra: " + p.BoardZone.Count);
        Check(foe.Card.Id == "CS2_182" && foe.Zone.Type == Zone.PLAY, "the enemy minion is untouched");
        // random across seeds, all of cost 4, buffs and damage gone
        var seen = new HashSet<string>(); int same = 0;
        for (int seed = 1; seed <= 40; seed++)
        {
            var f = Fresh(seed);
            var y = Summon(f.Player1, "CS2_182"); y.Damage = 3; y[GameTag.ATK] = 9;
            Play(f, f.Player1, id, y);
            var m = f.Player1.BoardZone.First(x => x.Card.Id != id);
            Check(m.Card.Cost == 4 && m.Card.Type == CardType.MINION && m.Card.Collectible, "a collectible 4-cost minion: " + m.Card.Id);
            Check(m.Damage == 0 && m.AttackDamage == m.Card.ATK, "damage and buffs are gone: " + m.Card.Id);
            seen.Add(m.Card.Id); if (m.Card.Id == "CS2_182") same++;
        }
        Check(seen.Count >= 8, "a wide pool of 4-cost minions: " + seen.Count);
        // cost is read from the target: a 1-cost and a 7-cost minion
        foreach (var (target, cost) in new[] { ("CS2_189", 1), ("CS2_162", 6), ("GVG_121", 12) })
        {
            var f = Fresh(7);
            var t = Summon(f.Player1, target);
            Play(f, f.Player1, id, t);
            var m = f.Player1.BoardZone.First(x => x.Card.Id != id);
            Check(m.Card.Cost == cost && m.Card.Type == CardType.MINION, "same cost " + cost + ": " + m.Card.Id);
        }
        // no minion of that cost: nothing happens (an invented cost)
        {
            var f = Fresh(9);
            var odd = Summon(f.Player1, "CS2_182"); odd[GameTag.COST] = 99;
            Play(f, f.Player1, id, odd);
            Check(odd.Card.Id == "CS2_182" && odd.Zone.Type == Zone.PLAY, "an empty cost pool changes nothing");
        }
        // full board: the transform needs no slot
        Park(p); Park(opp);
        Fill(p, "CS2_182"); p.BoardZone[6].Zone.Remove(p.BoardZone[6]);
        var rec = Add(p, id);
        PlayEntity(g, p, rec, p.BoardZone[0]);
        Check(p.BoardZone.Count == 7, "a full board still transforms");
        // the played Recombobulator is not itself a target of its own battlecry
        Park(p);
        var only = Add(p, id);
        Check(Playable(p, only), "empty board is fine");
        // a Deathrattle minion transforms without dying
        Park(p);
        var harvest = Summon(p, "EX1_556");
        Play(g, p, id, harvest);
        Check(Count(p, "skele21") == 0, "transforming a Deathrattle minion does not trigger it");
        Resume(g, next =>
        {
            var c2 = Get(next, p); var t = Summon(c2, "CS2_182");
            Play(next, c2, id, t);
            Check(c2.BoardZone.Any(m => m.Card.Cost == 4 && m.Card.Id != id), "clone / snapshot: the battlecry still works");
        });
        // the opponent's Recombobulator transforms its own minion
        End(g);
        Park(opp);
        var theirY = Summon(opp, "CS2_182");
        Play(g, opp, id, theirY);
        Check(opp.BoardZone.Any(m => m.Card.Cost == 4 && m.Card.Id != id), "the opponent transforms its own minion");
    }

    // ---- GVG_109 Mini-Mage ------------------------------------------------------------------------------------------

    private static void MiniMage(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        var mini = (Minion)card;
        Check(card.Cost == 3 && mini.AttackDamage == 3 && mini.Health == 1, "a 3/1 for 3");
        PlayEntity(g, p, card);
        Check(mini.HasStealth && mini.SpellPower == 1, "Stealth and Spell Damage +1");
        var fireball = Add(p, "CS2_029");
        PlayEntity(g, p, fireball, opp.Hero);
        Check(opp.Hero.Damage == 7, "Fireball deals 6+1: " + opp.Hero.Damage);
        var second = (Minion)Play(g, p, id);
        PlayEntity(g, p, Add(p, "CS2_029"), opp.Hero);
        Check(opp.Hero.Damage == 7 + 8, "two Mini-Mages: 6+2");
        // Stealth rules
        End(g);
        var enemyFireball = Add(opp, "CS2_029");
        Check(!Targetable(opp, enemyFireball, mini), "the opponent cannot target a Stealthed Mini-Mage");
        var foe = Summon(opp, "CS2_182"); foe.IsExhausted = false;
        Check(!opp.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Target?.Id == mini.Id), "nor attack it");
        Check(!opp.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER && t.Target?.Id == mini.Id), "nor use a hero power on it");
        End(g);
        Check(Targetable(p, Add(p, "CS2_029"), mini), "its owner can target it");
        // the enemy side does not benefit from my Spell Damage
        End(g);
        int before = p.Hero.Damage;
        PlayEntity(g, opp, enemyFireball, p.Hero);
        Check(p.Hero.Damage == before + 6, "the opponent's Fireball gets no bonus from my Mini-Mage");
        End(g);
        // attacking breaks Stealth; Silence removes both
        mini.IsExhausted = false;
        Attack(g, mini, foe);
        Check(!mini.HasStealth && mini.SpellPower == 1, "attacking breaks Stealth but Spell Damage stays");
        Silence(g, second);
        Check(!second.HasStealth && second.SpellPower == 0, "Silence removes Stealth and Spell Damage");
        Resume(g, next => Check(Get(next, mini).Zone.Type == Zone.GRAVEYARD || Get(next, mini).SpellPower == 1, "clone keeps the state"));
        // dead Mini-Mage: no bonus
        Kill(g, mini);
        int h2 = opp.Hero.Damage;
        PlayEntity(g, p, Add(p, "CS2_029"), opp.Hero);
        Check(opp.Hero.Damage == h2 + 6, "no Spell Damage once it is gone");
        // the opponent's own Mini-Mage boosts the opponent
        End(g);
        var theirs = (Minion)Play(g, opp, id);
        int h3 = p.Hero.Damage;
        PlayEntity(g, opp, Add(opp, "CS2_029"), p.Hero);
        Check(theirs.SpellPower == 1 && p.Hero.Damage == h3 + 7, "the opponent's Mini-Mage boosts the opponent's spell");
    }

    // ---- GVG_110 Dr. Boom -------------------------------------------------------------------------------------------

    private static void DrBoom(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        var boom = (Minion)card;
        Check(card.Cost == 7 && boom.AttackDamage == 7 && boom.Health == 7, "a 7/7 for 7");
        var friend = Summon(p, "CS2_182");
        PlayEntity(g, p, card);
        var bots = p.BoardZone.Where(m => m.Card.Id == "GVG_110t").ToList();
        Check(bots.Count == 2 && bots.All(b => b.Card.Cost == 1 && b.AttackDamage == 1 && b.Health == 1 && b.IsRace(Race.MECHANICAL)), "two 1/1 Mech Boom Bots");
        Check(p.BoardZone.Count == 4 && p.BoardZone[boom.ZonePosition - 1].Card.Id == "GVG_110t" && p.BoardZone[boom.ZonePosition + 1].Card.Id == "GVG_110t",
            "one Bot on each side of Dr. Boom");
        // room for only one / none
        Park(p);
        Fill(p); p.BoardZone[6].Zone.Remove(p.BoardZone[6]); p.BoardZone[5].Zone.Remove(p.BoardZone[5]);
        var b1 = (Minion)Add(p, id); PlayEntity(g, p, b1);
        Check(p.BoardZone.Count == 7 && Count(p, "GVG_110t") == 1, "room for one Bot: " + Count(p, "GVG_110t"));
        Park(p);
        Fill(p); p.BoardZone[6].Zone.Remove(p.BoardZone[6]);
        var b2 = (Minion)Add(p, id); PlayEntity(g, p, b2);
        Check(p.BoardZone.Count == 7 && Count(p, "GVG_110t") == 0, "no room: no Bots");
        Park(p);
        // the Deathrattle: 1-4 damage to a random enemy; heroes and minions both, never my side
        var hits = new HashSet<int>(); bool heroHit = false, minionHit = false;
        for (int seed = 1; seed <= 80; seed++)
        {
            var f = Fresh(seed);
            var magi = Summon(f.Player1, "CS2_197");
            var ally = Dummy(f.Player1);
            var e1 = Dummy(f.Player2); var e2 = Dummy(f.Player2);
            Play(f, f.Player1, id);
            var bot = f.Player1.BoardZone.First(m => m.Card.Id == "GVG_110t");
            Kill(f, bot);
            int heroDamage = f.Player2.Hero.Damage, minionDamage = e1.Damage + e2.Damage;
            int total = heroDamage + minionDamage;
            Check(total >= 1 && total <= 4, "1-4 damage in one hit despite spell damage: " + total);
            Check(heroDamage == 0 || minionDamage == 0, "a single target");
            Check(f.Player1.Hero.Damage == 0 && ally.Damage == 0 && magi.Damage == 0, "never my own side");
            hits.Add(total); if (heroDamage > 0) heroHit = true; if (minionDamage > 0) minionHit = true;
        }
        Check(hits.SetEquals(new[] { 1, 2, 3, 4 }), "damage 1, 2, 3 and 4 all occur: " + string.Join(",", hits));
        Check(heroHit && minionHit, "the hero and minions are both possible targets");
        // Silence and clone
        var live = (Minion)Play(g, p, id);
        var silenced = p.BoardZone.First(m => m.Card.Id == "GVG_110t"); Silence(g, silenced);
        var target = Dummy(opp);
        Kill(g, silenced);
        Check(target.Damage == 0 && opp.Hero.Damage == 0, "a Silenced Bot does not explode");
        var other = p.BoardZone.First(m => m.Card.Id == "GVG_110t" && m.Id != silenced.Id);
        Resume(g, next => { Kill(next, Get(next, other)); Check(Get(next, opp).Hero.Damage + Get(next, target).Damage >= 1, "clone / snapshot: the Bot still explodes"); });
        // the opponent's Dr. Boom
        End(g);
        Park(opp);
        var theirs = (Minion)Play(g, opp, id);
        Check(Count(opp, "GVG_110t") == 2 && Count(p, "GVG_110t") >= 1, "the opponent's Bots appear on its side");
    }

    // ---- GVG_111 Mimiron's Head -------------------------------------------------------------------------------------

    private static Minion Voltron(Controller c) => c.BoardZone.FirstOrDefault(m => m.Card.Id == "GVG_111t");

    private static void MimironsHead(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var head = (Minion)Play(g, p, id);
        Check(head.Card.Cost == 5 && head.AttackDamage == 4 && head.Health == 5 && head.IsRace(Race.MECHANICAL), "a 4/5 Mech for 5");
        var mech1 = Dummy(p, "GVG_093");
        var yeti = Dummy(p);
        End(g); End(g);
        Check(Voltron(p) == null && head.Zone.Type == Zone.PLAY, "two Mechs (Head included): nothing");
        var mech2 = Dummy(p, "GVG_093"); var enemyMech = Dummy(opp, "GVG_093");
        End(g);
        Check(Voltron(p) == null && mech2.Zone.Type == Zone.PLAY, "at the start of the opponent's turn: nothing");
        Resume(g, next =>
        {
            End(next);
            Check(Voltron(Get(next, p)) != null && Get(next, head).Zone.Type == Zone.GRAVEYARD, "clone / snapshot: the transformation happens");
        });
        End(g);
        var v = Voltron(p);
        Check(v != null && v.Card.Cost == 8 && v.AttackDamage == 4 && v.Health == 8 && v.IsRace(Race.MECHANICAL) && v.HasCharge, "a 4/8 Charge Mech: V-07-TR-0N");
        Check(head.Zone.Type == Zone.GRAVEYARD && mech1.Zone.Type == Zone.GRAVEYARD && mech2.Zone.Type == Zone.GRAVEYARD, "all three Mechs are destroyed");
        Check(yeti.Zone.Type == Zone.PLAY && enemyMech.Zone.Type == Zone.PLAY, "the non-Mech and the enemy Mech stay");
        Check(p.BoardZone.Count == 2, "only the Yeti and V-07-TR-0N: " + p.BoardZone.Count);
        // Charge and Mega-Windfury: four attacks, no fifth
        var wall = enemyMech;
        for (int i = 0; i < 4; i++)
        {
            Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == v.Id && t.Target?.Id == wall.Id), "attack " + (i + 1) + " is available");
            Attack(g, v, wall);
        }
        Check(wall.Damage == 16, "four hits of 4: " + wall.Damage);
        Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == v.Id), "no fifth attack");
        Resume(g, next => Check(Get(next, v).Card.Id == "GVG_111t" && Get(next, v)[GameTag.MEGA_WINDFURY] > 0, "clone keeps Mega-Windfury"));
        Silence(g, v);
        Check(!v.HasCharge && v[GameTag.MEGA_WINDFURY] == 0, "Silence removes Charge and Mega-Windfury");
        // a Deathrattle Mech and a full board
        Park(p); Park(opp);
        var head2 = (Minion)Play(g, p, id);
        var shredder = Summon(p, "GVG_096"); var dummy2 = Summon(p, "GVG_093");
        Fill(p, "GVG_093");
        Check(p.BoardZone.IsFull, "a full board of Mechs");
        End(g); End(g);
        Check(Voltron(p) != null && head2.Zone.Type == Zone.GRAVEYARD, "the board empties first, so V-07-TR-0N forms even on a full board");
        Check(shredder.Zone.Type == Zone.GRAVEYARD && p.BoardZone.Count == 2, "the destroyed Piloted Shredder's Deathrattle also ran: " + p.BoardZone.Count);
        // two Heads: only one V-07-TR-0N
        Park(p); Park(opp);
        var hA = (Minion)Play(g, p, id); var hB = (Minion)Play(g, p, id); Summon(p, "GVG_093");
        End(g); End(g);
        Check(Count(p, "GVG_111t") == 1 && Count(p, id) == 0 && p.BoardZone.Count == 1, "two Heads and a Mech: one V-07-TR-0N: " + p.BoardZone.Count);
        // Silenced Head, Mech in hand, enemy Mechs: nothing
        Park(p); Park(opp);
        var quiet = (Minion)Play(g, p, id); Silence(g, quiet);
        Dummy(p, "GVG_093"); Dummy(p, "GVG_093");
        End(g); End(g);
        Check(quiet.Zone.Type == Zone.PLAY && p.BoardZone.Count == 3, "a Silenced Head leaves all three Mechs: " + p.BoardZone.Count);
        Park(p); Park(opp);
        var lone = (Minion)Play(g, p, id); Add(p, "GVG_093"); Add(p, "GVG_093");
        Dummy(opp, "GVG_093"); Dummy(opp, "GVG_093"); Dummy(opp, "GVG_093");
        End(g); End(g);
        Check(lone.Zone.Type == Zone.PLAY && Voltron(p) == null, "Mechs in hand and enemy Mechs do not count");
        // the opponent's Head acts at the start of the opponent's turn on its own Mechs
        Park(p); Park(opp);
        End(g);
        var theirHead = (Minion)Play(g, opp, id); Dummy(opp, "GVG_093"); Dummy(opp, "GVG_093");
        End(g);
        Check(Voltron(opp) == null, "my turn start does not trigger the opponent's Head");
        End(g);
        Check(Voltron(opp) != null && Voltron(p) == null, "at the start of the opponent's turn its own Head forms V-07-TR-0N");
        // control change: a stolen Head counts the new owner's Mechs at the start of the new owner's turn
        {
            var f = PriestGame();
            End(f);
            var stolen = (Minion)Play(f, f.Player2, id); Dummy(f.Player2, "GVG_093");
            End(f);
            MindControl(f, f.Player1, stolen);
            Check(Voltron(f.Player1) == null, "fixture: the Head and no other Mech of the new owner");
            Dummy(f.Player1, "GVG_093"); Dummy(f.Player1, "GVG_093");
            End(f); End(f);
            Check(Voltron(f.Player1) != null && Voltron(f.Player2) == null && f.Player2.BoardZone.Count == 1, "the new owner's Mechs (and the Head) become V-07-TR-0N; the old owner's Mech stays");
        }
    }

    // ---- GVG_112 Mogor the Ogre -------------------------------------------------------------------------------------

    private static void MogorTheOgre(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var attacker = Summon(p, "CS2_182"); attacker[GameTag.HEALTH] = 400;
        var a = Dummy(opp, "CS2_179"); var b = Dummy(opp);
        Equip(p);
        // without Mogor every attack lands where it was aimed
        var plain = Trials(g, 60, a, new ICharacter[] { b, opp.Hero }, () => Strike(g, attacker, a));
        Check(plain.Intended == 60, "no Mogor: always the target: " + plain.Intended);
        var mogor = (Minion)Play(g, p, id);
        Check(mogor.Card.Cost == 6 && mogor.AttackDamage == 7 && mogor.Health == 6, "a 7/6 for 6");
        // my minion: about half the attacks go astray (Taunt, hero included)
        var r = Trials(g, 240, a, new ICharacter[] { b, opp.Hero }, () => Strike(g, attacker, a));
        Check(r.Intended >= 95 && r.Intended <= 145, "about half the attacks hit the target: " + r.Intended + "/240");
        Check(r.Others[0] >= 30 && r.Others[1] >= 30, "the rest is spread over the other enemy characters, the hero included: " + r.Others[0] + "/" + r.Others[1]);
        // the hero's attack is not a minion attack
        var hero = Trials(g, 40, a, new ICharacter[] { b, opp.Hero }, () => { p.Hero.NumAttacksThisTurn = 0; p.Hero.IsExhausted = false; var task = p.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == a.Id); Check(task != null, "hero attack option: " + string.Join(",", p.Options().Select(o => o.PlayerTaskType + ":" + o.Target?.Card.Id))); Check(g.Process(task), "hero attack accepted"); p.Hero.Weapon.Damage = 0; });
        Check(hero.Intended == 40, "a hero attack is never redirected: " + hero.Intended);
        // only the hero to hit: nothing to redirect to
        Park(opp);
        var lone = Trials(g, 30, opp.Hero, new ICharacter[0], () => Strike(g, attacker, opp.Hero));
        Check(lone.Intended == 30, "with no other enemy the attack stays: " + lone.Intended);
        // the opponent's minions are confused too
        Park(opp);
        var a2 = Dummy(p, "CS2_179"); var b2 = Dummy(p);
        var theirAttacker = Summon(opp, "CS2_182"); theirAttacker[GameTag.HEALTH] = 400;
        Equip(p, "CS2_106"); p.Hero.Weapon.Durability = 8;
        End(g);
        var theirs = Trials(g, 240, a2, new ICharacter[] { b2, p.Hero, attacker, mogor }, () => { theirAttacker.Damage = 0; Strike(g, theirAttacker, a2); });
        Check(theirs.Intended >= 80 && theirs.Intended <= 160, "the opponent's attacks go astray too (five candidates): " + theirs.Intended + "/240");
        // two Mogors: independent flips
        End(g);
        Park(opp);
        a = Dummy(opp, "CS2_179"); b = Dummy(opp);
        var mogor2 = (Minion)Play(g, p, id);
        var two = Trials(g, 400, a, new ICharacter[] { b, opp.Hero }, () => Strike(g, attacker, a));
        Check(two.Intended >= 115 && two.Intended <= 185, "two Mogors flip independently: the target keeps about 37.5% (150/400): " + two.Intended);
        // Silence him, kill him: attacks are exact again
        Silence(g, mogor2);
        Check(Trials(g, 40, a, new ICharacter[] { b, opp.Hero }, () => Strike(g, attacker, a)).Intended < 40, "one active Mogor still flips");
        Silence(g, mogor);
        var calm = Trials(g, 60, a, new ICharacter[] { b, opp.Hero }, () => Strike(g, attacker, a));
        Check(calm.Intended == 60, "both Silenced: always the target: " + calm.Intended);
        var mogor3 = (Minion)Play(g, p, id);
        Kill(g, mogor3);
        var dead = Trials(g, 60, a, new ICharacter[] { b, opp.Hero }, () => Strike(g, attacker, a));
        Check(dead.Intended == 60, "a dead Mogor has no effect: " + dead.Intended);
        // clone and snapshot keep the effect
        var live = (Minion)Play(g, p, id);
        Resume(g, next =>
        {
            var att = Get(next, attacker); var aa = Get(next, a);
            var rr = Trials(next, 160, aa, new ICharacter[] { Get(next, b), Get(next, opp).Hero }, () => Strike(next, att, aa));
            Check(rr.Intended >= 50 && rr.Intended <= 110, "clone / snapshot: still about half: " + rr.Intended + "/160");
        });
    }

    // ---- GVG_113 Foe Reaper 4000 ------------------------------------------------------------------------------------

    private static void FoeReaper4000(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var reaper = (Minion)Play(g, p, id);
        Check(reaper.Card.Cost == 8 && reaper.AttackDamage == 6 && reaper.Health == 9 && reaper.IsRace(Race.MECHANICAL), "a 6/9 Mech for 8");
        Park(opp);
        var l = Summon(opp, "CS2_182"); var m = Summon(opp, "CS2_182"); var r = Summon(opp, "CS2_182");
        foreach (var x in new[] { l, m, r }) { x[GameTag.ATK] = 0; x[GameTag.HEALTH] = 30; }
        Strike(g, reaper, m);
        Check(l.Damage == 6 && m.Damage == 6 && r.Damage == 6, "the target and both neighbours take 6: " + l.Damage + "/" + m.Damage + "/" + r.Damage);
        Check(reaper.Damage == 0, "the neighbours do not hit back");
        foreach (var x in new[] { l, m, r }) x.Damage = 0;
        Strike(g, reaper, l);
        Check(l.Damage == 6 && m.Damage == 6 && r.Damage == 0, "left edge: target and one neighbour");
        foreach (var x in new[] { l, m, r }) x.Damage = 0;
        Strike(g, reaper, r);
        Check(l.Damage == 0 && m.Damage == 6 && r.Damage == 6, "right edge: target and one neighbour");
        foreach (var x in new[] { l, m, r }) x.Damage = 0;
        Strike(g, reaper, opp.Hero);
        Check(opp.Hero.Damage == 6 && l.Damage == 0 && m.Damage == 0 && r.Damage == 0, "attacking a hero damages no minion");
        opp.Hero.Damage = 0;
        // a Divine Shield neighbour pops, a weak neighbour dies, the target retaliates
        Park(opp);
        var weak = Summon(opp, "CS2_182"); var tgt = Summon(opp, "CS2_182"); var shield = Summon(opp, "CS2_182"); shield.HasDivineShield = true;
        weak[GameTag.HEALTH] = 4; tgt[GameTag.HEALTH] = 40; shield[GameTag.HEALTH] = 30;
        Strike(g, reaper, tgt);
        Check(weak.Zone.Type == Zone.GRAVEYARD && shield.Damage == 0 && !shield.HasDivineShield, "a weak neighbour dies, a Divine Shield neighbour only loses the shield");
        Check(tgt.Damage == 6 && reaper.Damage == tgt.AttackDamage, "only the attacked minion fights back: " + reaper.Damage);
        // clone / snapshot
        Park(opp);
        var cl = Summon(opp, "CS2_182"); var cm = Summon(opp, "CS2_182"); var cr = Summon(opp, "CS2_182");
        foreach (var x in new[] { cl, cm, cr }) { x[GameTag.ATK] = 0; x[GameTag.HEALTH] = 30; }
        Resume(g, next =>
        {
            Strike(next, Get(next, reaper), Get(next, cm));
            Check(Get(next, cl).Damage == 6 && Get(next, cr).Damage == 6, "clone / snapshot: still hits both neighbours");
        });
        // a Stealth neighbour is hit too; target dying does not shield the neighbours
        cl.HasStealth = true;
        cm[GameTag.HEALTH] = 5;
        Strike(g, reaper, cm);
        Check(cm.Zone.Type == Zone.GRAVEYARD && cl.Damage == 6 && cr.Damage == 6, "a Stealth neighbour is hit and a dying target does not spare the others");
        // Silence
        Park(opp);
        var sl = Summon(opp, "CS2_182"); var sm = Summon(opp, "CS2_182"); var sr = Summon(opp, "CS2_182");
        foreach (var x in new[] { sl, sm, sr }) { x[GameTag.ATK] = 0; x[GameTag.HEALTH] = 30; }
        Silence(g, reaper);
        Strike(g, reaper, sm);
        Check(sm.Damage == 6 && sl.Damage == 0 && sr.Damage == 0, "a Silenced Foe Reaper hits only its target");
        // the opponent's Foe Reaper
        Park(p); Park(opp);
        End(g);
        var theirReaper = (Minion)Play(g, opp, id);
        var pl = Dummy(p); var pm = Dummy(p); var pr = Dummy(p);
        Strike(g, theirReaper, pm);
        Check(pl.Damage == 6 && pm.Damage == 6 && pr.Damage == 6, "the opponent's Foe Reaper cleaves my board");
    }

    // ---- GVG_115 Toshley --------------------------------------------------------------------------------------------

    private static void Toshley(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        var toshley = (Minion)card;
        Check(card.Cost == 6 && toshley.AttackDamage == 5 && toshley.Health == 7, "a 5/7 for 6");
        PlayEntity(g, p, card);
        Check(Parts(p) == 1 && Parts(opp) == 0, "the battlecry adds one Spare Part to my hand");
        Concealed(g, p.HandZone.First(x => x.Card.Id.StartsWith("PART_")), "the battlecry Spare Part");
        EmptyHand(p);
        Kill(g, toshley);
        Check(Parts(p) == 1 && Parts(opp) == 0, "the Deathrattle adds another");
        Concealed(g, p.HandZone.First(x => x.Card.Id.StartsWith("PART_")), "the Deathrattle Spare Part");
        // full hand: the battlecry's card is the one made room for, the Deathrattle's is lost
        EmptyHand(p);
        var t2 = Add(p, id); FillHand(p);
        PlayEntity(g, p, t2);
        Check(p.HandZone.Count == 10 && Parts(p) == 1, "nine cards left, the Spare Part takes the tenth");
        Kill(g, (Minion)t2);
        Check(p.HandZone.Count == 10 && Parts(p) == 1, "a full hand: the Deathrattle gets nothing");
        // Silence: no Deathrattle, the battlecry already happened
        EmptyHand(p);
        var t3 = (Minion)Play(g, p, id); EmptyHand(p);
        Silence(g, t3); Kill(g, t3);
        Check(Parts(p) == 0, "a Silenced Toshley has no Deathrattle");
        // clone, then seeds for variety
        EmptyHand(p);
        var t4 = (Minion)Play(g, p, id);
        Resume(g, next => { var c2 = Get(next, p); EmptyHand(c2); Kill(next, Get(next, t4)); Check(Parts(c2) == 1, "clone / snapshot: the Deathrattle still works"); });
        var seen = new HashSet<string>();
        for (int seed = 1; seed <= 40; seed++)
        {
            var f = Fresh(seed); var t = (Minion)Play(f, f.Player1, id);
            seen.Add(f.Player1.HandZone.First(x => x.Card.Id.StartsWith("PART_")).Card.Id);
            EmptyHand(f.Player1);
            Kill(f, t);
            seen.Add(f.Player1.HandZone.First(x => x.Card.Id.StartsWith("PART_")).Card.Id);
        }
        Check(seen.Count >= 4, "at least four different Spare Parts: " + seen.Count);
        // the opponent's Toshley feeds the opponent
        End(g);
        EmptyHand(p); EmptyHand(opp);
        var theirs = (Minion)Play(g, opp, id);
        Check(Parts(opp) == 1 && Parts(p) == 0, "the opponent's Toshley feeds the opponent");
        EmptyHand(opp);
        Kill(g, theirs);
        Check(Parts(opp) == 1 && Parts(p) == 0, "and so does its Deathrattle");
    }

    // ---- GVG_116 Mekgineer Thermaplugg ------------------------------------------------------------------------------

    private static void MekgineerThermaplugg(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var plugg = (Minion)Play(g, p, id);
        Check(plugg.Card.Cost == 9 && plugg.AttackDamage == 9 && plugg.Health == 7 && plugg.IsRace(Race.MECHANICAL), "a 9/7 Mech for 9");
        var friend = Summon(p, "CS2_182"); var e1 = Summon(opp, "CS2_182"); var e2 = Summon(opp, "CS2_182"); var e3 = Summon(opp, "CS2_182");
        KillSettle(g, friend);
        Check(Count(p, "EX1_029") == 0, "a friendly minion dying: nothing");
        KillSettle(g, e1);
        Check(Count(p, "EX1_029") == 1 && p.BoardZone.Last().Card.Id == "EX1_029", "an enemy minion dying: a Leper Gnome on my side, on the right");
        Check(Count(opp, "EX1_029") == 0, "none for the opponent");
        // two at once
        e2.ToBeDestroyed = true; e3.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate(); g.DeathProcessingAndAuraUpdate();
        Check(Count(p, "EX1_029") == 3, "two enemy minions dying together: two more Gnomes: " + Count(p, "EX1_029"));
        // a dying enemy Leper Gnome also counts
        var theirGnome = Summon(opp, "EX1_029");
        KillSettle(g, theirGnome);
        Check(Count(p, "EX1_029") == 4, "an enemy Leper Gnome dying counts");
        // my own Gnome dying: nothing
        var mine = First(p, "EX1_029");
        KillSettle(g, mine);
        Check(Count(p, "EX1_029") == 3, "my Leper Gnome dying summons nothing");
        // full board: nothing
        Park(p); Park(opp);
        var full = (Minion)Play(g, p, id); Fill(p);
        var victim = Summon(opp, "CS2_182");
        KillSettle(g, victim);
        Check(p.BoardZone.Count == 7 && Count(p, "EX1_029") == 0, "a full board gets no Gnome");
        // Silence
        Park(p); Park(opp);
        var quiet = (Minion)Play(g, p, id); Silence(g, quiet);
        KillSettle(g, Summon(opp, "CS2_182"));
        Check(Count(p, "EX1_029") == 0, "a Silenced Thermaplugg does nothing");
        // clone / snapshot, combat death
        Park(p); Park(opp);
        var live = (Minion)Play(g, p, id);
        var biter = Summon(opp, "CS2_182"); biter[GameTag.HEALTH] = 4;
        Resume(g, next => { KillSettle(next, Get(next, biter)); Check(Count(Get(next, p), "EX1_029") == 1, "clone / snapshot: still listening"); });
        var hit = Summon(p, "CS2_182"); hit.IsExhausted = false;
        Attack(g, hit, biter); g.DeathProcessingAndAuraUpdate();
        Check(biter.Zone.Type == Zone.GRAVEYARD && Count(p, "EX1_029") == 1, "an enemy minion killed in combat counts");
        // the opponent's Thermaplugg summons on its side when my minions die
        End(g);
        Park(opp);
        var theirs = (Minion)Play(g, opp, id);
        KillSettle(g, Summon(p, "CS2_182"));
        Check(Count(opp, "EX1_029") == 1 && Count(p, "EX1_029") == 1, "the opponent's Thermaplugg answers my minion deaths on its side");
        // control change: a stolen Thermaplugg answers its new enemy's deaths
        {
            var f = PriestGame();
            End(f);
            var stolen = (Minion)Play(f, f.Player2, id);
            End(f);
            MindControl(f, f.Player1, stolen);
            var old = Summon(f.Player2, "CS2_182"); var own = Summon(f.Player1, "CS2_182");
            KillSettle(f, own);
            Check(Count(f.Player1, "EX1_029") == 0 && Count(f.Player2, "EX1_029") == 0, "its new owner's minion dying summons nothing");
            KillSettle(f, old);
            Check(Count(f.Player1, "EX1_029") == 1 && Count(f.Player2, "EX1_029") == 0, "the old owner's minion dying summons a Gnome for the new owner");
        }
    }

    // ---- GVG_117 Gazlowe --------------------------------------------------------------------------------------------

    private static void Gazlowe(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var gaz = (Minion)Play(g, p, id);
        Check(gaz.Card.Cost == 6 && gaz.AttackDamage == 3 && gaz.Health == 6, "a 3/6 for 6");
        EmptyHand(p); EmptyHand(opp);
        PlayEntity(g, p, Add(p, "CS2_075"));
        Check(p.HandZone.Count == 1, "a 1-Cost spell adds one card");
        var gift = p.HandZone[0];
        Check(gift.Card.Type == CardType.MINION && gift.Card.IsRace(Race.MECHANICAL) && gift.Card.Collectible, "a Mech minion: " + gift.Card.Id);
        Concealed(g, gift, "the Mech");
        Check(opp.HandZone.Count == 0, "nothing for the opponent");
        EmptyHand(p);
        PlayEntity(g, p, Add(p, "GAME_005"));
        Check(p.HandZone.Count == 0, "a 0-Cost spell (The Coin) does not count");
        var expensive = Add(p, "CS2_029");
        PlayEntity(g, p, expensive, opp.Hero);
        Check(p.HandZone.Count == 0, "a 4-Cost spell does not count");
        // current cost, not the printed one: three Sorcerer's Apprentices make a Mage's Fireball cost 1, one makes a Mirror Image cost 0
        {
            var f = Fresh(8, CardClass.MAGE, CardClass.ROGUE);
            Play(f, f.Player1, id); EmptyHand(f.Player1);
            Summon(f.Player1, "EX1_608");
            var mirror = Add(f.Player1, "CS2_027"); f.DeathProcessingAndAuraUpdate();
            Check(mirror.Cost == 0, "fixture: Mirror Image costs 0 with an Apprentice");
            PlayEntity(f, f.Player1, mirror);
            Check(f.Player1.HandZone.Count == 0, "a 1-Cost spell that costs 0 now does not count");
            Summon(f.Player1, "EX1_608"); Summon(f.Player1, "EX1_608");
            var cheapened = Add(f.Player1, "CS2_029"); f.DeathProcessingAndAuraUpdate();
            Check(cheapened.Cost == 1, "fixture: a Fireball that costs 1: " + cheapened.Cost);
            PlayEntity(f, f.Player1, cheapened, f.Player2.Hero);
            Check(f.Player1.HandZone.Count == 1 && f.Player1.HandZone[0].Card.IsRace(Race.MECHANICAL), "a spell that costs 1 now counts");
        }
        // not a spell: a 1-Cost minion
        PlayEntity(g, p, Add(p, "CS1_042"));
        Check(p.HandZone.Count == 0, "a 1-Cost minion does not count");
        // full hand: nothing is added, the spell is still played
        var spell = Add(p, "CS2_075"); FillHand(p);
        int before = opp.Hero.Damage;
        PlayEntity(g, p, spell);
        Check(p.HandZone.Count == 10 && opp.Hero.Damage == before + 3, "the spell is still played and the Mech takes the freed slot");
        // two Gazlowes
        EmptyHand(p);
        var gaz2 = (Minion)Play(g, p, id); EmptyHand(p);
        PlayEntity(g, p, Add(p, "CS2_075"));
        Check(p.HandZone.Count == 2 && p.HandZone.All(x => x.Card.IsRace(Race.MECHANICAL)), "two Gazlowes: two Mechs");
        // clone / snapshot
        EmptyHand(p);
        Resume(g, next => { var c2 = Get(next, p); EmptyHand(c2); PlayEntity(next, c2, Add(c2, "CS2_075")); Check(c2.HandZone.Count == 2, "clone / snapshot: still listening"); });
        // Silence
        Silence(g, gaz); Silence(g, gaz2);
        EmptyHand(p);
        PlayEntity(g, p, Add(p, "CS2_075"));
        Check(p.HandZone.Count == 0, "Silenced Gazlowes add nothing");
        // random Mechs across seeds
        var mechs = RandomCardTask.GetCardList(p.Hero, CardType.INVALID, CardClass.INVALID, CardSet.INVALID, Race.MECHANICAL);
        Check(mechs.Count >= 20 && mechs.All(c => c.Type == CardType.MINION && c.Collectible && c.IsRace(Race.MECHANICAL)), "every candidate is a collectible Mech minion: "
            + string.Join(",", mechs.Where(c => c.Type != CardType.MINION || !c.Collectible || !c.IsRace(Race.MECHANICAL)).Select(c => c.Id)));
        var seen = new HashSet<string>();
        for (int seed = 1; seed <= 40; seed++)
        {
            var f = Fresh(seed); Play(f, f.Player1, id); EmptyHand(f.Player1);
            PlayEntity(f, f.Player1, Add(f.Player1, "CS2_075"));
            Check(f.Player1.HandZone.Count == 1 && f.Player1.HandZone[0].Card.IsRace(Race.MECHANICAL) && f.Player1.HandZone[0].Card.Type == CardType.MINION, "a Mech minion");
            seen.Add(f.Player1.HandZone[0].Card.Id);
        }
        Check(seen.Count >= 8, "a wide Mech pool: " + seen.Count);
        // the opponent's 1-Cost spell does nothing for me; its own Gazlowe works for it
        End(g);
        EmptyHand(p); EmptyHand(opp);
        var mine = Summon(p, id);
        PlayEntity(g, opp, Add(opp, "CS2_027"));
        Check(p.HandZone.Count == 0, "the opponent's 1-Cost spell does not feed my Gazlowe");
        Play(g, opp, id); EmptyHand(opp);
        PlayEntity(g, opp, Add(opp, "CS2_027"));
        Check(opp.HandZone.Count == 1 && opp.HandZone[0].Card.IsRace(Race.MECHANICAL), "the opponent's own Gazlowe feeds the opponent");
        // control change: a stolen Gazlowe feeds its new owner
        {
            var f = PriestGame();
            End(f);
            var stolen = (Minion)Play(f, f.Player2, id);
            End(f);
            MindControl(f, f.Player1, stolen); EmptyHand(f.Player1); EmptyHand(f.Player2);
            PlayEntity(f, f.Player1, Add(f.Player1, "CS1_130"), f.Player2.Hero);
            Check(f.Player1.HandZone.Count == 1 && f.Player2.HandZone.Count == 0, "a 1-Cost spell of the new owner feeds the new owner");
        }
    }

    // ---- GVG_118 Troggzor the Earthinator ---------------------------------------------------------------------------

    private static void Troggzor(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var trog = (Minion)Play(g, p, id);
        Check(trog.Card.Cost == 7 && trog.AttackDamage == 6 && trog.Health == 6, "a 6/6 for 7");
        PlayEntity(g, p, Add(p, "CS2_075"));
        Check(Count(p, "GVG_068") == 0 && Count(opp, "GVG_068") == 0, "my own spell: nothing");
        End(g);
        PlayEntity(g, opp, Add(opp, "GAME_005"));
        Check(Count(p, "GVG_068") == 1 && Count(opp, "GVG_068") == 0, "the opponent casts The Coin: a Burly Rockjaw Trogg on MY side");
        var rock = First(p, "GVG_068");
        Check(rock.Card.Cost == 4 && rock.Health == 5, "a Burly Rockjaw Trogg");
        PlayEntity(g, opp, Add(opp, "CS2_029"), p.Hero);
        Check(Count(p, "GVG_068") == 2 && trog.Zone.Type == Zone.PLAY, "every spell, damaging or not, summons one: " + Count(p, "GVG_068"));
        // a hero power and a minion are not spells
        int rocks = Count(p, "GVG_068");
        opp.Hero.HeroPower.IsExhausted = false;
        var power = PowerOptions(opp).First(t => t.Target?.Id == opp.Hero.Id || t.Target != null);
        Check(g.Process(power), "hero power used");
        PlayEntity(g, opp, Add(opp, "CS2_182"));
        Check(Count(p, "GVG_068") == rocks, "a hero power and a minion do not summon");
        // a secret is a cast spell (a fresh game: a Counterspell on the board would counter my later casts)
        {
            var f = Fresh(4);
            Summon(f.Player1, id); End(f);
            Secret(f, f.Player2, "EX1_287");
            Check(Count(f.Player1, "GVG_068") == 1, "playing a Secret counts: " + Count(f.Player1, "GVG_068"));
        }
        // full board
        Park(p);
        Fill(p, "CS2_182");
        PlayEntity(g, opp, Add(opp, "GAME_005"));
        Check(p.BoardZone.Count == 7 && Count(p, "GVG_068") == 0, "a full board gets nothing");
        // Silence
        End(g);
        Park(p);
        var quiet = (Minion)Play(g, p, id); Silence(g, quiet);
        End(g);
        PlayEntity(g, opp, Add(opp, "GAME_005"));
        Check(Count(p, "GVG_068") == 0, "a Silenced Troggzor does nothing");
        // clone and snapshot, a dead Troggzor
        End(g);
        Park(p); Park(opp);
        var live = (Minion)Play(g, p, id);
        End(g);
        Resume(g, next => { PlayEntity(next, Get(next, opp), Add(Get(next, opp), "GAME_005")); Check(Count(Get(next, p), "GVG_068") == 1, "clone / snapshot: still listening"); });
        KillSettle(g, live);
        PlayEntity(g, opp, Add(opp, "GAME_005"));
        Check(Count(p, "GVG_068") == 0, "a dead Troggzor does nothing");
        // the opponent's Troggzor answers my spells on its own side
        Park(opp);
        var theirs = (Minion)Play(g, opp, id);
        End(g);
        PlayEntity(g, p, Add(p, "GAME_005"));
        Check(Count(opp, "GVG_068") == 1 && Count(p, "GVG_068") == 0, "the opponent's Troggzor answers my spell on its side: " + Count(opp, "GVG_068") + "/" + Count(p, "GVG_068"));
        // control change: a stolen Troggzor answers its new enemy's spells
        {
            var f = PriestGame();
            End(f);
            var stolen = (Minion)Play(f, f.Player2, id);
            End(f);
            MindControl(f, f.Player1, stolen);
            int mine0 = Count(f.Player1, "GVG_068"), theirs0 = Count(f.Player2, "GVG_068");
            Check(mine0 == 0 && theirs0 == 1, "fixture: casting Mind Control summoned a Trogg for the old owner, who still controlled Troggzor: " + mine0 + "/" + theirs0);
            PlayEntity(f, f.Player1, Add(f.Player1, "GAME_005"));
            Check(Count(f.Player1, "GVG_068") == 0 && Count(f.Player2, "GVG_068") == 1, "its new owner's spell summons nothing");
            End(f);
            PlayEntity(f, f.Player2, Add(f.Player2, "GAME_005"));
            Check(Count(f.Player1, "GVG_068") == 1 && Count(f.Player2, "GVG_068") == 1, "the old owner's spell summons a Trogg for the new owner");
        }
    }

    // ---- GVG_119 Blingtron 3000 -------------------------------------------------------------------------------------

    private static void Blingtron3000(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 5 && ((Minion)card).AttackDamage == 3 && ((Minion)card).Health == 4 && ((Minion)card).IsRace(Race.MECHANICAL), "a 3/4 Mech for 5");
        PlayEntity(g, p, card);
        Check(p.Hero.Weapon != null && opp.Hero.Weapon != null, "each player gets a weapon");
        Check(p.Hero.Weapon.Card.Type == CardType.WEAPON && p.Hero.Weapon.Card.Collectible && opp.Hero.Weapon.Card.Type == CardType.WEAPON && opp.Hero.Weapon.Card.Collectible,
            "collectible weapons: " + p.Hero.Weapon.Card.Id + ", " + opp.Hero.Weapon.Card.Id);
        Check(p.Hero.Weapon.Durability > 0 && opp.Hero.Weapon.Durability > 0 && p.Hero.Weapon.Controller == p && opp.Hero.Weapon.Controller == opp, "usable weapons, each on its own hero");
        // an equipped weapon is replaced
        var oldMine = p.Hero.Weapon; var oldTheirs = opp.Hero.Weapon;
        Park(p);
        var second = (Minion)Play(g, p, id);
        Check(p.Hero.Weapon != oldMine && opp.Hero.Weapon != oldTheirs, "both old weapons are replaced");
        Check(oldMine.Zone.Type != Zone.PLAY && oldTheirs.Zone.Type != Zone.PLAY, "the replaced weapons left play");
        var weapons = RandomCardTask.GetCardList(p.Hero, CardType.WEAPON, CardClass.INVALID);
        Check(weapons.Count >= 20 && weapons.All(c => c.Type == CardType.WEAPON && c.Collectible && c[GameTag.DURABILITY] > 0), "every candidate is a collectible weapon: "
            + string.Join(",", weapons.Where(c => c.Type != CardType.WEAPON || !c.Collectible || c[GameTag.DURABILITY] <= 0).Select(c => c.Id)));
        // random across seeds, independent picks, weapons of any class
        var mineSeen = new HashSet<string>(); var theirSeen = new HashSet<string>(); int differ = 0; var classes = new HashSet<CardClass>();
        for (int seed = 1; seed <= 40; seed++)
        {
            var f = Fresh(seed);
            Play(f, f.Player1, id);
            var a = f.Player1.Hero.Weapon.Card; var b = f.Player2.Hero.Weapon.Card;
            mineSeen.Add(a.Id); theirSeen.Add(b.Id); if (a.Id != b.Id) differ++; classes.Add(a.Class); classes.Add(b.Class);
        }
        Check(mineSeen.Count >= 6 && theirSeen.Count >= 6, "a varied weapon pool: " + mineSeen.Count + "/" + theirSeen.Count);
        Check(differ >= 10, "the two picks are independent: " + differ);
        Check(classes.Count >= 4, "weapons of several classes: " + classes.Count);
        // clone / snapshot, and the weapon works
        Resume(g, next => { var c2 = Get(next, p); Play(next, c2, id); Check(c2.Hero.Weapon != null && Get(next, opp).Hero.Weapon != null, "clone / snapshot: the battlecry still works"); });
        // opponent's Blingtron equips both as well
        End(g);
        oldMine = p.Hero.Weapon;
        var theirs = (Minion)Play(g, opp, id);
        Check(p.Hero.Weapon != oldMine && opp.Hero.Weapon != null, "the opponent's Blingtron gives both players a weapon");
        // a Mech with no weapon slot problem: hero with no weapon at all (fresh game, nothing equipped)
        var fresh = Fresh(3);
        Check(fresh.Player1.Hero.Weapon == null && fresh.Player2.Hero.Weapon == null, "fixture: no weapons");
    }

    // ---- GVG_120 Hemet Nesingwary -----------------------------------------------------------------------------------

    private static void HemetNesingwary(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        var hemet = (Minion)card;
        Check(card.Cost == 5 && hemet.AttackDamage == 6 && hemet.Health == 3 && !hemet.IsRace(Race.BEAST), "a 6/3 for 5");
        Check(Playable(p, card), "playable with no Beast: the battlecry fizzles");
        var yeti = Summon(p, "CS2_182"); var foeYeti = Summon(opp, "CS2_182");
        Check(Playable(p, card) && !p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id && t.Target != null), "non-Beasts are no targets");
        var mineBeast = Summon(p, "CS2_172"); var foeBeast = Summon(opp, "CS2_172"); var sneaky = Summon(opp, "CS2_172"); sneaky.HasStealth = true;
        var sheep = Summon(opp, "GVG_076");
        Check(Targetable(p, card, mineBeast) && Targetable(p, card, foeBeast) && Targetable(p, card, sheep), "any Beast, friendly or enemy, including a Mech Beast");
        Check(!Targetable(p, card, sneaky) && !Targetable(p, card, yeti) && !Targetable(p, card, foeYeti) && !Targetable(p, card, opp.Hero) && !Targetable(p, card, p.Hero),
            "not a Stealth Beast, a non-Beast or a hero");
        PlayEntity(g, p, card, foeBeast);
        Check(foeBeast.Zone.Type == Zone.GRAVEYARD && mineBeast.Zone.Type == Zone.PLAY && hemet.Zone.Type == Zone.PLAY, "the chosen Beast is destroyed, nothing else");
        // my own Beast, a Divine Shield Beast and a Deathrattle Beast
        var h2 = Add(p, id);
        PlayEntity(g, p, h2, mineBeast);
        Check(mineBeast.Zone.Type == Zone.GRAVEYARD, "my own Beast can be chosen");
        var shielded = Summon(opp, "CS2_172"); shielded.HasDivineShield = true;
        PlayEntity(g, p, Add(p, id), shielded);
        Check(shielded.Zone.Type == Zone.GRAVEYARD, "a Divine Shield does not stop it");
        var creeper = Summon(opp, "FP1_002");
        PlayEntity(g, p, Add(p, id), creeper);
        Check(creeper.Zone.Type == Zone.GRAVEYARD && Count(opp, "FP1_002t") == 2, "a Deathrattle Beast dies and its Deathrattle runs");
        // only Stealth Beasts: no target is available, the card still plays
        Park(p); Park(opp);
        var hidden = Summon(opp, "CS2_172"); hidden.HasStealth = true;
        var lone = Add(p, id);
        Check(Playable(p, lone) && !Targetable(p, lone, hidden), "only a Stealth Beast: no target, still playable");
        PlayEntity(g, p, lone);
        Check(hidden.Zone.Type == Zone.PLAY, "the Stealth Beast survives");
        // clone / snapshot
        var clone = Summon(p, "CS2_172");
        Resume(g, next => { var c2 = Get(next, p); var h = Add(c2, id); PlayEntity(next, c2, h, Get(next, clone)); Check(Get(next, clone).Zone.Type == Zone.GRAVEYARD, "clone / snapshot: the battlecry works"); });
        // the opponent's Hemet
        End(g);
        var mine = Summon(p, "CS2_172");
        var theirHemet = Add(opp, id);
        Check(Targetable(opp, theirHemet, mine), "the opponent can choose my Beast");
        PlayEntity(g, opp, theirHemet, mine);
        Check(mine.Zone.Type == Zone.GRAVEYARD, "and destroys it");
    }

    // ---- GVG_121 Clockwork Giant ------------------------------------------------------------------------------------

    private static void ClockworkGiant(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        EmptyHand(p); EmptyHand(opp);
        var giant = Add(p, id);
        var m = (Minion)giant;
        Check(m.Card.Cost == 12 && m.AttackDamage == 8 && m.Health == 8 && m.IsRace(Race.MECHANICAL), "an 8/8 Mech printed at 12");
        Check(giant.Cost == 12 && !Playable(p, giant), "an empty enemy hand: 12 Mana, not playable");
        for (int n = 1; n <= 10; n++)
        {
            Add(opp, "CS2_182"); g.DeathProcessingAndAuraUpdate();
            Check(giant.Cost == 12 - n, "cost with " + n + " enemy cards: " + giant.Cost);
        }
        Check(Playable(p, giant), "ten enemy cards: playable");
        // the discount follows the enemy hand, not mine
        Add(p, "CS2_182");
        Check(giant.Cost == 2, "my own hand does not matter");
        opp.HandZone.Remove(opp.HandZone[0]); g.DeathProcessingAndAuraUpdate();
        Check(giant.Cost == 3, "playing or losing an enemy card raises the cost again: " + giant.Cost);
        Resume(g, next => { var gc = Get(next, giant); Check(gc.Cost == 3, "clone / snapshot: same cost"); Add(Get(next, opp), "CS2_182"); next.DeathProcessingAndAuraUpdate(); Check(gc.Cost == 2, "clone / snapshot: follows the enemy hand"); });
        // playing pays the current cost and leaves an 8/8 on the board
        PlayEntity(g, p, giant);
        Check(m.Zone.Type == Zone.PLAY && m.Card.Cost == 12 && m.AttackDamage == 8, "on the board");
        // no negative cost, other Giants discounted too
        EmptyHand(opp); FillHand(opp); g.DeathProcessingAndAuraUpdate();
        var g2 = Add(p, id); var g3 = Add(p, id); g.DeathProcessingAndAuraUpdate();
        Check(g2.Cost == 2 && g3.Cost == 2, "each Giant in hand is discounted");
        EmptyHand(opp); g.DeathProcessingAndAuraUpdate();
        Check(g2.Cost == 12 && g3.Cost == 12, "back to 12 when the enemy hand is empty");
        // the opponent's Giant counts MY hand
        End(g);
        EmptyHand(p); EmptyHand(opp);
        var theirs = Add(opp, id);
        Check(theirs.Cost == 12, "my empty hand: the opponent's Giant costs 12");
        for (int n = 0; n < 4; n++) Add(p, "CS2_182");
        g.DeathProcessingAndAuraUpdate();
        Check(theirs.Cost == 8, "four cards in my hand: 8");
        FillHand(p); g.DeathProcessingAndAuraUpdate();
        Check(theirs.Cost == 2, "a full hand of mine: 2");
        PlayEntity(g, opp, theirs);
        Check(((Minion)theirs).Zone.Type == Zone.PLAY, "the opponent plays it");
    }

    // ---- GVG_122 Wee Spellstopper -----------------------------------------------------------------------------------

    private static bool PowerTargets(Controller c, ICharacter target) => PowerOptions(c).Any(t => t.Target?.Id == target.Id);

    private static void WeeSpellstopper(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var stopper = (Minion)Entity.FromCard(p, Cards.FromId(id));
        Check(stopper.Card.Cost == 4 && stopper.AttackDamage == 2 && stopper.Health == 5, "a 2/5 for 4");
        var left = Summon(p, "CS2_182"); var middle = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182"); var far = Summon(p, "CS2_182");
        // put it between the second and third minion
        Generic.SummonBlock(g, stopper, 2, null);
        g.DeathProcessingAndAuraUpdate();
        Check(stopper.Zone.Type == Zone.PLAY && p.BoardZone.Count == 5, "five minions");
        Check(stopper.ZonePosition == 2 && p.BoardZone[1] == middle && p.BoardZone[3] == right, "fixture: Spellstopper between middle and right");
        End(g);
        var fireball = Add(opp, "CS2_029");
        Check(!Targetable(opp, fireball, middle) && !Targetable(opp, fireball, right), "adjacent minions cannot be targeted by the opponent's spells");
        Check(Targetable(opp, fireball, left) && Targetable(opp, fireball, far) && Targetable(opp, fireball, stopper), "non-adjacent minions and the Spellstopper itself can");
        Check(!PowerTargets(opp, middle) && !PowerTargets(opp, right) && PowerTargets(opp, left) && PowerTargets(opp, stopper), "nor by hero powers (Fireblast)");
        // an AoE and attacks still reach them; battlecries too
        var foe = Summon(opp, "CS2_182"); foe.IsExhausted = false;
        Check(opp.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == foe.Id && t.Target?.Id == middle.Id), "attacks can still target them");
        PlayEntity(g, opp, Add(opp, "CS2_032"));
        Check(middle.Damage == 4 && right.Damage == 4, "an AoE still damages them");
        Resume(g, next => Check(!Targetable(Get(next, opp), Add(Get(next, opp), "CS2_029"), Get(next, middle)), "clone / snapshot: the adjacent minion is still Elusive"));
        End(g);
        // my own spells are blocked too, a hand card is not affected
        var mine = Add(p, "CS2_029");
        Check(!Targetable(p, mine, middle) && Targetable(p, mine, left), "Elusive blocks my own spells as well");
        // a minion that arrives next to it becomes Elusive; when the neighbour dies the next one is adjacent
        var joiner = Summon(p, "CS2_182");
        p.BoardZone.Remove(joiner); p.BoardZone.Add(joiner, stopper.ZonePosition);
        g.DeathProcessingAndAuraUpdate();
        Check(!Targetable(p, mine, joiner), "a minion that arrives next to it is Elusive");
        Check(Targetable(p, mine, far), "the far minion is not");
        KillSettle(g, joiner);
        KillSettle(g, middle);
        Check(stopper.ZonePosition == 1 && p.BoardZone[0] == left, "fixture: the Spellstopper moved next to the left minion");
        Check(!Targetable(p, mine, left), "after the neighbour died, the next minion on that side is adjacent and Elusive");
        Check(!Targetable(p, mine, right), "the right neighbour stays Elusive");
        // Silence the Spellstopper: the aura goes away
        Silence(g, stopper);
        Check(Targetable(p, mine, left) && Targetable(p, mine, right), "Silence ends the effect");
        // a fresh one, then its death
        Park(p);
        var a = Summon(p, "CS2_182"); var b = Summon(p, "CS2_182");
        var second = (Minion)Entity.FromCard(p, Cards.FromId(id));
        Generic.SummonBlock(g, second, 1, null);
        g.DeathProcessingAndAuraUpdate();
        var spell = Add(p, "CS2_029");
        Check(!Targetable(p, spell, a) && !Targetable(p, spell, b) && Targetable(p, spell, second), "a new Spellstopper protects both neighbours");
        KillSettle(g, second);
        Check(Targetable(p, spell, a) && Targetable(p, spell, b), "once it is dead, the minions are targetable again");
        // a Spellstopper on the edge protects its single neighbour; the opponent's one protects its own side only
        Park(p);
        var c = Summon(p, "CS2_182");
        var edge = (Minion)Entity.FromCard(p, Cards.FromId(id));
        Generic.SummonBlock(g, edge, 1, null);
        g.DeathProcessingAndAuraUpdate();
        var spell2 = Add(p, "CS2_029");
        Check(!Targetable(p, spell2, c) && Targetable(p, spell2, edge), "an edge Spellstopper protects its one neighbour");
        Park(opp);
        var oc = Summon(opp, "CS2_182");
        var theirs = (Minion)Entity.FromCard(opp, Cards.FromId(id));
        Generic.SummonBlock(g, theirs, 1, null);
        g.DeathProcessingAndAuraUpdate();
        Check(Targetable(p, spell2, oc) == false, "the opponent's Spellstopper makes its neighbour Elusive to my spells");
        Check(Targetable(p, spell2, c) == false && Targetable(p, spell2, theirs), "and it does not change my side");
        // control change: a stolen Spellstopper protects the neighbours on its new side only
        {
            var f = PriestGame();
            var mine1 = Summon(f.Player1, "CS2_182");
            End(f);
            var foe1 = Summon(f.Player2, "CS2_182");
            var theirStopper = (Minion)Entity.FromCard(f.Player2, Cards.FromId(id));
            Generic.SummonBlock(f, theirStopper, 1, null);
            f.DeathProcessingAndAuraUpdate();
            End(f);
            var spell3 = Add(f.Player1, "CS1_130");
            Check(!Targetable(f.Player1, spell3, foe1), "fixture: the Spellstopper protects its own neighbour");
            MindControl(f, f.Player1, theirStopper);
            f.DeathProcessingAndAuraUpdate(); f.Player1.UsedMana = 0;
            var spell4 = Add(f.Player1, "CS1_130");
            Check(Targetable(f.Player1, spell4, foe1), "the old neighbour is no longer adjacent to it and is targetable");
            Check(Targetable(f.Player1, spell4, theirStopper), "the Spellstopper itself is targetable");
        }
    }

    // ---- GVG_123 Soot Spewer ----------------------------------------------------------------------------------------

    private static void SootSpewer(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        EmptyHand(p); EmptyHand(opp);
        var card = Add(p, id);
        var spewer = (Minion)card;
        Check(card.Cost == 3 && spewer.AttackDamage == 3 && spewer.Health == 4 && spewer.IsRace(Race.MECHANICAL), "a 3/4 Mech for 3");
        PlayEntity(g, p, card);
        Check(spewer.SpellPower == 1, "Spell Damage +1 (the base amount is 1)");
        Check(p.HandZone.Count == 0, "no other Mech: no spell (it does not count itself)");
        PlayEntity(g, p, Add(p, "CS2_029"), opp.Hero);
        Check(opp.Hero.Damage == 7, "it boosts Fireball: " + opp.Hero.Damage);
        // an enemy Mech and a hand Mech do not count
        Park(p); Park(opp);
        Summon(opp, "GVG_093"); Add(p, "GVG_093"); EmptyHand(p);
        var second = Add(p, id);
        PlayEntity(g, p, second);
        Check(p.HandZone.Count == 0, "an enemy Mech does not count");
        // another friendly Mech: a Fire spell
        Park(p);
        var mech = Summon(p, "GVG_093"); Silence(g, mech);
        var third = Add(p, id);
        PlayEntity(g, p, third);
        Check(p.HandZone.Count == 1, "with another Mech (Silenced counts): one card");
        var gift = p.HandZone[0];
        Check(gift.Card.Type == CardType.SPELL && gift.Card.Collectible, "a collectible spell: " + gift.Card.Id);
        Concealed(g, gift, "the Fire spell");
        Check(opp.HandZone.Count == 0, "nothing for the opponent");
        // the first Soot Spewer is a Mech for the second
        EmptyHand(p); Park(p);
        var one = (Minion)Play(g, p, id);
        Check(p.HandZone.Count == 0, "the first alone: nothing");
        var two = (Minion)Play(g, p, id);
        Check(p.HandZone.Count == 1 && one.SpellPower == 1 && two.SpellPower == 1, "the second sees the first: a spell, and Spell Damage stacks");
        // the spell is a Fire spell across seeds, from many classes
        var seen = new HashSet<string>(); var classes = new HashSet<CardClass>();
        for (int seed = 1; seed <= 60; seed++)
        {
            var f = Fresh(seed); EmptyHand(f.Player1);
            Summon(f.Player1, "GVG_093");
            Play(f, f.Player1, id);
            Check(f.Player1.HandZone.Count == 1, "exactly one card");
            var c2 = f.Player1.HandZone[0].Card;
            Check(c2.Type == CardType.SPELL && c2.Collectible && (c2[GameTag.SPELL_SCHOOL] == 2 || FireSpells.Contains(c2.AssetId)), "a Fire spell: " + c2.Id);
            seen.Add(c2.Id); classes.Add(c2.Class);
        }
        Check(seen.Count >= 8 && classes.Count >= 3, "a wide Fire spell pool: " + seen.Count + " spells, " + classes.Count + " classes");
        // full hand
        EmptyHand(p);
        Park(p); Summon(p, "GVG_093");
        var fullCard = Add(p, id); FillHand(p);
        PlayEntity(g, p, fullCard);
        Check(p.HandZone.Count == 10, "a full hand: still ten");
        // Silence removes Spell Damage
        Silence(g, one);
        Check(one.SpellPower == 0, "Silence removes Spell Damage");
        Resume(g, next => { var c2 = Get(next, p); EmptyHand(c2); Play(next, c2, id); Check(c2.HandZone.Count == 1, "clone / snapshot: the battlecry works"); });
        // the opponent's Soot Spewer
        End(g);
        EmptyHand(opp); Park(opp);
        Summon(p, "GVG_093");
        Play(g, opp, id);
        Check(opp.HandZone.Count == 0, "my Mech does not help the opponent");
        Summon(opp, "GVG_093");
        Play(g, opp, id);
        Check(opp.HandZone.Count == 1 && opp.HandZone[0].Card.Type == CardType.SPELL, "its own Mech does");
    }

    // Fire spells of the 37.0 export whose engine definition predates spell schools (the same identities as Volcanic Thrasher).
    private static readonly HashSet<int> FireSpells = new HashSet<int> {
        286, 315, 394, 585, 864, 950, 974, 1004, 1087, 1640, 1927, 2284,
        2289, 2323, 2330, 2359, 2432, 2480, 2539, 2541, 2678, 2864, 2874, 2997,
        25271, 25396, 25401, 38413, 38935, 39715, 39747, 39854, 40005, 40066, 40297, 40316,
        40317, 40498, 40500, 40545, 40546, 40938, 41151, 41159, 41690, 41878, 43407, 43411,
        43412, 43414, 45271, 46286, 46425, 46437, 46715, 46953, 47251, 47414, 48393, 48824,
        49333, 50086, 50120, 50122, 51161, 51371, 51530, 52703, 53823, 54429, 55101, 55250,
        55276, 55858, 55859, 55860, 55861, 55935, 56170, 56173, 57316, 57483, 57484, 58970,
        69501, 69502,
    };

    // ---- unregistered scenarios (BLOCKED-STANDARD) --------------------------------------------------------------------

    private static void RunUnregistered()
    {
        foreach (var (id, scenario) in new (string, Action<Game, Controller, string>)[] { ("GVG_103", MicroMachine), ("GVG_114", SneedsOldShredder), ("GVG_112", MogorTheOgre) })
            try
            {
                for (int seed = 1; seed <= 3; seed++)
                {
                    var game = Fresh(seed);
                    scenario(game, game.Player1, id);
                    Check(Canonical.State(game) == Canonical.State(game.CloneExact(history: true)), "exact clone after unregistered scenario " + id);
                    Check(Canonical.State(game) == Canonical.State(Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools)), "snapshot after unregistered scenario " + id);
                }
                Console.WriteLine("UNREGISTERED scenario " + id + " PASS; seeds=3; clone/snapshot");
            }
            catch (Exception error) { Console.WriteLine("UNREGISTERED scenario " + id + " FAIL (script not changed, BLOCKED-STANDARD): " + error.Message); }
    }

    private static void MicroMachine(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var micro = (Minion)Play(g, p, id);
        Check(micro.Card.Cost == 2 && micro.AttackDamage == 1 && micro.Health == 2 && micro.IsRace(Race.MECHANICAL), "a 1/2 Mech for 2");
        End(g);
        Check(micro.AttackDamage == 2, "start of the opponent's turn: +1 Attack");
        End(g);
        Check(micro.AttackDamage == 3, "start of my turn: +1 Attack");
        End(g); End(g);
        Check(micro.AttackDamage == 5, "each turn: +1 Attack");
        Silence(g, micro);
        End(g); End(g);
        Check(micro.AttackDamage == 1, "Silenced: base Attack, no growth");
    }

    private static void SneedsOldShredder(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var shredder = (Minion)Play(g, p, id);
        // 37.0 costs 7 (legacy 8): the identity row is only written for registered ids, so the cost is not checked here.
        Check(shredder.AttackDamage == 5 && shredder.Health == 7 && shredder.IsRace(Race.MECHANICAL), "a 5/7 Mech");
        KillSettle(g, shredder);
        Check(p.BoardZone.Count == 1 && p.BoardZone[0].Card.Rarity == Rarity.LEGENDARY && p.BoardZone[0].Card.Collectible, "a Legendary minion on my side");
    }
}
