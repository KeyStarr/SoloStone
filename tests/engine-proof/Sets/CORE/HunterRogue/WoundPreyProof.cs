using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class WoundPreyProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BAR_801", (g, p, id) => {

        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var spell = Add(p, id);
        foreach (var target in new ICharacter[] { friendly, enemy, p.Hero, p.Opponent.Hero })
            Core2026Proof.Check(p.Options().Any(t => t.Source?.Id == spell.Id && t.Target?.Id == target.Id), "any character may be targeted");
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id && t.Target == null), "target required");
        PlayEntity(g, p, spell, enemy);
        var hyena = p.BoardZone.Single(m => m.Card.Id == "BAR_035t");
        Core2026Proof.Check(enemy.Damage == 1 && hyena.AttackDamage == 1 && hyena.Health == 1 && hyena.IsRush && hyena.IsRace(Race.BEAST) && hyena.Card.Implemented, "one damage and exact 1/1 Rush Beast");
        Core2026Proof.Check(p.BoardZone.Last() == hyena, "spell token appends");
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == hyena.Id && t.Target == p.Opponent.Hero), "new Rush token cannot attack hero");
        Attack(g, hyena, enemy); Core2026Proof.Check(enemy.Damage == 2, "printed Rush permits immediate minion attack");
        p.Hero.HasDivineShield = true; Play(g, p, id, p.Hero);
        Core2026Proof.Check(!p.Hero.HasDivineShield && p.Hero.Damage == 0 && p.BoardZone.Count(m => m.Card.Id == "BAR_035t") == 1, "hero shield blocks damage but not summon");
        Summon(p, "EX1_012"); Play(g, p, id, enemy); Core2026Proof.Check(enemy.Damage == 4, "Spell Damage applies");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Play(g, p, id, p.Opponent.Hero); Core2026Proof.Check(p.BoardZone.Count == 7 && p.Opponent.Hero.Damage == 2, "full board still deals scaled damage");
        ClearBoard(g, p); var lethal = Summon(p.Opponent, "CS2_171"); Play(g, p, id, lethal);
        Core2026Proof.Check(lethal.Zone == p.Opponent.GraveyardZone && p.BoardZone.Single().Card.Id == "BAR_035t", "lethal damage still summons");
    });
}
