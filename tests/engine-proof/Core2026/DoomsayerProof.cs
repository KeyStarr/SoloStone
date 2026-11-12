using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class DoomsayerProof
{
    internal static void Run() => Case("CORE_NEW1_021", (g, p, id) => {
        Summon(p.Opponent, "CS2_182"); Play(g, p, id); End(g);
        Core2026Proof.Check(p.BoardZone.Count == 1 && p.Opponent.BoardZone.Count == 1, "Doomsayer waits for own turn");
        End(g); Core2026Proof.Check(p.BoardZone.Count == 0 && p.Opponent.BoardZone.Count == 0, "Doomsayer clears at own turn start");
    });
}
