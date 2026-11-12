using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PoolPilotProof;

internal static class AncientStegodonProof
{
    // Tier E Borovi Bence reveal + tier D exact export; owner assumption: data/rules-evidence/misc-cards/stegodon-option-ids.yaml.
    internal static void Run() => StandardSetsProof.Case("TLC_242", Unpinned_AncientStegodonOptions);
    private static void Unpinned_AncientStegodonOptions(Game g, Controller p, string id)
    {
        string[] options = { "UNG_999t6", "UNG_999t13", "UNG_999t14" };
        for (int i = 0; i < 3; i++)
        {
            StandardSetsProof.ParkBoard(p); EmptyHand(p);
            LocationProofs.Location(p); while (p.BoardZone.SlotCount < 6) Summon(p, "CS2_182");
            var source = (Minion)Play(g, p, id);
            Options(g, p, options);
            int pick = p.Choice.Choices.ElementAt(i);
            Resume(g, next => {
                Answer(next, pick);
                var m = (Minion)next.IdEntityDic[source.Id];
                Core2026Proof.Check(m.HasTaunt == (i == 0) && m.Poisonous == (i == 1)
                    && m.AttackDamage == (i == 2 ? 2 : 1) && m.Health == (i == 2 ? 6 : 5), "exact chosen Adapt only");
            });
            Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.HandZone.Count == 0 && p.Choice == null, "no hand or board capacity consumed by option");
            source.Silence();
            Core2026Proof.Check(!source.HasTaunt && !source.Poisonous && source.AttackDamage == 1 && source.Health == 5, "silence removes Adapt");
        }
    }
    internal static void Options(Game g, Controller p, string[] ids)
    {
        foreach (string id in ids) { CostSliceProof.Definition(id); CostSliceProof.Definition(id + "e"); }
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.Select(x => g.IdEntityDic[x].Card.Id).SequenceEqual(ids), "exact ordered fixed options");
        var before = g.SaveSnapshot(); var view = g.GetPendingDecisions().Single();
        Core2026Proof.Check(!g.TryAnswer(new DecisionAnswer(view.Id, view.ControllerId, new[] { int.MaxValue }), out _)
            && before.SequenceEqual(g.SaveSnapshot()), "invalid choice cannot mutate suspended state");
    }
    internal static void Answer(Game g, int entity)
    {
        var view = g.GetPendingDecisions().Single();
        Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(view.Id, view.ControllerId, new[] { entity }), out var error), error);
    }
}
