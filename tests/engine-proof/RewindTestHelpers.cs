using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;

internal static class RewindTestHelpers
{
    internal static bool IsRewindOffered(Game game) =>
        game.CurrentPlayer.Choice?.ChoiceAction == ChoiceAction.REWIND;

    internal static void KeepIfOffered(Game game)
    {
        if (!IsRewindOffered(game)) return;
        // Single-attempt RNG counts may span Keep: it advances choice/history bookkeeping, never RNG.
        var random = game.Random.CaptureState();
        Answer(game, "TIME_000ta");
        Core2026Proof.Check(random.Equals(game.Random.CaptureState()), "Keep consumes no RNG");
    }

    internal static void AnswerRewind(Game game) => Answer(game, "TIME_000tb");

    private static void Answer(Game game, string cardId)
    {
        Core2026Proof.Check(IsRewindOffered(game), "Rewind choice offered");
        PendingDecision decision = game.GetPendingDecisions().Single();
        var selected = decision.Options.Single(o => game.IdEntityDic[o.EntityId].Card.Id == cardId);
        Core2026Proof.Check(game.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId,
            new[] { selected.OptionId }), out var error), "Rewind answer accepted: " + error);
    }
}
