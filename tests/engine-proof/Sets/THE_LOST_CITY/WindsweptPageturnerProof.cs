using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class WindsweptPageturnerProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_220", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 0, "does not trigger on itself");
        Summon(p, "DINO_413"); Core2026Proof.Check(p.Opponent.Hero.Damage == 3, "secondary Elemental summon triggers three damage");
        Summon(p, "CS2_182"); Summon(p.Opponent, "DINO_413");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 3, "non-Elemental and opponent summon do not trigger");
        ClearBoard(g, p.Opponent);
        var clone = g.Clone(); Summon(clone.Player1, "CS2_033");
        Core2026Proof.Check(clone.Player2.Hero.Damage == 6 && p.Opponent.Hero.Damage == 3, "clone trigger isolated");
        End(g); End(g); Summon(p, "CS2_033");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 6, "ongoing board trigger survives turn boundary");
        source.Silence(); Summon(p, "CS2_033");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 6, "Silence removes trigger");
        ClearBoard(g, p); source = (Minion)Play(g, p, id); Kill(g, source); Summon(p, "CS2_033");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 6, "leaving play removes trigger");
        ClearBoard(g, p); Add(p, id); Summon(p, "CS2_033");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 6, "hand copy does not trigger");
    });
}
