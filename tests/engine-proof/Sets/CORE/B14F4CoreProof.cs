using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class B14F4CoreProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("CORE_BT_187", Scenario);
        StandardSetsProof.Case("CORE_BT_781", Bulwark);
        StandardSetsProof.Case("CORE_SCH_605", Thresher);
    }
    private static void Scenario(Game g, Controller p, string id)
    {
        var guard=Summon(p.Opponent,"CS2_121"); guard.HasTaunt=true;
        var attacker=Summon(p,"CS2_171"); var body=(Minion)Play(g,p,id);
        Core2026Proof.Check(body.HasCharge && attacker.ValidAttackTargets.Contains(p.Opponent.Hero),"friendly attacks ignore Taunt");
        Core2026Proof.Check(p.Options().Any(t=>t.PlayerTaskType==PlayerTaskType.MINION_ATTACK && t.Source?.Id==attacker.Id && t.Target==p.Opponent.Hero),
            "player options also bypass Taunt");
        p.Hero.AttackDamage=1; Core2026Proof.Check(p.Hero.ValidAttackTargets.Contains(p.Opponent.Hero),"hero also bypasses Taunt");
        body.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(!attacker.ValidAttackTargets.Contains(p.Opponent.Hero),"Silence restores Taunt");
    }

    private static void Bulwark(Game g, Controller p, string id)
    {
        Play(g,p,id); p.Hero.Armor=9; var source=Summon(p.Opponent,"CS2_182");
        for(int i=0;i<4;i++) { p.Hero.TakeDamage(source,4); g.DeathProcessingAndAuraUpdate(); }
        Core2026Proof.Check(p.Hero.Damage==0 && p.Hero.Armor==9 && p.Hero.Weapon==null,"four hits replace before Armor and break weapon");
        p.Hero.TakeDamage(source,4); Core2026Proof.Check(p.Hero.Armor==5,"damage resumes after break");
    }
    private static void Thresher(Game g, Controller p, string id)
    {
        var left=Summon(p.Opponent,"CS2_182"); var center=Summon(p.Opponent,"CS2_182"); var right=Summon(p.Opponent,"CS2_182");
        foreach(var m in p.Opponent.BoardZone) m.BaseHealth=30;
        var body=(Minion)Play(g,p,id); End(g); End(g);
        Core2026Proof.Check(g.Process(MinionAttackTask.Any(p,body,center)) && left.Damage==4 && center.Damage==4 && right.Damage==4
            && body.Damage==4,"cleave damages neighbors with one retaliation");
    }
}
