using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class NetherspiteHistorianProof
{
    internal static void Run() => Case("CORE_KAR_062", (g, p, id) => {
        Add(p, "EX1_043"); Play(g, p, id);
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.All(key => g.IdEntityDic[key].Card.IsRace(Race.DRAGON)), "holding Dragon offers Dragons");
        Core2026Proof.Check(g.Process(ChooseTask.Pick(p, p.Choice.Choices[0])), "Historian pick accepted");
    });
}
