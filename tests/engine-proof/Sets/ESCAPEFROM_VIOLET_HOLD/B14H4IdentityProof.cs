using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;
internal static partial class B14H4IdentityProof
{
    private static void VioletHold()=>StandardSetsProof.Case("JAIL_200",Unpinned_Scullery);
    private static void Unpinned_Scullery(Game g,Controller p,string id)
    {
        Unpinned_SculleryAttackBoundaries(g.Seed);
        Pools(g,id);var held=Add(p,id);var target=Summon(p.Opponent,"CS2_200");target.BaseHealth=100;target.AttackDamage=0;
        Play(g,p,"CS2_106");
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p,target)),"hero combat accepted");
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1]==3,"Unpinned_Scullery: one hero combat improves to three");
        Future(g,next=>{PlayEntity(next,next.Player1,next.IdEntityDic[held.Id]);Core2026Proof.Check(next.Player1.BoardZone.Count==2 && next.Player1.BoardZone.All(m=>m.Card.Cost==3),"Unpinned_Scullery: two exact improved summons");});
        ParkBoard(p);EmptyHand(p);
        for(int i=0;i<10;i++)
        { p.Hero.NumAttacksThisTurn=0;p.Hero.IsExhausted=false;p.Hero.AttackDamage=1;Core2026Proof.Check(g.Process(HeroAttackTask.Any(p,target)),"repeat hero combat"); }
        var above=Add(p,id);g.AuraUpdate();Core2026Proof.Check(above[GameTag.TAG_SCRIPT_DATA_NUM_1]==13,$"Unpinned_Scullery: bucket uncapped above ten actual={above[GameTag.TAG_SCRIPT_DATA_NUM_1]}");
        PlayEntity(g,p,above);Core2026Proof.Check(p.BoardZone.Count==0,"Unpinned_Scullery: empty exact bucket has no fallback");
    }
    private static void Unpinned_SculleryAttackBoundaries(long seed)
    {
        var g=DeathKnightC1Proof.NewGame(seed,CardClass.ROGUE);var p=g.Player1;
        var spell=Add(p,"JAIL_200");p.Hero.AttackDamage=1;
        var shield=Summon(p.Opponent,"CS2_231");shield.HasDivineShield=true;
        new AttackTask(EntityType.SOURCE,EntityType.TARGET).Process(g,p,p.Hero,shield);g.AuraUpdate();
        Core2026Proof.Check(spell[GameTag.TAG_SCRIPT_DATA_NUM_1]==3 && !shield.HasDivineShield && shield.Damage==0,
            "Unpinned_SculleryShieldedForcedCombat: forced hero attack with zero damage counts once");
        var attacker=Summon(p,"CS2_171");Attack(g,attacker,p.Opponent.Hero);
        Core2026Proof.Check(spell[GameTag.TAG_SCRIPT_DATA_NUM_1]==3,"minion combat does not improve Scullery");
        p.Opponent.Hero.AttackDamage=1;
        new AttackTask(EntityType.SOURCE,EntityType.TARGET).Process(g,p.Opponent,p.Opponent.Hero,p.Hero);g.AuraUpdate();
        Core2026Proof.Check(spell[GameTag.TAG_SCRIPT_DATA_NUM_1]==3,"opponent hero combat does not improve Scullery");
        var victim=Summon(p.Opponent,"CS2_231");
        SabberStoneCore.Model.TriggerManager.TriggerHandler cancel=_=>victim.ToBeDestroyed=true;
        g.TriggerManager.AttackTrigger+=cancel;
        new AttackTask(EntityType.SOURCE,EntityType.TARGET).Process(g,p,p.Hero,victim);
        g.TriggerManager.AttackTrigger-=cancel;g.AuraUpdate();
        Core2026Proof.Check(g.HistoryFacts.Query.AttacksFinished().Last().Cancelled && spell[GameTag.TAG_SCRIPT_DATA_NUM_1]==3,
            "Unpinned_SculleryCancelledHeroAttack: accepted proposal without combat gives no progress");
    }

}
