using SabberStoneCore.Actions;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
internal static class B16CopyExtNythendraProof
{
    internal static void Run()=>StandardSetsProof.Case("EDR_818",Unpinned_Nythendra);
    private static void Unpinned_Nythendra(Game g,Controller p,string id)
    {
        var source=Summon(p,id);new Enchant(Effects.SetAttack(3)).ActivateTo(source);Kill(g,source);
        Core2026Proof.Check(p.BoardZone.Count==3 && p.BoardZone.All(m=>m.Card.Id=="EDR_818t"),"split count uses death Attack");
        var enemy=Summon(p.Opponent,"EDR_818t");enemy.Silence();
        p.BoardZone[0].Silence();new Enchant(Effects.Attack_N(4),Effects.Health_N(4)).ActivateTo(p.BoardZone[1]);
        foreach(var branch in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g})
        {
            int deaths=branch.Player1.NumFriendlyMinionsThatDiedThisGame;
            End(branch);End(branch);
            var result=branch.Player1.BoardZone.Single();
            var consumed=branch.Player1.SetasideZone.First(m=>m.Card.Id=="EDR_818t");
            new SabberStoneCore.Tasks.SimpleTasks.DestroyTask(EntityType.TARGET)
                .Process(branch,branch.Player1,result,consumed);
            branch.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(result.Card.Id==id && result.AttackDamage==3 && result.Health==3,"all friendly Beetles merge, buffs discarded");
            Core2026Proof.Check(branch.Player1.NumFriendlyMinionsThatDiedThisGame==deaths,"consumption makes no death credit");
            result.Silence();Core2026Proof.Check(result.AttackDamage==3 && result.Health==3,"reformed base stats survive Silence");
            Core2026Proof.Check(branch.Player2.BoardZone.Single().Card.Id=="EDR_818t","opponent Beetle excluded");
        }
    }
}
