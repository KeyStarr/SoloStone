using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class VerdantDreamsaberProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_014", (g, p, id) => {
        var one = Summon(p.Opponent, "CS2_182"); var two = Summon(p.Opponent, "CS2_182");
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.Damage == 0 && one.Damage == 0 && two.Damage == 0, "printed five cost does not attack");
        ClearBoard(g, p); source = (Minion)Add(p, id); source.Cost = 4; PlayEntity(g, p, source);
        Core2026Proof.Check(source.Damage == 0 && one.Damage == 0 && two.Damage == 0, "four cost fails boundary");
        ClearBoard(g, p); one.AttackDamage = 1; two.AttackDamage = 1;
        source = (Minion)Add(p, id); source.Cost = 3; PlayEntity(g, p, source);
        Core2026Proof.Check(one.Damage == 4 && two.Damage == 4 && source.Damage == 2 && p.Opponent.Hero.Damage == 0, "three cost attacks two distinct enemy minions and takes retaliation");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); one = Summon(p.Opponent, "CS2_182"); one.AttackDamage = 1;
        source = (Minion)Add(p, id); source.Cost = 2; PlayEntity(g, p, source);
        Core2026Proof.Check(source.Damage == 1 && one.Damage == 4, "single enemy attacked only once");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); one = Summon(p.Opponent, "CS2_182"); two = Summon(p.Opponent, "CS2_182"); one.AttackDamage = 7; two.AttackDamage = 7;
        source = (Minion)Add(p, id); source.Cost = 3; PlayEntity(g, p, source);
        Core2026Proof.Check(source.Zone.Type == Zone.GRAVEYARD && new[] { one, two }.Count(m => m.Damage == 4) == 1, "death during first attack stops second attack");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); one = Summon(p.Opponent, "CS2_182"); one.AttackDamage = 1;
        source = (Minion)Add(p, id); source.Cost = 0; PlayEntity(g, p, source);
        Core2026Proof.Check(source.Damage == 1 && one.Damage == 4, "zero-cost play also meets condition");
        ClearBoard(g, p.Opponent); source = (Minion)Add(p, id); source.Cost = 3; PlayEntity(g, p, source);
        Core2026Proof.Check(source.Damage == 0 && p.Opponent.Hero.Damage == 0, "empty enemy board causes no attack");
    });
}
