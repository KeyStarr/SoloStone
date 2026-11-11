using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class CalloftheWildProof
{
    internal static void Run() => Case("CORE_OG_211", (g, p, id) => {
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 3 && p.BoardZone.Select(m => m.Card.Id).Order().SequenceEqual(new[] { "NEW1_032", "NEW1_033", "NEW1_034" }), "Call summons all Animal Companions");
    });
}
