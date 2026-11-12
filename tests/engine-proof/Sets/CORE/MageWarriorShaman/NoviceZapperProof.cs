using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class NoviceZapperProof
{
    internal static void Run() => StandardSetsProof.Case("CS3_007", (g, p, id) => {
        var summoned = Summon(p, id); Kill(g, summoned);
        Core2026Proof.Check(p.OverloadOwed == 0, "summoning does not incur Overload");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 3 && source.Health == 2 && source[GameTag.SPELLPOWER] == 1, "printed stats and keywords");
        Core2026Proof.Check(p.OverloadOwed == 1 && p.OverloadThisGame == 1, "exact Overload amount owed and recorded");
        var clone = g.Clone(); End(clone); End(clone);
        Core2026Proof.Check(clone.Player1.OverloadLocked == 1 && clone.Player1.OverloadOwed == 0 && p.OverloadOwed == 1 && p.OverloadLocked == 0, "clone locks correct amount independently");
        Play(g, p, "CS2_029", p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Damage == 7, "Spell Damage increases Fireball");
        source.Silence(); Play(g, p, "CS2_029", p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 13, "Silence removes Spell Damage");
        source.Silence(); Kill(g, source); End(g); End(g);
        Core2026Proof.Check(p.OverloadLocked == 1 && p.OverloadOwed == 0 && p.Opponent.OverloadLocked == 0, "Silence and death do not cancel owed mana; opponent unaffected");
        End(g); End(g); Core2026Proof.Check(p.OverloadLocked == 0, "mana unlocks following turn");
    });
}
