using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class CoreCostProof
{
    internal static void Count(Game g, string id)
    {
        var declaration = OfflinePoolDeclarations.Cards.Single(c => c.CardId == id);
        var admission = OfflinePoolAdmission.Evaluate(g.OfflinePools, new[] { declaration });
        admission.ValidateRegistered();
        Core2026Proof.Check(admission.Cards.Single().PoolRequirements.All(r => r.Count >= 3), $"{id}: admitted candidates meet threshold after source exclusion");
    }
    internal static void Answer(Game g)
        => Answer(g, g.GetPendingDecisions().Single().Options[0].EntityId);

    internal static void Answer(Game g, int selectedEntityId)
    {
        var pending = g.GetPendingDecisions().Single();
        var selected = pending.Options.Single(o => o.EntityId == selectedEntityId);
        Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(pending.Id, pending.ControllerId, new[] { selected.OptionId }), out var error), "Discover answer: " + error);
    }
}
