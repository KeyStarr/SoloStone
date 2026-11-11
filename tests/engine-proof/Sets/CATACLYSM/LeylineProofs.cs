using SabberStoneCore.EffectSystem;
using System.Collections.Generic;
using System.Reflection;
using SabberStoneCore.Triggers;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

// Behavioural leans: data/rules-evidence/leyline/*.yaml; coordinator D3-D5 override research deferrals.
internal static class LeylineProofs
{
    private static readonly string[] Family = { "MEND_500", "MEND_502", "MEND_504" };
    internal static void Run()
    {
        StandardSetsProof.Case("MEND_500", Lean_Bursting);
        StandardSetsProof.Case("MEND_502", Lean_Crystallized);
        StandardSetsProof.Case("MEND_504", Lean_Nexus);
        StandardSetsProof.Case("MEND_501", Lean_Walker);
        StandardSetsProof.Case("MEND_503", Lean_Triggers);
        StandardSetsProof.Case("MEND_506", Lean_Magnitude);
        StandardSetsProof.Case("MEND_505", Lean_Arcanomicon);
    }
    private static void EmptyHand(Controller p)
    { foreach (var x in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(x)); }
    private static void Deck(Controller p, params string[] ids)
    {
        foreach (var x in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(x));
        foreach (var id in ids.Reverse()) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId(id)));
    }
    private static void Lean_NativeText(Game g, Controller p)
    {
        var flags=BindingFlags.NonPublic|BindingFlags.Static;
        var match=new NativeMatch((_,__)=>{});
        typeof(NativeMatch).GetProperty("Game").SetValue(match,g);
        var full=PowerHistoryBuilder.FullEntity(Add(p,"MEND_500"));
        Core2026Proof.Check(full.Entity.Tags[GameTag.TAG_SCRIPT_DATA_NUM_1]==4 && full.Entity.Tags[GameTag.TAG_SCRIPT_DATA_NUM_2]==1,
            "first reveal captures derived singular text");
        var project=typeof(NativeMatch).GetMethod("ToNativeHistory",BindingFlags.NonPublic|BindingFlags.Instance);
        var native=(PegasusGame.PowerHistoryData)project.Invoke(match,new object[]{full});
        Core2026Proof.Check(native.FullEntity.Tags.Any(t=>t.Name==3 && t.Value==1),"native full includes captured total times");
        var hidden=PowerHistoryBuilder.FullEntity(Add(p.Opponent,"MEND_500"));
        native=(PegasusGame.PowerHistoryData)project.Invoke(match,new object[]{hidden});
        Core2026Proof.Check(native.FullEntity.Name=="" && native.FullEntity.Tags.All(t=>t.Name!=2 && t.Name!=3),"opponent hidden hand text stays private");
        var tags=new Dictionary<GameTag,int>{{(GameTag)31120,2},{(GameTag)31121,3},{(GameTag)31122,4},{GameTag.COST,1}};
        var convert=typeof(NativeMatch).GetMethod("ToNativeTags",flags);
        var wire=(List<PegasusGame.Tag>)convert.Invoke(null,new object[]{tags});
        Core2026Proof.Check(wire.Count==1 && wire[0].Name==48,"all private progression tags filtered from snapshot lists");
        foreach(var tag in tags.Keys.Where(t=>t!=GameTag.COST))
            Core2026Proof.Check(project.Invoke(match,new object[]{PowerHistoryBuilder.TagChange(p.Id,tag,9)})==null,"private raw tag change filtered");
        // Replay the old snapshot after live progression: old packets must not inherit future state.
        var old=g.IdEntityDic[full.Entity.Id]; LeylineRules.Upgrade(p,old,effect:1,extraTriggers:1);
        native=(PegasusGame.PowerHistoryData)project.Invoke(match,new object[]{full});
        Core2026Proof.Check(native.FullEntity.Tags.Any(t=>t.Name==2 && t.Value==4) && native.FullEntity.Tags.Any(t=>t.Name==3 && t.Value==1),"historical snapshot stays frozen");
        var show=PowerHistoryBuilder.ShowEntity(Add(p,"MEND_500"));
        Core2026Proof.Check(show.Entity.Tags[GameTag.TAG_SCRIPT_DATA_NUM_1]==5 && show.Entity.Tags[GameTag.TAG_SCRIPT_DATA_NUM_2]==2,"new generated show gets current text");
        native=(PegasusGame.PowerHistoryData)project.Invoke(match,new object[]{new PowerHistoryChangeEntity {
            CardId="CS2_029", Entity=new PowerHistoryEntity { Id=full.Entity.Id, Name="", Tags=new Dictionary<GameTag,int>{{GameTag.CARDTYPE,(int)CardType.SPELL}} } }});
        Core2026Proof.Check(native.ChangeEntity.Tags.Any(t=>t.Name==2 && t.Value==0) && native.ChangeEntity.Tags.Any(t=>t.Name==3 && t.Value==0),
            "transform clears previous Leyline text values");
        // Undo only fixture progress, before the main controller scope scenario.
        p[(GameTag)31121]=0; p[(GameTag)31122]=0; EmptyHand(p); EmptyHand(p.Opponent);
    }
    private static void Lean_Bursting(Game g, Controller p, string id)
    {
        int hp = p.Opponent.Hero.Health;
        Play(g,p,id); Core2026Proof.Check(p.Opponent.Hero.Health == hp,"no minion means no damage");
        var m = Summon(p.Opponent,"CS2_231"); m.HasStealth = true;
        Summon(p,"CS2_052"); Play(g,p,id);
        Core2026Proof.Check(m.Zone.Type == Zone.GRAVEYARD && p.Opponent.Hero.Health == hp-4,"random Stealth target, five damage, four spill without second Spell Damage");
        m = Summon(p.Opponent,"EX1_008"); m.HasDivineShield = true; hp = p.Opponent.Hero.Health;
        Play(g,p,id); Core2026Proof.Check(m.Health == m.Card.Health && !m.HasDivineShield && p.Opponent.Hero.Health == hp,"shield prevents spill");
        m.IsImmune = true; Play(g,p,id); Core2026Proof.Check(m.Health == m.Card.Health && p.Opponent.Hero.Health == hp,"immune prevents spill");
        m.IsImmune = false; Kill(g,m); ClearBoard(g,p);
        Play(g,p,"MEND_503"); hp=p.Opponent.Hero.Health;
        m=Summon(p.Opponent,"EX1_556"); Play(g,p,id);
        Core2026Proof.Check(m.Zone.Type==Zone.GRAVEYARD && p.Opponent.BoardZone.Count==0 && p.Opponent.Hero.Health==hp-4,
            "fresh repeat targets Deathrattle's 2/1 replacement, never dead original");
        // Lean: leyline-bursting.yaml and leyline-repeat.yaml; base-only damage must fail.
        LeylineRules.Upgrade(p,p.Hero,effect:1); p.Hero[GameTag.SPELLPOWER]=1;
        m=Summon(p.Opponent,"CS2_231"); hp=p.Opponent.Hero.Health; Play(g,p,id);
        Core2026Proof.Check(m.Zone.Type==Zone.GRAVEYARD && p.Opponent.Hero.Health==hp-5,
            "upgraded five damage plus one Spell Damage spills five over one health");
        var listener=Summon(p,"CS2_231");
        new Trigger(TriggerType.DEATH) { TriggerSource=TriggerSource.ENEMY,
            SingleTask=new CustomTask((game,owner,source,target,stack)=>owner.Hero[GameTag.SPELLPOWER]=3)
        }.Activate(listener);
        m=Summon(p.Opponent,"EX1_556"); hp=p.Opponent.Hero.Health; Play(g,p,id);
        Core2026Proof.Check(p.CurrentSpellPower==3 && m.Zone.Type==Zone.GRAVEYARD && p.Opponent.BoardZone.IsEmpty
            && p.Opponent.Hero.Health==hp-10,
            "fresh Spell Damage: six into three health spills three, then eight into one health spills seven");
        Kill(g,listener);
    }
    private static void Lean_Crystallized(Game g, Controller p, string id)
    {
        foreach (string name in PoolRequirement.PlayableClasses)
        {
            var hero=name=="DEMONHUNTER" ? (CardClass)14 : System.Enum.Parse<CardClass>(name);
            var candidates=OfflineCardPools.Candidates(g,hero,OfflinePoolFilter.Random(type:CardType.MINION).WithCost(6),123504);
            Core2026Proof.Check(candidates.Count>=3 && candidates.All(c=>c.Type==CardType.MINION && c.Cost==6 && c.Implemented),"Any-class exact-six admitted pool for "+name);
        }
        var left=Summon(p,"CS2_182"); var location=LocationProofs.Location(p);
        var fork=g.CloneExact(history:true); Play(g,p,id); Play(fork,fork.Player1,id);
        var m=p.BoardZone.Last();
        Core2026Proof.Check(m.Card.Type==CardType.MINION && m.Card.Cost==6 && m.AttackDamage==m.Card.ATK && m.BaseHealth==m.Card.Health
            && m.ZonePosition==2 && p.BoardZone.GetSlot(0)==left && p.BoardZone.GetSlot(1)==location,
            "exact six-cost admitted minion, printed stats, far right after physical location");
        Core2026Proof.Check(m.Card.Id==fork.Player1.BoardZone.Last().Card.Id,"clone RNG deterministic");
        while(!p.BoardZone.IsFull) Summon(p,"CS2_182");
        var random=g.Random.CaptureState(); Play(g,p,id);
        Core2026Proof.Check(p.BoardZone.SlotCount==7 && random.Equals(g.Random.CaptureState()),"full physical board no summon or roll");
        Kill(g,p.BoardZone.Last()); Kill(g,p.BoardZone.Last());
        var retained=p.BoardZone.Slots.Select(x=>x.Id).ToArray();
        LeylineRules.Upgrade(p,p.Hero,extraTriggers:1); Play(g,p,id);
        var summoned=p.BoardZone.Slots.Skip(5).OfType<Minion>().ToArray();
        Core2026Proof.Check(p.BoardZone.SlotCount==7 && p.BoardZone.Slots.Take(5).Select(x=>x.Id).SequenceEqual(retained)
            && summoned.Length==2 && summoned.All(x=>x.Card.Type==CardType.MINION && x.Card.Cost==6)
            && p.BoardZone.GetSlot(5)==summoned[0] && p.BoardZone.GetSlot(6)==summoned[1] && summoned[0].Id<summoned[1].Id,
            "two free physical slots receive both repeats at far right in creation order after retained location");
        Kill(g,left); Play(g,p,id);
        Core2026Proof.Check(p.BoardZone.SlotCount==7 && p.BoardZone.Last().Card.Cost==6,"one physical slot admits one of two repeats");
    }
    private static void Lean_Nexus(Game g, Controller p, string id)
    {
        Deck(p,"CS2_182","MEND_500","CS2_182"); Play(g,p,"MEND_503"); Play(g,p,"MEND_506");
        Play(g,p,id);
        Core2026Proof.Check(p.HandZone.Count==2 && p.HandZone[0].Card.Id=="CS2_182" && p.HandZone[0].Cost==2
            && p.HandZone[1].Card.Id=="MEND_500" && p.HandZone[1].Cost==2,"two captured draws discounted by two: "+string.Join(",",p.HandZone.Select(x=>x.Card.Id+":"+x.Cost)));
        Core2026Proof.Check(p.HandZone.All(x=>x.AppliedEnchantments.Count(e=>e.Card.Id=="MEND_504e")==1),"one exact enchantment per drawn card");
        Play(g,p,"MEND_501"); Core2026Proof.Check(p.HandZone[1].Cost==1,"Nexus permanent discount composes with global layer");
        g.AuraUpdate(); g.AuraUpdate(); Core2026Proof.Check(p.HandZone[1].Cost==1,"refresh never duplicates discount");
        var copy=Generic.Copy(p,p.Hero,p.HandZone[1],Zone.HAND);
        var foreign=Generic.Copy(p.Opponent,p.Opponent.Hero,p.HandZone[1],Zone.HAND); g.AuraUpdate();
        Core2026Proof.Check(copy.Cost==1 && foreign.Cost==2 && copy.AppliedEnchantments.Count(e=>e.Card.Id=="MEND_504e")==1,
            "Nexus per-card layer copies once; global discount derives from destination controller");
        EmptyHand(p); while(p.HandZone.Count<9) Add(p,"GAME_005"); Deck(p,"CS2_182","CS2_182");
        Play(g,p,id); Core2026Proof.Check(p.HandZone.Count==10 && p.HandZone.Last().Cost==2
            && p.GraveyardZone.Any(x=>x.Card.Id=="CS2_182" && x.AppliedEnchantments?.Any(e=>e.Card.Id=="MEND_504e")!=true),"partial hand burns second draw without enchantment");
        EmptyHand(p); Deck(p); int hp=p.Hero.Health; Play(g,p,id);
        Core2026Proof.Check(p.HandZone.Count==0 && p.Hero.Health==hp-3,"repeat empty-deck draws fatigue one plus two");
    }
    private static void Lean_Walker(Game g, Controller p, string id)
    {
        Lean_NativeText(g,p);
        var spell=Add(p,"MEND_500"); var deck=Entity.FromCard(p,Cards.FromId("MEND_502")); p.DeckZone.Add(deck);
        var other=Add(p.Opponent,"MEND_500"); var ordinary=Add(p,"CS2_029");
        var walker=(Minion)Play(g,p,id);
        Core2026Proof.Check(spell.Cost==3 && deck.Cost==4 && other.Cost==4 && ordinary.Cost==4,"controller-only hand/deck discount, unrelated spell unchanged");
        Core2026Proof.Check(spell[GameTag.TAG_SCRIPT_DATA_NUM_1]==4 && spell[GameTag.TAG_SCRIPT_DATA_NUM_2]==1,"singular hand text");
        var expectedGame=g.CloneExact(history:true);
        string expected=new EffectContext(expectedGame,p.Id,walker.Id).SampleDistinct(Family.Select(Cards.FromId).ToArray(),1)[0].Id;
        Core2026Proof.Check(LeylineRules.Family().Select(c=>c.Id).SequenceEqual(Family),"closed family exact printed IDs");
        var fork=g.CloneExact(history:true); int count=p.HandZone.Count; Kill(g,walker); Kill(fork,(Minion)fork.IdEntityDic[walker.Id]);
        var generated=p.HandZone.Last();
        Core2026Proof.Check(p.HandZone.Count==count+1 && generated.Card.Id==expected && generated.Cost==generated.Card.Cost-1
            && generated.Card.Id==fork.Player1.HandZone.Last().Card.Id,"fixed-family uniform RNG path, generated card derives progress, clone deterministic");
        walker=(Minion)Play(g,p,id); walker.Silence(); count=p.HandZone.Count; Kill(g,walker);
        Core2026Proof.Check(p.HandZone.Count==count && spell.Cost==2,"Silence removes Deathrattle but not earned progression");
        var cloned=g.CloneExact(history:true); Play(cloned,cloned.Player1,id);
        Core2026Proof.Check(cloned.IdEntityDic[spell.Id].Cost==1 && spell.Cost==2,"progress clone isolated");
        p.AddHeroAndPower(Cards.FromId("ICC_827"),Cards.FromId("DS1h_292"));
        Core2026Proof.Check(spell.Cost==2 && Add(p,"MEND_502").Cost==3,"hero replacement and future generation retain progress");
        Core2026Proof.Check(PowerHistoryBuilder.CreateGame(g,new[]{p,p.Opponent}).Players.All(x=>x.PowerEntity.Tags.Keys.All(t=>(int)t<31120 || (int)t>31122)),"private progression absent from full snapshot");
        EmptyHand(p); walker=(Minion)Play(g,p,id); while(!p.HandZone.IsFull) Add(p,"GAME_005"); Kill(g,walker);
        Core2026Proof.Check(p.HandZone.Count==10,"generated Leyline respects full hand");
        EmptyHand(p); var stolen=Summon(p.Opponent,id); int before=p.HandZone.Count;
        new ControlTask(EntityType.TARGET).Process(g,p,p.Hero,stolen); g.DeathProcessingAndAuraUpdate(); Kill(g,stolen);
        Core2026Proof.Check(p.HandZone.Count==before+1 && Family.Contains(p.HandZone.Last().Card.Id),"stolen Deathrattle belongs to controller at death");
        var costed=(Playable)Add(p,"MEND_500"); LeylineRules.Upgrade(p,p.Hero,cost:50);
        Core2026Proof.Check(costed.Cost==0 && deck.Cost==0,"global reduction floors hand and deck at zero");
        CostRules.AddNextCardRule(p,cardType:CardType.SPELL,setCost:2);
        Core2026Proof.Check(costed.Cost==2,"next-card SET overrides global numeric discount");
        PlayEntity(g,p,costed); Core2026Proof.Check(Add(p,"MEND_500").Cost==0,"next-card quote paid once then consumed");
    }
    private static void Lean_Triggers(Game g, Controller p, string id)
    {
        var spell=Add(p,"MEND_504"); var other=Add(p.Opponent,"MEND_504");
        var m=(Minion)Play(g,p,id); g.AuraUpdate();
        Core2026Proof.Check(spell[GameTag.TAG_SCRIPT_DATA_NUM_2]==2 && other[GameTag.TAG_SCRIPT_DATA_NUM_2]==1,"only own hand repeat text updates");
        m.Silence(); Kill(g,m); Play(g,p,id); Deck(p,"CS2_182","CS2_182","CS2_182"); PlayEntity(g,p,spell);
        Core2026Proof.Check(p.HandZone.Count==3 && p.HandZone.All(x=>x.Cost==3),"additive repeats survive granting source removal: "+string.Join(",",p.HandZone.Select(x=>x.Card.Id+":"+x.Cost)));
        End(g); End(g); var later=Add(p,"MEND_504"); g.AuraUpdate();
        Core2026Proof.Check(later[GameTag.TAG_SCRIPT_DATA_NUM_2]==3,"game progression survives turn boundary");
        EmptyHand(p); Deck(p,"CS2_182","CS2_182","CS2_182","CS2_182");
        var listener=Summon(p,"CS2_231");
        // A fixture listener upgrades between instances; removing the snapshot must break both assertions.
        var trigger=new Trigger(TriggerType.DRAW) { TriggerSource=TriggerSource.FRIENDLY,
            SingleTask=new CustomTask((game,owner,source,target,stack)=>LeylineRules.Upgrade(owner,(IPlayable)source,extraTriggers:1,effect:1)) };
        trigger.Activate(listener);
        int casts=p.NumSpellsPlayedThisGame; Play(g,p,"MEND_504");
        Core2026Proof.Check(p.HandZone.Count==3 && p.HandZone.All(x=>x.Cost==3) && p.NumSpellsPlayedThisGame==casts+1,
            "Lean cast-start count and magnitude snapshot despite draw-trigger upgrades; one spell cast");
        var fresh=Add(p,"MEND_504"); g.AuraUpdate();
        Core2026Proof.Check(fresh[GameTag.TAG_SCRIPT_DATA_NUM_1]==4 && fresh[GameTag.TAG_SCRIPT_DATA_NUM_2]==6,
            "draw listener actually upgraded each instance, affecting subsequent casts only");
        Kill(g,listener);
    }
    private static void Lean_Magnitude(Game g, Controller p, string id)
    {
        var spells=Family.Select(x=>Add(p,x)).ToArray(); var m=(Minion)Play(g,p,id); g.AuraUpdate();
        Core2026Proof.Check(m.Card.IsRace(Race.BEAST) && m.CantBeTargetedBySpells && m.CantBeTargetedByHeroPowers,"exact Beast Elusive mapping");
        Core2026Proof.Check(spells.Select(x=>x[GameTag.TAG_SCRIPT_DATA_NUM_1]).SequenceEqual(new[]{5,7,2})
            && spells.Select(x=>x.Cost).SequenceEqual(new[]{4,5,2}),"effect mapping changes magnitude only");
        m.Silence(); Kill(g,m); Core2026Proof.Check(spells[0][GameTag.TAG_SCRIPT_DATA_NUM_1]==5,"resolved upgrade survives Silence and death");
        // Exact dynamic pool above ordinary range: no clamping, sparse eleven and empty twelve.
        for(int i=0;i<4;i++) { var upgrade=(Minion)Play(g,p,id); Kill(g,upgrade); }
        PlayEntity(g,p,spells[1]); Core2026Proof.Check(p.BoardZone.Last().Card.Cost==11,"exact sparse eleven-cost result");
        m=(Minion)Play(g,p,id); Kill(g,m); int slots=p.BoardZone.SlotCount;
        Play(g,p,"MEND_502"); Core2026Proof.Check(p.BoardZone.SlotCount==slots,"empty twelve-cost pool no fallback");
    }
    private static void Lean_Arcanomicon(Game g, Controller p, string id)
    {
        Play(g,p,id);
        Core2026Proof.Check(p.HandZone.Select(x=>x.Card.Id).SequenceEqual(Family),"Lean printed-family generation order");
        var choice=p.Choice; Core2026Proof.Check(choice!=null && choice.Choices.Select(x=>g.IdEntityDic[x].Card.Id).SequenceEqual(new[]{"MEND_505t","MEND_505t2","MEND_505t3"}),"exact fixed distinct options, no hand slot consumed");
        var fork=g.CloneExact(history:true); var restored=Game.LoadSnapshot(g.SaveSnapshot());
        foreach(var candidate in new[]{g,fork,restored}) {
            var owner=candidate.Player1;
            int option=owner.Choice.Choices.Single(x=>candidate.IdEntityDic[x].Card.Id=="MEND_505t2");
            PlayerDecisionProof.Answer(candidate,option);
            Core2026Proof.Check(owner.Choice==null && owner.HandZone.Select(x=>x.Cost).SequenceEqual(new[]{2,3,0}),"one cost upgrade on all three, choice clone/snapshot");
        }
        EmptyHand(p);
        var counter=(Spell)Entity.FromCard(p.Opponent,Cards.FromId("EX1_287")); p.Opponent.SecretZone.Add(counter); counter.Power.Trigger.Activate(counter);
        Play(g,p,id); Core2026Proof.Check(p.Choice==null && p.HandZone.Count==0,"countered Arcanomicon neither generates nor chooses");
        while(p.HandZone.Count<8) Add(p,"GAME_005"); Play(g,p,id);
        Core2026Proof.Check(p.HandZone.Skip(8).Select(x=>x.Card.Id).SequenceEqual(Family.Take(2)),"partial hand survivors follow printed family order");
        PlayerDecisionProof.Answer(g,p.Choice.Choices[0]);
        Core2026Proof.Check(p.HandZone.Last()[GameTag.TAG_SCRIPT_DATA_NUM_2]==2,"Energize upgrades existing cards");
        EmptyHand(p); Play(g,p,id); PlayerDecisionProof.Answer(g,p.Choice.Choices[2]);
        Core2026Proof.Check(p.HandZone.Select(x=>x[GameTag.TAG_SCRIPT_DATA_NUM_1]).SequenceEqual(new[]{6,8,3}),"Empower plus two mapping");
        var before=(Cost:LeylineRules.CostReduction(p),Magnitude:LeylineRules.Magnitude(p.HandZone[0]),Times:LeylineRules.Times(p.HandZone[0]));
        EmptyHand(p); var automatic=(Spell)Entity.FromCard(p,Cards.FromId(id)); var autoFork=g.CloneExact(history:true);
        Generic.CastSpell(p,g,automatic,null,0); g.DeathProcessingAndAuraUpdate();
        Generic.CastSpell(autoFork.Player1,autoFork,(Spell)autoFork.IdEntityDic[automatic.Id],null,0); autoFork.DeathProcessingAndAuraUpdate();
        foreach(var owner in new[]{p,autoFork.Player1}) {
            var first=owner.HandZone.First();
            var delta=(Cost:LeylineRules.CostReduction(owner)-before.Cost,
                Magnitude:LeylineRules.Magnitude(first)-before.Magnitude,Times:LeylineRules.Times(first)-before.Times);
            Core2026Proof.Check(owner.Choice==null && owner.HandZone.Select(x=>x.Card.Id).SequenceEqual(Family)
                && (delta==(2,0,0) || delta==(0,2,0) || delta==(0,0,1)),
                "automatic cast applies exactly one permitted upgrade: cost minus two, effect plus two, or one extra trigger");
        }
        Core2026Proof.Check(LeylineRules.CostReduction(p)==LeylineRules.CostReduction(autoFork.Player1)
            && p.HandZone.Select(x=>(x.Cost,LeylineRules.Magnitude(x),LeylineRules.Times(x)))
                .SequenceEqual(autoFork.Player1.HandZone.Select(x=>(x.Cost,LeylineRules.Magnitude(x),LeylineRules.Times(x)))),
            "automatic choice clone determinism includes cost, magnitude and repeat state");
    }
}
