using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B14H2CostcastCardsProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, message);
    [GroupProof("b14-h2-costcast-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("EDR_844", Naralex);
        StandardSetsProof.Case("TIME_030", Unpinned_Divergence);
        StandardSetsProof.Case("TIME_036", Informant);
        StandardSetsProof.Case("TIME_039", Unpinned_DejaVuDistinctDefinitionWeights);
        StandardSetsProof.Case("BE_302", Unpinned_FlameShockSingleEnemyLegality);
        StandardSetsProof.Case("EDR_846", Unpinned_Shaladrassil);
        StandardSetsProof.Case("CORE_LOOT_373", Unpinned_HealingRainModifierBudget);
        StandardSetsProof.Case("TLC_106", Unpinned_UmbraFiveDeathSelection);
        StandardSetsProof.Case("CATA_492", Shrine);
    }
    internal static void Historyless()
    {
        foreach(var scenario in new (string Id, Action<Game,Controller,string> Run)[]{
            ("EDR_844",Naralex),("TIME_030",Unpinned_Divergence),("TIME_036",Informant),
            ("TIME_039",Unpinned_DejaVuDistinctDefinitionWeights),("BE_302",Unpinned_FlameShockSingleEnemyLegality),
            ("EDR_846",Unpinned_Shaladrassil),("CORE_LOOT_373",Unpinned_HealingRainModifierBudget),
            ("TLC_106",Unpinned_UmbraFiveDeathSelection),("CATA_492",Shrine)})
        {
            var g=HeroDivineShieldProof.NewGame(false);
            foreach(var p in new[]{g.Player1,g.Player2})foreach(var c in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(c));
            scenario.Run(g,g.Player1,scenario.Id);
        }
    }
    private static void Resume(Game g,Action<Game> action)
    {
        if(g.History) { PoolPilotProof.Resume(g,action); return; }
        var clone=g.CloneExact(false);var loaded=Game.LoadSnapshot(g.SaveSnapshot());
        foreach(var branch in new[]{g,clone,loaded})action(branch);
        Check(Canonical.State(g)==Canonical.State(clone) && Canonical.State(g)==Canonical.State(loaded),"historyless clone/snapshot continuation");
    }
    private static void Answer(Game g, Func<IPlayable,bool> choose = null)
    {
        var d = g.GetPendingDecisions().Single();
        int id = d.Options.First(o => choose == null || choose(g.IdEntityDic[o.OptionId])).OptionId;
        Check(g.TryAnswer(new DecisionAnswer(d.Id,d.ControllerId,new[]{id}),out var error),"answer: "+error);
    }
    private static void Naralex(Game g, Controller p, string id)
    {
        var source = Summon(p,id); var dragon = Add(p,"EX1_043"); var other = Add(p,"EX1_043");
        var enemy = Add(p.Opponent,"EX1_043"); g.AuraUpdate();
        Check(dragon.Cost == 1 && other.Cost == 1 && enemy.Cost == enemy.Card.Cost,"Naralex first Dragon, friendly hand only");
        Resume(g,next => {
            var owner=next.Player1; PlayEntity(next,owner,next.IdEntityDic[dragon.Id]);
            Check(next.IdEntityDic[other.Id].Cost==other.Card.Cost,"one Dragon consumes all Naralex copies this turn");
        });
        End(g); End(g); g.AuraUpdate(); Check(other.Cost==1,"new turn renews first Dragon");
        source.Silence();g.AuraUpdate();Check(other.Cost==other.Card.Cost,"Silence removes source-bound discount");
        var later=Summon(p,id);g.AuraUpdate();Check(other.Cost==1,"new active source grants current first Dragon");
        Kill(g,later);Check(other.Cost==other.Card.Cost,"leave-play removes discount");
    }
    private static void Unpinned_Divergence(Game g, Controller p, string id)
    {
        foreach(var fixture in new[]{(Seed:1,Cost:3,Attack:2,Health:2),(Seed:3,Cost:2,Attack:3,Health:2)})
        {
            foreach(var c in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(c));
            var source=(Minion)Add(p,"CS2_182"); Effects.Attack_N(1).ApplyTo(source); Effects.SetCost(5).ApplyTo(source);
            source.Damage=2; int slot=source.ZonePosition;
            g.SetRandomSeed(fixture.Seed);
            var expectedRng=g.Random.Clone();
            expectedRng.Next(1); // One candidate draw, then independent Cost, Attack and Health rounding draws.
            for(int i=0;i<3;i++)expectedRng.Next(2);
            Resume(g,next=> {
                Play(next,next.Player1,id);
                var halves=next.Player1.HandZone.OfType<Minion>().ToArray();
                var first=(Minion)next.IdEntityDic[source.Id];var second=halves.Single(c=>c.Id!=source.Id);
                Check(halves.Length==2 && first.ZonePosition==slot,"first half keeps entity and slot");
                Check((first.Cost,first.AttackDamage,first.BaseHealth)==(fixture.Cost,fixture.Attack,fixture.Health)
                    && (second.Cost,second.AttackDamage,second.BaseHealth)==(5-fixture.Cost,5-fixture.Attack,5-fixture.Health)
                    && halves.All(c=>c.Damage==0),"exact seeded independent odd halves and reset damage: "+fixture.Seed);
                Check(next.Random.CaptureState().Equals(expectedRng.CaptureState()),"split consumes exactly four RNG draws");
                Check(next.Random.Next(1000000)==expectedRng.Clone().Next(1000000),"split preserves next random outcome");
                var copy=Generic.Copy(next.Player2,next.Player1.Hero,first,Zone.HAND); next.AuraUpdate();
                Check(copy.Cost==first.Cost && ((Minion)copy).BaseHealth==first.BaseHealth,"copy retains half without splitting again");
                first.Silence();Check(first.AttackDamage==4 && first.BaseHealth==5,"Silence clears split stats");
            });
        }
        foreach(var c in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(c));
        Summon(p,"EX1_315");var one=(Minion)Add(p,"CS2_231");one.BaseHealth=1;CostRules.SetCostThisTurn(one,0);g.AuraUpdate();
        Play(g,p,id);Check(p.HandZone.Count==2 && p.HandZone.All(c=>c.Cost==0 && ((Minion)c).BaseHealth==1),"zero effective cost, overlay, Portal and one Health");
        CostRules.ExpireTurn(g,g.Turn);g.AuraUpdate();
        Check(p.HandZone.All(c=>c.Cost>=0),"existing cost overlay expires normally");
    }
    private static void Informant(Game g, Controller p, string id)
    {
        Summon(p.Opponent,"EX1_315");Add(p.Opponent,"CS2_231");var original=Add(p.Opponent,"CS2_182");g.AuraUpdate();
        Play(g,p,id);Check(p.Choice!=null,"rightmost private branch choice");
        Resume(g,next=> {
            Answer(next,c=>c.Card.Id==original.Card.Id);next.AuraUpdate();
            Check(next.Player1.HandZone.Single().Cost==4 && next.IdEntityDic[original.Id].Cost==2,"copied state recalculates recipient auras");
        });
        Play(g,p,id);Answer(g,c=>c.Card.Id=="TIME_036t");g.AuraUpdate();
        Check(original.Cost==4 && p.Opponent.HandZone.Count==2,"tax +2 attaches to original without moving it");
    }
    private static void Unpinned_DejaVuDistinctDefinitionWeights(Game g, Controller p, string id)
    {
        Summon(p.Opponent,"EX1_315");
        for(int i=0;i<4;i++)Add(p.Opponent,"CS2_182");
        var buffed=(Minion)Add(p.Opponent,"CS2_231");Effects.Attack_N(3).ApplyTo(buffed);g.AuraUpdate();
        Play(g,p,id);Check(p.Choice.Choices.Count==2 && p.Choice.Choices.Select(x=>g.IdEntityDic[x].Card.Id).Distinct().Count()==2,"duplicate definitions take only one offer slot");
        var preview=(Minion)g.IdEntityDic[p.Choice.Choices.Single(x=>g.IdEntityDic[x].Card.Id=="CS2_231")];
        Check(preview.AttackDamage==4,"private Discover preview retains representative hand buffs");
        Resume(g,next=> {
            Answer(next,c=>c.Card.Id=="CS2_231");next.AuraUpdate();
            var copy=(Minion)next.Player1.HandZone.Single();Check(copy.AttackDamage==4 && copy.Cost==0 && next.Player2.HandZone.Count==5,"saved representative buff and destination cost");
            Check(next.HistoryFacts.Query.Discoveries().Count()==1,"Deja Vu records exactly one Discover completion");
        });
    }
    private static void Unpinned_FlameShockSingleEnemyLegality(Game g, Controller p, string id)
    {
        Play(g,p,id,p.Opponent.Hero);
        Check(p.Opponent.Hero.Damage==2 && p.OverloadOwed==1 && p.Choice==null,"single enemy hit once, Overload once");
        var victim=Summon(p.Opponent,"EX1_556");victim.Damage=victim.BaseHealth-2;
        Play(g,p,id,victim);
        Check(victim.Zone==p.Opponent.GraveyardZone && p.Choice!=null,"first hit settles Deathrattle before second choice");
        Check(p.Choice.Choices.Any(x=>g.IdEntityDic[x].Card.Id=="skele21"),"second offer includes new Deathrattle summon");
        Answer(g,c=>c.Card.Id=="skele21");Check(p.OverloadOwed==2,"two hits still one Overload");
        StandardSetsProof.ParkBoard(p.Opponent);
        var fireImmune=Summon(p.Opponent,"FIR_959"); // Fyrakk is immune to Fire spells by identity (9ff3bf5); no tag is set
        Play(g,p,id,p.Opponent.Hero);
        Check(p.Choice==null && fireImmune.Damage==0,"second target uses complete Fire spell targeting rules");
        StandardSetsProof.ParkBoard(p.Opponent);p.Opponent.Hero.IsImmune=true;
        var unplayable=Add(p,id);Check(!p.Options().Any(t=>t.Source?.Id==unplayable.Id),"zero legal enemies cannot hand play");
        int owed=p.OverloadOwed;var auto=(Spell)Entity.FromCard(p,Cards.FromId(id),zone:p.SetasideZone);
        Generic.CastSpell(p,g,auto,null,0);Check(p.OverloadOwed==owed+1,"indirect zero-target completion still Overloads");
    }
    private static void Unpinned_Shaladrassil(Game g, Controller p, string id)
    {
        var held=Add(p,id);Effects.SetCost(2).ApplyTo(held);var costly=Add(p,"CS2_182");
        PlayEntity(g,p,costly);Effects.SetCost(9).ApplyTo(held);
        var copy=Generic.Copy(p,p.Hero,held,Zone.HAND);
        Check(CostRules.QualifiedHeldDreams(copy),"same-hand stateful copy inherits latch before either cast");
        Resume(g,next=> {
            PlayEntity(next,next.Player1,next.IdEntityDic[held.Id]);
            Check(next.Player1.HandZone.Count(c=>c.Card.Id.StartsWith("EDR_846t",StringComparison.Ordinal))==5,"payment-time latch survives later cost increase");
        });
        PlayEntity(g,p,copy);
        Check(p.HandZone.Count(c=>c.Card.Id.StartsWith("EDR_846t",StringComparison.Ordinal))==10,"stateful same-hand copy inherits latch");
        foreach(var c in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(c));
        Play(g,p,id);Check(p.HandZone.Select(c=>c.Card.Id).Order().SequenceEqual(new[]{"DREAM_01","DREAM_02","DREAM_03","DREAM_04","DREAM_05"}),"fresh cast starts unqualified, all five exact Dream definitions");
        Check(p.HandZone.Single(c=>c.Card.Id=="DREAM_01").Card.Cost==2,"Dream token uses pinned current snapshot, preserving legacy lookup");
        Unpinned_ShaladrassilRepeatQualification(g,p,id);
    }
    private static void Unpinned_ShaladrassilRepeatQualification(Game g, Controller p, string id)
    {
        foreach(string repeat in new[]{"Niri","EDR_464e2","BE_337e2","JAIL_123e","BE_801e"})
        {
            foreach(var c in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(c));
            var held=Add(p,id);Effects.SetCost(1).ApplyTo(held);
            Play(g,p,"CS2_182");
            Check(CostRules.QualifiedHeldDreams(held),"repeat fixture qualifies held Shaladrassil");
            Minion niri=null;
            if(repeat=="Niri")niri=Summon(p,"TLC_836");
            else if(repeat=="EDR_464e2" || repeat=="BE_337e2")RepeatRules.Grant(p,p.Hero,repeat,schools:repeat=="BE_337e2"?254:0);
            else RepeatRules.Mark(held,p.Hero,repeat);
            Resume(g,next=> {
                PlayEntity(next,next.Player1,next.IdEntityDic[held.Id]);
                var dreams=next.Player1.HandZone.Select(c=>c.Card.Id).Order().ToArray();
                Check(dreams.SequenceEqual(new[]{"DREAM_01","DREAM_02","DREAM_03","DREAM_04","DREAM_05",
                    "EDR_846t1","EDR_846t2","EDR_846t3","EDR_846t4","EDR_846t5"}),
                    "effect replay starts unqualified: five corrupted plus five normal Dreams under "+repeat);
            });
            if(niri!=null)niri.Silence();
        }
    }
    private static void Unpinned_HealingRainModifierBudget(Game g, Controller p, string id)
    {
        p.Hero.BaseHealth=100;p.Hero.Damage=60;
        Enchantment.GetInstance(p,p.Hero,p,Cards.FromId("CATA_216e"));
        Summon(p,"EX1_350");int before=p.Hero.Damage;
        Play(g,p,id);Check(p.Hero.Damage==before-28,"Cleric and Velen scale total once, not each allocation");
        p.Hero.Damage=1;Play(g,p,id);Check(p.Hero.Damage==0,"stop after full healing");
    }
    private static void Unpinned_UmbraFiveDeathSelection(Game g, Controller p, string id)
    {
        for(int i=0;i<6;i++)Kill(g,Summon(p,"EX1_029"));
        int before=p.Opponent.Hero.Damage;p.Opponent.Hero.BaseHealth=100;
        p[GameTag.EXTRA_DEATHRATTLES_BASE]=1;
        Resume(g,next=> {
            Play(next,next.Player1,id);
            Check(next.Player2.Hero.Damage==before+20,"five death-occurrence tickets, each doubled");
        });
    }
    private static void Shrine(Game g, Controller p, string id)
    {
        p[HeraldRules.Profile]=(int)CardClass.WARLOCK;
        var left=Summon(p,"CS2_182");var location=Add(p,id);p.UsedMana=0;
        Check(g.Process(PlayCardTask.Any(p,location,zonePosition:1)),"Shrine placement");var right=Summon(p,"CS2_182");
        int deck=p.DeckZone.Count;
        Resume(g,next=> {
            var owner=next.Player1;var action=owner.Options().Single(t=>t.PlayerTaskType==PlayerTaskType.ACTIVATE_LOCATION && t.Source?.Id==location.Id);
            Check(next.Process(action),"Shrine activation");
            Check(owner.BoardZone.GetSlot(2).Card.Id=="CATA_725t" && owner.BoardZone.Slots.Last().Id==right.Id && owner.DeckZone.Count==deck-1,"Herald immediately right, then draw one");
        });
    }
}
