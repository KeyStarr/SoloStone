using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class GnomeferatuProof
{
    internal static void Run() => Case("CORE_ICC_407", (g, p, id) => {
        var top = p.Opponent.DeckZone.TopCard; int deck = p.Opponent.DeckZone.Count;
        Play(g, p, id); Core2026Proof.Check(top.Zone.Type == Zone.GRAVEYARD && p.Opponent.DeckZone.Count == deck - 1, "Gnomeferatu removes top card");
    });
}
