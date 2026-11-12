using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class LightcallerProof
{
    internal static void Run() => StandardSetsProof.Case("BE_000", Unpinned_Lightcaller);
    private static void Unpinned_Lightcaller(Game g, Controller p, string id)
    {
        Play(g, p, id); var victim = Summon(p.Opponent, "CS2_182"); victim.BaseHealth = 20;
        p.Hero.Damage = 10; p.Opponent.Hero.Damage = 10; p.Hero.IsExhausted = false;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, victim)), "Lightcaller attack accepted");
        Core2026Proof.Check(p.Hero.Damage == 10 && p.Opponent.Hero.Damage == 6 && victim.Damage == 4,
            "Unpinned_Lightcaller each hero heals four; attacking hero also takes retaliation four");
        End(g); End(g); p.Hero.Damage = 15; p.Opponent.Hero.Damage = 28;
        p.Opponent.ControllerAuraEffects[GameTag.ALL_HEALING_DOUBLE] = 1;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "lethal face attack accepted");
        Core2026Proof.Check(g.State != State.COMPLETE && p.Opponent.Hero.Health == 6 && !p.Opponent.Hero.ToBeDestroyed
            && p.Hero.Damage == 11, "Unpinned_Lightcaller second recipient heals with own modifiers before final lethal check");
        Core2026Proof.Check(p.Hero.Weapon == null, "last durability consumed");
    }
}
