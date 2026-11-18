using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Config;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

/// <summary>Wild wave Y0b, lane m3f: Tournament cards AT_111 to AT_133, proven against the 37.0 export through WildSetsProof.Case.</summary>
internal static class WildY0bM3FProof
{
    private static void Ok(bool condition, string message) => Core2026Proof.Check(condition, message);
    private static readonly List<string> HeldReport = new();

    [GroupProof("wild-y0b-m3f-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        HeldReport.Clear();
        WildSetsProof.Case("AT_111", RefreshmentVendor);
        WildSetsProof.Case("AT_112", MasterJouster);
        WildSetsProof.Case("AT_113", Recruiter);
        WildSetsProof.Case("AT_114", EvilHeckler);
        WildSetsProof.Case("AT_115", FencingCoach);
        WildSetsProof.Case("AT_116", WyrmrestAgent);
        WildSetsProof.Case("AT_117", MasterOfCeremonies);
        WildSetsProof.Case("AT_118", GrandCrusader);
        WildSetsProof.Held("AT_119", KvaldirRaider);
        WildSetsProof.Case("AT_120", FrostGiant);
        WildSetsProof.Case("AT_121", CrowdFavorite);
        WildSetsProof.Case("AT_122", GormokTheImpaler);
        // AT_123 Chillmaw: BLOCKED-STANDARD (data/progression/core-2026-unlocks.json and plugin/LocalCardCatalog.cs name CORE_AT_123
        // through the copy group AT_123). Proof kept, not registered.
        Held("AT_123", "data/progression/core-2026-unlocks.json, plugin/LocalCardCatalog.cs", Chillmaw);
        WildSetsProof.Case("AT_124", BolfRamshield);
        WildSetsProof.Case("AT_125", Icehowl);
        WildSetsProof.Case("AT_127", NexusChampionSaraad);
        WildSetsProof.Held("AT_128", TheSkeletonKnight);
        WildSetsProof.Held("AT_129", FjolaLightbane);
        WildSetsProof.Held("AT_130", SeaReaver);
        WildSetsProof.Held("AT_131", EydisDarkbane);
        WildSetsProof.Held("AT_132", "held: upgraded hero powers not checkable; needs a scenario that uses the hero power after a Justicar upgrade", JusticarTrueheart);
        WildSetsProof.Case("AT_133", GadgetzanJouster);
        foreach (string line in HeldReport) Console.WriteLine(line);
    }

    // ---- helpers ----

    private static Game NewGame(int seed, CardClass first, CardClass second)
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

    private static void Seeds(CardClass first, CardClass second, Action<Game, int> scenario)
    {
        for (int seed = 1; seed <= 3; seed++) scenario(NewGame(seed, first, second), seed);
    }

    private static void Held(string id, string source, Action<Game, Controller, string> scenario)
    {
        var failures = new List<string>();
        for (int seed = 1; seed <= 3; seed++)
        {
            try { var game = NewGame(seed, CardClass.ROGUE, CardClass.MAGE); scenario(game, game.Player1, id); }
            catch (Exception error) { failures.Add("seed " + seed + ": " + error.Message); }
        }
        HeldReport.Add("HELD BLOCKED-STANDARD " + id + " (" + source + "): behaviour proof " + (failures.Count == 0 ? "passes" : "FAILS " + string.Join(" | ", failures)));
    }

    private static void Resume(Game game, Action<Game> continuation)
    {
        foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) })
        {
            Ok(Canonical.State(game) == Canonical.State(next), "state before continuation");
            continuation(next);
        }
    }

    private static void SetDeck(Controller p, params string[] ids)
    {
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        foreach (string id in ids) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId(id)));
    }

    private static void ToTurn(Game g, Controller p) { while (g.CurrentPlayer != p) End(g); }
    private static bool Alive(Minion m) => m.Zone?.Type == Zone.PLAY;
    private static bool Dead(Minion m) => m.Zone?.Type == Zone.GRAVEYARD;
    private static void Refresh(Controller p) { p.BaseMana = 10; p.UsedMana = 0; p.TemporaryMana = 0; }
    private static void FillHand(Controller p, int count) { while (p.HandZone.Count < count) Add(p, "CS2_182"); }

    private static bool Revealed(Game g, params int[] ids)
        => g.PowerHistory.Full.OfType<PowerHistoryMetaData>().Any(m => m.Type == MetaDataType.JOUST && ids.All(i => m.Info.Contains(i)));

    private static void Steal(Game g, Controller thief, Minion minion)
    {
        new ControlTask(EntityType.TARGET).Process(g, thief, thief.Hero, minion);
        g.DeathProcessingAndAuraUpdate();
        Ok(minion.Controller == thief, "control changed");
    }

    private static bool CanPower(Controller p) => p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER);

    private static void UseHeroPower(Game g, Controller p, ICharacter target = null)
    {
        ToTurn(g, p);
        Refresh(p);
        if (!CanPower(p)) { End(g); End(g); Refresh(p); }
        Ok(CanPower(p), "hero power is available");
        Ok(g.Process(HeroPowerTask.Any(p, target)), "hero power accepted");
    }

    private static bool CanAttack(Controller p, Minion m, ICharacter target)
        => p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == m.Id && t.Target?.Id == target.Id);

    private static bool CanPlayCard(Controller p, IPlayable card, ICharacter target = null)
        => p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id && t.Target?.Id == target?.Id);

    // Two identical games that differ only in the identity of one hidden entity: the viewer's fair projection must not differ.
    private static void HiddenSame(Game game, Func<Game, IPlayable> hidden, string substitute, int viewer, string what)
    {
        Game Variant(bool swap)
        {
            var copy = game.CloneExact(history: true);
            var entity = hidden(copy);
            if (swap) Generic.ChangeEntityBlock(entity.Controller, entity, Cards.FromId(substitute), false);
            if (copy.CurrentPlayer.PlayerId != viewer) End(copy);
            return copy;
        }
        var a = Variant(false); var b = Variant(true);
        var fa = FairBotState.Create(a, viewer, 42, DeckStyle.Control); var fb = FairBotState.Create(b, viewer, 42, DeckStyle.Control);
        var field = typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic);
        var ga = (Game)field.GetValue(fa); var gb = (Game)field.GetValue(fb);
        ga.AuraUpdate(); gb.AuraUpdate();
        Ok(Canonical.State(ga) == Canonical.State(gb) && fa.Evaluate() == fb.Evaluate()
            && fa.Actions.Select(x => x.ToString()).SequenceEqual(fb.Actions.Select(x => x.ToString())),
            what + ": hidden identity does not reach the other seat's projected state");
    }

    // ---- cards ----

    // ---- control change ----

    private static void StolenBolf(string id)
    {
        for (int seed = 1; seed <= 3; seed++)
        {
            var g = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var me = g.Player1; var foe = me.Opponent;
            var bolf = Summon(me, id);
            Steal(g, foe, bolf);
            ToTurn(g, me);
            Play(g, me, "CS2_029", me.Hero);
            Ok(me.Hero.Damage == 6 && bolf.Damage == 0, "a stolen Bolf no longer protects its old owner");
            Play(g, me, "CS2_029", foe.Hero);
            Ok(foe.Hero.Damage == 0 && bolf.Damage == 6, "a stolen Bolf protects its new owner's hero");
        }
    }

    private static void StolenInspire(string id, Func<Minion, bool> buffed)
    {
        for (int seed = 1; seed <= 3; seed++)
        {
            var g = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var me = g.Player1; var foe = me.Opponent;
            var minion = Summon(me, id);
            Steal(g, foe, minion);
            UseHeroPower(g, me);
            Ok(!buffed(minion), "the old owner's Hero Power no longer Inspires a stolen minion");
            UseHeroPower(g, foe, me.Hero);
            Ok(buffed(minion), "the new owner's Hero Power Inspires it");
        }
    }

    private static void StolenCrowdFavorite(string id)
    {
        for (int seed = 1; seed <= 3; seed++)
        {
            var g = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var me = g.Player1; var foe = me.Opponent;
            var cf = Summon(me, id);
            Steal(g, foe, cf);
            Play(g, me, "EX1_015");
            Ok(cf.AttackDamage == 4, "the old owner's Battlecry no longer grows a stolen Crowd Favorite");
            ToTurn(g, foe);
            Play(g, foe, "EX1_015");
            Ok(cf.AttackDamage == 5, "the new owner's Battlecry grows it");
        }
    }

    private static void StolenSpellTarget(string id, string spell, CardClass heroClass, Func<Game, Minion, bool> reacted)
    {
        for (int seed = 1; seed <= 3; seed++)
        {
            var g = NewGame(seed, heroClass, heroClass);
            var me = g.Player1; var foe = me.Opponent;
            var minion = Summon(me, id);
            Steal(g, foe, minion);
            Summon(me, "CS2_182");
            Play(g, me, spell, minion);
            Ok(!reacted(g, minion), "a spell from the old owner no longer triggers a stolen minion");
            minion.Damage = 0; minion.HasDivineShield = false; me.Hero.Damage = 0;
            foe.Hero.Damage = 0; foreach (var m in me.BoardZone) m.Damage = 0;
            ToTurn(g, foe);
            Play(g, foe, spell, minion);
            Ok(reacted(g, minion), "a spell from the new owner triggers it");
        }
    }

    private static void RefreshmentVendor(Game g0, Controller p0, string id)
    {
        var v0 = (Minion)Play(g0, p0, id);
        Ok(v0.AttackDamage == 3 && v0.Health == 5 && p0.UsedMana == 4, "Refreshment Vendor is a 4-mana 3/5");
        Seeds(CardClass.PRIEST, CardClass.WARRIOR, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            me.Hero.Damage = 10; foe.Hero.Damage = 3;
            Play(g, me, id);
            Ok(me.Hero.Damage == 6 && foe.Hero.Damage == 0, "restores 4 to each hero, capped at full health: mine " + me.Hero.Damage + " theirs " + foe.Hero.Damage);
            // Undamaged heroes stay at full health; minions are not healed.
            var hurt = Summon(foe, "CS2_182"); hurt.Damage = 3;
            Play(g, me, id);
            Ok(me.Hero.Damage == 2 && foe.Hero.Damage == 0 && hurt.Damage == 3, "second Vendor: 4 more to my hero, minions untouched");
            // Opponent's side restores both heroes too.
            ToTurn(g, foe);
            me.Hero.Damage = 5; foe.Hero.Damage = 9;
            var theirs = (Minion)Play(g, foe, id);
            Ok(me.Hero.Damage == 1 && foe.Hero.Damage == 5, "the opponent's Vendor heals both heroes");
            // Silence does not matter once played: the effect is the Battlecry.
            Ok(Alive(theirs), "Vendor stays");
            // Played with a full board it cannot be cast at all, but a clone keeps the result.
            Resume(g, next => Ok(next.Player1.Hero.Damage == 1 && next.Player2.Hero.Damage == 5, "clone keeps the healing result"));
            // A non-playing summon does not trigger the Battlecry.
            me.Hero.Damage = 8;
            Summon(me, id);
            Ok(me.Hero.Damage == 8, "a summoned Vendor has no Battlecry");
        });
    }

    // Shared Joust cases for the two Jousters: `won` / `baseline` say whether the minion shows its bonus.
    private static void JoustCases(string id, Func<Minion, bool> won, Func<Minion, bool> baseline, bool hiddenCheck)
    {
        string[] none = Array.Empty<string>();
        Minion PlayJouster(int seed, string[] mine, string[] theirs, out Game game)
        {
            game = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            SetDeck(game.Player1, mine); SetDeck(game.Player2, theirs);
            return (Minion)Play(game, game.Player1, id);
        }
        for (int seed = 1; seed <= 3; seed++)
        {
            var m = PlayJouster(seed, new[] { "CS2_200" }, new[] { "CS2_231" }, out var g1);
            Ok(won(m), "6 cost against 0 cost wins the Joust");
            Ok(Revealed(g1, g1.Player1.DeckZone.First().Id, g1.Player2.DeckZone.First().Id), "both Joust minions are shown in the Joust block");
            Resume(g1, next => Ok(won(next.Player1.BoardZone.First(x => x.Card.Id == id)), "a win survives a clone/snapshot"));
            m.Silence(); g1.AuraUpdate();
            Ok(baseline(m), "Silence removes the bonus");
            Ok(!won(PlayJouster(seed, new[] { "CS2_231" }, new[] { "CS2_200" }, out _)), "a cheaper minion loses");
            Ok(!won(PlayJouster(seed, new[] { "CS2_182" }, new[] { "CS2_182" }, out _)), "a tie loses");
            Ok(won(PlayJouster(seed, new[] { "CS2_231" }, new[] { "CS2_029", "CS2_029" }, out _)), "an opponent deck with no minion loses to any minion of mine");
            Ok(!won(PlayJouster(seed, new[] { "CS2_029", "CS2_029" }, new[] { "CS2_231" }, out _)), "no minion in my deck cannot win");
            Ok(!won(PlayJouster(seed, new[] { "CS2_029" }, new[] { "CS2_029" }, out _)), "no minion in either deck: no win");
            var empty = PlayJouster(seed, none, none, out var ge);
            Ok(!won(empty) && ge.Player1.BoardZone.Count == 1, "empty decks: nothing happens and nothing stalls");
            // Reveal does not draw or move anything.
            PlayJouster(seed, new[] { "CS2_200", "CS2_029" }, new[] { "CS2_231", "CS2_029" }, out var gm);
            Ok(gm.Player1.DeckZone.Count == 2 && gm.Player2.DeckZone.Count == 2, "revealing moves no card");
            // The opponent's Jouster compares in the other direction.
            var gf = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            ToTurn(gf, gf.Player2);
            SetDeck(gf.Player1, "CS2_231"); SetDeck(gf.Player2, "CS2_200");
            var theirs = (Minion)Play(gf, gf.Player2, id);
            Ok(won(theirs), "the opponent's Jouster compares their deck against mine");
        }
        // The pick is random among the minions in each deck: both outcomes happen and are reproducible.
        int wins = 0, losses = 0;
        for (int seed = 1; seed <= 24; seed++)
        {
            var m = PlayJouster(seed, new[] { "CS2_200", "CS2_231", "CS2_168" }, new[] { "CS2_182" }, out var g);
            if (won(m)) wins++; else losses++;
            var again = PlayJouster(seed, new[] { "CS2_200", "CS2_231", "CS2_168" }, new[] { "CS2_182" }, out _);
            Ok(won(again) == won(m), "same seed, same Joust");
        }
        Ok(wins > 0 && losses > 0, "a deck of mixed minion costs wins some Jousts and loses others: " + wins + "/" + losses);
        if (hiddenCheck)
        {
            // Only the Joust minions are shown; an unrevealed card stays hidden from the other seat.
            PlayJouster(1, new[] { "CS2_029", "CS2_200" }, new[] { "CS2_231", "CS2_029" }, out var gh);
            HiddenSame(gh, game => game.Player1.DeckZone.First(c => c.Card.Id == "CS2_029"), "CS2_024", 2, "unrevealed spell in my deck");
            HiddenSame(gh, game => game.Player2.DeckZone.First(c => c.Card.Id == "CS2_029"), "CS2_024", 1, "unrevealed spell in their deck");
        }
    }

    private static void MasterJouster(Game g0, Controller p0, string id)
    {
        var j0 = (Minion)Play(g0, p0, id);
        Ok(j0.AttackDamage == 5 && j0.Health == 6 && p0.UsedMana == 6, "Master Jouster is a 6-mana 5/6");
        JoustCases(id, m => m.HasTaunt && m.HasDivineShield, m => !m.HasTaunt && !m.HasDivineShield, true);
    }

    private static void GadgetzanJouster(Game g0, Controller p0, string id)
    {
        var j0 = (Minion)Play(g0, p0, id);
        Ok(j0.AttackDamage == 1 && j0.Health == 2 && p0.UsedMana == 1, "Gadgetzan Jouster is a 1-mana 1/2");
        JoustCases(id, m => m.AttackDamage == 2 && m.Health == 3, m => m.AttackDamage == 1 && m.Health == 2, false);
    }

    private static void Recruiter(Game g0, Controller p0, string id)
    {
        var r0 = (Minion)Play(g0, p0, id);
        Ok(r0.AttackDamage == 5 && r0.Health == 4 && p0.UsedMana == 5, "Recruiter is a 5-mana 5/4");
        for (int seed = 1; seed <= 3; seed++)
        {
            var gs = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var mine = Summon(gs.Player1, id);
            Steal(gs, gs.Player2, mine);
            UseHeroPower(gs, gs.Player1);
            Ok(!gs.Player1.HandZone.Any(c => c.Card.Id == "CS2_152") && !gs.Player2.HandZone.Any(c => c.Card.Id == "CS2_152"), "the old owner's Hero Power no longer Inspires a stolen Recruiter");
            UseHeroPower(gs, gs.Player2, gs.Player1.Hero);
            Ok(gs.Player2.HandZone.Count(c => c.Card.Id == "CS2_152") == 1 && !gs.Player1.HandZone.Any(c => c.Card.Id == "CS2_152"), "the new owner's Hero Power gives the Squire to the new owner");
        }
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var rec = (Minion)Play(g, me, id);
            int hand = me.HandZone.Count;
            Ok(hand == 0, "no Squire before Inspire");
            UseHeroPower(g, me);
            var squire = me.HandZone.Single();
            Ok(squire.Card.Id == "CS2_152" && ((Minion)squire).AttackDamage == 2 && ((Minion)squire).Health == 2, "Inspire adds a 2/2 Squire to hand");
            Resume(g, next => Ok(next.Player1.HandZone.Single().Card.Id == "CS2_152", "clone keeps the Squire"));
            // Each Hero Power use adds one; a second Recruiter adds another.
            Play(g, me, id);
            ToTurn(g, foe); ToTurn(g, me);
            UseHeroPower(g, me);
            Ok(me.HandZone.Count(c => c.Card.Id == "CS2_152") == 3, "two Recruiters on the next use add two more Squires");
            // The opponent's Hero Power does not trigger my Recruiter.
            int before = me.HandZone.Count;
            UseHeroPower(g, foe, me.Hero);
            Ok(me.HandZone.Count == before, "their Hero Power does not Inspire my minion");
            // Silenced Recruiter does nothing.
            var g2 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var s = (Minion)Play(g2, g2.Player1, id); s.Silence();
            UseHeroPower(g2, g2.Player1);
            Ok(g2.Player1.HandZone.Count == 0, "Silenced Recruiter does not Inspire");
            // Dead Recruiter does nothing.
            var g3 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var d = (Minion)Play(g3, g3.Player1, id); Kill(g3, d);
            UseHeroPower(g3, g3.Player1);
            Ok(g3.Player1.HandZone.Count == 0, "a dead Recruiter does not Inspire");
            // Full hand: the Squire is burned.
            var g4 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            Play(g4, g4.Player1, id);
            FillHand(g4.Player1, 10);
            UseHeroPower(g4, g4.Player1);
            Ok(g4.Player1.HandZone.Count == 10 && g4.Player1.HandZone.All(c => c.Card.Id != "CS2_152"), "full hand: no Squire");
            // The opponent's Recruiter fills their own hand.
            var g5 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            ToTurn(g5, g5.Player2);
            Play(g5, g5.Player2, id);
            UseHeroPower(g5, g5.Player2, g5.Player1.Hero);
            Ok(g5.Player2.HandZone.Count(c => c.Card.Id == "CS2_152") == 1 && g5.Player1.HandZone.Count == 0, "the opponent's Recruiter adds to their own hand");
        });
    }

    private static void EvilHeckler(Game g0, Controller p0, string id)
    {
        var h0 = (Minion)Play(g0, p0, id);
        Ok(h0.AttackDamage == 5 && h0.Health == 4 && h0.HasTaunt && p0.UsedMana == 4, "Evil Heckler is a 4-mana 5/4 Taunt");
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var heckler = (Minion)Play(g, me, id);
            var other = Summon(me, "CS2_182");
            var attacker = Summon(foe, "CS2_182");
            End(g);
            attacker.IsExhausted = false;
            Ok(CanAttack(foe, attacker, heckler) && !CanAttack(foe, attacker, other) && !CanAttack(foe, attacker, me.Hero), "only the Taunt can be attacked");
            Resume(g, next =>
            {
                var a = next.Player2.BoardZone.First();
                var t = next.Player1.BoardZone.First(m => m.Card.Id == id);
                Ok(CanAttack(next.Player2, a, t) && !CanAttack(next.Player2, a, next.Player1.Hero), "clone keeps the Taunt");
            });
            heckler.Silence();
            Ok(CanAttack(foe, attacker, other) && CanAttack(foe, attacker, me.Hero), "Silenced Heckler no longer protects");
            // The opponent's Heckler protects their side.
            var theirs = (Minion)Play(g, foe, id);
            End(g);
            var mine = Summon(me, "CS2_182"); mine.IsExhausted = false;
            Ok(CanAttack(me, mine, theirs) && !CanAttack(me, mine, foe.Hero), "their Heckler is the only legal attack target");
        });
    }

    private static void FencingCoach(Game g0, Controller p0, string id)
    {
        var c0 = (Minion)Play(g0, p0, id);
        Ok(c0.AttackDamage == 2 && c0.Health == 2 && p0.UsedMana == 3, "Fencing Coach is a 3-mana 2/2");
        Ok(CanPlayCard(p0, Add(p0, id)), "Fencing Coach has no target requirement and can be played on an empty board");
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            Ok(me.Hero.HeroPower.Cost == 2, "base Hero Power cost is 2");
            var coach = (Minion)Play(g, me, id);
            g.AuraUpdate();
            Ok(me.Hero.HeroPower.Cost == 0, "the next Hero Power costs 2 less");
            Ok(foe.Hero.HeroPower.Cost == 2, "the opponent's Hero Power is unaffected");
            Resume(g, next => Ok(next.Player1.Hero.HeroPower.Cost == 0, "the discount survives a clone/snapshot"));
            Refresh(me);
            Ok(g.Process(HeroPowerTask.Any(me)) && me.UsedMana == 0, "the discounted use costs no mana");
            g.AuraUpdate();
            Ok(me.Hero.HeroPower.Cost == 2, "the discount is spent by one use");
            // Discount waits across turns while unused, and survives the Coach dying or being Silenced.
            var g2 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var c2 = (Minion)Play(g2, g2.Player1, id);
            c2.Silence(); Kill(g2, c2);
            End(g2); End(g2);
            Ok(g2.Player1.HeroPowerActivationsThisTurn == 0 && g2.Player1.Hero.HeroPower.Cost == 0, "unused discount stays after the Coach is gone and a turn passes");
            // A used Hero Power keeps its discount for the next turn.
            var g3 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            Ok(g3.Process(HeroPowerTask.Any(g3.Player1)), "use first");
            Play(g3, g3.Player1, id);
            g3.AuraUpdate();
            Ok(g3.Player1.Hero.HeroPower.Cost == 0, "the discount shows while exhausted");
            End(g3); End(g3);
            Refresh(g3.Player1);
            Ok(g3.Process(HeroPowerTask.Any(g3.Player1)) && g3.Player1.UsedMana == 0, "and applies on the next turn's use");
            // Two Coaches: the next use costs 4 less, floored at 0, then both are spent.
            var g4 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            Play(g4, g4.Player1, id); Play(g4, g4.Player1, id);
            g4.AuraUpdate();
            Ok(g4.Player1.Hero.HeroPower.Cost == 0, "two discounts floor at 0");
            Refresh(g4.Player1);
            Ok(g4.Process(HeroPowerTask.Any(g4.Player1)), "use");
            g4.AuraUpdate();
            Ok(g4.Player1.Hero.HeroPower.Cost == 2, "both discounts are spent by the single use");
            // Opponent's Coach affects the opponent.
            var g5 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            ToTurn(g5, g5.Player2);
            Play(g5, g5.Player2, id);
            g5.AuraUpdate();
            Ok(g5.Player2.Hero.HeroPower.Cost == 0 && g5.Player1.Hero.HeroPower.Cost == 2, "the opponent's Coach discounts their own Hero Power");
            // The discount waits through my turn and is still there on theirs.
            End(g5); End(g5);
            Ok(g5.Player2.Hero.HeroPower.Cost == 0, "their discount waits until they use the power");
        });
    }

    private static void WyrmrestAgent(Game g0, Controller p0, string id)
    {
        var a0 = (Minion)Play(g0, p0, id);
        Ok(a0.AttackDamage == 1 && a0.Health == 4 && p0.UsedMana == 2 && !a0.HasTaunt, "Wyrmrest Agent is a 2-mana 1/4");
        Seeds(CardClass.PRIEST, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var plain = (Minion)Play(g, me, id);
            Ok(plain.AttackDamage == 1 && !plain.HasTaunt, "no Dragon in hand: no bonus");
            var dragon = Add(me, "NEW1_030");
            var agent = (Minion)Play(g, me, id);
            Ok(agent.AttackDamage == 2 && agent.Health == 4 && agent.HasTaunt, "a Dragon in hand gives +1 Attack and Taunt");
            Resume(g, next => Ok(next.Player1.BoardZone.Any(m => m.Card.Id == id && m.AttackDamage == 2 && m.HasTaunt), "clone keeps the bonus"));
            agent.Silence();
            Ok(agent.AttackDamage == 1 && !agent.HasTaunt, "Silence removes it");
            // A Dragon on the battlefield, in the deck, or in their hand does not count.
            var g2 = NewGame(seed, CardClass.PRIEST, CardClass.MAGE);
            Summon(g2.Player1, "EX1_562");
            SetDeck(g2.Player1, "EX1_562"); Add(g2.Player2, "EX1_562");
            var a2 = (Minion)Play(g2, g2.Player1, id);
            Ok(a2.AttackDamage == 1 && !a2.HasTaunt, "only a Dragon held in hand counts");
            // A Dragon of another cost (Chillmaw) counts as a Dragon.
            var g3 = NewGame(seed, CardClass.PRIEST, CardClass.MAGE);
            Add(g3.Player1, "AT_123");
            var a3 = (Minion)Play(g3, g3.Player1, id);
            Ok(a3.AttackDamage == 2 && a3.HasTaunt, "any Dragon in hand counts");
            // A non-Dragon beast in hand does not count.
            var g4 = NewGame(seed, CardClass.PRIEST, CardClass.MAGE);
            Add(g4.Player1, "CS2_203");
            Ok(((Minion)Play(g4, g4.Player1, id)).AttackDamage == 1, "other minions in hand do not count");
            // Opponent's Agent looks at their hand.
            var g5 = NewGame(seed, CardClass.PRIEST, CardClass.MAGE);
            Add(g5.Player1, "EX1_562"); ToTurn(g5, g5.Player2);
            Ok(((Minion)Play(g5, g5.Player2, id)).AttackDamage == 1, "their Agent does not see my Dragon");
            Add(g5.Player2, "EX1_562");
            Ok(((Minion)Play(g5, g5.Player2, id)).AttackDamage == 2, "their Agent sees their own Dragon");
        });
    }

    private static void MasterOfCeremonies(Game g0, Controller p0, string id)
    {
        var m0 = (Minion)Play(g0, p0, id);
        Ok(m0.AttackDamage == 4 && m0.Health == 2 && p0.UsedMana == 3, "Master of Ceremonies is a 3-mana 4/2");
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var base1 = (Minion)Play(g, me, id);
            Ok(base1.AttackDamage == 4 && base1.Health == 2, "no Spell Damage minion: no bonus");
            Summon(me, "CS2_231");
            var plain = (Minion)Play(g, me, id);
            Ok(plain.AttackDamage == 4, "a minion without Spell Damage does not count");
            var geo = Summon(me, "CS2_142");
            var boosted = (Minion)Play(g, me, id);
            Ok(boosted.AttackDamage == 6 && boosted.Health == 4, "a Spell Damage minion gives +2/+2");
            Resume(g, next => Ok(next.Player1.BoardZone.Any(m => m.Card.Id == id && m.AttackDamage == 6), "clone keeps the bonus"));
            boosted.Silence();
            Ok(boosted.AttackDamage == 4 && boosted.Health == 2, "Silence removes the bonus");
            // A Silenced Spell Damage minion does not count; neither does the enemy's.
            var g2 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var silent = Summon(g2.Player1, "CS2_142"); silent.Silence(); g2.AuraUpdate();
            Summon(g2.Player2, "CS2_142");
            Ok(((Minion)Play(g2, g2.Player1, id)).AttackDamage == 4, "Silenced or enemy Spell Damage does not count");
            // Mine: counts when the Spell Damage minion is mine, on their side too.
            var g3 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            ToTurn(g3, g3.Player2);
            Summon(g3.Player2, "CS2_142");
            Ok(((Minion)Play(g3, g3.Player2, id)).AttackDamage == 6, "the opponent's Master counts their own Spell Damage");
        });
    }

    private static void GrandCrusader(Game g0, Controller p0, string id)
    {
        var c0 = (Minion)Play(g0, p0, id);
        Ok(c0.AttackDamage == 5 && c0.Health == 5 && p0.UsedMana == 6, "Grand Crusader is a 6-mana 5/5");
        var seen = new HashSet<string>();
        var types = new HashSet<CardType>();
        for (int seed = 1; seed <= 30; seed++)
        {
            var g = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            var me = g.Player1;
            Play(g, me, id);
            Ok(me.HandZone.Count == 1, "one card is added");
            var card = me.HandZone.Single();
            // The random pool is the frozen legacy Wild generation list (it also holds cards the engine has no script for).
            Ok(card.Card.Class == CardClass.PALADIN && card.Card.Collectible, "a collectible Paladin card: " + card.Card.Id);
            seen.Add(card.Card.Id); types.Add(card.Card.Type);
            if (seed <= 3)
            {
                var again = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
                Play(again, again.Player1, id);
                Ok(again.Player1.HandZone.Single().Card.Id == card.Card.Id, "same seed, same card");
                Resume(g, next => Ok(next.Player1.HandZone.Single().Card.Id == card.Card.Id, "clone keeps the card"));
            }
        }
        Ok(seen.Count >= 8 && types.Count >= 2, "30 seeds give many distinct Paladin cards of several types: " + seen.Count + "/" + types.Count);
        // A Silenced or summoned Crusader has no Battlecry.
        var gs = NewGame(1, CardClass.WARRIOR, CardClass.MAGE);
        Summon(gs.Player1, id);
        Ok(gs.Player1.HandZone.Count == 0, "a summoned Crusader adds nothing");
        // The opponent's Crusader adds to their own hand.
        var go = NewGame(1, CardClass.WARRIOR, CardClass.MAGE);
        ToTurn(go, go.Player2);
        Play(go, go.Player2, id);
        Ok(go.Player2.HandZone.Count(c => c.Card.Class == CardClass.PALADIN) == 1 && go.Player1.HandZone.Count == 0, "the opponent's Crusader fills their hand");
        // Hidden information: the added card's identity does not reach the other seat, for either seat.
        var gh2 = NewGame(2, CardClass.WARRIOR, CardClass.MAGE);
        ToTurn(gh2, gh2.Player2);
        Play(gh2, gh2.Player2, id);
        string added2 = gh2.Player2.HandZone.First(c => c.Card.Class == CardClass.PALADIN).Card.Id;
        HiddenSame(gh2, game => game.Player2.HandZone.First(c => c.Card.Class == CardClass.PALADIN), added2 == "CS2_092" ? "CS2_101" : "CS2_092", 1, "the opponent's random Paladin card");
        var gh = NewGame(2, CardClass.WARRIOR, CardClass.MAGE);
        Play(gh, gh.Player1, id);
        string added = gh.Player1.HandZone.Single().Card.Id;
        HiddenSame(gh, game => game.Player1.HandZone.Single(), added == "CS2_092" ? "CS2_101" : "CS2_092", 2, "random Paladin card in hand");
    }

    private static void KvaldirRaider(Game g0, Controller p0, string id)
    {
        var k0 = (Minion)Play(g0, p0, id);
        Ok(k0.AttackDamage == 4 && k0.Health == 4 && p0.UsedMana == 5, "Kvaldir Raider is a 5-mana 4/4");
        StolenInspire(id, m => m.AttackDamage == 6 && m.Health == 6);
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var k = (Minion)Play(g, me, id);
            Ok(k.AttackDamage == 4 && k.Health == 4, "no bonus before Inspire");
            UseHeroPower(g, me);
            Ok(k.AttackDamage == 6 && k.Health == 6, "Inspire gives +2/+2");
            Resume(g, next => Ok(next.Player1.BoardZone.First(m => m.Card.Id == id).AttackDamage == 6, "clone keeps the buff"));
            UseHeroPower(g, me);
            Ok(k.AttackDamage == 8 && k.Health == 8, "it stacks on each use");
            UseHeroPower(g, foe, me.Hero);
            Ok(k.AttackDamage == 8, "their Hero Power does not Inspire it");
            k.Silence();
            Ok(k.AttackDamage == 4 && k.Health == 4, "Silence removes the stats and the trigger");
            UseHeroPower(g, me);
            Ok(k.AttackDamage == 4, "a Silenced Raider no longer Inspires");
            // Damaged Raider: the health buff raises current health too.
            var g2 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var d = (Minion)Play(g2, g2.Player1, id); d.Damage = 3;
            UseHeroPower(g2, g2.Player1);
            Ok(d.Health == 3 && d.AttackDamage == 6, "health buff keeps the damage: " + d.Health);
            // The opponent's Raider Inspires on their power only.
            var g3 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            ToTurn(g3, g3.Player2);
            var t = (Minion)Play(g3, g3.Player2, id);
            UseHeroPower(g3, g3.Player2, g3.Player1.Hero);
            Ok(t.AttackDamage == 6, "their Raider Inspires from their power");
        });
    }

    private static void FrostGiant(Game g0, Controller p0, string id)
    {
        var f0 = (Minion)Play(g0, p0, id);
        Ok(f0.AttackDamage == 8 && f0.Health == 8 && p0.UsedMana == 10, "Frost Giant is a 10-mana 8/8");
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var giant = Add(me, id); g.AuraUpdate();
            Ok(giant.Cost == 10, "no Hero Power uses yet: full cost");
            UseHeroPower(g, me);
            Ok(giant.Cost == 9, "one use: 9");
            ToTurn(g, foe);
            UseHeroPower(g, foe, me.Hero);
            g.AuraUpdate();
            Ok(giant.Cost == 9, "the opponent's uses do not count");
            UseHeroPower(g, me);
            UseHeroPower(g, me.Opponent.Opponent);
            Ok(giant.Cost == 7, "three uses: 7");
            Resume(g, next => Ok(next.Player1.HandZone.First(c => c.Card.Id == id).Cost == 7, "clone keeps the count"));
            // Plays at the reduced price and is a 8/8.
            Refresh(me);
            PlayEntity(g, me, me.HandZone.First(c => c.Card.Id == id));
            Ok(me.BoardZone.Any(m => m.Card.Id == id && m.AttackDamage == 8) && me.UsedMana == 7, "it is played at the reduced price");
            // Floor at 0 after ten or more uses.
            var g2 = NewGame(seed, CardClass.MAGE, CardClass.PALADIN);
            var late = Add(g2.Player1, id);
            for (int i = 0; i < 11; i++) { UseHeroPower(g2, g2.Player1, g2.Player2.Hero); g2.AuraUpdate(); }
            Ok(late.Cost == 0, "ten or more uses: cost is 0, never negative: " + late.Cost);
            // Opponent's Frost Giant counts their own uses.
            var g3 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var theirs = Add(g3.Player2, id);
            UseHeroPower(g3, g3.Player1);
            UseHeroPower(g3, g3.Player2, g3.Player1.Hero);
            g3.AuraUpdate();
            Ok(theirs.Cost == 9, "their Frost Giant counts their own uses only");
        });
    }

    private static void CrowdFavorite(Game g0, Controller p0, string id)
    {
        var c0 = (Minion)Play(g0, p0, id);
        Ok(c0.AttackDamage == 4 && c0.Health == 4 && p0.UsedMana == 4, "Crowd Favorite is a 4-mana 4/4");
        StolenCrowdFavorite(id);
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var cf = (Minion)Play(g, me, id);
            Kill(g, (Minion)Play(g, me, "CS2_182"));
            Ok(cf.AttackDamage == 4, "a card without Battlecry does not trigger it");
            Play(g, me, "EX1_015");
            Ok(cf.AttackDamage == 5 && cf.Health == 5, "a Battlecry minion gives +1/+1");
            Play(g, me, "CS2_203", Summon(foe, "CS2_182"));
            Ok(cf.AttackDamage == 6 && cf.Health == 6, "a targeted Battlecry minion triggers it as well");
            Resume(g, next => Ok(next.Player1.BoardZone.First(m => m.Card.Id == id).AttackDamage == 6, "clone keeps the buff"));
            Summon(me, "EX1_015");
            Ok(cf.AttackDamage == 6, "a summoned Battlecry minion was not played");
            Play(g, me, "CS2_029", foe.Hero);
            Ok(cf.AttackDamage == 6, "a spell does not trigger it");
            // Two Crowd Favorites both grow; the opponent's Battlecry does not trigger mine.
            var second = (Minion)Play(g, me, id);
            Play(g, me, "EX1_015");
            Ok(cf.AttackDamage == 7 && second.AttackDamage == 5, "every Crowd Favorite on the board grows");
            ToTurn(g, foe);
            Play(g, foe, "EX1_015");
            Ok(cf.AttackDamage == 7 && second.AttackDamage == 5, "the opponent's Battlecry does not trigger mine: " + cf.AttackDamage + "/" + second.AttackDamage);
            // Silenced Crowd Favorite stops and loses the buff.
            cf.Silence();
            Ok(cf.AttackDamage == 4 && cf.Health == 4, "Silence removes the buff");
            ToTurn(g, me);
            Play(g, me, "EX1_015");
            Ok(cf.AttackDamage == 4, "and the trigger");
            // The opponent's own Crowd Favorite.
            var g2 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            ToTurn(g2, g2.Player2);
            var theirs = (Minion)Play(g2, g2.Player2, id);
            Play(g2, g2.Player2, "EX1_015");
            Ok(theirs.AttackDamage == 5, "their Crowd Favorite grows from their Battlecries");
        });
    }

    private static void GormokTheImpaler(Game g0, Controller p0, string id)
    {
        var g00 = (Minion)Play(g0, p0, id);
        Ok(g00.AttackDamage == 4 && g00.Health == 4 && p0.UsedMana == 4, "Gormok is a 4-mana 4/4");
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var enemy = Summon(foe, "CS2_182");
            for (int i = 0; i < 3; i++) Summon(me, "CS2_231");
            var gormok = Add(me, id);
            Ok(CanPlayCard(me, gormok) && !CanPlayCard(me, gormok, enemy) && !CanPlayCard(me, gormok, foe.Hero), "three other minions: no target");
            Ok(!g.Process(PlayCardTask.Any(me, gormok, enemy)), "a target is refused with three other minions");
            Ok(g.Process(PlayCardTask.Any(me, gormok)) && enemy.Damage == 0 && foe.Hero.Damage == 0, "three other minions: no damage");
            var g2 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var m2 = g2.Player1; var f2 = m2.Opponent;
            var yeti = Summon(f2, "CS2_182"); var ally = Summon(m2, "CS2_231");
            for (int i = 0; i < 3; i++) Summon(m2, "CS2_231");
            var gm2 = Add(m2, id);
            Ok(!CanPlayCard(m2, gm2) && CanPlayCard(m2, gm2, yeti) && CanPlayCard(m2, gm2, f2.Hero) && CanPlayCard(m2, gm2, ally) && CanPlayCard(m2, gm2, m2.Hero),
                "four other minions: Battlecry needs a target and may pick any character");
            m2.BaseMana = 10; m2.UsedMana = 0;
            Ok(g2.Process(PlayCardTask.Any(m2, gm2, yeti)), "play on an enemy minion");
            Ok(yeti.Damage == 4 && Alive(yeti), "deals 4 damage");
            Resume(g2, next => Ok(next.Player2.BoardZone.First().Damage == 4, "clone keeps the damage"));
            // Face, and the Battlecry also kills.
            var g3 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var m3 = g3.Player1; var f3 = m3.Opponent;
            var small = Summon(f3, "CS2_168");
            for (int i = 0; i < 4; i++) Summon(m3, "CS2_231");
            Play(g3, m3, id, small);
            Ok(Dead(small), "kills a small minion");
            var gm3 = Add(m3, id);
            Summon(m3, "CS2_231"); g3.AuraUpdate();
            PlayEntity(g3, m3, gm3, f3.Hero);
            Ok(f3.Hero.Damage == 4, "hits the enemy hero for 4");
            // The opponent's Gormok on their side.
            var g4 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            ToTurn(g4, g4.Player2);
            for (int i = 0; i < 4; i++) Summon(g4.Player2, "CS2_231");
            Play(g4, g4.Player2, id, g4.Player1.Hero);
            Ok(g4.Player1.Hero.Damage == 4, "the opponent's Gormok counts their own minions");
            // Four other minions on the opponent's side only do not count for me.
            var g5 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            for (int i = 0; i < 4; i++) Summon(g5.Player2, "CS2_231");
            var gm5 = Add(g5.Player1, id);
            Ok(CanPlayCard(g5.Player1, gm5) && !CanPlayCard(g5.Player1, gm5, g5.Player2.Hero), "their minions do not count");
            // A summoned Gormok has no Battlecry.
            var g6 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            for (int i = 0; i < 4; i++) Summon(g6.Player1, "CS2_231");
            Summon(g6.Player1, id);
            Ok(g6.Player2.Hero.Damage == 0, "a summoned Gormok deals no damage");
        });
    }

    private static void Chillmaw(Game g, Controller p, string id)
    {
        var dragon = Add(p, "EX1_562");
        var chill = (Minion)Play(g, p, id);
        Ok(chill.AttackDamage == 6 && chill.Health == 6 && chill.HasTaunt && chill.Card.IsRace(Race.DRAGON), "Chillmaw is a 7-mana 6/6 Taunt Dragon");
        var enemy = Summon(p.Opponent, "CS2_182"); var ally = Summon(p, "CS2_182");
        Kill(g, chill);
        Ok(enemy.Damage == 3 && ally.Damage == 3 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "holding a Dragon: 3 damage to all minions");
        var g2 = NewGame(1, CardClass.ROGUE, CardClass.MAGE);
        var c2 = Summon(g2.Player1, id); var e2 = Summon(g2.Player2, "CS2_182");
        Kill(g2, c2);
        Ok(e2.Damage == 0, "no Dragon in hand: nothing");
        var g3 = NewGame(1, CardClass.ROGUE, CardClass.MAGE);
        Add(g3.Player1, "EX1_562"); var c3 = Summon(g3.Player1, id); c3.Silence(); var e3 = Summon(g3.Player2, "CS2_182");
        Kill(g3, c3);
        Ok(e3.Damage == 0, "Silenced: nothing");
        var g4 = NewGame(1, CardClass.ROGUE, CardClass.MAGE);
        Summon(g4.Player1, "EX1_562"); var c4 = Summon(g4.Player1, id); var e4 = Summon(g4.Player2, "CS2_182");
        Kill(g4, c4);
        Ok(e4.Damage == 0, "a Dragon on the battlefield does not count");
    }

    private static void BolfRamshield(Game g0, Controller p0, string id)
    {
        var b0 = (Minion)Play(g0, p0, id);
        Ok(b0.AttackDamage == 3 && b0.Health == 9 && p0.UsedMana == 6, "Bolf Ramshield is a 6-mana 3/9");
        StolenBolf(id);
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var bolf = (Minion)Play(g, me, id);
            ToTurn(g, foe);
            Play(g, foe, "CS2_029", me.Hero);
            Ok(me.Hero.Damage == 0 && bolf.Damage == 6, "a Fireball at my hero is taken by Bolf: hero " + me.Hero.Damage + " bolf " + bolf.Damage);
            Resume(g, next => Ok(next.Player1.Hero.Damage == 0 && next.Player1.BoardZone.First().Damage == 6, "clone keeps the redirect result"));
            // The enemy hero is not protected, and neither are my minions.
            Play(g, foe, "CS2_029", foe.Hero);
            Ok(foe.Hero.Damage == 6, "their hero takes damage normally");
            var other = Summon(me, "CS2_182");
            Play(g, foe, "CS2_024", other);
            Ok(other.Damage == 3, "my other minions take their own damage");
            // Combat damage to my hero lands on Bolf.
            var attacker = Summon(foe, "CS2_182"); attacker.IsExhausted = false;
            bolf.Damage = 0; other.Silence();
            var tauntless = me.BoardZone.Where(m => m.HasTaunt).ToArray();
            Ok(tauntless.Length == 0, "no Taunt on my board");
            Attack(g, attacker, me.Hero);
            Ok(me.Hero.Damage == 0 && bolf.Damage == 4, "an attack on my hero is redirected to Bolf: " + bolf.Damage);
            // Own spells aimed at my own hero too.
            ToTurn(g, me);
            Play(g, me, "CS2_029", me.Hero);
            Ok(me.Hero.Damage == 0 && Dead(bolf), "my own damage to my hero is also redirected (Bolf took 4 + 6 and died)");
        });
        // Lethal redirected: hero lives, Bolf dies.
        for (int seed = 1; seed <= 3; seed++)
        {
            var g = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var me = g.Player1; var foe = me.Opponent;
            var bolf = (Minion)Play(g, me, id);
            me.Hero.Damage = 29; bolf.Damage = 5;
            ToTurn(g, foe);
            Play(g, foe, "CS2_029", me.Hero);
            Ok(me.Hero.Damage == 29 && Dead(bolf) && me.Hero.Health == 1, "lethal damage to the hero kills Bolf and spares the hero");
            Play(g, foe, "CS2_029", me.Hero);
            Ok(me.Hero.Health <= 0 || g.State == State.COMPLETE, "with Bolf gone the hero takes the next one");
            // Divine Shield absorbs the redirected hit.
            var g2 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var b2 = (Minion)Play(g2, g2.Player1, id); b2.HasDivineShield = true;
            ToTurn(g2, g2.Player2);
            Play(g2, g2.Player2, "CS2_029", g2.Player1.Hero);
            Ok(g2.Player1.Hero.Damage == 0 && b2.Damage == 0 && !b2.HasDivineShield, "Bolf's Divine Shield pops, the hero takes nothing");
            // Silenced Bolf redirects nothing.
            var g3 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var b3 = (Minion)Play(g3, g3.Player1, id); b3.Silence();
            ToTurn(g3, g3.Player2);
            Play(g3, g3.Player2, "CS2_029", g3.Player1.Hero);
            Ok(g3.Player1.Hero.Damage == 6 && b3.Damage == 0, "Silenced Bolf: the hero takes it");
            // The opponent's Bolf protects their hero only.
            var g4 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            ToTurn(g4, g4.Player2);
            var b4 = (Minion)Play(g4, g4.Player2, id);
            Play(g4, g4.Player2, "CS2_029", g4.Player2.Hero);
            Ok(g4.Player2.Hero.Damage == 0 && b4.Damage == 6, "their Bolf protects their hero");
            ToTurn(g4, g4.Player1);
            Play(g4, g4.Player1, "CS2_029", g4.Player2.Hero);
            Ok(g4.Player2.Hero.Damage == 0 && Dead(b4), "and takes my damage aimed at their hero (6 + 6 kills it)");
            // Two Bolfs: damage is conserved.
            var g5 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var x = (Minion)Play(g5, g5.Player1, id); var y = (Minion)Play(g5, g5.Player1, id);
            ToTurn(g5, g5.Player2);
            Play(g5, g5.Player2, "CS2_029", g5.Player1.Hero);
            Ok(g5.Player1.Hero.Damage == 0 && x.Damage + y.Damage == 6, "two Bolfs share one hit, no more");
        }
    }

    private static void Icehowl(Game g0, Controller p0, string id)
    {
        var i0 = (Minion)Play(g0, p0, id);
        Ok(i0.AttackDamage == 10 && i0.Health == 10 && i0.HasCharge && p0.UsedMana == 9, "Icehowl is a 9-mana 10/10 Charge");
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var ice = (Minion)Play(g, me, id);
            Ok(!me.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == ice.Id), "no enemy minion: it has nothing to attack and cannot go face");
            var target = Summon(foe, "CS2_182");
            Ok(CanAttack(me, ice, target) && !CanAttack(me, ice, foe.Hero), "Charge lets it attack a minion at once, never the hero");
            Resume(g, next => Ok(!next.Player1.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Target?.Id == next.Player2.Hero.Id), "clone still cannot attack the hero"));
            Attack(g, ice, target);
            Ok(Dead(target), "it kills the minion");
            // Silence lifts the restriction.
            var g1 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var calm = Summon(g1.Player1, id);
            End(g1); End(g1);
            Ok(!CanAttack(g1.Player1, calm, g1.Player2.Hero) && g1.Player1.Options().Any(t => t.PlayerTaskType == PlayerTaskType.END_TURN) && !g1.Player1.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == calm.Id),
                "a rested Icehowl still cannot attack the hero");
            calm.Silence();
            Ok(CanAttack(g1.Player1, calm, g1.Player2.Hero), "a Silenced Icehowl can attack heroes");
            // A summoned Icehowl is restricted too; the opponent's Icehowl cannot hit my hero.
            var g2 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            ToTurn(g2, g2.Player2);
            var theirs = (Minion)Play(g2, g2.Player2, id);
            var mine = Summon(g2.Player1, "CS2_182");
            Ok(CanAttack(g2.Player2, theirs, mine) && !CanAttack(g2.Player2, theirs, g2.Player1.Hero), "the opponent's Icehowl cannot attack my hero");
            var summoned = Summon(g2.Player2, id); summoned.IsExhausted = false;
            Ok(!CanAttack(g2.Player2, summoned, g2.Player1.Hero), "a summoned Icehowl is restricted as well");
            // Control change keeps the restriction.
            var g3 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var held = Summon(g3.Player1, id);
            new ControlTask(EntityType.TARGET).Process(g3, g3.Player2, g3.Player2.Hero, held);
            g3.DeathProcessingAndAuraUpdate();
            ToTurn(g3, g3.Player2);
            held.IsExhausted = false;
            Summon(g3.Player1, "CS2_182");
            Ok(held.Controller == g3.Player2 && !CanAttack(g3.Player2, held, g3.Player1.Hero), "a stolen Icehowl still cannot attack heroes");
        });
    }

    private static void NexusChampionSaraad(Game g0, Controller p0, string id)
    {
        var s0 = (Minion)Play(g0, p0, id);
        Ok(s0.AttackDamage == 4 && s0.Health == 5 && p0.UsedMana == 5, "Nexus-Champion Saraad is a 5-mana 4/5");
        var seen = new HashSet<string>();
        for (int seed = 1; seed <= 30; seed++)
        {
            var g = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var me = g.Player1; var foe = me.Opponent;
            Play(g, me, id);
            Ok(me.HandZone.Count == 0, "no spell before Inspire");
            UseHeroPower(g, me);
            Ok(me.HandZone.Count == 1, "Inspire adds one card");
            var card = me.HandZone.Single();
            Ok(card.Card.Type == CardType.SPELL && card.Card.Collectible, "a collectible spell: " + card.Card.Id);
            seen.Add(card.Card.Id);
            if (seed <= 3)
            {
                var again = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
                Play(again, again.Player1, id); UseHeroPower(again, again.Player1);
                Ok(again.Player1.HandZone.Single().Card.Id == card.Card.Id, "same seed, same spell");
                Resume(g, next => Ok(next.Player1.HandZone.Single().Card.Id == card.Card.Id, "clone keeps the spell"));
                // Their Hero Power does not Inspire it.
                UseHeroPower(g, foe, me.Hero);
                Ok(me.HandZone.Count == 1, "the opponent's Hero Power does not Inspire it");
                // Silenced: nothing. Full hand: burned.
                var g2 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
                var s = (Minion)Play(g2, g2.Player1, id); s.Silence();
                UseHeroPower(g2, g2.Player1);
                Ok(g2.Player1.HandZone.Count == 0, "Silenced Saraad does not Inspire");
                var g3 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
                Play(g3, g3.Player1, id); FillHand(g3.Player1, 10);
                UseHeroPower(g3, g3.Player1);
                Ok(g3.Player1.HandZone.Count == 10 && g3.Player1.HandZone.All(c => c.Card.Type != CardType.SPELL), "full hand: the spell is burned");
                // The opponent's Saraad fills their own hand.
                var g4 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
                ToTurn(g4, g4.Player2);
                Play(g4, g4.Player2, id);
                UseHeroPower(g4, g4.Player2, g4.Player1.Hero);
                Ok(g4.Player2.HandZone.Count(c => c.Card.Type == CardType.SPELL) == 1 && !g4.Player1.HandZone.Any(c => c.Card.Type == CardType.SPELL), "their Saraad adds to their own hand");
            }
        }
        Ok(seen.Count >= 8, "30 seeds give many distinct spells: " + seen.Count);
        var gh = NewGame(2, CardClass.PALADIN, CardClass.MAGE);
        Play(gh, gh.Player1, id); UseHeroPower(gh, gh.Player1);
        string added = gh.Player1.HandZone.Single().Card.Id;
        HiddenSame(gh, game => game.Player1.HandZone.Single(), added == "CS2_029" ? "CS2_024" : "CS2_029", 2, "random spell in hand");
    }

    private static void TheSkeletonKnight(Game g0, Controller p0, string id)
    {
        var k0 = (Minion)Play(g0, p0, id);
        Ok(k0.AttackDamage == 7 && k0.Health == 4 && p0.UsedMana == 6, "The Skeleton Knight is a 6-mana 7/4");
        Minion KnightGame(int seed, string[] mine, string[] theirs, out Game game, CardClass first = CardClass.PALADIN)
        {
            game = NewGame(seed, first, CardClass.MAGE);
            SetDeck(game.Player1, mine); SetDeck(game.Player2, theirs);
            return Summon(game.Player1, id);
        }
        bool Returned(Game game) => game.Player1.HandZone.Count(c => c.Card.Id == id) == 1;
        for (int seed = 1; seed <= 3; seed++)
        {
            var k = KnightGame(seed, new[] { "CS2_200" }, new[] { "CS2_231" }, out var g);
            Kill(g, k);
            Ok(Returned(g) && Dead(k), "winning the Joust returns the Knight to hand");
            var back = (Minion)g.Player1.HandZone.Single();
            Ok(back.AttackDamage == 7 && back.Health == 4 && back.Cost == 6 && back.Id != k.Id, "it comes back as a fresh 6-mana 7/4");
            Ok(Revealed(g, g.Player1.DeckZone[0].Id, g.Player2.DeckZone[0].Id), "both Joust minions are shown in the Joust block");
            Resume(g, next => Ok(next.Player1.HandZone.Count(c => c.Card.Id == id) == 1, "clone keeps the returned Knight"));
            // Played again it joins the board; it can die and return again.
            ToTurn(g, g.Player1);
            PlayEntity(g, g.Player1, back);
            Kill(g, (Minion)g.Player1.BoardZone.First(m => m.Card.Id == id));
            Ok(Returned(g), "and again");
            // Losing, tying, no minion: it stays dead.
            var lo = KnightGame(seed, new[] { "CS2_231" }, new[] { "CS2_200" }, out var g2); Kill(g2, lo);
            Ok(!Returned(g2), "a lower cost loses the Joust");
            var tie = KnightGame(seed, new[] { "CS2_182" }, new[] { "CS2_182" }, out var g3); Kill(g3, tie);
            Ok(!Returned(g3), "a tie loses");
            var nomine = KnightGame(seed, new[] { "CS2_029" }, new[] { "CS2_231" }, out var g4); Kill(g4, nomine);
            Ok(!Returned(g4), "no minion in my deck loses");
            var nofoe = KnightGame(seed, new[] { "CS2_231" }, new[] { "CS2_029" }, out var g5); Kill(g5, nofoe);
            Ok(Returned(g5), "no minion in their deck: I win");
            var none = KnightGame(seed, Array.Empty<string>(), Array.Empty<string>(), out var g6); Kill(g6, none);
            Ok(!Returned(g6), "empty decks: nothing returns");
            var both = KnightGame(seed, new[] { "CS2_029" }, new[] { "CS2_029" }, out var g7); Kill(g7, both);
            Ok(!Returned(g7), "no minion on either side: nothing returns");
            // Silenced: no Deathrattle.
            var si = KnightGame(seed, new[] { "CS2_200" }, new[] { "CS2_231" }, out var g8); si.Silence(); Kill(g8, si);
            Ok(!Returned(g8) && !g8.PowerHistory.Full.OfType<PowerHistoryMetaData>().Any(m => m.Type == MetaDataType.JOUST), "Silenced Knight has no Deathrattle and reveals nothing");
            // Full hand: nothing comes back.
            var fh = KnightGame(seed, new[] { "CS2_200" }, new[] { "CS2_231" }, out var g9); FillHand(g9.Player1, 10); Kill(g9, fh);
            Ok(g9.Player1.HandZone.Count == 10 && !Returned(g9), "full hand: the Knight is not returned");
            // Buffs do not come back with it.
            var bk = KnightGame(seed, new[] { "CS2_200" }, new[] { "CS2_231" }, out var g10);
            Play(g10, g10.Player1, "CS2_092", bk);
            Ok(bk.AttackDamage == 11, "buffed Knight");
            Kill(g10, bk);
            Ok(Returned(g10) && ((Minion)g10.Player1.HandZone.Single()).AttackDamage == 7, "the returned Knight has no buff");
            // The opponent's Knight returns to their hand.
            var g11 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            SetDeck(g11.Player1, "CS2_231"); SetDeck(g11.Player2, "CS2_200");
            var enemy = Summon(g11.Player2, id); Kill(g11, enemy);
            Ok(g11.Player2.HandZone.Count(c => c.Card.Id == id) == 1 && g11.Player1.HandZone.Count == 0, "the opponent's Knight returns to their hand");
        }
        // Random pick among several minions per deck: both outcomes happen and a seed repeats.
        int wins = 0, losses = 0;
        for (int seed = 1; seed <= 24; seed++)
        {
            var k = KnightGame(seed, new[] { "CS2_200", "CS2_231", "CS2_168" }, new[] { "CS2_182" }, out var g); Kill(g, k);
            if (Returned(g)) wins++; else losses++;
            var k2 = KnightGame(seed, new[] { "CS2_200", "CS2_231", "CS2_168" }, new[] { "CS2_182" }, out var g2); Kill(g2, k2);
            Ok(Returned(g2) == Returned(g), "same seed, same Joust");
        }
        Ok(wins > 0 && losses > 0, "mixed decks win some Jousts and lose others: " + wins + "/" + losses);
        // Hidden information: an unrevealed card in either deck stays hidden; the returned Knight is public.
        var gh = NewGame(1, CardClass.PALADIN, CardClass.MAGE);
        SetDeck(gh.Player1, "CS2_029", "CS2_200"); SetDeck(gh.Player2, "CS2_231", "CS2_029");
        Kill(gh, Summon(gh.Player1, id));
        HiddenSame(gh, game => game.Player1.DeckZone.First(c => c.Card.Id == "CS2_029"), "CS2_024", 2, "unrevealed spell in my deck");
        HiddenSame(gh, game => game.Player2.DeckZone.First(c => c.Card.Id == "CS2_029"), "CS2_024", 1, "unrevealed spell in their deck");
    }

    private static void FjolaLightbane(Game g0, Controller p0, string id)
    {
        var f0 = (Minion)Play(g0, p0, id);
        Ok(f0.AttackDamage == 3 && f0.Health == 4 && p0.UsedMana == 3 && !f0.HasDivineShield, "Fjola Lightbane is a 3-mana 3/4");
        StolenSpellTarget(id, "CS2_004", CardClass.PRIEST, (g, m) => m.HasDivineShield);
        Seeds(CardClass.PRIEST, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var fjola = (Minion)Play(g, me, id);
            Ok(!fjola.HasDivineShield, "no shield before a spell");
            Play(g, me, "CS2_004", fjola);
            Ok(fjola.HasDivineShield && fjola.Health == 6, "targeting it with my spell grants Divine Shield");
            Resume(g, next => Ok(next.Player1.BoardZone.First().HasDivineShield, "clone keeps the shield"));
            // Another spell on another minion does not; an untargeted spell does not.
            var other = Summon(me, "CS2_182");
            fjola.HasDivineShield = false;
            Play(g, me, "CS2_004", other);
            Play(g, me, "EX1_621");
            Ok(!fjola.HasDivineShield, "spells aimed elsewhere or untargeted do nothing");
            // Hero Power targeting is not a spell.
            UseHeroPower(g, me, fjola);
            Ok(!fjola.HasDivineShield, "a Hero Power is not a spell");
            // The shield can be regained after it pops.
            Play(g, me, "CS2_004", fjola);
            Ok(fjola.HasDivineShield, "regained");
            // The opponent's spell on my Fjola does nothing.
            fjola.HasDivineShield = false;
            ToTurn(g, foe);
            Play(g, foe, "CS2_024", fjola);
            Ok(!fjola.HasDivineShield, "the opponent targeting it does not grant a shield");
            // Silenced: nothing.
            ToTurn(g, me);
            fjola.Silence();
            Play(g, me, "CS2_004", fjola);
            Ok(!fjola.HasDivineShield, "Silenced Fjola gains nothing");
            // Their Fjola with their spell.
            var g2 = NewGame(seed, CardClass.PRIEST, CardClass.PRIEST);
            ToTurn(g2, g2.Player2);
            var theirs = (Minion)Play(g2, g2.Player2, id);
            Play(g2, g2.Player2, "CS2_004", theirs);
            Ok(theirs.HasDivineShield, "their Fjola reacts to their own spell");
        });
    }

    private static void SeaReaver(Game g0, Controller p0, string id)
    {
        var s0 = (Minion)Play(g0, p0, id);
        Ok(s0.AttackDamage == 6 && s0.Health == 7 && p0.UsedMana == 6, "Sea Reaver is a 6-mana 6/7");
        // The draw rule that keeps "When you draw this" minions in hand reaches only legacy scripts, never a Standard module or reprint.
        var staying = Cards.All.Where(c => c.Implemented && c.Type == CardType.MINION && c.Power?.TopdeckTask != null && c[(GameTag)3128] == 0).ToArray();
        Ok(staying.Any(c => c.Id == id), "Sea Reaver is among the minions that keep their place in hand");
        Ok(staying.All(c => !EffectModules.ByDbfId.ContainsKey(c.AssetId) && !Reprints.ByDbfId.Values.Contains(c.AssetId) && !Reprints.ByDbfId.ContainsKey(c.AssetId)),
            "only legacy scripts keep their place in hand: " + string.Join(",", staying.Select(c => c.Id)));
        Seeds(CardClass.WARRIOR, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var a = Summon(me, "CS2_182"); var b = Summon(me, "CS2_182");
            var enemy = Summon(foe, "CS2_182");
            SetDeck(me, "CS2_182", id);
            Generic.Draw(me);
            g.DeathProcessingAndAuraUpdate();
            Ok(a.Damage == 1 && b.Damage == 1 && enemy.Damage == 0 && me.Hero.Damage == 0 && foe.Hero.Damage == 0,
                "drawing it deals 1 to each of my minions only: " + a.Damage + "/" + b.Damage + " enemy " + enemy.Damage + " hands " + me.HandZone.Count + " " + string.Join(",", me.HandZone.Select(c => c.Card.Id)));
            Ok(me.HandZone.Count == 1 && me.HandZone[0].Card.Id == id, "Sea Reaver stays in hand after being drawn");
            Resume(g, next => Ok(next.Player1.HandZone.Count == 1 && next.Player1.BoardZone.All(m => m.Damage == 1), "clone keeps the result"));
            // Playing it from hand does nothing more.
            PlayEntity(g, me, me.HandZone[0]);
            Ok(a.Damage == 1 && b.Damage == 1, "playing it deals no damage");
            // Minions at 1 health die from the draw.
            var g2 = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            var low = Summon(g2.Player1, "CS2_231"); var hi = Summon(g2.Player1, "CS2_182");
            SetDeck(g2.Player1, id);
            Generic.Draw(g2.Player1);
            g2.DeathProcessingAndAuraUpdate();
            Ok(Dead(low) && Alive(hi) && hi.Damage == 1, "a 1-health minion dies");
            // The opponent drawing it hurts their minions.
            var g3 = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            var mine3 = Summon(g3.Player1, "CS2_182"); var theirs3 = Summon(g3.Player2, "CS2_182");
            SetDeck(g3.Player2, id);
            Generic.Draw(g3.Player2);
            g3.DeathProcessingAndAuraUpdate();
            Ok(mine3.Damage == 0 && theirs3.Damage == 1 && g3.Player2.HandZone.Count == 1, "their Sea Reaver hits their own minions");
            // No minions: nothing happens and nothing breaks.
            var g4 = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            SetDeck(g4.Player1, id);
            Generic.Draw(g4.Player1);
            Ok(g4.Player1.HandZone.Count == 1 && g4.Player1.Hero.Damage == 0, "no minions: no damage");
            // Turn-start draw triggers it too.
            var g5 = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            var m5 = Summon(g5.Player1, "CS2_182");
            SetDeck(g5.Player1, "CS2_182", id);
            End(g5); End(g5);
            Ok(g5.Player1.HandZone.Any(c => c.Card.Id == id) && m5.Damage == 1, "the draw at turn start triggers it");
            // Created straight into hand: not drawn, no damage.
            var g6 = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            var m6 = Summon(g6.Player1, "CS2_182");
            Add(g6.Player1, id);
            Ok(m6.Damage == 0, "a Sea Reaver created in hand is not drawn");
        });
    }

    private static void EydisDarkbane(Game g0, Controller p0, string id)
    {
        var e0 = (Minion)Play(g0, p0, id);
        Ok(e0.AttackDamage == 3 && e0.Health == 4 && p0.UsedMana == 3, "Eydis Darkbane is a 3-mana 3/4");
        StolenSpellTarget(id, "CS2_024", CardClass.MAGE, (g, m) => g.Player1.Hero.Damage + g.Player1.BoardZone.Sum(x => x.Damage) == 3);
        // Random enemy: with three enemy minions and a hero, every candidate is hit over many seeds, exactly 3 damage once.
        var hit = new HashSet<int>();
        for (int seed = 1; seed <= 32; seed++)
        {
            var g = NewGame(seed, CardClass.MAGE, CardClass.PRIEST);
            var me = g.Player1; var foe = me.Opponent;
            var eydis = (Minion)Play(g, me, id);
            var a = Summon(foe, "CS2_182"); var b = Summon(foe, "CS2_182"); var c = Summon(foe, "CS2_182");
            var mine = Summon(me, "CS2_182");
            Play(g, me, "CS2_024", eydis);
            int total = a.Damage + b.Damage + c.Damage + foe.Hero.Damage;
            Ok(total == 3 && new[] { a.Damage, b.Damage, c.Damage, foe.Hero.Damage }.Count(x => x == 3) == 1, "exactly 3 damage to one enemy");
            Ok(eydis.Damage == 3 && mine.Damage == 0 && me.Hero.Damage == 0, "my own side only takes the Frostbolt");
            hit.Add(a.Damage == 3 ? 0 : b.Damage == 3 ? 1 : c.Damage == 3 ? 2 : 3);
            if (seed <= 3)
            {
                var again = NewGame(seed, CardClass.MAGE, CardClass.PRIEST);
                var e2 = (Minion)Play(again, again.Player1, id);
                var a2 = Summon(again.Player2, "CS2_182"); var b2 = Summon(again.Player2, "CS2_182"); var c2 = Summon(again.Player2, "CS2_182");
                Summon(again.Player1, "CS2_182");
                Play(again, again.Player1, "CS2_024", e2);
                Ok((a2.Damage == 3 ? 0 : b2.Damage == 3 ? 1 : c2.Damage == 3 ? 2 : 3) == (a.Damage == 3 ? 0 : b.Damage == 3 ? 1 : c.Damage == 3 ? 2 : 3), "same seed, same victim");
                Resume(g, next => Ok(next.Player1.BoardZone.First().Damage == 3, "clone keeps the result"));
            }
        }
        Ok(hit.Count == 4, "every enemy character is hit over 32 seeds: " + hit.Count);
        Seeds(CardClass.MAGE, CardClass.PRIEST, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var eydis = (Minion)Play(g, me, id);
            // No enemy minions: the hero takes it.
            Play(g, me, "CS2_024", eydis);
            Ok(foe.Hero.Damage == 3, "with no enemy minion the enemy hero takes 3");
            // A spell that does not target it, or a Hero Power, does nothing.
            foe.Hero.Damage = 0;
            Play(g, me, "CS2_024", foe.Hero);
            Ok(foe.Hero.Damage == 3, "a spell aimed at another target does not trigger it (only the Frostbolt itself)");
            foe.Hero.Damage = 0; eydis.Damage = 0;
            UseHeroPower(g, me, eydis);
            Ok(foe.Hero.Damage == 0, "a Hero Power is not a spell");
            // The opponent's spell does not trigger it.
            ToTurn(g, foe);
            eydis.Damage = 0;
            Play(g, foe, "CS2_004", eydis);
            Ok(foe.Hero.Damage == 0 && me.Hero.Damage == 0, "the opponent targeting it does not trigger it");
            // It also fires when the spell kills it.
            ToTurn(g, me);
            eydis.Damage = 0;
            Play(g, me, "CS2_029", eydis);
            Ok(Dead(eydis) && foe.Hero.Damage == 3, "a Fireball at Eydis still triggers it before she dies");
            // Silenced: no trigger.
            var g2 = NewGame(seed, CardClass.MAGE, CardClass.PRIEST);
            var s = (Minion)Play(g2, g2.Player1, id); s.Silence();
            Play(g2, g2.Player1, "CS2_024", s);
            Ok(g2.Player2.Hero.Damage == 0, "Silenced Eydis does nothing");
            // Their Eydis hits my side.
            var g3 = NewGame(seed, CardClass.MAGE, CardClass.MAGE);
            ToTurn(g3, g3.Player2);
            var theirs = (Minion)Play(g3, g3.Player2, id);
            Play(g3, g3.Player2, "CS2_024", theirs);
            Ok(g3.Player1.Hero.Damage == 3, "their Eydis hits my hero when I have no minions");
        });
    }

    private static void JusticarTrueheart(Game g0, Controller p0, string id)
    {
        var j0 = (Minion)Play(g0, p0, id);
        Ok(j0.AttackDamage == 6 && j0.Health == 4 && p0.UsedMana == 5, "Justicar Trueheart is a 5-mana 6/4");
        Seeds(CardClass.MAGE, CardClass.PRIEST, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            Ok(me.Hero.HeroPower.Card.Id == "CS2_034", "starts with Fireblast");
            Play(g, me, id);
            Ok(me.Hero.HeroPower.Card.Id == "AT_132_MAGE", "Fireblast becomes Fireblast Rank 2");
            UseHeroPower(g, me, foe.Hero);
            Ok(foe.Hero.Damage == 2, "Fireblast Rank 2 deals 2");
            Resume(g, next => Ok(next.Player1.Hero.HeroPower.Card.Id == "AT_132_MAGE", "clone keeps the upgrade"));
            // A second Justicar finds nothing to upgrade.
            Play(g, me, id);
            Ok(me.Hero.HeroPower.Card.Id == "AT_132_MAGE", "an upgraded Hero Power is not replaced again");
        });
        var upgrades = new Dictionary<CardClass, string>
        {
            [CardClass.PRIEST] = "AT_132_PRIEST", [CardClass.DRUID] = "AT_132_DRUID", [CardClass.HUNTER] = "AT_132_HUNTER",
            [CardClass.PALADIN] = "AT_132_PALADIN", [CardClass.ROGUE] = "AT_132_ROGUE", [CardClass.SHAMAN] = "AT_132_SHAMAN",
            [CardClass.WARLOCK] = "AT_132_WARLOCK", [CardClass.WARRIOR] = "AT_132_WARRIOR",
        };
        foreach (var (cls, upgrade) in upgrades)
        {
            for (int seed = 1; seed <= 3; seed++)
            {
                var g = NewGame(seed, cls, CardClass.MAGE);
                var me = g.Player1; var foe = me.Opponent;
                string before = me.Hero.HeroPower.Card.Id;
                Play(g, me, id);
                Ok(me.Hero.HeroPower.Card.Id == upgrade && before != upgrade, cls + " upgrades " + before + " to " + upgrade);
                Ok(me.Hero.HeroPower.Cost == 2 && !me.Hero.HeroPower.IsExhausted, cls + " upgraded power costs 2 and is ready");
                Refresh(me);
                switch (cls)
                {
                    case CardClass.PRIEST:
                        me.Hero.Damage = 10;
                        Ok(g.Process(HeroPowerTask.Any(me, me.Hero)) && me.Hero.Damage == 6, "Heal restores 4");
                        break;
                    case CardClass.DRUID:
                        Ok(g.Process(HeroPowerTask.Any(me)) && me.Hero.AttackDamage == 2 && me.Hero.Armor == 2, "Dire Shapeshift: +2 Attack and 2 Armor");
                        break;
                    case CardClass.HUNTER:
                        Ok(g.Process(HeroPowerTask.Any(me)) && foe.Hero.Damage == 3, "Ballista Shot: 3 damage to the enemy hero");
                        break;
                    case CardClass.PALADIN:
                        Ok(g.Process(HeroPowerTask.Any(me)) && me.BoardZone.Count(m => m.Card.Id == "CS2_101t") == 2, "The Silver Hand: two Recruits");
                        break;
                    case CardClass.ROGUE:
                        Ok(g.Process(HeroPowerTask.Any(me)) && me.Hero.Weapon != null && me.Hero.Weapon.AttackDamage == 2 && me.Hero.Weapon.Durability == 2, "Poisoned Daggers: 2/2 weapon");
                        break;
                    case CardClass.SHAMAN:
                        Ok(g.Process(HeroPowerTask.Any(me)) && me.Choice != null && me.Choice.Choices.Count >= 3, "Totemic Slam offers a choice of totems: " + me.Choice?.Choices.Count);
                        Ok(me.Choice.Choices.Select(c => g.IdEntityDic[c].Card.Id).Distinct().Count() == me.Choice.Choices.Count, "the totem offers are distinct");
                        var pick = me.Choice.Choices.First();
                        bool picked = g.Process(ChooseTask.Pick(me, pick));
                        Ok(picked && me.BoardZone.Any(m => m.Card.Id == g.IdEntityDic[pick].Card.Id), "Totemic Slam summons the chosen totem: " + picked + " " + string.Join(",", me.BoardZone.Select(m => m.Card.Id)));
                        break;
                    case CardClass.WARLOCK:
                        SetDeck(me, "CS2_182", "CS2_182");
                        Ok(g.Process(HeroPowerTask.Any(me)) && me.HandZone.Count == 1 && me.Hero.Damage == 0, "Soul Tap draws without damage");
                        break;
                    case CardClass.WARRIOR:
                        Ok(g.Process(HeroPowerTask.Any(me)) && me.Hero.Armor == 4, "Tank Up: 4 Armor");
                        break;
                }
                Ok(me.Hero.HeroPower.IsExhausted, cls + " the upgraded power can be used");
            }
        }
        // Heroes without an upgrade keep their power; the opponent's Justicar upgrades their power only.
        foreach (var cls in new[] { CardClass.DEMONHUNTER, CardClass.DEATHKNIGHT })
        {
            var g = NewGame(1, cls, CardClass.MAGE);
            string before = g.Player1.Hero.HeroPower.Card.Id;
            Play(g, g.Player1, id);
            Ok(g.Player1.Hero.HeroPower.Card.Id == before && g.Player1.BoardZone.Count == 1, cls + " has no upgrade: the power is kept");
        }
        var go = NewGame(1, CardClass.WARRIOR, CardClass.MAGE);
        ToTurn(go, go.Player2);
        Play(go, go.Player2, id);
        Ok(go.Player2.Hero.HeroPower.Card.Id == "AT_132_MAGE" && go.Player1.Hero.HeroPower.Card.Id == "CS2_102", "the opponent's Justicar upgrades their own power only");
        // A summoned Justicar has no Battlecry; a used Hero Power stays used when the upgrade lands.
        var gs = NewGame(1, CardClass.WARRIOR, CardClass.MAGE);
        Summon(gs.Player1, id);
        Ok(gs.Player1.Hero.HeroPower.Card.Id == "CS2_102", "a summoned Justicar upgrades nothing");
        // Silenced Hero Power replacement is not reachable; a Hero already replaced by a non-starting power keeps it.
        var gr = NewGame(1, CardClass.WARRIOR, CardClass.MAGE);
        Ok(new ReplaceHeroPower(Cards.FromId("CS2_034")).Process(gr, gr.Player1, gr.Player1.Hero, null) != TaskState.STOP, "set a foreign power");
        Play(gr, gr.Player1, id);
        Ok(gr.Player1.Hero.HeroPower.Card.Id == "AT_132_MAGE", "a Mage power on a Warrior upgrades to Rank 2");
    }
}
