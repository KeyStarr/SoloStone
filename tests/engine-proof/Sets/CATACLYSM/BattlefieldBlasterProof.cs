using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BattlefieldBlasterProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_209", BattlefieldBlaster);
    private static void BattlefieldBlaster(Game g, Controller p, string id)
    {
        var spell = Add(p, "CS2_029"); var minion = Add(p, "CS2_182");
        var opponent = Add(p.Opponent, "CS2_029"); Play(g, p, id);
        Core2026Proof.Check(p.Choice.Choices.SequenceEqual(new[] { spell.Id }), "offers only own hand spells");
        var clone = g.CloneExact(history: true); var restored = Game.LoadSnapshot(g.SaveSnapshot());
        foreach (var next in new[] { g, clone, restored }) PlayerDecisionProof.Answer(next, spell.Id);
        Core2026Proof.Check(spell[GameTag.SPELLPOWER] == 1 && spell.AppliedEnchantments.Single().Card.Id == "CATA_209e"
            && minion[GameTag.SPELLPOWER] == 0 && opponent[GameTag.SPELLPOWER] == 0, "one chosen spell enhanced");
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(restored), "pending choice clone/snapshot resumes identically");
        PoolPilotProof.EmptyHand(p); ParkBoard(p); Play(g, p, id);
        Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect, "no spell completes without prompt");
    }
}
