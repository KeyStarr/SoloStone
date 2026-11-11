using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class IsorathProof
{
    internal static void Run() => Unpinned_PayloadLifecycle();
    private static void Unpinned_PayloadLifecycle() => StandardSetsProof.Case("CATA_481", (g, p, id) => {
        var stolen = Add(p.Opponent, "CS2_029"); stolen.Cost = 2;
        var other = (Minion)Add(p.Opponent, "CS2_182"); other.AttackDamage = 8;
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsDormant && p.Opponent.HandZone.Count == 0, "two cards devoured then Dormant");
        DormantRules.Awaken(source);
        var copy = (Minion)Generic.Copy(p, p.Hero, source, Zone.PLAY);
        Kill(g, copy); Core2026Proof.Check(p.Opponent.HandZone.Count == 2, "Unpinned_CopyReturns independent set");
        Core2026Proof.Check(p.Opponent.HandZone.Single(c => c.Card.Id == "CS2_029").Cost == 2
            && ((Minion)p.Opponent.HandZone.Single(c => c.Card.Id == "CS2_182")).AttackDamage == 8, "captured modifications retained");
        g.TaskQueue.Execute(source.Power.DeathrattleTask, p.Opponent, source, null); g.ProcessTasks();
        Core2026Proof.Check(p.Opponent.HandZone.Count == 4, "Unpinned_RepeatRecipe uses recorded recipient independently of resolver");
        while (p.Opponent.HandZone.Count < 9) Add(p.Opponent, "CS2_120");
        int grave = p.Opponent.GraveyardZone.Count;
        Kill(g, source); Core2026Proof.Check(p.Opponent.HandZone.Count == 10 && p.Opponent.GraveyardZone.Count == grave + 1,
            "Unpinned_Overflow return sequentially burns second at capacity");
    });
}
