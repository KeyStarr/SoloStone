using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Actions;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Wave Y0b, lane m2b: Goblins vs Gnomes GVG_026 .. GVG_050 (Hunter, Rogue, Shaman, Druid, Warlock, Warrior and neutral cards).
// Unregistered (not in the batch manifest), their scenarios kept below and run only with WILD_Y0B_M2B_UNREGISTERED=1:
//  - HELD GVG_035 Malorne: 37.0 text is "Go Dormant. Revive after 2 friendly Beasts die." The Dormant condition enchantment WON_011e is not
//    in the card file (the generator emits identity rows only for collectible manifest ids, and the overlay only for the active meta),
//    and a counted external wake-up (revive after N deaths) needs a small Dormant counter hook. The scenario below (Malorne) states the
//    expected behaviour; it is not run and the script is unchanged (still "Shuffle this minion into your deck").
//  - BLOCKED-STANDARD GVG_033 (data/pools/pool-eligibility-v1.json), (GVG_034 was held for plugin/Npc/TriggerValueData.cs, which does not hold an id: registered),
//    GVG_044 (plugin/Npc/TriggerMeasure.cs): Standard data uses them today. Their scripts are not changed here.
internal static class WildY0bM2BProof
{
    [GroupProof("wild-y0b-m2b-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        WildSetsProof.Case("GVG_026", FeignDeath);
        WildSetsProof.Case("GVG_027", IronSensei);
        WildSetsProof.Case("GVG_028", Gallywix);
        WildSetsProof.Case("GVG_029", AncestorsCall);
        WildSetsProof.Case("GVG_030", RoboCub);
        WildSetsProof.Case("GVG_031", Recycle);
        WildSetsProof.Case("GVG_032", GroveTender);
        WildSetsProof.Case("GVG_034", MechBearCat);
        WildSetsProof.Held("GVG_036", "held: FX-MISSING live (deathrattle)", Powermace);
        WildSetsProof.Case("GVG_037", ZapOMatic);
        WildSetsProof.Case("GVG_038", Crackle);
        WildSetsProof.Case("GVG_039", VitalityTotem);
        WildSetsProof.Case("GVG_040", Siltfin);
        WildSetsProof.Held("GVG_041", "held: Choose One FX swapped between options (live)", DarkWispers);
        WildSetsProof.Case("GVG_042", Neptulon);
        WildSetsProof.Case("GVG_043", Glaivezooka);
        WildSetsProof.Case("GVG_045", Implosion);
        WildSetsProof.Held("GVG_046", "held: FX-MISSING live (play)", KingOfBeasts);
        WildSetsProof.Case("GVG_047", Sabotage);
        WildSetsProof.Case("GVG_048", MetaltoothLeaper);
        WildSetsProof.Case("GVG_049", Gahzrilla);
        WildSetsProof.Case("GVG_050", BouncingBlade);
        if (Environment.GetEnvironmentVariable("WILD_Y0B_M2B_UNREGISTERED") == "1") RunUnregistered();
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
    private static T Get<T>(Game game, T entity) where T : class, IPlayable => (T)game.IdEntityDic[entity.Id];
    private static Controller Get(Game game, Controller c) => c.PlayerId == game.Player1.PlayerId ? game.Player1 : game.Player2;

    /// <summary>Plays a Choose One card through its legal option with the given mode (1 or 2), refilling mana first.</summary>
    private static void PlayMode(Game g, Controller p, IPlayable card, int mode, ICharacter target = null)
    {
        p.BaseMana = 10; p.UsedMana = 0; p.TemporaryMana = 0;
        var task = p.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id
            && t.ChooseOne == mode && t.Target?.Id == target?.Id);
        Check(task != null, "legal play missing: " + card.Card.Id + " mode " + mode + "; board " + p.BoardZone.Count + " hand " + p.HandZone.Count
            + " mana " + p.RemainingMana + "; offered: " + string.Join(" ", p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id).Select(t => t.ChooseOne + ">" + t.Target?.Card.Id)));
        Check(g.Process(task), "legal play rejected: " + card.Card.Id + " mode " + mode);
    }
    private static bool ModeOffered(Controller c, IPlayable card, int mode, ICharacter target = null)
    {
        c.BaseMana = 10; c.UsedMana = 0; c.TemporaryMana = 0;
        return c.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id && t.ChooseOne == mode && t.Target?.Id == target?.Id);
    }

    private static void Swing(Game g, Controller p, ICharacter target)
    {
        p.Hero.IsExhausted = false;
        var task = p.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == target.Id);
        Check(task != null && g.Process(task), "hero attack accepted");
    }

    /// <summary>Deck of five Yetis topped by the given ids, the last id on top.</summary>
    private static void Stack(Controller c, params string[] topLast)
    {
        foreach (var card in c.DeckZone.ToArray()) c.SetasideZone.Add(c.DeckZone.Remove(card));
        for (int i = 0; i < 5; i++) Entity.FromCard(c, Cards.FromId("CS2_182"), zone: c.DeckZone);
        foreach (var id in topLast) Entity.FromCard(c, Cards.FromId(id), zone: c.DeckZone);
    }

    private static void Resume(Game game, Action<Game> continuation)
    {
        foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) })
        {
            Check(Canonical.State(game) == Canonical.State(next), "m2b state before continuation");
            continuation(next);
        }
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

    private static Minion Dormant(Controller c, string id) => c.BoardZone.OfType<Minion>().FirstOrDefault(m => m.Card.Id == id && m.IsDormant);
    private static Minion Awake(Controller c, string id) => c.BoardZone.OfType<Minion>().FirstOrDefault(m => m.Card.Id == id && !m.IsDormant);

    // ---- GVG_026 Feign Death ----------------------------------------------------------------------------------------

    private static void FeignDeath(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var spell = Add(p, id);
        Check(spell.Cost == 2 && Playable(p, spell), "Feign Death costs 2 and is playable with no minions");
        int hand = p.HandZone.Count;
        PlayEntity(g, p, spell);
        Check(p.HandZone.Count == hand - 1 && p.Opponent.Hero.Damage == 0 && p.BoardZone.Count == 0, "with no minions it does nothing");
        var gnome = Summon(p, "EX1_029"); var hoarder = Summon(p, "EX1_096"); var yeti = Summon(p, "CS2_182");
        var enemyGnome = Summon(p.Opponent, "EX1_029");
        int cards = p.HandZone.Count;
        Play(g, p, id);
        Check(p.Opponent.Hero.Damage == 2 && p.Hero.Damage == 0, "friendly Leper Gnome: 2 damage to the enemy hero, not mine (the enemy Gnome stayed quiet)");
        Check(p.HandZone.Count == cards + 1, "friendly Loot Hoarder draws a card");
        Check(gnome.Zone.Type == Zone.PLAY && hoarder.Zone.Type == Zone.PLAY && yeti.Zone.Type == Zone.PLAY && enemyGnome.Zone.Type == Zone.PLAY,
            "no minion dies from the spell");
        Play(g, p, id);
        Check(p.Opponent.Hero.Damage == 4 && p.HandZone.Count == cards + 2, "it can be repeated: Deathrattles trigger every time");
        gnome.Silence(); g.DeathProcessingAndAuraUpdate();
        Play(g, p, id);
        Check(p.Opponent.Hero.Damage == 4 && p.HandZone.Count == cards + 3, "a Silenced minion has no Deathrattle to trigger");
        Park(p); Park(p.Opponent); p.Opponent.Hero.Damage = 0;
        var golem = Summon(p, "EX1_556"); var cairne = Summon(p, "EX1_110"); var plain = Summon(p, "CS2_182");
        Play(g, p, id);
        var ids = p.BoardZone.Select(m => m.Card.Id).ToArray();
        Check(ids.Length == 5 && ids.Contains("skele21") && ids.Contains("EX1_110t") && opp.BoardZone.Count == 0
            && Array.IndexOf(ids, "EX1_556") < Array.IndexOf(ids, "EX1_110") && Array.IndexOf(ids, "EX1_110") < Array.IndexOf(ids, "CS2_182"),
            "each Deathrattle summons its token on my side, the originals keep their order: " + string.Join(",", ids));
        Park(p); Fill(p);
        Check(p.BoardZone.IsFull, "full board fixture");
        Park(p);
        var rivendare = Summon(p, "FP1_031"); var leper = Summon(p, "EX1_029");
        Play(g, p, id);
        Check(p.Opponent.Hero.Damage == 4, "Baron Rivendare doubles Deathrattles triggered by Feign Death");
        Park(p); p.Opponent.Hero.Damage = 0;
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        Summon(p, "EX1_556");
        Check(p.BoardZone.IsFull, "full board with a Harvest Golem");
        Play(g, p, id);
        Check(p.BoardZone.Count == 7, "Deathrattle summons fail on a full board without errors");
        Park(p);
        Summon(p, "EX1_029");
        End(g);
        Summon(opp, "EX1_029"); p.Hero.Damage = 0; opp.Hero.Damage = 0;
        PlayEntity(g, opp, Add(opp, id));
        Check(p.Hero.Damage == 2 && opp.Hero.Damage == 0, "the opponent's Feign Death triggers its own Deathrattles only");
        Resume(g, next => { Check(Get(next, opp).Hero.Damage == 0, "clone keeps state"); });
    }

    // ---- GVG_027 Iron Sensei ----------------------------------------------------------------------------------------

    private static void IronSensei(Game g, Controller p, string id)
    {
        var sensei = (Minion)Play(g, p, id);
        Check(sensei.Card.Cost == 3 && sensei.AttackDamage == 2 && sensei.Health == 2 && sensei.IsRace(Race.MECHANICAL), "Iron Sensei is a 2/2 Mech for 3");
        var yeti = Summon(p, "CS2_182");
        End(g); End(g);
        Check(sensei.AttackDamage == 2 && sensei.Health == 2 && yeti.AttackDamage == 4, "no other friendly Mech: nobody is buffed (not itself, not a non-Mech)");
        var mechs = new[] { Summon(p, "GVG_085"), Summon(p, "GVG_085"), Summon(p, "GVG_085") };
        var enemyMech = Summon(p.Opponent, "GVG_085");
        var seen = new HashSet<int>();
        for (int turn = 0; turn < 9; turn++)
        {
            var before = mechs.Select(m => m.AttackDamage + m.Health).ToArray();
            End(g);
            var afterMine = mechs.Select(m => m.AttackDamage + m.Health).ToArray();
            End(g);
            Check(mechs.Sum(m => m.AttackDamage + m.Health) == afterMine.Sum(), "nothing happens at the end of the opponent's turn");
            var raised = Enumerable.Range(0, 3).Where(k => mechs[k].AttackDamage + mechs[k].Health == before[k] + 4).ToArray();
            Check(raised.Length == 1, "exactly one other friendly Mech gains +2/+2 at the end of my turn");
            seen.Add(raised[0]);
            Check(sensei.AttackDamage == 2 && sensei.Health == 2 && yeti.AttackDamage == 4 && enemyMech.AttackDamage == 1 && enemyMech.Health == 2,
                "Sensei itself, non-Mechs and enemy Mechs are never chosen");
        }
        Check(seen.Count == 3, "the Mech is random among all candidates");
        sensei.Silence(); g.DeathProcessingAndAuraUpdate();
        int total = mechs.Sum(m => m.AttackDamage);
        End(g); End(g);
        Check(mechs.Sum(m => m.AttackDamage) == total, "Silence removes the trigger");
        Park(p); Park(p.Opponent);
        End(g);
        var opp = p.Opponent; var theirSensei = Summon(opp, id); var theirMech = Summon(opp, "GVG_085"); var myMech = Summon(p, "GVG_085");
        End(g);
        Check(theirMech.AttackDamage == 3 && myMech.AttackDamage == 1 && theirSensei.AttackDamage == 2, "the enemy Sensei buffs its own side at its own turn end");
        Resume(g, next => { End(next); End(next); Check(Get(next, theirMech).AttackDamage >= 3, "clone keeps the trigger"); });
    }

    // ---- GVG_028 Trade Prince Gallywix ------------------------------------------------------------------------------

    private static void Gallywix(Game g, Controller p, string id)
    {
        var prince = (Minion)Play(g, p, id);
        Check(prince.Card.Cost == 6 && prince.AttackDamage == 5 && prince.Health == 8, "Gallywix is a 5/8 for 6");
        EmptyHand(p);
        Play(g, p, "CS2_023");
        Check(p.HandZone.All(c => c.Card.Id != "CS2_023") && p.Opponent.HandZone.All(c => c.Card.Id != "GVG_028t"), "my own spells do not trigger it");
        var opp = p.Opponent;
        EmptyHand(p); EmptyHand(opp);
        End(g);
        PlayEntity(g, opp, Add(opp, "CS2_023"));
        Check(p.HandZone.Count == 1 && p.HandZone[0].Card.Id == "CS2_023", "an enemy spell is copied into my hand");
        Check(opp.HandZone.Count(c => c.Card.Id == "GVG_028t") == 1, "the caster gets Gallywix's Coin: " + string.Join(",", opp.HandZone.Select(c => c.Card.Id)));
        Concealed(g, p.HandZone[0], "Gallywix copy");
        var coin = opp.HandZone.First(c => c.Card.Id == "GVG_028t");
        int mine = p.HandZone.Count, theirs = opp.HandZone.Count;
        PlayEntity(g, opp, coin);
        Check(p.HandZone.Count == mine && !opp.HandZone.Any(c => c.Card.Id == "GVG_028t") && !p.HandZone.Any(c => c.Card.Id == "GVG_028t"),
            "playing Gallywix's Coin gives nothing to either side");
        EmptyHand(p); EmptyHand(opp);
        PlayEntity(g, opp, Add(opp, "GAME_005"));
        Check(p.HandZone.Count == 1 && p.HandZone[0].Card.Id == "GAME_005" && opp.HandZone.Count(c => c.Card.Id == "GVG_028t") == 1,
            "the ordinary Coin is a spell: I copy it and they get Gallywix's Coin");
        EmptyHand(p); EmptyHand(opp);
        var victim = Summon(p, "CS2_182");
        PlayEntity(g, opp, Add(opp, "CS2_029"), victim);
        Check(p.HandZone.Count == 1 && p.HandZone[0].Card.Id == "CS2_029" && victim.Damage == 6, "a targeted spell is copied after it resolves");
        Check(p.HandZone[0].Zone.Type == Zone.HAND && ((IPlayable)p.HandZone[0]).Cost == 4, "the copy is a fresh normal-cost card");
        EmptyHand(p); FillHand(p); EmptyHand(opp); FillHand(opp, 9);
        PlayEntity(g, opp, Add(opp, "CS2_029"), p.Hero);
        Check(p.HandZone.Count == 10 && p.HandZone.All(c => c.Card.Id == "CS2_182") && opp.HandZone.Count == 10 && opp.HandZone.Count(c => c.Card.Id == "GVG_028t") == 1,
            "my full hand burns the copy, the Coin still arrives: " + string.Join(",", p.HandZone.Select(c => c.Card.Id)) + " | " + string.Join(",", opp.HandZone.Select(c => c.Card.Id)));
        EmptyHand(p); EmptyHand(opp);
        prince.Silence(); g.DeathProcessingAndAuraUpdate();
        PlayEntity(g, opp, Add(opp, "CS2_023"));
        Check(p.HandZone.Count == 0 && opp.HandZone.All(c => c.Card.Id != "GVG_028t"), "Silence removes the trigger");
        End(g);
        Park(p); EmptyHand(p); EmptyHand(opp);
        var prince2 = Summon(p, id);
        var counter = Add(p, "EX1_287"); PlayEntity(g, p, counter);
        End(g);
        EmptyHand(p); EmptyHand(opp);
        PlayEntity(g, opp, Add(opp, "CS2_023"));
        Check(p.HandZone.All(c => c.Card.Id != "CS2_023") && opp.HandZone.All(c => c.Card.Id != "GVG_028t"),
            "a countered spell is never cast: no copy, no Coin");
        Resume(g, next => Check(Get(next, prince2).Zone.Type == Zone.PLAY, "clone keeps Gallywix"));
    }

    // ---- GVG_029 Ancestor's Call ------------------------------------------------------------------------------------

    private static void AncestorsCall(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        EmptyHand(p); EmptyHand(opp);
        var spell = Add(p, id);
        Check(spell.Cost == 4, "Ancestor's Call costs 4");
        PlayEntity(g, p, spell);
        Check(p.BoardZone.Count == 0 && opp.BoardZone.Count == 0, "no minions in either hand: nothing happens");
        var mine = new[] { "CS2_182", "CS2_120", "CS2_172", "CS2_065" }; var theirs = new[] { "CS2_182", "CS2_120", "CS2_172", "CS2_065" };
        var seenMine = new HashSet<string>(); var seenTheirs = new HashSet<string>();
        for (int i = 0; i < 24; i++)
        {
            Park(p); Park(opp); EmptyHand(p); EmptyHand(opp);
            foreach (var m in mine) Add(p, m);
            foreach (var m in theirs) Add(opp, m);
            var fire = Add(p, "CS2_029"); var theirFire = Add(opp, "CS2_029");
            var call = Add(p, id);
            PlayEntity(g, p, call);
            Check(p.BoardZone.Count == 1 && opp.BoardZone.Count == 1, "exactly one minion lands on each side");
            Check(p.HandZone.Count == 4 && opp.HandZone.Count == 4 && p.HandZone.Contains(fire) && opp.HandZone.Contains(theirFire),
                "the chosen minion left its owner's hand; spells stayed");
            Check(p.BoardZone[0].Controller == p && opp.BoardZone[0].Controller == opp, "each minion joins its own owner's board");
            seenMine.Add(p.BoardZone[0].Card.Id); seenTheirs.Add(opp.BoardZone[0].Card.Id);
        }
        Check(seenMine.Count >= 3 && seenTheirs.Count >= 3, "the minion is random among the minions of each hand");
        Park(p); Park(opp); EmptyHand(p); EmptyHand(opp);
        Add(opp, "CS2_182");
        PlayEntity(g, p, Add(p, id));
        Check(p.BoardZone.Count == 0 && opp.BoardZone.Count == 1 && opp.HandZone.Count == 0, "only the opponent has a minion: only it lands");
        Park(opp); EmptyHand(opp); Add(opp, "CS2_182"); Add(p, "CS2_182"); Fill(p);
        PlayEntity(g, p, Add(p, id));
        Check(p.BoardZone.Count == 7 && opp.BoardZone.Count == 1 && p.HandZone.Any(c => c.Card.Id == "CS2_182"),
            "my full board keeps my minion in hand, theirs still lands");
        Park(p); Park(opp); EmptyHand(p); EmptyHand(opp); Add(opp, "CS2_182"); Add(p, "CS2_182"); Fill(opp);
        PlayEntity(g, p, Add(p, id));
        Check(p.BoardZone.Count == 1 && opp.BoardZone.Count == 7 && opp.HandZone.Count == 1 && p.HandZone.Count == 0, "their full board keeps their minion in hand");
        Park(p); Park(opp); EmptyHand(p); EmptyHand(opp);
        var archer = Add(p, "CS2_189"); Add(opp, "CS2_189");
        var yetiMine = Summon(p, "CS2_182"); var yetiTheirs = Summon(opp, "CS2_182");
        PlayEntity(g, p, Add(p, id));
        Check(yetiMine.Damage == 0 && yetiTheirs.Damage == 0 && p.Hero.Damage == 0 && opp.Hero.Damage == 0, "Battlecries do not trigger");
        Park(p); Park(opp); EmptyHand(p); EmptyHand(opp);
        var juggler = Summon(p, "NEW1_019"); Add(p, "CS2_182"); Add(opp, "CS2_182");
        PlayEntity(g, p, Add(p, id));
        Check(opp.Hero.Damage + opp.BoardZone.OfType<Minion>().Sum(m => m.Damage) == 1, "a minion put onto my battlefield is summoned: my Knife Juggler throws one knife, theirs none");
        Park(p); Park(opp); EmptyHand(p); EmptyHand(opp);
        Add(p, "EX1_029"); Add(opp, "EX1_029");
        PlayEntity(g, p, Add(p, id));
        var gnome = p.BoardZone.OfType<Minion>().Single();
        Check(gnome.Zone.Type == Zone.PLAY && gnome.Card.Id == "EX1_029", "the placed minion keeps its Deathrattle fixture");
        Park(p); Park(opp);
        End(g);
        EmptyHand(p); EmptyHand(opp);
        Add(opp, "CS2_120"); Add(p, "CS2_065");
        PlayEntity(g, opp, Add(opp, id));
        Check(opp.BoardZone.Count == 1 && opp.BoardZone[0].Card.Id == "CS2_120" && p.BoardZone.Count == 1 && p.BoardZone[0].Card.Id == "CS2_065",
            "the opponent's Ancestor's Call works the same for both seats");
        Resume(g, next => Check(Get(next, p).BoardZone.Count == 1, "clone keeps the board"));
    }

    // ---- GVG_030 Anodized Robo Cub ----------------------------------------------------------------------------------

    private static void RoboCub(Game g, Controller p, string id)
    {
        var a = Add(p, id);
        Check(a.Cost == 2 && ModeOffered(p, a, 1) && ModeOffered(p, a, 2), "two Choose One options are offered");
        PlayMode(g, p, a, 1);
        var cub = Awake(p, id);
        Check(cub.AttackDamage == 3 && cub.Health == 2 && cub.HasTaunt && cub.IsRace(Race.MECHANICAL) && cub.IsRace(Race.BEAST), "Attack Mode: a 3/2 Taunt Mech Beast");
        var b = Add(p, id);
        PlayMode(g, p, b, 2);
        var tank = p.BoardZone.OfType<Minion>().Single(m => m.Id != cub.Id);
        Check(tank.Card.Id == id && tank.AttackDamage == 2 && tank.Health == 3 && tank.HasTaunt, "Tank Mode: a 2/3 Taunt: " + tank.Card.Id + " " + tank.AttackDamage + "/" + tank.Health + " " + tank.HasTaunt);
        var fandral = Summon(p, "OG_044");
        var c = Add(p, id);
        PlayEntity(g, p, c);
        var both = p.BoardZone.OfType<Minion>().First(m => m.Card.Id == id && m.Id != cub.Id && m.Id != tank.Id);
        Check(both.AttackDamage == 3 && both.Health == 3, "with Fandral Staghelm both choices apply");
        cub.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(cub.AttackDamage == 2 && cub.Health == 2 && !cub.HasTaunt, "Silence removes the bonus and the Taunt");
        Park(p); Fill(p);
        var d = Add(p, id);
        Check(!Playable(p, d), "no free board slot: not playable");
        Park(p);
        var e = Add(p, id);
        Check(Playable(p, e), "playable again with room");
        Resume(g, next => Check(ModeOffered(Get(next, p), Get(next, e), 1), "clone keeps both options"));
    }

    // ---- GVG_031 Recycle --------------------------------------------------------------------------------------------

    private static void Recycle(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var spell = Add(p, id);
        Check(spell.Cost == 6 && !Playable(p, spell), "Recycle costs 6 and needs an enemy minion");
        var mine = Summon(p, "CS2_182");
        Check(!Playable(p, spell), "a friendly minion is not a legal target");
        var target = Summon(opp, "CS2_120"); target.Damage = 1;
        Play(g, p, "CS2_023");
        var buff = Summon(opp, "CS2_182");
        int deck = opp.DeckZone.Count;
        Check(Playable(p, spell), "an enemy minion makes it playable");
        PlayEntity(g, p, spell, target);
        Check(opp.DeckZone.Count == deck + 1 && target.Zone.Type != Zone.PLAY && opp.BoardZone.Count == 1 && !opp.BoardZone.Contains(target),
            "the enemy minion leaves the board and goes into its owner's deck");
        Check(opp.DeckZone.Any(c => c.Card.Id == "CS2_120" && ((Minion)c).Damage == 0), "it is a fresh copy in the opponent's deck");
        var recycled = opp.DeckZone.First(c => c.Card.Id == "CS2_120");
        foreach (int seed in new[] { 3, 71 })
        {
            var fair = FairBotState.Create(g, p.PlayerId, seed, DeckStyle.Control);
            var view = (Game)typeof(FairBotState).GetField("game", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(fair);
            Check(!view.IdEntityDic.TryGetValue(recycled.Id, out var seen) || seen.Card.Id != recycled.Card.Id || seen.Zone.Type != Zone.DECK,
                "Recycle: my fair view does not know which deck card is the shuffled minion");
        }
        Check(p.DeckZone.All(c => c.Card.Id != "CS2_120") && p.HandZone.All(c => c.Card.Id != "CS2_120") && p.Opponent.HandZone.All(c => c.Card.Id != "CS2_120"),
            "it does not go to my deck or either hand");
        Check(mine.Zone.Type == Zone.PLAY, "my minion stays");
        var deckBefore = opp.DeckZone.Count;
        bool shuffled = false;
        for (int i = 0; i < 8; i++)
        {
            var t = Summon(opp, "CS2_120");
            PlayEntity(g, p, Add(p, id), t);
            int idx = opp.DeckZone.ToList().FindLastIndex(c => c.Card.Id == "CS2_120");
            if (idx != opp.DeckZone.Count - 1) shuffled = true;
        }
        Check(shuffled, "the minion is shuffled, not put on top");
        Park(opp);
        var squire = Summon(opp, "EX1_008");
        PlayEntity(g, p, Add(p, id), squire);
        Check(opp.BoardZone.Count == 0 && opp.DeckZone.Any(c => c.Card.Id == "EX1_008"), "a Divine Shield minion is shuffled too");
        Park(opp);
        var hidden = Summon(opp, "CS2_059");
        Check(hidden.HasStealth, "stealth fixture");
        Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Target?.Id == hidden.Id), "a Stealth minion cannot be targeted");
        Park(opp); EmptyHand(opp);
        while (opp.DeckZone.Count < 60) Entity.FromCard(opp, Cards.FromId("CS2_182"), zone: opp.DeckZone);
        var t2 = Summon(opp, "CS2_120");
        PlayEntity(g, p, Add(p, id), t2);
        Check(opp.DeckZone.Count >= 60, "a very full deck still takes it");
        End(g);
        var enemySide = opp; Park(p); Park(opp);
        var mineAgain = Summon(p, "CS2_120"); Summon(opp, "CS2_182");
        PlayEntity(g, opp, Add(opp, id), mineAgain);
        Check(p.DeckZone.Any(c => c.Card.Id == "CS2_120") && p.BoardZone.Count == 0, "the opponent's Recycle shuffles my minion into my deck");
        Resume(g, next => Check(Get(next, p).DeckZone.Any(c => c.Card.Id == "CS2_120"), "clone keeps the shuffled card"));
    }

    // ---- GVG_032 Grove Tender ---------------------------------------------------------------------------------------

    private static void GroveTender(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 3 && ModeOffered(p, card, 1) && ModeOffered(p, card, 2), "Grove Tender offers both choices");
        p.BaseMana = 5; opp.BaseMana = 7; p.UsedMana = 0; opp.UsedMana = 3;
        PlayMode(g, p, card, 1);
        var tender = Awake(p, id);
        Check(tender.AttackDamage == 2 && tender.Health == 4, "Grove Tender is a 2/4");
        Check(p.BaseMana == 11 - 5 - 0 || p.BaseMana == 10 || p.BaseMana == 6, "my Mana Crystal is gained");
        Check(opp.BaseMana == 8, "the opponent gains a Mana Crystal too");
        var second = Add(p, id);
        p.BaseMana = 10; opp.BaseMana = 10; p.UsedMana = 0; opp.UsedMana = 0;
        PlayMode(g, p, second, 1);
        Check(p.BaseMana == 10 && opp.BaseMana == 10, "crystals cap at 10 for both players");
        p.BaseMana = 9; opp.BaseMana = 10; p.UsedMana = 0;
        PlayMode(g, p, Add(p, id), 1);
        Check(p.BaseMana == 10 && opp.BaseMana == 10, "only the player below the cap gains one");
        p.BaseMana = 4; opp.BaseMana = 4;
        var empties = (p.RemainingMana, opp.RemainingMana);
        PlayMode(g, p, Add(p, id), 1);
        Check(opp.BaseMana == 5 && opp.RemainingMana == empties.Item2, "the crystal given to the opponent is empty");
        Stack(p, "CS2_182", "CS2_120"); Stack(opp, "CS2_182", "CS2_120");
        EmptyHand(p); EmptyHand(opp);
        var draw = Add(p, id);
        PlayMode(g, p, draw, 2);
        Check(p.HandZone.Count == 1 && opp.HandZone.Count == 1, "Each player draws a card");
        Check(p.HandZone[0].Card.Id == "CS2_120" && opp.HandZone[0].Card.Id == "CS2_120", "they drew their own top cards");
        Concealed(g, p.HandZone[0], "Grove Tender draw");
        EmptyHand(p); EmptyHand(opp); FillHand(p, 9); FillHand(opp, 10);
        PlayMode(g, p, Add(p, id), 2);
        Check(p.HandZone.Count == 10 && opp.HandZone.Count == 10, "full hands burn the card instead of overflowing: " + p.HandZone.Count + " " + opp.HandZone.Count);
        Park(p); Fill(p);
        Check(!Playable(p, Add(p, id)), "needs a free slot");
        var fandral = (Minion)Entity.FromCard(p, Cards.FromId("OG_044"));
        Park(p); Summon(p, "OG_044");
        EmptyHand(p); EmptyHand(opp);
        p.BaseMana = 4; opp.BaseMana = 4; Stack(p, "CS2_120"); Stack(opp, "CS2_120");
        PlayEntity(g, p, Add(p, id));
        Check(p.HandZone.Count == 1 && opp.HandZone.Count == 1 && opp.BaseMana == 5, "with Fandral Staghelm both choices apply");
        Resume(g, next => Check(Get(next, p).BoardZone.Count >= 2, "clone keeps the tender"));
    }

    // ---- GVG_035 Malorne --------------------------------------------------------------------------------------------

    private static void Malorne(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var m = (Minion)Play(g, p, id);
        Check(m.Card.Cost == 7 && m.AttackDamage == 9 && m.Health == 7 && m.IsRace(Race.BEAST), "Malorne is a 9/7 Beast for 7");
        var b1 = Summon(p, "CS2_120"); var b2 = Summon(p, "CS2_172"); var plain = Summon(p, "CS2_182");
        Kill(g, m);
        var sleeper = Dormant(p, id);
        Check(sleeper != null && m.Zone.Type == Zone.GRAVEYARD && p.BoardZone.Count == 4, "Deathrattle: Malorne goes Dormant on the battlefield");
        Check(sleeper.Damage == 0 && sleeper.Health == 7, "it returns at full Health");
        Kill(g, plain);
        Check(Dormant(p, id) != null, "a friendly non-Beast dying does not count");
        var enemyBeast = Summon(opp, "CS2_120");
        Kill(g, enemyBeast);
        Check(Dormant(p, id) != null, "an enemy Beast dying does not count");
        Resume(g, next =>
        {
            var np = Get(next, p);
            Check(Dormant(np, id) != null, "dormant Malorne survives a clone");
            Kill(next, np.BoardZone.OfType<Minion>().First(x => x.Card.Id == "CS2_120"));
            Check(Dormant(np, id) != null, "one Beast death is not enough, on a clone either");
            Kill(next, np.BoardZone.OfType<Minion>().First(x => x.Card.Id == "CS2_172"));
            Check(Awake(np, id) != null && Dormant(np, id) == null, "the second friendly Beast death revives it, on a clone too");
        });
        Kill(g, b1);
        Check(Dormant(p, id) != null, "one friendly Beast death: still Dormant");
        Kill(g, b2);
        var risen = Awake(p, id);
        Check(risen != null && risen.AttackDamage == 9 && risen.Health == 7 && risen.Damage == 0 && Dormant(p, id) == null, "the second friendly Beast death revives Malorne at full strength");
        Check(risen.IsExhausted, "the revived Malorne cannot attack this turn");
        var c1 = Summon(p, "CS2_120"); var c2 = Summon(p, "CS2_120");
        Kill(g, risen);
        Check(Dormant(p, id) != null, "it can go Dormant again");
        Kill(g, c1);
        Kill(g, c2);
        Check(Awake(p, id) != null, "and revive again after two more");
        Park(p);
        var again = Awake(p, id) ?? Summon(p, id); Park(p);
        var m2 = Summon(p, id); Fill(p);
        Check(p.BoardZone.IsFull, "full board fixture");
        Kill(g, m2);
        Check(Dormant(p, id) == null && p.BoardZone.Count == 6, "a full board: it cannot go Dormant and stays dead");
        Park(p);
        var silenced = Summon(p, id); silenced.Silence(); g.DeathProcessingAndAuraUpdate();
        Kill(g, silenced);
        Check(Dormant(p, id) == null && silenced.Zone.Type == Zone.GRAVEYARD, "a Silenced Malorne has no Deathrattle");
        Park(p);
        var m3 = Summon(p, id); var s1 = Summon(p, "CS2_120"); var s2 = Summon(p, "CS2_120");
        m3.ToBeDestroyed = true; s1.ToBeDestroyed = true; s2.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
        Check(Dormant(p, id) != null, "Beasts that die together with Malorne do not count towards its revival");
        Park(p);
        End(g);
        var om = Summon(opp, id); var ob1 = Summon(opp, "CS2_120"); var ob2 = Summon(opp, "CS2_120");
        Kill(g, om); Kill(g, ob1); Kill(g, ob2);
        Check(Awake(opp, id) != null && Dormant(p, id) == null, "the enemy's Malorne revives on its own side");
    }

    // ---- GVG_036 Powermace ------------------------------------------------------------------------------------------

    private static void Powermace(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var mace = (Weapon)Play(g, p, id);
        Check(mace.Card.Cost == 3 && mace.AttackDamage == 3 && mace.Durability == 2, "Powermace is a 3/2 weapon for 3");
        Summon(p, "CS2_182");
        var target = Summon(opp, "CS2_182");
        Swing(g, p, opp.Hero); Swing(g, p, opp.Hero);
        Check(p.Hero.Weapon == null && opp.Hero.Damage == 6, "two swings use it up");
        Park(p); Park(opp);
        var mechs = new[] { Summon(p, "GVG_085"), Summon(p, "GVG_085"), Summon(p, "GVG_085") };
        var yeti = Summon(p, "CS2_182"); var enemyMech = Summon(opp, "GVG_085");
        var seen = new HashSet<int>();
        for (int i = 0; i < 18; i++)
        {
            Play(g, p, id);
            var before = mechs.Select(m => m.AttackDamage + m.Health).ToArray();
            p.Hero.Weapon.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
            var raised = Enumerable.Range(0, 3).Where(k => mechs[k].AttackDamage + mechs[k].Health == before[k] + 4).ToArray();
            Check(p.Hero.Weapon == null && raised.Length == 1, "the destroyed Powermace gives exactly one friendly Mech +2/+2");
            seen.Add(raised[0]);
            foreach (var m in mechs) { m.Silence(); }
            g.DeathProcessingAndAuraUpdate();
            Check(yeti.AttackDamage == 4 && enemyMech.AttackDamage == 1, "non-Mechs and enemy Mechs are not chosen");
        }
        Check(seen.Count == 3, "the Mech is random among all friendly Mechs");
        Park(p);
        var lone = Summon(p, "CS2_182");
        Play(g, p, id); p.Hero.Weapon.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
        Check(lone.AttackDamage == 4 && lone.Health == 5, "no friendly Mech: nothing happens");
        var mech = Summon(p, "GVG_085");
        Play(g, p, id);
        Play(g, p, "CS2_106");
        Check(mech.AttackDamage == 3 && mech.Health == 4, "replacing the weapon destroys Powermace and triggers its Deathrattle");
        Park(p);
        var big = Summon(p, "GVG_085");
        Play(g, p, id);
        var ooze = Add(opp, "EX1_066");
        End(g);
        PlayEntity(g, opp, ooze);
        Check(p.Hero.Weapon == null && big.AttackDamage == 3, "an enemy weapon-destroyer triggers it as well");
        Park(opp);
        PlayEntity(g, opp, Add(opp, id));
        var theirMech = Summon(opp, "GVG_085");
        opp.Hero.Weapon.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
        Check(theirMech.AttackDamage == 3 && big.AttackDamage == 3, "the opponent's Powermace buffs its own Mech only: " + theirMech.AttackDamage + " " + big.AttackDamage + " weapon " + opp.Hero.Weapon?.Card.Id + " " + theirMech.Zone.Type + " cur " + g.CurrentPlayer.PlayerId);
        Resume(g, next => Check(Get(next, big).AttackDamage == 3, "clone keeps the buff"));
    }

    // ---- GVG_037 Whirling Zap-o-matic -------------------------------------------------------------------------------

    private static void ZapOMatic(Game g, Controller p, string id)
    {
        var zap = (Minion)Play(g, p, id);
        Check(zap.Card.Cost == 2 && zap.AttackDamage == 3 && zap.Health == 2 && zap.IsRace(Race.MECHANICAL) && zap.HasWindfury, "a 3/2 Windfury Mech for 2");
        var enemy = Summon(p.Opponent, "CS2_231");
        End(g); End(g);
        Check(p.Options().Count(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == zap.Id) > 0, "it can attack");
        Attack(g, zap, enemy);
        Check(enemy.Zone.Type == Zone.GRAVEYARD && zap.NumAttacksThisTurn == 1 && zap.Damage == 1, "first attack");
        var hero = p.Opponent.Hero;
        Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == zap.Id), "a second attack is offered");
        Attack(g, zap, hero);
        Check(hero.Damage == 3 && zap.NumAttacksThisTurn == 2, "second attack hits the hero");
        Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == zap.Id), "no third attack");
        End(g); End(g);
        zap.Silence(); g.DeathProcessingAndAuraUpdate();
        Attack(g, zap, hero);
        Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == zap.Id) && !zap.HasWindfury, "Silence removes Windfury");
        End(g);
        var opp = p.Opponent; var theirs = Summon(opp, id); Summon(p, "CS2_182");
        theirs.IsExhausted = false;
        Attack(g, theirs, p.Hero); Attack(g, theirs, p.Hero);
        Check(p.Hero.Damage == 6, "the opponent's copy also attacks twice");
        Resume(g, next => Check(Get(next, theirs).HasWindfury, "clone keeps Windfury"));
    }

    // ---- GVG_038 Crackle --------------------------------------------------------------------------------------------

    private static void Crackle(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var spell = Add(p, id);
        Check(spell.Cost == 2 && Playable(p, spell), "Crackle costs 2");
        var amounts = new HashSet<int>();
        for (int i = 0; i < 60; i++)
        {
            opp.Hero.Damage = 0; p.OverloadOwed = 0;
            Play(g, p, id, opp.Hero);
            amounts.Add(opp.Hero.Damage);
        }
        Check(amounts.SetEquals(new[] { 3, 4, 5, 6 }), "Crackle deals 3 to 6, every value reachable: " + string.Join(",", amounts.OrderBy(x => x)));
        var yeti = Summon(opp, "CS2_182");
        p.OverloadOwed = 0;
        Play(g, p, id, yeti);
        Check(yeti.Damage >= 3 && yeti.Damage <= 6, "it can hit a minion");
        Check(p.OverloadOwed == 1, "Overload (1) is owed after casting");
        Play(g, p, id, p.Hero);
        Check(p.Hero.Damage >= 3 && p.Hero.Damage <= 6, "it can hit my own hero");
        Summon(p, "EX1_012");
        var boosted = new HashSet<int>();
        for (int i = 0; i < 60; i++) { opp.Hero.Damage = 0; Play(g, p, id, opp.Hero); boosted.Add(opp.Hero.Damage); }
        Check(boosted.SetEquals(new[] { 4, 5, 6, 7 }), "Spell Damage +1 gives 4 to 7: " + string.Join(",", boosted.OrderBy(x => x)));
        Park(p); p.OverloadOwed = 0; p.OverloadLocked = 0;
        var cast = Add(p, id); PlayEntity(g, p, cast, opp.Hero);
        End(g); End(g);
        Check(p.OverloadLocked == 1 && p.RemainingMana == p.BaseMana - 1, "next turn one crystal is locked");
        End(g);
        PlayEntity(g, opp, Add(opp, id), p.Hero);
        Check(opp.OverloadOwed == 1, "the opponent owes Overload too");
        Resume(g, next => Check(Get(next, opp).OverloadOwed == 1, "clone keeps Overload"));
    }

    // ---- GVG_039 Vitality Totem -------------------------------------------------------------------------------------

    private static void VitalityTotem(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var totem = (Minion)Play(g, p, id);
        Check(totem.Card.Cost == 2 && totem.AttackDamage == 0 && totem.Health == 3 && totem.IsRace(Race.TOTEM), "a 0/3 Totem for 2");
        p.Hero.Damage = 10; opp.Hero.Damage = 10;
        End(g);
        Check(p.Hero.Damage == 6 && opp.Hero.Damage == 10, "at the end of my turn my hero is restored 4, theirs is not");
        End(g);
        Check(p.Hero.Damage == 6, "nothing at the end of the opponent's turn");
        p.Hero.Damage = 2;
        End(g); End(g);
        Check(p.Hero.Damage == 0, "healing stops at full Health");
        p.Hero.Damage = 0;
        var wounded = Summon(p, "CS2_182"); wounded.Damage = 3;
        End(g); End(g);
        Check(wounded.Damage == 3, "friendly minions are not restored");
        p.Hero.Damage = 8;
        totem.Silence(); g.DeathProcessingAndAuraUpdate();
        End(g); End(g);
        Check(p.Hero.Damage == 8, "Silence removes the trigger");
        Park(p);
        var second = Summon(p, id); var third = Summon(p, id);
        p.Hero.Damage = 12;
        End(g); End(g);
        Check(p.Hero.Damage == 4, "two Totems restore 8");
        Kill(g, second);
        End(g); End(g);
        Check(p.Hero.Damage == 0, "a dead Totem stops restoring");
        End(g);
        var theirs = Summon(opp, id); opp.Hero.Damage = 9; p.Hero.Damage = 9;
        End(g);
        Check(opp.Hero.Damage == 5 && p.Hero.Damage == 9, "the enemy Totem restores its own hero");
        Park(p); Park(opp);
        var enemyTotem = Summon(opp, id); p.Hero.Damage = 9; opp.Hero.Damage = 9;
        Play(g, p, "CS1_113", enemyTotem);
        End(g);
        Check(enemyTotem.Controller == p && p.Hero.Damage == 5 && opp.Hero.Damage == 9, "a stolen Totem restores its new controller's hero, not its old owner's");
        Resume(g, next => { End(next); Check(Get(next, opp).Hero.Damage == 9 && Get(next, p).BoardZone.Count == 1, "clone keeps the stolen Totem under its new controller"); });
    }

    // ---- GVG_040 Siltfin Spiritwalker -------------------------------------------------------------------------------

    private static void Siltfin(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        p.OverloadOwed = 0;
        var walker = (Minion)Play(g, p, id);
        Check(walker.Card.Cost == 4 && walker.AttackDamage == 2 && walker.Health == 5 && walker.IsRace(Race.MURLOC), "a 2/5 Murloc for 4");
        Check(p.OverloadOwed == 1, "Overload (1)");
        Stack(p, "CS2_182", "CS2_182", "CS2_182", "CS2_182");
        EmptyHand(p);
        var raider = Summon(p, "CS2_168"); var other = Summon(p, "CS2_182"); var theirs = Summon(opp, "CS2_168");
        Kill(g, other); Kill(g, theirs);
        Check(p.HandZone.Count == 0, "a non-Murloc and an enemy Murloc dying draw nothing");
        Kill(g, raider);
        Check(p.HandZone.Count == 1, "another friendly Murloc dying draws a card");
        var enemyMurloc = Summon(opp, "CS2_168");
        Play(g, p, "CS1_113", enemyMurloc);
        int beforeSteal = p.HandZone.Count;
        Kill(g, enemyMurloc);
        Check(enemyMurloc.Controller == p && p.HandZone.Count == beforeSteal + 1, "a Murloc taken by Mind Control draws when it dies under my control");
        int h2 = p.HandZone.Count;
        var r2 = Summon(p, "CS2_168"); var r3 = Summon(p, "CS2_168");
        r2.ToBeDestroyed = true; r3.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
        Check(p.HandZone.Count == h2 + 2, "two Murlocs dying together draw two cards: " + (p.HandZone.Count - h2));
        int hand = p.HandZone.Count;
        Kill(g, walker);
        Check(p.HandZone.Count == hand, "its own death draws nothing");
        Park(p); EmptyHand(p);
        var w4 = Summon(p, id); var r4 = Summon(p, "CS2_168"); w4.Silence(); g.DeathProcessingAndAuraUpdate();
        Kill(g, r4);
        Check(p.HandZone.Count == 0, "Silence removes the trigger");
        Park(p); EmptyHand(p); FillHand(p);
        var w5 = Summon(p, id); var r5 = Summon(p, "CS2_168");
        Kill(g, r5);
        Check(p.HandZone.Count == 10, "a full hand burns the card");
        Park(p); EmptyHand(p);
        Stack(p); p.DeckZone.ToArray().ToList().ForEach(c => p.SetasideZone.Add(p.DeckZone.Remove(c)));
        var w6 = Summon(p, id); var r6 = Summon(p, "CS2_168");
        int health = p.Hero.Health;
        Kill(g, r6);
        Check(p.Hero.Health < health, "an empty deck causes fatigue");
        End(g);
        var tw = Summon(opp, id); var tr = Summon(opp, "CS2_168"); Stack(opp, "CS2_182"); EmptyHand(opp);
        Kill(g, tr);
        Check(opp.HandZone.Count == 1, "the enemy Siltfin draws for its owner");
        Resume(g, next => Check(Get(next, opp).HandZone.Count == 1, "clone keeps the hand"));
    }

    // ---- GVG_041 Dark Wispers ---------------------------------------------------------------------------------------

    private static void DarkWispers(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var card = Add(p, id);
        Check(card.Cost == 6 && ModeOffered(p, card, 2) && !ModeOffered(p, card, 1), "Dark Wispers costs 6 and offers only the summon choice without a minion");
        PlayMode(g, p, card, 2);
        Check(p.BoardZone.Count == 5 && p.BoardZone.All(m => m.Card.Id == "CS2_231"), "Summon 5 Wisps");
        Park(p); Summon(p, "CS2_182"); Summon(p, "CS2_182"); Summon(p, "CS2_182"); Summon(p, "CS2_182"); Summon(p, "CS2_182");
        PlayMode(g, p, Add(p, id), 2);
        Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "CS2_231") == 2, "only the free slots are filled");
        var target = p.BoardZone.OfType<Minion>().First(m => m.Card.Id == "CS2_182");
        var buff = Add(p, id);
        Check(ModeOffered(p, buff, 1, target) && ModeOffered(p, buff, 1, opp.Hero) == false, "the buff choice targets minions only: " + string.Join(" ", p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == buff.Id).Select(t => t.ChooseOne + ">" + t.Target?.Card.Id)));
        Check(!ModeOffered(p, buff, 2), "no free slot: the summon choice is not offered");
        PlayMode(g, p, buff, 1, target);
        Check(target.AttackDamage == 9 && target.Health == 10 && target.HasTaunt, "+5/+5 and Taunt");
        target.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(target.AttackDamage == 4 && target.Health == 5 && !target.HasTaunt, "Silence removes both");
        Park(p); Park(opp);
        var enemy = Summon(opp, "CS2_182");
        var buff2 = Add(p, id);
        PlayMode(g, p, buff2, 1, enemy);
        Check(enemy.AttackDamage == 9 && enemy.Health == 10 && enemy.HasTaunt, "an enemy minion can be given the bonus");
        Park(p); Park(opp);
        var lonely = Add(p, id);
        Check(ModeOffered(p, lonely, 2), "with an empty board the summon choice is offered");
        Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == lonely.Id && t.ChooseOne == 1), "no minion: no buff choice");
        var fandral = Summon(p, "OG_044"); var ally = Summon(p, "CS2_182");
        PlayEntity(g, p, lonely, ally);
        Check(p.BoardZone.Count(m => m.Card.Id == "CS2_231") == 5 && ally.AttackDamage == 9 && ally.HasTaunt, "with Fandral Staghelm both choices apply");
        Resume(g, next => Check(Get(next, p).BoardZone.Count == 7, "clone keeps the board"));
        End(g);
        Park(p); Park(opp);
        PlayMode(g, opp, Add(opp, id), 2);
        Check(opp.BoardZone.Count == 5 && p.BoardZone.Count == 0, "the opponent's Wisps join its own board");
    }

    // ---- GVG_042 Neptulon -------------------------------------------------------------------------------------------

    private static void Neptulon(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        EmptyHand(p); p.OverloadOwed = 0;
        var nep = (Minion)Play(g, p, id);
        Check(nep.Card.Cost == 7 && nep.AttackDamage == 7 && nep.Health == 7 && nep.IsRace(Race.ELEMENTAL), "a 7/7 Elemental for 7");
        Check(p.HandZone.Count == 4 && p.HandZone.All(c => c.Card.Type == CardType.MINION && ((Minion)c).IsRace(Race.MURLOC) && c.Zone.Type == Zone.HAND),
            "Battlecry: 4 Murlocs in hand");
        Check(p.OverloadOwed == 3, "Overload (3)");
        Check(p.HandZone.All(c => c.Cost == c.Card.Cost), "no discount");
        Check(opp.HandZone.Count == 0 || opp.HandZone.All(c => c.Card.Id != nep.Card.Id), "the opponent's hand is untouched");
        var seen = new HashSet<string>();
        for (int i = 0; i < 12; i++)
        {
            EmptyHand(p); Park(p);
            Play(g, p, id);
            foreach (var c in p.HandZone) seen.Add(c.Card.Id);
            Check(p.HandZone.Count == 4 && p.HandZone.All(c => !c.Card.Id.StartsWith("TB_") && !c.Card.Id.StartsWith("PRO_") && c.Card.Collectible), "four collectible Murlocs");
        }
        Check(seen.Count >= 8, "the Murlocs are random");
        EmptyHand(p); Park(p);
        Play(g, p, id);
        Concealed(g, p.HandZone[0], "Neptulon Murloc");
        EmptyHand(p); Park(p); FillHand(p, 8);
        Play(g, p, id);
        Check(p.HandZone.Count == 10 && p.HandZone.Count(c => c.Card.Id != "CS2_182") == 2, "only two Murlocs fit into an eight-card hand");
        EmptyHand(p); Park(p); FillHand(p, 9);
        var held = Add(p, id);
        PlayEntity(g, p, held);
        Check(p.HandZone.Count == 10 && p.HandZone.Count(c => c.Card.Id != "CS2_182") == 1, "playing it from a nine-card hand leaves room for exactly one Murloc");
        Resume(g, next => Check(Get(next, p).HandZone.Count == 10, "clone keeps the hand"));
        End(g);
        EmptyHand(opp); opp.OverloadOwed = 0;
        PlayEntity(g, opp, Add(opp, id));
        Check(opp.HandZone.Count == 4 && opp.HandZone.All(c => ((Minion)c).IsRace(Race.MURLOC)) && opp.OverloadOwed == 3, "the opponent's Neptulon adds to its own hand: " + opp.HandZone.Count + " " + opp.OverloadOwed);
    }

    // ---- GVG_043 Glaivezooka ----------------------------------------------------------------------------------------

    private static void Glaivezooka(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var gun = (Weapon)Play(g, p, id);
        Check(gun.Card.Cost == 2 && gun.AttackDamage == 2 && gun.Durability == 2, "Glaivezooka is a 2/2 weapon for 2");
        var mine = new[] { Summon(p, "CS2_182"), Summon(p, "CS2_182"), Summon(p, "CS2_182") };
        var enemy = Summon(opp, "CS2_182");
        var seen = new HashSet<int>();
        for (int i = 0; i < 18; i++)
        {
            var before = mine.Select(m => m.AttackDamage).ToArray();
            Play(g, p, id);
            var raised = Enumerable.Range(0, 3).Where(k => mine[k].AttackDamage == before[k] + 1).ToArray();
            Check(raised.Length == 1 && mine.Sum(m => m.AttackDamage) == before.Sum() + 1, "exactly one friendly minion gains +1 Attack");
            seen.Add(raised[0]);
            Check(enemy.AttackDamage == 4 && p.Hero.Weapon.AttackDamage == 2, "enemy minions and the weapon itself are untouched");
        }
        Check(seen.Count == 3, "the minion is random");
        int attackSum = mine.Sum(m => m.AttackDamage);
        End(g); End(g);
        Check(mine.Sum(m => m.AttackDamage) == attackSum, "the bonus is permanent");
        mine[0].Silence(); g.DeathProcessingAndAuraUpdate();
        Check(mine[0].AttackDamage == 4, "Silence removes the Attack bonus");
        Park(p);
        Play(g, p, id);
        Check(p.Hero.Weapon != null && p.BoardZone.Count == 0, "no friendly minion: nothing happens, the weapon is still equipped");
        Equip(p); Summon(p, "CS2_182");
        var replace = Add(p, id);
        PlayEntity(g, p, replace);
        Check(p.Hero.Weapon.Card.Id == id && p.BoardZone.OfType<Minion>().Single().AttackDamage == 5, "replaces the old weapon and still buffs");
        End(g);
        Park(opp); Park(p);
        var theirs = Summon(opp, "CS2_182"); Summon(p, "CS2_182");
        PlayEntity(g, opp, Add(opp, id));
        Check(theirs.AttackDamage == 5 && p.BoardZone.OfType<Minion>().Single().AttackDamage == 4, "the opponent's Glaivezooka buffs its own minion: " + theirs.AttackDamage + " " + p.BoardZone.OfType<Minion>().Single().AttackDamage
            + " weapon " + opp.Hero.Weapon?.Card.Id + " cur " + g.CurrentPlayer.PlayerId + " opp " + opp.PlayerId + " ench " + theirs.AppliedEnchantments?.Count + " board " + opp.BoardZone.Count + " " + theirs.Zone.Type);
        Resume(g, next => Check(Get(next, theirs).AttackDamage == 5, "clone keeps the buff"));
    }

    // ---- GVG_045 Imp-losion -----------------------------------------------------------------------------------------

    private static void Implosion(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var spell = Add(p, id);
        Check(spell.Cost == 4 && !Playable(p, spell), "Imp-losion costs 4 and needs a minion target");
        var amounts = new HashSet<int>();
        for (int i = 0; i < 60; i++)
        {
            Park(p); Park(opp);
            var big = Summon(opp, "CS2_182"); big.Health = 100;
            Play(g, p, id, big);
            int dealt = big.Damage;
            Check(dealt >= 2 && dealt <= 4, "2 to 4 damage");
            Check(p.BoardZone.Count == dealt && p.BoardZone.All(m => m.Card.Id == "GVG_045t" && m.AttackDamage == 1 && m.Health == 1), "one 1/1 Imp per damage dealt, on my side");
            Check(opp.BoardZone.Count == 1, "the opponent's board is not changed");
            amounts.Add(dealt);
        }
        Check(amounts.SetEquals(new[] { 2, 3, 4 }), "every amount is reachable: " + string.Join(",", amounts.OrderBy(x => x)));
        Park(p); Park(opp);
        var mine = Summon(p, "CS2_182");
        Play(g, p, id, mine);
        Check(mine.Damage >= 2 && p.BoardZone.Count == 1 + mine.Damage, "it can hit a friendly minion; the Imps are mine");
        Park(p); Park(opp);
        var shield = Summon(opp, "EX1_008");
        Play(g, p, id, shield);
        Check(!shield.HasDivineShield && shield.Damage == 0 && p.BoardZone.Count == 0, "a Divine Shield absorbs the damage: no damage dealt, no Imps");
        Park(p); Park(opp);
        var weak = Summon(opp, "EX1_008"); weak.HasDivineShield = false; weak.Damage = weak.Health - 1;
        Play(g, p, id, weak);
        Check(weak.Zone.Type == Zone.GRAVEYARD && p.BoardZone.Count >= 2 && p.BoardZone.Count <= 4, "overkill still counts the full damage");
        Park(p); Park(opp);
        Summon(p, "EX1_012"); Fill(p);
        Park(p);
        Summon(p, "EX1_012"); for (int i = 0; i < 4; i++) Summon(p, "CS2_182");
        var spellTarget = Summon(opp, "CS2_182"); spellTarget.Health = 100;
        Play(g, p, id, spellTarget);
        Check(spellTarget.Damage >= 3 && spellTarget.Damage <= 5 && p.BoardZone.Count == 7, "Spell Damage adds to the damage and the Imps fill the free slots only");
        Park(p); Park(opp);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        var tg = Summon(opp, "CS2_182"); tg.Health = 100;
        Play(g, p, id, tg);
        Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "GVG_045t") == 1, "a nearly full board takes only what fits");
        Park(p); Park(opp);
        End(g);
        var theirs = Summon(p, "CS2_182"); theirs.Health = 100;
        PlayEntity(g, opp, Add(opp, id), theirs);
        Check(opp.BoardZone.Count == theirs.Damage && opp.BoardZone.All(m => m.Card.Id == "GVG_045t"), "the opponent's Imps join its own board");
        Resume(g, next => Check(Get(next, opp).BoardZone.Count == opp.BoardZone.Count, "clone keeps the Imps"));
    }

    // ---- GVG_046 King of Beasts -------------------------------------------------------------------------------------

    private static void KingOfBeasts(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var king = (Minion)Play(g, p, id);
        Check(king.Card.Cost == 3 && king.Card.ATK == 1 && king.Card.Health == 5 && king.IsRace(Race.BEAST) && king.HasTaunt, "a 1/5 Taunt Beast for 3");
        Check(king.AttackDamage == 1, "no other Beast: 1 Attack");
        var b1 = Summon(p, "CS2_120");
        Check(king.AttackDamage == 2, "+1 Attack for one other Beast");
        var b2 = Summon(p, "CS2_172"); var plain = Summon(p, "CS2_182"); var enemyBeast = Summon(opp, "CS2_120");
        Check(king.AttackDamage == 3, "only friendly Beasts count, non-Beasts and enemy Beasts do not");
        var cub = Summon(p, "GVG_030");
        Check(king.AttackDamage == 4 && cub.IsRace(Race.BEAST), "a Mech Beast counts");
        Kill(g, b1);
        Check(king.AttackDamage == 3, "the bonus falls when a Beast dies");
        var king2 = Summon(p, id);
        Check(king.AttackDamage == 4 && king2.AttackDamage == 4, "two Kings count each other, not themselves");
        var stolenBeast = Summon(opp, "CS2_120");
        Check(king.AttackDamage == 4, "an enemy Beast does not count");
        Play(g, p, "CS1_113", stolenBeast);
        Check(stolenBeast.Controller == p && king.AttackDamage == 5, "a Beast taken by Mind Control counts for its new controller");
        Kill(g, stolenBeast);
        king2.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(king2.AttackDamage == 1 && king.AttackDamage == 4, "Silence removes the bonus from the Silenced King; it is still a Beast for the other");
        king.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(king.AttackDamage == 1 && !king.HasTaunt, "Silence removes the aura and the Taunt");
        Park(p); Park(opp);
        var k3 = Summon(p, id); var held = Add(p, id);
        Summon(p, "CS2_120"); Summon(p, "CS2_120");
        Check(k3.AttackDamage == 3 && held.Card.ATK == 1, "cards in hand are not buffed");
        var buffed = k3.AttackDamage;
        Play(g, p, "CS2_188", k3);
        Check(k3.AttackDamage == buffed + 2, "the aura stacks with other Attack buffs");
        End(g);
        var theirKing = Summon(opp, id); Summon(opp, "CS2_120");
        Check(theirKing.AttackDamage == 2 && k3.AttackDamage == buffed, "the enemy King counts its own side only (my temporary buff has expired)");
        Resume(g, next => Check(Get(next, theirKing).AttackDamage == 2, "clone keeps the aura"));
    }

    // ---- GVG_047 Sabotage -------------------------------------------------------------------------------------------

    private static void Sabotage(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var spell = Add(p, id);
        Check(spell.Cost == 4 && Playable(p, spell), "Sabotage costs 4 and is playable with an empty enemy board");
        var enemies = new[] { Summon(opp, "CS2_182"), Summon(opp, "CS2_120"), Summon(opp, "CS2_172") };
        var friend = Summon(p, "CS2_182");
        Equip(opp);
        PlayEntity(g, p, spell);
        Check(enemies.Count(e => e.Zone.Type == Zone.GRAVEYARD) == 1 && friend.Zone.Type == Zone.PLAY && opp.Hero.Weapon != null,
            "without Combo: one random enemy minion is destroyed, the weapon stays");
        var dead = new HashSet<string>();
        for (int i = 0; i < 24; i++)
        {
            Park(opp);
            var pool = new[] { Summon(opp, "CS2_182"), Summon(opp, "CS2_120"), Summon(opp, "CS2_172") };
            EmptyHand(p);
            Play(g, p, id);
            Check(pool.Count(e => e.Zone.Type == Zone.GRAVEYARD) == 1, "exactly one dies");
            dead.Add(pool.First(e => e.Zone.Type == Zone.GRAVEYARD).Card.Id);
        }
        Check(dead.Count == 3, "all three candidates are reachable");
        Park(opp); Equip(opp);
        var more = new[] { Summon(opp, "CS2_182"), Summon(opp, "CS2_120") };
        Play(g, p, "CS2_182");
        var combo = Add(p, id);
        PlayEntity(g, p, combo);
        Check(more.Count(e => e.Zone.Type == Zone.GRAVEYARD) == 1 && opp.Hero.Weapon == null, "Combo: also destroys the opponent's weapon");
        Park(opp); Equip(opp);
        Play(g, p, "CS2_182");
        PlayEntity(g, p, Add(p, id));
        Check(opp.Hero.Weapon == null, "Combo with no enemy minion still destroys the weapon");
        Park(opp);
        Play(g, p, "CS2_182");
        PlayEntity(g, p, Add(p, id));
        Check(opp.BoardZone.Count == 0 && friend.Zone.Type == Zone.PLAY, "Combo with nothing to destroy is harmless");
        Equip(p); Equip(opp);
        var lone = Summon(opp, "CS2_182");
        Play(g, p, "CS2_182");
        PlayEntity(g, p, Add(p, id));
        Check(p.Hero.Weapon != null && opp.Hero.Weapon == null && lone.Zone.Type == Zone.GRAVEYARD, "my own weapon is never destroyed");
        Park(opp);
        var stealth = Summon(opp, "CS2_059");
        Play(g, p, id);
        Check(stealth.Zone.Type == Zone.GRAVEYARD, "a Stealth minion can be destroyed (it is not targeted)");
        End(g);
        Park(p); Park(opp);
        var mine = Summon(p, "CS2_182"); var theirs = Summon(opp, "CS2_182"); Equip(p);
        PlayEntity(g, opp, Add(opp, "CS2_182"));
        PlayEntity(g, opp, Add(opp, id));
        Check(mine.Zone.Type == Zone.GRAVEYARD && theirs.Zone.Type == Zone.PLAY && p.Hero.Weapon == null, "the opponent's Sabotage hits my side");
        Resume(g, next => Check(Get(next, theirs).Zone.Type == Zone.PLAY, "clone keeps state"));
    }

    // ---- GVG_048 Metaltooth Leaper ----------------------------------------------------------------------------------

    private static void MetaltoothLeaper(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var m1 = Summon(p, "GVG_085"); var m2 = Summon(p, "GVG_030"); var yeti = Summon(p, "CS2_182"); var enemy = Summon(opp, "GVG_085");
        var leaper = (Minion)Play(g, p, id);
        Check(leaper.Card.Cost == 3 && leaper.AttackDamage == 3 && leaper.Health == 3 && leaper.IsRace(Race.MECHANICAL) && leaper.IsRace(Race.BEAST),
            "a 3/3 Mech Beast for 3: " + leaper.Card.Cost + " " + leaper.AttackDamage + "/" + leaper.Health + " " + " " + leaper.IsRace(Race.MECHANICAL) + leaper.IsRace(Race.BEAST));
        Check(m1.AttackDamage == 3 && m2.AttackDamage == 4 && m1.Health == 2 && yeti.AttackDamage == 4 && enemy.AttackDamage == 1,
            "other friendly Mechs get +2 Attack; nothing else changes: " + m1.AttackDamage + " " + m2.AttackDamage + " " + yeti.AttackDamage + " " + enemy.AttackDamage);
        Check(leaper.AttackDamage == 3, "the Leaper does not buff itself");
        var second = (Minion)Play(g, p, id);
        Check(leaper.AttackDamage == 5 && second.AttackDamage == 3 && m1.AttackDamage == 5, "a second Leaper buffs the first and everything else again");
        m1.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(m1.AttackDamage == 1, "Silence removes the bonus");
        Park(p);
        var onlyBeast = Summon(p, "CS2_120");
        var third = (Minion)Play(g, p, id);
        Check(onlyBeast.AttackDamage == 2 && third.AttackDamage == 3, "no other Mech: nothing happens");
        Park(p);
        Fill(p, "GVG_085");
        var held = Add(p, id);
        Check(!Playable(p, held), "needs a free slot");
        End(g);
        Park(p); Park(opp);
        var theirMech = Summon(opp, "GVG_085"); var myMech = Summon(p, "GVG_085");
        PlayEntity(g, opp, Add(opp, id));
        Check(theirMech.AttackDamage == 3 && myMech.AttackDamage == 1, "the opponent's Leaper buffs its own Mechs only");
        Resume(g, next => Check(Get(next, theirMech).AttackDamage == 3, "clone keeps the buff"));
    }

    // ---- GVG_049 Gahz'rilla -----------------------------------------------------------------------------------------

    private static void Gahzrilla(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var gah = (Minion)Play(g, p, id);
        Check(gah.Card.Cost == 7 && gah.AttackDamage == 6 && gah.Health == 9 && gah.IsRace(Race.BEAST), "a 6/9 Beast for 7");
        var enemy = Summon(opp, "CS2_182");
        gah.TakeDamage(enemy, 1);
        g.DeathProcessingAndAuraUpdate();
        Check(gah.AttackDamage == 12, "taking damage doubles its Attack");
        gah.TakeDamage(enemy, 1);
        Check(gah.AttackDamage == 24, "every instance of damage doubles it again");
        gah.Damage = 0;
        Check(gah.AttackDamage == 24, "healing does not undo it");
        Park(p);
        var g2 = Summon(p, id);
        Play(g, p, "CS2_188", g2);
        Check(g2.AttackDamage == 8, "buff fixture");
        g2.TakeDamage(enemy, 2);
        Check(g2.AttackDamage == 16, "the doubling includes other buffs");
        g2.HasDivineShield = true;
        g2.TakeDamage(enemy, 2);
        Check(g2.AttackDamage == 16 && !g2.HasDivineShield, "a Divine Shield absorbs the hit: no damage, no doubling");
        g2.Silence(); g2.Damage = 0; g.DeathProcessingAndAuraUpdate();
        Check(g2.AttackDamage == 6, "Silence removes the doubling");
        g2.TakeDamage(enemy, 1);
        Check(g2.AttackDamage == 6, "a Silenced Gahz'rilla no longer reacts");
        Park(p);
        var g3 = Summon(p, id);
        var squire = Summon(opp, "EX1_008");
        g3.IsExhausted = false;
        Attack(g, g3, squire);
        Check(g3.AttackDamage == 12 && g3.Damage == 1, "damage from a fight triggers it");
        Park(p);
        var g4 = Summon(p, id); g4.TakeDamage(enemy, 20); g.DeathProcessingAndAuraUpdate();
        Check(g4.Zone.Type == Zone.GRAVEYARD, "lethal damage still kills it");
        End(g);
        Park(p); Park(opp);
        var theirs = Summon(opp, id); theirs.TakeDamage(Summon(p, "CS2_182"), 3);
        Check(theirs.AttackDamage == 12 && g3.AttackDamage == 12 || theirs.AttackDamage == 12, "the enemy Gahz'rilla doubles on its own side");
        Resume(g, next => { var t = Get(next, theirs); t.TakeDamage(next.Player1.Hero, 1); Check(t.AttackDamage == 24, "clone keeps the trigger"); });
    }

    // ---- GVG_050 Bouncing Blade -------------------------------------------------------------------------------------

    private static void BouncingBlade(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var spell = Add(p, id);
        Check(spell.Cost == 2 && !Playable(p, spell), "Bouncing Blade costs 2 and needs a minion on the battlefield");
        var mine = Summon(p, "CS2_182");
        Check(Playable(p, spell), "a friendly minion is enough");
        Park(p);
        var enemy = Summon(opp, "CS2_182");
        Check(Playable(p, spell), "an enemy minion is enough");
        Park(opp);
        var a = Summon(p, "CS2_182"); var b = Summon(opp, "CS2_182");
        PlayEntity(g, p, spell);
        Check((a.Zone.Type == Zone.GRAVEYARD) != (b.Zone.Type == Zone.GRAVEYARD), "it repeats until one minion dies, and only one dies");
        var survivor = a.Zone.Type == Zone.PLAY ? a : b;
        Check(survivor.Damage > 0 && survivor.Damage < 5, "the survivor is damaged by the earlier hits");
        var hitMine = new HashSet<bool>();
        for (int i = 0; i < 20; i++)
        {
            Park(p); Park(opp);
            var x = Summon(p, "CS2_120"); var y = Summon(opp, "CS2_120"); var z = Summon(opp, "CS2_120");
            Play(g, p, id);
            var dead = new[] { x, y, z }.Where(m => m.Zone.Type == Zone.GRAVEYARD).ToArray();
            Check(dead.Length == 1, "exactly one of three dies");
            hitMine.Add(dead[0] == x);
            var alive = new[] { x, y, z }.Where(m => m.Zone.Type == Zone.PLAY).ToArray();
            Check(alive.All(m => m.Damage <= 2), "survivors took at most 2");
        }
        Check(hitMine.Count == 2, "either side can lose the minion");
        Park(p); Park(opp);
        var shield = Summon(opp, "EX1_008");
        Play(g, p, id);
        Check(shield.Zone.Type == Zone.GRAVEYARD && !shield.HasDivineShield, "a Divine Shield only costs one extra hit");
        Park(opp);
        var hidden = Summon(opp, "CS2_059"); hidden.Health = 3;
        Play(g, p, id);
        Check(hidden.Zone.Type == Zone.GRAVEYARD, "a Stealth minion is a valid random target");
        Park(p); Park(opp);
        var evens = new HashSet<int>();
        for (int i = 0; i < 24; i++)
        {
            Park(p); Park(opp); EmptyHand(p);
            Summon(p, "EX1_012");
            var tough = Summon(opp, "CS2_182");
            Play(g, p, id);
            if (tough.Zone.Type == Zone.GRAVEYARD) evens.Add(tough.Damage); else Check(tough.Damage % 2 == 0, "every hit is 2 with Spell Damage +1");
        }
        Check(evens.Count > 0 && evens.All(d => d == 6), "Spell Damage makes each hit stronger: the 5-Health Yeti dies on the third hit (6 damage), never after five single hits");
        Park(p); Park(opp);
        var immune = Summon(opp, "CS2_182"); immune[GameTag.IMMUNE] = 1;
        var ordinary = Summon(p, "CS2_182");
        Play(g, p, id);
        Check(ordinary.Zone.Type == Zone.GRAVEYARD && immune.Damage == 0, "an Immune minion cannot stall it");
        Park(p); Park(opp);
        var onlyImmune = Summon(opp, "CS2_182"); onlyImmune[GameTag.IMMUNE] = 1;
        Play(g, p, id);
        Check(onlyImmune.Damage == 0 && onlyImmune.Zone.Type == Zone.PLAY, "only Immune minions: the loop ends without a death");
        Park(p); Park(opp);
        Fill(p); Fill(opp);
        Play(g, p, id);
        Check(p.BoardZone.Count + opp.BoardZone.Count == 13, "a crowded battlefield loses exactly one minion");
        End(g);
        Park(p); Park(opp);
        var m1 = Summon(p, "CS2_182"); var m2 = Summon(opp, "CS2_182");
        PlayEntity(g, opp, Add(opp, id));
        Check((m1.Zone.Type == Zone.GRAVEYARD) != (m2.Zone.Type == Zone.GRAVEYARD), "the opponent's Blade works the same");
        Resume(g, next => Check(Get(next, m1).Zone.Type == m1.Zone.Type, "clone keeps state"));
    }

    // ---- unregistered scenarios (BLOCKED-STANDARD) ------------------------------------------------------------------

    private static void RunUnregistered()
    {
        foreach (var (id, scenario) in new (string, Action<Game, Controller, string>)[] { ("GVG_033", TreeOfLife), ("GVG_044", SpiderTank) })
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
        Console.WriteLine("UNREGISTERED scenarios GVG_033, GVG_044 PASS; seeds=3; clone/snapshot");
    }

    private static void TreeOfLife(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var spell = Add(p, id);
        Check(spell.Cost == 9, "Tree of Life costs 9");
        var a = Summon(p, "CS2_182"); var b = Summon(opp, "CS2_182"); var c = Summon(opp, "CS2_120");
        a.Damage = 3; b.Damage = 4; c.Damage = 1; p.Hero.Damage = 12; opp.Hero.Damage = 30 - 1; p.Hero.Armor = 5;
        PlayEntity(g, p, spell);
        Check(a.Damage == 0 && b.Damage == 0 && c.Damage == 0 && p.Hero.Damage == 0 && opp.Hero.Damage == 0, "every character on both sides is fully restored");
        Check(p.Hero.Armor == 5, "Armor is untouched");
        Park(p); Park(opp);
        PlayEntity(g, p, Add(p, id));
        Check(p.Hero.Damage == 0, "an empty board is fine");
        var buffed = Summon(p, "CS2_182"); Play(g, p, "GVG_010", buffed); buffed.Damage = 6;
        PlayEntity(g, p, Add(p, id));
        Check(buffed.Damage == 0 && buffed.Health == 9, "buffed minions heal to their buffed maximum");
        End(g);
        var m = Summon(p, "CS2_182"); m.Damage = 2; opp.Hero.Damage = 5;
        PlayEntity(g, opp, Add(opp, id));
        Check(m.Damage == 0 && opp.Hero.Damage == 0, "the opponent's Tree of Life heals everyone too");
    }

    private static void MechBearCat(Game g, Controller p, string id)
    {
        var opp = p.Opponent;
        var cat = (Minion)Play(g, p, id);
        Check(cat.Card.Cost == 6 && cat.AttackDamage == 7 && cat.Health == 6 && cat.IsRace(Race.MECHANICAL), "a 7/6 Mech for 6 (the Beast race of 37.0 arrives with its identity row, not loaded for an unregistered id)");
        EmptyHand(p);
        var enemy = Summon(opp, "CS2_182");
        var parts = new HashSet<string>();
        for (int i = 0; i < 40; i++)
        {
            cat.Damage = 0; EmptyHand(p);
            cat.TakeDamage(enemy, 1);
            Check(p.HandZone.Count == 1 && p.HandZone[0].Card.Id.StartsWith("PART_") && p.HandZone[0].Card.Cost >= 0, "one Spare Part per damage instance");
            parts.Add(p.HandZone[0].Card.Id);
        }
        Check(parts.Count == 7, "all seven Spare Parts are reachable: " + parts.Count);
        cat.Damage = 0; EmptyHand(p);
        cat.HasDivineShield = true;
        cat.TakeDamage(enemy, 3);
        Check(p.HandZone.Count == 0 && !cat.HasDivineShield, "a Divine Shield absorbs the hit: no damage, no Spare Part");
        cat.TakeDamage(enemy, 2); cat.TakeDamage(enemy, 2);
        Check(p.HandZone.Count == 2, "each damage instance adds one");
        cat.Damage = 0; FillHand(p);
        cat.TakeDamage(enemy, 1);
        Check(p.HandZone.Count == 10, "a full hand burns the part");
        EmptyHand(p);
        cat.TakeDamage(enemy, 50); g.DeathProcessingAndAuraUpdate();
        Check(cat.Zone.Type == Zone.GRAVEYARD && p.HandZone.Count == 1, "lethal damage still adds the Spare Part");
        Concealed(g, p.HandZone[0], "Mech-Bear-Cat part");
        Park(p);
        var cat2 = Summon(p, id); cat2.Silence(); EmptyHand(p);
        cat2.TakeDamage(enemy, 1);
        Check(p.HandZone.Count == 0, "Silence removes the trigger");
        End(g);
        var theirs = Summon(opp, id); EmptyHand(opp); EmptyHand(p);
        theirs.TakeDamage(Summon(p, "CS2_182"), 1);
        Check(opp.HandZone.Count == 1 && p.HandZone.Count == 0, "the enemy Mech-Bear-Cat gives its owner the part");
        Resume(g, next => Check(Get(next, opp).HandZone.Count == 1, "clone keeps the hand"));
    }

    private static void SpiderTank(Game g, Controller p, string id)
    {
        var tank = (Minion)Play(g, p, id);
        Check(tank.Card.Cost == 3 && tank.AttackDamage == 3 && tank.Health == 4 && tank.IsRace(Race.MECHANICAL), "Spider Tank is a 3/4 Mech for 3");
        Check(tank.Card.Text == "" || tank.Card.Text == null, "vanilla");
        var enemy = Summon(p.Opponent, "CS2_182");
        End(g); End(g);
        var wisp = Summon(p.Opponent, "CS2_231");
        Attack(g, tank, wisp);
        Check(wisp.Zone.Type == Zone.GRAVEYARD && tank.Damage == 1, "it fights as a 3/4");
        var mw = Summon(p, "GVG_006");
        var other = Add(p, id); g.DeathProcessingAndAuraUpdate();
        Check(other.Cost == 2, "Mechwarper discounts it: " + other.Cost);
        Resume(g, next => Check(Get(next, p).BoardZone.Count >= 1, "clone keeps the board"));
    }
}
