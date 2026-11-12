using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class TimeSkipperProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_054", (g, p, id) => {
        var held = Add(p, id); var source = (Minion)Play(g, p, id); var clone = g.CloneExact(history: true); int hand = p.HandZone.Count;
        End(clone); Core2026Proof.Check(clone.Player1.HandZone.Count == hand + 1 && p.HandZone.Count == hand, "clone end-turn grant independent");
        End(g); Core2026Proof.Check(p.HandZone.Last().Card.Id == "GAME_005" && p.HandZone.Count == hand + 1, "friendly turn gives friendly Coin");
        int opponentHand = p.Opponent.HandZone.Count; End(g);
        Core2026Proof.Check(p.Opponent.HandZone.Last().Card.Id == "GAME_005" && p.Opponent.HandZone.Count == opponentHand + 1, "opponent turn gives opponent Coin");
        Core2026Proof.Check(Cards.FromId("GAME_005").Cost == 0 && Cards.FromId("GAME_005").Implemented, "exact playable Coin");
        source.Silence(); hand = p.HandZone.Count; End(g); Core2026Proof.Check(p.HandZone.Count == hand, "Silence removes both-turn trigger");
        End(g); var dead = Summon(p, id); Kill(g, dead); hand = p.HandZone.Count; End(g); Core2026Proof.Check(p.HandZone.Count == hand, "dead source and held copy never trigger");
        End(g); source = Summon(p, id); while (p.HandZone.Count < 10) Add(p, "CS2_182"); End(g);
        Core2026Proof.Check(p.HandZone.Count == 10, "Coin respects full hand");
    });
}
