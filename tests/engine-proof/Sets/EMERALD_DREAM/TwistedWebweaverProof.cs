using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TwistedWebweaverProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_540", (g, p, id) => {
        Play(g, p, "CS2_182"); ClearBoard(g, p);
        End(g); Play(g, p.Opponent, "CS2_171"); End(g);
        var weaver = Play(g, p, id);
        int hand = p.HandZone.Count;
        Summon(p, "CS2_182");
        Core2026Proof.Check(p.HandZone.Count == hand, "summon not play");
        Play(g, p, "CS2_171");
        Core2026Proof.Check(p.HandZone.Count == hand, "opponent prior play does not count");
        Play(g, p, "CS2_182");
        Core2026Proof.Check(p.HandZone.Count == hand + 1, "prior own turn before weaver existed qualifies");
        var clone = g.Clone(); Play(clone, clone.Player1, "CS2_171");
        Core2026Proof.Check(clone.Player1.HandZone.Count == hand + 2 && p.HandZone.Count == hand + 1, "current turn repeat and clone independent");
        new SilenceTask(EntityType.TARGET).Process(g, p, p.Hero, weaver, new TaskStack());
        Kill(g, p.BoardZone.First(x => x.Card.Id == "CS2_182"));
        Play(g, p, "CS2_182");
        Core2026Proof.Check(p.HandZone.Count == hand + 1, "silence removes draw trigger");
        ClearBoard(g, p);
        hand = p.HandZone.Count;
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == hand, "weaver cannot trigger on its own play even with prior matching play");
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == hand + 1, "only older weaver triggers when second is played");
        ClearBoard(g, p);
        Play(g, p, "CORE_EX1_005"); ClearBoard(g, p);
        var victim = (Minion)Play(g, p, id); victim.AttackDamage = 7;
        hand = p.HandZone.Count;
        Play(g, p, "CORE_EX1_005", victim);
        Core2026Proof.Check(victim.Zone == p.GraveyardZone && p.HandZone.Count == hand + 1, "draw happens before repeated minion Battlecry kills weaver");
    });
}
