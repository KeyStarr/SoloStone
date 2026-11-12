using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class AstromancerProof
{
    internal static void Run() => Case("CORE_BOT_256", (g, p, id) => {
        for (int i = 0; i < 3; i++) Add(p, "CS2_182");
        var source = Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.Single(m => m.Id != source.Id).Card.Cost == 3, "summon matches remaining hand size");
    });
}
