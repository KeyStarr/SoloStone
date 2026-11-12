using SabberStoneCore.Actions;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class ThassarianProof
{
    internal static void Run() => DeathKnightC2Proof.Case("RLK_223", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 2 && source[SabberStoneCore.Enums.GameTag.REBORN] == 1 && source.AttackDamage == 3 && source.Health == 3,
            "Battlecry deals two to sole enemy, 3/3 Reborn");
        DeathKnightC2Proof.Continue(g, copy => {
            var cp = copy.Player1; Kill(copy, (Minion)copy.IdEntityDic[source.Id]);
            var reborn = cp.BoardZone.Single();
            Core2026Proof.Check(cp.Opponent.Hero.Damage == 4 && reborn.Card.Id == id && reborn.Health == 1 && reborn[SabberStoneCore.Enums.GameTag.REBORN] == 0
                && CorpseRules.Available(cp) == 1, "Deathrattle then fresh one-Health Reborn without Battlecry");
            Kill(copy, reborn);
            Core2026Proof.Check(cp.Opponent.Hero.Damage == 6 && CorpseRules.Available(cp) == 2, "Reborn retains Deathrattle");
        });
        source = Summon(p, id); source.Silence(); g.DeathProcessingAndAuraUpdate(); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 0 && p.Opponent.Hero.Damage == 6, "summon skips Battlecry; Silence removes Deathrattle/Reborn");
        var shield = Summon(p.Opponent, "CS2_182"); shield.HasDivineShield = true; p.Opponent.Hero.IsImmune = true;
        Play(g, p, id);
        Core2026Proof.Check(shield.Health == 5 && p.Opponent.Hero.Damage == 6, "random damage respects Divine Shield and Immune");
    });
}
