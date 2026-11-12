using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class StoryOfAmaraProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_835", (g, p, id) => {
        p.Hero.Damage = 17; p.Hero.Armor = 6;
        Play(g, p, id);
        Core2026Proof.Check(p.Hero.Health == 40 && p.Hero.BaseHealth == 40 && p.Hero.Damage == 0 && p.Hero.Armor == 6 && p.Opponent.Hero.Health == 30, "set damaged own hero to 40, preserve Armor and opponent");
        p.Hero.Damage = 5; Play(g, p, id);
        Core2026Proof.Check(p.Hero.Health == 40 && p.Hero.Damage == 0, "repeat restores Health to 40");
        p.Hero[GameTag.HEALTH] = 50; p.Hero.Damage = 3; Play(g, p, id);
        Core2026Proof.Check(p.Hero.Health == 40 && p.Hero.BaseHealth == 40 && p.Hero.Damage == 0, "set rather than heal when above 40");
    });
}
