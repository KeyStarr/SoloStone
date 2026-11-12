using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static DiscoverRealProof;
internal static class B14K6JailProof
{
    internal static void Run() { StandardSetsProof.Case("JAIL_515",Unpinned_ShadowRoundsChainTermination); StandardSetsProof.Case("JAIL_474",JadeGuardians); }
    private static void JadeGuardians(Game original,Controller ignored,string id)
    {
        foreach(int count in new[]{0,2,9})
        {
            var g=B14K6CataclysmProof.NewRogue(original.Seed);var p=g.Player1;
            for(int i=0;i<count;i++)Play(g,p,"CS2_025");
            var held=Add(p,id);g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1]==count,"visible discount counts own actual 2-Mana plays before this card entered hand");
            var candidates=B14K6CataclysmProof.Pool(g,id);var expected=g.CloneExact(history:true);
            var wanted=Enumerable.Range(0,2).Select(_=>candidates[expected.Random.Next(candidates.Length)].Id).ToArray();
            Saved(g,next=>{
                var owner=next.Player1;PlayEntity(next,owner,next.IdEntityDic[held.Id]);
                Core2026Proof.Check(owner.HandZone.Select(c=>c.Card.Id).SequenceEqual(wanted),"two seeded independent picks, not first two candidates");
                var results=owner.HandZone.ToArray();
                foreach(var result in results)
                {
                    Core2026Proof.Check(result is Minion minion && minion.AttackDamage==result.Card.ATK && minion.Health==result.Card.Health
                        && result.Card.Cost==8 && result.Cost==Math.Max(0,8-count),"8-Cost minion, exact generated stats and permanent visible discount");
                    CostSliceProof.Visible(result,Math.Max(0,8-count));
                }
                End(next);End(next);
                Core2026Proof.Check(results.All(c=>c.Cost==Math.Max(0,8-count)),"discount persists beyond turn");
            });
        }
        var negative=B14K6CataclysmProof.NewRogue(original.Seed);var me=negative.Player1;
        var cheap=Add(me,"CS2_046");cheap.Cost=1;PlayEntity(negative,me,cheap);
        var health=Add(me,"CS2_025");CostRules.AddPaymentOverride((Playable)health,SabberStoneCore.Model.Costs.CostPaymentKind.Health);
        me.BaseMana=10;me.UsedMana=0;Core2026Proof.Check(negative.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(me,health)),"2-Health play accepted");
        Generic.CastSpell(me,negative,(Spell)Entity.FromCard(me,Cards.FromId("CS2_025")),null,0);
        End(negative);Play(negative,me.Opponent,"CS2_025");End(negative);
        foreach(var old in me.HandZone.ToArray())me.SetasideZone.Add(me.HandZone.Remove(old));
        var source=Add(me,id);source.Cost=2;PlayEntity(negative,me,source);
        Core2026Proof.Check(me.HandZone.Count==2 && me.HandZone.All(c=>c.Card.Cost==8 && c.Cost==7),"exclude opponent, effects, Health and non-two payments; include Guardians own two-Mana play");
        var empty=B14K6CataclysmProof.NewRogue(original.Seed);
        LostCityPoolProof.WithPool(empty,id,Array.Empty<string>(),()=>{
            Play(empty,empty.Player1,id);Core2026Proof.Check(empty.Player1.HandZone.IsEmpty,"empty pool generates neither minion");
        });
        var duplicate=B14K6CataclysmProof.NewRogue(original.Seed);
        LostCityPoolProof.WithPool(duplicate,id,new[]{"CORE_EX1_383"},()=>{
            Play(duplicate,duplicate.Player1,id);
            Core2026Proof.Check(duplicate.Player1.HandZone.Count==2 && duplicate.Player1.HandZone.All(c=>c.Card.Id=="CORE_EX1_383" && c.Cost==8),"independent picks allow duplicate definitions");
        });
        for(int occupied=8;occupied<=10;occupied++)
        {
            var g=B14K6CataclysmProof.NewRogue(original.Seed);var p=g.Player1;
            while(p.HandZone.Count<occupied)Add(p,"CS2_182");
            var spell=(Spell)Entity.FromCard(p,Cards.FromId(id));Generic.CastSpell(p,g,spell,null,0);
            Core2026Proof.Check(p.HandZone.Count==10 && p.HandZone.Count(c=>c.Card.Cost==8)==Math.Min(2,10-occupied),"two random grants respect hand capacity");
        }
    }
    // repeat-effects/shadow-rounds-{cast-history,chain-termination,damage}.yaml STILL-UNPINNED.
    // Sources: tier C wiki.gg/Shadow_Rounds and Defile; tier D pinned export; tier E reveal/loop reports.
    // Owner approved fallback: 50 total damage applications, including initial hit; final deaths still settle.
    private static void Unpinned_ShadowRoundsChainTermination(Game original,Controller ignored,string id)
    {
        foreach(bool portalFirst in new[]{false,true})
        {
            var g=New(original.Seed);var p=g.Player1;
            var portal=portalFirst?Summon(p.Opponent,"UNG_829t2"):null;
            var first=Summon(p.Opponent,"CS2_231");var second=Summon(p.Opponent,"CS2_231");
            portal??=Summon(p.Opponent,"UNG_829t2");
            Core2026Proof.Check(portal.Untouchable && !portal.IsDormant,"Nether Portal is untouchable without Dormant");
            Play(g,p,id,first);
            Core2026Proof.Check(first.Zone==p.Opponent.GraveyardZone && second.Zone==p.Opponent.GraveyardZone
                && p.Opponent.BoardZone.Single().Id==portal.Id && portal.Damage==0,
                "untouchable Portal excluded from random continuation on reordered board; both Wisps die");
        }
        foreach(bool shield in new[]{false,true})
        {
            var g=New(original.Seed);var p=g.Player1;var first=Summon(p.Opponent,"CS2_231");first.HasDivineShield=shield;
            var other=Summon(p.Opponent,"CS2_182");other.BaseHealth=1;
            Play(g,p,id,first);
            Core2026Proof.Check(shield ? first.Zone==p.Opponent.BoardZone && !first.HasDivineShield && other.Zone==p.Opponent.BoardZone : p.Opponent.BoardZone.IsEmpty,"surviving shield stops; deaths continue");
            Core2026Proof.Check(g.HistoryFacts.Query.Plays().Count(f=>f.DbfId==Cards.FromId(id).AssetId)==1,"one player spell play for entire chain");
        }
        var loop=New(original.Seed);var owner=loop.Player1;
        var blood=Summon(owner.Opponent,"TLC_234");
        Saved(loop,next=>{
            Spell source=null;
            for(int cast=1;cast<=2;cast++)
            {
                var victim=next.Player2.BoardZone.Single();
                if(cast==1)source=(Spell)Play(next,next.Player1,id,victim);
                else { Generic.CastSpell(next.Player1,next,source,victim,0);next.DeathProcessingAndAuraUpdate(); }
                Core2026Proof.Check(next.Player2.GraveyardZone.Count(c=>c.Card.Id=="TLC_234"||c.Card.Id=="TLC_234t")==50*cast
                    && next.Player2.BoardZone.Count==1 && next.DeadMinions.Count==0,"50-hit cap settles last reciprocal Deathrattle; fresh budget next cast");
                Core2026Proof.Check(!PayloadRules.Entries(source).Any() && next.HistoryFacts.Query.Plays().Count(f=>f.DbfId==source.Card.AssetId)==1,
                    "saved invocation budget retired; effect recast adds no player play");
            }
        });
        var sample=New(original.Seed);var p1=sample.Player1;var target=Summon(p1.Opponent,"CS2_231");
        var candidates=new[]{Summon(p1.Opponent,"CS2_182"),Summon(p1.Opponent,"CS2_200"),Summon(p1.Opponent,"CS2_172")};
        foreach(var m in candidates)m.BaseHealth=20;
        if(original.Seed%2==0){foreach(var m in candidates)p1.Opponent.BoardZone.Remove(m);foreach(var m in candidates.Reverse())p1.Opponent.BoardZone.Add(m);}
        int[] order=p1.Opponent.BoardZone.Where(m=>m.Id!=target.Id).Select(m=>m.Id).ToArray();var expected=sample.CloneExact(history:true);
        int wanted=order[expected.Random.Next(order.Length)];Play(sample,p1,id,target);
        Core2026Proof.Check(candidates.Single(m=>m.Damage>0).Id==wanted,"seeded random next victim after reordered board, not always first");
        var bonus=New(original.Seed);var spellPower=Summon(bonus.Player1,"CS2_052");var tough=Summon(bonus.Player2,"CS2_182");tough.BaseHealth=3;
        Play(bonus,bonus.Player1,id,tough);Core2026Proof.Check(tough.Zone==bonus.Player2.GraveyardZone,"current Spell Damage included");
        var immune=New(original.Seed);var protectedVictim=Summon(immune.Player2,"CS2_182");protectedVictim.IsImmune=true;
        var initial=Summon(immune.Player2,"CS2_231");Play(immune,immune.Player1,id,initial);
        Core2026Proof.Check(protectedVictim.Damage==0 && immune.Player2.BoardZone.Single().Id==protectedVictim.Id,"Immune survivor stops continuation");
        var stealth=New(original.Seed);var visible=Summon(stealth.Player2,"CS2_231");var hidden=Summon(stealth.Player2,"CS2_182");
        hidden.HasStealth=true;hidden.CantBeTargetedBySpells=true;hidden.BaseHealth=1;
        Play(stealth,stealth.Player1,id,visible);Core2026Proof.Check(stealth.Player2.BoardZone.IsEmpty,"automatic random continuation includes Stealth and Elusive");
        var powerLoss=New(original.Seed);var powerOwner=powerLoss.Player1;
        var aura=Summon(powerOwner,"CS2_052");aura.BaseHealth=1;
        var bomb=Summon(powerOwner.Opponent,"EX1_097");bomb.BaseHealth=3;
        var survivor=Summon(powerOwner.Opponent,"CS2_182");survivor.BaseHealth=10;
        Play(powerLoss,powerOwner,id,bomb);
        Core2026Proof.Check(aura.Zone==powerOwner.GraveyardZone && survivor.Damage==4,"Deathrattle kills Spell Damage source; next hit recalculates base two");
    }
}
