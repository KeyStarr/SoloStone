using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SigilofHatchingProof
{
    internal static void Run() => StandardSetsProof.Case("BE_873", (g, p, id) => {
        var spell = Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 0, "summon delayed");
        var restored = Game.LoadSnapshot(g.SaveSnapshot()); End(restored); End(restored);
        Core2026Proof.Check(restored.Player1.BoardZone.Count == 3 && spell.Zone.Type == Zone.GRAVEYARD && p.BoardZone.Count == 0, "pending graveyard sigil survives snapshot without affecting original");
        var clone = g.Clone(); End(clone); Core2026Proof.Check(clone.Player1.BoardZone.Count == 0, "opponent start excluded"); End(clone);
        Core2026Proof.Check(clone.Player1.BoardZone.Count == 3 && p.BoardZone.Count == 0 && spell.ActivatedTrigger != null, "one-shot spell trigger clone isolated");
        End(g); Core2026Proof.Check(p.BoardZone.Count == 0, "opponent turn no summon"); End(g);
        var tokens = p.BoardZone.ToArray(); Core2026Proof.Check(tokens.Length == 3 && tokens.All(m => m.Card.Id == "BE_873t" && m.AttackDamage == 3 && m.Health == 1 && m.IsRush && m.IsRace(Race.BEAST) && m.Card.Implemented) && tokens.Select(m => m.Id).SequenceEqual(tokens.Select(m => m.Id).Order()), "three exact printed Rush Shredclaws in creation order");
        End(g); End(g); Core2026Proof.Check(p.BoardZone.Count == 3 && spell.ActivatedTrigger == null, "one shot never repeats");
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182"); Play(g, p, id); End(g); End(g);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Last().Card.Id == "BE_873t", "partial board fills one slot at right");
        Play(g, p, id); End(g); End(g); Core2026Proof.Check(p.BoardZone.Count == 7, "full board no overflow");
        Kill(g, p.BoardZone[0]); End(g); End(g); Core2026Proof.Check(p.BoardZone.Count == 6, "full-board one-shot consumed");
        ClearBoard(g, p);
        var doubled = Add(p, id); RepeatRules.Mark(doubled, p.Hero, "JAIL_123e"); PlayEntity(g, p, doubled);
        PoolPilotProof.Resume(g, next => {
            End(next); End(next);
            Core2026Proof.Check(next.Player1.BoardZone.Count == 6 && next.Player1.BoardZone.All(m => m.Card.Id == "BE_873t"),
                "repeated Sigil schedules both three-token activations across clone/snapshot");
            End(next); End(next);
            Core2026Proof.Check(next.Player1.BoardZone.Count == 6, "both repeated Sigil activations are one-shot");
        });
    });

    [GroupProof("b14-integration-sigil-repeat")]
    internal static void Repeated()
    {
        var repeated = HeraldProof.New(CardClass.MAGE); var caster = repeated.Player1;
        var twice = Add(caster, "BE_873"); twice[(GameTag)32100] = 1;
        Core2026Proof.Check(repeated.Process(PlayCardTask.Any(caster, twice)), "repeated Sigil arms both delayed effects");
        foreach (var branch in new[] { repeated, repeated.CloneExact(history: true), Game.LoadSnapshot(repeated.SaveSnapshot()) })
        {
            End(branch); End(branch);
            Core2026Proof.Check(branch.Player1.BoardZone.Count == 6, "both Sigil bodies survive clone/snapshot and trigger once");
            End(branch); End(branch);
            Core2026Proof.Check(branch.Player1.BoardZone.Count == 6, "repeated Sigil callbacks retire independently");
        }
    }
}
