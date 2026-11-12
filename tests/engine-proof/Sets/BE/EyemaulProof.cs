using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EyemaulProof
{
    internal static void Run() => StandardSetsProof.Case("BE_349", (g, p, id) => {
        Play(g, p, id); var weapon = p.Hero.Weapon;
        Core2026Proof.Check(weapon.AttackDamage == 2 && weapon.Durability == 2, "weapon stats");
        var left = Summon(p, "CS2_182"); Attack(g, left, p.Opponent.Hero);
        Core2026Proof.Check(p.BoardZone.Count == 1, "minion attack does not trigger");
        p.Hero.IsExhausted = false;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "hero attack accepted");
        var tokens = p.BoardZone.Where(m => m.Card.Id == "BE_349t").ToArray();
        Core2026Proof.Check(tokens.Length == 2 && tokens.All(m => m.AttackDamage == 2 && m.Health == 2 && m.IsRush && m.IsRace(Race.ELEMENTAL) && m.Card.Implemented) && p.BoardZone[0] == left && tokens[0].Id < tokens[1].Id, "two printed Rush Elementals appended in creation order");
        var clone = g.Clone(); End(clone); End(clone); var cp = clone.Player1; cp.Hero.IsExhausted = false;
        Core2026Proof.Check(clone.Process(HeroAttackTask.Any(cp, cp.Opponent.Hero)) && cp.BoardZone.Count == 5 && p.BoardZone.Count == 3 && weapon.Durability == 1, "weapon trigger clone isolated");
        End(g); var enemyAttacker = Summon(p.Opponent, "CS2_182"); Attack(g, enemyAttacker, p.Hero);
        Core2026Proof.Check(p.BoardZone.Count == 3, "opponent attack excluded"); End(g);
        for (int i = p.BoardZone.Count; i < 6; i++) Summon(p, "CS2_182");
        p.Hero.IsExhausted = false; Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "last durability attack accepted");
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "BE_349t") == 3 && p.Hero.Weapon == null, "last durability triggers and board limit respected");
        End(g); End(g); Play(g, p, "CS2_106"); p.Hero.IsExhausted = false;
        Kill(g, p.BoardZone[0]); Kill(g, p.BoardZone[0]);
        int remainingTokens = p.BoardZone.Count(m => m.Card.Id == "BE_349t");
        Core2026Proof.Check(p.BoardZone.Count == 5, "replacement weapon attack has two free slots");
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && p.BoardZone.Count == 5
            && p.BoardZone.Count(m => m.Card.Id == "BE_349t") == remainingTokens, "removed weapon trigger inactive");
    });
}
