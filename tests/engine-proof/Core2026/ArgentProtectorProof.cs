using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class ArgentProtectorProof
{
    internal static void Run() => Case("CORE_EX1_362", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); Play(g, p, id, friend);
        Core2026Proof.Check(friend.HasDivineShield, "Protector grants Divine Shield");
    });
}
