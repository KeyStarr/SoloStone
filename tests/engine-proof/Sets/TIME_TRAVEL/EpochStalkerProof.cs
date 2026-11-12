using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EpochStalkerProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_605", (g, p, id) => {
        var hand = (Minion)Add(p, id); hand.AttackDamage = 5; hand.BaseHealth = 6;
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        p.BaseMana = 10; p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, hand, zonePosition: left.ZonePosition + 1)), "middle play accepted");
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == id) == 2 && p.BoardZone.Where(m => m.Card.Id == id).All(m => m.Card.Id == id && m.AttackDamage == 5 && m.Health == 6 && m.IsRush && m.CantBeTargetedBySpells), "copy preserves buffed stats and keywords");
        var copy = p.BoardZone.Single(m => m.Card.Id == id && m.Id != hand.Id);
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, hand.Id, copy.Id, right.Id }), "copy immediately right of source between neighbours");
        var enemy = Summon(p.Opponent, "CS2_182");
        AttackOptionsProof.Lifecycle(g, hand, enemy, rush: true);
        AttackOptionsProof.Lifecycle(g, copy, enemy, rush: true);
        Elusive(g, p, hand); ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == id) == 1, "full board suppresses copy");
    });
}
