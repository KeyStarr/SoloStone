using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class LiferenderProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_614", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_200"); var friendly = Summon(p, "CS2_200");
        var held = Add(p, id);
        Core2026Proof.Check(p.Options().Where(t => t.Source?.Id == held.Id).All(t => t.Target == null), "no health change offers no target");
        PlayEntity(g, p, held); Core2026Proof.Check(enemy.Damage == 0 && ((Minion)held).Health == 4, "negative unchanged Health");
        p.Opponent.Hero.TakeDamage(p.Hero, 1);
        Play(g, p, id); Core2026Proof.Check(enemy.Damage == 0, "opponent Health change excluded");
        p.Hero.Armor = 5; p.Hero.TakeDamage(p.Opponent.Hero, 2);
        Play(g, p, id); Core2026Proof.Check(enemy.Damage == 0, "armor-only hit excluded");
        ClearBoard(g, p);
        p.Hero.TakeDamage(p.Opponent.Hero, 4); p.Hero.TakeHeal(p.Hero, 1);
        Core2026Proof.Check(p.Hero.Health == 30, "damage then heal returns to original Health");
        var source = Add(p, id);
        Core2026Proof.Check(source.Card.ConsumesHistory && g.HistoryFacts.HasConsumers, "declared history consumer");
        var targets = p.Options().Where(t => t.Source?.Id == source.Id).ToArray();
        Core2026Proof.Check(targets.Any(t => t.Target?.Id == enemy.Id) && targets.All(t => t.Target is Minion && t.Target.Controller == p.Opponent), "conditional enemy-minion targeting");
        PlayEntity(g, p, source, enemy); Core2026Proof.Check(enemy.Damage == 6, "change before card creation enables six damage");
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        End(g); End(g);
        enemy = Summon(p.Opponent, "CS2_200");
        Play(g, p, id); Core2026Proof.Check(enemy.Damage == 0, "previous-turn health changes excluded");
        var clone = g.Clone(); var cp = clone.Player1;
        cp.Hero.Health = 20;
        Play(clone, cp, id, (Minion)clone.IdEntityDic[enemy.Id]);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[enemy.Id]).Damage == 6 && enemy.Damage == 0, "clone-only set Health enables effect; original unaffected");
        ClearBoard(g, p); p.Hero.Health = 20;
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.Damage == 6, "set current Health qualifies without damage");
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Single().Health == 4, "satisfied condition with no enemy target remains playable");
    });
}
