using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Actions;
using static Core2026Proof;

internal static class B14F3MimironProof
{
    [GroupProof("b14-f3-mimiron-cards", ProofKind.Cards)]
    internal static void Run() => StandardSetsProof.Case("BE_588", Unpinned_MimironPerfectTech);
    private static void Unpinned_MimironPerfectTech(Game g, Controller p, string id)
    {
        Play(g,p,id); Core2026Proof.Check(p.Choice==null,"no friendly Mech means no choice");
        Summon(p,"GVG_058"); p.Opponent.Hero.Armor=10;
        Play(g,p,id);
        var offers=p.Choice.Choices.Select(i=>g.IdEntityDic[i]).ToArray();
        Core2026Proof.Check(offers.Length==3 && offers.Select(e=>e.Card.Id).Distinct().Count()==3
            && offers.Any(e=>e.Card.Id=="DRG_069"),"matching Armor tech included before uniform fill");
        Core2026Proof.Check(g.Process(SabberStoneCore.Tasks.PlayerTasks.ChooseTask.Pick(p,offers.Single(e=>e.Card.Id=="DRG_069").Id)),"tech chosen");
        PlayEntity(g,p,p.HandZone.Single()); Core2026Proof.Check(p.Opponent.Hero.Armor==0,"generated Platebreaker works");
        var outcomes=new System.Collections.Generic.HashSet<string>();
        for(int seed=1;seed<=12;seed++)
        {
            string[] expected=null;
            foreach(bool reverse in new[]{false,true})
            {
                var game=PastLegacyQualificationProof.New(seed);var player=game.Player1;
                Summon(player,"GVG_058");player.Opponent.Hero.Armor=10;
                player.Opponent.Hero.AddWeapon((Weapon)Entity.FromCard(player.Opponent,Cards.FromId("CS2_106")));
                Entity.FromCard(player.Opponent,Cards.FromId("EX1_287"),zone:player.Opponent.SecretZone);
                var enemies=new[]{"CS2_179","CS2_201","EX1_110"};
                foreach(var enemy in reverse ? enemies.Reverse() : enemies) Summon(player.Opponent,enemy);
                Play(game,player,id);
                var sampled=player.Choice.Choices.Select(i=>game.IdEntityDic[i].Card.Id).ToArray();
                Core2026Proof.Check(sampled.Distinct().Count()==3,"tech offers are distinct");
                if(expected!=null)Core2026Proof.Check(sampled.SequenceEqual(expected),"context uses conditions, not board/history order");
                expected=sampled;outcomes.Add(string.Join(",",sampled));
            }
        }
        Core2026Proof.Check(outcomes.Count>1,"seeded matching-tech selection is not always first");
    }
}
