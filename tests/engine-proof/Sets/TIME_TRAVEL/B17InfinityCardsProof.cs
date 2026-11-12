using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class B17InfinityCardsProof
{
    [GroupProof("b17-infinity-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("END_012", Unpinned_HandFiniteInfinity);
        StandardSetsProof.Case("TIME_024", Unpinned_UnboundedNextStart);
    }

    private static void Unpinned_HandFiniteInfinity(Game g, Controller p, string id)
    {
        Play(g, p, id);
        var weapon = p.Hero.Weapon;
        Core2026Proof.Check(weapon.AttackDamage == int.MaxValue && p.Hero.CantAttackHeroes, "temporary finite infinity and hero restriction");
        new Effect(GameTag.ATK, EffectOperator.ADD, 2).ApplyTo(weapon);
        Core2026Proof.Check(weapon.AttackDamage == int.MaxValue, "weapon growth capped");
        var copiedAttack = Summon(p, "CS2_182");
        Effects.SetAttack(weapon.AttackDamage).ApplyTo(copiedAttack);
        foreach (var future in new[] { g.CloneExact(), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            End(future);
            Core2026Proof.Check(future.Player1.Hero.Weapon.AttackDamage == 6, "expiry removes only setter, retaining later +2 in clone/load");
        }
        End(g);
        Core2026Proof.Check(weapon.AttackDamage == 6 && p.Hero.CantAttackHeroes, "expiry restores surviving weapon modifiers");
        Core2026Proof.Check(copiedAttack.AttackDamage == int.MaxValue, "numeric Attack copy has independent lifetime");
        copiedAttack.Silence();
        Core2026Proof.Check(copiedAttack.AttackDamage == copiedAttack.Card.ATK, "Silence removes copied numeric buff");
    }

    private static void Unpinned_UnboundedNextStart(Game g, Controller p, string id)
    {
        var m = (Minion)Play(g, p, id);
        Core2026Proof.Check(m.AttackDamage == 8 && m.AppliedEnchantments.Any(e => e.Card.Id == "TIME_024e2"), "pending marker and unchanged Attack");
        End(g);
        Core2026Proof.Check(m.AttackDamage == 8, "opponent start does not resolve");
        foreach (var future in new[] { g.CloneExact(), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            End(future);
            Core2026Proof.Check(((Minion)future.IdEntityDic[m.Id]).AttackDamage == int.MaxValue, "pending trigger clone/load");
        }
        End(g);
        Core2026Proof.Check(m.AttackDamage == int.MaxValue && m.AppliedEnchantments.All(e => e.Card.Id != "TIME_024e2")
            && m.AppliedEnchantments.Any(e => e.Card.Id == "TIME_024e"), "pending retired before resolved setter");
        var copy = (Minion)Generic.Copy(p, p.Hero, m, Zone.PLAY);
        Core2026Proof.Check(copy.AttackDamage == int.MaxValue, "exact board copy retains numeric Attack");
        m.Silence();
        Core2026Proof.Check(m.AttackDamage == 8 && copy.AttackDamage == int.MaxValue, "independent copy and Silence");
        copy.Damage = 3;
        new SwapAttackHealthTask(EntityType.TARGET, "EX1_059e").Process(g, p, p.Hero, copy);
        Core2026Proof.Check(copy.AttackDamage == 5 && copy.Health == int.MaxValue && copy.Damage == 0, "damaged Attack/Health swap");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        int count = p.BoardZone.Count;
        int entities = g.IdEntityDic.Count;
        new SummonCopyTask(EntityType.TARGET).Process(g, p, p.Hero, copy);
        Core2026Proof.Check(p.BoardZone.Count == count && g.IdEntityDic.Count == entities, "full board creates no recipient");
        g.DeathProcessingAndAuraUpdate();
    }
}
