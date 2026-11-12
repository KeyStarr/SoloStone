using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Rewind;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static RewindTestHelpers;

// Catches per-card replay duplication, lost nested continuations and snapshot-only futures.
internal static class RewindCardsProof
{
    private static void Check(bool ok, string message) => Core2026Proof.Check(ok, "Rewind smoke: " + message);
    internal static readonly string[] Ids = ("TIME_000 TIME_001 TIME_002 TIME_003 TIME_004 TIME_008 TIME_014 TIME_018 "
        + "TIME_033 TIME_034 TIME_038 TIME_433 TIME_441 TIME_602 CORE_EDR_004 CORE_EDR_004_2026").Split(' ');

    private static bool Herald(string id) => id.StartsWith("CORE_EDR_004", StringComparison.Ordinal);
    private static CardClass Hero(string id) => id switch {
        "TIME_000" => CardClass.MAGE, "TIME_001" => CardClass.ROGUE, "TIME_008" => CardClass.WARLOCK,
        "TIME_014" => CardClass.SHAMAN, "TIME_018" => CardClass.PALADIN, "TIME_033" => CardClass.DRUID,
        "TIME_034" => CardClass.WARRIOR, "TIME_433" => CardClass.PRIEST, "TIME_441" => CardClass.DEMONHUNTER,
        "TIME_602" => CardClass.HUNTER, _ => CardClass.HUNTER
    };

