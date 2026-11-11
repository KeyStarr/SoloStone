using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class KeeperOfFlameProof
{
    internal static void Run() => Unpinned_PayloadLifecycle();
    private static void Unpinned_PayloadLifecycle() => StandardSetsProof.Case("FIR_928", (g, p, id) => {
        var held = (Minion)Add(p, "CS2_182"); var played = (Minion)Add(p, "CS2_120");
        Play(g, p, id);
        g.TriggerManager.OnTurnStartTrigger(p); g.ProcessTasks();
        Core2026Proof.Check(held.AppliedEnchantments.Single(e => e.Card.Id == "FIR_928e3")[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3,
            "Unpinned_InstallationTurn never counts even before its start trigger checkpoint");
        var copy = (Minion)Generic.Copy(p, p.Hero, held, Zone.HAND);
        Core2026Proof.Check(held.AttackDamage == 7 && held.BaseHealth == 8 && copy.AttackDamage == 7, "+3/+3 once, independent hand copy");
        p.UsedMana = 0; Core2026Proof.Check(g.Process(PlayCardTask.Any(p, played)), "play buffed minion");
        played.Silence();
        for (int i = 0; i < 3; i++) { End(g); End(g); }
        Core2026Proof.Check(held.Zone == p.GraveyardZone && copy.Zone == p.GraveyardZone && played.Zone == p.BoardZone,
            "Unpinned_OwnerThirdStart destroys held copies only, played Silence survives");
    });
}
