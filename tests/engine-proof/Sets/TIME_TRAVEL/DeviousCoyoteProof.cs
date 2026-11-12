using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DeviousCoyoteProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_047", (g, p, id) => {
        p.Hero.TakeDamage(p.Opponent.Hero, 1);
        var held = Add(p, id); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(held.Cost == 5 && held[GameTag.COST] == 5 && held.Card.ConsumesHistory, "own hero damage does not discount");
        p.Opponent.Hero.Armor = 20;
        p.Opponent.Hero.TakeDamage(p.Opponent.Hero, 4);
        p.Opponent.Hero.TakeDamage(p.Hero, 2);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(held.Cost == 3 && held[GameTag.COST] == 3, "two armor hits count twice, any source, not six damage");
        var late = Add(p, id); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(late.Cost == 3, "history before creation retained");
        p.Opponent.Hero.IsImmune = true; p.Opponent.Hero.TakeDamage(p.Hero, 1); p.Opponent.Hero.IsImmune = false;
        p.Opponent.Hero.TakeDamage(p.Hero, 0); p.Opponent.Hero.Health = 20;
        g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(held.Cost == 3, "Immune/zero/set Health do not count");
        var clone = g.Clone(); clone.Player2.Hero.TakeDamage(clone.Player1.Hero, 1); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(clone.IdEntityDic[held.Id].Cost == 2 && held.Cost == 3, "clone damage/cost isolated");
        for (int i = 0; i < 6; i++) p.Opponent.Hero.TakeDamage(p.Hero, 1);
        g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(held.Cost == 0 && held[GameTag.COST] == 0, "discount floors at zero");
        PlayEntity(g, p, held); var body = (Minion)held;
        Core2026Proof.Check(body.AttackDamage == 5 && body.Health == 3 && body.HasStealth, "printed stats and Stealth preserved");
        End(g); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(late.Cost == 5, "global turn boundary resets discount");
        p.Opponent.Hero.TakeDamage(p.Opponent.Hero, 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(late.Cost == 4, "enemy self damage during enemy turn counts this global turn");
        End(g); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(late.Cost == 5, "opponent previous-turn hits excluded");
    });
}
