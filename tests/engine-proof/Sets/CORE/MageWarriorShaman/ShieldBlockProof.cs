using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class ShieldBlockProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_606", (g, p, id) => {
        int top = p.DeckZone.TopCard.Id; Play(g, p, id);
        Core2026Proof.Check(p.Hero.Armor == 5 && p.HandZone.Single().Id == top && p.Opponent.Hero.Armor == 0, "five Armor and exact top-card draw for owner");
        PoolPilotProof.EmptyHand(p); PoolPilotProof.ClassDeck(p); int health = p.Hero.Health;
        Play(g, p, id);
        Core2026Proof.Check(p.Hero.Armor == 9 && p.Hero.Health == health && p.Hero.Fatigue == 1, "Armor gained before empty-deck fatigue");
    }, legacyPoolMember: true);
}
