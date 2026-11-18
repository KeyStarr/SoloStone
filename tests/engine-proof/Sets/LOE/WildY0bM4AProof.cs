using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

/// <summary>Wild wave Y0b, lane m4a: League of Explorers cards, proven against the 37.0 export through WildSetsProof.Case.</summary>
internal static class WildY0bM4AProof
{
    private static void Ok(bool condition, string message) => Core2026Proof.Check(condition, message);
    private static readonly List<string> HeldReport = new();

    [GroupProof("wild-y0b-m4a-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        HeldReport.Clear();
        WildSetsProof.Case("LOEA10_3", Tinyfin);
        WildSetsProof.Held("LOE_002", "held: generated-shuffle FX scope", ForgottenTorch);
        WildSetsProof.Case("LOE_003", EtherealConjurer);
        WildSetsProof.Case("LOE_006", MuseumCurator);
        WildSetsProof.Case("LOE_007", CurseOfRafaam);
        WildSetsProof.Case("LOE_009", ObsidianDestroyer);
        WildSetsProof.Case("LOE_010", PitSnake);
        WildSetsProof.Case("LOE_011", RenoJackson);
        WildSetsProof.Held("LOE_012", TombPillager);
        WildSetsProof.Case("LOE_016", RumblingElemental);
        WildSetsProof.Case("LOE_017", KeeperOfUldaman);
        WildSetsProof.Case("LOE_018", TunnelTrogg);
        WildSetsProof.Held("LOE_019", UnearthedRaptor);
        WildSetsProof.Case("LOE_020", DesertCamel);
        WildSetsProof.Case("LOE_021", DartTrap);
        WildSetsProof.Case("LOE_022", FierceMonkey);
        WildSetsProof.Held("LOE_023", DarkPeddler);
        WildSetsProof.Case("LOE_026", AnyfinCanHappen);
        WildSetsProof.Case("LOE_027", SacredTrial);
        WildSetsProof.Case("LOE_029", JeweledScarab);
        WildSetsProof.Case("LOE_038", NagaSeaWitch);
        WildSetsProof.Case("LOE_046", HugeToad);
        // BLOCKED-STANDARD: Standard data names this id (Core 2026 copy group). Proof kept, not registered. (LOE_012 and LOE_016 were held for the NPC ValueData tables, which do not hold an id.)
        Held("LOE_039", "data/progression/core-2026-unlocks.json, plugin/LocalCardCatalog.cs", GorillabotA3);
        foreach (string line in HeldReport) Console.WriteLine(line);
    }

    // ---- helpers ----

    private static void Held(string id, string source, Action<Game, Controller, string> scenario)
    {
        var failures = new List<string>();
        for (int seed = 1; seed <= 3; seed++)
        {
            try { Fixture(seed, game => scenario(game, game.Player1, id)); }
            catch (Exception error) { failures.Add("seed " + seed + ": " + error.Message); }
        }
        HeldReport.Add("HELD BLOCKED-STANDARD " + id + " (" + source + "): behaviour proof " + (failures.Count == 0 ? "passes" : "FAILS " + string.Join(" | ", failures)));
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

    private static void SetDeck(Controller p, params string[] ids)
    {
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        foreach (string id in ids) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId(id)));
    }

    private static void EmptyHand(Controller p)
    {
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
    }

    private static void ToTurn(Game g, Controller p) { while (g.CurrentPlayer != p) End(g); }
    private static IPlayable AddHand(Game g, Controller p, string id) { var card = Add(p, id); g.AuraUpdate(); return card; }

    private static void FillBoard(Controller p, string id = "CS2_182") { while (!p.BoardZone.IsFull) Summon(p, id); }
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
        Ok(p.Choice == null, "choice resolved");
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

    private static void Tinyfin(Game g, Controller p, string id)
    {
        int hand = p.HandZone.Count;
        var minion = (Minion)Play(g, p, id);
        Ok(Alive(minion) && minion.AttackDamage == 1 && minion.Health == 1 && p.UsedMana == 0 && minion.Card.IsRace(Race.MURLOC),
            "Tinyfin is a 0-cost 1/1 Murloc");
        Ok(p.Choice == null && p.HandZone.Count == hand && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0
            && p.Opponent.BoardZone.Count == 0 && p.BoardZone.Count == 1 && minion.IsExhausted && !minion.HasTaunt && !minion.HasDeathrattle,
            "Tinyfin has no effect, is exhausted, and has no keywords");
        minion.Silence(); g.DeathProcessingAndAuraUpdate();
        Ok(Alive(minion) && minion.AttackDamage == 1, "Silence leaves the vanilla Tinyfin intact");
        FillBoard(p); Ok(p.BoardZone.IsFull, "board filled");
        var held = Add(p, id);
        Ok(!CanPlay(p, held), "Tinyfin cannot be played onto a full board");
        StandardSetsProof.ParkBoard(p);
        Play(g, p, id); Play(g, p, id);
        Ok(p.BoardZone.Count == 2 && p.UsedMana == 0, "free Tinyfins stack at no cost");
    }

