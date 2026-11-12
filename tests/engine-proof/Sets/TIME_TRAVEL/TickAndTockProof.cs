using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static BattleAtTheEndTimeProof;

internal static class TickAndTockProof
{
    internal static void Run()
    {
        var card = Cards.FromId("END_017t");
        Core2026Proof.Check(card?.Implemented == true && card.Cost == 5 && card.ATK == 8 && card.Health == 8
            && card.IsRace(SabberStoneCore.Enums.Race.DRAGON), "Tick and Tock pinned reward identity");
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        int deck = p.DeckZone.Count;
        var summoned = Summon(p, "END_017t");
        Core2026Proof.Check(p.HandZone.Count == 0 && p.DeckZone.Count == deck, "summon does not run battlecry");
        summoned.IsSilenced = true; Kill(g, summoned);
        for (int i = 0; i < 8; i++) Add(p, "CS2_231");
        var tick = (Minion)Play(g, p, "END_017t");
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 2, "draw exactly to capacity after battlecry card leaves hand");
        Unpinned_EmptyIsNotDiscard(g, tick);
        Unpinned_DrawUntilFullStopsOnEmptyDeck(0);
        Unpinned_DrawUntilFullStopsOnEmptyDeck(2);
        CloneSnapshotDeathrattle();
        Console.WriteLine("PASS END_017t Tick and Tock");
    }

    private static void Unpinned_EmptyIsNotDiscard(Game g, Minion tick)
    {
        var opponent = tick.Controller.Opponent;
        var imp = Summon(opponent, "KAR_089");
        Add(opponent, "KAR_205"); Add(opponent, "UNG_836"); Add(opponent, "CS2_182");
        var ids = opponent.HandZone.Select(c => c.Id).ToArray();
        int deck = opponent.DeckZone.Count, discarded = opponent.DiscardedEntities.Count, last = opponent.LastCardDiscarded;
        Kill(g, tick);
        Core2026Proof.Check(opponent.HandZone.Count == 0 && ids.All(id => g.IdEntityDic[id].Zone == opponent.GraveyardZone)
            && opponent.BoardZone.Single() == imp && opponent.DeckZone.Count == deck
            && opponent.DiscardedEntities.Count == discarded && opponent.LastCardDiscarded == last,
            "Unpinned_EmptyIsNotDiscard: no Imp draw, Golem summon, return, or discard bookkeeping");
        var control = Add(opponent, "CS2_182"); Generic.DiscardBlock(opponent, control);
        Core2026Proof.Check(opponent.HandZone.Count == 1 && opponent.DeckZone.Count == deck - 1, "discard control proves Imp listener active");
        Empty(g, opponent);
        var emptyTarget = Summon(tick.Controller, "END_017t"); Kill(g, emptyTarget);
        Core2026Proof.Check(opponent.HandZone.Count == 0 && opponent.DeckZone.Count == deck - 1, "empty opponent hand is no-op");
    }

    private static void Unpinned_DrawUntilFullStopsOnEmptyDeck(int remaining)
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        foreach (var card in p.DeckZone.ToArray().Skip(remaining)) p.SetasideZone.Add(p.DeckZone.Remove(card));
        int health = p.Hero.Health, fatigue = p.Hero.Fatigue, drawn = p.NumCardsDrawnThisTurn;
        Play(g, p, "END_017t");
        Core2026Proof.Check(p.HandZone.Count == remaining && p.DeckZone.Count == 0 && p.Hero.Health == health
            && p.Hero.Fatigue == fatigue && p.NumCardsDrawnThisTurn == drawn + remaining,
            "Unpinned_DrawUntilFullStopsOnEmptyDeck: short/empty deck without fatigue");
    }

    private static void CloneSnapshotDeathrattle()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        var tick = (Minion)Play(g, p, "END_017t");
        End(g); var opponent = p.Opponent;
        var quest = (Spell)Play(g, opponent, "END_017"); Fill(g, opponent);
        Core2026Proof.Check(quest.QuestProgress == 1, "opponent quest mid-progress before deathrattle");
        var clone = g.CloneExact(history: true); var bytes = g.SaveSnapshot(); var restored = Game.LoadSnapshot(bytes);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && bytes.SequenceEqual(restored.SaveSnapshot()), "reward clone/snapshot equal mid-progress");
        Kill(clone, (Minion)clone.IdEntityDic[tick.Id]);
        Core2026Proof.Check(opponent.HandZone.Count == 10 && quest.QuestProgress == 1, "reward clone independent");
        Kill(restored, (Minion)restored.IdEntityDic[tick.Id]); Kill(g, tick);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(restored)
            && Canonical.History(g) == Canonical.History(restored), "deathrattle resumes identically");
        foreach (var game in new[] { g, clone, restored })
            Core2026Proof.Check(game.Player2.HandZone.Single().Card.Id == "END_017t"
                && game.Player2.SecretZone.ModernQuests.Count == 0, "deathrattle emptiness completes opponent quest exactly once");
    }
}
