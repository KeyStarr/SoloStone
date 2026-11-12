using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RedgillRazorjawProof
{
    internal static void Run() => StandardSetsProof.Case("CS3_038", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        var enemy = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(source.IsRush && source.IsRace(Race.MURLOC) && source.AttackDamage == 3 && source.Health == 1, "printed Rush Murloc 3/1");
        var attacks = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == source.Id).ToArray();
        Core2026Proof.Check(attacks.Any(t => t.Target?.Id == enemy.Id) && attacks.All(t => t.Target?.Id != p.Opponent.Hero.Id), "Rush immediately attacks minions, not heroes");
        Core2026Proof.Check(g.Process(attacks.First(t => t.Target?.Id == enemy.Id)), "immediate Rush attack accepted");
        Core2026Proof.Check(enemy.Damage == 3 && source.Zone == p.GraveyardZone, "combat deals printed damage and kills one-Health source");
        var later = Summon(p, id); End(g); End(g);
        Core2026Proof.Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == later.Id
            && t.Target?.Id == p.Opponent.Hero.Id), "Rush minion can attack hero on following friendly turn");
    });
}
