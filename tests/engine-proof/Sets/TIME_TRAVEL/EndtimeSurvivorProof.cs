using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EndtimeSurvivorProof
{
    internal static void Run() => StandardSetsProof.Case("END_019", (g, p, id) => {
        var body = (Minion)Play(g, p, id);
        Core2026Proof.Check(body.AttackDamage == 5 && body.Health == 6 && body.HasTaunt && body.Card.ConsumesHistory, "negative and printed Taunt/stats");
        p.Opponent.Hero.TakeDamage(p.Hero, 1);
        body = (Minion)Play(g, p, id); Core2026Proof.Check(body.AttackDamage == 5 && body.Health == 6, "opponent damage excluded");
        p.Hero.Health = 20;
        body = (Minion)Play(g, p, id); Core2026Proof.Check(body.AttackDamage == 5, "set Health is not damage");
        p.Hero.IsImmune = true; p.Hero.TakeDamage(p.Opponent.Hero, 1); p.Hero.IsImmune = false;
        p.Hero.TakeDamage(p.Hero, 0);
        body = (Minion)Play(g, p, id); Core2026Proof.Check(body.AttackDamage == 5, "zero and prevented damage excluded");
        ClearBoard(g, p);
        var clone = g.Clone(); clone.Player1.Hero.Armor = 5; clone.Player1.Hero.TakeDamage(clone.Player2.Hero, 1);
        body = (Minion)Play(clone, clone.Player1, id);
        Core2026Proof.Check(body.AttackDamage == 8 && body.Health == 9 && p.BoardZone.IsEmpty, "clone-only armor hit enables buff, original unaffected");
        p.Hero.Armor = 5; p.Hero.TakeDamage(p.Hero, 2);
        body = (Minion)Play(g, p, id);
        Core2026Proof.Check(body.AttackDamage == 8 && body.Health == 9 && body.HasTaunt
            && body.AppliedEnchantments.Single().Card.Id == "END_019e", "armor hit before creation qualifies; exact enchantment");
        body.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(body.AttackDamage == 5 && body.Health == 6 && !body.HasTaunt, "silence removes buff and Taunt");
        End(g); p.Hero.TakeDamage(p.Opponent.Hero, 1); End(g);
        body = (Minion)Play(g, p, id); Core2026Proof.Check(body.AttackDamage == 5 && body.Health == 6, "damage on opponent's previous turn excluded");
    });
}
