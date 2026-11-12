using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class EvHooksEdgeProof
{
    internal static void Run()
    {
        var errors = new List<string>();
        foreach (var proof in new Action[] { Unpinned_Startup, Unpinned_EssenceOrderAndCapacity, Unpinned_SidequestState,
            Unpinned_WatfinMutations, Unpinned_StarStateAndRandom, Unpinned_LocationState }) {
            try { proof(); Console.WriteLine("PASS " + proof.Method.Name); }
            catch(Exception e) { errors.Add(proof.Method.Name + ": " + e); }
        }
        Check(errors.Count == 0, string.Join("\n", errors));
        Console.WriteLine("PASS EVENT Unpinned startup/Essences/sidequest/mutations/Star/location edges");
    }
    private static Game Fixture() => QuestLifecycleProof.Fixture();
    private static void Check(bool ok, string why) => Core2026Proof.Check(ok,why);
    private static Game[] Forks(Game g) => new[] { g.CloneExact(history:true), Game.LoadSnapshot(g.SaveSnapshot()) };
    private static void Pick(Game g, int id=0) => EvHooksCardsProof.Pick(g,id);
    private static void Drain(Game g) => EvHooksCardsProof.Drain(g);
    private static void Rewind(Game g) => B15QuestClosureProof.RewindState(g);
    private static void Hidden(Game g, Controller owner, IEnumerable<int> ids)
    {
        var before = Canonical.State(g); var random = g.Random.CaptureState();
        string history = g.HistoryFacts.CanonicalText();
        var fair = FairBotState.Create(g,owner.Opponent.PlayerId,83,DeckStyle.Value);
        var view = (Game)typeof(FairBotState).GetField("game",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(fair);
        var identities=ids.Select(id=>g.IdEntityDic[id].Card.Id).ToHashSet();
        Check(!view.IdEntityDic.Values.Any(c=>c.Controller.PlayerId==owner.PlayerId
            && (c.Zone?.Type is Zone.HAND or Zone.DECK or Zone.SETASIDE) && identities.Contains(c.Card.Id)),
            "opponent fair/bot view hides generated and retired cards after canonical actor renumbering");
        Check(before==Canonical.State(g) && history==g.HistoryFacts.CanonicalText() && random.Equals(g.Random.CaptureState()),
            "fair projection preserves authoritative gameplay/history/RNG");
    }
    private static void Unpinned_Startup()
    {
        var original = Enumerable.Repeat("CS2_182",12).Append("CATA_EVENT_110").ToArray();
        var g = B15GameSetupProof.Opening(original, original,31);
        Check(g.IdEntityDic.Values.Count(e=>DragonSoulShattered.Essence(e.Card))==0,"Essences not created before mulligans");
        foreach (var next in Forks(g).Prepend(g)) {
            foreach (var p in new[]{next.Player1,next.Player2}) Check(next.Process(ChooseTask.Mulligan(p,new List<int>())),"mulligan accepted");
            next.NextStep=Step.MAIN_BEGIN;
            Check(next.IdEntityDic.Values.Count(e=>DragonSoulShattered.Essence(e.Card))==12,"one six-Essence split per original parent on both seats");
            foreach (var p in new[]{next.Player1,next.Player2}) {
                var essences=next.IdEntityDic.Values.Where(e=>e.Controller==p && DragonSoulShattered.Essence(e.Card)).ToArray();
                Check(essences.Select(e=>e.Card.Id).Distinct().Count()==6 && essences.All(e=>e.Zone==p.DeckZone || e.Zone==p.HandZone),"startup six distinct in deck before normal first draw");
                Hidden(next,p,essences.Where(e=>e.Zone==p.DeckZone).Select(e=>e.Id));
            }
        }
        var late=Fixture(); var source=Add(late.Player1,"CATA_EVENT_110");
        Generic.CastSpell(late.Player1,late,(Spell)source,null,0); Drain(late);
        Check(!late.IdEntityDic.Values.Any(e=>DragonSoulShattered.Essence(e.Card)),"generated late parent never retroactively splits");
        var side=B15GameSetupProof.Opening(Enumerable.Repeat("CS2_182",10).Append("TLC_EVENT_400").ToArray());
        Check(side.Player1.HandZone.All(e=>e.Card.Id!="TLC_EVENT_400"),"Sidequest receives no guaranteed opening draw");
    }
    private static void Unpinned_EssenceOrderAndCapacity()
    {
        foreach (bool full in new[]{false,true}) {
            var g=Fixture();var p=g.Player1;
            PoolPilotProof.ClassDeck(p);
            Entity.FromCard(p,Cards.FromId("CATA_EVENT_110t5"),zone:p.DeckZone);
            var left=Add(p,"CATA_EVENT_110t6"); var blue=Add(p,"CATA_EVENT_110t3");var right=Add(p,"CATA_EVENT_110t4");
            if(full) { PoolPilotProof.FillHand(p); for(int i=0;i<7;i++) Summon(p,"CS2_231"); }
            foreach(var next in Forks(g).Prepend(g)) {
                var owner=next.Player1;owner.UsedMana=0;
                Check(next.Process(PlayCardTask.Any(owner,next.IdEntityDic[blue.Id])),"Blue middle play");Drain(next);
                Check(owner.Hero.Armor==0 && owner.HandZone.Any(e=>e.Card.Id=="CATA_EVENT_110t5"),"newly drawn Essence excluded from captured run; reservation frees hand space");
                var casts=next.HistoryFacts.Query.Casts().Where(f=>f.Outcome==HistoryCastOutcome.Started && new[]{left.Id,blue.Id,right.Id}.Contains(f.EntityId)).OrderBy(f=>f.CastId).ToArray();
                Check(casts.Select(f=>f.EntityId).SequenceEqual(new[]{blue.Id,left.Id,right.Id}),"selected first then original left-to-right cast order");
                Check(owner.BoardZone.Count==(full?7:1) && owner.UsedMana==0,"full board loses Green only; Bronze still refreshes");
            }
        }
        var counter=Fixture(); var c=counter.Player1;
        var secret=(Spell)Entity.FromCard(c.Opponent,Cards.FromId("EX1_287")); Generic.CastSpell(c.Opponent,counter,secret,null,0);secret.IsExhausted=false;var black=Add(c,"CATA_EVENT_110t5");var green=Add(c,"CATA_EVENT_110t6");c.UsedMana=0;
        Check(counter.Process(PlayCardTask.Any(c,black)),"countered root play");
        Check(c.Hero.Armor==0 && green.Zone==c.HandZone && green[DragonSoulShattered.ChainRoot]==0,"countered root cancels run without consuming neighbors");
        var effects=Fixture(); var p2=effects.Player1; Summon(p2,"CS2_052"); // Spell Damage +1
        var enemy=Summon(p2.Opponent,"CS2_200"); var friendly=Summon(p2,"CS2_200");
        Play(effects,p2,"CATA_EVENT_110t7");
        Check(enemy.Damage==5 && friendly.Damage==0,"Storm one enemy-only Spell Damage wave");
        Play(effects,p2,"CATA_EVENT_110t2",p2.Opponent.Hero);
        Check(p2.Opponent.Hero.Damage==9,"Red manual enemy hero scales with Spell Damage");
        int armor=p2.Hero.Armor;Play(effects,p2,"CATA_EVENT_110t5");Check(p2.Hero.Armor==armor+12,"Black Armor never scales with Spell Damage");
        var empty=Fixture();PoolPilotProof.ClassDeck(empty.Player1);Play(empty,empty.Player1,"CATA_EVENT_110t3");
        Check(empty.Player1.Hero.Damage==0 && empty.Player1.HandZone.Count==0,"empty spell tutor has no fatigue or substitution");
    }
    private static void Unpinned_SidequestState()
    {
        var g=Fixture();var p=g.Player1;
        Play(g,p,"CS2_172"); var q=(Spell)Play(g,p,"TLC_EVENT_400");Summon(p,"CS2_172");Drain(g);
        Check(q.QuestProgress==0,"prequest and summoned-only Beasts excluded");
        Play(g,p,"EX1_564",p.BoardZone.First(m=>m.Card.IsRace(Race.BEAST)));
        Check(q.QuestProgress==0,"copying a Beast after playing an untyped Faceless does not count Beast play");
        Check(!QuestRules.CanInstall(p,q.Card),"duplicate active Sidequest prohibited");
        End(g);Play(g,p.Opponent,"CS2_172");Check(q.QuestProgress==0,"opponent play excluded");End(g);
        Play(g,p,"CS2_172");Rewind(g);
        Check(q.QuestProgress==1,"quest progress retained by Rewind");
        foreach(var next in Forks(g)) {
            var owner=next.Player1;Play(next,owner,"BE_734");Play(next,owner,"CS2_172");
            Check(((Spell)next.IdEntityDic[q.Id]).QuestProgress==3 && owner.Choice!=null,"All-type play advances once after clone/snapshot");
            var first=owner.Choice.Choices.Select(i=>next.IdEntityDic[i].Card).ToArray();
            Check(first.Select(c=>c.Id).Distinct().Count()==first.Length && first.All(c=>c.Cost<=5 && c.IsRace(Race.BEAST)),"first crafting choices distinct capped Beasts");
            Pick(next);var second=owner.Choice.Choices.Select(i=>next.IdEntityDic[i].Card).ToArray();
            Check(second.Select(c=>c.Id).Distinct().Count()==second.Length && second.All(c=>!c.Echo),"second crafting choices distinct non-Echo");
            foreach(var saved in Forks(next).Prepend(next)) {
                Pick(saved);Drain(saved);var beast=saved.Player1.HandZone.Single(e=>e.Card.Id=="ICC_828t");
                Check(beast.Card.Cost==first[0].Cost+second[0].Cost && beast.Card.ATK==first[0].ATK+second[0].ATK
                    && beast.Card.Health==first[0].Health+second[0].Health,"composite stats/cost sum exactly across saved second choice");
                Check(beast.Cost==Math.Max(0,beast.Card.Cost-3),"composite separate -3 modifier");
                Hidden(saved,saved.Player1,new[]{beast.Id}); Rewind(saved);
                Check(saved.IdEntityDic[beast.Id].Cost==beast.Cost,"Rewind retains custom definition and discount");
            }
        }
        var cap=Fixture();var cp=cap.Player1;Play(cap,cp,"TLC_EVENT_400");
        Play(cap,cp,"UNG_028");
        foreach(string secretId in new[]{"EX1_287","EX1_289","EX1_295"}) Play(cap,cp,secretId);
        var excess=Add(cp,"EX1_294");cp.UsedMana=0;
        Check(!cp.Options().Any(t=>t.Source?.Id==excess.Id) && !cap.Process(PlayCardTask.Any(cp,excess)),
            "Sidequest/legacy Quest/Secrets share five slots");
        var full=Fixture();var o=full.Player1;var quest=(Spell)Play(full,o,"TLC_EVENT_400");
        QuestRules.Advance(quest,3);PoolPilotProof.FillHand(o);Pick(full);Pick(full);Drain(full);
        Check(o.HandZone.Count==10 && o.PendingQuestRewards.Count==0 && !o.HandZone.Any(e=>e.Card.Id=="ICC_828t"),"full-hand Sidequest reward lost without q12 waiting");
        Console.WriteLine("POOL Storm Standard text="+BuildABeast.StormCandidates(g,p,PoolScope.Standard,true).Length
            +"; Wild keywords="+BuildABeast.StormCandidates(g,p,PoolScope.Wild,false).Length);
    }
    private static void Unpinned_WatfinMutations()
    {
        var seen=new HashSet<string>();
        for(int seed=1;seed<=120 && seen.Count<5;seed++) {
            var g=Fixture();var p=g.Player1;g.SetRandomSeed(seed);
            var source=(Minion)Play(g,p,"JAIL_EVENT_100");
            var offers=p.Choice.Choices.Select(i=>g.IdEntityDic[i]).ToArray();var custom=offers.Single(e=>e.Card.Id=="JAIL_EVENT_100t");
            var original=Cards.FromAssetId(custom.Card[GameTag.MODULAR_ENTITY_PART_1]);
            string kind=custom.Card.Cost!=original.Cost?"cost":custom.Card.ATK!=original.ATK?"attack":custom.Card.Health!=original.Health?"health":custom.Card.Name!=original.Name?"name":"type";
            Check(custom.Card.Cost<original.Cost || custom.Card.ATK>original.ATK || custom.Card.Health>original.Health
                || custom.Card.Name!=original.Name || custom.Card.Races.Except(original.Races).Any(),"suspicious mutation always visibly differs");
            Check(offers.Where(e=>e!=custom).Select(e=>e.Card.Id).Append(original.Id).Distinct().Count()==offers.Length,"Discover original minions distinct");
            Hidden(g,p,offers.Select(e=>e.Id));
            if(!seen.Add(kind)) continue;
            foreach(var next in Forks(g).Prepend(g)) {
                Pick(next,custom.Id);Drain(next);var result=next.IdEntityDic[custom.Id];
                Check(((Minion)next.IdEntityDic[source.Id]).AttackDamage==4,"suspicious selection buffs source exactly once");
                Check(result.Zone==next.Player1.HandZone && result.Card.ATK==custom.Card.ATK,"custom definition retained after saved choice");
                Rewind(next); Check(next.IdEntityDic[custom.Id].Card.Name==custom.Card.Name,"mutation retained through Rewind");
                Hidden(next,next.Player1,offers.Where(e=>e.Id!=source.Id).Select(e=>e.Id));
                var owner=next.Player1;var made=(Minion)next.IdEntityDic[custom.Id];
                owner.HandZone.Remove(made); Generic.SummonBlock(next,made,-1,null); Drain(next);
                Check(made.Card.ATK==custom.Card.ATK,"intrinsic mutation persists on summon");
                made.Silence();Check(made.AttackDamage==made.Card.ATK,"Silence preserves intrinsic mutated base Attack");
            }
        }
        Check(seen.Count==5,"all five seeded suspicious mutation categories covered");
        foreach(bool full in new[]{false,true}) {
            var g=Fixture();var p=g.Player1;var source=(Minion)Play(g,p,"JAIL_EVENT_100");var ids=p.Choice.Choices.ToArray();
            int chosen=ids.First(i=>g.IdEntityDic[i].Card.Id!="JAIL_EVENT_100t");
            if(full) PoolPilotProof.FillHand(p);
            Pick(g,chosen);Drain(g);Check(source.AttackDamage==3 && source.Health==2,"incorrect option never grants Watfin bonus");
            Check(g.IdEntityDic[chosen].Zone.Type==(full?Zone.GRAVEYARD:Zone.HAND),"ordinary Discover overflow");
        }
        var gone=Fixture();var s=(Minion)Play(gone,gone.Player1,"JAIL_EVENT_100");int suspicious=gone.Player1.Choice.Choices.Single(i=>gone.IdEntityDic[i].Card.Id=="JAIL_EVENT_100t");
        gone.Player1.SetasideZone.Add(gone.Player1.BoardZone.Remove(s));Pick(gone,suspicious);Drain(gone);
        Check(s.AttackDamage==3 && gone.Player1.HandZone.Single().Card.Id=="JAIL_EVENT_100t","absent source loses buff but discovered card still granted");
    }
    private static void Unpinned_StarStateAndRandom()
    {
        foreach(int seed in new[]{3,19,41}) {
            var g=Fixture();var p=g.Player1;Play(g,p,"JAIL_EVENT_101");Play(g,p,"JAIL_EVENT_101");
            var power=p.Hero.HeroPower;var one=Summon(p.Opponent,"CS2_200");var two=Summon(p.Opponent,"CS2_200");
            two.HasStealth=true;Summon(p,"CS2_052");
            g.SetRandomSeed(seed);var rng=new Util.DeepCloneableRandom(seed);var expected=new ICharacter[]{one,two,p.Opponent.Hero}[rng.Next(3)];
            foreach(var next in Forks(g).Prepend(g)) {
                var owner=next.Player1;owner.UsedMana=0;Check(next.Process(HeroPowerTask.Any(owner)),"random Star activation");Drain(next);
                Check(((ICharacter)next.IdEntityDic[expected.Id]).Damage==3,"seeded Star includes hero and Stealth; excludes Spell Damage");
                Check(next.Random.CaptureState().Equals(rng.CaptureState()),"Star consumes one ordered random selection");
                var star=(HeroPower)next.IdEntityDic[power.Id];
                Summon(owner.Opponent,"CS2_065");Drain(next);Check(star.IsExhausted,"enemy Demon cannot refresh");
                Summon(owner,"CS2_065");Drain(next);Check(!star.IsExhausted,"friendly Demon refreshes without changing upgrades");
                Rewind(next);owner=next.Player1;Check(next.Player1.Hero.HeroPower[SoulImmolation.Upgrades]==1,"power-local damage retained through Rewind");
                new ReplaceHeroPower(Cards.FromId("CS2_034")).Process(next,owner,owner.Hero,null);
                Play(next,owner,"JAIL_EVENT_101");Check(owner.Hero.HeroPower[SoulImmolation.Upgrades]==0,"replacement then reinstall resets upgrades");
            }
        }
    }
    private static void CastHome(Game g, Controller c, BoardObject location)
    {
        var spell=Add(c,"TIME_EVENT_997");c.UsedMana=0;
        var options=c.Options().OfType<PlayCardTask>().Where(t=>t.Source?.Id==spell.Id).ToArray();
        Check(options.Any(t=>t.ActionTarget?.Id==location.Id) && options.All(t=>t.ZonePosition==-1)
            && options.Select(Simulation.TaskKey).Distinct().Count()==options.Length,
            "location-target spell options have target and no placement position");
        Check(g.Process(PlayCardTask.Any(c,spell,location)),"Welcome Home location target accepted");
    }
    private static void Unpinned_LocationState()
    {
        var golden=Fixture();var goldenLocation=LocationProofs.Location(golden.Player1);
        var goldenSpell=Add(golden.Player1,"TIME_EVENT_997");goldenSpell[GameTag.PREMIUM]=1;
        Check(golden.Process(PlayCardTask.Any(golden.Player1,goldenSpell,goldenLocation)),"Golden Welcome Home cast");
        goldenLocation.Destroy();Drain(golden);
        Check(golden.Player1.BoardZone.Single()[GameTag.PREMIUM]==1,"pinned Golden Welcome Home grants Golden summon since 34.4");
        foreach(bool enemy in new[]{false,true}) {
            var g=Fixture();var p=g.Player1;var owner=enemy?p.Opponent:p;var location=LocationProofs.Location(owner);
            int durability=location.Durability;location.IsExhausted=true;location[GameTag.EXHAUSTED]=1;location[GameTag.LOCATION_ACTION_COOLDOWN]=1;
            CastHome(g,p,location);CastHome(g,p,location);
            Check(location.Durability==durability && !location.IsExhausted,"reopen used and already-ready location without restoring durability");
            Rewind(g);
            foreach(var next in Forks(g).Prepend(g)) {
                var loc=(BoardObject)next.IdEntityDic[location.Id];var c=loc.Controller;
                Check(loc.AppliedEnchantments.Count(e=>e.Card.Id=="TIME_EVENT_997e")==2,"two independent Deathrattles retained clone/snapshot/Rewind");
                var candidates=OfflineCardPools.Candidates(next,c.HeroClass,WelcomeHome.Pool(),123416);Check(candidates.Count>=3,"three-cost exact bucket has at least three candidates");
                next.SetRandomSeed(37);var rng=new Util.DeepCloneableRandom(37);var expected=new[]{candidates[rng.Next(candidates.Count)].Id,candidates[rng.Next(candidates.Count)].Id};
                loc.Destroy();Drain(next);
                Check(c.BoardZone.Select(m=>m.Card.Id).SequenceEqual(expected.Reverse()),"independent seeded Deathrattles use dying physical slot");
            }
        }
        var full=Fixture();var o=full.Player1;var loc2=LocationProofs.Location(o);CastHome(full,o,loc2);
        for(int i=0;i<6;i++) Summon(o,"CS2_231");loc2.Destroy();Drain(full);
        Check(o.BoardZone.Count==7 && o.BoardZone.Count(m=>m.Card.Cost==3)==1,"location frees one slot for granted summon");
        var empty=Fixture();var e=LocationProofs.Location(empty.Player1);CastHome(empty,empty.Player1,e);
        PoolPilotProof.Pool(empty,"TIME_EVENT_997",0,()=>{e.Destroy();Drain(empty);Check(empty.Player1.BoardZone.SlotCount==0,"D2 empty exact bucket produces nothing");});
    }
}
