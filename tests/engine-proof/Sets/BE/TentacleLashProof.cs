using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
internal static class TentacleLashProof
{
    internal static void Run() => StandardSetsProof.Case("BE_771",Unpinned_AttackDiscountAndChoice);
    private static void Unpinned_AttackDiscountAndChoice(Game g,SabberStoneCore.Model.Entities.Controller p,string id)
    {
        Play(g,p,id); var cthun = Add(p,"BE_013");
        var minion = Summon(p,"CS2_182"); Attack(g,minion,p.Opponent.Hero);
        Core2026Proof.Check(p.Choice == null && cthun.Cost == 10,"minion attacks do not trigger Lash");
        p.Hero.IsExhausted = false;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p,p.Opponent.Hero)),"hero attack accepted");
        Core2026Proof.Check(p.Choice != null && cthun.Cost == 9 && Add(p,"BE_013").Cost == 9,"discount precedes choice and includes future C'Thun copies");
        CthunRulesProof.Pick(g,"BE_013t4");
        End(g); End(g); p.Hero.IsExhausted = false;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p,p.Opponent.Hero)),"last durability attack accepted");
        Core2026Proof.Check(p.Choice != null && p.Hero.Weapon == null && cthun.Cost == 8,"last durability still discounts and offers choice");
        CthunRulesProof.Pick(g,"BE_013t4");
        End(g); End(g); Play(g,p,"CS2_106"); p.Hero.IsExhausted = false;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p,p.Opponent.Hero)) && p.Choice == null && cthun.Cost == 8,"removed Lash no longer triggers, discounts persist across turns and replacement");
    }
}
