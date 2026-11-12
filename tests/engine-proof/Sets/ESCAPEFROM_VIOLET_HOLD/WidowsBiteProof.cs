using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class WidowsBiteProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_436", (g, p, id) => {

        p.Hero.Armor = 2; Play(g, p, id);
        var feast = p.HandZone.Single();
        Core2026Proof.Check(p.Hero.AttackDamage == 1 && p.Hero.Armor == 3 && feast.Card.Id == "JAIL_436t" && feast.Cost == 2 && feast.Card.Implemented, "Bite grants attack, armor and exact Feast");
        var clone = g.Clone(); PlayEntity(clone, clone.Player1, clone.IdEntityDic[feast.Id]); End(clone);
        Core2026Proof.Check(clone.Player1.Hero.AttackDamage == 0 && clone.Player1.Hero.Armor == 5 && p.Hero.AttackDamage == 1 && p.Hero.Armor == 3, "temporary effects expire independently in clone");
        PlayEntity(g, p, feast); var banquet = p.HandZone.Single();
        Core2026Proof.Check(p.Hero.AttackDamage == 3 && p.Hero.Armor == 5 && banquet.Card.Id == "JAIL_436t2" && banquet.Cost == 2 && banquet.Card.Implemented, "Feast stacks and generates exact Banquet");
        PlayEntity(g, p, banquet); Core2026Proof.Check(p.Hero.AttackDamage == 7 && p.Hero.Armor == 9 && p.HandZone.Count == 0, "Banquet ends chain with four attack/armor");
        End(g); Core2026Proof.Check(p.Hero.AttackDamage == 0 && p.Hero.Armor == 9 && p.Opponent.Hero.AttackDamage == 0, "all attack expires; armor permanent; opponent untouched"); End(g);
        var source = Play(g, p, id); while (p.HandZone.Count < 10) Add(p, "CS2_182");
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Count(c => c.Card.Id == "JAIL_436t") == 1 && p.Hero.AttackDamage == 2 && p.Hero.Armor == 11, "full hand burns generated Feast but keeps attack/armor");
    });
}
