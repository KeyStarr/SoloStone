using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

/// <summary>Wild wave Y0b, lane m4b: League of Explorers cards, proven against the 37.0 export through WildSetsProof.Case.</summary>
internal static class WildY0bM4BProof
{
    private static void Ok(bool condition, string message) => Core2026Proof.Check(condition, message);
    private static readonly List<string> HeldReport = new();

    [GroupProof("wild-y0b-m4b-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        HeldReport.Clear();
        WildSetsProof.Case("LOE_047", TombSpider);
        WildSetsProof.Case("LOE_050", MountedRaptor);
        WildSetsProof.Case("LOE_053", DjinniOfZephyrs);
        WildSetsProof.Held("LOE_061", "held: FX-MISSING live (deathrattle)", AnubisathSentinel);
        WildSetsProof.Held("LOE_073", FossilizedDevilsaur);
        WildSetsProof.Case("LOE_076", SirFinley);
        WildSetsProof.Case("LOE_077", BrannBronzebeard);
        WildSetsProof.Held("LOE_079", "held: generated-shuffle FX scope", EliseStarseeker);
        WildSetsProof.Case("LOE_086", SummoningStone);
        WildSetsProof.Case("LOE_089", WobblingRunts);
        WildSetsProof.Case("LOE_092", ArchThiefRafaam);
        WildSetsProof.Case("LOE_104", Entomb);
        WildSetsProof.Case("LOE_105", ExplorersHat);
        WildSetsProof.Held("LOE_107", "held: AdaptiveEffect tag history (bug-fix session)", EerieStatue);
        WildSetsProof.Held("LOE_110", AncientShade);
        WildSetsProof.Held("LOE_111", "held: generated-shuffle FX scope", ExcavatedEvil);
        WildSetsProof.Case("LOE_113", EveryfinIsAwesome);
        WildSetsProof.Held("LOE_116", "held: FX-MISSING live (play)", ReliquarySeeker);
        WildSetsProof.Case("LOE_118", CursedBlade);
        WildSetsProof.Case("LOE_119", AnimatedArmor);
        // HELD: the client stores SPELLPOWER 1 for this Spell Damage +2 card, so its identity row would break the effect.
        // Proof kept, not registered; it runs against the base card, which still carries +2.
        Held("LOE_051", "export tag 192 = 1 for Spell Damage +2 (generator fix pending)", JungleMoonkin);
        foreach (string line in HeldReport) Console.WriteLine(line);
    }

    // ---- helpers ----

    private static void Held(string id, string reason, Action<Game, Controller, string> scenario)
    {
        var failures = new List<string>();
        for (int seed = 1; seed <= 3; seed++)
        {
            try { Fixture(seed, game => scenario(game, game.Player1, id)); }
            catch (Exception error) { failures.Add("seed " + seed + ": " + error.Message); }
        }
        HeldReport.Add("HELD " + id + " (" + reason + "): behaviour proof " + (failures.Count == 0 ? "passes" : "FAILS " + string.Join(" | ", failures)));
    }

    private static void Fixture(int seed, Action<Game> scenario)
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
        scenario(game);
    }

