using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TemporalConstructProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_858", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); TargetRules(g, p, id, enemy, friend, true);
        int hand = p.HandZone.Count; Play(g, p, id, enemy); Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && p.HandZone.Count == hand, "exact lethal draws zero");
        ClearBoard(g, p); enemy = Summon(p.Opponent, "CS2_182"); enemy.Damage = 3; int count = p.DeckZone.Count;
        Play(g, p, id, enemy); Core2026Proof.Check(p.DeckZone.Count == count - 3 && p.HandZone.Count == hand + 3, "three excess draws three");
        ClearBoard(g, p); enemy = Summon(p.Opponent, "CS2_182"); enemy.BaseHealth = 8; count = p.DeckZone.Count;
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.Damage == 5 && p.DeckZone.Count == count, "nonlethal draws zero");
        ClearBoard(g, p); enemy.HasDivineShield = true; enemy.Damage = 7;
        Play(g, p, id, enemy); Core2026Proof.Check(!enemy.HasDivineShield && enemy.Damage == 7 && p.DeckZone.Count == count, "Shield produces no excess");
        ClearBoard(g, p); enemy.IsImmune = true; var source = Summon(p, id); source.Card.Power.PowerTask.Process(g, p, source, enemy, new TaskStack()); Core2026Proof.Check(enemy.Damage == 7 && p.DeckZone.Count == count, "Immune produces no excess");
        ClearBoard(g, p); enemy.IsImmune = false;
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        Play(g, p, id, enemy); Core2026Proof.Check(p.Hero.Damage == 10 && p.Hero.Fatigue == 4, "four excess attempts on empty deck cause cumulative fatigue");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 1, "no enemy allows untargeted minion");
    });
}
