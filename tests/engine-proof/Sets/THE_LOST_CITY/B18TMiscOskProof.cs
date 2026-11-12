using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
internal static class B18TMiscOskProof
{
    [GroupProof("b18-t-misc-osk-cards",ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("TLC_452",(g,p,id)=>Unpinned_Rerolls());
        StandardSetsProof.Case("YOG_514",(g,p,id)=>{
            var candidates = OfflineCardPools.Candidates(g, p.HeroClass, ChaoticTendril.Pool.WithCost(1), 101021);
            var random = g.Random.Clone(); var expected = candidates[random.Next(candidates.Count)];
            Play(g,p,id);
            var cast = g.HistoryFacts.Query.Casts().First(f => f.Outcome == HistoryCastOutcome.Started);
            Core2026Proof.Check(cast.Definition.ReadDefinition().Id == expected.Id && expected.Cost == 1
                && p[(GameTag)33903] == 1 && p.Choice == null, "Tendril casts the seeded one-Cost spell and advances its tier, even if that spell kills it");
        });
        Unpinned_AllVariants(); Unpinned_CopyAndBanish(); PoolsAndExpiry(); Capacity(); Unpinned_Boundaries();
    }
    private static void Capacity()
    {
        foreach(int count in new[]{0,1}) {
            var g=DiscoverRealProof.New();var p=g.Player1;
            var target=count==0?null:Summon(p.Opponent,"CS2_200");
            Play(g,p,"TLC_452t21",target);
            while(p.Choice!=null) DiscoverRealProof.Answer(g,p.Choice.Choices[0]);
            Assert(p.Opponent.BoardZone.IsEmpty,"banish shortfall completes without a duplicate target");
        }
        var full=DiscoverRealProof.New();var owner=full.Player1;
        for(int i=0;i<6;i++) Summon(owner,"CS2_231");var enemy=Summon(owner.Opponent,"CS2_200");
        Play(full,owner,"TLC_452t19",enemy);Assert(owner.BoardZone.Count==7 && !owner.BoardZone.Any(m=>m.Card.Id=="CS2_200"),"full board skips copy");
    }
    private static void PoolsAndExpiry()
    {
        var g=DiscoverRealProof.New();var p=g.Player1;
        Console.WriteLine("Osk pools deathrattle/six/secrets="+string.Join("/",new[]{TitanographerOsk.Deathrattles,TitanographerOsk.Six,TitanographerOsk.Secrets}.Select(f=>OfflineCardPools.Candidates(g,CardClass.MAGE,f,TitanographerOsk.DbfId).Count)));
        Console.WriteLine("Tendril exact-cost buckets 1..10="+string.Join(",",Enumerable.Range(1,10).Select(n=>OfflineCardPools.Candidates(g,CardClass.MAGE,OfflinePoolFilter.Random(type:CardType.SPELL).WithCost(n),101021).Count)));
        var frost=Play(g,p,"TLC_452t2");var spell=Add(p,"CS2_029");g.AuraUpdate();
        Assert(spell.Cost==1,"next spell costs three less");
        DiscoverRealProof.Saved(g,next=>{
            var owner=next.Player1;
            PlayEntity(next,owner,next.IdEntityDic[spell.Id],owner.Opponent.Hero);
            Assert(owner.Opponent.Hero.Health==21,"Frost applies Spell Damage three to the next spell");
            var second=Add(owner,"CS2_029");Assert(second.Cost==4,"Frost consumed");
            PlayEntity(next,owner,second,owner.Opponent.Hero);Assert(owner.Opponent.Hero.Health==15,"later spell gets no bonus");
        });
        var tax=DiscoverRealProof.New();var a=tax.Player1;var taxed=Add(a.Opponent,"CS2_200");
        Play(tax,a,"TLC_452t15");Assert(taxed.Cost==7,"tax affects opponent cards");
        DiscoverRealProof.Saved(tax,next=>{
            End(next);Assert(next.IdEntityDic[taxed.Id].Cost==7,"tax persists through opponent turn");
            End(next);Assert(next.IdEntityDic[taxed.Id].Cost==6,"tax expires after opponent turn");
        });
    }
    private static void Assert(bool value,string text)=>Core2026Proof.Check(value,text);
    private static void Unpinned_Boundaries()
    {
        foreach(string id in new[] { "TLC_452t3","TLC_452t6","TLC_452t20","TLC_452t28" })
        foreach(int allies in new[] { 5,6 }) {
            var g=DiscoverRealProof.New();var p=g.Player1;
            for(int i=0;i<allies;i++) Summon(p,"CS2_231");
            var card=Add(p,id);p.BaseMana=10;p.UsedMana=0;
            DiscoverRealProof.Saved(g,next=>{
                PlayEntity(next,next.Player1,next.IdEntityDic[card.Id]);
                Assert(next.Player1.BoardZone.Count==7,id+" fills at most remaining slot without displacing existing minions");
                Assert(next.Player1.BoardZone.Count(m=>m.Card.Id=="CS2_231")==allies,id+" preserves existing full-board occupants");
            });
        }
        {
            var g=DiscoverRealProof.New();var p=g.Player1;
            PoolPilotProof.FillHand(p,8);var deck=DiscoverRealProof.Deck(p,"CS2_200","CS2_182");
            Play(g,p,"TLC_452t18");
            Assert(p.HandZone.Count==10 && p.DeckZone.IsEmpty && deck.All(c=>c.Zone==p.HandZone && c.Cost==2 && ((Minion)c).AttackDamage==2),"t18 fills two available hand slots with modified real minions");
            var full=DiscoverRealProof.New();var owner=full.Player1;PoolPilotProof.FillHand(owner,9);
            var burn=DiscoverRealProof.Deck(owner,"CS2_200","CS2_182");Play(full,owner,"TLC_452t18");
            Assert(owner.HandZone.Count==10 && owner.DeckZone.IsEmpty && burn.Count(c=>c.Zone==owner.GraveyardZone)==1 &&
                burn.Single(c=>c.Zone==owner.HandZone).Cost==2,"t18 full hand burns second drawn minion without overflow");
        }
        {
            var g=DiscoverRealProof.New();var p=g.Player1;var deck=DiscoverRealProof.Deck(p,"CS2_029","CS2_182");
            Play(g,p,"TLC_452t32");Assert(p.HandZone.IsEmpty && p.DeckZone.Count==2,"t32 no weapon never substitutes another draw");
            var victim=Summon(p.Opponent,"CS2_200");victim.Damage=2;
            var osk=(Minion)Play(g,p,"TLC_452t1",victim);
            Assert(osk.BaseHealth==11 && p.Hero.BaseHealth==35,"t1 gains damaged target's current five Health");
            osk.Silence();Assert(osk.BaseHealth==6 && p.Hero.BaseHealth==35,"t1 proper self buff silences independently of hero grant");
        }
        {
            var g=DiscoverRealProof.New();var p=g.Player1;
            var osk=Play(g,p,"TLC_452t13",p.Opponent.Hero);
            Assert(p.Opponent.Hero.Health==25,"t13 also deals five to hero targets");
            var titan=Summon(p.Opponent,"CS2_200");titan[(GameTag)2772]=1;
            var copy=Add(p,"TLC_452t19");
            Assert(!copy.IsValidPlayTarget(titan),"t19 cannot select a Titan");
        }
        {
            var g=GiftedDiscoverProof.New(7,CardClass.MAGE);var p=g.Player1;
            for(int i=0;i<5;i++) {
                Play(g,p,"TLC_452t14");
                Assert(p.SecretZone.Count==i+1 && p.SecretZone.Select(s=>s.Card.Id).Distinct().Count()==i+1,"t14 excludes already active Secrets");
            }
            Kill(g,p.BoardZone.First());var rng=g.Random.CaptureState();Play(g,p,"TLC_452t14");
            Assert(p.SecretZone.Count==5 && g.Random.CaptureState().Equals(rng),"t14 at Secret cap consumes no pick or duplicate");
        }
    }
    private static void Unpinned_Rerolls()
    {
        for(int seed=1;seed<=3;seed++) {
            var g=DiscoverRealProof.New(seed);var p=g.Player1;
            var rng=g.Random.Clone();var expected=TitanographerOsk.Variants[rng.Next(31)];
            var card=Add(p,"TLC_452");Assert(card.Card.Id==expected,"seeded immediate ability");
            foreach(var effect in Effects.AttackHealth_N(2)) effect.ApplyTo(card);CostRules.AddModifier((Playable)card,EffectOperator.SUB,2);
            var copy=Generic.Copy(p,card,card,Zone.HAND);Assert(copy.Card.Id==expected,"initialized hand copy preserves ability");
            DiscoverRealProof.Saved(g,next=>{
                End(next);Assert(next.IdEntityDic[card.Id].Card.Id==expected,"enemy turn does not reroll");
                var future=next.Random.Clone();var first=TitanographerOsk.Variants[future.Next(31)];var second=TitanographerOsk.Variants[future.Next(31)];
                End(next);
                Assert(next.IdEntityDic[card.Id].Card.Id==first && next.IdEntityDic[copy.Id].Card.Id==second,"owner turn rolls in hand order");
                Assert(next.IdEntityDic[card.Id].Cost==4 && ((Minion)next.IdEntityDic[card.Id]).AttackDamage==8,"reroll retains cost/stat buffs");
            });
        }
    }
    private static void Unpinned_AllVariants()
    {
        foreach(int seed in new[] { 1,7 })
        foreach(string id in TitanographerOsk.Variants) {
            var g=GiftedDiscoverProof.New(seed,CardClass.MAGE);var p=g.Player1;
            var held=Add(p,"CS2_200");var spell=Add(p,"CS2_029");var enemyHeld=Add(p.Opponent,"CS2_182");
            var ally=Summon(p,"CS2_182");var victim=Summon(p.Opponent,"CS2_200");var other=Summon(p.Opponent,"CS2_182");
            p.Hero.Damage=12;
            if(id=="TLC_452t27") ally.Damage=3;
            var deck=DiscoverRealProof.Deck(p,id=="TLC_452t7"?Enumerable.Repeat("CS2_182",12).ToArray():
                new[] { "CS2_029","CS2_106","CS2_200","CS2_182" });
            var card=Add(p,id);p.BaseMana=10;p.UsedMana=0;
            DiscoverRealProof.Saved(g,next=>{
                var owner=next.Player1;var osk=(Minion)next.IdEntityDic[card.Id];
                var a=(Minion)next.IdEntityDic[ally.Id];var v=(Minion)next.IdEntityDic[victim.Id];var b=(Minion)next.IdEntityDic[other.Id];
                var rng=next.Random.Clone();
                var legal=osk.ValidPlayTargets.Where(osk.IsValidPlayTarget).ToArray();
                Assert(next.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(owner,osk,
                    legal.Length>0?legal.FirstOrDefault(t=>t.Id==v.Id)??legal[0]:null)),"ability play: "+id);
                if(id=="TLC_452t4") {
                    var offers=DiscoverRealProof.Offers(next);
                    var pool=OfflineCardPools.Candidates(next,CardClass.MAGE,TitanographerOsk.Deathrattles,TitanographerOsk.DbfId);
                    Assert(offers.Length==3 && offers.Select(o=>o.Card.Id).Distinct().Count()==3 &&
                        offers.All(o=>pool.Any(c=>c.Id==o.Card.Id)),"t4 distinct eligible Deathrattle offers");
                    var chosen=offers[0];int cost=chosen.Cost;
                    DiscoverRealProof.Answer(next,chosen.Id);
                    Assert(chosen.Zone==owner.HandZone && chosen.Cost==Math.Max(0,cost-3),"t4 delivers chosen minion with discount");
                }
                if(id=="TLC_452t21") DiscoverRealProof.Answer(next,b.Id);
                Assert(owner.Choice==null,"ability completes: "+id);
                switch(id) {
                    case "TLC_452t1":
                        Assert(v.Zone==owner.Opponent.GraveyardZone && osk.BaseHealth==13 && owner.Hero.BaseHealth==37 && owner.Hero.Health==25,"t1 destroys and grants current Health to both recipients");break;
                    case "TLC_452t2": Assert(next.IdEntityDic[spell.Id].Cost==1 && next.IdEntityDic[held.Id].Cost==6,"t2 discounts spells only");break;
                    case "TLC_452t3":
                        var undead=owner.BoardZone.Where(m=>m.Card.Id=="TTN_737t2").ToArray();
                        Assert(undead.Length==2 && undead.All(m=>m.AttackDamage==3 && m.Health==3 && m.HasTaunt && m[GameTag.REBORN]==1),"t3 two 3/3 Taunt Reborn Undead");break;
                    case "TLC_452t4": break; // Chosen result checked above, before the offer leaves scope.
                    case "TLC_452t5": Assert(next.IdEntityDic[held.Id].Cost==4 && next.IdEntityDic[spell.Id].Cost==4 && next.IdEntityDic[enemyHeld.Id].Cost==4,"t5 reduces friendly held minions only");break;
                    case "TLC_452t6":
                        var elementals=owner.BoardZone.Where(m=>m.Card.Id=="TTN_862t4").ToArray();
                        Assert(elementals.Length==4 && elementals.All(m=>m.AttackDamage==2 && m.Health==2 && m.HasTaunt),"t6 four 2/2 Taunt Elementals");break;
                    case "TLC_452t7": Assert(owner.HandZone.Count==10 && owner.DeckZone.Count==4 && deck.TakeLast(8).All(c=>next.IdEntityDic[c.Id].Zone==owner.HandZone),"t7 draws physical cards until full");break;
                    case "TLC_452t8": Assert(owner.Hero.Health==30 && owner.Opponent.Hero.Health==30,"t8 fully heals own hero only");break;
                    case "TLC_452t9": Assert(owner.UsedMana==0 && owner.RemainingMana==10,"t9 refreshes spent crystals after paying six");break;
                    case "TLC_452t13": Assert(v.Health==2 && b.Health==5 && owner.Opponent.Hero.Health==30,"t13 exactly five damage to chosen target only");break;
                    case "TLC_452t14":
                        var secrets=OfflineCardPools.Candidates(next,CardClass.MAGE,TitanographerOsk.Secrets,TitanographerOsk.DbfId);
                        Assert(owner.SecretZone.Count==1 && owner.SecretZone.Single().Card.Id==secrets[rng.Next(secrets.Count)].Id && owner.UsedMana==6,"t14 seeded Mage Secret cast without extra payment");break;
                    case "TLC_452t15": Assert(next.IdEntityDic[enemyHeld.Id].Cost==5 && next.IdEntityDic[held.Id].Cost==6,"t15 taxes enemy hand only");break;
                    case "TLC_452t16": Assert(v.AttackDamage==2 && v.Health==2 && b.AttackDamage==2 && b.Health==2 && a.AttackDamage==4 && osk.AttackDamage==6,"t16 sets all enemy bodies only");break;
                    case "TLC_452t17": Assert(a.AttackDamage==6 && a.Health==7 && osk.AttackDamage==6 && osk.Health==6 && v.AttackDamage==6,"t17 buffs other friendly minions only");break;
                    case "TLC_452t18":
                        var candidates=new[] { deck[2],deck[3] }.ToList();
                        var first=candidates[rng.Next(candidates.Count)];candidates.Remove(first);var second=candidates[rng.Next(candidates.Count)];
                        Assert(owner.HandZone.Skip(2).Select(c=>c.Id).SequenceEqual(new[] {first.Id,second.Id}) && owner.DeckZone.Count==2 &&
                            owner.HandZone.Skip(2).Cast<Minion>().All(m=>m.AttackDamage==2 && m.Health==2 && m.Cost==2) &&
                            next.IdEntityDic[deck[0].Id].Zone==owner.DeckZone && next.IdEntityDic[deck[1].Id].Zone==owner.DeckZone,"t18 seeded distinct physical minions become 2/2/2; skips nonminions");break;
                    case "TLC_452t19":
                        var copy=owner.BoardZone.Single(m=>m.Card.Id=="CS2_200");
                        Assert(copy.Id!=v.Id && copy.AttackDamage==8 && copy.Health==9 && v.Controller==owner.Opponent && v.Health==7,"t19 copies chosen enemy with +2/+2 without changing original");break;
                    case "TLC_452t20":
                        var six=OfflineCardPools.Candidates(next,CardClass.MAGE,TitanographerOsk.Six,osk.Card.AssetId);
                        var summon=owner.BoardZone.Single(m=>m.Id!=osk.Id && m.Id!=a.Id);
                        var expectedSix=six[rng.Next(six.Count)];
                        Assert(summon.Card.Id==expectedSix.Id && summon.Card.Cost==6 && summon.HasTaunt && summon.HasLifeSteal,
                            $"t20 seeded six-Cost summon gains both keywords: expected={expectedSix.Id}, actual={summon.Card.Id}, cost={summon.Card.Cost}, taunt={summon.HasTaunt}, lifesteal={summon.HasLifeSteal}");break;
                    case "TLC_452t21": Assert(v.Zone==owner.Opponent.SetasideZone && b.Zone==owner.Opponent.SetasideZone && v[GameTag.ZONE]==(int)Zone.REMOVEDFROMGAME && b[GameTag.ZONE]==(int)Zone.REMOVEDFROMGAME && owner.Opponent.GraveyardZone.IsEmpty,"t21 removes two distinct targets without deaths");break;
                    case "TLC_452t22":
                        int index=rng.Next(3);
                        Assert(osk.AttackDamage==8 && osk.Health==7 && v.Health==(index==0?3:7) && b.Health==(index==1?1:5) && owner.Opponent.Hero.Health==(index==2?26:30),"t22 buffs self and deals four to seeded enemy only");break;
                    case "TLC_452t23": Assert(osk.AttackDamage==7 && osk.Health==8 && owner.HandZone.Count==3 && next.IdEntityDic[deck[^1].Id].Zone==owner.HandZone,"t23 buffs self and draws exact top card");break;
                    case "TLC_452t24": Assert(osk.AttackDamage==6 && osk.Health==9 && osk.CantBeTargetedBySpells,"t24 Health and Elusive");break;
                    case "TLC_452t26": Assert(v.Zone==owner.Opponent.GraveyardZone && osk.Health==6 && b.Health==5,"t26 lethal twenty to another minion only");break;
                    case "TLC_452t27": Assert(v.Health==4 && b.Health==2 && owner.Opponent.Hero.Health==27 && owner.Hero.Health==24 && a.Health==5 && osk.Health==6,"t27 damages all enemies and heals all friendlies with cap");break;
                    case "TLC_452t28": Assert(owner.BoardZone.Count(m=>m.Card.Id=="TTN_960t5" && m.AttackDamage==6 && m.Health==6)==2,"t28 two 6/6 Infernals");break;
                    case "TLC_452t29": Assert(owner.BoardZone.Count==1 && owner.BoardZone.Single().Id==osk.Id && owner.Opponent.BoardZone.IsEmpty && new[] {a,v,b}.All(m=>m[GameTag.ZONE]==(int)Zone.REMOVEDFROMGAME) && owner.GraveyardZone.IsEmpty && owner.Opponent.GraveyardZone.IsEmpty,"t29 banishes both sides except self without deaths");break;
                    case "TLC_452t30": Assert(osk.Health==11 && osk.AttackDamage==6 && owner.Hero.Armor==5,"t30 self Health and hero Armor");break;
                    case "TLC_452t31":
                        Assert(osk.AttackDamage==11 && owner.Hero.AttackDamage==5,"t31 both Attack grants");
                        End(next);Assert(owner.Hero.AttackDamage==0 && osk.AttackDamage==11,"t31 only hero grant expires");break;
                    case "TLC_452t32": Assert(osk.AttackDamage==8 && osk.Health==8 && next.IdEntityDic[deck[1].Id].Zone==owner.HandZone && owner.HandZone.Count==3 && next.IdEntityDic[deck[0].Id].Zone==owner.DeckZone,"t32 buffs self and tutors physical weapon past spell");break;
                    case "TLC_452t33": Assert(owner.HandZone.Count==10 && owner.HandZone.Skip(2).All(c=>c.Card.Id=="YOG_514" && ((Minion)c).AttackDamage==1 && ((Minion)c).Health==1) && owner.DeckZone.Count==4,"t33 fills only free slots with 1/1 Tendrils, no draw");break;
                    case "TLC_452t34": Assert(v.Zone==owner.Opponent.BoardZone && v.Health==3 && b.Zone==owner.Opponent.GraveyardZone && a.Health==5 && osk.Health==6 && v.NumAttacksThisTurn==0,"t34 forced combat resolves and skips dead cohort member without ordinary budget");break;
                    case "TLC_452t35": Assert(v.Controller==owner && v.Zone==owner.BoardZone && v.AttackDamage==6 && v.Health==7 && b.Controller==owner.Opponent,"t35 transfers chosen enemy only with stats intact");break;
                    default: throw new Exception("Missing Osk effect assertion: "+id);
                }
            });
            Console.WriteLine("PASS Osk effect "+id+" seed="+seed+" original/clone/load");
        }
        Assert(TitanographerOsk.Variants.Length==31 && !TitanographerOsk.Variants.Contains("TLC_452t10") && !TitanographerOsk.Variants.Contains("TLC_452t25"),"exact excluded variants");
    }
    private static void Unpinned_CopyAndBanish()
    {
        var g=DiscoverRealProof.New();var p=g.Player1;var victim=Summon(p.Opponent,"CS2_200");
        foreach(var effect in Effects.AttackHealth_N(2)) effect.ApplyTo(victim);victim.Damage=3;
        Play(g,p,"TLC_452t19",victim);
        var copy=p.BoardZone.Single(m=>m.Card.Id=="CS2_200");
        Assert(copy.AttackDamage==10 && copy.BaseHealth==11 && copy.Damage==3,"enemy copy retains buffs/damage then gains +2/+2");
        var second=Summon(p.Opponent,"CS2_182");var firstId=victim.Id;var secondId=second.Id;
        Play(g,p,"TLC_452t21",victim);
        Assert(p.Choice!=null && !p.Choice.Choices.Contains(firstId),"second banish choice excludes first");
        DiscoverRealProof.Saved(g,next=>{
            DiscoverRealProof.Answer(next,secondId);
            Assert(next.Player2.BoardZone.Count==0 && next.IdEntityDic[firstId].Zone==next.Player2.SetasideZone && next.IdEntityDic[firstId][GameTag.ZONE]==(int)Zone.REMOVEDFROMGAME,"both banished without death; native removed zone");
        });
    }
}
