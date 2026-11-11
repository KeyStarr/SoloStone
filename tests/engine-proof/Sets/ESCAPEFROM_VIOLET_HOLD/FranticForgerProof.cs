using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_FranticForgerProof
{
    internal static void Run() => Unpinned_CostMemory();
    // Owner-approved working assumptions: docs/design/costmemory.md.
    private static void Unpinned_CostMemory() => StandardSetsProof.Case("JAIL_986", (g, p, id) => {
        p.BaseMana = 4;
        var source = Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source)), "Forger pays own Cost first");
        var spell = p.HandZone.Single();
        Core2026Proof.Check(spell.Card.Type == CardType.SPELL && spell.Cost <= 2 && TemporaryRules.IsTemporary(spell),
            "postpayment affordable Temporary spell");
        End(g);
        Core2026Proof.Check(spell.Zone == p.GraveyardZone, "Temporary expires at owner turn end");
        End(g);
        ParkBoard(p); PoolPilotProof.EmptyHand(p);
        VioletHoldPoolProof.WithPool(g, id, 0, () => {
            long draws = g.Random.DrawCount;
            Play(g, p, id);
            Core2026Proof.Check(p.HandZone.Count == 0 && g.Random.DrawCount == draws, "empty affordability pool fizzles without RNG");
        });
        ParkBoard(p);
        var affordable = CostRules.AffordableSpells(p, new[] { Cards.FromId("CS2_004") });
        Core2026Proof.Check(affordable.Length == 1 && !p.BoardZone.Any(), "target-only spell admitted without legal minion target");
    });
}
