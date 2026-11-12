using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class Batch14K7TimeTravelProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_022", Serpent);

    private static void Serpent(Game g, Controller p, string id)
    {
        var held = Add(p, id);
        Core2026Proof.Check(held.Cost == 8 && held[GameTag.COST] == 8, "no Dormant minion means printed cost");
        Summon(p, "CS2_182");
        var dormant = Summon(p.Opponent, "EDR_416t");
        Core2026Proof.Check(dormant.IsDormant && held.Cost == 4 && held[GameTag.COST] == 4, "enemy Dormant minion reduces visible cost by four");
        var clone = g.CloneExact(history: true);
        DormantRules.Awaken((Minion)clone.IdEntityDic[dormant.Id]); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(clone.IdEntityDic[held.Id].Cost == 8 && held.Cost == 4, "awakening restores cost, clone independent");
        var own = Summon(p, "EDR_416t");
        Core2026Proof.Check(held.Cost == 4, "multiple Dormant minions do not stack discount");
        DormantRules.Awaken(dormant); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(held.Cost == 4, "friendly Dormant minion alone qualifies");
        var copy = Generic.Copy(p, p.Hero, held, Zone.HAND);
        Core2026Proof.Check(copy.Cost == 4, "hand copy recomputes Dormant predicate");
        DormantRules.Awaken(own); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(held.Cost == 8 && copy.Cost == 8, "last Dormant awakening restores both costs");
        var next = Summon(p.Opponent, "EDR_416t");
        PlayEntity(g, p, held);
        Core2026Proof.Check(held is Minion m && m.AttackDamage == 7 && m.Health == 9 && m.IsRush && p.UsedMana == 4, "discounted play pays four; exact 7/9 Rush body");
        ((Minion)held).Silence();
        Core2026Proof.Check(!((Minion)held).IsRush && held.OngoingEffect == null, "Silence removes printed Rush and self aura");
        End(g); End(g);
        Core2026Proof.Check(copy.Cost == 4, "one owner turn leaves Dormant timer running");
        End(g); End(g);
        Core2026Proof.Check(copy.Cost == 8, "normal timer awakening updates held cost across turns");
    }
}
