using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
internal static class B16C16FoodChainProof
{
    [GroupProof("b16-c16-food-chain-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("TLC_830", Unpinned_ExactAttackSnapshot);
        Shokk();
        Unpinned_BattlecryDormantAndOpponent();
    }
    private static void Unpinned_BattlecryDormantAndOpponent()
    {
        var g=B15DrawReplacementProof.NewGame();var p=g.Player1;
        var q=(Spell)Play(g,p,"TLC_830");
        var raptor=(Minion)Play(g,p,"TLC_245");
        Core2026Proof.Check(q.QuestProgress==0,"quest waits while Battlecry decision is open");
        int pick=p.Choice.Choices.First(x=>g.IdEntityDic[x].Card.Text.Contains("+3 Attack"));
        PlayerDecisionProof.Answer(g,pick);
        Core2026Proof.Check(raptor.AttackDamage==5 && q.QuestProgress==0,"Battlecry Attack gain does not replace frozen two");
        StandardSetsProof.ParkBoard(p);
        var dormant=(Minion)Add(p,"TIME_046");
        Generic.AddEnchantmentBlock(g,Cards.FromId("TLC_254e"),dormant,dormant);
        Generic.AddEnchantmentBlock(g,Cards.FromId("TLC_254e"),dormant,dormant);
        PlayEntity(g,p,dormant);
        Core2026Proof.Check(dormant.IsDormant && q.QuestProgress==1,"handbuffed Dormant Beast counts before awakening");
        End(g);Play(g,p.Opponent,"CS2_172");
        Core2026Proof.Check(q.QuestProgress==1,"opponent Beast excluded");
        End(g);StandardSetsProof.ParkBoard(p);
        Play(g,p,"BE_734");
        Core2026Proof.Check(q.QuestProgress==2,"printed All satisfies Beast membership");
    }
    private static void Shokk()
    {
        foreach(int seed in new[]{1,7,19})
        foreach(int occupancy in new[]{0,9})
        {
            var g=B15DrawReplacementProof.NewGame(seed); var p=g.Player1;
            while(p.HandZone.Count<occupancy) Add(p,"CS2_231");
            var shokk=Add(p,"TLC_830t");
            var expectedGame=g.CloneExact();
            PlayEntity(g,p,shokk);
            foreach(int attack in new[]{8,6,4})
            {
                var filter=new OfflinePoolFilter(classMode:PoolClassMode.Any,type:CardType.MINION,race:Race.BEAST,exactAttack:attack);
                var candidates=OfflineCardPools.Candidates(g,p.HeroClass,filter,117673);
                Console.WriteLine("POOL Shokk attack="+attack+" count="+candidates.Count);
                var expected=OfflineCardPools.SampleDistinct(expectedGame,filter,p.HeroClass,117673);
                if(expected.Length==0) continue;
                var offers=p.Choice.Choices.ToArray();
                Core2026Proof.Check(offers.Select(x=>g.IdEntityDic[x].Card.Id).SequenceEqual(expected.Select(x=>x.Id)),"Shokk seeded ordered offers");
                Core2026Proof.Check(offers.Select(x=>g.IdEntityDic[x].Card.Id).Distinct().Count()==offers.Length,"distinct offers");
                foreach(var branch in new[]{g.CloneExact(),Game.LoadSnapshot(g.SaveSnapshot())})
                {
                    PlayerDecisionProof.Answer(branch,offers[0]);
                    Core2026Proof.Check(branch.IdEntityDic[offers[0]].Cost==2,"Shokk open-choice clone/load sets Cost");
                }
                PlayerDecisionProof.Answer(g,offers[0]);
                Core2026Proof.Check(g.IdEntityDic[offers[0]].Cost==2,"Shokk selected Cost two");
            }
            Core2026Proof.Check(p.Choice==null && p.HandZone.Count==Math.Min(10,occupancy+3),"Shokk three choices finish with ordinary capacity");
        }
    }
    private static void Unpinned_ExactAttackSnapshot(Game g, Controller p, string id)
    {
        var quest=(Spell)Play(g,p,id);
        Play(g,p,"CS2_172"); // three Attack Beast
        Play(g,p,"CS2_172");
        Core2026Proof.Check(quest.QuestProgress==1,"exact requirement cannot count twice");
        int mask=quest[PayloadRules.Value];
        B15QuestClosureProof.RewindState(g);
        p=g.Player1; quest=(Spell)g.IdEntityDic[quest.Id];
        Core2026Proof.Check(quest[PayloadRules.Value]==mask,"Rewind preserves exact Attack mask");
        foreach(var branch in new[]{g.CloneExact(),Game.LoadSnapshot(g.SaveSnapshot()),g})
        {
            var owner=branch.Player1;
            foreach(int attack in new[]{1,5,7})
            {
                StandardSetsProof.ParkBoard(owner);
                var beast=(Minion)Add(owner,"CS2_172"); beast[GameTag.ATK]=attack;
                owner.BaseMana=10; owner.UsedMana=0;
                Core2026Proof.Check(branch.Process(PlayCardTask.Any(owner,beast)),"play handbuffed Beast");
            }
            Core2026Proof.Check(((Spell)branch.IdEntityDic[quest.Id]).QuestProgress==4
                && owner.HandZone.Count(c=>c.Card.Id=="TLC_830t")==1,"frozen exact Attack requirements survive clone/load and grant Shokk");
        }
    }
}
