using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class LotusBookieProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_720", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == 0, "no battlecry generation"); Kill(g, source);
        var coin = p.HandZone.Single(); Core2026Proof.Check(coin.Card.Id == "GAME_005" && coin.Cost == 0 && coin.Card.Type == CardType.SPELL && coin.Card.Implemented, "kill generates pinned Coin");
        source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 1, "silence removes deathrattle");
        source = (Minion)Play(g, p, id); while (p.HandZone.Count < 10) Add(p, "CS2_182");
        Kill(g, source); Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Count(c => c.Card.Id == "GAME_005") == 1, "full hand rejects generated Coin");
    });
}
