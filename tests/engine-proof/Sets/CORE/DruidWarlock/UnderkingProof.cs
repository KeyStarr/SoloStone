using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class UnderkingProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_RLK_657", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRush && source.IsRace(Race.UNDEAD) && source.AttackDamage == 6 && source.Health == 6
            && p.Hero.Armor == 6 && p.Opponent.Hero.Armor == 0, "printed Rush Undead and Battlecry armor");
        var attacks = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source == source).ToArray();
        Core2026Proof.Check(attacks.Any(t => t.Target == enemy) && attacks.All(t => t.Target is Minion), "printed Rush attacks minions on play turn");
        Kill(g, source); Core2026Proof.Check(p.Hero.Armor == 12, "death adds six armor");
        source = (Minion)Play(g, p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.Hero.Armor == 18, "Silence removes Deathrattle");
        var opposing = Summon(p.Opponent, id); Kill(g, opposing);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 6 && p.Hero.Armor == 18, "Deathrattle belongs to current controller");
    });
}
