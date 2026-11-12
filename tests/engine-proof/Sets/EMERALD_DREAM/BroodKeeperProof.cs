using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BroodKeeperProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_457", (g, p, id) => {
        Play(g, p, id); Core2026Proof.Check(p.Hero.Weapon == null, "no Dragon means no weapon");
        Summon(p, "TIME_056"); Play(g, p, id); Core2026Proof.Check(p.Hero.Weapon == null, "board Dragon does not count");
        Add(p, "TIME_056"); Play(g, p, id);
        var sword = p.Hero.Weapon;
        Core2026Proof.Check(sword.Card.Id == "EDR_457t" && sword.AttackDamage == 2 && sword.Durability == 2 && sword.Card.Implemented, "equips pinned 2/2 Nightmare Slicer");
        Play(g, p, id); Core2026Proof.Check(p.Hero.Weapon.Id != sword.Id && p.Hero.Weapon.Card.Id == "EDR_457t" && sword.Zone.Type == Zone.GRAVEYARD, "replaces existing weapon");
    });
}
