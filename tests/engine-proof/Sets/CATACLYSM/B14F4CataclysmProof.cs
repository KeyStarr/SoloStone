using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class B14F4CataclysmProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("CATA_564", Scenario);
        StandardSetsProof.Case("CATA_151", Unpinned_Azshara);
    }
    private static void Scenario(Game g, Controller p, string id)
    {
        var target=Summon(p,"CS2_171"); var enemy=Summon(p.Opponent,"CS2_182");
        target.BaseHealth=100; enemy.BaseHealth=100;
        Core2026Proof.Check(g.Process(MinionAttackTask.Any(p,target,enemy)),"attack before Mega-Windfury grant");
        Play(g,p,id,target);
        Core2026Proof.Check(target[GameTag.MEGA_WINDFURY]==1 && target.CantAttackHeroes,"grant has both restrictions");
        for(int i=1;i<4;i++) Core2026Proof.Check(g.Process(MinionAttackTask.Any(p,target,enemy)),"four attacks: "+i);
        Core2026Proof.Check(!target.CanAttack && !g.Process(MinionAttackTask.Any(p,target,enemy)),"no fifth attack");
        target.Silence(); Core2026Proof.Check(target[GameTag.MEGA_WINDFURY]==0 && !target.CantAttackHeroes,"grant silenced");
    }

    private static void Unpinned_Azshara(Game g, Controller p, string id)
    {
        var body=(Minion)Play(g,p,id);
        Core2026Proof.Check(p.BoardZone.Count==3 && p.Hero.HasWindfury && p.Hero.AttackDamage==2,"appendages summon first and grant temporary hero Attack");
        body.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(!p.Hero.HasWindfury && p.BoardZone.Count==3,"Silence removes hero aura, not appendages");
        End(g); Core2026Proof.Check(p.Hero.AttackDamage==0,"tentacle Attack expires");
    }
}
