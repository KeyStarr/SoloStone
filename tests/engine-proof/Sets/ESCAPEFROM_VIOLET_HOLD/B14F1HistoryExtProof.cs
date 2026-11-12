using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B14F1VioletHoldProof
{
    internal static void Run()=>StandardSetsProof.Case("CAP_806",Unpinned_RaithEligibleWhileReturnedRebornBodyAlive);
    private static void Unpinned_RaithEligibleWhileReturnedRebornBodyAlive(Game g, Controller p, string id)
    {
        var original=Summon(p,"CS2_120");original.BaseHealth=5;original.AttackDamage=4;original[GameTag.REBORN]=1;Kill(g,original);
        Core2026Proof.Check(p.BoardZone.Count==1,"successful returned body remains alive");
        Play(g,p,id);Core2026Proof.Check(p.BoardZone.Count(m=>m.Card.Id=="CS2_120")==2,"Raith raises source while returned body lives");
        Core2026Proof.Check(p.BoardZone.Single(m=>m.Card.Id=="CS2_120" && m[GameTag.HAS_BEEN_REBORN]==0).Health==3,
            "fresh full-health definition without original buffs");
        var returned=p.BoardZone.Single(m=>m[GameTag.HAS_BEEN_REBORN]>0);Play(g,p,"CAP_801",returned);Kill(g,returned);
        int count=p.BoardZone.Count;Play(g,p,id);
        Core2026Proof.Check(p.BoardZone.Count==count+3,"two successful occurrences give two new bodies plus Raith");
        var raith=p.BoardZone.First(m=>m.Card.Id==id);raith.ActivateTask(PowerActivation.POWER);g.ProcessTasks();g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.IsFull && g.HistoryFacts.Query.Reborns().Count()==2,"overflow lost for invocation; persistent entries reusable");
        var choices=new HashSet<int>();
        foreach(int seed in new[]{1,123456789,2147483646})
        foreach(bool reverse in new[]{false,true})
        {
            var combat=PlayerDecisionProof.Fixture(seed);var corpse=Summon(combat.Player1,"CS2_120");corpse[GameTag.REBORN]=1;Kill(combat,corpse);
            var enemies=new[]{Summon(combat.Player2,reverse?"CS2_120":"CS2_182"),Summon(combat.Player2,reverse?"CS2_182":"CS2_120")};
            foreach(var enemy in enemies) {enemy.AttackDamage=0;enemy.Health=8;}
            if(reverse)for(int i=0;i<7;i++)combat.Random.Next(2);
            var rng=combat.Random.Clone();int selected=rng.Next(2);choices.Add(selected);
            Play(combat,combat.Player1,id);
            var raised=combat.Player1.BoardZone.Single(m=>m.Card.Id=="CS2_120" && m[GameTag.HAS_BEEN_REBORN]==0);
            Core2026Proof.Check(enemies[selected].Damage==2 && enemies[1-selected].Damage==0
                && raised.NumAttacksThisTurn==0 && raised.IsExhausted,"seeded/reordered forced target; normal attack budget unchanged");
        }
        Core2026Proof.Check(choices.Count==2,"target vectors cover both enemy slots");
        var failed=HistoryFactsProof.Fixture();var deathrattle=Summon(failed.Player1,"EX1_556");deathrattle[GameTag.REBORN]=1;
        while(!failed.Player1.BoardZone.IsFull)Summon(failed.Player1,"CS2_231");Kill(failed,deathrattle);
        Core2026Proof.Check(!failed.HistoryFacts.Query.Reborns().Any(),"Deathrattle fills last slot; failed Reborn contributes no entry");
    }
}
