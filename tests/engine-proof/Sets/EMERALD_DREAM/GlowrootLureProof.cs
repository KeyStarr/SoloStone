using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static EmeraldDreamBatch3Proof;

internal static class GlowrootLureProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_477", (g, p, id) => {
        var held = Add(p, id); Cost(held, 6); UsePower(g, p); Cost(held, 5);
        Core2026Proof.Check(p.NumTimesHeroPowerUsedThisGame == 1, "core increments cumulative power count");
        var clone = g.Clone(); End(clone); End(clone); UsePower(clone, clone.Player1);
        Cost(clone.IdEntityDic[held.Id], 4); Cost(held, 5);
        End(g); UsePower(g, p.Opponent); Cost(held, 5);
        End(g); Cost(held, 5); var later = Add(p, id); g.DeathProcessingAndAuraUpdate(); Cost(later, 5);
        for (int i = 0; i < 6; i++) { UsePower(g, p); End(g); End(g); }
        Cost(held, 0); Cost(later, 0);
        PlayEntity(g, p, held); var source = (Minion)held;
        Core2026Proof.Check(source.HasTaunt && source.AttackDamage == 6 && source.Health == 6, "printed stats on free play");
        Core2026Proof.Check(source.OngoingEffect == null, "hand cost effect removed on play");
        source.Silence(); Core2026Proof.Check(!source.HasTaunt && source.OngoingEffect == null, "Silence does not reinstall hand effect");
        Kill(g, source); Core2026Proof.Check(source.OngoingEffect == null, "no hand aura survives source death");
    });
}
