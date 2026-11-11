using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DarkscaleBroodmotherProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_111", (g, p, id) => {
        var source = Add(p, id); p.BaseMana = 10; p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source)), "Broodmother play accepted"); Core2026Proof.Check(p.UsedMana == 3, "source Dragon does not satisfy holding Dragon");
        Add(p, "CATA_111"); source = Add(p, id); p.UsedMana = 7;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source)), "Dragon condition play accepted"); Core2026Proof.Check(p.UsedMana == 8 && p.BaseMana == 10, $"refreshes exactly two spent mana: used={p.UsedMana}, base={p.BaseMana}, dragon={p.DragonInHand}");
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack()); Core2026Proof.Check(p.UsedMana == 6, "repeated refresh");
        p.UsedMana = 1; source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack()); Core2026Proof.Check(p.UsedMana == 0, "refresh capped at spent mana");
    });
}
