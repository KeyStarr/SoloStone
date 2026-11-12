using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B14F1CoreProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("CORE_BAR_313", PriestOfAnshe);
        StandardSetsProof.Case("CORE_ETC_523", DeathMetalKnight);
        StandardSetsProof.Case("CORE_RLK_116", NecroticMortician);
    }
    private static void PriestOfAnshe(Game g, Controller p, string id)
    {
        var target=Summon(p,"CS2_182");target.TakeDamage(p.Hero,1);target.TakeHeal(p.Hero,1);
        var priest=(Minion)Play(g,p,id);Core2026Proof.Check(priest.AttackDamage==8 && priest.Health==8,"friendly minion healing qualifies");
        End(g);End(g);var ordinary=(Minion)Play(g,p,id);
        Core2026Proof.Check(ordinary.AttackDamage==5,"old-turn healing expires");
    }
    private static void DeathMetalKnight(Game g, Controller p, string id)
    {
        p.Hero.TakeDamage(p.Opponent.Hero,2);p.Hero.TakeHeal(p.Hero,1);p.UsedMana=10;
        var card=Add(p,id);int health=p.Hero.Health;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p,card)) && p.Hero.Health==health-3 && p.UsedMana==10,"healed hero switches to Health payment");
        End(g);End(g);var next=Add(p,id);p.UsedMana=10;
        Core2026Proof.Check(!g.Process(PlayCardTask.Any(p,next)),"old-turn heal does not permit Health payment");
        p.UsedMana=0;health=p.Hero.Health;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p,next)) && p.Hero.Health==health && p.UsedMana==3,"normal Mana payment restored next turn");
    }
    private static void NecroticMortician(Game g, Controller p, string id)
    {
        var undead=Summon(p,"RLK_503");Kill(g,undead);
        Play(g,p,id);Core2026Proof.Check(p.Choice!=null && p.Choice.Choices.Count==3,"recent Undead death enables Discover");
        Core2026Proof.Check(p.Choice.Choices.Select(e=>g.IdEntityDic[e].Card.Id).Distinct().Count()==3,"Discover offers three distinct definitions");
        var chosen=p.Choice.Choices.First();PlayerDecisionProof.Answer(g,chosen);
        Core2026Proof.Check(g.IdEntityDic[chosen].Card[GameTag.COST_UNHOLY]>0,"Unholy Rune card obtained");
    }
}
