using System.Linq;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static B14H3LifecycleProof;
using static StandardSetsProof;


internal static class B14H3DeathwingProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_190h",Pinned_DeathwingCataclysmCountsOneTwoFour);
    private static void Pinned_DeathwingCataclysmCountsOneTwoFour(Game g,Controller p,string id)
    {
        foreach (int heralds in new[] {0,1,2,3,4,5})
        {
            ParkBoard(p); ParkBoard(p.Opponent); PoolPilotProof.EmptyHand(p);
            p[HeraldRules.Progress] = heralds;
            var replaced = Summon(p.Opponent,"EX1_556"); // Harvest Golem dies to Raze before next choice.
            var hero = Add(p,id); int armor = p.Hero.Armor; var cls = p.HeroClass;
            PlayEntity(g,p,hero);
            B14H3LifecycleProof.Check(p.Hero == hero && p.HeroClass == cls && p.Hero.Armor == armor+12 && p.Choice != null,
                "hero replacement keeps class, grants Armor, suspends Battlecry");
            int count = 1 << Math.Min(2,heralds/2);
            foreach (var next in new[] {g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot()),g})
            {
                int prompts = 0;
                foreach (string option in new[] {"CATA_190t12","CATA_190t11","CATA_190t10","CATA_190t13"}.Take(count))
                {
                    B14H3LifecycleProof.Check(next.Player1.Choice.Choices.Count == 4-prompts,"distinct Cataclysm offers");
                    Answer(next,option); prompts++;
                    if (option == "CATA_190t12")
                        B14H3LifecycleProof.Check(next.Player2.BoardZone.Single().Card.Id == "skele21","Raze Deathrattle settles before next choice");
                }
                B14H3LifecycleProof.Check(next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect && prompts == count,"pinned 1/2/4 tier budget");
                B14H3LifecycleProof.Check(next.Player1.NumOptionsPlayedThisTurn > 0,"hero play completion runs after final choice");
                if (count >= 2) B14H3LifecycleProof.Check(next.Player2.BoardZone.IsEmpty,"Topple hits Raze replacement");
                if (count == 4)
                {
                    B14H3LifecycleProof.Check(next.Player1.BoardZone.Single().Card.Id == "CATA_190t14","exact 12/12 Dragon token");
                    B14H3LifecycleProof.Check(next.Player1.DeckZone.Count(x => x.Cost == 1 && x.Card.Rarity == Rarity.LEGENDARY && x.Card.IsRace(Race.DRAGON)) >= 5,
                        "five seeded Legendary Dragons shuffled at one Cost");
                }
            }
        }
        Unpinned_DeathwingDistinctCataclysmsSettleBeforeNextChoice(g,p,id);
    }
    private static void Unpinned_DeathwingDistinctCataclysmsSettleBeforeNextChoice(Game g,Controller p,string id)
    {
        ParkBoard(p); ParkBoard(p.Opponent); p[HeraldRules.Progress] = 4; p.ExtraBattlecry = true;
        while (!p.BoardZone.IsFull) Summon(p,"CS2_182");
        Play(g,p,id);
        for (int invocation=0;invocation<2;invocation++)
        foreach (string option in new[] {"CATA_190t10","CATA_190t11","CATA_190t12","CATA_190t13"})
        {
            B14H3LifecycleProof.Check(p.Choice != null && p.Choice.Choices.Any(x => g.IdEntityDic[x].Card.Id == option),"ineffective options offered; repeat invocation restores all choices");
            Answer(g,option);
        }
        p.ExtraBattlecry = false;
        B14H3LifecycleProof.Check(p.Choice == null && p.BoardZone.Count == 7,"full-board and no-target choices consume budget");
        int attack = p.Hero.AttackDamage;
        p.Hero.HeroPower.ActivateTask(); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        B14H3LifecycleProof.Check(p.Hero.AttackDamage == attack+5,"Ruthless Hero Power grants five Attack");
        End(g); B14H3LifecycleProof.Check(p.Hero.AttackDamage == 0,"Ruthless expires this turn");
        End(g); End(g);
        p.Opponent[HeraldRules.Progress] = 2; Play(g,p.Opponent,id); Answer(g,"CATA_190t11"); Answer(g,"CATA_190t10");
        B14H3LifecycleProof.Check(p.Opponent.HeroClass == CardClass.MAGE && p.Opponent.BoardZone.Single().Card.Id == "CATA_190t14","opponent neutral hero retains class");
    }
}
