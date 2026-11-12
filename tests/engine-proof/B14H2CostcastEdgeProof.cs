using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B14H2CostcastEdgeProof
{
    private static void Check(bool value,string message)=>Core2026Proof.Check(value,message);
    private static Game Clean(bool history)
    {
        var g=HeroDivineShieldProof.NewGame(history);
        foreach(var p in new[]{g.Player1,g.Player2})foreach(var c in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(c));
        return g;
    }
    internal static void Run()
    {
        foreach(bool history in new[]{false,true})
        {
            Multipliers(history); SplitEntry(history); Overflow(history); Payment(history); Tokens(history); UmbraTickets(history); DistinctOffers(history);
        }
    }
    private static void Multipliers(bool history)
    {
        var g=Clean(history);var p=g.Player1;
        var first=Summon(p,"FP1_031");var second=Summon(p,"FP1_031");
        first.Silence();
        var victim=Summon(p,"EX1_029");
        foreach(var branch in new[]{g,g.CloneExact(history),Game.LoadSnapshot(g.SaveSnapshot())})
        {
            Kill(branch,(Minion)branch.IdEntityDic[victim.Id]);
            Check(branch.Player2.Hero.Damage==4,"one remaining Baron doubles across clone/snapshot");
        }
        second.Silence();Summon(p.Opponent,"FP1_031");
        Kill(g,Summon(p,"EX1_029"));Check(p.Opponent.Hero.Damage==6,"opponent aura does not double friendly deaths");
        p[GameTag.EXTRA_DEATHRATTLES_BASE]=1;p[GameTag.EXTRA_DEATHRATTLES_ADDITIONAL]=1;
        Kill(g,Summon(p,"EX1_029"));Check(p.Opponent.Hero.Damage==12,"additional repeats add to base multiplier");
        p[GameTag.EXTRA_DEATHRATTLES_ADDITIONAL]=0;
        var vanilla=Summon(p,"CS2_231");vanilla.Silence();
        Generic.AddEnchantmentBlock(g,Cards.FromId("EX1_158e"),p.Hero,vanilla);
        Kill(g,vanilla);Check(p.BoardZone.Count(m=>m.Card.Id=="EX1_158t")==2,"granted Deathrattle doubles after Silence");
    }
    private static void SplitEntry(bool history)
    {
        var g=Clean(history);var p=g.Player1;var held=(Minion)Add(p,"CS2_182");var spell=Add(p,"TIME_030");bool observed=false;
        TriggerManager.TriggerHandler watch=sender=>{
            if(sender is Minion copy && copy.Id!=held.Id && copy.Zone==p.HandZone)
            {
                observed=true;
                Check(held.Cost+copy.Cost==4 && held.AttackDamage+copy.AttackDamage==4 && held.BaseHealth+copy.BaseHealth==5,
                    "both halves initialized before second-half hand-entry reactions");
            }
        };
        g.TriggerManager.ZoneTrigger+=watch;
        try {PlayEntity(g,p,spell);} finally {g.TriggerManager.ZoneTrigger-=watch;}
        Check(observed,"second half uses normal hand admission");
        CostRules.RestorePrintedCost(held);
        Check(held.Cost==4 && p.HandZone.OfType<Minion>().Single(m=>m.Id!=held.Id).Cost==2,
            "printed-cost restoration removes only that half owned adjustment");
    }
    private static void Overflow(bool history)
    {
        var g=Clean(history);var p=g.Player1;
        var held=(Minion)Add(p,"CS2_182");held.BaseHealth=1;
        var payload=PayloadRules.StoreCard(held,81,Cards.FromId("CS2_231"));
        while(!p.HandZone.IsFull)Add(p,"GAME_005");
        var spell=(Spell)Entity.FromCard(p,Cards.FromId("TIME_030"),zone:p.SetasideZone);
        int discarded=p.DiscardedEntities.Count;
        Generic.CastSpell(p,g,spell,null,0);
        Check(held.Zone==p.HandZone && held.Cost==2 && held.BaseHealth==1 && p.HandZone.Count==10
            && p.DiscardedEntities.Count==discarded,"full-hand split keeps first half and burns overflow without discard");
        Check(PayloadRules.Entries(held,81).Single().Id==payload.Id,"original payload remains independent");
        foreach(var c in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(c));
        var empty=(Spell)Entity.FromCard(p,Cards.FromId("TIME_030"),zone:p.SetasideZone);
        var rng=g.Random.CaptureState();Generic.CastSpell(p,g,empty,null,0);
        Check(rng.Equals(g.Random.CaptureState()),"no hand minion consumes no RNG");
    }
    private static void Payment(bool history)
    {
        var g=Clean(history);var p=g.Player1;
        var held=Add(p,"EDR_846");held.Cost=2;
        var equal=Add(p,"CS2_182");equal.Cost=2;PlayEntity(g,p,equal);
        Check(!CostRules.QualifiedHeldDreams(held),"equal effective Cost does not qualify");
        var health=Add(p,"CS2_182");CostRules.AddPaymentOverride((Playable)health,CostPaymentKind.Health);
        p.UsedMana=0;Check(g.Process(PlayCardTask.Any(p,health)),"Health payment play accepted");
        Check(p.Hero.Damage==4 && p.UsedMana==0 && CostRules.QualifiedHeldDreams(held),"numeric Cost qualifies when Health pays");
        p.SetasideZone.Add(p.HandZone.Remove(held));p.HandZone.Add(p.SetasideZone.Remove(held));
        Check(!CostRules.QualifiedHeldDreams(held),"new hand residence resets qualification");
        End(g);p.Opponent.BaseMana=10;p.Opponent.UsedMana=0;Play(g,p.Opponent,"CS2_182");
        Check(!CostRules.QualifiedHeldDreams(held),"opponent play cannot qualify held card");
    }
    private static void Tokens(bool history)
    {
        var g=Clean(history);var p=g.Player1;
        var sister=Summon(p,"EDR_846t3");Check(p.Hero.CantBeTargetedBySpells && p.Hero.CantBeTargetedByHeroPowers,"corrupted Sister grants hero Elusive");
        sister.Silence();g.AuraUpdate();Check(!p.Hero.CantBeTargetedBySpells && !p.Hero.CantBeTargetedByHeroPowers,"Sister Silence removes hero aura");
        var target=Summon(p,"CS2_182");Play(g,p,"EDR_846t1",target);
        Check(target.IsImmune && target.AttackDamage==9 && target.BaseHealth==10,"Nightmare uses separate permanent stats and temporary Immune");
        End(g);Check(!target.IsImmune && target.AttackDamage==9 && target.BaseHealth==10,"Immune expires, stats remain");End(g);
        target.Silence();Check(target.AttackDamage==4 && target.BaseHealth==5,"Nightmare stats are removable buffs");
        foreach(string id in new[]{"EX1_572","VAN_EX1_572","CORE_VAN_EX1_572","CS3_033","LEG_CS3_033","DRG_320","EDR_000","Story_07_Ysera"})
        {
            StandardSetsProof.ParkBoard(p);StandardSetsProof.ParkBoard(p.Opponent);
            p.Hero.Damage=0;p.Opponent.Hero.Damage=0;
            var own=Summon(p,id);var enemy=Summon(p.Opponent,id);own.Silence();enemy.HasDivineShield=true;enemy.IsImmune=true;
            Play(g,p,"DREAM_02");Check(own.Damage==0 && enemy.Damage==0,"normal Awakens exempts exact Ysera identities: "+id);
            Play(g,p,"EDR_846t4");Check(own.Zone==p.GraveyardZone && enemy.Zone==p.Opponent.GraveyardZone,"corrupted Awakening destroys both Yseras through Silence/Shield/Immune: "+id);
        }
    }
    private static void DistinctOffers(bool history)
    {
        for(int seed=1;seed<=8;seed++)
        {
            var g=Clean(history);var p=g.Player1;
            foreach(string id in new[]{"CS2_182","CS2_182","CS2_182","CS2_182","CS2_231","CS2_120","CS2_200"})Add(p.Opponent,id);
            g.SetRandomSeed(seed);var oracle=g.CloneExact(history);
            var groups=p.Opponent.HandZone.GroupBy(c=>c.Card.Id).Select(a=>a.ToArray()).ToList();var expected=new List<string>();
            for(int i=0;i<3;i++){int j=oracle.Random.Next(groups.Count);var group=groups[j];groups.RemoveAt(j);expected.Add(group[oracle.Random.Next(group.Length)].Card.Id);}
            Play(g,p,"TIME_039");
            Check(p.Choice.Choices.Select(id=>g.IdEntityDic[id].Card.Id).SequenceEqual(expected),"seeded distinct-definition sampling ignores copy multiplicity");
        }
    }
    private static void UmbraTickets(bool history)
    {
        var g=Clean(history);var p=g.Player1;p.Opponent.Hero.BaseHealth=100;
        foreach(string id in new[]{"EX1_029","EX1_096","EX1_012","EX1_556","FP1_007","EX1_534"})
        {
            Kill(g,Summon(p,id));StandardSetsProof.ParkBoard(p);
        }
        var tickets=g.HistoryFacts.Query.Deaths().Where(f=>f.ControllerId==p.Id && f.Definition.ReadDefinition()[GameTag.DEATHRATTLE]>0).ToList();
        var oracle=g.CloneExact(history);for(int i=0;i<5;i++){int j=i+oracle.Random.Next(tickets.Count-i);(tickets[i],tickets[j])=(tickets[j],tickets[i]);}
        var source=Play(g,p,"TLC_106");
        var selected=g.IdEntityDic.Values.OfType<Minion>().Where(m=>m[GameTag.DISPLAYED_CREATOR]==source.Id && m.Zone==p.SetasideZone).OrderBy(m=>m.Id).Select(m=>m.Card.Id);
        Check(selected.SequenceEqual(tickets.Take(5).Select(t=>t.Definition.ReadDefinition().Id)),"seeded death tickets selected before any activation, without replacement");
    }
}