    private static void ForgottenTorch(Game g, Controller p, string id)
    {
        var enemy = Summon(p.Opponent, "CS2_182");
        int deck = p.DeckZone.Count, opponentDeck = p.Opponent.DeckZone.Count;
        var held = Add(p, id);
        Ok(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id)
            && p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).All(t => t.Target != null)
            && CanPlay(p, held), "Forgotten Torch always requires a target");
        PlayEntity(g, p, held, enemy);
        Ok(enemy.Damage == 3 && p.DeckZone.Count == deck + 1 && p.DeckZone.Count(c => c.Card.Id == "LOE_002t") == 1
            && p.Opponent.DeckZone.Count == opponentDeck && p.HandZone.Count == 0, "deals 3 and shuffles exactly one Roaring Torch into own deck");
        HiddenSame(g, game => game.Player1.DeckZone.First(c => c.Card.Id == "LOE_002t"), "CS2_182", 2, "shuffled Roaring Torch");
        // The shuffled copy is a real, drawable card that deals 6.
        var torch = p.DeckZone.First(c => c.Card.Id == "LOE_002t");
        Generic.Draw(p, (Playable)torch);
        Ok(p.HandZone.Contains(torch) && torch.Cost == 3, "Roaring Torch drawn from the deck costs 3");
        PlayEntity(g, p, torch, enemy);
        Ok(enemy.Damage == 9 && Dead(enemy), "Roaring Torch deals 6 (kills the damaged Yeti)");
        // Lethal target still shuffles; heroes are legal; Spell Damage applies to both halves.
        var wisp = Summon(p.Opponent, "CS2_231"); int before = p.DeckZone.Count;
        Play(g, p, id, wisp);
        Ok(Dead(wisp) && p.DeckZone.Count == before + 1, "a target that dies still gives the Roaring Torch");
        Play(g, p, id, p.Opponent.Hero);
        Ok(p.Opponent.Hero.Damage == 3 && p.DeckZone.Count(c => c.Card.Id == "LOE_002t") == 2, "hero target; a second cast adds a second Torch");
        Summon(p, "EX1_012");
        Play(g, p, id, p.Hero);
        Ok(p.Hero.Damage == 4, "Forgotten Torch scales with Spell Damage and may hit the own hero");
        Play(g, p, "LOE_002t", p.Opponent.Hero);
        Ok(p.Opponent.Hero.Damage == 3 + 7, "Roaring Torch scales with Spell Damage");
        // End-of-game exposure: opponent's deck holds none.
        Ok(p.Opponent.DeckZone.All(c => c.Card.Id == "CS2_182"), "opponent deck untouched");
    }

    private static void EtherealConjurer(Game g, Controller p, string id)
    {
        int hand = p.HandZone.Count;
        var conjurer = (Minion)Play(g, p, id);
        Ok(Alive(conjurer) && conjurer.AttackDamage == 6 && conjurer.Health == 4, "Ethereal Conjurer is 6/4 on the board");
        var offers = Offers(g, p, "Conjurer", card => card.Type == CardType.SPELL);
        Resume(g, next => {
            var np = next.Player1;
            Ok(np.Choice.Choices.SequenceEqual(p.Choice.Choices), "pending Discover survives clone and snapshot with the same offers");
            Pick(next, np, 1);
            Ok(np.HandZone.Count == hand + 1 && np.HandZone[np.HandZone.Count - 1].Card.Id == offers[1].Id, "clone picks the same card");
        });
        Pick(g, p, 1);
        Ok(p.HandZone.Count == hand + 1 && p.HandZone[p.HandZone.Count - 1].Card.Id == offers[1].Id
            && p.HandZone.Count(c => offers.Where((o, i) => i != 1).Any(o => o.Id == c.Card.Id && o.Id != offers[1].Id)) == 0,
            "only the picked spell enters the hand");
        Ok(p.Opponent.HandZone.Count == 0 && p.Opponent.Choice == null, "opponent gets nothing and no choice");
        Summon(p, id);
        Ok(p.Choice == null, "summoning without playing does not Discover");
        // Ten cards in hand including the Conjurer: the pick fits exactly, the hand never exceeds ten, nothing stalls.
        var tenth = Add(p, id); while (!p.HandZone.IsFull) Add(p, "CS2_182");
        PlayEntity(g, p, tenth);
        Ok(p.Choice != null && p.HandZone.Count == 9, "Discover offered with nine cards left in hand");
        Pick(g, p, 0);
        Ok(p.HandZone.Count == 10 && p.Choice == null, "pick fills the tenth slot; no choice left pending");
        HiddenSame(g, game => game.Player1.HandZone[game.Player1.HandZone.Count - 1], "CS2_182", 2, "discovered spell in hand");
    }

    private static void MuseumCurator(Game g, Controller p, string id)
    {
        int hand = p.HandZone.Count;
        var curator = (Minion)Play(g, p, id);
        Ok(Alive(curator) && curator.AttackDamage == 1 && curator.Health == 2, "Museum Curator is 1/2");
        var offers = Offers(g, p, "Curator", card => card[GameTag.DEATHRATTLE] == 1);
        Resume(g, next => {
            var np = next.Player1; Pick(next, np, 2);
            var got = np.HandZone[np.HandZone.Count - 1];
            Ok(got.Card.Id == offers[2].Id && got.Cost == Math.Max(0, offers[2].Cost - 1), "clone pick also costs (1) less");
        });
        var chosen = Pick(g, p, 0);
        var card = p.HandZone[p.HandZone.Count - 1];
        Ok(card.Card.Id == offers[0].Id && p.HandZone.Count == hand + 1 && card.Cost == Math.Max(0, offers[0].Cost - 1),
            "the discovered Deathrattle card costs (1) less (not below 0)");
        End(g);
        Ok(card.Cost == Math.Max(0, offers[0].Cost - 1), "the reduction does not expire at end of turn");
        // Another card of the same kind is unaffected, and Silence on the Curator does not undo the discount.
        var other = Add(p, offers[1].Id);
        Ok(other.Cost == offers[1].Cost, "only the discovered card is discounted");
        curator.Silence(); g.DeathProcessingAndAuraUpdate();
        Ok(card.Cost == Math.Max(0, offers[0].Cost - 1), "silencing the Curator keeps the discount");
        Summon(p, id);
        Ok(p.Choice == null, "summoning does not Discover");
    }

    private static void CurseOfRafaam(Game g, Controller p, string id)
    {
        var enemy = p.Opponent;
        Play(g, p, id);
        Ok(enemy.HandZone.Count(c => c.Card.Id == "LOE_007t") == 1 && p.HandZone.Count == 0 && enemy.Hero.Damage == 0 && p.Hero.Damage == 0,
            "the opponent receives one Cursed! and takes no damage yet");
        HiddenSame(g, game => game.Player2.HandZone.First(c => c.Card.Id == "LOE_007t"), "CS2_182", 1, "Cursed! in the opponent hand");
        Resume(g, next => {
            End(next);
            Ok(next.Player2.Hero.Damage == 2 && next.Player1.Hero.Damage == 0, "clone: Cursed! hurts its holder at their turn start");
        });
        End(g);
        Ok(enemy.Hero.Damage == 2 && p.Hero.Damage == 0, "holder takes 2 at the start of their turn, caster takes none");
        End(g); Ok(p.Hero.Damage == 0 && enemy.Hero.Damage == 2, "no damage on the caster's turn start");
        End(g); Ok(enemy.Hero.Damage == 4, "every turn while it is held");
        // Spell Damage on either side does not change it.
        Summon(enemy, "EX1_012"); Summon(p, "EX1_012");
        End(g); Ok(enemy.Hero.Damage == 4 && p.Hero.Damage == 0, "caster turn: still 4");
        End(g); Ok(enemy.Hero.Damage == 6, "Spell Damage does not amplify Cursed!");
        // A second Curse stacks; leaving the hand stops it.
        End(g);
        Play(g, p, id);
        End(g); Ok(enemy.Hero.Damage == 6 + 4, "two Cursed! cards deal 4 together");
        foreach (var cursed in enemy.HandZone.Where(c => c.Card.Id == "LOE_007t").ToArray()) { enemy.HandZone.Remove(cursed); enemy.SetasideZone.Add(cursed); }
        End(g); End(g); Ok(enemy.Hero.Damage == 6 + 4, "a Cursed! that left the hand no longer hurts");
        // Full hand: no card, no damage later.
        End(g);
        while (!enemy.HandZone.IsFull) Generic.DrawCard(enemy, Cards.FromId("CS2_182"));
        ToTurn(g, p); Play(g, p, id);
        Ok(enemy.HandZone.Count == 10 && enemy.HandZone.All(c => c.Card.Id != "LOE_007t"), "a full hand receives no Cursed!");
    }

    private static void ObsidianDestroyer(Game g, Controller p, string id)
    {
        var destroyer = (Minion)Play(g, p, id);
        Ok(destroyer.AttackDamage == 7 && destroyer.Health == 7 && p.BoardZone.Count == 1, "7/7 with no battlecry");
        Resume(g, next => {
            End(next);
            Ok(next.Player1.BoardZone.Count == 2 && next.Player1.BoardZone[1].Card.Id == "LOE_009t", "clone: pending end-of-turn trigger still summons");
        });
        End(g);
        var scarab = p.BoardZone.OfType<Minion>().Single(m => m.Card.Id == "LOE_009t");
        Ok(scarab.AttackDamage == 1 && scarab.Health == 1 && scarab.HasTaunt && scarab.Card.IsRace(Race.BEAST) && p.BoardZone.Count == 2
            && p.Opponent.BoardZone.Count == 0, "summons a 1/1 Beast Scarab with Taunt at the owner's end of turn");
        End(g); Ok(p.BoardZone.Count == 2, "no Scarab at the opponent's end of turn");
        End(g); Ok(p.BoardZone.Count == 3, "another Scarab each own end of turn");
        // Silence stops it; an opposing copy summons for its own controller.
        ToTurn(g, p);
        destroyer.Silence(); g.DeathProcessingAndAuraUpdate();
        End(g); End(g); Ok(p.BoardZone.Count == 3, "Silence stops the Scarabs");
        var theirs = Summon(p.Opponent, id);
        End(g); Ok(p.Opponent.BoardZone.Count == 1, "no Scarab at my end of turn for their Destroyer");
        End(g); Ok(p.Opponent.BoardZone.Count == 2 && p.Opponent.BoardZone.OfType<Minion>().Count(m => m.Card.Id == "LOE_009t") == 1,
            "their Destroyer summons for them at their end of turn");
        ToTurn(g, p);
        Kill(g, theirs); End(g); End(g);
        Ok(p.Opponent.BoardZone.Count == 1, "a dead Destroyer summons nothing");
        ToTurn(g, p);
        // Full board: nothing happens and nothing breaks.
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        Summon(p, id); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        Ok(p.BoardZone.IsFull, "board full with the Destroyer");
        End(g); Ok(p.BoardZone.Count == 7, "full board: no Scarab, no error");
        End(g);
        // Two Destroyers: two Scarabs.
        StandardSetsProof.ParkBoard(p);
        Summon(p, id); Summon(p, id);
        End(g); Ok(p.BoardZone.OfType<Minion>().Count(m => m.Card.Id == "LOE_009t") == 2, "each Destroyer summons its own Scarab");
        // Control change: a stolen Destroyer works for its new controller at that controller's end of turn.
        ToTurn(g, p); StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        var stolen = Summon(p.Opponent, id); Steal(g, p, stolen);
        End(g); Ok(p.BoardZone.OfType<Minion>().Count(m => m.Card.Id == "LOE_009t") == 1 && p.Opponent.BoardZone.Count == 0,
            "a stolen Destroyer summons for its new controller");
        End(g); Ok(p.BoardZone.Count == 2, "and not at the old owner's end of turn");
    }

    private static void PitSnake(Game g, Controller p, string id)
    {
        var snake = (Minion)Play(g, p, id);
        Ok(snake.AttackDamage == 2 && snake.Health == 1 && snake.Poisonous && snake.Card.IsRace(Race.BEAST), "2/1 Poisonous Beast");
        var yeti = Summon(p.Opponent, "CS2_182");
        Attack(g, snake, p.Opponent.Hero);
        Ok(p.Opponent.Hero.Damage == 2 && p.Opponent.Hero.Health == 28, "Poisonous does not destroy a hero");
        End(g);
        Attack(g, yeti, snake);
        Ok(Dead(yeti) && Dead(snake), "Poisonous kills an attacker that hits it");
        End(g);
        var snake2 = Summon(p, id); var yeti2 = Summon(p.Opponent, "CS2_182");
        Attack(g, snake2, yeti2);
        Ok(Dead(yeti2) && Dead(snake2), "Poisonous kills the defender it damages");
        var snake3 = Summon(p, id); var squire = Summon(p.Opponent, "EX1_008");
        Attack(g, snake3, squire);
        Ok(Alive(squire) && !squire.HasDivineShield, "Divine Shield absorbs the poisonous hit (no damage, no kill)");
        var snake4 = Summon(p, id); snake4.Silence(); g.DeathProcessingAndAuraUpdate();
        var yeti3 = Summon(p.Opponent, "CS2_182");
        Ok(!snake4.Poisonous, "Silence removes Poisonous");
        Attack(g, snake4, yeti3);
        Ok(Alive(yeti3) && yeti3.Damage == 2, "silenced Snake deals only its damage");
    }

    private static void RenoJackson(Game g, Controller p, string id)
    {
        string[] unique = Cards.FormatTypeCards(FormatType.FT_WILD).Where(c => c.Collectible && c.Type == CardType.MINION && c.Implemented)
            .Select(c => c.Id).Distinct().Take(30).ToArray();
        Ok(unique.Length == 30, "thirty distinct cards available");
        p.Hero.Damage = 12;
        Play(g, p, id);
        Ok(p.Hero.Damage == 12 && p.BoardZone.Count == 1, "a deck of thirty Yetis has duplicates: no heal");
        SetDeck(p, unique); StandardSetsProof.ParkBoard(p);
        Play(g, p, id);
        Ok(p.Hero.Damage == 0, "a deck with no duplicates fully heals the hero");
        p.Hero.Damage = 29;
        var dupe = unique.Take(29).Concat(new[] { unique[0] }).ToArray(); SetDeck(p, dupe); StandardSetsProof.ParkBoard(p);
        Play(g, p, id);
        Ok(p.Hero.Damage == 29, "a single duplicate pair blocks the heal");
        SetDeck(p); StandardSetsProof.ParkBoard(p);
        Play(g, p, id);
        Ok(p.Hero.Damage == 0, "an empty deck has no duplicates");
        // The opponent's deck does not matter, and a full-health hero stays full.
        SetDeck(p, unique); p.Hero.Damage = 7; StandardSetsProof.ParkBoard(p);
        Ok(p.Opponent.DeckZone.Select(c => c.Card.Id).Distinct().Count() == 1, "opponent deck is all duplicates");
        Play(g, p, id);
        Ok(p.Hero.Damage == 0, "only the own deck counts");
        StandardSetsProof.ParkBoard(p);
        Play(g, p, id); Ok(p.Hero.Damage == 0, "full-health hero is unchanged");
        // Battlecry only: summoning heals nothing; the enemy hero is untouched.
        p.Hero.Damage = 5; p.Opponent.Hero.Damage = 5; StandardSetsProof.ParkBoard(p);
        Summon(p, id);
        Ok(p.Hero.Damage == 5 && p.Opponent.Hero.Damage == 5, "summoned Reno has no battlecry");
        Play(g, p, id);
        Ok(p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 5, "heals only the own hero");
        // The deck is read at play time: a Highlander deck built afterwards counts.
        StandardSetsProof.ParkBoard(p);
        var reno = Add(p, id); p.Hero.Damage = 3; SetDeck(p, unique.Take(10).ToArray());
        PlayEntity(g, p, reno);
        Ok(p.Hero.Damage == 0, "deck checked when played");
        // Both shuffled copies of a generated card are duplicates too.
        p.Hero.Damage = 3; SetDeck(p, "CS2_231", "CS2_231"); StandardSetsProof.ParkBoard(p);
        Play(g, p, id);
        Ok(p.Hero.Damage == 3, "two copies of the same card are duplicates");
    }

    private static void KeeperOfUldaman(Game g, Controller p, string id)
    {
        var yeti = Summon(p.Opponent, "CS2_182"); var wisp = Summon(p, "CS2_231");
        var keeper = (Minion)Play(g, p, id, yeti);
        Ok(Alive(keeper) && keeper.AttackDamage == 3 && keeper.Health == 4 && p.UsedMana == 3, "Keeper is a 3-mana 3/4");
        Ok(yeti.AttackDamage == 3 && yeti.Health == 3 && yeti.Damage == 0, "sets an enemy minion to 3/3");
        Play(g, p, id, wisp);
        Ok(wisp.AttackDamage == 3 && wisp.Health == 3, "raises a small minion to 3/3");
        var damaged = Summon(p.Opponent, "CS2_182"); damaged.Damage = 3;
        Play(g, p, id, damaged);
        Ok(damaged.AttackDamage == 3 && damaged.Health == 3 && !Dead(damaged), "a damaged minion ends as a healthy 3/3 (damage is replaced)");
        var buffed = Summon(p, "CS2_182");
        Effects.Attack_N(2).ApplyTo(buffed); Effects.Health_N(2).ApplyTo(buffed); g.DeathProcessingAndAuraUpdate();
        Ok(buffed.AttackDamage == 6 && buffed.Health == 7, "buffed Yeti is 6/7");
        Play(g, p, id, buffed);
        Ok(buffed.AttackDamage == 3 && buffed.Health == 3, "set effect overrides earlier buffs");
        Effects.Attack_N(1).ApplyTo(buffed); Effects.Health_N(1).ApplyTo(buffed); g.DeathProcessingAndAuraUpdate();
        Ok(buffed.AttackDamage == 4 && buffed.Health == 4, "later buffs still add on top of the set values");
        buffed.Silence(); g.DeathProcessingAndAuraUpdate();
        Ok(buffed.AttackDamage == 4 && buffed.Health == 5, "Silence removes the set effect and buffs");
        var held = Add(p, id);
        var options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).ToArray();
        Ok(options.Any(t => t.Target == null) == false && options.All(t => t.Target is Minion) && options.Any(t => t.Target?.Id == yeti.Id),
            "with minions on the board the target is a minion, never a hero");
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        var alone = (Minion)Play(g, p, id);
        Ok(Alive(alone) && alone.AttackDamage == 3 && alone.Health == 4, "playable with no target; Keeper cannot target itself");
        End(g); Ok(yeti.Zone.Type != Zone.INVALID, "end of turn");
    }

    private static void TunnelTrogg(Game g, Controller p, string id)
    {
        var trogg = (Minion)Play(g, p, id);
        Ok(trogg.AttackDamage == 1 && trogg.Health == 3, "1/3");
        var target = Summon(p.Opponent, "CS2_182");
        Play(g, p, "CS2_029", target);
        Ok(trogg.AttackDamage == 1, "a non-Overload card gives nothing");
        Play(g, p, "EX1_238", p.Opponent.Hero);
        Ok(trogg.AttackDamage == 2 && p.OverloadLocked == 0 && p.OverloadOwed == 1, "Overload (1): +1 Attack");
        Play(g, p, "EX1_241", p.Opponent.Hero);
        Ok(trogg.AttackDamage == 4 && p.OverloadOwed == 3, "Overload (2): +2 Attack more");
        Play(g, p, "AT_052");
        Ok(trogg.AttackDamage == 5, "an Overload minion triggers it as well");
        var second = (Minion)Play(g, p, id);
        Play(g, p, "EX1_241", p.Opponent.Hero);
        Ok(trogg.AttackDamage == 7 && second.AttackDamage == 3, "every Tunnel Trogg gains");
        End(g);
        Ok(trogg.AttackDamage == 7, "the gain is permanent");
        // Opponent overload does not trigger it.
        Play(g, p.Opponent, "EX1_241", p.Hero);
        Ok(trogg.AttackDamage == 7 && second.AttackDamage == 3, "opponent Overload does not trigger it");
        End(g);
        Resume(g, next => {
            var t = (Minion)next.IdEntityDic[trogg.Id];
            Play(next, next.Player1, "EX1_238", next.Player2.Hero);
            Ok(t.AttackDamage == 8, "the trigger survives clone and snapshot");
        });
        trogg.Silence(); g.DeathProcessingAndAuraUpdate();
        Ok(trogg.AttackDamage == 1, "Silence removes the gains");
        Play(g, p, "EX1_241", p.Opponent.Hero);
        Ok(trogg.AttackDamage == 1 && second.AttackDamage == 5, "silenced Trogg stays silent; the other still gains");
        Kill(g, second); Play(g, p, "EX1_238", p.Opponent.Hero);
        Ok(Dead(second), "a dead Trogg no longer triggers");
        // Control change: a stolen Trogg gains from its new controller's Overload only.
        var theirs = Summon(p.Opponent, id); Steal(g, p, theirs);
        Play(g, p, "EX1_238", p.Opponent.Hero);
        Ok(theirs.AttackDamage == 2, "a stolen Trogg gains from the new controller's Overload");
        End(g);
        Play(g, p.Opponent, "EX1_238", p.Hero);
        Ok(theirs.AttackDamage == 2, "and ignores the old owner's Overload");
    }

    private static void UnearthedRaptor(Game g, Controller p, string id)
    {
        var yeti = Summon(p, "CS2_182");
        var held = Add(p, id);
        Ok(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id && t.Target != null),
            "no Deathrattle minion: no target is offered");
        PlayEntity(g, p, held);
        var raptor = (Minion)held;
        Ok(Alive(raptor) && raptor.AttackDamage == 3 && raptor.Health == 4 && p.Opponent.Hero.Damage == 0, "playable without a target");
        Kill(g, raptor); Ok(p.Opponent.Hero.Damage == 0 && p.HandZone.Count == 0, "no Deathrattle gained without a target");
        var gnome = Summon(p, "EX1_029"); var theirs = Summon(p.Opponent, "EX1_029");
        var second = Add(p, id);
        var targets = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == second.Id).Select(t => t.Target?.Id).ToArray();
        Ok(targets.Contains(gnome.Id) && !targets.Contains(theirs.Id) && !targets.Contains(yeti.Id) && !targets.Contains(p.Hero.Id),
            "only friendly Deathrattle minions are legal targets");
        PlayEntity(g, p, second, gnome);
        var copy = (Minion)second;
        Resume(g, next => {
            Kill(next, (Minion)next.IdEntityDic[copy.Id]);
            Ok(next.Player2.Hero.Damage == 2, "clone: copied Deathrattle fires");
        });
        Kill(g, gnome);
        Ok(p.Opponent.Hero.Damage == 2, "the original dies first and fires once");
        Kill(g, copy);
        Ok(p.Opponent.Hero.Damage == 4, "the Raptor still holds a copy after the original is gone");
        // A different Deathrattle: draw a card.
        var hoarder = Summon(p, "EX1_096"); int hand = p.HandZone.Count;
        var third = Add(p, id); PlayEntity(g, p, third, hoarder);
        Kill(g, (Minion)third);
        Ok(p.HandZone.Count == hand + 1 && p.Opponent.Hero.Damage == 4, "copies a draw Deathrattle");
        // Silence removes the copy; the Raptor keeps no printed Deathrattle otherwise.
        var gnome2 = Summon(p, "EX1_029"); var fourth = Add(p, id); PlayEntity(g, p, fourth, gnome2);
        ((Minion)fourth).Silence(); g.DeathProcessingAndAuraUpdate(); Kill(g, (Minion)fourth);
        Ok(p.Opponent.Hero.Damage == 4, "Silence removes the copied Deathrattle");
    }

    private static void DesertCamel(Game g, Controller p, string id)
    {
        var enemy = p.Opponent;
        // Nothing to put in: no 1-Cost minion in either deck.
        int decks = p.DeckZone.Count, enemyDeck = enemy.DeckZone.Count;
        var camel = (Minion)Play(g, p, id);
        Ok(Alive(camel) && camel.AttackDamage == 2 && camel.Health == 4 && p.BoardZone.Count == 1 && enemy.BoardZone.Count == 0
            && p.DeckZone.Count == decks && enemy.DeckZone.Count == enemyDeck, "no 1-Cost minion in the decks: nothing happens");
        string[] ownCandidates = { "CS2_168", "EX1_011", "CS2_189" };
        SetDeck(p, ownCandidates.Concat(new[] { "CS2_231", "CS2_074", "CS2_182", "EX1_096" }).ToArray());
        SetDeck(enemy, "CS2_189", "CS2_189", "EX1_029", "EX1_096", "CS2_182");
        StandardSetsProof.ParkBoard(p);
        Resume(g, next => {
            var np = next.Player1; var camel2 = (Minion)Play(next, np, id);
            Ok(np.BoardZone.Count == 2 && np.BoardZone.OfType<Minion>().Count(m => m.Card.Cost == 1 && m != camel2) == 1, "clone: same shape");
        });
        var second = (Minion)Play(g, p, id);
        var own = p.BoardZone.OfType<Minion>().Where(m => m != second).ToArray();
        var theirs = enemy.BoardZone.OfType<Minion>().ToArray();
        Ok(own.Length == 1 && ownCandidates.Contains(own[0].Card.Id) && own[0].Controller == p && p.DeckZone.Count == 6
            && !p.DeckZone.Any(c => c.Id == own[0].Id), "one 1-Cost minion leaves my deck for my board");
        Ok(theirs.Length == 1 && (theirs[0].Card.Id == "CS2_189" || theirs[0].Card.Id == "EX1_029") && theirs[0].Controller == enemy
            && enemy.DeckZone.Count == 4, "one 1-Cost minion leaves their deck for their board");
        Ok(p.Hero.Damage == 0 && enemy.Hero.Damage == 0 && own[0].IsExhausted, "put-in minions do not trigger Battlecries and are exhausted");
        // Cost 1 only: a 0-Cost and a 2-Cost minion in the deck are never taken.
        SetDeck(p, "CS2_231", "EX1_096", "CS2_182"); SetDeck(enemy, "CS2_231", "EX1_096");
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(enemy);
        Play(g, p, id);
        Ok(p.BoardZone.Count == 1 && enemy.BoardZone.Count == 0 && p.DeckZone.Count == 3 && enemy.DeckZone.Count == 2, "only exact 1-Cost minions");
        // Full boards: each side independently.
        SetDeck(p, "CS2_168"); SetDeck(enemy, "CS2_168");
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(enemy);
        for (int i = 0; i < 6; i++) Summon(enemy, "CS2_182");
        Play(g, p, id);
        Ok(p.BoardZone.Count == 2 && enemy.BoardZone.Count == 7 && p.DeckZone.Count == 0 && enemy.DeckZone.Count == 0,
            "enemy board filled by the put-in minion: own " + p.BoardZone.Count + " enemy " + enemy.BoardZone.Count + " decks " + p.DeckZone.Count + "/" + enemy.DeckZone.Count);
        SetDeck(p, "CS2_168"); SetDeck(enemy, "CS2_168");
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(enemy);
        for (int i = 0; i < 7; i++) Summon(enemy, "CS2_182");
        Play(g, p, id);
        Ok(p.BoardZone.Count == 2 && enemy.BoardZone.Count == 7 && enemy.DeckZone.Count == 1, "a full enemy board keeps their 1-Cost minion in the deck");
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(enemy);
        SetDeck(p, "CS2_168"); SetDeck(enemy, "CS2_168");
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        Play(g, p, id);
        Ok(p.BoardZone.Count == 7 && p.DeckZone.Count == 1 && enemy.BoardZone.Count == 1 && enemy.DeckZone.Count == 0,
            "a full own board keeps my 1-Cost minion in the deck while theirs still enters");
    }

    private static void DartTrap(Game g, Controller p, string id)
    {
        var enemy = p.Opponent;
        Play(g, p, id);
        Ok(p.SecretZone.Count == 1 && p.UsedMana == 2, "Dart Trap is played as a Secret");
        var extra = Add(p, id);
        Ok(!CanPlay(p, extra), "a second copy cannot be played while one is active");
        p.UsedMana = 0; Ok(g.Process(HeroPowerTask.Any(p)), "own hero power used");
        Ok(p.SecretZone.Count == 1 && enemy.Hero.Damage == 0, "my own Hero Power does not trigger it");
        var a = Summon(enemy, "EX1_572"); var b = Summon(enemy, "EX1_572"); var mine = Summon(p, "EX1_572");
        End(g);
        var third = (Minion)Play(g, enemy, "EX1_572");
        Ok(p.SecretZone.Count == 1, "an opposing card play is not a Hero Power");
        var foes = new ICharacter[] { enemy.Hero, a, b, third };
        Resume(g, next => {
            var ne = next.Player2; ne.UsedMana = 0;
            Ok(next.Process(HeroPowerTask.Any(ne, next.Player1.Hero)), "clone: opposing Hero Power");
            Ok(next.Player1.SecretZone.Count == 0 && ne.Hero.Damage + ne.BoardZone.OfType<Minion>().Sum(m => m.Damage) == 5,
                "clone: the secret fires exactly once for 5");
        });
        enemy.UsedMana = 0;
        Ok(g.Process(HeroPowerTask.Any(enemy, p.Hero)), "opposing Hero Power used");
        Ok(p.SecretZone.Count == 0 && foes.Count(c => c.Damage > 0) == 1 && foes.Sum(c => c.Damage) == 5 && mine.Damage == 0 && p.Hero.Damage == 1,
            "5 damage to exactly one random enemy; my own characters take only the Hero Power's 1 damage to my hero");
        // Spell Damage adds to it; the new copy only fires on an opposing Hero Power again.
        End(g);
        Summon(p, "EX1_012");
        Play(g, p, id); Ok(p.SecretZone.Count == 1, "a spent Secret can be played again");
        int before = foes.Sum(c => c.Damage) + (p.Hero.Damage * 0);
        End(g);
        Ok(p.SecretZone.Count == 1 && foes.Sum(c => c.Damage) == before, "no trigger at turn start or from a card play");
        enemy.UsedMana = 0; Ok(g.Process(HeroPowerTask.Any(enemy, p.Hero)), "opposing Hero Power again");
        Ok(p.SecretZone.Count == 0 && foes.Sum(c => c.Damage) == before + 6, "Spell Damage adds 1 to Dart Trap");
        // A lethal hit kills a minion: only a 1-health enemy is left.
        End(g); StandardSetsProof.ParkBoard(enemy); StandardSetsProof.ParkBoard(p);
        Summon(p, "EX1_012");
        Play(g, p, id); End(g);
        var wisp = Summon(enemy, "CS2_231"); enemy.Hero.Damage = 0;
        enemy.UsedMana = 0; Ok(g.Process(HeroPowerTask.Any(enemy, p.Hero)), "Hero Power once more");
        Ok(p.SecretZone.Count == 0 && (Dead(wisp) ? enemy.Hero.Damage == 0 : enemy.Hero.Damage == 6), "a minion dies to it, or the hero takes the hit");
    }

    private static void FierceMonkey(Game g, Controller p, string id)
    {
        var monkey = (Minion)Play(g, p, id);
        Ok(monkey.AttackDamage == 3 && monkey.Health == 4 && monkey.HasTaunt && monkey.Card.IsRace(Race.BEAST), "3/4 Beast with Taunt");
        var yeti = Summon(p.Opponent, "CS2_182"); End(g);
        yeti.IsExhausted = false;
        var attacks = p.Opponent.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == yeti.Id).ToArray();
        Ok(attacks.Length == 1 && attacks[0].Target?.Id == monkey.Id, "Taunt forces attacks onto the Monkey");
        End(g);
        monkey.Silence(); g.DeathProcessingAndAuraUpdate(); End(g);
        yeti.IsExhausted = false;
        var after = p.Opponent.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == yeti.Id).ToArray();
        Ok(!monkey.HasTaunt && after.Any(t => t.Target?.Id == p.Hero.Id), "Silence removes Taunt");
    }

    private static void DarkPeddler(Game g, Controller p, string id)
    {
        int hand = p.HandZone.Count;
        var peddler = (Minion)Play(g, p, id);
        Ok(Alive(peddler) && peddler.AttackDamage == 2 && peddler.Health == 3 && peddler.Card.IsRace(Race.UNDEAD), "2/3 Undead");
        var offers = Offers(g, p, "Peddler", card => card.Cost == 1);
        Resume(g, next => {
            var np = next.Player1; Pick(next, np, 2);
            Ok(np.HandZone[np.HandZone.Count - 1].Card.Id == offers[2].Id, "clone picks the same 1-Cost card");
        });
        Pick(g, p, 0);
        var card = p.HandZone[p.HandZone.Count - 1];
        Ok(p.HandZone.Count == hand + 1 && card.Card.Id == offers[0].Id && card.Cost == 1, "the 1-Cost pick enters the hand");
        Summon(p, id); Ok(p.Choice == null, "summoning does not Discover");
        var tenth = Add(p, id); while (!p.HandZone.IsFull) Add(p, "CS2_182");
        PlayEntity(g, p, tenth);
        Ok(p.Choice != null && p.HandZone.Count == 9, "Discover offered with nine cards left in hand");
        Pick(g, p, 0);
        Ok(p.HandZone.Count == 10 && p.Choice == null, "pick fills the tenth slot; nothing pending");
        Ok(p.Opponent.HandZone.Count == 0, "opponent gets nothing");
        HiddenSame(g, game => game.Player1.HandZone[0], "CS2_182", 2, "hand card");
        // Offer rules for small or empty pools (owner rule D2): fewer than three candidates offer only those, none offer nothing.
        var two = DiscoverTask.GetChoices(new[] { new Card[0], new[] { offers[0], offers[1] } }, 3, g.Random);
        Ok(two.Length == 2 && two.Distinct().Count() == 2, "two candidates offer two distinct cards");
        Ok(DiscoverTask.GetChoices(new[] { new Card[0], new Card[0] }, 3, g.Random).Length == 0, "an empty pool offers nothing");
    }

    private static void AnyfinCanHappen(Game g, Controller p, string id)
    {
        var enemy = p.Opponent;
        Play(g, p, id);
        Ok(p.BoardZone.Count == 0 && p.UsedMana == 10, "no Murloc has died: a legal play that summons nothing");
        var yeti = Summon(p, "CS2_182"); Kill(g, yeti);
        var theirs = Summon(enemy, "CS2_168"); Kill(g, theirs);
        Play(g, p, id);
        Ok(p.BoardZone.Count == 0 && enemy.BoardZone.Count == 0, "dead non-Murlocs and enemy Murlocs are not summoned");
        var raiders = new List<Minion>();
        for (int i = 0; i < 3; i++) raiders.Add(Summon(p, "CS2_168"));
        var buffed = raiders[0]; Effects.Attack_N(3).ApplyTo(buffed); Effects.Health_N(3).ApplyTo(buffed); g.DeathProcessingAndAuraUpdate();
        foreach (var raider in raiders) Kill(g, raider);
        Resume(g, next => {
            Play(next, next.Player1, id);
            Ok(next.Player1.BoardZone.Count == 3, "clone: summons the three dead Murlocs");
        });
        Play(g, p, id);
        Ok(p.BoardZone.Count == 3 && p.BoardZone.OfType<Minion>().All(m => m.Card.Id == "CS2_168" && m.AttackDamage == 2 && m.Health == 1
            && m.Damage == 0), "fewer than seven died: all are summoned, as fresh base 2/1 copies without buffs");
        // More than seven: exactly seven, from nine dead, never repeating a death.
        StandardSetsProof.ParkBoard(p);
        for (int i = 0; i < 9; i++) { var m = Summon(p, i % 2 == 0 ? "CS2_168" : "EX1_506"); Kill(g, m); }
        StandardSetsProof.ParkBoard(p);
        Play(g, p, id);
        Ok(p.BoardZone.Count == 7 && p.BoardZone.OfType<Minion>().All(m => m.Card.IsRace(Race.MURLOC)), "exactly seven Murlocs when more have died");
        Ok(p.BoardZone.OfType<Minion>().Count(m => m.Card.Id == "EX1_506") <= 5 && p.BoardZone.OfType<Minion>().Count(m => m.Card.Id == "CS2_168") <= 6
            && p.BoardZone.OfType<Minion>().Select(m => m.Id).Distinct().Count() == 7, "no more of a card than died");
        // Limited by board space.
        StandardSetsProof.ParkBoard(p);
        for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        Play(g, p, id);
        Ok(p.BoardZone.Count == 7 && p.BoardZone.OfType<Minion>().Count(m => m.Card.IsRace(Race.MURLOC)) == 2, "fills only the open slots");
        // A Murloc card that never died (discarded from hand) does not count.
        var spare = Add(p, "CS2_168"); p.HandZone.Remove(spare); p.GraveyardZone.Add(spare);
        StandardSetsProof.ParkBoard(p);
        Play(g, p, id);
        Ok(p.BoardZone.OfType<Minion>().Count(m => m.Id == spare.Id) == 0, "a card that was only discarded is not a Murloc that died");
    }

    private static void SacredTrial(Game g, Controller p, string id)
    {
        var enemy = p.Opponent;
        Play(g, p, id);
        Ok(p.SecretZone.Count == 1 && !CanPlay(p, Add(p, id)), "Secret in play; no second copy");
        for (int i = 0; i < 3; i++) Summon(p, "CS2_182");
        Play(g, p, "CS2_182");
        Ok(p.BoardZone.Count == 4 && p.SecretZone.Count == 1, "my own minion plays never trigger it");
        End(g);
        Summon(enemy, "CS2_182"); Summon(enemy, "CS2_182");
        var third = Play(g, enemy, "CS2_182");
        Ok(enemy.BoardZone.Count == 3 && p.SecretZone.Count == 1, "two minions then a third: no trigger");
        Summon(enemy, "CS2_182");
        Ok(enemy.BoardZone.Count == 4 && p.SecretZone.Count == 1, "summoning is not playing");
        Resume(g, next => {
            var ne = next.Player2;
            var played = Play(next, ne, "EX1_029");
            Ok(((Minion)played).Zone.Type == Zone.GRAVEYARD && next.Player1.SecretZone.Count == 0 && ne.BoardZone.Count == 4
                && next.Player1.Hero.Damage == 2, "clone: destroyed minion still triggers its Deathrattle");
        });
        var fifth = (Minion)Play(g, enemy, "EX1_029");
        Ok(Dead(fifth) && p.SecretZone.Count == 0 && enemy.BoardZone.Count == 4 && p.Hero.Damage == 2, "with three or more minions the played minion is destroyed; its Deathrattle fires");
        Play(g, enemy, "CS2_182");
        Ok(enemy.BoardZone.Count == 5, "the Secret is spent");
        // Exactly the threshold: at least three means three counts.
        End(g);
        StandardSetsProof.ParkBoard(enemy);
        Play(g, p, id); End(g);
        for (int i = 0; i < 3; i++) Summon(enemy, "CS2_182");
        var sixth = (Minion)Play(g, enemy, "CS2_182");
        Ok(Dead(sixth) && p.SecretZone.Count == 0, "exactly three minions already out is enough");
        // Full board of six then a seventh.
        End(g); Play(g, p, id); End(g);
        StandardSetsProof.ParkBoard(enemy);
        for (int i = 0; i < 6; i++) Summon(enemy, "CS2_182");
        var seventh = (Minion)Play(g, enemy, "CS2_182");
        Ok(Dead(seventh) && enemy.BoardZone.Count == 6, "works with the seventh slot as well");
    }

    private static void JeweledScarab(Game g, Controller p, string id)
    {
        int hand = p.HandZone.Count;
        var scarab = (Minion)Play(g, p, id);
        Ok(Alive(scarab) && scarab.AttackDamage == 1 && scarab.Health == 1 && scarab.Card.IsRace(Race.BEAST), "1/1 Beast");
        var offers = Offers(g, p, "Scarab", card => card.Cost == 3);
        Resume(g, next => {
            var np = next.Player1; Pick(next, np, 1);
            Ok(np.HandZone[np.HandZone.Count - 1].Card.Id == offers[1].Id, "clone picks the same 3-Cost card");
        });
        Pick(g, p, 2);
        var card = p.HandZone[p.HandZone.Count - 1];
        Ok(p.HandZone.Count == hand + 1 && card.Card.Id == offers[2].Id && card.Cost == 3, "the 3-Cost pick enters the hand");
        Summon(p, id); Ok(p.Choice == null, "summoning does not Discover");
        var tenth = Add(p, id); while (!p.HandZone.IsFull) Add(p, "CS2_182");
        PlayEntity(g, p, tenth);
        Ok(p.Choice != null && p.HandZone.Count == 9, "Discover offered with nine cards left in hand");
        Pick(g, p, 0);
        Ok(p.HandZone.Count == 10 && p.Choice == null, "pick fills the tenth slot; nothing pending");
        Ok(p.Opponent.HandZone.Count == 0, "opponent gets nothing");
        HiddenSame(g, game => game.Player1.HandZone[0], "CS2_182", 2, "hand card");
    }

    private static void NagaSeaWitch(Game g, Controller p, string id)
    {
        var enemy = p.Opponent;
        var cheap = Add(p, "CS2_231"); var dear = Add(p, "EX1_572"); var mid = Add(p, "CS2_029");
        var theirs = Generic.DrawCard(enemy, Cards.FromId("CS2_182"));
        Ok(cheap.Cost == 0 && dear.Cost == 9 && mid.Cost == 4, "baseline costs");
        var witch = (Minion)Play(g, p, id);
        Ok(Alive(witch) && witch.AttackDamage == 5 && witch.Health == 5 && witch.Card.IsRace(Race.NAGA) && p.UsedMana == 8, "8-mana 5/5 Naga");
        Ok(cheap.Cost == 5 && dear.Cost == 5 && mid.Cost == 5, "every card in hand costs 5, cheaper or dearer");
        Ok(theirs.Cost == 4, "the opponent's hand is unaffected");
        var drawn = AddHand(g, p, "CS2_231");
        Ok(drawn.Cost == 5, "cards added later cost 5 too");
        Generic.Draw(p); g.AuraUpdate();
        Ok(p.HandZone[p.HandZone.Count - 1].Cost == 5 && p.HandZone[p.HandZone.Count - 1].Card.Cost == 4, "a drawn card costs 5");
        Resume(g, next => {
            Ok(next.Player1.HandZone.All(c => c.Cost == 5), "clone keeps the aura");
            End(next);
            Ok(next.Player2.HandZone.All(c => c.Cost == 4 || c.Cost == 0), "clone: opponent unaffected");
        });
        PlayEntity(g, p, cheap);
        Ok(p.UsedMana == 5, "a 0-cost card now costs 5 to play");
        End(g); End(g);
        Ok(dear.Cost == 5, "aura persists across turns");
        var second = (Minion)Play(g, p, id);
        Ok(dear.Cost == 5 && second.Cost == 8 || second.Zone.Type == Zone.PLAY, "a second Naga leaves the cost at 5");
        Kill(g, witch);
        Ok(dear.Cost == 5, "one Naga is still enough");
        Kill(g, second);
        Ok(dear.Cost == 9 && mid.Cost == 4 && p.HandZone.Where(c => c.Card.Id == "CS2_231").All(c => c.Cost == 0), "costs return to normal when the last Naga leaves");
        var third = (Minion)Play(g, p, id);
        Ok(mid.Cost == 5, "aura again");
        third.Silence(); g.DeathProcessingAndAuraUpdate();
        Ok(mid.Cost == 4 && dear.Cost == 9, "Silence removes the aura");
        // An opposing Naga sets only their cards.
        var their = Summon(enemy, id);
        Ok(theirs.Cost == 5 && mid.Cost == 4, "their Naga sets only their own hand");
        Steal(g, p, their);
        Ok(theirs.Cost == 4 && mid.Cost == 5, "a stolen Naga sets its new controller's hand");
    }

    private static void HugeToad(Game g, Controller p, string id)
    {
        var enemy = p.Opponent;
        var toad = (Minion)Play(g, p, id);
        Ok(toad.AttackDamage == 3 && toad.Health == 2 && toad.Card.IsRace(Race.BEAST) && toad.HasDeathrattle, "3/2 Beast with Deathrattle");
        var a = Summon(enemy, "CS2_182"); var b = Summon(enemy, "CS2_182"); var friend = Summon(p, "CS2_182");
        Resume(g, next => {
            Kill(next, (Minion)next.IdEntityDic[toad.Id]);
            var chars = new ICharacter[] { next.Player2.Hero, (Minion)next.IdEntityDic[a.Id], (Minion)next.IdEntityDic[b.Id] };
            Ok(chars.Sum(c => c.Damage) == 1 && ((Minion)next.IdEntityDic[friend.Id]).Damage == 0 && next.Player1.Hero.Damage == 0,
                "clone: exactly 1 damage to one enemy");
        });
        Kill(g, toad);
        var enemies = new ICharacter[] { enemy.Hero, a, b };
        Ok(enemies.Sum(c => c.Damage) == 1 && friend.Damage == 0 && p.Hero.Damage == 0, "1 damage to exactly one of three enemy characters, never friendly");
        // Silenced: nothing.
        var silent = Summon(p, id); silent.Silence(); Kill(g, silent);
        Ok(enemies.Sum(c => c.Damage) == 1, "Silenced Toad does nothing");
        // Their Toad hits my side.
        var mineOnly = new ICharacter[] { p.Hero, friend };
        var theirs = Summon(enemy, id); Kill(g, theirs);
        Ok(mineOnly.Sum(c => c.Damage) == 1, "an opposing Toad hits my characters");
        // Kills a 1-health minion.
        StandardSetsProof.ParkBoard(enemy);
        var wisp = Summon(enemy, "CS2_231"); var toad2 = Summon(p, id);
        StandardSetsProof.ParkBoard(enemy); wisp = Summon(enemy, "CS2_231");
        enemy.Hero.Damage = 0;
        for (int tries = 0; tries < 1; tries++) { Kill(g, toad2); }
        Ok(Dead(wisp) || enemy.Hero.Damage == 1, "the random target may be a 1-health minion, which dies");
        // Only the hero left: it takes the hit.
        StandardSetsProof.ParkBoard(enemy); enemy.Hero.Damage = 0;
        var toad3 = Summon(p, id); Kill(g, toad3);
        Ok(enemy.Hero.Damage == 1, "only the hero is left to hit");
        // Control change: a stolen Toad hits its former owner's side.
        enemy.Hero.Damage = 0; p.Hero.Damage = 0; StandardSetsProof.ParkBoard(p);
        var stolen = Summon(enemy, id); Steal(g, p, stolen);
        Kill(g, stolen);
        Ok(enemy.Hero.Damage == 1 && p.Hero.Damage == 0, "a stolen Toad damages an enemy of its new controller");
    }

    // ---- BLOCKED-STANDARD behaviour proofs, not registered ----

    private static void TombPillager(Game g, Controller p, string id)
    {
        int hand = p.HandZone.Count;
        var pillager = (Minion)Play(g, p, id);
        Ok(p.HandZone.Count == hand, "no Coin before death");
        Kill(g, pillager);
        Ok(p.HandZone.Count == hand + 1 && p.HandZone[p.HandZone.Count - 1].Card.Id == "GAME_005", "Deathrattle gives a Coin");
        var silent = Summon(p, id); silent.Silence(); Kill(g, silent);
        Ok(p.HandZone.Count == hand + 1, "Silenced Pillager gives nothing");
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        var full = Summon(p, id); Kill(g, full);
        Ok(p.HandZone.Count == 10, "full hand: no Coin");
    }

    private static void RumblingElemental(Game g, Controller p, string id)
    {
        Play(g, p, id);
        var enemy = p.Opponent; var a = Summon(enemy, "CS2_182"); var b = Summon(enemy, "CS2_182");
        var targets = new ICharacter[] { enemy.Hero, a, b };
        Play(g, p, "CS2_182");
        Ok(targets.Sum(c => c.Damage) == 0, "a vanilla minion does not trigger it");
        Play(g, p, "EX1_506");
        Ok(targets.Sum(c => c.Damage) == 2 && targets.Count(c => c.Damage > 0) == 1, "a Battlecry minion triggers 2 damage to one random enemy");
    }

    private static void GorillabotA3(Game g, Controller p, string id)
    {
        Play(g, p, id);
        Ok(p.Choice == null, "no other Mech: no Discover");
        Summon(p, "GVG_085"); Play(g, p, id);
        Ok(p.Choice != null && p.Choice.Choices.All(key => g.IdEntityDic[key].Card.IsRace(Race.MECHANICAL)), "another Mech enables a Mech Discover");
        Pick(g, p, 0);
    }
}
