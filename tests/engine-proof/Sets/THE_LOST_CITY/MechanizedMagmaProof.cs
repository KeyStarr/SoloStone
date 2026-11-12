using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MechanizedMagmaProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_224", (g, p, id) => {
        var fresh = (Minion)Play(g, p, id);
        Play(g, p, "CS2_029", fresh);
        Core2026Proof.Check(fresh.Zone == p.BoardZone && fresh.AttackDamage == 6 && fresh.BaseHealth == 9 && fresh.Health == 3,
            "Fireball grants stats before damage so fresh Magma survives at three Health");
        Kill(g, fresh);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRace(Race.ELEMENTAL) && source.IsRace(Race.MECHANICAL), "both printed types");
        Play(g, p, "CS2_029", p.Opponent.Hero);
        Core2026Proof.Check(source.AttackDamage == 6 && source.BaseHealth == 9, "legacy Fireball grants four stats");
        var apprentice = Summon(p, "EX1_608");
        var breath = Add(p, "DINO_406"); g.AuraUpdate();
        Core2026Proof.Check(breath.Cost == 2, $"discount fixture cost={breath.Cost}");
        PlayEntity(g, p, breath, p.Opponent.Hero);
        Core2026Proof.Check(source.AttackDamage == 9 && source.BaseHealth == 12, $"paid cost two plus Fire Breath's own +1/+1: {source.AttackDamage}/{source.BaseHealth}");
        var free = Add(p, "CS2_029"); free.Cost = 0; PlayEntity(g, p, free, p.Opponent.Hero);
        Core2026Proof.Check(source.AttackDamage == 9 && source.BaseHealth == 12, "zero-cost Fire spell gives zero stats");
        Play(g, p, "CS2_024", p.Opponent.Hero);
        Core2026Proof.Check(source.AttackDamage == 9, "non-Fire spell does not trigger");
        var clone = g.Clone(); var discounted = Add(clone.Player1, "CS2_029"); clone.AuraUpdate();
        PlayEntity(clone, clone.Player1, discounted, clone.Player2.Hero);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[source.Id]).AttackDamage == 12 && source.AttackDamage == 9, "clone isolated and discounted Fireball grants three stats");
        p.Opponent.Hero.Damage = 0; End(g); Play(g, p.Opponent, "CS2_029", p.Hero);
        Core2026Proof.Check(source.AttackDamage == 9, "opponent Fire spell does not trigger"); End(g);
        Core2026Proof.Check(source.AttackDamage == 9, "buff does not expire");
        Play(g, p, "BE_228", source); Play(g, p, "CS2_029", p.Opponent.Hero);
        Core2026Proof.Check(source.AttackDamage == 2 && source.BaseHealth == 5, "Silence removes buffs and trigger");
        Kill(g, source); Play(g, p, "CS2_029", p.Opponent.Hero);
        Core2026Proof.Check(source.AttackDamage == 2, "dead source does not trigger");
        var hand = (Minion)Add(p, id); Play(g, p, "CS2_029", p.Opponent.Hero);
        Core2026Proof.Check(hand.AttackDamage == 2, "hand copy does not trigger");
    });
}
