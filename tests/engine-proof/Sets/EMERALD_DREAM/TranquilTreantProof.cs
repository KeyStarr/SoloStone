using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TranquilTreantProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_861", (g, p, id) => {
        var source = (Minion)Play(g, p, id); p.BaseMana = 4; p.UsedMana = 2; p.Opponent.BaseMana = 9; p.Opponent.UsedMana = 3;
        Kill(g, source);
        Core2026Proof.Check(p.BaseMana == 5 && p.UsedMana == 3 && p.RemainingMana == 2 && p.Opponent.BaseMana == 10 && p.Opponent.UsedMana == 4 && p.Opponent.RemainingMana == 6, "both gain exactly one empty crystal; available mana unchanged");
        source = (Minion)Play(g, p, id); p.UsedMana = 4; p.Opponent.UsedMana = 7; Kill(g, source);
        Core2026Proof.Check(p.BaseMana == 10 && p.UsedMana == 4 && p.Opponent.BaseMana == 10 && p.Opponent.UsedMana == 7, "both capped at ten; no mana lost or gained");
        source = (Minion)Play(g, p, id); p.BaseMana = 0; p.UsedMana = 0; p.Opponent.BaseMana = 0; p.Opponent.UsedMana = 0; Kill(g, source);
        Core2026Proof.Check(p.BaseMana == 1 && p.UsedMana == 1 && p.Opponent.BaseMana == 1 && p.Opponent.UsedMana == 1, "zero crystals gains an empty crystal for each player");
        source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); p.BaseMana = 5; p.UsedMana = 0; Kill(g, source);
        Core2026Proof.Check(p.BaseMana == 5 && p.UsedMana == 0 && p.Opponent.BaseMana == 1, "silence removes both-player ramp");
    });
}
