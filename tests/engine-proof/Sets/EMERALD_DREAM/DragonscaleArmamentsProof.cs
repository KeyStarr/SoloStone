using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class DragonscaleArmamentsProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_251", (g, p, id) => {
        var source = Add(p, id);
        Core2026Proof.Check(source.Card.ConsumesHistory && g.HistoryFacts.HasConsumers, "origin-history consumer enrolled");
        // V1 keeps origin across same-ID form changes, even across CLR type replacement.
        var original = p.DeckZone[0];
        Generic.ChangeEntityBlock(p, original, Cards.FromId("CS2_029"), false);
        original = g.IdEntityDic[original.Id];
        var copy = Generic.Copy(p, source, original, Zone.SETASIDE);
        Generic.ShuffleIntoDeck(p, source, copy);
        var enemySpell = p.Opponent.DeckZone[0];
        Generic.ChangeEntityBlock(p.Opponent, enemySpell, Cards.FromId("CS2_029"), false);
        enemySpell = g.IdEntityDic[enemySpell.Id];
        var foreignMinion = Entity.FromCard(p, Cards.FromId("CS2_182"));
        Generic.ShuffleIntoDeck(p, source, foreignMinion);
        End(g); End(g);
        foreach (var card in p.HandZone.Where(c => c.Id != source.Id).ToArray())
            p.SetasideZone.Add(p.HandZone.Remove(card));
        var before = g.SaveSnapshot();
        var clone = g.Clone();
        var cloneOriginal = clone.IdEntityDic[original.Id];
        clone.Player1.SetasideZone.Add(clone.Player1.DeckZone.Remove(cloneOriginal));
        PlayEntity(clone, clone.Player1, clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(clone.IdEntityDic[copy.Id].Zone == clone.Player1.HandZone
            && cloneOriginal.Zone == clone.Player1.SetasideZone, "clone draws only remaining foreign partition");
        Core2026Proof.Check(before.SequenceEqual(g.SaveSnapshot()), "clone draw and deck mutation leave original bytes unchanged");
        PlayEntity(g, p, source);
        Core2026Proof.Check(p.HandZone.Select(c => c.Id).SequenceEqual(new[] { original.Id, copy.Id }),
            "same printed spell drawn original first, generated copy second, across turns");
        Core2026Proof.Check(original.Card.Id == "CS2_029" && original.Cost == 4 && copy.Cost == 4
            && p.Opponent.DeckZone.Contains(enemySpell) && foreignMinion.Zone == p.DeckZone,
            "exact spell identity; opposing deck and foreign minion excluded");
        Core2026Proof.Check(!g.HistoryFacts.Query.StartedInDeck(copy.Id, p.Id), "copy of starting spell stays foreign");
        Generic.ShuffleIntoDeck(p, p.Hero, p.HandZone.Remove(original));
        var late = Add(p, id);
        PlayEntity(g, p, late);
        Core2026Proof.Check(original.Zone == p.HandZone && copy.Zone == p.HandZone,
            "returned original retains origin before late-created consumer; missing foreign category adds no draw");
        Core2026Proof.Check(g.HistoryFacts.Query.HandEntries().Count(f => f.EntityId == original.Id && f.EntryKind == HistoryHandEntryKind.Draw) == 2,
            "original drawn twice as same physical entity");

        Boundaries(g);
        OpponentOrigin(g);
        StartingSpells(g.Seed);
        HiddenPermutation();
        MaskedDraw(g);
    });

    private static void OpponentOrigin(Game template)
    {
        var game = template.CloneExact(history: true); var p = game.Player1;
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        var stolen = p.Opponent.DeckZone[0];
        Generic.ChangeEntityBlock(p.Opponent, stolen, Cards.FromId("CS2_029"), false);
        stolen = p.Opponent.DeckZone.Remove(game.IdEntityDic[stolen.Id]);
        stolen.Controller = p;
        Generic.ShuffleIntoDeck(p, p.Hero, stolen);
        Play(game, p, "EDR_251");
        Core2026Proof.Check(p.HandZone.Single().Id == stolen.Id
            && game.HistoryFacts.Query.StartedInDeck(stolen.Id, p.Opponent.Id)
            && !game.HistoryFacts.Query.StartedInDeck(stolen.Id, p.Id),
            "opponent's starting spell shuffled into own deck belongs only to foreign partition");
    }

    private static void Boundaries(Game template)
    {
        foreach (bool initial in new[] { false, true })
        foreach (bool foreign in new[] { false, true })
        foreach (bool fullHand in new[] { false, true })
        {
            var game = template.CloneExact(history: true); var p = game.Player1;
            foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
            foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
            // An enrolled original can return to the deck; a newly generated same-dbf copy cannot inherit it.
            var original = p.SetasideZone.First(c => c.Card.Id == "CS2_029"
                && game.HistoryFacts.Query.StartedInDeck(c.Id, p.Id));
            if (initial) Generic.ShuffleIntoDeck(p, p.Hero, p.SetasideZone.Remove(original));
            var generated = Entity.FromCard(p, Cards.FromId("CS2_029"));
            if (foreign) Generic.ShuffleIntoDeck(p, p.Hero, generated);
            var source = Add(p, "EDR_251");
            if (fullHand) while (!p.HandZone.IsFull) Add(p, "CS2_231");
            int fatigue = p.Hero.Fatigue, health = p.Hero.Health, draws = p.NumCardsDrawnThisTurn;
            PlayEntity(game, p, source);
            Core2026Proof.Check(p.Hero.Fatigue == fatigue && p.Hero.Health == health, "absent categories never fatigue, including empty deck");
            Core2026Proof.Check(p.NumCardsDrawnThisTurn == draws + (initial ? 1 : 0) + (foreign ? 1 : 0),
                "each present partition draws once; missing categories never draw an arbitrary card");
            Core2026Proof.Check(!initial || original.Zone == p.HandZone, "original fills first hand slot");
            Core2026Proof.Check(!foreign || generated.Zone.Type == (fullHand && initial ? Zone.GRAVEYARD : Zone.HAND),
                "second spell burns only when first draw fills hand");
            Core2026Proof.Check(p.DeckZone.IsEmpty, "both available partitions removed from deck");
        }
    }

    private static void StartingSpells(long seed)
    {
        var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_029"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame(); var p = game.Player1;
        var originals = p.DeckZone.Select(c => c.Id).ToHashSet();
        var generated = Entity.FromCard(p, Cards.FromId("CS2_029"));
        Generic.ShuffleIntoDeck(p, p.Hero, generated);
        var source = Add(p, "EDR_251");
        int count = p.HandZone.Count;
        PlayEntity(game, p, source);
        Core2026Proof.Check(p.HandZone.Count == count + 1 && generated.Zone == p.HandZone
            && p.HandZone.Count(c => originals.Contains(c.Id)) == 1,
            "real starting-deck spells partition from identical newly shuffled spell before card creation");
        Core2026Proof.Check(Canonical.State(game) == Canonical.State(Game.LoadSnapshot(game.SaveSnapshot())),
            "real starting-deck origin and future recipe survive snapshot");
    }

    private static void HiddenPermutation()
    {
        foreach (bool inDeck in new[] { false, true })
        {
            Game Fixture(bool withCard)
            {
                var game = HistoryFactsProof.Fixture(); var owner = game.Player2;
                var hidden = Entity.FromCard(owner, Cards.FromId(withCard ? "EDR_251" : "CS2_182"));
                if (inDeck) owner.DeckZone.Add(hidden); else owner.HandZone.Add(hidden);
                game.PowerHistory.Last.Clear();
                Generic.ShuffleIntoDeck(owner, owner.Hero, Entity.FromCard(owner, Cards.FromId("CS2_029")));
                game.AuraUpdate();
                return game;
            }
            var a = Fixture(true); var b = Fixture(false);
            Core2026Proof.Check(a.PowerHistory.Print(false) == b.PowerHistory.Print(false), "hidden consumer emits no shuffle blocks/tags");
            var fa = FairBotState.Create(a, a.Player1.PlayerId, 42, DeckStyle.Control);
            var fb = FairBotState.Create(b, b.Player1.PlayerId, 42, DeckStyle.Control);
            var ga = HistoryFairOriginProof.FairGame(fa); var gb = HistoryFairOriginProof.FairGame(fb);
            Core2026Proof.Check(ga.HistoryFacts.CanonicalText() == gb.HistoryFacts.CanonicalText()
                && Canonical.State(ga) == Canonical.State(gb) && fa.Evaluate() == fb.Evaluate()
                && fa.Actions.Select(x => x.ToString()).SequenceEqual(fb.Actions.Select(x => x.ToString())),
                "hidden card presence leaves projected state, facts, evaluation and actions equal");
        }
    }

    private static void MaskedDraw(Game template)
    {
        var game = template.CloneExact(history: true); var p = game.Player1;
        var source = Add(p, "EDR_251");
        var other = game.CloneExact(history: true);
        var hidden = other.Player1.DeckZone[0];
        Generic.ChangeEntityBlock(other.Player1, hidden, Cards.FromId("CS2_029"), false);
        var before = game.SaveSnapshot();
        var fa = FairBotState.Create(game, p.PlayerId, 88, DeckStyle.Control);
        var fb = FairBotState.Create(other, p.PlayerId, 88, DeckStyle.Control);
        foreach (var fair in new[] { fa, fb })
        {
            var projected = HistoryFairOriginProof.FairGame(fair);
            int hand = projected.Player1.HandZone.Count;
            PlayEntity(projected, projected.Player1, projected.IdEntityDic[source.Id]);
            Core2026Proof.Check(projected.Player1.HandZone.Count == hand + 1
                && projected.Player1.HandZone.Count(c => c is Unknown) == 2 && !fair.CanExpand,
                "both masked origin draws anonymous; unknown hand stops bot expansion");
        }
        Core2026Proof.Check(Canonical.State(HistoryFairOriginProof.FairGame(fa)) == Canonical.State(HistoryFairOriginProof.FairGame(fb)),
            "masked partitions never inspect hidden spell identities or origin counts");
        Core2026Proof.Check(before.SequenceEqual(game.SaveSnapshot()), "masked simulation leaves authoritative snapshot unchanged");
    }
}
