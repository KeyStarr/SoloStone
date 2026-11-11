using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MoargForgefiendProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_SW_068", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 8 && source.Health == 8 && source.HasTaunt && source.HasDeathrattle
            && source.IsRace(Race.MECHANICAL) && source.IsRace(Race.DEMON), "printed 8/8 Taunt Mech/Demon");
        Play(g, p, "GVG_055", source);
        Core2026Proof.Check(source.AttackDamage == 10 && source.Health == 10, "Mech-targeted Screwjank buff accepts secondary race");
        Play(g, p, "EX1_596", source);
        Core2026Proof.Check(source.AttackDamage == 12 && source.Health == 12, "Demonfire buffs Demon rather than dealing damage");
        var copy = Generic.Copy(p, p.Hero, source, Zone.HAND);
        Core2026Proof.Check(copy.Card.IsRace(Race.MECHANICAL) && copy.Card.IsRace(Race.DEMON), "copy preserves both types");
        p.Hero.Armor = 3; int enemyArmor = p.Opponent.Hero.Armor;
        Kill(g, source);
        Core2026Proof.Check(p.Hero.Armor == 11 && p.Opponent.Hero.Armor == enemyArmor, "death grants eight Armor only to owner");
        source = (Minion)Play(g, p, id); source.Silence();
        Core2026Proof.Check(!source.HasTaunt && source.IsRace(Race.MECHANICAL) && source.IsRace(Race.DEMON), "Silence removes Taunt but retains both printed types");
        Kill(g, source);
        Core2026Proof.Check(p.Hero.Armor == 11, "Silence removes Armor deathrattle");
    });
}
