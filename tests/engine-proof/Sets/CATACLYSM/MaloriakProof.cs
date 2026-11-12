using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MaloriakProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_494", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play accepted");
        var right = Summon(p, "CS2_182");
        var dormantGame = g.CloneExact(history: true); dormantGame.IdEntityDic[left.Id][GameTag.DORMANT] = 1;
        Generic.DiscardBlock(dormantGame.Player1, Add(dormantGame.Player1, "CS2_120"));
        Core2026Proof.Check(dormantGame.Player1.BoardZone[2].Card.Id == "CS2_120"
            && dormantGame.Player1.BoardZone[3].Id == right.Id, "Dormant left neighbour counts for source position");
        var discarded = (Minion)Add(p, "CS2_120"); discarded.AttackDamage = 9; discarded.BaseHealth = 9;
        Generic.DiscardBlock(p, discarded); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Any(m => m.Card.Id == "CS2_120"), "discard summons minion copy");
        var copy = p.BoardZone.Single(m => m.Card.Id == "CS2_120");
        Core2026Proof.Check(copy.Id != discarded.Id && copy.AttackDamage == 2 && copy.Health == 3,
            "discarded minion summons fresh base copy without hand buffs");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, copy.Id, right.Id }), "copy beside Maloriak");
        Generic.DiscardBlock(p, Add(p, "CS2_029")); Generic.DiscardBlock(p.Opponent, Add(p.Opponent, "CS2_171"));
        Core2026Proof.Check(p.BoardZone.Count == 4 && p.Opponent.BoardZone.Count == 0, "spells and opposing discards do not trigger");
        var clone = g.CloneExact(history: true); Generic.DiscardBlock(clone.Player1, Add(clone.Player1, "CS2_171"));
        Core2026Proof.Check(clone.Player1.BoardZone.Count == 5 && p.BoardZone.Count == 4, "discard in clone isolated");
        End(g); End(g); Generic.DiscardBlock(p, Add(p, "CS2_171"));
        Core2026Proof.Check(p.BoardZone.Count == 5 && p.BoardZone[2].Card.Id == "CS2_171", "trigger survives turns and inserts beside source");
        while (p.BoardZone.Count < 7) Summon(p, "CS2_182");
        Generic.DiscardBlock(p, Add(p, "CS2_171")); Core2026Proof.Check(p.BoardZone.Count == 7, "full board discards without summoning");
        Kill(g, right); source.Silence(); g.DeathProcessingAndAuraUpdate();
        Generic.DiscardBlock(p, Add(p, "CS2_171")); Core2026Proof.Check(p.BoardZone.Count == 6, "Silence removes discard trigger");
        Kill(g, source); source = Summon(p, id); Kill(g, source);
        int count = p.BoardZone.Count; Generic.DiscardBlock(p, Add(p, "CS2_171"));
        Core2026Proof.Check(p.BoardZone.Count == count, "dead source no longer triggers");
        source.Card.Power.Trigger.SingleTask.Process(g, p, source, discarded, new TaskStack());
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count == count + 1 && p.BoardZone[count].Card.Id == "CS2_120", "queued effect appends copy when source has left play");
    });
}
