using System.Reflection;
using System.Runtime.Serialization;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class HistoryResolutionProof
{
    private static void Check(bool value,string message)=>Core2026Proof.Check(value,"history 2b: "+message);
    internal static void Run()
    {
        HistoryResolutionReviewProof.Run();
        foreach(bool history in new[]{false,true}) { Damage(history); Payments(history); Combat(history); Residence(history); }
        Voraxx(); ProducerAnnotations(); ArmorSet(); Persistence();
        Console.WriteLine("PASS history 2b: damage/Health/armor, combat, payments, residence, Voraxx, clone/snapshot/canonical/fair view");
    }
    private static void Damage(bool history)
    {
        var g=HistoryFactsProof.Fixture(history);var p=g.Player1;var enemy=g.Player2;var q=g.HistoryFacts.Query;
        int visible=0,armorVisible=0,healVisible=0;bool verifyDamage=true;
        g.TriggerManager.DamageTrigger += entity=>{
            if(!verifyDamage) return;
            var d=q.Damage().Last();
            Check(d.VictimId==entity.Id && d.Amount>0,"damage committed before damage reactions");
            if(entity is Hero && d.HealthDamage>0)
                Check(q.HealthChanges().Last().Sequence==d.Sequence+1 && q.HealthChanges().Last().Reason==HistoryHealthChangeReason.Damage,
                    "damage and Health change published together before reactions: "+d.Sequence+" / "+q.HealthChanges().Last().Sequence+" / "+q.HealthChanges().Last().Reason+" / verify="+verifyDamage);
            visible++;
        };
        g.TriggerManager.ArmorTrigger += entity=>{Check(q.ArmorGains().Last().Amount==4,"armor visible before OnArmorTrigger");armorVisible++;};
        g.TriggerManager.HealTrigger += entity=>{Check(q.HealthChanges().Last().Reason==HistoryHealthChangeReason.Heal,"heal visible before reaction");healVisible++;};
        enemy.Hero.GainArmor(p.Hero,4);
        int changes=q.HealthChanges().Count();
        enemy.Hero.TakeDamage(p.Hero,3);
        var armor=q.Damage().Single();
        Check(armor.Amount==3 && armor.ArmorDamage==3 && armor.HealthDamage==0 && armor.PreHitHealth==30
            && q.HealthChanges().Count()==changes && q.HeroTookDamage(enemy.Id,q.ThisTurn),"armor-only damage counts without Health change");
        enemy.Hero.TakeDamage(p.Hero,3);
        var hit=q.Damage().Last();
        Check(hit.Amount==3 && hit.ArmorDamage==1 && hit.HealthDamage==2 && q.HeroDamageEventCount(enemy.Id,q.ThisTurn)==2,"split armor/Health damage");
        Check(q.HeroHealthChanged(enemy.Id,q.ThisTurn) && q.HealthChanges().Last().OldHealth==30 && q.HealthChanges().Last().NewHealth==28,"current Health timeline");
        enemy.Hero.TakeHeal(p.Hero,1);
        Check(enemy.Hero.Health==29 && healVisible==1,"heal retained");
        int count=q.Damage().Count();verifyDamage=false;
        enemy.Hero.IsImmune=true;enemy.Hero.TakeDamage(p.Hero,2);enemy.Hero.IsImmune=false;
        enemy.Hero.TakeDamage(p.Hero,0);
        var shield=Summon(enemy,"CS2_231");shield.HasDivineShield=true;shield.TakeDamage(p.Hero,2);
        Check(q.Damage().Count()==count && !shield.HasDivineShield,"Immune/zero/Divine Shield no damage facts");
        verifyDamage=true;
        var spell=(Spell)Entity.FromCard(p,Cards.FromId("CS2_029"));
        Generic.CastSpell(p,g,spell,enemy.Hero,0);
        var direct=q.Damage().Last();
        Check(direct.SourceId==spell.Id && direct.SourceControllerId==p.Id && direct.SourceKind==CardType.SPELL && direct.CastId>0
            && q.AnySpellDamage(p.Id,q.ThisTurn) && q.SpellDamageAmount(p.Id,q.ThisTurn)==6,"immediate spell attribution, any and sum");
        var body=Summon(p,"CS2_231");enemy.Hero.TakeDamage(body,1);
        Check(q.SpellDamageAmount(p.Id,q.ThisTurn)==6,"minion effect/combat never inherits parent spell credit");
        enemy.Hero.Health=20;enemy.Hero.BaseHealth=25;
        Check(q.HealthChanges().Last().Reason==HistoryHealthChangeReason.Set && q.HealthChanges().Last().NewHealth==25,"set and max-Health changes");
        var replacement=(Hero)Entity.FromCard(enemy,Cards.FromId("HERO_08"));replacement.Health=27;replacement.HeroPower=enemy.Hero.HeroPower;enemy.Hero=replacement;
        Check(q.HealthChanges().Last().Reason==HistoryHealthChangeReason.Replacement && q.HealthChanges().Last().OldHealth==25
            && q.HealthChanges().Last().NewHealth==27,"replacement retains controller timeline");
        Check(visible==4 && armorVisible==1 && q.ArmorGainedAmount(enemy.Id,q.ThisTurn)==4,"one fact per committed family");
        End(g);Check(!q.HeroTookDamage(enemy.Id,q.ThisTurn) && !q.HeroHealthChanged(enemy.Id,q.ThisTurn)
            && q.HeroDamageEventCount(enemy.Id,q.PreviousTurnOf(p.Id))==4,"turn windows keep opponent damage chronology");
    }
    private static void Payments(bool history)
    {
        var g=HistoryFactsProof.Fixture(history);var p=g.Player1;var q=g.HistoryFacts.Query;
        g.TriggerManager.PlayCardTrigger += entity=>Check(q.Payments().Last().RelatedPlayId==q.Plays().Last().PlayId
            && q.Residences(entity.Id,Zone.HAND).Last().ExitSequence.HasValue,"payment and hand exit visible before play reactions");
        g.TriggerManager.CardPreparedTrigger += _=>Check(q.Payments().Last().ActionKind==HistoryPaymentKind.Prepare,"Prepare payment visible before reaction");
        var holding=Add(p,"CS2_182");
        Play(g,p,"CS2_168");
        var payment=q.Payments().Single();var play=q.Plays().Single();
        Check(payment.Mana==1 && payment.ActionKind==HistoryPaymentKind.Play && payment.RelatedPlayId==play.PlayId && payment.Sequence<play.Sequence,"actual payment linked to later committed play");
        p.UsedMana=0;Check(g.Process(HeroPowerTask.Any(p,p.Opponent.Hero)),"hero power accepted");
        var trade=Add(p,"CORE_EX1_005");p.UsedMana=0;Check(g.Process(TradeCardTask.Any(p,trade)),"Trade accepted");
        var prepare=Add(p,"CS2_029");prepare.Cost=5;PrepareRules.Grant(prepare);p.UsedMana=0;Check(g.Process(PrepareCardTask.Any(p,prepare)),"Prepare accepted");
        Check(q.Payments().Select(f=>f.ActionKind).SequenceEqual(new[]{HistoryPaymentKind.Play,HistoryPaymentKind.HeroPower,HistoryPaymentKind.Trade,HistoryPaymentKind.Prepare})
            && q.Plays().Count()==1 && q.ManaSpentWhileHolding(holding.Id,p.Id)==8,"holding includes powers/Trade/Prepare actual mana, threshold eight");
        var two=Add(p,"CS2_120");PlayEntity(g,p,two);
        Check(q.TwoManaPlaysWhileInHandOrDeck(holding.Id,p.Id)==1,"Lotus includes actual two-mana plays only");
        var healthSpell=Add(p,"CS2_029");p.ControllerAuraEffects[GameTag.SPELLS_COST_HEALTH]=1;
        p.UsedMana=0;int damage=q.Damage().Count(),old=p.Hero.Health;
        Check(g.Process(PlayCardTask.Any(p,healthSpell,p.Opponent.Hero)),"Health payment accepted");
        Check(q.Payments().Last().Mana==0 && q.Payments().Last().Health==4
            && q.HealthChanges().Last(f=>f.ControllerId==p.Id).Reason==HistoryHealthChangeReason.Payment
            && p.Hero.Health==old-4 && q.Damage().Count()==damage+1,"Health payment is Mana=0, payment reason, no damage to payer");
        p.ControllerAuraEffects[GameTag.SPELLS_COST_HEALTH]=0;
        int before=q.ManaSpentWhileHolding(holding.Id,p.Id);
        PlayEntity(g,p,holding);
        Check(q.ManaSpentWhileHolding(holding.Id,p.Id)==before && q.Payments().Last().RelatedPlayId==q.Plays().Last().PlayId,"own linked play payment excluded after leaving hand");
        long ownPlay=q.Plays().Last().PlayId;
        Check(q.Payments(q.HeldBeforeThisPlay(holding.Id,ownPlay)).Sum(f=>f.Mana)==before,"held window excludes own linked payment");
        var zero=Add(p,"GAME_005");zero[GameTag.TAG_LAST_KNOWN_COST_IN_HAND]=2;PlayEntity(g,p,zero);
        Check(q.Payments().Last().Mana==0 && q.TwoManaPlaysWhileInHandOrDeck(holding.Id,p.Id)==1,"zero-cost payment ignores stale last cost");
        var ownTwo=Add(p,"CS2_120");PlayEntity(g,p,ownTwo);
        Check(q.TwoManaPlaysWhileInHandOrDeck(ownTwo.Id,p.Id)==0 && q.TwoManaPlaysWhileInHandOrDeck(ownTwo.Id,p.Id,excludeOwnPlay:false)==1,
            "Lotus can exclude its own linked two-mana play");
        var other=Entity.FromCard(p.Opponent,Cards.FromId("CS2_120"));p.UsedMana=0;p.TemporaryMana=2;
        int plays=q.Plays().Count();int facts=g.HistoryFacts.Count;
        // Cost rules: PayPhase quotes the payer's own sources only; a foreign source is rejected without side effects.
        Check(!Generic.PayPhase(p,other) && g.HistoryFacts.Count==facts && p.TemporaryMana==2 && p.UsedMana==0,"foreign PayPhase rejected without payment");
        Generic.SpendMana(p,other,other.Cost);
        Check(q.Payments().Last().ControllerId==p.Id && q.Payments().Last().ActionKind==HistoryPaymentKind.Other && q.Payments().Last().Mana==2 && p.TemporaryMana==0 && p.UsedMana==0
            && q.Plays().Count()==plays,"Other/temporary mana retained without play");
    }
    private static void Combat(bool history)
    {
        var g=HistoryFactsProof.Fixture(history);var p=g.Player1;var q=g.HistoryFacts.Query;var attacker=Summon(p,"CS2_182");
        int starts=0,finishes=0;
        g.TriggerManager.AttackTrigger += entity=>{Check(q.AttacksStarted().Last().AttackerId==entity.Id,"start visible before attack reaction");starts++;};
        g.TriggerManager.AfterAttackTrigger += _=>{Check(q.AttacksFinished().Last().ReachedCombat,"finish visible before after-attack reaction");finishes++;};
        Attack(g,attacker,p.Opponent.Hero);
        Check(q.AttacksStarted().Single().Origin==HistoryAttackOrigin.Player && q.FriendlyAttacksReachedCombat(p.Id)==1,"player lifetime attack");
        new AttackTask(EntityType.SOURCE,EntityType.OP_HERO).Process(g,p,attacker,null);
        Check(q.AttacksFinished().Last().Origin==HistoryAttackOrigin.Forced && q.FriendlyAttacksReachedCombat(p.Id)==2,"AttackTask forced attack records once");
        TriggerManager.TriggerHandler cancel=_=>attacker.ToBeDestroyed=true;
        g.TriggerManager.AttackTrigger+=cancel;
        new AttackTask(EntityType.SOURCE,EntityType.OP_HERO).Process(g,p,attacker,null);
        g.TriggerManager.AttackTrigger-=cancel;
        Check(q.AttacksFinished().Last().Cancelled && !q.AttacksFinished().Last().ReachedCombat && q.FriendlyAttacksReachedCombat(p.Id)==2
            && starts==3 && finishes==2,"cancelled accepted attack gets finish, no reached combat credit");
        var redirect=Summon(p,"CS2_182");var target=Summon(p.Opponent,"CS2_182");
        // Target-trigger redirect uses the engine's existing event metadata, never a new trigger/task.
        g.TriggerManager.TargetTrigger += _=>{
            var data=typeof(Game).GetProperty("CurrentEventData",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(g);
            data.GetType().GetProperty("EventTarget").SetValue(data,target);
        };
        new AttackTask(EntityType.SOURCE,EntityType.OP_HERO).Process(g,p,redirect,null);
        Check(q.AttacksFinished().Last().TargetId==target.Id,"finished target captures redirect");
    }
    private static void Residence(bool history)
    {
        var g=HistoryFactsProof.Fixture(history);var p=g.Player1;var q=g.HistoryFacts.Query;
        TriggerManager.TriggerHandler draw=entity=>Check(q.Residences(entity.Id,Zone.HAND).Last().ExitSequence==null
            && q.Residences(entity.Id,Zone.DECK).Last().ExitSequence.HasValue,"draw residence visible before draw reaction");
        TriggerManager.TriggerHandler shuffle=entity=>Check(q.Residences(entity.Id,Zone.DECK).Last().ExitSequence==null,"deck residence visible before shuffle reaction");
        g.TriggerManager.DrawTrigger+=draw;g.TriggerManager.ShuffleIntoDeckTrigger+=shuffle;
        var entity=p.DeckZone.TopCard;Generic.Draw(p,entity);
        var deck=q.Residences(entity.Id,Zone.DECK).Single();var first=q.Residences(entity.Id,Zone.HAND).Single();
        Check(deck.ExitSequence>deck.EntrySequence && first.ExitSequence==null && q.Residence(entity.Id,Zone.HAND).Contains(first.EntrySequence),"draw closes deck and opens hand residence");
        PlayEntity(g,p,entity);var played=(Minion)entity;
        Generic.ReturnToHandBlock(p,played);
        var second=q.Residences(entity.Id,Zone.HAND).Last();
        Check(q.Residences(entity.Id,Zone.HAND).Count()==2 && first.EntrySequence<second.EntrySequence
            && q.Residences(entity.Id,Zone.HAND).First().ExitSequence.HasValue,"bounce opens new residence");
        Generic.ShuffleIntoDeck(p,p.Hero,p.HandZone.Remove(entity));
        Check(q.Residences(entity.Id,Zone.DECK).Count()==2 && q.Residences(entity.Id,Zone.HAND).Last().ExitSequence.HasValue,"shuffle deck re-entry");
        Play(g,p,"CS2_120");Check(q.TwoManaPlaysWhileInHandOrDeck(entity.Id,p.Id)==1,"deck residence includes two-mana play");
        p.DeckZone.Remove(entity);entity.Controller=p.Opponent;entity[GameTag.CONTROLLER]=p.Opponent.PlayerId;p.Opponent.HandZone.Add(entity);
        Check(q.Residences(entity.Id,Zone.HAND).Last().ControllerId==p.Opponent.Id && q.ManaSpentWhileHolding(entity.Id,p.Id)==0,
            "control transfer closes old residence and opens new owner, no foreign holding credit");
        g.TriggerManager.DrawTrigger-=draw;g.TriggerManager.ShuffleIntoDeckTrigger-=shuffle;
        var bytes=g.SaveSnapshot();var loaded=Game.LoadSnapshot(bytes);
        Check(bytes.SequenceEqual(loaded.SaveSnapshot()) && loaded.HistoryFacts.CanonicalText()==g.HistoryFacts.CanonicalText(),"residence snapshots stable");
        var deckMember=p.DeckZone.TopCard;
        SpecificTask.SwapDecks.Process(g,p,p.Hero,null);
        var transfers=q.Residences(deckMember.Id,Zone.DECK).ToArray();
        Check(transfers.Length==2 && transfers[0].ExitSequence.HasValue && transfers[1].ControllerId==p.Opponent.Id,
            "actual Togwaggle deck swap closes and reopens residence by owner");
        Check(Game.LoadSnapshot(g.SaveSnapshot()).HistoryFacts.CanonicalText()==g.HistoryFacts.CanonicalText(),"deck-swap residence snapshot");
    }
    private static void Voraxx()
    {
        var g=HistoryFactsProof.Fixture();var p=g.Player1;var voraxx=Summon(p,"UNG_843");
        Play(g,p,"CS2_004",voraxx);
        var casts=g.HistoryFacts.Query.Casts().Where(f=>f.Outcome==HistoryCastOutcome.Started).ToArray();
        Check(g.HistoryFacts.Query.Plays().Count()==1 && casts.Length==2 && casts.Select(f=>f.CasterKind).SequenceEqual(new[]{HistoryCastKind.Player,HistoryCastKind.Repeat}),"actual Voraxx one spell play/two casts");
        Check(g.HistoryFacts.Query.Casts().GroupBy(f=>f.CastId).All(group=>group.Select(f=>f.Outcome)
            .SequenceEqual(new[]{HistoryCastOutcome.Started,HistoryCastOutcome.Completed})),"Voraxx both invocations finish exactly once");
        Check(p.BoardZone.Single(m=>m.Card.Id=="UNG_999t2t1").Health==3,"Voraxx copied spell scheduling/body unchanged");
    }
    private static void ArmorSet()
    {
        var g=HistoryFactsProof.Fixture();var p=g.Player1;var q=g.HistoryFacts.Query;
        p.Hero.Armor=5;p.Hero.Armor=2;p.Hero[GameTag.ARMOR]=4;
        Check(q.ArmorGainedAmount(p.Id,q.ThisTurn)==7 && q.ArmorGains().Count()==2,"direct committed armor increases include legacy producers, losses excluded");
    }
    private static void ProducerAnnotations()
    {
        var g=HistoryFactsProof.Fixture();var p=g.Player1;
        bool rejected=false;
        try { Entity.FromCard(p,Cards.FromId("GAME_005")).ActivateTask(PowerActivation.POWER); }
        catch(InvalidOperationException e) { rejected=e.Message.Contains("producer annotation"); }
        Check(rejected,"unclassified executable spell activation fails closed");
        var spell=Entity.FromCard(p,Cards.FromId("GAME_005"));
        rejected=false;
        try { new ActivatePowerTask(EntityType.TARGET).Process(g,p,p.Hero,spell); }
        catch(InvalidOperationException e) { rejected=e.Message.Contains("producer annotation"); }
        Check(rejected,"unclassified ActivatePowerTask spell producer fails closed");
        rejected=false;
        try { new EnqueuePendingTask(EntityType.HERO).Process(g,p,p.Hero,spell); }
        catch(InvalidOperationException e) { rejected=e.Message.Contains("producer annotation"); }
        Check(rejected,"unclassified pending spell-body producer fails closed");
        var choice=Add(p,"NEW1_008");
        Check(g.Process(PlayCardTask.Any(p,choice,chooseOne:1)) && !g.HistoryFacts.Query.Casts().Any(),"minion Choose One spell-shaped option resolves without cast");
        new AdaptTask(EntityType.SOURCE).Process(g,p,choice,null);
        Check(p.Choice!=null && Generic.ChoicePick(p,g,p.Choice.Choices.First(id=>g.IdEntityDic[id].Card.Id!="UNG_999t2")) && !g.HistoryFacts.Query.Casts().Any(),"actual Adapt spell-shaped option is not a cast");
        Kill(g,(Minion)choice);p.UsedMana=0;
        Summon(p.Opponent,"CS2_182");Summon(p.Opponent,"CS2_182");
        Play(g,p,"ICC_054");
        Check(g.HistoryFacts.Query.Casts().Where(f=>f.Outcome==HistoryCastOutcome.Started).Select(f=>f.CasterKind)
            .SequenceEqual(new[]{HistoryCastKind.Player,HistoryCastKind.Repeat}),"actual recursive Spreading Plague records Repeat once");
    }
    internal static Game Populated(bool history=false)
    {
        var g=HistoryFactsProof.Fixture(history);var p=g.Player1;
        Entity.FromCard(p,HistoryCanonicalProof.Consumer());
        p.Hero.GainArmor(p.Hero,3);p.Hero.TakeDamage(p.Opponent.Hero,4);p.Hero.TakeHeal(p.Hero,1);
        Play(g,p,"CS2_120");var a=Summon(p,"CS2_182");Attack(g,a,p.Opponent.Hero);
        return g;
    }
    private static void Persistence()
    {
        var g=Populated();string text=g.HistoryFacts.CanonicalText();var bytes=g.SaveSnapshot();
        foreach(var copy in new[]{g.CloneExact(),g.CloneForSimulation(17),Game.LoadSnapshot(bytes)})
        {
            Check(copy.HistoryFacts.CanonicalText()==text && Canonical.State(copy)==Canonical.State(g),"new fields survive clones/snapshot/canonical");
            copy.Player1.Hero.TakeDamage(copy.Player2.Hero,1);
            Check(g.HistoryFacts.CanonicalText()==text,"no mutable tail alias");
        }
        foreach(var type in new[]{typeof(DamageResolvedHistoryFact),typeof(HeroHealthChangedFact),typeof(ArmorGainedFact),typeof(AttackHistoryFact),typeof(AttackFinishedFact),typeof(ManaSpentFact),typeof(ResidenceChangedFact)})
        {
            var detached=Game.LoadSnapshot(bytes);var fact=detached.HistoryFacts.Records.First(type.IsInstanceOfType);
            string canonical=Canonical.State(detached);
            foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
            {
                var old=field.GetValue(fact);object changed=old is bool flag?!flag:old is Enum?Enum.ToObject(field.FieldType,Convert.ToInt32(old)+1):old is long number?number+1:(object)(Convert.ToInt32(old)+1);
                field.SetValue(fact,changed);
                Check(Canonical.State(detached)!=canonical,"canonical includes "+type.Name+"."+field.Name);
                field.SetValue(fact,old);
            }
        }
        var projected=g.CloneExact();projected.ProjectHistoryForFairView(g.Player1.PlayerId);
        Check(projected.HistoryFacts.Query.Damage().Count()==g.HistoryFacts.Query.Damage().Count(),"public damage preserved");
        bool rejected=false;try{projected.HistoryFacts.Query.Residence(g.Player2.DeckZone[0].Id,Zone.DECK);}catch(NotSupportedException){rejected=true;}
        Check(rejected,"private residence queries fail closed");
        var hiddenHeroGame=HistoryFactsProof.Fixture();var owner=hiddenHeroGame.Player1;var former=owner.Hero;
        former.TakeDamage(owner.Opponent.Hero,1);
        var replacement=(Hero)Entity.FromCard(owner,Cards.FromId("HERO_08"));replacement.HeroPower=former.HeroPower;owner.Hero=replacement;
        owner.DeckZone.Add(former);hiddenHeroGame.ProjectHistoryForFairView(owner.PlayerId);
        Check(hiddenHeroGame.HistoryFacts.Query.HealthChanges().First().HeroId==0,"former hero hidden in deck loses private historical entity link");
        void Reject<T>(string field,object value) where T:HistoryFact
        {
            var bad=Game.LoadSnapshot(bytes);var fact=bad.HistoryFacts.Records.OfType<T>().First();
            typeof(T).GetField("<"+field+">k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(fact,value);
            bool failed=false;try{Game.LoadSnapshot(bad.SaveSnapshot());}catch(SerializationException){failed=true;}
            Check(failed,"invalid "+typeof(T).Name+"."+field+" rejected");
        }
        Reject<DamageResolvedHistoryFact>("HealthDamage",-1);
        Reject<DamageResolvedHistoryFact>("PreHitHealth",999);
        Reject<ManaSpentFact>("RelatedPlayId",999L);
        Reject<ManaSpentFact>("Mana",-1);
        Reject<HeroHealthChangedFact>("Reason",(HistoryHealthChangeReason)999);
        Reject<ArmorGainedFact>("Amount",0);
        Reject<ResidenceChangedFact>("Zone",Zone.PLAY);
        foreach(var family in new[]{"ManaSpentFact","DamageResolvedHistoryFact","AttackFinishedFact","ResidenceChangedFact","HeroHealthChangedFact","ArmorGainedFact"})
        {
            var bad=Game.LoadSnapshot(bytes);var fact=bad.HistoryFacts.Records.First(f=>f.GetType().Name==family);
            typeof(HistoryFact).GetField("<ControllerId>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(fact,999999);
            rejected=false;try{Game.LoadSnapshot(bad.SaveSnapshot());}catch(SerializationException){rejected=true;}
            Check(rejected,"snapshot validates "+family);
        }
    }
}
