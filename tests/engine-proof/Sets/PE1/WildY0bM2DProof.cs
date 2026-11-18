using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Actions;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Wave Y0b, lane m2d: Goblins vs Gnomes GVG_076 .. GVG_100 (neutral Mechs and Gnomes, Anima Golem, Druid of the Fang, Steamwheedle Sniper, Ogre Ninja ...).
// Unregistered (not in the batch manifest), their scenarios kept below and run only with WILD_Y0B_M2D_UNREGISTERED=1:
//  - BLOCKED-STANDARD GVG_079 (data/pools/pool-eligibility-v1.json: dependency of the Standard module TIME_017),
//    GVG_082 and GVG_100 (data/pools/pool-eligibility-v1.json legacy rows), GVG_085 (plugin/LocalCardCatalog.cs CoreUnlock row and
//    data/progression/core-2026-unlocks.json copyGroup): Standard data uses them today. Their scripts are not changed here.
// HELD GVG_087 Steamwheedle Sniper (Your Hero Power can target minions): it needs the REQ_STEADY_SHOT targeting rule, which would change the
// Standard Steady Shot (DS1h_292, AT_132_HUNTER; a Wisprider played by a non-Hunter holding it). The script and the rule stay at base; the
// scenario below is kept for WILD_HELD=1 and FAILS until the card is released with a Standard golden audit.
internal static class WildY0bM2DProof
{
    [GroupProof("wild-y0b-m2d-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        WildSetsProof.Case("GVG_076", ExplosiveSheep);
        WildSetsProof.Case("GVG_077", AnimaGolem);
        WildSetsProof.Case("GVG_078", MechanicalYeti);
        WildSetsProof.Case("GVG_080", DruidOfTheFang);
        WildSetsProof.Case("GVG_081", GilblinStalker);
        WildSetsProof.Case("GVG_083", UpgradedRepairBot);
        WildSetsProof.Case("GVG_084", FlyingMachine);
        WildSetsProof.Case("GVG_086", SiegeEngine);
        WildSetsProof.Held("GVG_087", SteamwheedleSniper);
        WildSetsProof.Case("GVG_088", OgreNinja);
        WildSetsProof.Case("GVG_089", Illuminator);
        WildSetsProof.Case("GVG_090", MadderBomber);
        WildSetsProof.Case("GVG_091", ArcaneNullifier);
        WildSetsProof.Case("GVG_092", GnomishExperimenter);
        WildSetsProof.Case("GVG_093", TargetDummy);
        WildSetsProof.Case("GVG_094", Jeeves);
        WildSetsProof.Case("GVG_095", GoblinSapper);
        WildSetsProof.Case("GVG_096", PilotedShredder);
        WildSetsProof.Case("GVG_097", LilExorcist);
        WildSetsProof.Case("GVG_098", GnomereganInfantry);
        WildSetsProof.Case("GVG_099", BombLobber);
        if (Environment.GetEnvironmentVariable("WILD_Y0B_M2D_UNREGISTERED") == "1") RunUnregistered();
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

    private static void Resume(Game game, Action<Game> continuation)
    {
        foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) })
        {
            Check(Canonical.State(game) == Canonical.State(next), "m2d state before continuation");
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
            Check(intended.Damage > 0 ^ others.Any(o => o.Damage > 0), "an attack lands on exactly one of the candidates");
            if (intended.Damage > 0) hitIntended++;
            for (int k = 0; k < others.Length; k++) if (others[k].Damage > 0) hitOthers[k]++;
            NoCombatFlags(g, "after attack " + i);
        }
        intended.Damage = 0; foreach (var o in others) o.Damage = 0;
        return (hitIntended, hitOthers);
    }

    private static void Hit(Game g, ICharacter target, IPlayable source, int n) { target.TakeDamage(source, n); g.DeathProcessingAndAuraUpdate(); }
    private static void SetDamage(Game g, ICharacter target, int n) { target.Damage = n; g.DeathProcessingAndAuraUpdate(); }
    private static Minion Dummy(Controller c, string id = "CS2_182") { var m = Summon(c, id); m[GameTag.ATK] = 0; m[GameTag.HEALTH] = 20; return m; }
    private static void Armor(Game g, Controller c, int n) { c.Hero.GainArmor(Add(c, "CS2_182"), n); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate(); }

    /// <summary>Deck of five Yetis topped by the given ids, the last id on top.</summary>
    private static void Stack(Controller c, params string[] topLast)
    {
        foreach (var card in c.DeckZone.ToArray()) c.SetasideZone.Add(c.DeckZone.Remove(card));
        for (int i = 0; i < 5; i++) Entity.FromCard(c, Cards.FromId("CS2_182"), zone: c.DeckZone);
        foreach (var id in topLast) Entity.FromCard(c, Cards.FromId(id), zone: c.DeckZone);
    }

    private static Spell Secret(Game g, Controller owner, string id)
    {
        var secret = (Spell)Add(owner, id);
        PlayEntity(g, owner, secret);
        Check(secret.Zone.Type == Zone.SECRET && owner.SecretZone.Contains(secret), "secret fixture " + id);
        return secret;
    }

    /// <summary>Kill, then a second cleanup pass: the minion a Deathrattle summoned is still pending until the next death-processing step.</summary>
    private static void KillSettle(Game g, Minion m) { Kill(g, m); g.DeathProcessingAndAuraUpdate(); }
    private static void Silence(Game g, Minion m) { m.Silence(); g.DeathProcessingAndAuraUpdate(); }
    private static int Parts(Controller c) => c.HandZone.Count(x => x.Card.Id.StartsWith("PART_"));

    // ---- GVG_076 Explosive Sheep ------------------------------------------------------------------------------------

    private static void ExplosiveSheep(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var sheep = (Minion)Play(g, p, id);
        Check(sheep.Card.Cost == 2 && sheep.AttackDamage == 1 && sheep.Health == 1 && sheep.IsRace(Race.MECHANICAL) && sheep.IsRace(Race.BEAST), "a 1/1 Mech Beast for 2");
        var big = Summon(opp, "CS2_182"); var mine = Summon(p, "CS2_182"); var shielded = Summon(opp, "CS2_182"); shielded.HasDivineShield = true;
        var small = Summon(opp, "CS2_168"); var magi = Summon(p, "CS2_197");
        Check(magi.SpellPower == 1, "fixture: spell damage");
        Kill(g, sheep);
        Check(sheep.Zone.Type == Zone.GRAVEYARD, "the Sheep died");
        Check(big.Damage == 2 && mine.Damage == 2 && magi.Damage == 2, "2 damage to every minion, spell damage does not add: " + big.Damage + "/" + mine.Damage + "/" + magi.Damage);
        Check(shielded.Damage == 0 && !shielded.HasDivineShield, "Divine Shield absorbs the blast");
        Check(small.Zone.Type == Zone.GRAVEYARD, "a 1-health minion dies");
        Check(p.Hero.Damage == 0 && opp.Hero.Damage == 0, "heroes are not hit");
        // Silence: no Deathrattle
        var quiet = (Minion)Play(g, p, id);
        Silence(g, quiet);
        Kill(g, quiet);
        Check(big.Damage == 2 && mine.Damage == 2, "a Silenced Sheep does not blast");
        // two Sheep chain
        Park(p); Park(opp);
        var a = (Minion)Play(g, p, id); var b = (Minion)Play(g, p, id); var wall = Dummy(opp);
        Kill(g, a);
        Check(b.Zone.Type == Zone.GRAVEYARD && wall.Damage == 4, "the blast kills the other Sheep, which blasts again: " + wall.Damage);
        // killed in combat
        Park(p); Park(opp);
        var fighter = Summon(opp, "CS2_182"); var third = (Minion)Play(g, p, id); var bystander = Dummy(p);
        Resume(g, next => { Check(Get(next, third).Zone.Type == Zone.PLAY, "clone keeps the pending Deathrattle"); Kill(next, Get(next, third)); Check(Get(next, bystander).Damage == 2, "clone resolves the Deathrattle"); });
        third.IsExhausted = false; Attack(g, third, fighter);
        Check(third.Zone.Type == Zone.GRAVEYARD && fighter.Damage >= 2 && bystander.Damage == 2, "combat death resolves the blast");
        // the opponent's Sheep hits my side
        End(g);
        var m1 = Dummy(p); var theirs = (Minion)Play(g, opp, id);
        Kill(g, theirs);
        Check(m1.Damage == 2, "the opponent's Sheep hits my minions");
    }

    // ---- GVG_077 Anima Golem ----------------------------------------------------------------------------------------

    private static void AnimaGolem(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var golem = (Minion)Play(g, p, id);
        Check(golem.Card.Cost == 6 && golem.AttackDamage == 9 && golem.Health == 9 && golem.IsRace(Race.MECHANICAL), "a 9/9 Mech for 6");
        Dummy(opp);
        Resume(g, next => { End(next); Check(Get(next, golem).Zone.Type == Zone.GRAVEYARD, "clone: destroyed at the end of my turn"); });
        End(g);
        Check(golem.Zone.Type == Zone.GRAVEYARD, "alone at the end of my turn: destroyed, enemy minions do not count");
        // opponent's turn, own golem alone
        Park(opp);
        var theirs = (Minion)Play(g, opp, id);
        End(g);
        Check(theirs.Zone.Type == Zone.GRAVEYARD, "the opponent's Golem dies at the end of its own turn when alone");
        // a friend keeps it alive; at the end of the OPPONENT's turn it is checked again
        var keeper = Dummy(p);
        var again = (Minion)Play(g, p, id);
        End(g);
        Check(again.Zone.Type == Zone.PLAY && keeper.Zone.Type == Zone.PLAY, "not alone: survives my end of turn");
        Kill(g, keeper);
        Check(again.Zone.Type == Zone.PLAY, "the friend's death alone does not destroy it");
        End(g);
        Check(again.Zone.Type == Zone.GRAVEYARD, "alone at the end of the OPPONENT's turn: destroyed too");
        // two Golems keep each other
        Park(p); Park(opp);
        var one = (Minion)Play(g, p, id); var two = (Minion)Play(g, p, id);
        End(g);
        Check(one.Zone.Type == Zone.PLAY && two.Zone.Type == Zone.PLAY, "two Golems are not each other's only minion");
        End(g);
        Check(one.Zone.Type == Zone.PLAY && two.Zone.Type == Zone.PLAY, "and at the other end of turn too");
        Kill(g, two);
        End(g); End(g);
        Check(one.Zone.Type == Zone.GRAVEYARD, "the last one goes");
        // Silence
        Park(p);
        var quiet = (Minion)Play(g, p, id);
        Silence(g, quiet);
        End(g); End(g);
        Check(quiet.Zone.Type == Zone.PLAY, "a Silenced Golem stays");
        // enemy minions never make it "not alone"
        Park(p); Park(opp);
        var lone = (Minion)Play(g, p, id); Summon(opp, "CS2_182");
        End(g);
        Check(lone.Zone.Type == Zone.GRAVEYARD, "enemy minions do not count as company");
    }

    // ---- GVG_078 Mechanical Yeti ------------------------------------------------------------------------------------

    private static void MechanicalYeti(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var yeti = (Minion)Play(g, p, id);
        Check(yeti.Card.Cost == 4 && yeti.AttackDamage == 4 && yeti.Health == 5 && yeti.IsRace(Race.MECHANICAL), "a 4/5 Mech for 4");
        EmptyHand(p); EmptyHand(opp);
        Kill(g, yeti);
        Check(p.HandZone.Count == 1 && opp.HandZone.Count == 1, "each player gets one card: " + p.HandZone.Count + "/" + opp.HandZone.Count);
        Check(p.HandZone[0].Card.Id.StartsWith("PART_") && opp.HandZone[0].Card.Id.StartsWith("PART_"), "both are Spare Parts");
        Concealed(g, p.HandZone[0], "my Spare Part"); Concealed(g, opp.HandZone[0], "the opponent's Spare Part");
        // full hand: the owner gets nothing, the other side still does
        EmptyHand(p); EmptyHand(opp);
        var full = (Minion)Play(g, p, id);
        EmptyHand(opp); FillHand(p);
        Kill(g, full);
        Check(p.HandZone.Count == 10 && Parts(p) == 0 && opp.HandZone.Count == 1, "a full hand gets nothing, the other player does");
        EmptyHand(p); EmptyHand(opp); FillHand(opp);
        var full2 = (Minion)Play(g, p, id); EmptyHand(p);
        Kill(g, full2);
        Check(p.HandZone.Count == 1 && opp.HandZone.Count == 10 && Parts(opp) == 0, "and the other way round");
        // Silence
        EmptyHand(p); EmptyHand(opp);
        var quiet = (Minion)Play(g, p, id); EmptyHand(p);
        Silence(g, quiet); Kill(g, quiet);
        Check(p.HandZone.Count == 0 && opp.HandZone.Count == 0, "a Silenced Yeti gives nothing");
        Resume(g, next =>
        {
            var y = (Minion)Play(next, Get(next, p), id); EmptyHand(Get(next, p)); EmptyHand(Get(next, opp));
            Kill(next, y);
            Check(Get(next, p).HandZone.Count == 1 && Get(next, opp).HandZone.Count == 1, "clone / snapshot: the Deathrattle still works");
        });
        // the opponent's Yeti, on its turn: both players still get one
        End(g);
        EmptyHand(p); EmptyHand(opp);
        var theirs = (Minion)Play(g, opp, id); EmptyHand(opp);
        Kill(g, theirs);
        Check(p.HandZone.Count == 1 && opp.HandZone.Count == 1, "the opponent's Yeti also gives each player one");
        // random parts: independent picks across seeds, several different Spare Parts
        var mineSeen = new HashSet<string>(); var theirSeen = new HashSet<string>(); int differ = 0;
        for (int seed = 1; seed <= 30; seed++)
        {
            var f = Fresh(seed); var y = (Minion)Play(f, f.Player1, id); EmptyHand(f.Player1); EmptyHand(f.Player2);
            Kill(f, y);
            var a = f.Player1.HandZone[0].Card.Id; var b = f.Player2.HandZone[0].Card.Id;
            mineSeen.Add(a); theirSeen.Add(b); if (a != b) differ++;
        }
        Check(mineSeen.Count >= 3 && theirSeen.Count >= 3, "at least three different Spare Parts each: " + mineSeen.Count + "/" + theirSeen.Count);
        Check(differ >= 5, "the two picks are independent: " + differ);
    }

    // ---- GVG_080 Druid of the Fang ----------------------------------------------------------------------------------

    private static Minion Cobra(Controller c) => c.BoardZone.FirstOrDefault(m => m.Card.Id == "GVG_080t");

    private static void DruidOfTheFang(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 5 && ((Minion)card).AttackDamage == 4 && ((Minion)card).Health == 4, "a 4/4 for 5");
        PlayEntity(g, p, card);
        Check(Cobra(p) == null && ((Minion)card).Zone.Type == Zone.PLAY && ((Minion)card).AttackDamage == 4, "no Beast: stays a 4/4");
        // enemy Beast, a Beast in hand, a dead Beast: none count
        Summon(opp, "CS2_172");
        Add(p, "CS2_172");
        var dead = Summon(p, "CS2_172"); Kill(g, dead);
        var second = Play(g, p, id);
        Check(Cobra(p) == null && p.BoardZone.Count(m => m.Card.Id == id) == 2, "an enemy Beast, a Beast in hand and a dead Beast do not count");
        // a friendly Beast transforms it
        var beast = Summon(p, "CS2_172");
        var third = Play(g, p, id);
        var cobra = Cobra(p);
        Check(cobra != null && cobra.AttackDamage == 7 && cobra.Health == 7 && cobra.Card.Cost == 5 && cobra.IsRace(Race.BEAST), "with a Beast it becomes a 7/7 Beast");
        Check(p.BoardZone.Count == 4 && p.BoardZone.Count(m => m.Card.Id == id) == 2, "one Druid replaced, the other two untouched: " + p.BoardZone.Count);
        Check(third.Zone.Type != Zone.PLAY || third.Card.Id != id, "the played minion is gone as a Druid");
        Concealed(g, Add(p, id), "a Druid in hand");
        // the new Cobra is a Beast: the next Druid transforms too, even without the first Beast
        Kill(g, beast);
        Play(g, p, id);
        Check(p.BoardZone.Count(m => m.Card.Id == "GVG_080t") == 2, "the 7/7 is itself a Beast");
        // Silence the token: base stats stay
        cobra.Silence();
        Check(cobra.AttackDamage == 7 && cobra.Health == 7, "Silence keeps the transformed base stats");
        // Silenced Beast still counts; Mech Beast (Explosive Sheep) counts
        Park(p);
        var silencedBeast = Summon(p, "CS2_172"); silencedBeast.Silence();
        Play(g, p, id);
        Check(Cobra(p) != null, "a Silenced Beast is still a Beast");
        Park(p);
        Summon(p, "GVG_076");
        Play(g, p, id);
        Check(Cobra(p) != null, "a Mech Beast counts");
        // a Beast that gets Silenced cannot lose its race; a Murloc is no Beast
        Park(p);
        Summon(p, "CS2_168");
        Play(g, p, id);
        Check(Cobra(p) == null, "a Murloc is not a Beast");
        // the board is full except the Druid slot: a transform needs no extra room
        Park(p); Fill(p, "CS2_172"); p.BoardZone[6].Zone.Remove(p.BoardZone[6]);
        Check(p.BoardZone.Count == 6, "fixture: six Beasts");
        Play(g, p, id);
        Check(p.BoardZone.Count == 7 && Cobra(p) != null, "full board still transforms");
        // damaged Cobra
        var cobra2 = Cobra(p); Hit(g, cobra2, Summon(opp, "CS2_182"), 3);
        Check(cobra2.Health == 4 || cobra2.Damage == 3, "the 7/7 is a normal minion: damage 3");
        Resume(g, next => Check(Cobra(Get(next, p)) != null && Cobra(Get(next, p)).AttackDamage == 7, "clone keeps the Cobra"));
        // the opponent's Druid looks at the opponent's Beasts
        End(g);
        Park(p); Park(opp);
        Summon(p, "CS2_172");
        Play(g, opp, id);
        Check(Cobra(opp) == null, "my Beast does not transform the opponent's Druid");
        Summon(opp, "CS2_172");
        Play(g, opp, id);
        Check(Cobra(opp) != null, "its own Beast does");
    }

    // ---- GVG_081 Gilblin Stalker ------------------------------------------------------------------------------------

    private static void GilblinStalker(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var stalker = (Minion)Play(g, p, id);
        Check(stalker.Card.Cost == 2 && stalker.AttackDamage == 2 && stalker.Health == 3 && stalker.HasStealth && !stalker.IsRace(Race.MURLOC), "a 2/3 Stealth for 2");
        var enemy = Dummy(opp);
        var fire = Add(p, "CS2_029");
        Check(Targetable(p, fire, stalker), "my own spells can target it");
        End(g);
        var fireball = Add(opp, "CS2_029");
        Check(!Targetable(opp, fireball, stalker), "the opponent cannot target it with a spell");
        enemy.IsExhausted = false;
        Check(!opp.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Target?.Id == stalker.Id), "and cannot attack it");
        Check(!opp.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER && t.Target?.Id == stalker.Id), "nor use a hero power on it");
        PlayEntity(g, opp, Add(opp, "CS2_032"));
        Check(stalker.Damage == 4 || stalker.Zone.Type == Zone.GRAVEYARD, "an AoE still hits it");
        End(g);
        var again = (Minion)Play(g, p, id);
        Check(again.HasStealth, "Stealth stays across turns");
        again.IsExhausted = false;
        Attack(g, again, enemy);
        Check(!again.HasStealth, "attacking breaks Stealth");
        var quiet = (Minion)Play(g, p, id); Silence(g, quiet);
        Check(!quiet.HasStealth && quiet.AttackDamage == 2, "Silence removes Stealth");
        Resume(g, next => Check(Get(next, quiet).Zone.Type == Zone.PLAY && !Get(next, quiet).HasStealth, "clone keeps state"));
    }

    // ---- GVG_083 Upgraded Repair Bot --------------------------------------------------------------------------------

    private static void UpgradedRepairBot(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var bot = Add(p, id);
        Check(bot.Cost == 5 && ((Minion)bot).AttackDamage == 5 && ((Minion)bot).Health == 5 && ((Minion)bot).IsRace(Race.MECHANICAL), "a 5/5 Mech for 5");
        Check(Playable(p, bot), "playable with no target");
        var mech = Summon(p, "GVG_093"); var plain = Summon(p, "CS2_182"); var enemyMech = Summon(opp, "GVG_093");
        Check(Targetable(p, bot, mech) && !Targetable(p, bot, plain) && !Targetable(p, bot, enemyMech) && !Targetable(p, bot, opp.Hero) && !Targetable(p, bot, p.Hero),
            "only a friendly Mech is a target");
        PlayEntity(g, p, bot, mech);
        Check(mech.Health == 6 && mech.AttackDamage == 0 && enemyMech.Health == 2, "+4 Health on the target only");
        Check(((Minion)bot).Health == 5, "the Bot itself is not buffed");
        // the Bot is a Mech and can be the target of the next one
        var second = Add(p, id);
        Check(Targetable(p, second, (Minion)bot), "another Repair Bot can buff the first");
        PlayEntity(g, p, second, (Minion)bot);
        Check(((Minion)bot).Health == 9 && ((Minion)bot).AttackDamage == 5, "5/9");
        // damaged Mech: current health rises along with the max
        Hit(g, mech, Summon(opp, "CS2_182"), 3);
        Check(mech.Health == 3, "fixture: damaged");
        PlayEntity(g, p, Add(p, id), mech);
        Check(mech.Health == 7 && mech.Damage == 3, "the buff stacks on a damaged Mech: " + mech.Health);
        Resume(g, next => Check(Get(next, mech).Health == 7, "clone keeps the buff"));
        var fresh = Summon(p, "GVG_093");
        PlayEntity(g, p, Add(p, id), fresh);
        Check(fresh.Health == 6, "fixture buffed");
        Silence(g, fresh);
        Check(fresh.Health == 2, "Silence removes the Health");
        // stealth Mech is a legal friendly target; no Mech: still playable
        Park(p);
        Summon(p, "CS2_182");
        var lonely = Add(p, id);
        Check(Playable(p, lonely), "no Mech: the battlecry fizzles");
        PlayEntity(g, p, lonely);
        // the opponent
        End(g);
        var theirsMech = Summon(opp, "GVG_093");
        var theirs = Add(opp, id);
        Check(Targetable(opp, theirs, theirsMech) && !Targetable(opp, theirs, mech), "the opponent buffs its own Mech");
        PlayEntity(g, opp, theirs, theirsMech);
        Check(theirsMech.Health == 6 && mech.Health == 7, "buff landed on its own Mech");
    }

    // ---- GVG_084 Flying Machine -------------------------------------------------------------------------------------

    private static void FlyingMachine(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var machine = (Minion)Play(g, p, id);
        Check(machine.Card.Cost == 3 && machine.AttackDamage == 1 && machine.Health == 4 && machine.HasWindfury && machine.IsRace(Race.MECHANICAL), "a 1/4 Windfury Mech for 3");
        var wall = Dummy(opp);
        End(g); End(g);
        machine.IsExhausted = false; machine.NumAttacksThisTurn = 0;
        Attack(g, machine, wall);
        Check(!machine.IsExhausted && wall.Damage == 1, "after one attack it can attack again");
        var second = p.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == machine.Id && t.Target?.Id == opp.Hero.Id);
        Check(second != null && g.Process(second) && opp.Hero.Damage == 1, "the second attack goes to the hero");
        Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == machine.Id), "no third attack");
        End(g); End(g);
        Resume(g, next => Check(Get(next, machine).HasWindfury, "clone keeps Windfury"));
        Silence(g, machine);
        Check(!machine.HasWindfury, "Silence removes Windfury");
        machine.IsExhausted = false; machine.NumAttacksThisTurn = 0;
        Attack(g, machine, wall);
        Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == machine.Id), "a Silenced Flying Machine attacks once");
        End(g);
        var theirs = (Minion)Play(g, opp, id);
        End(g); End(g);
        Check(theirs.HasWindfury, "the opponent's copy has Windfury too");
    }

    // ---- GVG_086 Siege Engine ---------------------------------------------------------------------------------------

    private static void SiegeEngine(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var engine = (Minion)Play(g, p, id);
        Check(engine.Card.Cost == 5 && engine.AttackDamage == 5 && engine.Health == 5 && engine.IsRace(Race.MECHANICAL), "a 5/5 Mech for 5");
        Armor(g, p, 3);
        Check(engine.AttackDamage == 6 && p.Hero.Armor == 3, "gaining Armor: +1 Attack");
        Armor(g, p, 5);
        Check(engine.AttackDamage == 7 && p.Hero.Armor == 8, "+1 per gain, not per point");
        p.Hero.TakeDamage(Add(opp, "CS2_182"), 4); g.DeathProcessingAndAuraUpdate();
        Check(engine.AttackDamage == 7 && p.Hero.Armor == 4, "losing Armor gives nothing");
        Armor(g, opp, 4);
        Check(engine.AttackDamage == 7 && opp.Hero.Armor == 4, "the enemy's Armor does not count");
        Play(g, p, "GVG_053");
        Check(engine.AttackDamage == 8 && p.Hero.Armor == 9, "a Shieldmaiden battlecry counts");
        var other = (Minion)Play(g, p, id);
        Armor(g, p, 1);
        Check(engine.AttackDamage == 9 && other.AttackDamage == 6, "every Siege Engine reacts");
        Resume(g, next => { Armor(next, Get(next, p), 2); Check(Get(next, engine).AttackDamage == 10, "clone keeps listening"); });
        Silence(g, engine);
        Check(engine.AttackDamage == 5, "Silence removes the Attack");
        Armor(g, p, 2);
        Check(engine.AttackDamage == 5 && other.AttackDamage == 7, "a Silenced Engine no longer reacts");
        Kill(g, other);
        Armor(g, p, 2);
        End(g);
        var theirs = (Minion)Play(g, opp, id);
        Armor(g, p, 2); Armor(g, opp, 1);
        Check(theirs.AttackDamage == 6, "the opponent's Engine reacts to its own Armor only: " + theirs.AttackDamage);
    }

    // ---- GVG_087 Steamwheedle Sniper --------------------------------------------------------------------------------

    private static IEnumerable<PlayerTask> PowerOptions(Controller c) => c.Options().Where(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER);

    private static void UsePower(Game g, Controller c, ICharacter target)
    {
        c.BaseMana = 10; c.UsedMana = 0;
        var task = PowerOptions(c).FirstOrDefault(t => t.Target?.Id == target?.Id);
        Check(task != null && g.Process(task), "hero power accepted on " + (target?.Card.Id ?? "no target"));
    }

    private static void SteamwheedleSniper(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 2 && ((Minion)card).AttackDamage == 2 && ((Minion)card).Health == 3, "a 2/3 for 2");
        PlayEntity(g, p, card);
        var sniper = (Minion)card;
        // a hero power that is not Steady Shot is unaffected (Rogue: no target; the Mage's Fireblast already takes any target)
        p.Hero.HeroPower.IsExhausted = false;
        Check(PowerOptions(p).Any() && PowerOptions(p).All(t => t.Target == null), "the Rogue hero power is still untargeted");
        // Hunter games
        for (int seed = 1; seed <= 2; seed++)
        {
            var f = Fresh(seed, CardClass.HUNTER, CardClass.MAGE);
            var me = f.Player1; var them = f.Player2;
            Check(me.Hero.HeroPower.Card.Id == "DS1h_292", "fixture: Steady Shot");
            var a = Dummy(them); var friend = Dummy(me); var sneaky = Summon(them, "EX1_010"); var elusive = Summon(them, "GVG_091");
            Check(PowerOptions(me).Count() == 1 && PowerOptions(me).Single().Target == null, "without Sniper: one untargeted hero power");
            UsePower(f, me, null);
            Check(them.Hero.Damage == 2 && a.Damage == 0, "Steady Shot hits the enemy hero");
            End(f); End(f);
            var s = (Minion)Play(f, me, id);
            var targets = PowerOptions(me).Select(t => t.Target?.Id ?? -1).ToList();
            Check(!targets.Contains(-1), "with Sniper the hero power needs a target");
            Check(targets.Contains(them.Hero.Id) && targets.Contains(a.Id) && targets.Contains(friend.Id) && targets.Contains(s.Id), "enemy hero and any minion are targets");
            Check(!targets.Contains(me.Hero.Id), "not my own hero");
            Check(!targets.Contains(sneaky.Id), "Stealth minions stay hidden");
            Check(!targets.Contains(elusive.Id), "Elusive minions stay hidden");
            Resume(f, next => Check(PowerOptions(Get(next, me)).Any(t => t.Target?.Id == a.Id), "clone keeps the option"));
            UsePower(f, me, a);
            Check(a.Damage == 2 && them.Hero.Damage == 2, "Steady Shot hits the chosen minion");
            End(f); End(f);
            var weak = Summon(them, "CS2_231");
            UsePower(f, me, weak);
            Check(weak.Zone.Type == Zone.GRAVEYARD && them.Hero.Damage == 2, "it can kill a minion");
            End(f); End(f);
            UsePower(f, me, them.Hero);
            Check(them.Hero.Damage == 4, "or still hit the hero");
            // Taunt does not restrict hero powers
            var taunt = Dummy(them, "CS2_179");
            End(f); End(f);
            Check(PowerOptions(me).Any(t => t.Target?.Id == a.Id) && PowerOptions(me).Any(t => t.Target?.Id == taunt.Id), "Taunt is ignored by hero powers");
            // Silence or death of the Sniper restores the plain hero power
            s.Silence(); f.DeathProcessingAndAuraUpdate();
            Check(PowerOptions(me).Count() == 1 && PowerOptions(me).Single().Target == null, "Silenced Sniper: plain Steady Shot again");
            UsePower(f, me, null);
            Check(them.Hero.Damage == 6, "plain Steady Shot hits the hero");
            End(f); End(f);
            var s2 = (Minion)Play(f, me, id);
            Kill(f, s2);
            Check(PowerOptions(me).Single().Target == null, "dead Sniper: plain Steady Shot again");
            // the opponent's Sniper does not help me
            End(f);
            var s3 = (Minion)Play(f, them, id);
            Check(PowerOptions(them).All(t => t.Target != null) && PowerOptions(them).Any(t => t.Target?.Id == friend.Id), "the Mage opponent still has targets (Fireblast takes any)");
            End(f);
            Kill(f, s3);
        }
        // Hunter against Hunter: only the Sniper's owner gets targets
        {
            var f = Fresh(5, CardClass.HUNTER, CardClass.HUNTER);
            var mine = Dummy(f.Player1);
            End(f);
            Play(f, f.Player2, id);
            Check(PowerOptions(f.Player2).Any() && PowerOptions(f.Player2).All(t => t.Target != null), "their Sniper makes their hero power targeted");
            End(f);
            Check(PowerOptions(f.Player1).Count() == 1 && PowerOptions(f.Player1).Single().Target == null, "but not mine");
            var theirMinion = Dummy(f.Player2);
            Check(!PowerOptions(f.Player1).Any(t => t.Target?.Id == theirMinion.Id), "no minion targets for the other Hunter");
        }
    }

    // ---- GVG_088 Ogre Ninja -----------------------------------------------------------------------------------------

    private static void OgreNinja(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var ninja = (Minion)Play(g, p, id);
        Check(ninja.Card.Cost == 5 && ninja.AttackDamage == 6 && ninja.Health == 6 && ninja.HasStealth, "a 6/6 Stealth for 5");
        var enemy = Dummy(opp);
        End(g);
        Check(!opp.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Target?.Id == ninja.Id), "Stealth: the enemy cannot attack it");
        End(g);
        for (int i = 0; i < 20; i++) { opp.Hero.Damage = 0; ninja.Damage = 0; ninja.HasStealth = true; Park(opp); Strike(g, ninja, opp.Hero); Check(opp.Hero.Damage == 6, "the hero is the only enemy: the attack stays"); Check(!ninja.HasStealth, "attacking breaks Stealth"); }
        var a = Dummy(opp); var b = Dummy(opp); var c = Dummy(opp);
        var (intended, others) = Trials(g, 100, a, new ICharacter[] { b, c, opp.Hero }, () => Strike(g, ninja, a));
        Check(intended >= 30 && intended <= 70, "about half the attacks reach the intended target: " + intended);
        Check(others.All(x => x >= 3), "every other enemy, hero included, is a candidate: " + string.Join("/", others));
        var (hi, ho) = Trials(g, 100, opp.Hero, new ICharacter[] { a, b, c }, () => Strike(g, ninja, opp.Hero));
        Check(hi >= 30 && hi <= 70 && ho.All(x => x >= 3), "hero as intended target: " + hi + "/" + string.Join("/", ho));
        Park(opp);
        var taunt = Dummy(opp, "CS2_179"); var plain = Dummy(opp); var sneaky = Dummy(opp, "EX1_010");
        var (ti, to) = Trials(g, 100, taunt, new ICharacter[] { plain, sneaky, opp.Hero }, () => Strike(g, ninja, taunt));
        Check(ti >= 30 && ti <= 70 && to.All(x => x >= 3), "Taunt and Stealth do not limit the wrong target: " + ti + "/" + string.Join("/", to));
        Park(opp);
        var x1 = Dummy(opp); var x2 = Dummy(opp);
        Silence(g, ninja);
        for (int i = 0; i < 40; i++) { x1.Damage = 0; x2.Damage = 0; ninja.Damage = 0; Strike(g, ninja, x1); Check(x1.Damage == 6 && x2.Damage == 0 && opp.Hero.Damage == 0, "a Silenced Ogre Ninja attacks what it is told"); }
        End(g);
        Park(p); Park(opp);
        var theirs = (Minion)Play(g, opp, id);
        var m1 = Dummy(p); var m2 = Dummy(p);
        var (oi, oo) = Trials(g, 100, m1, new ICharacter[] { m2, p.Hero }, () => Strike(g, theirs, m1));
        Check(oi >= 30 && oi <= 70 && oo.All(x => x >= 10), "the opponent's Ninja misdirects against my side: " + oi + "/" + string.Join("/", oo));
        Resume(g, next => Check(Get(next, theirs).Zone.Type == Zone.PLAY && Get(next, theirs).HasStealth == theirs.HasStealth, "clone keeps the Ninja"));
    }

    // ---- GVG_089 Illuminator ----------------------------------------------------------------------------------------

    private static void Illuminator(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 3 && ((Minion)card).AttackDamage == 2 && ((Minion)card).Health == 4, "a 2/4 for 3");
        PlayEntity(g, p, card);
        var ill = (Minion)card;
        SetDamage(g, p.Hero, 10);
        End(g);
        Check(p.Hero.Damage == 10, "no Secret at the end of my turn: no healing");
        End(g);
        var trap = Secret(g, p, "EX1_610");
        End(g);
        Check(p.Hero.Damage == 6, "a Secret at the end of my turn: restore 4 Health: " + p.Hero.Damage);
        End(g);
        Check(p.Hero.Damage == 6, "not at the end of the opponent's turn");
        SetDamage(g, p.Hero, 2);
        End(g);
        Check(p.Hero.Damage == 0, "no overheal");
        // T6, the opponent's turn: my Secret leaves play, the opponent holds one
        p.SetasideZone.Add(p.SecretZone.Remove(trap)); g.DeathProcessingAndAuraUpdate();
        SetDamage(g, p.Hero, 8);
        var theirTrap = Secret(g, opp, "EX1_610");
        End(g); End(g);
        Check(p.Hero.Damage == 8, "an enemy Secret does not count, and no Secret of mine: " + p.Hero.Damage);
        opp.SetasideZone.Add(opp.SecretZone.Remove(theirTrap));
        End(g);
        // two Illuminators heal twice; Silence stops each one
        Secret(g, p, "EX1_610");
        var second = (Minion)Play(g, p, id);
        Resume(g, next => { End(next); Check(Get(next, p).Hero.Damage == 0, "clone: two Illuminators heal 8"); });
        End(g);
        Check(p.Hero.Damage == 0, "two Illuminators heal 4 each");
        End(g);
        SetDamage(g, p.Hero, 9);
        Silence(g, ill);
        End(g);
        Check(p.Hero.Damage == 5, "only the Silenced one stopped: " + p.Hero.Damage);
        End(g);
        Silence(g, second);
        End(g);
        Check(p.Hero.Damage == 5, "both Silenced: " + p.Hero.Damage);
        // the opponent's Illuminator on its own turn, with its own Secret
        Secret(g, opp, "EX1_610");
        SetDamage(g, opp.Hero, 9);
        Play(g, opp, id);
        End(g);
        Check(opp.Hero.Damage == 5 && p.Hero.Damage == 5, "the opponent's Illuminator heals its own hero: " + opp.Hero.Damage);
    }

    // ---- GVG_090 Madder Bomber --------------------------------------------------------------------------------------

    private static void MadderBomber(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 5 && ((Minion)card).AttackDamage == 5 && ((Minion)card).Health == 4, "a 5/4 for 5");
        PlayEntity(g, p, card);
        var bomber = (Minion)card;
        Check(bomber.Damage == 0 && p.Hero.Damage + opp.Hero.Damage == 6, "only heroes: 6 damage split between them: " + p.Hero.Damage + "/" + opp.Hero.Damage);
        // random split across characters, never on the bomber
        var hits = new int[5]; var distinct = new HashSet<string>();
        for (int seed = 1; seed <= 40; seed++)
        {
            var f = Fresh(seed);
            var friend = Dummy(f.Player1); var e1 = Dummy(f.Player2); var e2 = Dummy(f.Player2);
            var b = (Minion)Play(f, f.Player1, id);
            var all = new ICharacter[] { f.Player1.Hero, f.Player2.Hero, friend, e1, e2 };
            int total = all.Sum(x => x.Damage);
            Check(total == 6 && b.Damage == 0, "6 points, none on the Bomber: " + total + "/" + b.Damage);
            for (int i = 0; i < 5; i++) if (all[i].Damage > 0) hits[i]++;
            distinct.Add(string.Join(",", all.Select(x => x.Damage)));
        }
        Check(hits.All(h => h >= 15), "every other character is a possible target: " + string.Join("/", hits));
        Check(distinct.Count >= 8, "the split varies with the seed: " + distinct.Count);
        // Divine Shield and Stealth
        var fs = Fresh(7);
        var sh = Dummy(fs.Player2); sh.HasDivineShield = true;
        var st = Dummy(fs.Player2, "EX1_010"); st[GameTag.HEALTH] = 20; st[GameTag.ATK] = 0;
        Play(fs, fs.Player1, id);
        Check(fs.Player1.Hero.Damage + fs.Player2.Hero.Damage + sh.Damage + st.Damage <= 6, "Divine Shield absorbs a point");
        // the opponent's Bomber can hit its own side and mine
        End(g);
        var friendOpp = Dummy(opp); var mine = Dummy(p);
        var theirs = (Minion)Play(g, opp, id);
        Check(theirs.Damage == 0 && p.Hero.Damage + opp.Hero.Damage + friendOpp.Damage + mine.Damage >= 6, "the opponent's Bomber works the same");
        Resume(g, next => Check(Get(next, theirs).Damage == 0, "clone keeps the board"));
    }

    // ---- GVG_091 Arcane Nullifier X-21 ------------------------------------------------------------------------------

    private static void ArcaneNullifier(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 4 && ((Minion)card).AttackDamage == 2 && ((Minion)card).Health == 5 && ((Minion)card).IsRace(Race.MECHANICAL) && ((Minion)card).HasTaunt, "a 2/5 Taunt Mech for 4");
        PlayEntity(g, p, card);
        var nullifier = (Minion)card;
        var fire = Add(p, "CS2_029");
        Check(!Targetable(p, fire, nullifier), "my own spells cannot target it");
        End(g);
        var fireball = Add(opp, "CS2_029");
        Check(!Targetable(opp, fireball, nullifier), "the opponent's spells cannot target it");
        Check(!PowerOptions(opp).Any(t => t.Target?.Id == nullifier.Id), "nor the Mage hero power");
        Check(opp.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == fireball.Id && t.Target?.Id == opp.Hero.Id), "control: other targets work");
        // AoE and attacks still reach it; Taunt forces attacks
        var enemy = Summon(opp, "CS2_182"); enemy.IsExhausted = false;
        var attackTargets = opp.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == enemy.Id).Select(t => t.Target.Id).ToList();
        Check(attackTargets.Count == 1 && attackTargets[0] == nullifier.Id, "Taunt: it is the only attack target");
        PlayEntity(g, opp, Add(opp, "CS2_032"));
        Check(nullifier.Damage == 4, "Flamestrike hits it");
        Check(!Targetable(opp, Add(opp, "CS2_024"), nullifier), "no single-target spell");
        Resume(g, next => Check(!Targetable(Get(next, opp), Add(Get(next, opp), "CS2_029"), Get(next, nullifier)), "clone keeps Elusive"));
        // battlecries may target it (it is not a spell or hero power)
        End(g);
        var bot = Add(p, "GVG_083");
        Check(Targetable(p, bot, nullifier), "a battlecry can target it");
        // Silence removes Elusive
        Silence(g, nullifier);
        Check(Targetable(p, Add(p, "CS2_029"), nullifier) && !nullifier.HasTaunt, "Silenced: a spell can target it");
        End(g);
        var theirs = (Minion)Play(g, opp, id);
        End(g);
        Check(!Targetable(p, Add(p, "CS2_029"), theirs), "the opponent's copy is Elusive for me too");
    }

    // ---- GVG_092 Gnomish Experimenter -------------------------------------------------------------------------------

    private static void GnomishExperimenter(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 3 && ((Minion)card).AttackDamage == 3 && ((Minion)card).Health == 2, "a 3/2 for 3");
        EmptyHand(p);
        card = Add(p, id);
        Stack(p, "CS2_029", "CS2_182");
        int deck = p.DeckZone.Count;
        PlayEntity(g, p, card);
        Check(p.HandZone.Count == 1 && p.HandZone[0].Card.Id == "GVG_092t" && p.DeckZone.Count == deck - 1, "a minion is drawn and becomes a Chicken: " + p.HandZone[0].Card.Id);
        var chicken = p.HandZone[0];
        Check(chicken.Cost == 1 && ((Minion)chicken).AttackDamage == 1 && ((Minion)chicken).Health == 1 && ((Minion)chicken).IsRace(Race.BEAST), "a 1/1 Beast for 1: " + chicken.Cost + " " + ((Minion)chicken).AttackDamage + "/" + ((Minion)chicken).Health + " " + string.Join("/", chicken.Card.Races));
        Concealed(g, chicken, "the transformed card");
        PlayEntity(g, p, chicken);
        Check(p.BoardZone.Any(m => m.Card.Id == "GVG_092t") && chicken.Zone.Type == Zone.PLAY, "the Chicken can be played");
        // a spell stays
        EmptyHand(p); Stack(p, "CS2_182", "CS2_029");
        PlayEntity(g, p, Add(p, id));
        Check(p.HandZone.Count == 1 && p.HandZone[0].Card.Id == "CS2_029", "a spell stays a spell");
        // a weapon stays
        EmptyHand(p); Stack(p, "CS2_182", "CS2_106");
        PlayEntity(g, p, Add(p, id));
        Check(p.HandZone.Single().Card.Id == "CS2_106", "a weapon stays");
        // empty deck: fatigue, no Chicken
        EmptyHand(p);
        foreach (var d in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(d));
        int fatigue = p.Hero.Fatigue;
        PlayEntity(g, p, Add(p, id));
        Check(p.HandZone.Count == 0 && p.Hero.Fatigue == fatigue + 1, "an empty deck: Fatigue, no Chicken");
        // full hand: the drawn minion burns
        Stack(p, "CS2_182");
        EmptyHand(p);
        var xp = Add(p, id); FillHand(p);
        int burnDeck = p.DeckZone.Count;
        PlayEntity(g, p, xp);
        Check(p.HandZone.Count == 10 && p.HandZone.Count(x => x.Card.Id == "GVG_092t") == 1 && p.DeckZone.Count == burnDeck - 1, "the slot of the played card takes the drawn card, which becomes a Chicken: " + p.HandZone.Count);
        // the opponent's draw
        End(g);
        EmptyHand(opp); Stack(opp, "CS2_182");
        PlayEntity(g, opp, Add(opp, id));
        Check(opp.HandZone.Count == 1 && opp.HandZone[0].Card.Id == "GVG_092t", "the opponent's Experimenter turns its own draw");
        Concealed(g, opp.HandZone[0], "the opponent's Chicken");
        Resume(g, next => Check(Get(next, opp).HandZone.Count == 1 && Get(next, opp).HandZone[0].Card.Id == "GVG_092t", "clone keeps the Chicken"));
    }

    // ---- GVG_093 Target Dummy ---------------------------------------------------------------------------------------

    private static void TargetDummy(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 0 && ((Minion)card).AttackDamage == 0 && ((Minion)card).Health == 2 && ((Minion)card).IsRace(Race.MECHANICAL) && ((Minion)card).HasTaunt, "a 0/2 Taunt Mech for 0");
        PlayEntity(g, p, card);
        var dummy = (Minion)card;
        Check(p.UsedMana == 0, "it costs nothing");
        var enemy = Dummy(opp); enemy[GameTag.ATK] = 3;
        End(g);
        enemy.IsExhausted = false;
        var targets = opp.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == enemy.Id).Select(t => t.Target.Id).ToList();
        Check(targets.Count == 1 && targets[0] == dummy.Id, "Taunt forces the attack");
        Attack(g, enemy, dummy);
        Check(dummy.Damage == 2 || dummy.Zone.Type == Zone.GRAVEYARD, "it absorbs the hit");
        End(g);
        var second = (Minion)Play(g, p, id);
        second.IsExhausted = false;
        Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == second.Id), "0 Attack: it cannot attack");
        Silence(g, second);
        Check(!second.HasTaunt, "Silence removes Taunt");
        Resume(g, next => Check(Get(next, second).Zone.Type == Zone.PLAY && !Get(next, second).HasTaunt, "clone keeps state"));
    }

    // ---- GVG_094 Jeeves ---------------------------------------------------------------------------------------------

    private static void Jeeves(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var jeeves = (Minion)Play(g, p, id);
        Check(jeeves.Card.Cost == 4 && jeeves.AttackDamage == 1 && jeeves.Health == 4 && jeeves.IsRace(Race.MECHANICAL), "a 1/4 Mech for 4");
        Check(p.HandZone.Count == 0, "playing it draws nothing");
        End(g);
        Check(p.HandZone.Count == 3 && opp.HandZone.Count == 1, "my end of turn: I draw to 3, the opponent only its turn draw: " + p.HandZone.Count + "/" + opp.HandZone.Count);
        Concealed(g, opp.HandZone[0], "a drawn card");
        Concealed(g, p.HandZone[0], "a Jeeves card");
        End(g);
        Check(opp.HandZone.Count == 3 && p.HandZone.Count == 4, "the opponent's end of turn: it draws until 3: " + opp.HandZone.Count + "/" + p.HandZone.Count);
        // holding more than 3: nothing is drawn (and nothing discarded)
        End(g); End(g);
        Check(p.HandZone.Count == 5 && opp.HandZone.Count == 4, "no draw above 3: " + p.HandZone.Count + "/" + opp.HandZone.Count);
        // one card in hand: two draws
        EmptyHand(p); Add(p, "CS2_182");
        int deck = p.DeckZone.Count;
        End(g);
        Check(p.HandZone.Count == 3 && p.DeckZone.Count == deck - 2, "one card in hand: draws two: " + p.HandZone.Count);
        // exactly three on the opponent's side: nothing
        EmptyHand(opp); for (int i = 0; i < 3; i++) Add(opp, "CS2_182");
        int oppDeck = opp.DeckZone.Count;
        End(g);
        Check(opp.HandZone.Count == 3 && opp.DeckZone.Count == oppDeck, "exactly three: nothing");
        // empty deck: Fatigue for every missing card
        EmptyHand(p);
        foreach (var d in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(d));
        int fatigue = p.Hero.Fatigue; int hp = p.Hero.Damage;
        Check(fatigue == 0, "fixture: no Fatigue yet");
        End(g);
        Check(p.HandZone.Count == 0 && p.Hero.Fatigue == 3 && p.Hero.Damage == hp + 1 + 2 + 3, "empty deck: three Fatigue hits, no endless loop: " + p.Hero.Fatigue + "/" + (p.Hero.Damage - hp));
        // clone: the trigger still works
        Resume(g, next => { var o = Get(next, opp); EmptyHand(o); End(next); Check(Get(next, opp).HandZone.Count == 3, "clone: Jeeves still draws"); });
        // Silence
        Silence(g, jeeves);
        EmptyHand(opp);
        End(g);
        Check(opp.HandZone.Count == 0, "a Silenced Jeeves draws nothing");
        // the opponent's Jeeves also draws for me at the end of MY turn ("that player")
        Stack(p, "CS2_182");
        End(g);
        var theirs = (Minion)Play(g, opp, id);
        End(g);
        EmptyHand(p);
        End(g);
        Check(p.HandZone.Count == 3, "the opponent's Jeeves draws for me at my end of turn: " + p.HandZone.Count);
        Check(theirs.Zone.Type == Zone.PLAY, "it stays");
    }

    // ---- GVG_095 Goblin Sapper --------------------------------------------------------------------------------------

    private static void SetHand(Game g, Controller c, int n)
    {
        EmptyHand(c);
        for (int i = 0; i < n; i++) Add(c, "CS2_182");
        g.DeathProcessingAndAuraUpdate();
    }

    private static void GoblinSapper(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 3 && ((Minion)card).AttackDamage == 2 && ((Minion)card).Health == 4, "a 2/4 for 3");
        PlayEntity(g, p, card);
        var sapper = (Minion)card;
        SetHand(g, opp, 0);
        Check(sapper.AttackDamage == 2, "an empty enemy hand: 2");
        SetHand(g, opp, 5);
        Check(sapper.AttackDamage == 2, "five cards: still 2");
        SetHand(g, opp, 6);
        Check(sapper.AttackDamage == 6, "six cards: +4");
        SetHand(g, opp, 7);
        Check(sapper.AttackDamage == 6, "seven cards: still +4, not more");
        SetHand(g, opp, 10);
        Check(sapper.AttackDamage == 6, "a full enemy hand: +4");
        Resume(g, next => { Check(Get(next, sapper).AttackDamage == 6, "clone keeps the aura"); SetHand(next, Get(next, opp), 5); Check(Get(next, sapper).AttackDamage == 2, "clone: back to 2 at five cards"); });
        // real flows: the opponent's turn draw takes it from five to six, playing a card takes it back
        SetHand(g, opp, 5);
        Check(sapper.AttackDamage == 2, "five cards");
        End(g);
        Check(opp.HandZone.Count == 6 && sapper.AttackDamage == 6, "the opponent's turn draw makes six: +4: " + opp.HandZone.Count + "/" + sapper.AttackDamage);
        var spent = opp.HandZone.First(x => x.Card.Id == "CS2_182");
        PlayEntity(g, opp, spent);
        Check(opp.HandZone.Count == 5 && sapper.AttackDamage == 2, "the opponent plays a card: five again: " + sapper.AttackDamage);
        End(g);
        // other buffs stack on top; my own hand does not count
        SetHand(g, opp, 6);
        Play(g, p, "CS2_087", sapper);
        Check(sapper.AttackDamage == 9, "other buffs stack: 2 + 3 + 4: " + sapper.AttackDamage);
        SetHand(g, opp, 0);
        FillHand(p); g.DeathProcessingAndAuraUpdate();
        Check(sapper.AttackDamage == 5, "my own full hand does not count: 2 + 3");
        EmptyHand(p);
        SetHand(g, opp, 6);
        Silence(g, sapper);
        Check(sapper.AttackDamage == 2, "Silence removes the aura and the buff");
        // the opponent's Sapper reads my hand
        End(g);
        SetHand(g, opp, 0);
        var theirs = (Minion)Play(g, opp, id);
        SetHand(g, p, 6);
        Check(theirs.AttackDamage == 6, "the opponent's Sapper counts MY hand: " + theirs.AttackDamage);
        SetHand(g, p, 5);
        Check(theirs.AttackDamage == 2, "five cards of mine: 2");
        SetHand(g, p, 6);
    }

    // ---- GVG_096 Piloted Shredder -----------------------------------------------------------------------------------

    private static void PilotedShredder(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var shredder = (Minion)Play(g, p, id);
        Check(shredder.Card.Cost == 4 && shredder.AttackDamage == 4 && shredder.Health == 3 && shredder.IsRace(Race.MECHANICAL), "a 4/3 Mech for 4");
        KillSettle(g, shredder);
        Check(p.BoardZone.Count == 1 && p.BoardZone[0].Card.Cost == 2 && p.BoardZone[0].Card.Collectible && opp.BoardZone.Count == 0, "a 2-cost collectible minion on my side");
        // varied across seeds
        var seen = new HashSet<string>();
        for (int seed = 1; seed <= 40; seed++)
        {
            var f = Fresh(seed);
            var s = (Minion)Play(f, f.Player1, id);
            KillSettle(f, s);
            Check(f.Player1.BoardZone.Count == 1 && f.Player2.BoardZone.Count == 0, "exactly one minion appears");
            var m = f.Player1.BoardZone[0];
            Check(m.Card.Cost == 2 && m.Card.Type == CardType.MINION && m.Card.Collectible, "a collectible 2-cost minion: " + m.Card.Id);
            seen.Add(m.Card.Id);
        }
        Check(seen.Count >= 8, "a wide pool of 2-cost minions: " + seen.Count);
        // full board: the Shredder's own slot is free
        Park(p);
        var last = (Minion)Play(g, p, id); Fill(p);
        Check(p.BoardZone.IsFull, "full board");
        KillSettle(g, last);
        Check(p.BoardZone.Count == 7, "the dead Shredder's slot takes the new minion");
        Park(p);
        // Silence
        var quiet = (Minion)Play(g, p, id); Silence(g, quiet); KillSettle(g, quiet);
        Check(p.BoardZone.Count == 0, "a Silenced Shredder summons nothing");
        // opponent's side and clone
        var live = (Minion)Play(g, p, id);
        Resume(g, next => { KillSettle(next, Get(next, live)); Check(Get(next, p).BoardZone.Count == 1, "clone: the Deathrattle works"); });
        End(g);
        var theirs = (Minion)Play(g, opp, id);
        KillSettle(g, theirs);
        Check(opp.BoardZone.Count == 1 && opp.BoardZone[0].Card.Cost == 2, "the opponent's Shredder summons on its own side");
    }

    // ---- GVG_097 Lil' Exorcist --------------------------------------------------------------------------------------

    private static void LilExorcist(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 3 && ((Minion)card).AttackDamage == 2 && ((Minion)card).Health == 3 && ((Minion)card).HasTaunt, "a 2/3 Taunt for 3");
        PlayEntity(g, p, card);
        var ex = (Minion)card;
        Check(ex.AttackDamage == 2 && ex.Health == 3, "no enemy Deathrattle minion: no buff");
        Park(p);
        // enemy deathrattles count; plain ones and friendly ones do not
        var d1 = Summon(opp, "EX1_556"); var d2 = Summon(opp, "EX1_096"); var plain = Summon(opp, "CS2_182"); Summon(p, "EX1_556");
        Add(p, "EX1_556"); Add(opp, "EX1_556");
        var ex2 = (Minion)Play(g, p, id);
        Check(ex2.AttackDamage == 4 && ex2.Health == 5, "+1/+1 for each of the two enemy Deathrattle minions: " + ex2.AttackDamage + "/" + ex2.Health);
        Resume(g, next => Check(Get(next, ex2).AttackDamage == 4 && Get(next, ex2).Health == 5, "clone keeps the buff"));
        // a Silenced Deathrattle minion no longer counts; a stealthed one does
        Silence(g, d1);
        Summon(opp, "EX1_556"); var stealthy = Summon(opp, "EX1_010");
        var ex3 = (Minion)Play(g, p, id);
        Check(ex3.AttackDamage == 4 && ex3.Health == 5, "Silenced Deathrattle does not count, a new one does: " + ex3.AttackDamage);
        // Silence: back to base
        Silence(g, ex3);
        Check(ex3.AttackDamage == 2 && ex3.Health == 3 && !ex3.HasTaunt, "Silence removes the buff and Taunt");
        // the buff is a real buff: a damaged Exorcist keeps the max
        var ex4 = (Minion)Play(g, p, id);
        Hit(g, ex4, plain, 2);
        Check(ex4.Health == 3 && ex4.Damage == 2, "damage on top of the buffed health");
        // many Deathrattles
        Park(opp); Park(p);
        for (int i = 0; i < 4; i++) Summon(opp, "EX1_096");
        var ex5 = (Minion)Play(g, p, id);
        Check(ex5.AttackDamage == 6 && ex5.Health == 7, "four Deathrattle minions: 6/7");
        // the opponent's Exorcist counts mine
        End(g);
        Park(p); Park(opp);
        Summon(p, "EX1_556"); Summon(p, "EX1_096"); Summon(p, "EX1_096"); Summon(opp, "EX1_096");
        var theirs = (Minion)Play(g, opp, id);
        Check(theirs.AttackDamage == 5 && theirs.Health == 6, "the opponent's Exorcist counts MY three Deathrattle minions");
    }

    // ---- GVG_098 Gnomeregan Infantry --------------------------------------------------------------------------------

    private static void GnomereganInfantry(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 3 && ((Minion)card).AttackDamage == 1 && ((Minion)card).Health == 4 && ((Minion)card).HasCharge && ((Minion)card).HasTaunt, "a 1/4 Charge Taunt for 3");
        PlayEntity(g, p, card);
        var inf = (Minion)card;
        Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == inf.Id && t.Target?.Id == opp.Hero.Id), "Charge: it can attack at once");
        var enemy = Summon(opp, "CS2_182");
        End(g);
        enemy.IsExhausted = false;
        Check(opp.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == enemy.Id).Select(t => t.Target.Id).SequenceEqual(new[] { inf.Id }), "Taunt: the only target");
        enemy[GameTag.ATK] = 0; enemy[GameTag.HEALTH] = 20;
        End(g);
        Strike(g, inf, enemy);
        Check(enemy.Damage == 1, "attacks for 1");
        var quiet = (Minion)Play(g, p, id);
        Silence(g, quiet);
        Check(!quiet.HasCharge && !quiet.HasTaunt, "Silence removes both");
        Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == quiet.Id), "a Silenced one cannot attack on arrival");
        Resume(g, next => Check(Get(next, inf).HasCharge && Get(next, inf).HasTaunt, "clone keeps the keywords"));
        End(g);
        var theirs = (Minion)Play(g, opp, id);
        Check(opp.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == theirs.Id), "the opponent's copy charges too");
    }

    // ---- GVG_099 Bomb Lobber ----------------------------------------------------------------------------------------

    private static void BombLobber(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 5 && ((Minion)card).AttackDamage == 3 && ((Minion)card).Health == 3, "a 3/3 for 5");
        Check(Playable(p, card), "playable with no enemy minion");
        PlayEntity(g, p, card);
        Check(opp.Hero.Damage == 0 && p.Hero.Damage == 0 && ((Minion)card).Damage == 0, "no enemy minion: nothing is hit, not even the hero");
        var friend = Dummy(p);
        PlayEntity(g, p, Add(p, id));
        Check(friend.Damage == 0 && opp.Hero.Damage == 0, "friendly minions and heroes are not candidates");
        // random among three, order-sensitive across seeds; a lone Stealth minion is a legal victim
        var hits = new int[3];
        for (int seed = 1; seed <= 36; seed++)
        {
            var f = Fresh(seed);
            var a = Dummy(f.Player2); var b = Dummy(f.Player2); var c = Dummy(f.Player2); var mine = Dummy(f.Player1);
            Play(f, f.Player1, id);
            var all = new[] { a, b, c };
            Check(all.Sum(x => x.Damage) == 4 && all.Count(x => x.Damage > 0) == 1 && mine.Damage == 0 && f.Player2.Hero.Damage == 0, "exactly one enemy minion takes 4");
            for (int i = 0; i < 3; i++) if (all[i].Damage > 0) hits[i]++;
        }
        Check(hits.All(h => h >= 5), "each candidate can be chosen: " + string.Join("/", hits));
        var fs = Fresh(3);
        var sneaky = Summon(fs.Player2, "EX1_010"); sneaky[GameTag.HEALTH] = 20;
        Play(fs, fs.Player1, id);
        Check(sneaky.Damage == 4, "Stealth does not protect from a random effect");
        var fd = Fresh(4);
        var shield = Dummy(fd.Player2); shield.HasDivineShield = true;
        Play(fd, fd.Player1, id);
        Check(!shield.HasDivineShield && shield.Damage == 0, "Divine Shield pops");
        var fk = Fresh(5);
        var weak = Summon(fk.Player2, "CS2_168");
        Play(fk, fk.Player1, id);
        Check(weak.Zone.Type == Zone.GRAVEYARD, "a lethal hit kills it");
        // the opponent's Lobber
        End(g);
        Park(p); Park(opp);
        var mine1 = Dummy(p); var mine2 = Dummy(p);
        var theirs = Add(opp, id);
        PlayEntity(g, opp, theirs);
        Check(mine1.Damage + mine2.Damage == 4 && p.Hero.Damage == 0 && opp.BoardZone.All(m => m.Damage == 0), "the opponent's Lobber hits my minions only");
        Resume(g, next => Check(Get(next, p).Hero.Damage == 0, "clone keeps the board"));
    }

    // ---- BLOCKED-STANDARD scenarios (unregistered) ------------------------------------------------------------------

    private static void RunUnregistered()
    {
        foreach (var (id, scenario) in new (string, Action<Game, Controller, string>)[] {
            ("GVG_079", ForceTankMax), ("GVG_082", ClockworkGnome), ("GVG_085", AnnoyOTron), ("GVG_100", FloatingWatcher) })
            for (int seed = 1; seed <= 3; seed++)
            {
                var game = Fresh(seed);
                scenario(game, game.Player1, id);
                Check(Canonical.State(game) == Canonical.State(game.CloneExact(history: true)), "exact clone after unregistered scenario " + id);
                Check(Canonical.State(game) == Canonical.State(Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools)), "snapshot after unregistered scenario " + id);
            }
        Console.WriteLine("UNREGISTERED scenarios GVG_079, GVG_082, GVG_085, GVG_100 PASS; seeds=3; clone/snapshot");
    }

    private static void ForceTankMax(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var tank = (Minion)Play(g, p, id);
        Check(tank.Card.Cost == 8 && tank.AttackDamage == 7 && tank.Health == 7 && tank.HasDivineShield && tank.IsRace(Race.MECHANICAL), "a 7/7 Divine Shield Mech for 8");
        var enemy = Summon(opp, "CS2_182");
        tank.TakeDamage(enemy, 6);
        Check(!tank.HasDivineShield && tank.Damage == 0, "the shield absorbs one hit");
        tank.TakeDamage(enemy, 2);
        Check(tank.Damage == 2, "then it takes damage");
        var quiet = (Minion)Play(g, p, id); quiet.Silence();
        Check(!quiet.HasDivineShield, "Silence removes the shield");
    }

    private static void ClockworkGnome(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var gnome = (Minion)Play(g, p, id);
        Check(gnome.Card.Cost == 1 && gnome.AttackDamage == 2 && gnome.Health == 1 && gnome.IsRace(Race.MECHANICAL), "a 2/1 Mech for 1");
        EmptyHand(p); EmptyHand(opp);
        Kill(g, gnome);
        Check(p.HandZone.Count == 1 && p.HandZone[0].Card.Id.StartsWith("PART_") && opp.HandZone.Count == 0, "one Spare Part for me only");
        Concealed(g, p.HandZone[0], "the Spare Part");
        EmptyHand(p);
        var full = (Minion)Play(g, p, id); FillHand(p);
        Kill(g, full);
        Check(p.HandZone.Count == 10, "a full hand gets nothing");
        EmptyHand(p);
        var quiet = (Minion)Play(g, p, id); quiet.Silence(); EmptyHand(p);
        Kill(g, quiet);
        Check(p.HandZone.Count == 0, "Silenced: nothing");
        var seen = new HashSet<string>();
        for (int seed = 1; seed <= 30; seed++)
        {
            var f = Fresh(seed); var m = (Minion)Play(f, f.Player1, id); EmptyHand(f.Player1);
            Kill(f, m);
            seen.Add(f.Player1.HandZone[0].Card.Id);
        }
        Check(seen.Count >= 3, "at least three different Spare Parts: " + seen.Count);
        End(g);
        EmptyHand(opp);
        var theirs = (Minion)Play(g, opp, id); EmptyHand(opp); EmptyHand(p);
        Kill(g, theirs);
        Check(opp.HandZone.Count == 1 && p.HandZone.Count == 0, "the opponent's Gnome feeds the opponent");
    }

    private static void AnnoyOTron(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var bot = (Minion)Play(g, p, id);
        Check(bot.Card.Cost == 2 && bot.AttackDamage == 1 && bot.Health == 2 && bot.HasTaunt && bot.HasDivineShield && bot.IsRace(Race.MECHANICAL), "a 1/2 Taunt Divine Shield Mech for 2");
        var enemy = Summon(opp, "CS2_182"); enemy.IsExhausted = false;
        End(g);
        enemy.IsExhausted = false;
        Check(opp.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == enemy.Id).Select(t => t.Target.Id).SequenceEqual(new[] { bot.Id }), "Taunt: the only target");
        Attack(g, enemy, bot);
        Check(bot.Damage == 0 && !bot.HasDivineShield && bot.Zone.Type == Zone.PLAY, "the shield absorbs the attack");
        var quiet = (Minion)Play(g, opp, id); quiet.Silence();
        Check(!quiet.HasTaunt && !quiet.HasDivineShield, "Silence removes both");
    }

    private static void FloatingWatcher(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var watcher = (Minion)Play(g, p, id);
        Check(watcher.Card.Cost == 5 && watcher.AttackDamage == 4 && watcher.Health == 4 && watcher.IsRace(Race.DEMON), "a 4/4 Demon for 5");
        var src = Summon(opp, "CS2_182");
        p.Hero.TakeDamage(src, 2); g.DeathProcessingAndAuraUpdate();
        Check(watcher.AttackDamage == 6 && watcher.Health == 6, "my hero took damage on my turn: +2/+2");
        p.Hero.TakeDamage(src, 1); g.DeathProcessingAndAuraUpdate();
        Check(watcher.AttackDamage == 8 && watcher.Health == 8, "every damage instance counts");
        opp.Hero.TakeDamage(src, 3); g.DeathProcessingAndAuraUpdate();
        Check(watcher.AttackDamage == 8, "the enemy hero does not count");
        Armor(g, p, 5);
        p.Hero.TakeDamage(src, 2); g.DeathProcessingAndAuraUpdate();
        Check(watcher.AttackDamage == 10 && p.Hero.Armor == 3, "damage absorbed by Armor still counts");
        Hit(g, watcher, src, 1);
        Check(watcher.AttackDamage == 10, "its own damage does not count");
        End(g);
        p.Hero.TakeDamage(src, 2); g.DeathProcessingAndAuraUpdate();
        Check(watcher.AttackDamage == 10, "not on the opponent's turn");
        End(g);
        var quiet = (Minion)Play(g, p, id); quiet.Silence();
        p.Hero.TakeDamage(src, 1); g.DeathProcessingAndAuraUpdate();
        Check(quiet.AttackDamage == 4 && watcher.AttackDamage == 12, "a Silenced Watcher stops: " + watcher.AttackDamage);
        Resume(g, next => { Get(next, p).Hero.TakeDamage(Get(next, src), 1); next.DeathProcessingAndAuraUpdate(); Check(Get(next, watcher).AttackDamage == 14, "clone keeps listening"); });
        End(g);
        var theirs = (Minion)Play(g, opp, id);
        opp.Hero.TakeDamage(Summon(p, "CS2_182"), 2); g.DeathProcessingAndAuraUpdate();
        Check(theirs.AttackDamage == 6, "the opponent's Watcher on its own turn");
    }
}