    private static void Resume(Game game, Action<Game> continuation)
    {
        foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) })
        {
            Ok(Canonical.State(game) == Canonical.State(next), "state before continuation");
            continuation(next);
        }
    }

    // The same scenario under sixteen seeds: the set of results shows a random effect really draws from several candidates.
    private static HashSet<string> Across(Func<Game, string> run)
    {
        var results = new HashSet<string>();
        for (int seed = 1; seed <= 16; seed++) Fixture(seed, game => results.Add(run(game)));
        return results;
    }

    private static void SetDeck(Controller p, params string[] ids)
    {
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        foreach (string id in ids) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId(id)));
    }

    private static void ToTurn(Game g, Controller p) { while (g.CurrentPlayer != p) End(g); }
    private static void FillBoard(Controller p, string id = "CS2_182") { while (!p.BoardZone.IsFull) Summon(p, id); }
    private static void FillHand(Controller p) { while (!p.HandZone.IsFull) Add(p, "CS2_182"); }
    private static void EmptyHand(Controller p) { foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); } }
    private static void Steal(Game g, Controller thief, Minion minion)
    {
        new ControlTask(EntityType.TARGET).Process(g, thief, thief.Hero, minion);
        g.DeathProcessingAndAuraUpdate();
        Ok(minion.Controller == thief, "control changed");
    }
    private static bool Alive(Minion m) => m.Zone?.Type == Zone.PLAY;
    private static bool Dead(Minion m) => m.Zone?.Type == Zone.GRAVEYARD;
    private static bool CanPlay(Controller p, IPlayable card)
        => p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id);
    private static Minion[] MinionsOf(Controller p) => p.BoardZone.OfType<Minion>().ToArray();
    // Deathrattle summons are made while deaths are processed; the second pass is the one a played command always ends with.
    private static void Settle(Game g) { g.DeathProcessingAndAuraUpdate(); g.DeathProcessingAndAuraUpdate(); }
    private static void Kill(Game g, Minion m) { Core2026Proof.Kill(g, m); Settle(g); }
    private static void Silence(Game g, Minion m) { m.Silence(); g.DeathProcessingAndAuraUpdate(); }

    private static Card[] Offers(Game g, Controller p, string what, Func<Card, bool> filter)
    {
        Ok(p.Choice != null && p.Choice.Choices.Count == 3, what + " offers exactly three cards");
        var offers = p.Choice.Choices.Select(key => g.IdEntityDic[key].Card).ToArray();
        Ok(offers.Select(card => card.Id).Distinct().Count() == 3, what + " offers are distinct");
        var pool = Cards.FormatTypeClassCards(FormatType.FT_WILD)[p.BaseClass];
        foreach (var card in offers)
            Ok(filter(card) && pool.Contains(card) && card.Collectible,
                what + " offer " + card.Id + " (filter " + filter(card) + ", pool " + pool.Contains(card) + ", collectible " + card.Collectible
                + ") must come from the Wild class-or-neutral pool and match the filter");
        return offers;
    }

    private static IPlayable Pick(Game g, Controller p, int index)
    {
        int key = p.Choice.Choices[index];
        var chosen = g.IdEntityDic[key];
        Ok(g.Process(ChooseTask.Pick(p, key)), "pick accepted");
        return chosen;
    }

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

    private static void TombSpider(Game g, Controller p, string id)
    {
        int hand = p.HandZone.Count;
        var spider = (Minion)Play(g, p, id);
        Ok(Alive(spider) && spider.AttackDamage == 3 && spider.Health == 3 && spider.Card.IsRace(Race.BEAST) && p.UsedMana == 4,
            "Tomb Spider is a 4-mana 3/3 Beast");
        var offers = Offers(g, p, "Tomb Spider", card => card.IsRace(Race.BEAST));
        Resume(g, next => {
            var np = next.Player1;
            Ok(np.Choice.Choices.SequenceEqual(p.Choice.Choices), "pending Discover survives clone and snapshot");
            Pick(next, np, 2);
            Ok(np.Choice == null && np.HandZone.Count == hand + 1 && np.HandZone[np.HandZone.Count - 1].Card.Id == offers[2].Id, "clone picks the same card");
        });
        Pick(g, p, 1);
        Ok(p.Choice == null && p.HandZone.Count == hand + 1 && p.HandZone[p.HandZone.Count - 1].Card.Id == offers[1].Id
            && p.Opponent.HandZone.Count == 0 && p.Opponent.Choice == null, "only the picked Beast enters the hand; the opponent gets nothing");
        HiddenSame(g, game => game.Player1.HandZone[game.Player1.HandZone.Count - 1], "CS2_182", 2, "discovered Beast in hand");
        Summon(p, id);
        Ok(p.Choice == null, "summoning without playing does not Discover");
        // Brann: the battlecry runs twice, so two Discovers resolve one after the other.
        StandardSetsProof.ParkBoard(p); Summon(p, "LOE_077");
        int before = p.HandZone.Count;
        Play(g, p, id);
        Offers(g, p, "first Discover", card => card.IsRace(Race.BEAST)); Pick(g, p, 0);
        Ok(p.Choice != null, "Brann: a second Discover follows");
        Offers(g, p, "second Discover", card => card.IsRace(Race.BEAST)); Pick(g, p, 1);
        Ok(p.Choice == null && p.HandZone.Count == before + 2, "Brann: two Beasts picked, two cards gained");
        // A full hand burns the pick; nothing stalls.
        StandardSetsProof.ParkBoard(p);
        Play(g, p, id); FillHand(p);
        Pick(g, p, 0);
        Ok(p.Choice == null && p.HandZone.IsFull, "pick with a full hand is burned and nothing stalls");
        // The opponent Discovers from their own class pool.
        ToTurn(g, p.Opponent);
        Play(g, p.Opponent, id);
        Offers(g, p.Opponent, "opponent Discover", card => card.IsRace(Race.BEAST));
        Ok(p.Choice == null, "the caster's seat has no choice");
        Pick(g, p.Opponent, 0);
        Ok(p.Opponent.Choice == null, "opponent pick resolves");
    }

    private static void MountedRaptor(Game g, Controller p, string id)
    {
        var raptor = (Minion)Play(g, p, id);
        Ok(raptor.AttackDamage == 3 && raptor.Health == 2 && p.UsedMana == 3 && raptor.Card.IsRace(Race.BEAST), "3-mana 3/2 Beast");
        Ok(p.BoardZone.Count == 1, "playing it summons nothing (no battlecry): board " + string.Join(",", p.BoardZone.OfType<Minion>().Select(m => m.Card.Id)));
        Resume(g, next => {
            var r = (Minion)next.IdEntityDic[raptor.Id]; Kill(next, r);
            Ok(next.Player1.BoardZone.Count == 1 && next.Player1.BoardZone[0].Cost == 1, "clone: the Deathrattle still summons a 1-Cost minion");
        });
        var friend = Summon(p, "CS2_182");
        Kill(g, raptor);
        var summoned = MinionsOf(p).Where(m => m != friend).ToArray();
        Ok(summoned.Length == 1 && summoned[0].Card.Cost == 1 && summoned[0].Card.Collectible && summoned[0].Card.Type == CardType.MINION
            && Cards.FormatTypeCards(FormatType.FT_WILD).Contains(summoned[0].Card), "exactly one collectible Wild 1-Cost minion: "
            + string.Join(",", summoned.Select(m => m.Card.Id + "/cost" + m.Card.Cost + "/coll" + m.Card.Collectible + "/" + m.Card.Type)));
        Ok(p.Opponent.BoardZone.Count == 0, "the opponent side gets nothing");
        // Seeded random: the result varies over seeds, and the same seed always gives the same minion (clone agrees above).
        var results = Across(game => { var q = game.Player1; var r = Summon(q, id); Kill(game, r); return q.BoardZone[0].Card.Id; });
        Ok(results.Count >= 3, "the summoned minion varies over seeds: " + string.Join(",", results));
        // Silence stops the Deathrattle; a full board gets nothing; a position beside the neighbours is kept.
        StandardSetsProof.ParkBoard(p);
        var silenced = Summon(p, id); Silence(g, silenced); Kill(g, silenced);
        Ok(p.BoardZone.Count == 0, "Silence removes the Deathrattle");
        var left = Summon(p, "CS2_182"); var middle = Summon(p, id); var right = Summon(p, "CS2_182");
        Kill(g, middle);
        Ok(p.BoardZone.Count == 3 && p.BoardZone[0] == left && p.BoardZone[2] == right && p.BoardZone[1].Cost == 1, "the minion appears in the Raptor's slot");
        StandardSetsProof.ParkBoard(p);
        var full = Summon(p, id); FillBoard(p);
        Kill(g, full);
        Ok(p.BoardZone.Count == 7 && p.BoardZone[0].Cost == 1 && p.BoardZone[0].Card.Id != "CS2_182", "a Raptor dying on a full board frees its slot for the summon, which takes it");
        // Two Raptors dying together each summon; the opponent's Raptor summons for the opponent.
        StandardSetsProof.ParkBoard(p);
        var a = Summon(p, id); var b = Summon(p, id);
        a.ToBeDestroyed = true; b.ToBeDestroyed = true; Settle(g);
        Ok(p.BoardZone.Count == 2 && p.BoardZone.All(x => x.Cost == 1), "two Raptors summon two minions");
        var theirs = Summon(p.Opponent, id); Kill(g, theirs);
        Ok(p.Opponent.BoardZone.Count == 1 && p.Opponent.BoardZone[0].Cost == 1 && p.BoardZone.Count == 2, "the opponent's Raptor summons for the opponent");
        // Brann does not double a Deathrattle.
        StandardSetsProof.ParkBoard(p); Summon(p, "LOE_077"); var braved = Summon(p, id); Kill(g, braved);
        Ok(p.BoardZone.Count == 2, "Brann does not double the Deathrattle");
        // Control change: a stolen Raptor summons for its new controller.
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        var taken = Summon(p.Opponent, id); Steal(g, p, taken); Kill(g, taken);
        Ok(p.BoardZone.Count == 1 && p.Opponent.BoardZone.Count == 0, "a stolen Raptor summons for its new controller");
    }

    private static void JungleMoonkin(Game g, Controller p, string id)
    {
        var enemy = Summon(p.Opponent, "CS2_182");
        var moonkin = (Minion)Play(g, p, id);
        Ok(moonkin.AttackDamage == 4 && moonkin.Health == 4 && p.UsedMana == 4, "4-mana 4/4");
        Play(g, p, "CS2_029", enemy);
        Ok(enemy.Damage == 8, "own Fireball gets +2 Spell Damage");
        End(g);
        Play(g, p.Opponent, "CS2_029", p.Hero);
        Ok(p.Hero.Damage == 8, "the opponent's Fireball gets +2 too");
        Silence(g, moonkin);
        Play(g, p.Opponent, "CS2_029", p.Hero);
        Ok(p.Hero.Damage == 8 + 6, "Silence removes the bonus for both");
    }

    private static void DjinniOfZephyrs(Game g, Controller p, string id)
    {
        var djinni = (Minion)Play(g, p, id);
        Ok(Alive(djinni) && djinni.AttackDamage == 4 && djinni.Health == 6 && p.UsedMana == 5 && djinni.Card.IsRace(Race.ELEMENTAL), "5-mana 4/6 Elemental");
        var ally = Summon(p, "CS2_182");
        Play(g, p, "CS2_092", ally);
        Ok(ally.AttackDamage == 8 && ally.Health == 9 && djinni.AttackDamage == 8 && djinni.Health == 10,
            "a spell cast on another friendly minion is copied onto the Djinni");
        // Negatives: spell on the Djinni itself, on an enemy minion, on a hero, or without a target.
        Play(g, p, "CS2_092", djinni);
        Ok(djinni.AttackDamage == 12 && djinni.Health == 14 && ally.AttackDamage == 8, "cast on the Djinni itself: no copy");
        var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, "CS2_092", enemy);
        Ok(enemy.AttackDamage == 8 && djinni.AttackDamage == 12, "cast on an enemy minion: no copy");
        p.Opponent.Hero.Damage = 0; Play(g, p, "CS2_029", p.Opponent.Hero);
        Ok(djinni.Damage == 0 && p.Opponent.Hero.Damage == 6, "cast on a hero: no copy");
        int hand = p.HandZone.Count;
        Play(g, p, "CS2_004", ally);
        Ok(ally.Health == 11 && djinni.Health == 16, "Power Word: Shield: the copy gives the Djinni +2 Health");
        Ok(p.HandZone.Count == hand + 2, "and the copy's draw happens as well as the original's");
        Play(g, p, "CS2_023");
        Ok(djinni.Health == 16, "an untargeted spell is not copied");
        // Damage spell on a friendly minion: the copy hits the Djinni.
        var big = Summon(p, "CS2_182");
        Play(g, p, "CS2_029", big);
        Ok(big.Damage == 6 && djinni.Damage == 6, "Fireball on a friendly minion burns the Djinni too");
        // The copy is a copy, not a second cast: a minion that dies from the spell still gets its copy cast.
        var wisp = Summon(p, "CS2_231");
        Play(g, p, "CS2_029", wisp);
        Ok(Dead(wisp) && djinni.Damage == 12, "the target dying does not prevent the copy");
        // Choose One: the copy keeps the chosen mode.
        var wrathTarget = Summon(p, "CS2_182");
        var wrath = Add(p, "EX1_154");
        int deckBefore = p.DeckZone.Count;
        var chooseTwo = p.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == wrath.Id && t.Target?.Id == wrathTarget.Id && t.ChooseOne == 2);
        Ok(chooseTwo != null && g.Process(chooseTwo), "Wrath, second mode, accepted");
        Ok(wrathTarget.Damage == 1 && djinni.Damage == 13, "the copy uses the same mode on the Djinni (1 damage)");
        Ok(p.DeckZone.Count == deckBefore - 2, "both halves of the second mode draw");
        // Silence stops it; two Djinnis each copy; a Djinni controlled by the opponent works for them.
        var silenced = djinni; Silence(g, silenced);
        Play(g, p, "CS2_092", ally);
        Ok(silenced.AttackDamage == 4 && ally.AttackDamage == 12, "a silenced Djinni does not copy");
        StandardSetsProof.ParkBoard(p);
        var d1 = Summon(p, id); var d2 = Summon(p, id); var buddy = Summon(p, "CS2_182");
        Play(g, p, "CS2_092", buddy);
        Ok(buddy.AttackDamage == 8 && d1.AttackDamage == 8 && d2.AttackDamage == 8, "two Djinnis each get a copy");
        var stolen = Summon(p.Opponent, id); var theirAlly = Summon(p.Opponent, "CS2_182");
        ToTurn(g, p.Opponent);
        Play(g, p.Opponent, "CS2_092", theirAlly);
        Ok(stolen.AttackDamage == 8 && d1.AttackDamage == 8 && theirAlly.AttackDamage == 8, "the opponent's Djinni copies the opponent's spell only; mine do not");
        ToTurn(g, p);
        Steal(g, p, stolen);
        Play(g, p, "CS2_092", buddy);
        Ok(stolen.AttackDamage == 12 && buddy.AttackDamage == 12 && d1.AttackDamage == 12 && d2.AttackDamage == 12 && stolen.Controller == p,
            "after a control change the new controller's spells are copied (all three Djinnis follow): stolen " + stolen.AttackDamage + " buddy " + buddy.AttackDamage + " d1 " + d1.AttackDamage + " d2 " + d2.AttackDamage);
        // Clone: the trigger survives.
        Resume(g, next => {
            var np = next.Player1; var buddyCopy = (Minion)next.IdEntityDic[buddy.Id]; var d1Copy = (Minion)next.IdEntityDic[d1.Id];
            Play(next, np, "CS2_092", buddyCopy);
            Ok(d1Copy.AttackDamage == 16 && buddyCopy.AttackDamage == 16, "the trigger survives clone and snapshot");
        });
    }

    private static void AnubisathSentinel(Game g, Controller p, string id)
    {
        var sentinel = (Minion)Play(g, p, id);
        Ok(sentinel.AttackDamage == 4 && sentinel.Health == 4 && p.UsedMana == 5, "5-mana 4/4");
        Kill(g, sentinel);
        Ok(p.BoardZone.Count == 0, "alone: the Deathrattle has no target and nothing breaks");
        StandardSetsProof.ParkBoard(p);
        var a = Summon(p, "CS2_182"); var b = Summon(p, "CS2_182"); var c = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "CS2_182");
        sentinel = Summon(p, id);
        Resume(g, next => {
            var s = (Minion)next.IdEntityDic[sentinel.Id]; Kill(next, s);
            Ok(next.Player1.BoardZone.OfType<Minion>().Count(m => m.AttackDamage == 7) == 1, "clone: the Deathrattle still buffs one minion: " + string.Join(",", next.Player1.BoardZone.OfType<Minion>().Select(m => m.Card.Id + ":" + m.AttackDamage)));
        });
        Kill(g, sentinel);
        var buffed = MinionsOf(p).Where(m => m.AttackDamage == 7 && m.Health == 8).ToArray();
        Ok(buffed.Length == 1 && MinionsOf(p).Count(m => m.AttackDamage == 4) == 2 && enemy.AttackDamage == 4 && enemy.Health == 5,
            "exactly one friendly minion gets +3/+3; the enemy is untouched");
        var results = Across(game => {
            var q = game.Player1; var x = Summon(q, "CS2_182"); var y = Summon(q, "CS2_182"); var z = Summon(q, "CS2_182");
            Kill(game, Summon(q, id));
            return string.Join("", new[] { x, y, z }.Select(m => m.AttackDamage == 7 ? "1" : "0"));
        });
        Ok(results.Count >= 3, "the target varies over seeds: " + string.Join(",", results));
        End(g);
        Ok(buffed[0].AttackDamage == 7, "the buff is permanent");
        Silence(g, buffed[0]);
        Ok(buffed[0].AttackDamage == 4 && buffed[0].Health == 5, "Silence removes the +3/+3");
        // Silenced Sentinel: nothing. Two Sentinels dying together never buff each other.
        ToTurn(g, p); StandardSetsProof.ParkBoard(p);
        var quiet = Summon(p, id); var witness = Summon(p, "CS2_182"); Silence(g, quiet); Kill(g, quiet);
        Ok(witness.AttackDamage == 4, "a silenced Sentinel gives nothing");
        var s1 = Summon(p, id); var s2 = Summon(p, id);
        s1.ToBeDestroyed = true; s2.ToBeDestroyed = true; Settle(g);
        Ok(witness.AttackDamage == 10 && witness.Health == 11 && p.BoardZone.Count == 1, "two Sentinels dying together buff the survivor twice, never each other");
        // The opponent's Sentinel buffs the opponent's minions.
        StandardSetsProof.ParkBoard(p.Opponent);
        var theirs = Summon(p.Opponent, id); var theirFriend = Summon(p.Opponent, "CS2_182");
        Kill(g, theirs);
        Ok(theirFriend.AttackDamage == 7 && witness.AttackDamage == 10, "the opponent's Sentinel buffs the opponent's side: friend " + theirFriend.AttackDamage + " witness " + witness.AttackDamage
            + " board " + string.Join(",", MinionsOf(p.Opponent).Select(m => m.Card.Id + ":" + m.AttackDamage)));
        // A buffed target that leaves play is never chosen.
        StandardSetsProof.ParkBoard(p);
        var lone = Summon(p, "CS2_231"); Kill(g, Summon(p, id));
        Ok(lone.AttackDamage == 4 && lone.Health == 4, "a 1/1 becomes 4/4");
        Summon(p, "LOE_077"); var again = Summon(p, id); Kill(g, again);
        Ok(MinionsOf(p).Sum(m => m.AttackDamage) == 4 + 2 + 3, "Brann does not double the Deathrattle (exactly +3 in total)");
        // Control change: a stolen Sentinel buffs its new controller's minions.
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        var myYeti = Summon(p, "CS2_182"); var enemyYeti = Summon(p.Opponent, "CS2_182"); var stolenSentinel = Summon(p.Opponent, id);
        Steal(g, p, stolenSentinel); Kill(g, stolenSentinel);
        Ok(myYeti.AttackDamage == 7 && enemyYeti.AttackDamage == 4, "a stolen Sentinel buffs the thief's minions");
    }

    private static void FossilizedDevilsaur(Game g, Controller p, string id)
    {
        var alone = (Minion)Play(g, p, id);
        Ok(alone.AttackDamage == 8 && alone.Health == 8 && p.UsedMana == 8 && alone.Card.IsRace(Race.BEAST) && alone.Card.IsRace(Race.UNDEAD),
            "8-mana 8/8 Undead Beast");
        Ok(!alone.HasTaunt, "alone: another Beast is required, itself does not count");
        StandardSetsProof.ParkBoard(p);
        Summon(p, "CS2_182");
        var murloc = (Minion)Play(g, p, id);
        Ok(!murloc.HasTaunt, "a non-Beast friend does not count");
        StandardSetsProof.ParkBoard(p);
        Summon(p.Opponent, "CS2_172");
        var enemyBeast = (Minion)Play(g, p, id);
        Ok(!enemyBeast.HasTaunt, "an enemy Beast does not count");
        StandardSetsProof.ParkBoard(p);
        var beast = Summon(p, "CS2_172");
        var held = Add(p, id);
        Resume(g, next => {
            var np = next.Player1; var played = (Minion)Play(next, np, id);
            Ok(played.HasTaunt, "clone: with a Beast the battlecry gives Taunt");
        });
        PlayEntity(g, p, held);
        var taunting = (Minion)held;
        Ok(taunting.HasTaunt && taunting.AttackDamage == 8, "with another Beast: gains Taunt");
        // Another Devilsaur is itself a Beast (and Undead): the second one taunts, the first one stays as it was.
        StandardSetsProof.ParkBoard(p);
        var first = (Minion)Play(g, p, id); var second = (Minion)Play(g, p, id);
        Ok(!first.HasTaunt && second.HasTaunt, "the second Devilsaur counts the first as another Beast");
        Silence(g, second);
        Ok(!second.HasTaunt, "Silence removes the Taunt");
        // Summoned (no battlecry) gets nothing; the Beast may be buffed or silenced and still counts.
        StandardSetsProof.ParkBoard(p);
        Summon(p, "CS2_172");
        var summoned = Summon(p, id);
        Ok(!summoned.HasTaunt, "summoning does not trigger the battlecry");
        StandardSetsProof.ParkBoard(p);
        var silencedBeast = Summon(p, "CS2_172"); Silence(g, silencedBeast);
        Ok(((Minion)Play(g, p, id)).HasTaunt, "a silenced Beast is still a Beast");
        // Brann changes nothing but does not break it; the opponent's Devilsaur reads the opponent's board.
        StandardSetsProof.ParkBoard(p); Summon(p, "LOE_077"); Summon(p, "CS2_172");
        Ok(((Minion)Play(g, p, id)).HasTaunt, "Brann: still one Taunt");
        ToTurn(g, p.Opponent);
        StandardSetsProof.ParkBoard(p.Opponent); Summon(p.Opponent, "CS2_182"); Summon(p, "CS2_172");
        Ok(!((Minion)Play(g, p.Opponent, id)).HasTaunt, "the opponent's Devilsaur ignores my Beast");
    }

    private static void SirFinley(Game g, Controller p, string id)
    {
        string original = p.Hero.HeroPower.Card.Id;
        Ok(original == "CS2_083b", "fixture hero power is the Rogue one");
        var poolGame = g.CloneExact();
        new DiscoverTask(DiscoverType.BASIC_HEROPOWERS, 100).Process(poolGame, poolGame.Player1, poolGame.Player1.Hero, null);
        var fullPool = poolGame.Player1.Choice.Choices.Select(key => poolGame.IdEntityDic[key].Card.Id).Append(original).ToArray();
        var expectedPool = new[] { "CS2_102", "CS2_049", "CS2_083b", "CS2_101", "DS1h_292", "CS2_017", "CS2_056", "CS1h_001", "CS2_034", "HERO_10bp", "HERO_11bp" };
        Ok(fullPool.Length == 11 && fullPool.Distinct().Count() == 11 && fullPool.ToHashSet().SetEquals(expectedPool)
            && fullPool.Select(power => Cards.FromId(power).Class).Distinct().Count() == 11,
            "full Finley pool: nine classic classes plus DH and DK, no duplicates; held class excluded from offers");
        var finley = (Minion)Play(g, p, id);
        Ok(Alive(finley) && finley.AttackDamage == 1 && finley.Health == 3 && p.UsedMana == 1 && finley.Card.IsRace(Race.MURLOC), "1-mana 1/3 Murloc");
        Ok(p.Choice != null && p.Choice.Choices.Count == 3, "three options");
        var offers = p.Choice.Choices.Select(key => g.IdEntityDic[key].Card.Id).ToArray();
        var offerClasses = offers.Select(o => Cards.FromId(o).Class).ToArray();
        Ok(offers.Distinct().Count() == 3 && offerClasses.Distinct().Count() == 3 && offerClasses.All(c => c != CardClass.ROGUE && c != CardClass.NEUTRAL && c != CardClass.INVALID),
            "three distinct basic hero powers of other classes: " + string.Join(",", offers.Select((o, i) => o + "/" + offerClasses[i])));
        Resume(g, next => {
            var np = next.Player1;
            Ok(np.Choice.Choices.Select(key => next.IdEntityDic[key].Card.Id).SequenceEqual(offers), "pending Discover survives clone and snapshot");
            Pick(next, np, 1);
            Ok(np.Choice == null && np.Hero.HeroPower.Card.Id == offers[1], "clone picks the same hero power");
        });
        Pick(g, p, 2);
        Ok(p.Choice == null && p.Hero.HeroPower.Card.Id == offers[2], "the picked hero power replaces mine");
        Ok(p.Opponent.Hero.HeroPower.Card.Id == "CS2_034" && p.Opponent.Choice == null, "the opponent keeps theirs");
        string current = p.Hero.HeroPower.Card.Id;
        // A second Finley: "new" means not the one I now hold.
        var second = Across(game => {
            var q = game.Player1; Play(game, q, id); Pick(game, q, 0);
            string held = q.Hero.HeroPower.Card.Id;
            Play(game, q, id);
            var again = q.Choice.Choices.Select(key => game.IdEntityDic[key].Card.Id).ToArray();
            Ok(again.Distinct().Count() == 3 && !again.Contains(held) && again.Select(a => Cards.FromId(a).Class).Distinct().Count() == 3
                && again.All(a => Cards.FromId(a).Class != Cards.FromId(held).Class), "second Finley offers new, distinct basic powers of other classes: held "
                + held + "/" + Cards.FromId(held).Class + " offers " + string.Join(",", again.Select(a => a + "/" + Cards.FromId(a).Class)));
            return string.Join(",", again);
        });
        Ok(second.Count >= 2, "the options vary over seeds");
        // Summoned Finley does nothing; the hero power still works after the swap.
        StandardSetsProof.ParkBoard(p);
        Summon(p, id);
        Ok(p.Choice == null && p.Hero.HeroPower.Card.Id == current, "summoned Finley has no battlecry");
        Ok(p.Hero.HeroPower.Card.Id != "CS2_083b", "hero power changed");
        // Brann: two Discovers.
        Summon(p, "LOE_077");
        Play(g, p, id); Pick(g, p, 0);
        Ok(p.Choice != null, "Brann: second Discover");
        Pick(g, p, 0);
        Ok(p.Choice == null, "Brann: both resolved");
        // The opponent can Finley too, from their own power.
        ToTurn(g, p.Opponent);
        Play(g, p.Opponent, id);
        var theirOffers = p.Opponent.Choice.Choices.Select(key => g.IdEntityDic[key].Card.Id).ToArray();
        Ok(theirOffers.Distinct().Count() == 3 && theirOffers.All(o => Cards.FromId(o).Class != CardClass.MAGE), "the opponent's options exclude their own class's power");
        Pick(g, p.Opponent, 0);
        Ok(p.Opponent.Hero.HeroPower.Card.Id != "CS2_034" && p.Choice == null, "opponent swapped; mine untouched");
    }

    private static void BrannBronzebeard(Game g, Controller p, string id)
    {
        var brann = (Minion)Play(g, p, id);
        Ok(Alive(brann) && brann.AttackDamage == 2 && brann.Health == 4 && p.UsedMana == 3, "3-mana 2/4");
        EmptyHand(p); int hand = p.HandZone.Count;
        Play(g, p, "EX1_015");
        Ok(p.HandZone.Count == hand + 2, "a battlecry draw happens twice");
        // Deathrattles and spells are not doubled; summoned minions have no battlecry.
        EmptyHand(p); var hoarder = Summon(p, "EX1_096"); hand = p.HandZone.Count; Kill(g, hoarder);
        Ok(p.HandZone.Count == hand + 1, "a Deathrattle is not doubled");
        hand = p.HandZone.Count; Summon(p, "EX1_015");
        Ok(p.HandZone.Count == hand, "a summoned minion does not trigger its battlecry");
        EmptyHand(p); hand = p.HandZone.Count; Play(g, p, "CS2_023");
        Ok(p.HandZone.Count == hand + 2, "Arcane Intellect (a spell) is not doubled");
        // A second Brann does not make it four.
        var second = Summon(p, id);
        EmptyHand(p); hand = p.HandZone.Count; Play(g, p, "EX1_015");
        Ok(p.HandZone.Count == hand + 2, "two Branns still double only once");
        Resume(g, next => {
            var np = next.Player1; EmptyHand(np); int h = np.HandZone.Count; Play(next, np, "EX1_015");
            Ok(np.HandZone.Count == h + 2, "the aura survives clone and snapshot");
        });
        Kill(g, second);
        EmptyHand(p); hand = p.HandZone.Count; Play(g, p, "EX1_015");
        Ok(p.HandZone.Count == hand + 2, "one Brann left still doubles");
        Silence(g, brann);
        EmptyHand(p); hand = p.HandZone.Count; Play(g, p, "EX1_015");
        Ok(p.HandZone.Count == hand + 1, "Silence ends the doubling");
        // The opponent's Brann does not help me; control change moves the effect.
        StandardSetsProof.ParkBoard(p);
        var theirs = Summon(p.Opponent, id);
        EmptyHand(p); hand = p.HandZone.Count; Play(g, p, "EX1_015");
        Ok(p.HandZone.Count == hand + 1, "the enemy's Brann does not double my battlecry");
        Steal(g, p, theirs);
        EmptyHand(p); hand = p.HandZone.Count; Play(g, p, "EX1_015");
        Ok(p.HandZone.Count == hand + 2, "a stolen Brann doubles for its new controller");
        ToTurn(g, p.Opponent);
        EmptyHand(p.Opponent); hand = p.Opponent.HandZone.Count; Play(g, p.Opponent, "EX1_015");
        Ok(p.Opponent.HandZone.Count == hand + 1, "and no longer for the old one");
        // Playing Brann with Brann on board: Brann has no battlecry of its own.
        ToTurn(g, p);
        Play(g, p, id);
        Ok(p.Choice == null, "Brann has no battlecry to double");
        // In hand it does nothing.
        StandardSetsProof.ParkBoard(p);
        EmptyHand(p); Add(p, id); hand = p.HandZone.Count; Play(g, p, "EX1_015");
        Ok(p.HandZone.Count == hand + 1, "Brann in hand does not double");
    }

    private static void EliseStarseeker(Game g, Controller p, string id)
    {
        int deck = p.DeckZone.Count, opponentDeck = p.Opponent.DeckZone.Count;
        var elise = (Minion)Play(g, p, id);
        Ok(Alive(elise) && elise.AttackDamage == 3 && elise.Health == 5 && p.UsedMana == 4, "4-mana 3/5");
        Ok(p.DeckZone.Count == deck + 1 && p.DeckZone.Count(c => c.Card.Id == "LOE_019t") == 1 && p.Opponent.DeckZone.Count == opponentDeck && p.HandZone.Count == 0,
            "exactly one Map shuffled into my deck");
        HiddenSame(g, game => game.Player1.DeckZone.First(c => c.Card.Id == "LOE_019t"), "CS2_182", 2, "shuffled Map");
        var map = p.DeckZone.First(c => c.Card.Id == "LOE_019t");
        Ok(map.Card.Type == CardType.SPELL && map.Card.Cost == 2, "the Map is a 2-cost spell");
        Resume(g, next => {
            var np = next.Player1; var m = np.DeckZone.First(c => c.Card.Id == "LOE_019t");
            Generic.Draw(np, (Playable)m); PlayEntity(next, np, m);
            Ok(np.DeckZone.Any(c => c.Card.Id == "LOE_019t2"), "clone: the Map shuffles the Golden Monkey in");
        });
        Generic.Draw(p, (Playable)map);
        Ok(p.HandZone.Contains(map), "the Map is an ordinary draw (not cast when drawn)");
        int deckBefore = p.DeckZone.Count, handBefore = p.HandZone.Count;
        PlayEntity(g, p, map);
        Ok(p.DeckZone.Count(c => c.Card.Id == "LOE_019t2") == 1 && p.DeckZone.Count == deckBefore, "the Map shuffles the Golden Monkey in and draws a card");
        Ok(p.HandZone.Count == handBefore, "the Map left the hand and a card came in");
        HiddenSame(g, game => game.Player1.DeckZone.First(c => c.Card.Id == "LOE_019t2"), "CS2_182", 2, "shuffled Golden Monkey");
        // The Golden Monkey is a real card: 6/6 Taunt that replaces hand and deck with Legendary minions.
        var monkey = p.DeckZone.First(c => c.Card.Id == "LOE_019t2");
        Generic.Draw(p, (Playable)monkey);
        var played = (Minion)monkey;
        PlayEntity(g, p, monkey);
        Ok(played.AttackDamage == 6 && played.Health == 6 && played.HasTaunt && p.DeckZone.All(c => c.Card.Rarity == Rarity.LEGENDARY),
            "Golden Monkey is a 6/6 Taunt that turns the deck Legendary");
        // Brann doubles Elise; the opponent's Elise shuffles into the opponent's deck only.
        Summon(p, "LOE_077"); deck = p.DeckZone.Count;
        Play(g, p, id);
        Ok(p.DeckZone.Count == deck + 2 && p.DeckZone.Count(c => c.Card.Id == "LOE_019t") == 2, "Brann: two Maps");
        ToTurn(g, p.Opponent);
        int mine = p.DeckZone.Count; Play(g, p.Opponent, id);
        Ok(p.DeckZone.Count == mine && p.Opponent.DeckZone.Count(c => c.Card.Id == "LOE_019t") == 1, "the opponent's Elise fills the opponent's deck");
        HiddenSame(g, game => game.Player2.DeckZone.First(c => c.Card.Id == "LOE_019t"), "CS2_182", 1, "the opponent's shuffled Map");
        // Summoned Elise has no battlecry; a deck at its limit just refuses the card.
        ToTurn(g, p); mine = p.DeckZone.Count; Summon(p, id);
        Ok(p.DeckZone.Count == mine, "summoned Elise does nothing");
        SetDeck(p, Enumerable.Repeat("CS2_182", p.DeckZone.MaxSize).ToArray());
        StandardSetsProof.ParkBoard(p);
        Play(g, p, id);
        Ok(p.DeckZone.Count == p.DeckZone.MaxSize && g.CurrentPlayer == p, "a full deck does not overflow and nothing stalls: deck " + p.DeckZone.Count);
        Silence(g, elise);
        Ok(p.DeckZone.Count == p.DeckZone.MaxSize, "Silence on Elise leaves the deck as it is");
    }

    private static void SummoningStone(Game g, Controller p, string id)
    {
        var stone = (Minion)Play(g, p, id);
        Ok(Alive(stone) && stone.AttackDamage == 0 && stone.Health == 6 && p.UsedMana == 5, "5-mana 0/6");
        p.Opponent.Hero.Damage = 0; Play(g, p, "CS2_029", p.Opponent.Hero);
        var summoned = MinionsOf(p).Where(m => m != stone).ToArray();
        Ok(summoned.Length == 1 && summoned[0].Card.Cost == 4 && summoned[0].Card.Collectible, "a 4-Cost spell summons a random 4-Cost minion");
        Ok(p.Opponent.BoardZone.Count == 0, "on my side only");
        Resume(g, next => {
            var np = next.Player1; np.Opponent.Hero.Damage = 0; Play(next, np, "CS2_029", np.Opponent.Hero);
            Ok(MinionsOf(np).Count(m => m.Card.Cost == 4) >= 1 && np.BoardZone.Count == 3, "clone: the trigger still fires");
        });
        var results = Across(game => {
            var q = game.Player1; Summon(q, id); Play(game, q, "CS2_029", q.Opponent.Hero);
            return q.BoardZone[1].Card.Id;
        });
        Ok(results.Count >= 3, "the summoned minion varies over seeds: " + string.Join(",", results));
        // Other costs: a 0-cost spell, a 1-cost spell, and a discounted spell (Preparation makes Fireball cost 1).
        StandardSetsProof.ParkBoard(p); Summon(p, id);
        Play(g, p, "CS2_008", p.Opponent.Hero);
        Ok(MinionsOf(p).Length == 2 && MinionsOf(p).Where(m => m.Card.Id != id).All(m => m.Card.Cost == 0), "Moonfire (0 mana) summons a 0-Cost minion");
        StandardSetsProof.ParkBoard(p); Summon(p, id);
        Play(g, p, "CS2_005");
        Ok(MinionsOf(p).Length == 2 && MinionsOf(p).Where(m => m.Card.Id != id).All(m => m.Card.Cost == 1), "Claw (1 mana) summons a 1-Cost minion");
        StandardSetsProof.ParkBoard(p); Summon(p, id);
        PlayEntity(g, p, Add(p, "EX1_145"));
        var discounted = Add(p, "CS2_029"); g.AuraUpdate();
        int paid = discounted.Cost;
        Ok(paid < 4, "Preparation lowers Fireball: cost " + paid);
        PlayEntity(g, p, discounted, p.Opponent.Hero);
        Ok(MinionsOf(p).Length == 3 && MinionsOf(p).Last().Card.Cost == paid, "a discounted spell summons by the cost it was cast for (" + paid + "): "
            + string.Join(",", MinionsOf(p).Select(m => m.Card.Id + "/" + m.Card.Cost)));
        // Full board: nothing happens.
        StandardSetsProof.ParkBoard(p); Summon(p, id); FillBoard(p);
        p.Opponent.Hero.Damage = 0; Play(g, p, "CS2_029", p.Opponent.Hero);
        Ok(p.BoardZone.Count == 7, "full board: no summon, no error");
        // Opponent spells and minions (not spells) do not trigger it.
        StandardSetsProof.ParkBoard(p); var s2 = Summon(p, id);
        Play(g, p, "CS2_182");
        Ok(p.BoardZone.Count == 2, "a minion play does not trigger it");
        ToTurn(g, p.Opponent);
        Play(g, p.Opponent, "CS2_029", p.Opponent.Hero);
        Ok(p.BoardZone.Count == 2, "the opponent's spell does not trigger it");
        ToTurn(g, p);
        // Silence and death stop it; two Stones summon two minions.
        Silence(g, s2);
        p.Opponent.Hero.Damage = 0; Play(g, p, "CS2_029", p.Opponent.Hero);
        Ok(p.BoardZone.Count == 2, "a silenced Stone does nothing");
        StandardSetsProof.ParkBoard(p); Summon(p, id); Summon(p, id);
        p.Opponent.Hero.Damage = 0; Play(g, p, "CS2_029", p.Opponent.Hero);
        Ok(p.BoardZone.Count == 4, "two Stones: two summons: " + string.Join(",", MinionsOf(p).Select(m => m.Card.Id)));
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        var taken = Summon(p.Opponent, id); Steal(g, p, taken);
        p.Opponent.Hero.Damage = 0; Play(g, p, "CS2_029", p.Opponent.Hero);
        Ok(p.BoardZone.Count == 2 && p.Opponent.BoardZone.Count == 0, "a stolen Stone summons for its new controller");
    }

    private static void WobblingRunts(Game g, Controller p, string id)
    {
        var runts = (Minion)Play(g, p, id);
        Ok(runts.AttackDamage == 2 && runts.Health == 6 && p.UsedMana == 6, "6-mana 2/6");
        Ok(p.BoardZone.Count == 1, "playing it summons nothing (no battlecry): board " + string.Join(",", p.BoardZone.OfType<Minion>().Select(m => m.Card.Id)));
        Resume(g, next => {
            var r = (Minion)next.IdEntityDic[runts.Id]; Kill(next, r);
            Ok(next.Player1.BoardZone.Count == 3, "clone: the Deathrattle still summons three");
        });
        var left = Summon(p, "CS2_182"); Kill(g, runts);
        // runts was at slot 0, left is at slot 1: the Runts appear in the dead minion's place, in order.
        Ok(p.BoardZone.Count == 4 && p.BoardZone.OfType<Minion>().Take(3).Select(m => m.Card.Id).SequenceEqual(new[] { "LOE_089t", "LOE_089t2", "LOE_089t3" }),
            "three distinct Runts in order: " + string.Join(",", p.BoardZone.OfType<Minion>().Select(m => m.Card.Id)));
        Ok(p.BoardZone.OfType<Minion>().Take(3).All(m => m.AttackDamage == 2 && m.Health == 2) && p.BoardZone[3] == left, "each Runt is a 2/2, beside the neighbour");
        // Boundaries: a board with only two free slots, no slots, and the opponent's side.
        StandardSetsProof.ParkBoard(p);
        var dying = Summon(p, id); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        Kill(g, dying);
        Ok(p.BoardZone.Count == 7 && p.BoardZone.OfType<Minion>().Count(m => m.Card.Id.StartsWith("LOE_089t")) == 2, "five others leave room for two Runts only");
        StandardSetsProof.ParkBoard(p);
        var packed = Summon(p, id); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        Kill(g, packed);
        Ok(p.BoardZone.Count == 7 && p.BoardZone.OfType<Minion>().Count(m => m.Card.Id.StartsWith("LOE_089t")) == 1, "one free slot: one Runt");
        StandardSetsProof.ParkBoard(p);
        var silenced = Summon(p, id); Silence(g, silenced); Kill(g, silenced);
        Ok(p.BoardZone.Count == 0, "Silence removes the Deathrattle");
        var theirs = Summon(p.Opponent, id); Kill(g, theirs);
        Ok(p.Opponent.BoardZone.Count == 3 && p.BoardZone.Count == 0, "the opponent's Runts appear on the opponent's side");
        // Two together, Brann does not double.
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        var one = Summon(p, id); var two = Summon(p, id); one.ToBeDestroyed = true; two.ToBeDestroyed = true; Settle(g);
        Ok(p.BoardZone.Count == 6, "two Wobbling Runts: six Runts");
        StandardSetsProof.ParkBoard(p); Summon(p, "LOE_077"); var braved = Summon(p, id); Kill(g, braved);
        Ok(p.BoardZone.Count == 4, "Brann does not double the Deathrattle");
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        var taken = Summon(p.Opponent, id); Steal(g, p, taken); Kill(g, taken);
        Ok(p.BoardZone.Count == 3 && p.Opponent.BoardZone.Count == 0, "a stolen Wobbling Runts summons the Runts for its new controller");
    }

    private static void ArchThiefRafaam(Game g, Controller p, string id)
    {
        var rafaam = (Minion)Play(g, p, id);
        Ok(Alive(rafaam) && rafaam.AttackDamage == 7 && rafaam.Health == 8 && p.UsedMana == 9, "9-mana 7/8");
        Ok(p.Choice != null && p.Choice.Choices.Count == 3, "three artifacts offered");
        var offers = p.Choice.Choices.Select(key => g.IdEntityDic[key].Card.Id).ToArray();
        Ok(offers.OrderBy(x => x).SequenceEqual(new[] { "LOEA16_3", "LOEA16_4", "LOEA16_5" }), "the three artifacts: " + string.Join(",", offers));
        Resume(g, next => {
            var np = next.Player1;
            Ok(np.Choice.Choices.Select(key => next.IdEntityDic[key].Card.Id).SequenceEqual(offers), "pending Discover survives clone and snapshot");
            Pick(next, np, 0);
            Ok(np.HandZone[np.HandZone.Count - 1].Card.Id == offers[0], "clone picks the same artifact");
        });
        int hand = p.HandZone.Count;
        Pick(g, p, 1);
        var got = p.HandZone[p.HandZone.Count - 1];
        Ok(got.Card.Id == offers[1] && p.HandZone.Count == hand + 1 && got.Cost == 10 && p.Choice == null && p.Opponent.HandZone.Count == 0, "the picked artifact (10 mana) enters my hand only");
        HiddenSame(g, game => game.Player1.HandZone[game.Player1.HandZone.Count - 1], "CS2_182", 2, "discovered artifact");
        Summon(p, id);
        Ok(p.Choice == null, "summoning does not Discover");
        // The artifacts themselves.
        // Lantern of Power: +10/+10 on any minion.
        var target = Summon(p.Opponent, "CS2_182"); var mine = Summon(p, "CS2_231");
        p.UsedMana = 0; var lantern = Add(p, "LOEA16_3");
        var lanternPlays = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == lantern.Id).ToArray();
        Ok(lanternPlays.Length > 0 && lanternPlays.All(t => t.Target is Minion) && lanternPlays.Any(t => t.Target.Id == target.Id), "Lantern targets minions only, enemy minions included");
        PlayEntity(g, p, lantern, mine);
        Ok(mine.AttackDamage == 11 && mine.Health == 11, "Lantern gives +10/+10");
        // Timepiece of Horror: 10 missiles at enemies only.
        StandardSetsProof.ParkBoard(p.Opponent);
        var y1 = Summon(p.Opponent, "CS2_182"); var y2 = Summon(p.Opponent, "CS2_182"); var y3 = Summon(p.Opponent, "CS2_182");
        Play(g, p, "LOEA16_4");
        int total = p.Opponent.Hero.Damage + new[] { y1, y2, y3 }.Sum(m => m.Damage);
        Ok(total == 10 && p.Hero.Damage == 0 && mine.Damage == 0, "Timepiece deals exactly 10 split among enemies only");
        StandardSetsProof.ParkBoard(p.Opponent); p.Opponent.Hero.Damage = 0;
        var z1 = Summon(p.Opponent, "CS2_182"); var z2 = Summon(p.Opponent, "CS2_182"); var spellPower = Summon(p, "EX1_012");
        Play(g, p, "LOEA16_4");
        Ok(p.Opponent.Hero.Damage + z1.Damage + z2.Damage == 11, "Spell Damage +1 adds a missile: 11");
        Kill(g, spellPower);
        Ok(Across(game => {
            var q = game.Player1; var a = Summon(q.Opponent, "CS2_182"); var b = Summon(q.Opponent, "CS2_182");
            Play(game, q, "LOEA16_4"); return a.Damage + "/" + b.Damage;
        }).Count >= 3, "the split varies over seeds");
        // Mirror of Doom: fill the board with 3/3 Mummy Zombies.
        StandardSetsProof.ParkBoard(p);
        Summon(p, "CS2_231"); Summon(p, "CS2_231");
        Play(g, p, "LOEA16_5");
        var zombies = p.BoardZone.OfType<Minion>().Where(m => m.Card.Id == "LOEA16_5t").ToArray();
        Ok(p.BoardZone.IsFull && zombies.Length == 5 && zombies.All(z => z.AttackDamage == 3 && z.Health == 3 && z.Card.IsRace(Race.UNDEAD)),
            "Mirror fills the board with 3/3 Undead zombies: " + string.Join(",", p.BoardZone.OfType<Minion>().Select(m => m.Card.Id + ":" + m.AttackDamage + "/" + m.Health)));
        Ok(MinionsOf(p.Opponent).All(m => m.Card.Id != "LOEA16_5t"), "the opponent's board gets no zombies");
        StandardSetsProof.ParkBoard(p);
        Play(g, p, "LOEA16_5");
        Ok(p.BoardZone.IsFull && MinionsOf(p).Count(m => m.Card.Id == "LOEA16_5t") == 7, "an empty board gets seven zombies");
        Play(g, p, "LOEA16_5");
        Ok(p.BoardZone.IsFull && g.CurrentPlayer == p, "a full board: the Mirror still resolves and nothing breaks");
        // A full hand burns the pick; nothing stalls.
        StandardSetsProof.ParkBoard(p);
        Play(g, p, id); FillHand(p);
        Pick(g, p, 0);
        Ok(p.Choice == null && p.HandZone.IsFull && g.CurrentPlayer == p, "pick with a full hand is burned and nothing stalls");
        // The opponent's Rafaam offers the opponent artifacts.
        ToTurn(g, p.Opponent);
        Play(g, p.Opponent, id);
        Ok(p.Opponent.Choice != null && p.Choice == null, "opponent Discovers; I do not");
        Pick(g, p.Opponent, 0);
        Ok(p.Opponent.Choice == null, "opponent pick resolves");
    }

    private static void Entomb(Game g, Controller p, string id)
    {
        var held = Add(p, id);
        Ok(!CanPlay(p, held), "no enemy minion: not playable");
        var friend = Summon(p, "CS2_182");
        Ok(!CanPlay(p, held), "a friendly minion does not make it playable");
        var enemy = Summon(p.Opponent, "CS2_200");
        Ok(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id) && p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).All(t => t.Target?.Id == enemy.Id), "only enemy minions are targets");
        int deck = p.DeckZone.Count;
        Effects.Attack_N(3).ApplyTo(enemy); g.DeathProcessingAndAuraUpdate();
        PlayEntity(g, p, held, enemy);
        Ok(p.UsedMana == 6 && p.Opponent.BoardZone.Count == 0 && enemy.Zone?.Type != Zone.PLAY, "the enemy minion leaves the board");
        Ok(p.DeckZone.Count == deck + 1 && p.DeckZone.Count(c => c.Card.Id == "CS2_200") == 1, "a Boulderfist Ogre copy is shuffled into my deck");
        var shuffled = p.DeckZone.First(c => c.Card.Id == "CS2_200");
        Ok(shuffled.Controller == p && ((Minion)shuffled).AttackDamage == 6 && ((Minion)shuffled).Health == 7, "it is mine and is a fresh card (no buffs)");
        HiddenSame(g, game => game.Player1.DeckZone.First(c => c.Card.Id == "CS2_200"), "CS2_182", 2, "entombed minion");
        Resume(g, next => {
            var np = next.Player1; var c = np.DeckZone.First(x => x.Card.Id == "CS2_200");
            Generic.Draw(np, (Playable)c);
            Ok(np.HandZone.Contains(c) && np.Opponent.BoardZone.Count == 0, "clone: drawn like any card of mine");
        });
        Generic.Draw(p, (Playable)shuffled);
        StandardSetsProof.ParkBoard(p);
        PlayEntity(g, p, shuffled);
        Ok(((Minion)shuffled).Controller == p && ((Minion)shuffled).AttackDamage == 6 && p.BoardZone.Count == 1, "I can play it as my own minion");
        // Deathrattle does not trigger on the shuffle; the dying-by-removal minion is not 'dead'.
        var ratter = Summon(p.Opponent, "EX1_096");
        int graveyard = p.Opponent.GraveyardZone.Count; int hand = p.Opponent.HandZone.Count;
        Play(g, p, id, ratter);
        Ok(p.Opponent.GraveyardZone.Count == graveyard && p.Opponent.BoardZone.Count == 0, "no death occurs");
        // An enemy that cannot be targeted is not offered; a damaged enemy comes back healthy.
        var stealth = Summon(p.Opponent, "EX1_522"); // Patient Assassin: Stealth
        var wounded = Summon(p.Opponent, "CS2_200"); wounded.Damage = 4;
        var again = Add(p, id);
        Ok(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == again.Id && t.Target?.Id == stealth.Id), "a Stealthed enemy is not targetable");
        PlayEntity(g, p, again, wounded);
        var fresh = p.DeckZone.Last(c => c.Card.Id == "CS2_200");
        Ok(((Minion)fresh).Damage == 0, "the shuffled copy is undamaged");
        // The opponent entombs mine into their deck.
        ToTurn(g, p.Opponent);
        var theirs = Add(p.Opponent, id);
        StandardSetsProof.ParkBoard(p);
        var mineOnBoard = Summon(p, "CS2_231");
        int oppDeck = p.Opponent.DeckZone.Count, myDeck = p.DeckZone.Count;
        PlayEntity(g, p.Opponent, theirs, mineOnBoard);
        Ok(p.BoardZone.Count == 0 && p.Opponent.DeckZone.Count == oppDeck + 1 && p.Opponent.DeckZone.Count(c => c.Card.Id == "CS2_231") >= 1 && p.DeckZone.Count == myDeck,
            "the opponent entombs my minion into their own deck: board " + p.BoardZone.Count + " oppDeck " + p.Opponent.DeckZone.Count + "/" + oppDeck);
        HiddenSame(g, game => game.Player2.DeckZone.First(c => c.Card.Id == "CS2_231" && c.Controller == game.Player2 && game.Player2.DeckZone.Count(x => x.Card.Id == "CS2_231") >= 1), "CS2_182", 1, "the opponent's entombed minion");
    }

    private static void ExplorersHat(Game g, Controller p, string id)
    {
        var held = Add(p, id);
        Ok(held.Cost == 1 && !CanPlay(p, held), "costs 1; needs a minion target");
        var minion = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "CS2_182");
        var targets = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).Select(t => t.Target?.Id).ToArray();
        Ok(targets.Contains(minion.Id) && targets.Contains(enemy.Id) && !targets.Contains(p.Hero.Id) && !targets.Contains(p.Opponent.Hero.Id), "any minion, never a hero");
        PlayEntity(g, p, held, minion);
        Ok(minion.AttackDamage == 5 && minion.Health == 6 && p.UsedMana == 1, "+1/+1 for 1 mana");
        int hand = p.HandZone.Count;
        Resume(g, next => {
            var m = (Minion)next.IdEntityDic[minion.Id]; Kill(next, m);
            Ok(next.Player1.HandZone.Count(c => c.Card.Id == id) == 1, "clone: the Deathrattle still gives a Hat");
        });
        Kill(g, minion);
        Ok(p.HandZone.Count == hand + 1 && p.HandZone.Count(c => c.Card.Id == id) == 1 && p.HandZone[p.HandZone.Count - 1].Cost == 1, "dying gives a new Explorer's Hat");
        // The Hat chain: play the new Hat, the next death gives another.
        var again = Summon(p, "CS2_182");
        PlayEntity(g, p, p.HandZone.First(c => c.Card.Id == id), again);
        Kill(g, again);
        Ok(p.HandZone.Count(c => c.Card.Id == id) == 1, "the chain continues");
        // Stacked Hats give two Hats; Silence removes buff and Deathrattle.
        var stacked = Summon(p, "CS2_182");
        foreach (var hat in p.HandZone.Where(c => c.Card.Id == id).ToArray()) p.HandZone.Remove(hat);
        PlayEntity(g, p, Add(p, id), stacked); PlayEntity(g, p, Add(p, id), stacked);
        Ok(stacked.AttackDamage == 6 && stacked.Health == 7, "two Hats: +2/+2");
        Kill(g, stacked);
        Ok(p.HandZone.Count(c => c.Card.Id == id) == 2, "two Hats, two Deathrattles");
        foreach (var hat in p.HandZone.Where(c => c.Card.Id == id).ToArray()) p.HandZone.Remove(hat);
        var quiet = Summon(p, "CS2_182"); PlayEntity(g, p, Add(p, id), quiet); Silence(g, quiet);
        Ok(quiet.AttackDamage == 4 && quiet.Health == 5, "Silence removes the +1/+1");
        Kill(g, quiet);
        Ok(p.HandZone.All(c => c.Card.Id != id), "a silenced minion gives no Hat");
        // A full hand burns the Hat; nothing stalls.
        var full = Summon(p, "CS2_182"); PlayEntity(g, p, Add(p, id), full); FillHand(p);
        Kill(g, full);
        Ok(p.HandZone.Count == 10 && g.CurrentPlayer == p, "a full hand: the Hat is burned");
        // The Deathrattle belongs to whoever controls the minion when it dies.
        ToTurn(g, p);
        var target = Summon(p.Opponent, "CS2_182");
        EmptyHand(p); var fresh = Add(p, id);
        PlayEntity(g, p, fresh, target);
        Ok(target.AttackDamage == 5, "an enemy minion can be buffed");
        int theirHand = p.Opponent.HandZone.Count; int myHand = p.HandZone.Count;
        Kill(g, target);
        Ok(p.Opponent.HandZone.Count == theirHand + 1 && p.HandZone.Count == myHand, "the Hat goes to the dying minion's controller");
    }

    private static void EerieStatue(Game g, Controller p, string id)
    {
        var statue = (Minion)Play(g, p, id);
        Ok(statue.AttackDamage == 7 && statue.Health == 7 && p.UsedMana == 4, "4-mana 7/7");
        statue.IsExhausted = false;
        Ok(statue.CanAttack, "alone on the battlefield: it can attack the hero");
        var enemy = Summon(p.Opponent, "CS2_182");
        Ok(!statue.CanAttack && !p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == statue.Id),
            "an enemy minion on the battlefield stops it");
        Kill(g, enemy);
        statue.IsExhausted = false;
        Ok(statue.CanAttack, "the enemy minion died: it can attack again");
        var friend = Summon(p, "CS2_182");
        Ok(!statue.CanAttack, "a friendly minion also stops it");
        Resume(g, next => {
            var s = (Minion)next.IdEntityDic[statue.Id]; s.IsExhausted = false;
            Ok(!s.CanAttack, "clone: still cannot attack");
            Kill(next, (Minion)next.IdEntityDic[friend.Id]); s.IsExhausted = false;
            Ok(s.CanAttack, "clone: attacks when it is alone again");
        });
        Silence(g, statue);
        statue.IsExhausted = false;
        Ok(statue.CanAttack, "Silence removes the restriction");
        // The restriction follows the board: a second Statue stops the first; a lone Statue attacks and deals damage.
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        var s1 = Summon(p, id); s1.IsExhausted = false;
        Attack(g, s1, p.Opponent.Hero);
        Ok(p.Opponent.Hero.Damage == 7, "alone, the Statue hits for 7");
        End(g); End(g);
        var s2 = Summon(p, id); s1.IsExhausted = false; s2.IsExhausted = false;
        Ok(!s1.CanAttack && !s2.CanAttack, "two Statues stop each other");
        // The opponent's Statue is stopped by my minions in the same way.
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        var theirs = Summon(p.Opponent, id); ToTurn(g, p.Opponent); theirs.IsExhausted = false;
        Ok(theirs.CanAttack, "their Statue alone can attack");
        Summon(p, "CS2_231");
        Ok(!theirs.CanAttack, "my minion stops their Statue");
    }

    private static void AncientShade(Game g, Controller p, string id)
    {
        int deck = p.DeckZone.Count, theirDeck = p.Opponent.DeckZone.Count;
        var shade = (Minion)Play(g, p, id);
        Ok(Alive(shade) && shade.AttackDamage == 7 && shade.Health == 4 && p.UsedMana == 4 && shade.Card.IsRace(Race.UNDEAD), "4-mana 7/4 Undead");
        Ok(p.DeckZone.Count == deck + 1 && p.DeckZone.Count(c => c.Card.Id == "LOE_110t") == 1 && p.Opponent.DeckZone.Count == theirDeck,
            "an Ancient Curse is shuffled into my deck only");
        HiddenSame(g, game => game.Player1.DeckZone.First(c => c.Card.Id == "LOE_110t"), "CS2_182", 2, "shuffled Ancient Curse");
        var curse = p.DeckZone.First(c => c.Card.Id == "LOE_110t");
        Resume(g, next => {
            var np = next.Player1; var c = np.DeckZone.First(x => x.Card.Id == "LOE_110t");
            Generic.Draw(np, (Playable)c);
            Ok(np.Hero.Damage == 7, "clone: drawing the curse hurts for 7");
        });
        // Drawing it: 7 damage to me, the card is gone and a replacement card is drawn instead.
        SetDeck(p, "CS2_182", "LOE_110t", "CS2_231"); p.Hero.Damage = 0; p.Hero.Fatigue = 0;
        EmptyHand(p); int hand = p.HandZone.Count;
        Generic.Draw(p, (Playable)p.DeckZone.First(c => c.Card.Id == "LOE_110t"));
        Ok(p.Hero.Damage == 7 && p.HandZone.All(c => c.Card.Id != "LOE_110t") && p.HandZone.Count == hand + 1 && p.DeckZone.Count == 1,
            "the curse is cast on draw: 7 damage, no card in hand, a replacement draw follows: damage " + p.Hero.Damage + " hand " + p.HandZone.Count + " deck " + p.DeckZone.Count);
        // Spell Damage does not change it; the opponent's hero is not hurt.
        Summon(p, "EX1_012");
        p.Hero.Damage = 0; SetDeck(p, "LOE_110t", "CS2_182");
        Generic.Draw(p, (Playable)p.DeckZone.First(c => c.Card.Id == "LOE_110t"));
        Ok(p.Hero.Damage == 7 && p.Opponent.Hero.Damage == 0, "7 damage regardless of Spell Damage, and only to me");
        // The curse is the only card: the replacement draw hits fatigue.
        p.Hero.Damage = 0; SetDeck(p, "LOE_110t");
        Generic.Draw(p, (Playable)p.DeckZone.First(c => c.Card.Id == "LOE_110t"));
        Ok(p.Hero.Damage == 7 + 1 && p.HandZone.All(c => c.Card.Id != "LOE_110t"), "with no card behind it, the replacement draw is fatigue: " + p.Hero.Damage);
        // Armor absorbs part of the 7.
        StandardSetsProof.ParkBoard(p);
        p.Hero.Damage = 0; p.Hero.Armor = 3; SetDeck(p, "LOE_110t", "CS2_182", "CS2_182");
        Generic.Draw(p, (Playable)p.DeckZone.First(c => c.Card.Id == "LOE_110t"));
        Ok(p.Hero.Armor == 0 && p.Hero.Damage == 4, "armor absorbs part of the 7");
        // Brann: two curses; summoned Shade does nothing; the opponent's Shade curses the opponent's deck.
        StandardSetsProof.ParkBoard(p); Summon(p, "LOE_077"); SetDeck(p, "CS2_182", "CS2_182"); deck = p.DeckZone.Count;
        Play(g, p, id);
        Ok(p.DeckZone.Count(c => c.Card.Id == "LOE_110t") == 2 && p.DeckZone.Count == deck + 2, "Brann: two curses");
        Summon(p, id);
        Ok(p.DeckZone.Count == deck + 2, "summoned Shade has no battlecry");
        ToTurn(g, p.Opponent);
        Play(g, p.Opponent, id);
        Ok(p.Opponent.DeckZone.Count(c => c.Card.Id == "LOE_110t") == 1 && p.DeckZone.Count == deck + 2, "the opponent's Shade curses the opponent's deck");
        HiddenSame(g, game => game.Player2.DeckZone.First(c => c.Card.Id == "LOE_110t"), "CS2_182", 1, "the opponent's Ancient Curse");
    }

    private static void ExcavatedEvil(Game g, Controller p, string id)
    {
        var mine = Summon(p, "CS2_182"); var wisp = Summon(p, "CS2_231");
        var theirs = Summon(p.Opponent, "CS2_182"); var their2 = Summon(p.Opponent, "CS2_231");
        int opponentDeck = p.Opponent.DeckZone.Count, myDeck = p.DeckZone.Count;
        Play(g, p, id);
        Ok(p.UsedMana == 5 && mine.Damage == 3 && theirs.Damage == 3 && Dead(wisp) && Dead(their2), "3 damage to all minions on both sides, for 5 mana");
        Ok(p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "heroes untouched");
        Ok(p.Opponent.DeckZone.Count == opponentDeck + 1 && p.Opponent.DeckZone.Count(c => c.Card.Id == id) == 1 && p.DeckZone.Count == myDeck && p.HandZone.Count == 0,
            "the card is shuffled into the opponent's deck, not mine");
        HiddenSame(g, game => game.Player2.DeckZone.First(c => c.Card.Id == id), "CS2_182", 1, "Excavated Evil in the opponent deck");
        Resume(g, next => {
            var np = next.Player2; var c = np.DeckZone.First(x => x.Card.Id == id);
            Generic.Draw(np, (Playable)c);
            Ok(np.HandZone.Contains(c) && c.Card.Id == id, "clone: the opponent draws the card like any other");
        });
        // The opponent draws it, plays it, and it comes back to me.
        var evil = p.Opponent.DeckZone.First(c => c.Card.Id == id);
        Generic.Draw(p.Opponent, (Playable)evil);
        ToTurn(g, p.Opponent);
        int mineDeck = p.DeckZone.Count; var tough = Summon(p, "CS2_182");
        PlayEntity(g, p.Opponent, evil);
        Ok(p.DeckZone.Count == mineDeck + 1 && p.DeckZone.Count(c => c.Card.Id == id) == 1, "their copy shuffles into my deck");
        Ok(tough.Damage == 3 && Dead(mine), "and damages every minion (my 3-damaged Yeti dies)");
        // Spell Damage adds to the 3; an empty board still shuffles; a full opposing deck gets nothing.
        ToTurn(g, p); StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        Summon(p, "EX1_012"); var yeti = Summon(p.Opponent, "CS2_182");
        Play(g, p, id);
        Ok(yeti.Damage == 4, "Spell Damage +1: 4 damage");
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        int before = p.Opponent.DeckZone.Count;
        Play(g, p, id);
        Ok(p.Opponent.DeckZone.Count == before + 1, "an empty board still shuffles");
        SetDeck(p.Opponent, Enumerable.Repeat("CS2_182", p.Opponent.DeckZone.MaxSize).ToArray());
        Play(g, p, id);
        Ok(p.Opponent.DeckZone.Count == p.Opponent.DeckZone.MaxSize && g.CurrentPlayer == p, "a full opposing deck: no overflow, no stall");
    }

    private static void EveryfinIsAwesome(Game g, Controller p, string id)
    {
        var held = Add(p, id);
        Ok(held.Cost == 7, "7 mana with no Murloc");
        var m1 = Summon(p, "CS2_168");
        Ok(held.Cost == 6, "one Murloc: 6");
        Summon(p.Opponent, "CS2_168");
        Ok(held.Cost == 6, "an enemy Murloc does not count");
        var m2 = Summon(p, "CS2_168"); var m3 = Summon(p, "CS2_168");
        var other = Summon(p, "CS2_182");
        Ok(held.Cost == 4, "three Murlocs: 4");
        var enemy = Summon(p.Opponent, "CS2_182");
        PlayEntity(g, p, held);
        Ok(p.UsedMana == 4, "paid 4");
        Ok(m1.AttackDamage == 4 && m1.Health == 3 && other.AttackDamage == 6 && other.Health == 7 && enemy.AttackDamage == 4 && enemy.Health == 5,
            "all my minions get +2/+2, not the enemy's: m1 " + m1.AttackDamage + "/" + m1.Health + " other " + other.AttackDamage + "/" + other.Health + " enemy " + enemy.AttackDamage + "/" + enemy.Health);
        Kill(g, m1);
        var next = Add(p, id);
        Ok(next.Cost == 5, "a Murloc that died no longer counts: 5");
        StandardSetsProof.ParkBoard(p);
        Ok(next.Cost == 7, "no Murlocs again: 7");
        Summon(p, "CS2_168"); Summon(p, "CS2_168"); Summon(p, "CS2_168"); Summon(p, "CS2_168"); Summon(p, "CS2_168"); Summon(p, "CS2_168"); Summon(p, "CS2_168");
        Ok(next.Cost == 0, "seven Murlocs: costs 0, never negative");
        Resume(g, nextGame => {
            var c = nextGame.Player1.HandZone.First(x => x.Card.Id == id);
            Ok(c.Cost == 0, "clone keeps the dynamic cost");
        });
        PlayEntity(g, p, next);
        Ok(p.BoardZone.OfType<Minion>().All(m => m.AttackDamage == 4), "it still buffs at cost 0");
        // No minions: playable, nothing to buff. A Murloc in hand does not count. Silence a Murloc does not change the race.
        StandardSetsProof.ParkBoard(p);
        var empty = Add(p, id); Add(p, "CS2_168");
        Ok(empty.Cost == 7 && CanPlay(p, empty), "Murlocs in hand do not count; playable with an empty board");
        var silenced = Summon(p, "CS2_168"); Silence(g, silenced);
        Ok(empty.Cost == 6, "a silenced Murloc is still a Murloc");
        PlayEntity(g, p, empty);
        Ok(silenced.AttackDamage == 4, "buff applies to a silenced minion");
        // Silence on a buffed minion removes it; the opponent's copy works for the opponent.
        Silence(g, silenced);
        Ok(silenced.AttackDamage == 2, "Silence removes the +2/+2");
        ToTurn(g, p.Opponent);
        var theirs = Add(p.Opponent, id);
        Ok(theirs.Cost == 6, "the opponent's copy counts the opponent's Murloc");
    }

    private static void ReliquarySeeker(Game g, Controller p, string id)
    {
        var seeker = (Minion)Play(g, p, id);
        Ok(seeker.AttackDamage == 1 && seeker.Health == 1 && p.UsedMana == 1, "1-mana 1/1");
        Ok(seeker.AttackDamage == 1 && seeker.Health == 1, "alone: no bonus");
        StandardSetsProof.ParkBoard(p);
        for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        var five = (Minion)Play(g, p, id);
        Ok(five.AttackDamage == 1 && five.Health == 1, "five other minions: no bonus");
        StandardSetsProof.ParkBoard(p);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        var held = Add(p, id);
        Resume(g, next => {
            var np = next.Player1; var played = (Minion)Play(next, np, id);
            Ok(played.AttackDamage == 5 && played.Health == 5, "clone: six others: +4/+4");
        });
        PlayEntity(g, p, held);
        var six = (Minion)held;
        Ok(six.AttackDamage == 5 && six.Health == 5 && p.BoardZone.IsFull, "six other minions: 5/5");
        Silence(g, six);
        Ok(six.AttackDamage == 1 && six.Health == 1, "Silence removes the bonus");
        // Enemy minions do not count; summoning does not trigger it.
        StandardSetsProof.ParkBoard(p);
        for (int i = 0; i < 3; i++) { Summon(p, "CS2_182"); Summon(p.Opponent, "CS2_182"); }
        Ok(((Minion)Play(g, p, id)).AttackDamage == 1, "enemy minions do not count");
        StandardSetsProof.ParkBoard(p);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        Ok(((Minion)Summon(p, id)).AttackDamage == 1, "summoned: no battlecry");
        // A location takes a slot but is not a minion: five minions and a location are not six minions.
        StandardSetsProof.ParkBoard(p);
        for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        LocationProofs.Location(p);
        Ok(p.BoardZone.Slots.Count() == 6 && MinionsOf(p).Length == 5, "five minions and a location");
        Ok(((Minion)Play(g, p, id)).AttackDamage == 1, "a location is not a minion: no bonus");
        // Brann doubles it: +8/+8, and Brann itself counts as one of the six.
        StandardSetsProof.ParkBoard(p);
        Summon(p, "LOE_077"); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        var doubled = (Minion)Play(g, p, id);
        Ok(doubled.AttackDamage == 9 && doubled.Health == 9, "Brann + five others: +4/+4 twice");
        // The opponent's copy counts the opponent's board.
        ToTurn(g, p.Opponent);
        StandardSetsProof.ParkBoard(p.Opponent);
        for (int i = 0; i < 6; i++) Summon(p.Opponent, "CS2_182");
        Ok(((Minion)Play(g, p.Opponent, id)).AttackDamage == 5, "the opponent's Seeker counts the opponent's minions");
    }

    // The opponent casts Fireball (6 damage) at a target of mine on their own turn, then it is my turn again.
    private static void Hit(Game g, Controller p, ICharacter target)
    {
        if (p.DeckZone.Count < 5) SetDeck(p, Enumerable.Repeat("CS2_182", 10).ToArray());
        ToTurn(g, p.Opponent);
        EmptyHand(p.Opponent);
        Play(g, p.Opponent, "CS2_029", target);
        ToTurn(g, p);
        EmptyHand(p);
    }

    private static void CursedBlade(Game g, Controller p, string id)
    {
        Play(g, p, id);
        var weapon = p.Hero.Weapon;
        Ok(weapon != null && weapon.Card.Id == id && weapon.AttackDamage == 2 && weapon.Durability == 3 && p.UsedMana == 1, "1-mana 2/3 weapon");
        Hit(g, p, p.Hero);
        Ok(p.Hero.Damage == 12, "damage to my hero is doubled (6 becomes 12)");
        p.Opponent.Hero.Damage = 0; Play(g, p, "CS2_029", p.Opponent.Hero);
        Ok(p.Opponent.Hero.Damage == 6, "damage to the enemy hero is not doubled");
        var minion = Summon(p, "CS2_182"); Hit(g, p, minion);
        Ok(minion.Damage == 6, "damage to my minions is not doubled");
        // Armor absorbs the doubled amount.
        p.Hero.Damage = 0; p.Hero.Armor = 5;
        Hit(g, p, p.Hero);
        Ok(p.Hero.Armor == 0 && p.Hero.Damage == 7, "doubled 12 hits armor 5 first, leaving 7: armor " + p.Hero.Armor + " damage " + p.Hero.Damage);
        Resume(g, next => {
            var np = next.Player1; np.Hero.Damage = 0; np.Hero.Armor = 0;
            Hit(next, np, np.Hero);
            Ok(np.Hero.Damage == 12, "clone: the weapon still doubles");
        });
        // Fatigue is damage too.
        p.Hero.Damage = 0; p.Hero.Fatigue = 0;
        SetDeck(p);
        Generic.Draw(p);
        Ok(p.Hero.Damage == 2, "fatigue 1 is doubled: " + p.Hero.Damage);
        // Combat damage to my hero is doubled.
        p.Hero.Damage = 0; SetDeck(p, "CS2_182");
        var attacker = Summon(p.Opponent, "CS2_182");
        ToTurn(g, p.Opponent);
        Attack(g, attacker, p.Hero);
        Ok(p.Hero.Damage == 8, "a 4-damage hit becomes 8: " + p.Hero.Damage);
        ToTurn(g, p);
        // The weapon wears out on attacks; a destroyed or replaced Blade stops doubling.
        p.Hero.Damage = 0; p.Hero.Armor = 0;
        p.Hero.Weapon.Destroy(); g.DeathProcessingAndAuraUpdate();
        Hit(g, p, p.Hero);
        Ok(p.Hero.Damage == 6, "without the Blade: normal damage: damage " + p.Hero.Damage + " weapon " + (p.Hero.Weapon?.Card.Id ?? "none") + " dur " + p.Hero.Weapon?.Durability);
        Play(g, p, id); Play(g, p, "CS2_106"); // Fiery War Axe replaces the Blade
        p.Hero.Damage = 0;
        Hit(g, p, p.Hero);
        Ok(p.Hero.Damage == 6, "replaced by another weapon: normal damage");
        // The opponent's Blade doubles damage to the opponent's hero only.
        ToTurn(g, p.Opponent);
        Play(g, p.Opponent, id);
        p.Opponent.Hero.Damage = 0; p.Hero.Damage = 0;
        ToTurn(g, p);
        p.Opponent.Hero.Damage = 0; Play(g, p, "CS2_029", p.Opponent.Hero);
        Ok(p.Opponent.Hero.Damage == 12 && p.Hero.Damage == 0, "the opponent's Blade doubles damage to the opponent's hero only");
        // Durability: the Blade breaks after three attacks, and the doubling ends with it.
        ToTurn(g, p.Opponent);
        p.Opponent.Hero.Weapon.Destroy(); g.DeathProcessingAndAuraUpdate();
        Play(g, p.Opponent, id);
        var blade = p.Opponent.Hero.Weapon; blade.Damage = 2;
        var wisp = Summon(p, "CS2_231");
        p.Opponent.Hero.IsExhausted = false;
        var attack = p.Opponent.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == wisp.Id);
        Ok(attack != null && g.Process(attack), "hero attack accepted");
        g.DeathProcessingAndAuraUpdate();
        p.Opponent.Hero.Damage = 0;
        ToTurn(g, p);
        p.Opponent.Hero.Damage = 0; Play(g, p, "CS2_029", p.Opponent.Hero);
        Ok(p.Opponent.Hero.Weapon == null && p.Opponent.Hero.Damage == 6, "after its last durability the Blade is gone and damage is normal: " + p.Opponent.Hero.Damage);
    }

    private static void AnimatedArmor(Game g, Controller p, string id)
    {
        var armor = (Minion)Play(g, p, id);
        Ok(Alive(armor) && armor.AttackDamage == 4 && armor.Health == 4 && p.UsedMana == 4, "4-mana 4/4");
        Hit(g, p, p.Hero);
        Ok(p.Hero.Damage == 1, "6 damage becomes 1: " + p.Hero.Damage);
        Hit(g, p, p.Hero);
        Ok(p.Hero.Damage == 2, "every hit is capped to 1");
        Resume(g, next => {
            var np = next.Player1; Hit(next, np, np.Hero);
            Ok(np.Hero.Damage == 3, "clone: still capped");
        });
        p.Hero.Armor = 5;
        Hit(g, p, p.Hero);
        Ok(p.Hero.Armor == 4 && p.Hero.Damage == 2, "the cap applies before armor: 1 armor lost: armor " + p.Hero.Armor + " damage " + p.Hero.Damage);
        // Only my hero; minions take full damage; the enemy hero is not capped.
        var minion = Summon(p, "CS2_182"); Hit(g, p, minion);
        Ok(minion.Damage == 6, "minions take full damage");
        p.Opponent.Hero.Damage = 0; Play(g, p, "CS2_029", p.Opponent.Hero);
        Ok(p.Opponent.Hero.Damage == 6, "the enemy hero takes full damage");
        // Damage that is already 1 stays 1; a spell that deals none deals none.
        p.Hero.Damage = 0; p.Hero.Armor = 0;
        ToTurn(g, p.Opponent);
        Play(g, p.Opponent, "CS2_031", p.Hero); // Ice Lance: freezes
        ToTurn(g, p);
        Ok(p.Hero.Damage == 0, "no damage stays no damage");
        // Combat damage.
        var hitter = Summon(p.Opponent, "CS2_182"); ToTurn(g, p.Opponent);
        Attack(g, hitter, p.Hero);
        Ok(p.Hero.Damage == 1, "a 4-damage attack is capped to 1: " + p.Hero.Damage);
        // Fatigue.
        ToTurn(g, p);
        p.Hero.Damage = 0; p.Hero.Fatigue = 4; SetDeck(p);
        Generic.Draw(p);
        Ok(p.Hero.Damage == 1 && p.Hero.Fatigue == 5, "fatigue 5 is capped to 1: " + p.Hero.Damage);
        // Silence removes it; death too; the opponent's Armor protects the opponent only; control change moves it.
        Silence(g, armor);
        p.Hero.Damage = 0; Hit(g, p, p.Hero);
        Ok(p.Hero.Damage == 6, "silenced: full damage: damage " + p.Hero.Damage + " silenced " + armor.IsSilenced + " zone " + armor.Zone?.Type);
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        var live = Summon(p, id); p.Hero.Damage = 0;
        Kill(g, live);
        Hit(g, p, p.Hero);
        Ok(p.Hero.Damage == 6, "a dead Animated Armor protects no one");
        var theirs = Summon(p.Opponent, id);
        p.Hero.Damage = 0; p.Opponent.Hero.Damage = 0;
        Hit(g, p, p.Hero);
        Ok(p.Hero.Damage == 6, "the opponent's Armor does not protect my hero");
        p.Opponent.Hero.Damage = 0; Play(g, p, "CS2_029", p.Opponent.Hero);
        Ok(p.Opponent.Hero.Damage == 1, "it protects its own controller");
        Steal(g, p, theirs);
        p.Hero.Damage = 0; Hit(g, p, p.Hero);
        Ok(p.Hero.Damage == 1, "a stolen Animated Armor protects the thief");
        // Together with Cursed Blade the cap wins (the doubled amount is capped at 1).
        p.Hero.Damage = 0; Play(g, p, "LOE_118");
        Hit(g, p, p.Hero);
        Ok(p.Hero.Damage == 1, "with Cursed Blade the final amount is still capped at 1: " + p.Hero.Damage);
    }
}
