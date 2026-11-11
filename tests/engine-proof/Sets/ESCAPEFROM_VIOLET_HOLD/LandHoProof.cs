using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class LandHoProof
{
    internal static void Run() => StandardSetsProof.Case("CAP_102", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182"); int deck = p.DeckZone.Count;
        Play(g, p, id); var tokens = p.BoardZone.Where(m => m.Card.Id == "CAP_107t").OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(p.DeckZone.Count == deck - 2 && p.HandZone.Count == 2, "draws two cards");
        Core2026Proof.Check(tokens.Length == 2 && tokens.All(m => m.AttackDamage == 1 && m.Health == 1 && m.Cost == 1 && m.Card.Implemented && m.IsRace(Race.PIRATE) && !m.CanAttack), "two exact 1/1 Pirate Cannoneers");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, right.Id, tokens[0].Id, tokens[1].Id }), "spell tokens append in creation order");
        var clone = g.CloneExact(history: true); int health = p.Opponent.Hero.Health; End(clone);
        Core2026Proof.Check(clone.Player2.Hero.Health == health - 2 && p.Opponent.Hero.Health == health, "two turn-end shots and clone isolation");
        tokens[0].Silence(); Kill(g, tokens[1]); End(g);
        Core2026Proof.Check(p.Opponent.Hero.Health == health, "silenced and dead Cannoneers do not fire"); End(g);
        while (p.BoardZone.Count < 6) Summon(p, "CS2_182"); deck = p.DeckZone.Count;
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7 && p.DeckZone.Count == deck - 2, "one free board slot still draws two and summons one");
        deck = p.DeckZone.Count; Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7 && p.DeckZone.Count == deck - 2, "full board still draws two");
    });
}
