using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class GorillabotA3Proof
{
    internal static void Run() => Case("CORE_LOE_039", (g, p, id) => {
        Summon(p, "GVG_085"); Play(g, p, id);
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.All(key => g.IdEntityDic[key].Card.IsRace(Race.MECHANICAL)), "another Mech enables Mech Discover");
        Core2026Proof.Check(g.Process(ChooseTask.Pick(p, p.Choice.Choices[0])), "Gorillabot pick accepted");
    });
}
