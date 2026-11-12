using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned data/carddb/37.0.0.254477/cards.json.gz (identity and current text).
internal static class ArchmageAntonidasProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_559", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Play(g, p, "CS2_182");
        Core2026Proof.Check(p.HandZone.Count == 0, "minion plays do not trigger");
        PoolPilotProof.Resume(g, next => {
            Play(next, next.Player1, "GAME_005"); var fireball = next.Player1.HandZone.Single();
            Core2026Proof.Check(fireball.Card.Id == "CS2_029" && fireball.Cost == 4 && fireball.Card.Text.StartsWith("Deal $6 damage."), "spell generates exact Fireball across clone/snapshot");
            PlayEntity(next, next.Player1, fireball, next.Player2.Hero);
            Core2026Proof.Check(next.Player2.Hero.Damage == 6 && next.Player1.HandZone.Single().Card.Id == "CS2_029", "generated Fireball deals six and triggers another Fireball");
        });
        End(g); int hand = p.HandZone.Count; int enemyHand = p.Opponent.HandZone.Count; Play(g, p.Opponent, "GAME_005");
        Core2026Proof.Check(p.HandZone.Count == hand && p.Opponent.HandZone.Count == enemyHand, "opponent spell never triggers"); End(g);
        var second = (Minion)Play(g, p, id);
        while (p.HandZone.Count < 9) Add(p, "CS2_182");
        int burned = p.GraveyardZone.Count(x => x.Card.Id == "CS2_029"); Play(g, p, "GAME_005");
        Core2026Proof.Check(p.HandZone.IsFull && p.GraveyardZone.Count(x => x.Card.Id == "CS2_029") == burned + 1, "two listeners fill final slot then burn excess Fireball");
        Kill(g, second);
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        source.Silence(); Play(g, p, "GAME_005"); Core2026Proof.Check(p.HandZone.Count == 0, "Silence removes generation trigger");
        Kill(g, source); var live = (Minion)Play(g, p, id); Kill(g, live); Play(g, p, "GAME_005");
        Core2026Proof.Check(p.HandZone.Count == 0, "source leaving play removes generation trigger");
    }, legacyPoolMember: true);
}
