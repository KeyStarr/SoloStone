using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static B15DrawReplacementProof;

internal static class B15DrawReplacementCardsProof
{
    [GroupProof("b15-draw-replacement-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("BE_250", Unpinned_DiscsSpellPermutationNonspellStability);
        B15DrawReplacementCoreProof.Run();
        B15DrawReplacementJailProof.Run();
        B15DrawReplacementTimeProof.Run();
        B15DrawReplacementQuestProof.Run();
    }
    private static void Unpinned_DiscsSpellPermutationNonspellStability(Game g, Controller p, string id)
    {
        EmptyDeck(p);
        var low = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        var fire = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone); fire.Cost = 2;
        var high = Entity.FromCard(p, Cards.FromId("CS2_200"), zone: p.DeckZone);
        var frost = Entity.FromCard(p, Cards.FromId("CS2_024"), zone: p.DeckZone);
        var heal = Entity.FromCard(p, Cards.FromId("CS1_130"), zone: p.DeckZone);
        int shuffles = g.HistoryFacts.Query.Shuffles().Count();
        Play(g, p, id);
        Core2026Proof.Check(p.AppliedEnchantments?.Count(e => e.Card.Id == "BE_250e") == 1
            && p.DeckZone.Take(2).Select(c => c.Id).SequenceEqual(new[] { low.Id, high.Id })
            && p.DeckZone.Skip(2).All(c => c is Spell) && fire.Cost == 2
            && g.HistoryFacts.Query.Shuffles().Count() == shuffles,
            "Discs permutes spell group in place, preserves nonspell order/modifiers and emits no shuffle fact");
        var top = p.DeckZone.TopCard;
        Generic.Draw(p);
        Core2026Proof.Check(p.Choice == null && top.Zone == p.HandZone, "explicit draws stay ordinary");
        top = p.DeckZone.TopCard;
        g.MainDraw();
        Core2026Proof.Check(p.Choice?.Choices.Count == 2, "normal draw opens exactly two direction options");
        foreach (bool bottom in new[] { false, true })
        foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var owner = next.Player1; int selected = owner.Choice.Choices[bottom ? 1 : 0];
            var decision = next.GetPendingDecisions().Single();
            Core2026Proof.Check(next.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(
                decision.Id, decision.ControllerId, new[] { selected }), out var error), "saved Discs choice: " + error);
            Core2026Proof.Check(next.IdEntityDic[bottom ? low.Id : top.Id].Zone == owner.HandZone
                && owner.Choice == null && next.GetPendingDecisions().All(d => d.Kind == SabberStoneCore.Model.Decisions.DecisionKind.Options), "saved choice draws correct end once and finishes");
        }
        var pending = g.GetPendingDecisions().Single();
        Core2026Proof.Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(
            pending.Id, pending.ControllerId, new[] { p.Choice.Choices[0] }), out _), "complete original Discs prompt");
        Play(g, p, id);
        Core2026Proof.Check(p.AppliedEnchantments.Count(e => e.Card.Id == "BE_250e") == 1, "recast refreshes one persistent choice");
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        g.MainDraw(); pending = g.GetPendingDecisions().Single();
        int bottomId = p.DeckZone[0].Id;
        Core2026Proof.Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(
            pending.Id, pending.ControllerId, new[] { p.Choice.Choices[1] }), out _)
            && g.IdEntityDic[bottomId].Zone == p.GraveyardZone, "full hand still chooses direction then burns selected original");
        // Empty deck takes ordinary fatigue without an empty/stuck prompt.
        EmptyDeck(p); int damage = p.Hero.Damage;
        g.MainDraw();
        Core2026Proof.Check(p.Choice == null && p.Hero.Damage == damage + 1, "Discs empty deck fatigues normally");
    }
}
