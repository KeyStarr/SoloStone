using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

/// <summary>Integration 2026-10-20: the history store composes with Dark Gift, Outcast, Death Knight
/// Corpses, selector decision spells and Deathrattle-slot summons.</summary>
internal static class HistoryCompositionProof
{
    private static void Check(bool ok, string message) => Core2026Proof.Check(ok, "history composition: " + message);
    private static void Deck(Controller p, params string[] ids)
    {
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        foreach (string id in ids.Reverse()) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId(id)));
    }
    private static SpellCastFact[] CastsOf(Game g, IEntity e) => g.HistoryFacts.Query.Casts().Where(f => f.EntityId == e.Id).ToArray();

    internal static void Run()
    {
        DecisionSpellCastOnce();
        DecisionSpellWithoutPrompt();
        DarkGiftDiscover();
        OutcastPlay();
        CorpsesAndDeaths();
        DeathrattleSlotSummon();
        TransportRegressions();
        Console.WriteLine("PASS history composition: selector cast once (suspended/snapshot/inline), Dark Gift Discover, Outcast play, Corpses, Deathrattle slot summons, Mayor-retargeted Sylvanas's Triumph, empty AttackTask");
    }

    // Selector decision spell: the spell is held out of the graveyard until the answer, but its one Player cast
    // completes when the recipe first returns, so a mid-decision snapshot validates and the answer adds no cast.
    private static void DecisionSpellCastOnce()
    {
        foreach (bool snapshot in new[] { false, true })
        {
            var g = PlayerDecisionProof.Fixture(19); var p = g.Player1;
            Deck(p, "CS2_182", "CS2_029"); int pick = p.DeckZone.TopCard.Id;
            int afterCast = 0; IPlayable source = null;
            // Trigger subscriptions are not snapshot continuations: observe after-cast timing only without snapshots.
            if (!snapshot) g.TriggerManager.AfterCastTrigger += e => {
                if (source == null || e.Id != source.Id) return;
                afterCast++;
                Check(CastsOf(g, e).Count(f => f.Outcome == HistoryCastOutcome.Completed) == 1, "Completed visible before after-cast trigger");
            };
            source = Play(g, p, "TIME_770");
            Check(p.Choice != null && source.Zone != p.GraveyardZone && g.SpellCompletion != null, "selector prompt pending, spell held");
            var casts = CastsOf(g, source);
            Check(casts.Length == 2 && casts[0].Outcome == HistoryCastOutcome.Started && casts[1].Outcome == HistoryCastOutcome.Completed
                && casts.All(f => f.CastId == casts[0].CastId && f.CasterKind == HistoryCastKind.Player), "one Player cast started and completed while pending");
            Check(g.HistoryFacts.Query.Plays().Count(f => f.EntityId == source.Id) == 1, "one play fact");
            var games = new List<Game> { g };
            if (snapshot)
            {
                var restored = Game.LoadSnapshot(g.SaveSnapshot());
                Check(restored.HistoryFacts.CanonicalText() == g.HistoryFacts.CanonicalText(), "mid-decision snapshot keeps the store");
                games.Add(restored); games.Add(g.CloneExact(history: true));
            }
            foreach (var game in games)
            {
                PlayerDecisionProof.Answer(game, pick);
                var spell = game.IdEntityDic[source.Id];
                var q = game.HistoryFacts.Query;
                Check(spell.Zone == spell.Controller.GraveyardZone && game.SpellCompletion == null, "spell finishes after answer");
                Check(CastsOf(game, spell).Length == 2, "answer adds no cast facts");
                Check(q.Casts(q.ThisTurn).Count(f => f.ControllerId == spell.Controller.Id && f.Outcome == HistoryCastOutcome.Completed
                    && f.CasterKind == HistoryCastKind.Player) == 1 && spell.Controller.NumSpellsPlayedThisGame == 1, "spells cast this turn counts it once");
                Check(game.HistoryFacts.CanonicalText() == g.HistoryFacts.CanonicalText(), "answer paths agree on the store");
            }
            if (!snapshot) Check(afterCast == 1, "after-cast fired once");
        }
    }

    // No candidates: the decision recipe finishes inline; Completed precedes FinishSpellPlay's after-cast triggers.
    private static void DecisionSpellWithoutPrompt()
    {
        var g = PlayerDecisionProof.Fixture(19); var p = g.Player1; Deck(p);
        IPlayable source = null; int afterCast = 0;
        g.TriggerManager.AfterCastTrigger += e => {
            if (source == null || e.Id != source.Id) return;
            afterCast++;
            Check(CastsOf(g, e).Count(f => f.Outcome == HistoryCastOutcome.Completed) == 1, "inline Completed before after-cast");
        };
        source = Add(p, "TIME_770"); PlayEntity(g, p, source);
        Check(p.Choice == null && g.SpellCompletion == null && afterCast == 1, "no prompt; finished inline");
        var casts = CastsOf(g, source);
        Check(casts.Length == 2 && casts[1].Outcome == HistoryCastOutcome.Completed, "inline decision spell cast once");
    }

    // Dark Gift Discover (hand spell, offline pool) is a semantic Discover.
    private static void DarkGiftDiscover()
    {
        var g = PlayerDecisionProof.Fixture(19); var p = g.Player1;
        int before = g.HistoryFacts.Query.Discoveries().Count();
        Play(g, p, "EDR_882");
        Check(p.Choice != null && DarkGifts.IsGiftChoice(p.Choice), "Dark Gift Discover pending");
        var restored = Game.LoadSnapshot(g.SaveSnapshot());
        foreach (var game in new[] { g, restored })
        {
            int pick = game.Player1.Choice.Choices[0];
            PlayerDecisionProof.Answer(game, pick);
            var found = game.HistoryFacts.Query.Discoveries().ToArray();
            Check(found.Length == before + 1 && found.Last().ControllerId == game.Player1.Id && found.Last().SelectedEntityIds.SequenceEqual(new[] { pick }),
                "Dark Gift pick recorded as one Discover");
        }
    }

    // Outcast hand edge: one play, one Player cast, and both draws are Draw hand entries.
    private static void OutcastPlay()
    {
        var g = PlayerDecisionProof.Fixture(19); var p = g.Player1;
        long seq = g.HistoryFacts.Query.Events<HistoryFact>().LastOrDefault()?.Sequence ?? 0;
        var source = Play(g, p, "CORE_BT_491");
        var q = g.HistoryFacts.Query;
        var draws = q.HandEntries().Where(f => f.Sequence > seq && f.EntryKind == HistoryHandEntryKind.Draw).ToArray();
        Check(q.Plays().Count(f => f.EntityId == source.Id) == 1 && CastsOf(g, source).Length == 2, "Outcast play/cast once");
        Check(draws.Length == 2 && p.HandZone.Count == 2, "Outcast draws two recorded draws");
    }

    // Corpses: each board death is one death fact and one Corpse; committed spending is separately attributed.
    private static void CorpsesAndDeaths()
    {
        var g = new Game(new GameConfig { EnableCorpses = true, RandomSeed = 31, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            History = true, Logging = false, Player1HeroClass = CardClass.DEATHKNIGHT, Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        g.StartGame(); var p = g.Player1;
        var a = Summon(p, "CS2_182"); var b = Summon(p, "CS2_182");
        Kill(g, a); Kill(g, b);
        var deaths = g.HistoryFacts.Query.Deaths().Where(f => f.ControllerId == p.Id).ToArray();
        Check(deaths.Select(f => f.EntityId).SequenceEqual(new[] { a.Id, b.Id }) && CorpseRules.Available(p) == 2, "deaths and Corpses agree");
        int count = g.HistoryFacts.Count;
        Check(CorpseRules.TrySpend(p, 2) && CorpseRules.Available(p) == 0 && g.HistoryFacts.Count == count+1
            && g.HistoryFacts.Query.CorpsesSpent().Single().Amount==2, "Corpse spending appends one exact fact");
        Game.LoadSnapshot(g.SaveSnapshot());
    }

    // Deathrattle summons land in the dying slot; the death fact precedes the unplayed summon fact.
    private static void DeathrattleSlotSummon()
    {
        var g = PlayerDecisionProof.Fixture(19); var p = g.Player1;
        var left = Summon(p, "CS2_182"); var egg = Summon(p, "TLC_831"); var right = Summon(p, "CS2_182");
        long seq = g.HistoryFacts.Query.Events<HistoryFact>().LastOrDefault()?.Sequence ?? 0;
        Kill(g, egg);
        var q = g.HistoryFacts.Query;
        var death = q.Deaths().Single(f => f.Sequence > seq);
        var summon = q.Summons().Single(f => f.Sequence > seq);
        var token = (Minion)g.IdEntityDic[summon.EntityId];
        Check(death.EntityId == egg.Id && death.Sequence < summon.Sequence && summon.FromPlayId == 0, "death then unplayed summon");
        Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, token.Id, right.Id }), "summon fills the dying slot");
        Check(!q.Plays().Any(f => f.EntityId == token.Id), "a Deathrattle summon is not a play");
    }

    // Strict-transport regressions from integration stress decks (2026-10-20 cost-rules merge).
    private static void TransportRegressions()
    {
        // Mayor Noggenfogger re-randomizes a first Sylvanas's Triumph while its own play fact is already recorded.
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        var mayor = Summon(p, "CFM_670"); var enemy = Summon(p.Opponent, "CS2_182");
        int before = p.Hero.Damage + p.Opponent.Hero.Damage + mayor.Damage + enemy.Damage;
        var triumph = Add(p, "CATA_557");
        Check(triumph.Card.TargetingAvailabilityPredicate(p, triumph.Card), "first Triumph targets in hand");
        PlayEntity(g, p, triumph, enemy);
        int dealt = p.Hero.Damage + p.Opponent.Hero.Damage + (mayor.Zone == p.BoardZone ? mayor.Damage : 4) + (enemy.Zone == p.Opponent.BoardZone ? enemy.Damage : 5) - before;
        Check(dealt == 3 && g.HistoryFacts.Query.Plays().Count(f => f.EntityId == triumph.Id) == 1, "Mayor retargets the first Triumph to one legal target");
        var second = Add(p, "CATA_557");
        Check(!second.Card.TargetingAvailabilityPredicate(p, second.Card), "a later Triumph still sees the earlier play");
        // A random cast whose attack task finds no attacker or defender stops instead of indexing an empty list.
        Check(new AttackTask(EntityType.STACK, EntityType.STACK).Process(g, p, p.Hero, null, new TaskStack { Playables = new List<IPlayable>() }) == TaskState.STOP,
            "empty AttackTask stops");
    }
}
