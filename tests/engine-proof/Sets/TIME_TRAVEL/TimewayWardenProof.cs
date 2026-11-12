using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class TimewayWardenProof
{
    internal static void Run() => Unpinned_PayloadLifecycle();
    private static void QueuedAwakening(Game g, Controller p, string id) {
        var prisoner = Summon(p.Opponent, "EDR_840t");
        DormantRules.Awaken(prisoner);
        int attack = p.Opponent.Hero.AttackDamage;
        var warden = (Minion)Play(g, p, id, prisoner);
        Kill(g, warden);
        Core2026Proof.Check(!prisoner.IsDormant && p.Opponent.Hero.AttackDamage == attack + 3,
            "queued Warden release resolves the Hound awakening once without draining its parent task");
        Core2026Proof.Check(g.TaskQueue.IsQuiescent, "queued awakening leaves no event or task behind");
    }
    private static void Unpinned_PayloadLifecycle() => StandardSetsProof.Case("TIME_442", (g, p, id) => {
        var a = Summon(p.Opponent, "CS2_182"); var b = Summon(p.Opponent, "CS2_120");
        var w = (Minion)Play(g, p, id, a);
        Core2026Proof.Check(a.IsDormant && DormantRules.TurnsRemaining(a) == 10000, "enemy imprisoned");
        g.TaskQueue.Execute(w.Power.PowerTask, p, w, b); g.ProcessTasks();
        var copy = (Minion)Generic.Copy(p, p.Hero, w, Zone.PLAY);
        Kill(g, copy);
        Core2026Proof.Check(!a.IsDormant && !b.IsDormant && w.Zone == p.BoardZone, "Unpinned_CopyAndRepeat release both while original lives");
        DormantRules.Make(a, 10000, "TIME_442e", w); var generation = DormantRules.SleepGeneration(a);
        Kill(g, w); Core2026Proof.Check(a.IsDormant && DormantRules.SleepGeneration(a) == generation, "Unpinned_StaleGeneration does not release new prison");
        var repeater = Summon(p, "CS2_182"); g.TaskQueue.Execute(Cards.FromId(id).Power.PowerTask, p, repeater, b); g.ProcessTasks();
        Kill(g, repeater); Core2026Proof.Check(b.IsDormant, "repeating Battlecry never grants printed release Deathrattle");
        DormantRules.Awaken(b);
        var silenced = (Minion)Play(g, p, id, b); silenced.Silence(); Kill(g, silenced);
        Core2026Proof.Check(b.IsDormant, "Silence cancels printed release and removes links");
        QueuedAwakening(g, p, id);
    });
}
