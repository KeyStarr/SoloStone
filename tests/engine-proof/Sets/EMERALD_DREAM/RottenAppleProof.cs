using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RottenAppleProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_482", (g, p, id) => {
        p.Hero.Damage = 20;
        Play(g, p, id);
        Core2026Proof.Check(p.Hero.Health == 22, "heals twelve immediately");
        End(g); Core2026Proof.Check(p.Hero.Health == 19, "casting turn is first tick");
        End(g); Core2026Proof.Check(p.Hero.Health == 19, "no opponent-end tick");
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(clone.Player1.Hero.Health == 16 && p.Hero.Health == 19, "second tick clone isolated");
        End(g); Core2026Proof.Check(p.Hero.Health == 16, "second owner end ticks");
        End(g); End(g); Core2026Proof.Check(p.Hero.Health == 16, "two ticks only");
        Core2026Proof.Check(!p.AppliedEnchantments.Any(x => x.Card.Id == "EDR_482e"), "expired enchant removed");
        End(g);
        Play(g, p, id); Play(g, p, id);
        End(g); Core2026Proof.Check(p.Hero.Health == 24, "two independent timers stack, heal capped at max health");
        Summon(p, "CS2_052"); End(g); End(g);
        Core2026Proof.Check(p.Hero.Health == 16, "each second tick uses original spell and current Spell Damage");
        Core2026Proof.Check(!p.AppliedEnchantments.Any(x => x.Card.Id == "EDR_482e"), "both stacked timers expire");
    });
}
