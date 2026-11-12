using System;
using System.Linq;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class B14K4EmeraldDreamProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("EDR_455", Unpinned_Succumb);
        DeathKnightC1Proof.Case("FIR_951", Unpinned_Volcoross);
    }
    // discover-pools/succumb-distinct-dragons.yaml (C/D; owner-approved assumption): distinct death definitions, fresh base summon.
    private static void Unpinned_Succumb(Game g, Controller p, string id)
    {
        Play(g,p,id); Core2026Proof.Check(p.Choice==null && p.BoardZone.IsEmpty,"empty death history skips choice");
        // Ensure the consumer is active before creating death facts.
        Kill(g,Summon(p,"CS2_182")); Kill(g,Summon(p.Opponent,"EX1_043"));
        Play(g,p,id); Core2026Proof.Check(p.Choice==null,"non-Dragon and enemy Dragon deaths excluded");
        var dragon=Summon(p,"EX1_043"); dragon.AttackDamage=20; dragon.Damage=1; Kill(g,dragon); Kill(g,Summon(p,"EX1_043"));
        Play(g,p,id);
        Core2026Proof.Check(p.Choice!=null && p.Choice.Choices.Count==1 && g.IdEntityDic[p.Choice.Choices[0]].Card.Id=="EX1_043","duplicate deaths yield one exact Dragon offer");
        foreach(var branch in new[]{g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot()),g})
        {
            var owner=branch.Player1; Core2026Proof.Check(B14K4WarriorDkProof.Answer(branch,owner.Choice.Choices[0]),"Dragon choice accepted after clone/snapshot");
            Core2026Proof.Check(branch.HistoryFacts.Query.Discoveries().Count()==1,"Succumb records exactly one completed Discover");
            var copy=owner.BoardZone.Single(); Core2026Proof.Check(copy.Card.Id=="EX1_043" && copy.AttackDamage==4 && copy.Health==1 && copy.Damage==0,"resurrection creates base Dragon, without former buffs or damage");
        }
        ParkBoard(p);
        foreach(var x in new[]{"EX1_572","EX1_562","EX1_561"}) Kill(g,Summon(p,x));
        foreach(bool reverse in new[]{false,true})
        {
            var branch=DeathKnightC1Proof.NewGame(g.Seed,CardClass.WARRIOR); var owner=branch.Player1;
            Play(branch,owner,id);
            var deaths=new[]{"EX1_043","EX1_043","EX1_572","EX1_562","EX1_561"}; if(reverse) Array.Reverse(deaths);
            foreach(string x in deaths) Kill(branch,Summon(owner,x));
            branch.Random.SetSeed(g.Seed);
            var definitions=deaths.Distinct().Select(Cards.FromId).ToArray();
            var expected=new EffectContext(branch.CloneExact(),owner.Id,owner.Hero.Id).SampleDistinct(definitions,3).Select(c=>c.Id).ToArray();
            Play(branch,owner,id);
            var offers=owner.Choice.Choices.Select(x=>branch.IdEntityDic[x].Card.Id).ToArray();
            Core2026Proof.Check(offers.SequenceEqual(expected) && offers.Distinct().Count()==3,"seeded distinct definition sampling under reordered histories rejects weighting duplicate deaths");
            DeathKnightC1Proof.FullBoard(owner); int chosen=owner.Choice.Choices[0]; Core2026Proof.Check(B14K4WarriorDkProof.Answer(branch,chosen),"full-board choice accepted");
            Core2026Proof.Check(owner.BoardZone.SlotCount==7,"full board burns resurrection");
        }
    }
    // corpses/corpse-options-unaffordable.yaml (C/D; approved assumption): omit unaffordable options, no prompt below ten.
    private static void Unpinned_Volcoross(Game g, Controller p, string id)
    {
        foreach(int corpses in new[]{0,9,10,19,20,29,30,35})
        {
            var branch=DeathKnightC1Proof.NewGame(g.Seed); var owner=branch.Player1; CorpseRules.Gain(owner,corpses);
            var body=(Minion)Play(branch,owner,id);
            Core2026Proof.Check(body.IsRush && body.HasTaunt,"Volcoross printed Rush and Taunt");
            var expected=new[]{10,20,30}.Where(x=>x<=corpses).ToArray();
            if(expected.Length==0) { Core2026Proof.Check(owner.Choice==null && body.AttackDamage==5 && body.Health==5 && CorpseRules.Available(owner)==corpses,"unfunded base play has no choice/payment"); continue; }
            Core2026Proof.Check(owner.Choice.Choices.Select(x=>branch.IdEntityDic[x].Card.Id).SequenceEqual(expected.Select(n=>"FIR_951t"+(n/10+1))),"only exact affordable options in order");
            foreach(int amount in expected)
            {
                foreach(var next in new[]{branch.CloneExact(history:true),Game.LoadSnapshot(branch.SaveSnapshot())})
                {
                    var chooser=next.Player1; int answer=chooser.Choice.Choices.Single(x=>next.IdEntityDic[x].Card.Id=="FIR_951t"+(amount/10+1));
                    Core2026Proof.Check(B14K4WarriorDkProof.Answer(next,answer),"funded option accepted after clone/snapshot");
                    var grown=(Minion)next.IdEntityDic[body.Id];
                    Core2026Proof.Check(grown.AttackDamage==5+amount && grown.Health==5+amount && CorpseRules.Available(chooser)==corpses-amount,"exact chosen stats/payment");
                    grown.Silence(); Core2026Proof.Check(grown.AttackDamage==5 && grown.Health==5,"stat grant silences");
                }
            }
        }
    }
}