    private static (Game Game, int Source, int Cost, int Deck) Open(int seed, string id)
    {
        var game = PinnedPoolFixture.Use(GiftedDiscoverProof.New(seed, Hero(id))); var p = game.Player1;
        if (id == "TIME_018") p.Hero.Damage = 20;
        if (id is "TIME_001" or "TIME_004" or "TIME_433" or "TIME_441" or "TIME_033" or "TIME_602")
            for (int i = 0; i < 3; i++) Summon(p.Opponent, "CS2_232");
        if (id == "TIME_033") Summon(p, "CS2_182");
        if (id == "TIME_008")
            for (int i = 0; i < 3; i++) { Add(p, "CS2_182"); Add(p.Opponent, "CS2_171"); }
        var source = Add(p, id); int deck = p.DeckZone.Count;
        if (id == "TIME_033")
        {
            // Six crystals leave no spare mana for a random CATA_135 to spend.
            p.BaseMana = 6; p.UsedMana = 0;
            Check(game.Process(p.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id)), "Druid legal hand play");

        }
        else PlayEntity(game, p, source);
        if (Herald(id))
        {
            Check(!IsRewindOffered(game) && game.Rewind?.Stage == RewindStage.Attempt && game.Resolution.HasSuspendedEffect,
                id + " Discover precedes Rewind offer");
            AnswerDiscover(game);
        }
        Offered(game, source.Id);
        Outcome(game, id, source.Id, deck);
        return (game, source.Id, source.Card.Cost, deck);
    }

    private static void Offered(Game game, int source)
    {
        Check(IsRewindOffered(game) && !game.Resolution.HasSuspendedEffect && game.TaskQueue.IsQuiescent,
            "Rewind offered only at settled boundary");
        PendingDecision decision = game.GetPendingDecisions().Single();
        Check(decision.SourceId == source && decision.MinSelections == 1 && decision.MaxSelections == 1 &&
            decision.Options.Select(o => game.IdEntityDic[o.EntityId].Card.Id).Order().SequenceEqual(new[] { "TIME_000ta", "TIME_000tb" }),
            "one mandatory Keep/Rewind offer for source");
    }

    private static long AnswerDiscover(Game game)
    {
        Check(game.Resolution.HasSuspendedEffect && !IsRewindOffered(game), "fresh nested Discover pending");
        var decision = game.GetPendingDecisions().Single();
        Check(game.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId,
            new[] { decision.Options[0].OptionId }), out var error), "nested Discover answer: " + error);
        return decision.Id;
    }

    private static void Accounting(Game game, string id, int source, int cost, int used, int resets)
    {
        var p = game.Player1; var entity = game.IdEntityDic[source];
        Check(p.UsedMana == cost && p.NumCardsPlayedThisTurn == 1 && p.PlayHistory.Count == 1 &&
            game.HistoryFacts.Query.Plays().Count(f => f.EntityId == source) == 1,
            id + " single net payment, cards-played counter and CardPlayedFact");
        // Pool growth can select Typhoon: it shuffles the source and resets its instance tags.
        bool shuffled = id == "TIME_033" && game.HistoryFacts.Query.Casts()
            .Any(f => f.DbfId == 114357 && f.Outcome == HistoryCastOutcome.Completed);
        Check(entity[GameTag.USED_REWIND] == (shuffled ? 0 : used) && (shuffled ? entity.Zone.Type == Zone.DECK :
            entity.Card.Type == CardType.SPELL ? entity.Zone.Type == Zone.GRAVEYARD :
            (id == "TIME_033" ? entity.Zone.Type is Zone.PLAY or Zone.GRAVEYARD : entity.Zone.Type == Zone.PLAY)), id + " source identity, zone and USED_REWIND");
        Check(game.PowerHistory.Full.Count(e => e is PowerHistoryResetGame) == resets &&
            game.PowerHistory.Full.Count(e => e is PowerHistoryBlockStart b && b.BlockType == BlockType.GAME_RESET) == resets,
            id + " exactly one GAME_RESET block and payload per rewind");
    }

    private static void Outcome(Game game, string id, int source, int deck)
    {
        var p = game.Player1;
        int damage = p.Opponent.Hero.Damage + p.Opponent.BoardZone.Sum(m => m.Damage);
        switch (id)
        {
            case "TIME_000": Check(p.HandZone.Count == 1 && p.HandZone[0] is Minion &&
                p.HandZone[0].Cost == Math.Max(0, p.HandZone[0].Card.Cost - 3), "Portal one discounted outcome"); break;
            case "TIME_001": Check(damage == 6, "Daggers one six-damage outcome"); break;
            case "TIME_002": Check(p.HandZone.Count == 2 && p.HandZone.All(c => c.Card.Type == CardType.SPELL &&
                game.OfflinePools.Standard.Single(m => m.CardId == c.Card.Id).Classes.Contains(p.HeroClass.ToString())), "Wizard two own-class spells"); break;
            case "TIME_003": Check(p.HandZone.Count == 1 && p.DeckZone.Count == deck - 1 && p.HandZone[0] is Minion drawn &&
                drawn.AppliedEnchantments.Single().Card.Id == "TIME_003e" && drawn.AttackDamage == drawn.Card.ATK + 2 &&
                drawn.BaseHealth == drawn.Card.Health + 2, "Vanguard one physical draw and buff"); break;
            case "TIME_004": Check(damage == 7, "Crasher one seven-damage outcome"); break;
            case "TIME_008": Check(p.HandZone.Count == 2 && p.Opponent.HandZone.Count == 2 &&
                p.DiscardedEntities.Count == 1 && p.Opponent.DiscardedEntities.Count == 1, "Doomspeaker one discard per player"); break;
            case "TIME_014": Check(p.OverloadOwed == 3 && p.BoardZone.Count > 0 && p.BoardZone.Sum(m => m.Card.Cost) <= 12,
                "Multiverse one budget and one Overload"); break;
            case "TIME_018": Check(p.HandZone.Count == 2 && p.HandZone.All(c => c.Card[GameTag.SPELL_SCHOOL] == 5) &&
                p.Hero.Damage == Math.Max(0, 20 - p.HandZone.Sum(c => c.Cost)), "Mend two Holy spells and one heal"); break;
            case "TIME_033": Check(game.HistoryFacts.Query.Casts().Count(f => f.CasterKind == SabberStoneCore.Model.HistoryCastKind.Effect && f.Outcome == SabberStoneCore.Model.HistoryCastOutcome.Completed) == 2,
                "Druid exactly two effect casts"); break;
            case "TIME_034": Check(p.Hero.Weapon != null && p.Opponent.Hero.Weapon != null &&
                p.Hero.Weapon.AppliedEnchantments.Count(e => e.Card.Id == "TIME_034e") == 1,
                "Announcer one weapon per player and one own buff"); break;
            case "TIME_038": Check(p.BoardZone.Count >= 3 && p.BoardZone.Count <= 4 &&
                p.BoardZone.Where(m => m.Id != source).All(m => m.Card.Rarity == Rarity.LEGENDARY), "Clocksworth one summon pair (including summon aura)"); break;
            case "TIME_433": Check(p.Opponent.BoardZone.Count == 2 && p.Opponent.GraveyardZone.OfType<Minion>().Count(m => m.IsSilenced) == 1,
                "Cease one silenced and destroyed enemy"); break;
            case "TIME_441": Check(damage == 8 && p.Opponent.BoardZone.Count(m => m.Damage == 4) + (p.Opponent.Hero.Damage == 4 ? 1 : 0) == 2,
                "Aeon Rend two distinct four-damage targets"); break;
            case "TIME_602": Check(p.BoardZone.Concat(p.GraveyardZone.OfType<Minion>()).Count(m => m.Card.Cost == 3 && m.IsRace(Race.BEAST)
                && m.NumAttacksThisTurn == 0) == 1
                && game.HistoryFacts.Query.AttacksFinished().Count(f => f.Origin == HistoryAttackOrigin.Forced && f.ReachedCombat) == 1,
                "Wormhole one Beast and one immediate attack without spending normal allowance"); break;
            default: Check(Herald(id) && game.HistoryFacts.Query.Discoveries().Count() == 1 &&
                p.HandZone.Count + p.DeckZone.Count - deck == 1 && !game.Resolution.HasSuspendedEffect,
                "Raptor one saved Discover grant (including Sweet Dreams)"); break;
        }
    }

    private static string Finish(Game game, string id, int source, int cost, int deck, bool rewind)
    {
        int history = game.PowerHistory.Full.Count;
        var offer = game.GetPendingDecisions().Single();
        int keepId = offer.Options.Single(o => game.IdEntityDic[o.EntityId].Card.Id == "TIME_000ta").OptionId;
        long discover = 0;
        if (rewind)
        {
            var attemptOnly = game.IdEntityDic.Select(e => e.Key).Except(PinnedPoolFixture.LoadSnapshot(game.Rewind.Baseline).IdEntityDic.Select(e => e.Key)).ToArray();
            long cursor = game.Random.DrawCount;
            int plays = game.PowerHistory.Full.Count(e => e is PowerHistoryBlockStart b && b.BlockType == BlockType.PLAY && b.Source == source);
            AnswerRewind(game);
            Check(attemptOnly.All(i => !game.IdEntityDic.ContainsKey(i)) && game.Random.DrawCount > cursor &&
                game.PowerHistory.Full.Count(e => e is PowerHistoryBlockStart b && b.BlockType == BlockType.PLAY && b.Source == source) == plays + 1,
                id + " attempt-only entities erased; source replayed with fresh RNG draws");
            if (Herald(id))
            {
                Check(!IsRewindOffered(game) && game.Rewind?.Stage == RewindStage.Attempt,
                    "replay waits for fresh Discover, not second Rewind offer");
                discover = AnswerDiscover(game);
                Check(discover > offer.Id, "replayed Discover has fresh monotonic id");
            }
            if (id == "TIME_038") { Offered(game, source); KeepIfOffered(game); }
        }
        else KeepIfOffered(game);
        Check(game.Rewind == null && game.Player1.Choice == null && !IsRewindOffered(game), "transaction ends; no second offer");
        var bytes = game.SaveSnapshot();
        Check(!game.TryAnswer(new DecisionAnswer(offer.Id, offer.ControllerId, new[] { keepId }), out _) &&
            bytes.SequenceEqual(game.SaveSnapshot()), "offer answered exactly once; duplicate answer has zero mutation");
        Accounting(game, id, source, cost, rewind ? 1 : 0, rewind ? 1 : 0);
        Outcome(game, id, source, deck);
        return Canonical.State(game) + Canonical.Value(game.Random.CaptureState()) +
            Canonical.Value(game.PowerHistory.Full.Skip(history));
    }

    private static void Futures(int seed, string id, bool rewind)
    {
        var (game, source, cost, deck) = Open(seed, id);
        byte[] bytes = game.SaveSnapshot(); var load = PinnedPoolFixture.LoadSnapshot(bytes); var clone = game.CloneExact(history: true);
        Check(bytes.SequenceEqual(load.SaveSnapshot()), id + " offered snapshot byte round trip");
        string expected = Finish(game, id, source, cost, deck, rewind);
        Check(expected == Finish(load, id, source, cost, deck, rewind) && expected == Finish(clone, id, source, cost, deck, rewind),
            id + " Keep/Rewind original, loaded and exact-clone futures include RNG and history");
    }

    private static void Chain(int seed)
    {
        var (game, source, cost, deck) = Open(seed, "TIME_038");
        long previous = 0;
        for (int used = 1; used <= 3; used++)
        {
            var offer = game.GetPendingDecisions().Single(); Check(offer.Id > previous, "chain offers fresh ids"); previous = offer.Id;
            AnswerRewind(game);
            Accounting(game, "TIME_038", source, cost, used, used); Outcome(game, "TIME_038", source, deck);
            Check(game.IdEntityDic[source].Card.Id == "TIME_038t" + used, "Clocksworth chain variant");
            Check(IsRewindOffered(game) == (used < 3), "exactly three Clocksworth rewinds");
        }
        Check(game.Rewind == null, "Clocksworth exhausted transaction ends");
    }

    private static void Morchie(int seed)
    {
        foreach (string mode in new[] { "active", "silenced", "removed" })
        {
            var game = PinnedPoolFixture.Use(GiftedDiscoverProof.New(seed, CardClass.ROGUE)); var p = game.Player1;
            var morchie = Summon(p, "END_036");
            if (mode == "silenced") { morchie.Silence(); game.DeathProcessingAndAuraUpdate(); }
            if (mode == "removed") Kill(game, morchie);
            var source = Play(game, p, "TIME_001"); Offered(game, source.Id);
            byte[] bytes = game.SaveSnapshot(); var load = PinnedPoolFixture.LoadSnapshot(bytes); var clone = game.CloneExact(history: true);
            foreach (var next in new[] { game, load, clone })
            {
                AnswerRewind(next);
                Check(next.Player2.Hero.Damage == (mode == "active" ? 12 : 6) && next.Rewind == null && next.Player1.Choice == null,
                    "Morchie " + mode + " applies both outcomes only while active and ends transaction");
                Accounting(next, "TIME_001", source.Id, 3, mode == "active" ? 0 : 1, mode == "active" ? 0 : 1);
            }
            Check(Canonical.State(game) == Canonical.State(load) && Canonical.State(game) == Canonical.State(clone) &&
                game.Random.CaptureState().Equals(load.Random.CaptureState()) && game.Random.CaptureState().Equals(clone.Random.CaptureState()),
                "Morchie offered snapshot/clone futures");
        }
    }

    private static void Generators(int seed)
    {
        foreach (string id in new[] { "TIME_035", "END_036" })
        {
            var game = PinnedPoolFixture.Use(GiftedDiscoverProof.New(seed, CardClass.MAGE)); var p = game.Player1;
            // No admissions added: use existing transient catalog seam, restored before generated hand play.
            RewindSliceProof.WithRewindPool(game, () => {
                if (id == "TIME_035") Kill(game, (Minion)Play(game, p, id));
                else
                {
                    Play(game, p, id);
                    Check(!IsRewindOffered(game) && p.Choice.Choices.All(i => game.IdEntityDic[i].Card[GameTag.REWIND] == 1),
                        "Morchie offers only own-tag Rewind cards");
                    AnswerDiscover(game);
                    ((Minion)p.BoardZone.Single(m => m.Card.Id == id)).Silence(); game.DeathProcessingAndAuraUpdate();
                }
                Check(game.Rewind == null && p.HandZone.Count == 1 && p.HandZone[0].Card[GameTag.REWIND] == 1,
                    id + " has no own Rewind; generates only actual Rewind card");
            });
            int source = p.HandZone.Single().Id;
            PlayEntity(game, p, game.IdEntityDic[source]); Offered(game, source);
            AnswerRewind(game);
            Check(game.Rewind == null && game.IdEntityDic[source][GameTag.USED_REWIND] == 1 &&
                game.PowerHistory.Full.Count(e => e is PowerHistoryResetGame) == 1, "generated Rewind card participates in normal transaction");
        }
    }

    private static void Indirect(int seed)
    {
        foreach (string id in Ids.Where(id => Cards.FromId(id).Type == CardType.SPELL))
        {
            var game = PinnedPoolFixture.Use(GiftedDiscoverProof.New(seed, Hero(id)));
            var spell = (Spell)Entity.FromCard(game.Player1, Cards.FromId(id));
            Summon(game.Player2, "CS2_182");
            Generic.CastSpell(game.Player1, game, spell, null, 0); game.DeathProcessingAndAuraUpdate();
            Check(game.Rewind == null && !IsRewindOffered(game) && game.Player1.Choice == null &&
                game.PowerHistory.Full.All(e => e is not PowerHistoryResetGame) && game.Player1.NumCardsPlayedThisTurn == 0,
                id + " effect cast never opens Rewind or records hand play");
        }
        var druid = PinnedPoolFixture.Use(GiftedDiscoverProof.New(seed, CardClass.DRUID));
        TimeTravelS2Proof.WithCards(druid, new[] { "CORE_EX1_169" }, () => {
            var source = Summon(druid.Player1, "TIME_033"); PoolPilotProof.Resolve(druid, source);
            Check(druid.Player1.TemporaryMana == 2 && druid.Rewind == null && !IsRewindOffered(druid),
                "Druid effect-only Battlecry casts twice without hand-play offer");
        });
    }

    internal static void Run()
    {
        var failures = new List<string>();
        foreach (string id in Ids)
        {
            try
            {
                for (int seed = 1; seed <= 3; seed++) { Futures(seed, id, false); Futures(seed, id, true); }
                Console.WriteLine("PASS Rewind smoke " + id + ": Keep, replay, offered load/clone; seeds=1-3");
            }
            catch (Exception e) { failures.Add(id + ": " + e); }
        }
        foreach (string feature in new[] { "Clocksworth chain", "Morchie", "generators", "indirect" })
        {
            try
            {
                for (int seed = 1; seed <= 3; seed++)
                    switch (feature) { case "Clocksworth chain": Chain(seed); break; case "Morchie": Morchie(seed); break;
                        case "generators": Generators(seed); break; default: Indirect(seed); break; }
                Console.WriteLine("PASS Rewind smoke " + feature + "; seeds=1-3");
            }
            catch (Exception e) { failures.Add(feature + ": " + e); }
        }
        Check(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine("rewind-smoke: cards=18/18 (16 Rewind + 2 generators); seeds=3; PASS");
    }
}
