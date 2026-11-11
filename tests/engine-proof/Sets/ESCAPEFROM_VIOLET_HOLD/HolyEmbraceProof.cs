using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class HolyEmbraceProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_941", (g, p, id) => {

        var own = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var source = Add(p, id);
        var options = p.Options().Where(t => t.Source?.Id == source.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(new[] { p.Hero.Id, p.Opponent.Hero.Id, own.Id, enemy.Id }.All(target => options.Any(t => t.Target?.Id == target)), "healing targets either hero or minion");
        p.Hero.Damage = 7; PlayEntity(g, p, source, p.Hero);
        var dark = p.HandZone.Single(); Core2026Proof.Check(p.Hero.Damage == 3 && dark.Card.Id == "JAIL_941t" && dark.Cost == 2 && dark.Card.Implemented, "four healing plus exact Dark Embrace");
        PlayEntity(g, p, dark, enemy); Core2026Proof.Check(enemy.Health == 1 && p.HandZone.Count == 0, "Dark deals four without generating more cards");
        own.Damage = 2; Play(g, p, id, own); Core2026Proof.Check(own.Health == 5, "healing capped at maximum Health");
        dark = p.HandZone.Single(); Summon(p, "EX1_012"); PlayEntity(g, p, dark, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 5, "Dark gains spell damage and targets heroes");
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        source.Card.Power.PowerTask.Process(g, p, source, p.Hero, new TaskStack());
        Core2026Proof.Check(p.HandZone.Count == 10 && p.Hero.Damage == 0, "full hand burns Dark while healing remains");
    });
}
