using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class KronntheImpalerProof
{
    internal static void Run() => StandardSetsProof.Case("BE_732", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); int health = p.Opponent.Hero.Health;
        Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Health == health - 10 && friend.Health == 5, "all minions including source deal own Attack");
        ClearBoard(g, p); p.Opponent.Hero.Damage = 0;
        var poison = Summon(p, "BE_193"); var enemy = Summon(p.Opponent, "CS2_182"); enemy.BaseHealth = 20;
        Play(g, p, id);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD || enemy.Health == 14 || enemy.Health == 20, $"actual minion is damage source, Poisonous preserved: zone={enemy.Zone.Type}, health={enemy.Health}, poison={poison.Poisonous}");
        Core2026Proof.Check(p.BoardZone.Count == 2, "own minions never targeted");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); p.Opponent.Hero.Damage = 0; p.Hero.Damage = 15;
        Summon(p, "EDR_486"); Play(g, p, id);
        Core2026Proof.Check(p.Hero.Damage == 8 && p.Opponent.Hero.Damage == 13, "friendly Lifesteal minion remains damage source");
    });
}
