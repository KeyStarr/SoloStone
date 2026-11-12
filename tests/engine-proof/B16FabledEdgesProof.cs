using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
internal static class B16FabledEdgesProof
{
    internal static void Run()
    {
        Unpinned_StoredRewindOverflow(); Unpinned_Boons(); Unpinned_Hammer(); AvatarHero();
        Unpinned_RafaamDrawAndDiscount(); Unpinned_Kingslayers(); Unpinned_City(); Unpinned_WellCast(); Unpinned_BoonSummon();
    }
    private static void AvatarHero()
    {
        var game = New();
        Play(game, game.Player1, "TIME_209t2", game.Player1.Hero);
        foreach (var branch in new[] { game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()), game })
        {
            var owner = branch.Player1;
            var victim = Summon(owner.Opponent, "CS2_182");
            int health = owner.Opponent.Hero.Health;
            Core2026Proof.Check(owner.Hero.AttackDamage == 2 && branch.Process(HeroAttackTask.Any(owner, owner.Opponent.Hero)), "Avatar enchants and attacks with a friendly hero");
            Core2026Proof.Check(owner.Opponent.Hero.Health == health - 4 && victim.Damage == 2, "Avatar hero attack damages every enemy once across clone/load");
            End(branch);
            Core2026Proof.Check(owner.Hero.AttackDamage == 0 && !owner.Hero.AppliedEnchantments.Any(e => e.Card.Id == "TIME_209t2e"), "Avatar hero enchantment and trigger expire together");
        }
    }
    private static Game New()
    {
        var g=new Game(new GameConfig {StartPlayer=1,RandomSeed=43,Shuffle=false,SkipMulligan=true,History=true,Logging=false,
            Player1Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList(),Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList()});
        g.StartGame(); foreach(var p in new[]{g.Player1,g.Player2}) ClearHand(p); return g;
    }
    private static void ClearHand(Controller p)
    { foreach(var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card)); }
    private static void ClearDeck(Controller p)
    { foreach(var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card)); }
    private static void Answer(Game g,string id=null)
    {
        var choice=g.GetPendingDecisions().Single();
        int entity=id==null?g.CurrentPlayer.Choice.Choices[0]:g.CurrentPlayer.Choice.Choices.Single(x=>g.IdEntityDic[x].Card.Id==id);
        Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(choice.Id,choice.ControllerId,new[]{entity}),out var error),"answer: "+error);
    }
    private static void Rewind(Game g)
    {
        Play(g,g.Player1,"TIME_001"); Core2026Proof.Check(g.Rewind!=null,"state coexists with Rewind");
        var t=g.Rewind;
        Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(t.OfferChoiceId,t.ControllerId,new[]{t.RewindEntityId}),out _),"rewind state accepted");
        while(g.Rewind!=null) {t=g.Rewind; Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(t.OfferChoiceId,t.ControllerId,new[]{t.KeepEntityId}),out _),"keep accepted");}
    }
    private static void Unpinned_StoredRewindOverflow()
    {
        var g=New();var original=Add(g.Player1,"TIME_020"); FabledRules.Vanish(original); Rewind(g);
        foreach(var x in new[]{g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())})
        {
            while(!x.Player2.HandZone.IsFull) Add(x.Player2,"CS2_182");
            var result=FabledRules.ReturnVanished(x.Player2,120074);
            Core2026Proof.Check(result.Id==original.Id && result.Zone.Type==Zone.GRAVEYARD && x.Player2.HandZone.Count==10,"full hand burns original once after Rewind/clone/load");
            Core2026Proof.Check(FabledRules.ReturnVanished(x.Player1,120074)==null,"burned original cannot return again");
        }
    }
    private static void Unpinned_Boons()
    {
        var g=New();var p=g.Player1;
        foreach(var option in new[]{"TIME_619t3","TIME_619t3","TIME_619t4","TIME_619t5"})
        { Play(g,p,"TIME_619t2"); Answer(g,option); }
        var body=Summon(p,"TIME_619t"); g.AuraUpdate();
        Core2026Proof.Check(body.HasTaunt && body.HasLifeSteal && body[GameTag.RUSH]>0,"three persistent keywords");
        Core2026Proof.Check(body[GameTag.TAUNT]==1 && body[GameTag.LIFESTEAL]==1,"stacked Boons expose boolean keywords");
        Core2026Proof.Check(body[GameTag.TAG_SCRIPT_DATA_NUM_1]==12,"each boon adds two, including repeat");
        var enemy=Summon(p.Opponent,"TIME_619t");
        Core2026Proof.Check(!enemy.HasTaunt && !enemy.HasLifeSteal && enemy[GameTag.RUSH]==0,"other controller gets no Boons");
        p.AppliedEnchantments.First(e=>e.Card.Id=="TIME_619e").Remove();g.AuraUpdate();
        Core2026Proof.Check(body.HasTaunt,"removing one repeated Taunt source retains the other");
        Rewind(g);
        foreach(var x in new[]{g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())})
        {
            var c=x.Player1; var bwon=(Minion)x.IdEntityDic[body.Id];
            Core2026Proof.Check(bwon.HasTaunt && bwon.HasLifeSteal && bwon[GameTag.RUSH]>0,"Boons survive clone/load/Rewind");
            bwon.Silence();x.AuraUpdate();Core2026Proof.Check(bwon.HasTaunt && bwon.HasLifeSteal && !bwon.HasDeathrattle,"external Boons survive Silence");
            Kill(x,bwon);
            var retained=Entity.FromCard(c,Cards.FromId("TIME_619t"),zone:c.DeckZone);
            Play(x,c,"TIME_619"); Answer(x,"TIME_619t3");
            Core2026Proof.Check(retained.Zone==c.DeckZone && c.BoardZone.Any(m=>m.Card.Id=="TIME_619t"),"friendly death wins over deck tutor");
        }
        foreach(int cost in new[]{4,6,8,10,12,14})
            Console.WriteLine("Bwonsamdi cost="+cost+" bucket="+OfflineCardPools.Candidates(g,p.HeroClass,OfflinePoolFilter.Random(type:CardType.MINION).WithCost(cost),120183).Count);
    }
    private static void Unpinned_Hammer()
    {
        var g=New();var p=g.Player1; var hammer=(Weapon)Play(g,p,"TIME_209t");
        hammer.Damage+=hammer.Durability;g.DeathProcessingAndAuraUpdate();
        var grown=(Weapon)p.DeckZone.Single(c=>c.Card.Id=="TIME_209t");
        Core2026Proof.Check(grown.AttackDamage==Cards.FromId("TIME_209t").ATK+2,"Hammer death gains permanent two");
        var muradin=(Minion)Play(g,p,"TIME_209"); Rewind(g);
        foreach(var x in new[]{g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())})
        {
            var captured=(Minion)x.IdEntityDic[muradin.Id]; Kill(x,captured);
            var released=(Weapon)x.Player1.HandZone.Single(c=>c.Card.Id=="TIME_209t");
            Core2026Proof.Check(released.AttackDamage==grown.AttackDamage,"capture/release retains permanent bonus after clone/load/Rewind");
            PlayEntity(x,x.Player1,released); released.Damage+=released.Durability;x.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(x.Player1.DeckZone.Single(c=>c.Card.Id=="TIME_209t")[GameTag.ATK]==grown.AttackDamage+2,"second weapon death stacks permanent bonus");
        }
        var silent=(Minion)g.IdEntityDic[muradin.Id];silent.Silence();Kill(g,silent);
        Core2026Proof.Check(!g.Player1.HandZone.Any(c=>c.Card.Id=="TIME_209t"),"Silenced Muradin releases nothing");
        var avatar=Summon(g.Player1,"CS2_182");int attack=avatar.AttackDamage;
        Play(g,g.Player1,"TIME_209t2",avatar);Core2026Proof.Check(avatar.AttackDamage==attack+2,"Avatar buff");
        End(g);Core2026Proof.Check(avatar.AttackDamage==attack,"Avatar expires at end of turn");
    }
    private static void Unpinned_RafaamDrawAndDiscount()
    {
        var g=New();var p=g.Player1;ClearDeck(p);
        foreach(var id in new[]{"TIME_005t1","TIME_005t1","TIME_005t2","TIME_005t4"}) Entity.FromCard(p,Cards.FromId(id),zone:p.DeckZone);
        Play(g,p,"TIME_005t3");
        Core2026Proof.Check(p.Choice.Choices.Select(x=>g.IdEntityDic[x].Card.Id).Distinct().Count()==3,"Discover offers distinct definitions");
        foreach(var x in new[]{g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())})
        { Answer(x,"TIME_005t1"); Core2026Proof.Check(x.Player1.DeckZone.Count==3 && x.Player1.HandZone.Count(c=>c.Card.Id=="TIME_005t1")==1,"saved Discover moves a real deck entity"); }
        Answer(g,"TIME_005t1"); Play(g,p,"TIME_005t8"); End(g);End(g);
        Play(g,p,"CS2_182");var other=Add(p,"TIME_005t4");g.AuraUpdate();
        Core2026Proof.Check(other.Cost==2,"Murloc discount survives turns and non-family plays; cost="+other.Cost+" ledger="+string.Join(",",p.AppliedEnchantments.Select(e=>e.Card.Id)));
        PlayEntity(g,p,other);var after=Add(p,"TIME_005t4");g.AuraUpdate();Core2026Proof.Check(after.Cost==5,"next family consumes grant once");
    }
    private static void Unpinned_Kingslayers()
    {
        var g=New();var p=g.Player1;ClearDeck(p);ClearDeck(p.Opponent);
        var llane=Entity.FromCard(p.Opponent,Cards.FromId("TIME_875t"),zone:p.Opponent.DeckZone);
        Play(g,p,"TIME_875t1");int fatigue=p.Hero.Fatigue;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p,p.Opponent.Hero)),"Kingslayers attack");
        Core2026Proof.Check(llane.Zone==p.Opponent.HandZone && p.Hero.Fatigue==fatigue,"Llane eligible; empty search never fatigues");
        End(g);int before=p.Opponent.Hero.Fatigue;PlayEntity(g,p.Opponent,llane);
        Core2026Proof.Check(llane.Zone==p.Opponent.DeckZone && p.Opponent.Hero.Fatigue==before+1,"Llane draws fatigue before same-entity shuffle for current player");
    }
    private static void Unpinned_City()
    {
        var g=New();var p=g.Player1;var city=(BoardObject)Play(g,p,"TIME_211t2t");var target=Summon(p,"CS2_182");
        Core2026Proof.Check(LocationRules.Activate(p,city,target),"empowered City activation");
        var copy=p.BoardZone.Last();Core2026Proof.Check(copy!=target && copy.AttackDamage==target.AttackDamage*2 && copy.Health==target.Health*2,"City doubles summoned copy with enchantment");
        copy.Silence();Core2026Proof.Check(copy.AttackDamage==target.AttackDamage && copy.Health==target.Health,"City stats are removable buff layer");
        while(!p.BoardZone.IsFull) Summon(p,"CS2_182");city[GameTag.LOCATION_ACTION_COOLDOWN]=0;int durability=city.Durability;
        Core2026Proof.Check(!LocationRules.Activate(p,city,target) && city.Durability==durability,"full board rejects City without spending durability");
    }
    private static void Unpinned_WellCast()
    {
        var g=New();var p=g.Player1;var well=(BoardObject)Play(g,p,"TIME_211t1t");
        var pool=OfflineCardPools.Candidates(g,p.HeroClass,OfflinePoolFilter.Random(type:CardType.SPELL),119928).ToArray();
        int seed=Enumerable.Range(1,1000).First(n=> {
            g.SetRandomSeed(n);var probe=g.CloneExact(history:true);
            return Enumerable.Range(0,10).Any(_=>pool[probe.Random.Next(pool.Length)].Id=="CORE_CS2_023");
        });
        g.SetRandomSeed(seed);Core2026Proof.Check(LocationRules.Activate(p,well),"marked Well activation");
        var spell=p.HandZone.First(c=>c.Card.Id=="CORE_CS2_023");
        foreach(var other in p.HandZone.Where(c=>c!=spell).ToArray()) p.SetasideZone.Add(p.HandZone.Remove(other));
        well.Destroy();g.DeathProcessingAndAuraUpdate();
        foreach(var x in new[]{g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())})
        {
            int before=x.Player1.DeckZone.Count;PlayEntity(x,x.Player1,x.IdEntityDic[spell.Id]);
            Core2026Proof.Check(x.Player1.DeckZone.Count==before-4 && x.Player1.UsedMana==3,"Well spell casts two complete bodies for one payment after source destruction and restore");
        }
    }
    private static void Unpinned_BoonSummon()
    {
        var g=New();var p=g.Player1;Play(g,p,"TIME_619t2");Answer(g,"TIME_619t3");
        var bwon=Summon(p,"TIME_619t");g.SetRandomSeed(13);
        var pool=OfflineCardPools.Candidates(g,p.HeroClass,OfflinePoolFilter.Random(type:CardType.MINION).WithCost(6),120183).ToArray();
        string expected=pool[g.CloneExact(history:true).Random.Next(pool.Length)].Id;
        foreach(var x in new[]{g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())})
        {
            Kill(x,(Minion)x.IdEntityDic[bwon.Id]);
            Core2026Proof.Check(x.Player1.BoardZone.Any(m=>m.Card.Id==expected && m.HasTaunt),"Bwonsamdi seeded exact-cost summon inherits controller Boons");
        }
        for(int i=0;i<3;i++){Play(g,p,"TIME_619t2");Answer(g,"TIME_619t3");}
        Kill(g,bwon);Core2026Proof.Check(p.BoardZone.IsEmpty,"empty exact cost twelve yields nothing");
    }

}
