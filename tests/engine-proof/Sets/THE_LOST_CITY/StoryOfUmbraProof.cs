using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static DeathKnightC1Proof;
using static PoolPilotProof;

internal static class StoryOfUmbraProof
{
    private static void Answer(Game game)
    {
        var view = game.GetPendingDecisions().Single();
        var answer = new SabberStoneCore.Model.Decisions.DecisionAnswer(view.Id, view.ControllerId, new[] { view.Options.First().OptionId });
        Core2026Proof.Check(game.TryAnswer(answer, out var error), error);
    }

    internal static void Run() => DeathKnightC1Proof.Case("DINO_415", (g, p, id) => {
        Core2026Proof.Check(CorpseRules.Available(p) == 0, "zero Corpses does not gate Story");
        Pool(g, id, 0, () => {
            var random = g.Random.CaptureState(); int entities = g.IdEntityDic.Count;
            Play(g, p, id);
            Core2026Proof.Check(p.Choice == null && p.BoardZone.SlotCount == 0 && g.Random.CaptureState().Equals(random)
                && g.IdEntityDic.Count == entities + 1, "empty pool creates no offers or summons and consumes no RNG");
        });
        SourceExcluded(g, id);
        int spells = p.NumSpellsPlayedThisGame, cards = p.NumCardsPlayedThisTurn;
        var source = Play(g, p, id);
        // Selector decision-spell semantics: the spell is held out of the graveyard and every cast/play tail waits for the answer.
        Core2026Proof.Check(g.SpellCompletion?.SourceId == source.Id && !Generic.HasDecisionPlay(p) && source.Zone != p.GraveyardZone
            && source.Zone != p.HandZone && p.NumSpellsPlayedThisGame == spells && p.NumCardsPlayedThisTurn == cards + 1,
            "Story pends as a decision spell: held out of graveyard, after-cast tail deferred");
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), source.Card.AssetId);
        Core2026Proof.Check(candidates.Count >= 3 && candidates.All(c => c.Type == CardType.MINION && c.Cost >= 5 && c.Deathrattle), "admitted expensive Deathrattle pool meets floor");
        Core2026Proof.Check(p.Choice.Choices.Count == 3 && p.Choice.Choices.All(x => candidates.Any(c => c.Id == g.IdEntityDic[x].Card.Id))
            && p.BoardZone.SlotCount == 0, "offers precede summon and are admitted controller-or-neutral identities");
        int offer = p.Choice.Choices.First(); string chosen = g.IdEntityDic[offer].Card.Id;
        var before = g.SaveSnapshot(); var decision = g.GetPendingDecisions().Single();
        Core2026Proof.Check(!g.TryAnswer(new(decision.Id, decision.ControllerId, new[] { int.MaxValue }), out _)
            && before.SequenceEqual(g.SaveSnapshot()), "invalid answer preserves suspended state/RNG");
        Resume(g, next => {
            Answer(next);
            var held = next.IdEntityDic[source.Id]; var owner = next.Player1;
            Core2026Proof.Check(next.SpellCompletion == null && held.Zone == owner.GraveyardZone && owner.NumSpellsPlayedThisGame == spells + 1
                && owner.NumCardsPlayedThisTurn == cards + 1 && owner.IsComboActive && owner.GraveyardZone.Count(e => e.Id == source.Id) == 1,
                "after the answer the spell reaches the graveyard once and cast/play tails run once (clone, snapshot, original)");
        });
        var generated = p.BoardZone.First(m => m.Card.Id == chosen);
        Core2026Proof.Check(generated[GameTag.DISPLAYED_CREATOR] == source.Id && generated.Controller == p && p.HandZone.Count == 0
            && p.Choice == null, "chosen minion summoned once with creator; never added to hand");
        foreach (var minion in p.BoardZone.ToArray()) p.SetasideZone.Add(p.BoardZone.Remove(minion));
        EmptyHand(p);
        // Name the singleton explicitly: the admitted pool grows, so "first candidate" is not a stable fixture.
        LostCityPoolProof.WithPool(g, id, new[] { "CORE_EX1_110" }, () => {
            var location = LocationProofs.Location(p);
            int corpses = CorpseRules.Available(p);
            source = Play(g, p, id);
            Core2026Proof.Check(g.IdEntityDic[p.Choice.Choices.Single()].Card.Id == "CORE_EX1_110", "singleton fixture is Cairne");
            FillHand(p);
            Answer(g);
            Core2026Proof.Check(p.BoardZone.GetSlot(0) == location && p.BoardZone.GetSlot(1).Card.Id == "CORE_EX1_110"
                && p.BoardZone.GetSlot(2).Card.Id == "EX1_110t" && p.HandZone.Count == 10 && CorpseRules.Available(p) == corpses,
                "summon appends after location, living Cairne triggers Baine without hand space");
            Kill(g, p.BoardZone.Single(m => m.Card.Id == "CORE_EX1_110"));
            Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "EX1_110t") == 2 && CorpseRules.Available(p) == corpses + 1,
                "triggering does not consume Deathrattle or record a death");
            StandardSetsProof.ClearBoard(g, p); location.Destroy(); g.DeathProcessingAndAuraUpdate(); EmptyHand(p);
            FullBoard(p);
            source = Play(g, p, id); Answer(g);
            Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count == 6 && p.BoardZone.All(m => m.Card.Id == "CS2_182")
                && p.Choice == null, "full mixed board consumes choice without summon or phantom Deathrattle");
            Kill(g, p.BoardZone.Last());
            source = Play(g, p, id); Answer(g);
            Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.GetSlot(6).Card.Id == "CORE_EX1_110"
                && p.BoardZone.All(m => m.Card.Id != "EX1_110t"), "last slot admits Cairne but blocks its Deathrattle summon");
        });
    });
}
