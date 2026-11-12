using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TankRegimentProof
{
    internal static void Run() => StandardSetsProof.Case("BE_550", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var source = (Minion)Add(p, id); source.AttackDamage = 5; source.BaseHealth = 6;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle Regiment play");
        var copies = p.BoardZone.Where(m => m.Card.Id == id && m != source).OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(copies.Length == 2 && copies.All(m => m.AttackDamage == 5 && m.Health == 6 && m.HasDeathrattle), "two buffed copies with Deathrattle");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, copies[0].Id, copies[1].Id, right.Id }), "copies right in creation order");
        int health = p.Opponent.Hero.Health; Kill(g, copies[0]); Core2026Proof.Check(p.Opponent.Hero.Health == health - 3, "copy Deathrattle hits only enemy hero");
        ClearBoard(g, p); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == id) == 2, "partial board space");
        ClearBoard(g, p);
        left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182");
        Kill(g, source); source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        copies = p.BoardZone.Where(m => m.Card.Id == id).OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(copies.Length == 2 && copies.All(m => m.AttackDamage == 3 && m.Health == 3) && p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, right.Id, copies[0].Id, copies[1].Id }), "removed source appends two 3/3 copies in creation order");
        g.DeathProcessingAndAuraUpdate();
    });
}
