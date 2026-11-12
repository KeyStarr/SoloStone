using System.Diagnostics;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class HistoryResolutionReviewProof
{
    private static void Check(bool value,string message)=>Core2026Proof.Check(value,"history review: "+message);
    internal static void Run()
    {
        var failures=new List<Exception>();
        foreach(var proof in new Action[]{FairConsumers,RemainingMana,HealthEnchantments,QueryBound,SpellDamage})
            try { proof(); } catch(Exception e) { Console.WriteLine("FAIL history review: "+proof.Method.Name+": "+e.Message);failures.Add(e); }
        if(failures.Count!=0) throw new AggregateException(failures);
        Console.WriteLine("PASS history review: owner-visible FairBot consumers; Mossbinding Other debit; Health enchant apply/remove; bounded COW queries; active spell attribution and exact damage");
    }
    private static readonly FieldInfo FairGame=typeof(FairBotState).GetField("game",BindingFlags.Instance|BindingFlags.NonPublic);
    private static void FairConsumers()
    {
        var g=HistoryFactsProof.Fixture(false);var p=g.Player1;
        var brood=(Playable)Add(p,"CS2_182");var lotus=(Playable)Entity.FromCard(p,Cards.FromId("CS2_182"));p.DeckZone.Add(lotus);
        new AdaptiveCostEffect(c=>c.Game.HistoryFacts.Query.ManaSpentWhileHolding(c.Id,c.Controller.Id)).Activate(brood);
        Play(g,p,"CS2_120");
        Generic.Draw(p,lotus);Add(p.Opponent,"CS2_029");
        new AdaptiveCostEffect(c=>c.Game.HistoryFacts.Query.TwoManaPlaysWhileInHandOrDeck(c.Id,c.Controller.Id),EffectOperator.ADD).Activate(lotus);
        var fair=FairBotState.Create(g,p.PlayerId,47,DeckStyle.Control);var copy=(Game)FairGame.GetValue(fair);
        Check(copy.IdEntityDic[brood.Id].Cost==2 && copy.IdEntityDic[lotus.Id].Cost==5,"FairBotState cost consumers retain own holding and hand/deck progress");
        _=fair.Evaluate();_=fair.Actions;
        var action=fair.Actions.Single(a=>a.Type==PlayerTaskType.PLAY_CARD && a.Source==brood.Id && a.Position==0);
        var child=fair.Apply(action);var future=(Game)FairGame.GetValue(child);var q=future.HistoryFacts.Query;
        Check(q.ManaSpentWhileHolding(brood.Id,p.Id)==2 && q.TwoManaPlaysWhileInHandOrDeck(brood.Id,p.Id)==1
            && q.TwoManaPlaysWhileInHandOrDeck(brood.Id,p.Id,false)==2,"projected future play retains residence, excludes own payment/play");
        Check(future.IdEntityDic[lotus.Id].Cost==6 && copy.IdEntityDic[lotus.Id].Cost==5 && g.IdEntityDic[lotus.Id].Cost==5,"fair search sibling/source progress isolated");
        foreach(int hidden in new[]{p.DeckZone[0].Id,p.Opponent.HandZone.FirstOrDefault()?.Id??p.Opponent.DeckZone[0].Id})
        {
            bool denied=false;try{_=copy.HistoryFacts.Query.Residence(hidden,Zone.DECK);}catch(NotSupportedException){denied=true;}
            Check(denied,"hidden residence links unavailable");
        }
    }
    private static void RemainingMana()
    {
        foreach(bool history in new[]{false,true})
        foreach(var (temporary,locked,baseMana) in new[]{(0,0,10),(3,1,10),(0,0,2)})
        {
            var g=HistoryFactsProof.Fixture(history);var p=g.Player1;var held=Add(p,"CS2_182");
            p.BaseMana=baseMana;p.TemporaryMana=temporary;p.OverloadLocked=locked;
            int available=p.RemainingMana,total=p.TotalManaSpentThisGame;
            var moss=Add(p,"CATA_135");Check(g.Process(PlayCardTask.Any(p,moss)),"Mossbinding play accepted");var q=g.HistoryFacts.Query;var payments=q.Payments().ToArray();
            Check(payments.Length==2 && payments[0].ActionKind==HistoryPaymentKind.Play && payments[0].Mana==2
                && payments[1].ActionKind==HistoryPaymentKind.Other && payments[1].Mana==available-2
                && payments[1].RelatedPlayId==0 && payments[1].EntityId==payments[0].EntityId,
                "Mossbinding remaining mana recorded once as Other, separate from play");
            Check(q.ManaSpentWhileHolding(held.Id,p.Id)==available && p.RemainingMana==0 && p.TemporaryMana==0
                && p.TotalManaSpentThisGame==total+available,"Mossbinding full actual debit includes temporary mana, excludes locked crystals");
        }
    }
    private static void HealthEnchantments()
    {
        foreach(bool history in new[]{false,true}) HealthEnchantments(history);
    }
    private static void HealthEnchantments(bool history)
    {
        var g=HistoryFactsProof.Fixture(history);var p=g.Player1;var q=g.HistoryFacts.Query;p.Hero.Damage=5;
        int count=q.HealthChanges().Count();
        // Health_N uses GenericEffect's ref write, unlike the public Health setter.
        var effects=typeof(Enchant).Assembly.GetType("SabberStoneCore.Enchants.Effects");
        var health=(IEffect)effects.GetMethod("Health_N",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{3});
        health.ApplyTo(p.Hero);
        var added=q.HealthChanges().Skip(count).ToArray();
        Check(added.Length==1 && added[0].OldHealth==25 && added[0].NewHealth==28 && added[0].Reason==HistoryHealthChangeReason.Set,
            "Health_N apply publishes exactly one committed current hero Health change");
        count=q.HealthChanges().Count();health.RemoveFrom(p.Hero);
        var removed=q.HealthChanges().Skip(count).ToArray();
        Check(removed.Length==1 && removed[0].OldHealth==28 && removed[0].NewHealth==25 && removed[0].Reason==HistoryHealthChangeReason.Set,
            "Health_N removal publishes exactly one committed current hero Health change");
        count=q.HealthChanges().Count();var minion=Summon(p,"CS2_182");health.ApplyTo(minion);health.RemoveFrom(minion);
        var detached=(Hero)Entity.FromCard(p,Cards.FromId("HERO_08"));health.ApplyTo(detached);health.RemoveFrom(detached);
        Check(q.HealthChanges().Count()==count,"Health enchantments ignore minions and non-current heroes");
        var zero=health.ChangeValue(0);zero.ApplyTo(p.Hero);zero.RemoveFrom(p.Hero);
        Check(q.HealthChanges().Count()==count,"zero Health enchantment has no change fact");
    }
    private static void QueryBound()
    {
        var g=HistoryFactsProof.Fixture(false);var p=g.Player1;var held=Add(p,"CS2_182");var lotus=Add(p,"CS2_182");var deck=p.DeckZone[0];
        var debit=Entity.FromCard(p,Cards.FromId("CS2_120"));
        while(g.HistoryFacts.Count<10000) {p.UsedMana=0;Generic.PayPhase(p,debit);}
        int mana=g.HistoryFacts.Query.Payments().Sum(f=>f.Mana);
        Play(g,p,"CS2_120");Play(g,p,"CS2_120");
        long secondPlay=g.HistoryFacts.Query.Plays().Last().PlayId;
        var beforePlay=g.HistoryFacts.Query.BeforeThisPlay(secondPlay);
        Check(beforePlay.ManaSpentWhileHolding(held.Id,p.Id)==mana+4 && beforePlay.TwoManaPlaysWhileInHandOrDeck(deck.Id,p.Id)==1,
            "indexed publication cursor includes payment, excludes uncommitted linked play");
        var q=g.HistoryFacts.Query;long before=q.EntriesExamined;
        for(int i=0;i<100;i++)
            Check(q.ManaSpentWhileHolding(held.Id,p.Id)==mana+4 && q.TwoManaPlaysWhileInHandOrDeck(deck.Id,p.Id)==2,"10,000-event literal progress");
        Check(q.EntriesExamined-before<=800,"100 holding/Lotus query pairs inspect <=800 index entries over 10,000 events; got "+(q.EntriesExamined-before));
        var bytes=g.SaveSnapshot();var rootText=g.HistoryFacts.CanonicalText();
        foreach(var copy in new[]{g.Clone(),g.CloneExact(),g.CloneForSimulation(31),Game.LoadSnapshot(bytes)})
        {
            Check(copy.HistoryFacts.Query.ManaSpentWhileHolding(held.Id,p.Id)==mana+4 && copy.HistoryFacts.Query.TwoManaPlaysWhileInHandOrDeck(deck.Id,p.Id)==2,"query indexes survive clone/snapshot");
            var own=copy.Player1;own.UsedMana=0;Generic.PayPhase(own,copy.IdEntityDic[debit.Id]);
            Check(copy.HistoryFacts.Query.ManaSpentWhileHolding(held.Id,p.Id)==mana+6 && g.HistoryFacts.CanonicalText()==rootText
                && g.HistoryFacts.Query.ManaSpentWhileHolding(held.Id,p.Id)==mana+4,"copy-on-write payment prefix never mutates source");
            Play(copy,own,"CS2_120");
            Check(copy.HistoryFacts.Query.ManaSpentWhileHolding(held.Id,p.Id)==mana+8 && copy.HistoryFacts.Query.TwoManaPlaysWhileInHandOrDeck(deck.Id,p.Id)==3
                && g.HistoryFacts.Query.TwoManaPlaysWhileInHandOrDeck(deck.Id,p.Id)==2,"copy-on-write residence accumulators never mutate source or siblings");
        }
        new AdaptiveCostEffect(c=>Math.Min(8,c.Game.HistoryFacts.Query.ManaSpentWhileHolding(c.Id,c.Controller.Id))).Activate((Playable)held);
        new AdaptiveCostEffect(c=>c.Game.HistoryFacts.Query.TwoManaPlaysWhileInHandOrDeck(c.Id,c.Controller.Id),EffectOperator.ADD).Activate((Playable)lotus);
        var timer=Stopwatch.StartNew();var fair=FairBotState.Create(g,p.PlayerId,11,DeckStyle.Control);
        foreach(var action in fair.Actions.Take(12)) {var child=fair.Apply(action);_=child.Evaluate();}
        Console.WriteLine(FormattableString.Invariant($"PASS history query bound: events={g.HistoryFacts.Count}; queries=200; entries={q.EntriesExamined-before}; FairBot search wall={timer.Elapsed.TotalMilliseconds:F3}ms (informational)"));
    }
    private static void SpellDamage()
    {
        var g=HistoryFactsProof.Fixture(false);var p=g.Player1;var enemy=p.Opponent;var q=g.HistoryFacts.Query;
        var body=Summon(p,"CS2_231");bool nested=false;
        g.TriggerManager.DamageTrigger += victim=>{
            if(nested || victim!=enemy.Hero) return;
            nested=true;
            Check(q.Casts().Last().Outcome==HistoryCastOutcome.Started,"nested minion damage occurs inside active spell invocation");
            enemy.Hero.TakeDamage(body,1);
        };
        var fireball=(Spell)Entity.FromCard(p,Cards.FromId("CS2_029"));Generic.CastSpell(p,g,fireball,enemy.Hero,0);
        var hits=q.Damage().ToArray();
        Check(hits.Length==2 && hits[0].SourceId==fireball.Id && hits[0].Amount==6 && hits[0].CastId>0
            && hits[1].SourceId==body.Id && hits[1].SourceKind==CardType.MINION && hits[1].Amount==1 && hits[1].CastId==0
            && q.SpellDamageAmount(p.Id,q.ThisTurn)==6,"active spell never credits nested minion damage");
        var aoe=HistoryFactsProof.Fixture(false);var ap=aoe.Player1;Summon(ap.Opponent,"CS2_182");Summon(ap.Opponent,"CS2_182");
        var explosion=(Spell)Entity.FromCard(ap,Cards.FromId("CS2_025"));Generic.CastSpell(ap,aoe,explosion,null,0);
        hits=aoe.HistoryFacts.Query.Damage().ToArray();
        Check(hits.Length==2 && hits.All(d=>d.SourceId==explosion.Id && d.Amount==1 && d.HealthDamage==1 && d.ArmorDamage==0 && d.CastId>0)
            && aoe.HistoryFacts.Query.SpellDamageAmount(ap.Id,aoe.HistoryFacts.Query.ThisTurn)==2,"AOE: one fact per victim, exact sum two");
        var missiles=HistoryFactsProof.Fixture(false);var mp=missiles.Player1;
        var barrage=(Spell)Entity.FromCard(mp,Cards.FromId("EX1_277"));Generic.CastSpell(mp,missiles,barrage,null,0);
        hits=missiles.HistoryFacts.Query.Damage().ToArray();
        Check(hits.Length==3 && hits.All(d=>d.SourceId==barrage.Id && d.VictimId==mp.Opponent.Hero.Id && d.Amount==1 && d.CastId>0)
            && missiles.HistoryFacts.Query.SpellDamageAmount(mp.Id,missiles.HistoryFacts.Query.ThisTurn)==3,"missiles: three separate one-damage facts");
        var life=HistoryFactsProof.Fixture(false);var lp=life.Player1;lp.Hero.Damage=10;var tiny=Summon(lp.Opponent,"CS2_231");
        var drain=(Spell)Entity.FromCard(lp,Cards.FromId("CORE_BT_801"));Generic.CastSpell(lp,life,drain,tiny,0);
        hits=life.HistoryFacts.Query.Damage().ToArray();
        var healing=life.HistoryFacts.Query.HealthChanges().Where(h=>h.Reason==HistoryHealthChangeReason.Heal).ToArray();
        Check(hits.Length==1 && hits[0].SourceId==drain.Id && hits[0].CastId>0 && hits[0].Amount==3 && hits[0].PreHitHealth==1 && hits[0].HealthDamage==3 && hits[0].ArmorDamage==0
            && lp.Hero.Health==23 && healing.Length==1 && healing[0].OldHealth==20 && healing[0].NewHealth==23,
            "lifesteal/overkill: one three-damage fact against one Health; one three-Health heal");
    }
}
