using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PastVarietyModernProof
{
    private static OfflinePoolManifest CandidatePools(string only=null)
    {
        var xml=OfflinePoolFixtures.Catalog();var members=xml.Root.Element("Members");
        if(only!=null) members.Elements("Card").Where(c=>(string)c.Attribute("scope")=="Past").Remove();
        foreach(string id in Ids) {
            if(only!=null&&id!=only)continue;
            members.Elements("Card").Where(c=>(string)c.Attribute("cardId")==id).Remove();
            members.Add(ClientExport.PoolMember(id,"PastVarietyModernProof.Run"));
        }
        return OfflinePoolFixtures.Manifest(xml);
    }
    private static void PoolsAndGeneration()
    {
        var pools=CandidatePools();
        foreach(var source in new[]{("TIME_016","mech",CardClass.PALADIN),("TIME_704","spell",CardClass.DRUID),("TIME_707","choose-one",CardClass.DRUID)}) {
            var g=Fresh(7,pools);var bucket=OfflineCardPools.Candidates(g,source.Item3,TimeTravelPastPools.Filter(source.Item1,source.Item2),Cards.FromId(source.Item1).AssetId);
            var required=source.Item1=="TIME_016"?Ids.Take(3):source.Item1=="TIME_704"?Ids.Skip(3):new[]{"ETC_373"};
            Core2026Proof.Check(required.All(id=>bucket.Any(c=>c.Id==id)),"modern candidate closure "+source.Item1);
            Console.WriteLine("POOL "+source.Item1+"="+bucket.Count+" "+string.Join(",",bucket.Select(c=>c.Id)));
        }
        var reached=new HashSet<string>();
        for(int seed=1;seed<=64;seed++) {
            var g=Fresh(seed,pools);var p=g.Player1;p.Hero[GameTag.CLASS]=(int)CardClass.PALADIN;
            var bucket=OfflineCardPools.Candidates(g,CardClass.PALADIN,TimeTravelPastPools.Filter("TIME_016","mech"),119964).ToList();
            Core2026Proof.Check(bucket.Count>=3,"Neon three or more candidates");
            var rng=g.Random.Clone();var expected=new List<string>();
            // SampleDistinct uses the shared partial Fisher-Yates sampler.
            for(int i=0;i<Math.Min(3,bucket.Count);i++) {int pick=rng.Next(i,bucket.Count);(bucket[i],bucket[pick])=(bucket[pick],bucket[i]);expected.Add(bucket[i].Id);}
            var neon=Generic.DrawCard(p,Cards.FromId("TIME_016"));Play(g,p,neon);
            var actual=p.Choice.Choices.Select(n=>g.IdEntityDic[n].Card.Id).ToArray();
            Core2026Proof.Check(actual.SequenceEqual(expected)&&actual.Distinct().Count()==3,"Neon seeded ordered distinct offers seed="+seed);
            int selection=p.Choice.Choices.Last();string chosen=g.IdEntityDic[selection].Card.Id;reached.Add(chosen);
            Resume(g,next=> {
                var owner=next.Player1;Core2026Proof.Check(Answer(next,selection),"Neon answer after clone/snapshot");
                var granted=(Minion)owner.HandZone.Single();Core2026Proof.Check(granted.Card.Id==chosen&&granted.AttackDamage==granted.Card.ATK+5&&granted.Health==granted.Card.Health+5,"Neon actual +5/+5 delivery");
                if(Ids.Take(3).Contains(chosen)) {
                    var buddy=Core2026Proof.Summon(owner,"YOD_010");
                    if(chosen=="NX2_022")Core2026Proof.Add(owner,"YOD_010");
                    Play(next,owner,granted);
                    if(chosen=="YOD_010") {
                        Core2026Proof.Kill(next,granted);var reborn=owner.BoardZone.Single(m=>m.Card.Id==chosen&&m.Id!=buddy.Id);
                        Core2026Proof.Check(reborn.AttackDamage==2&&reborn.Health==1&&reborn[GameTag.REBORN]==0,"Neon-buffed Shotbot Reborn loses +5/+5");
                    }
                    if(chosen=="TSC_059")Core2026Proof.Check(buddy.HasDivineShield&&buddy.HasTaunt&&!granted.HasDivineShield,"generated Bubblebot Battlecry applies to other Mech");
                    if(chosen=="NX2_022")Core2026Proof.Check(granted.HasWindfury&&granted[GameTag.RUSH]==1,"generated Goldwing retains Rush and resolves holding-Mech Battlecry");
                }
            });
        }
        Core2026Proof.Check(Ids.Take(3).All(reached.Contains),"Neon reaches each modern Mech through actual generation");
        var full=Fresh(3,pools);var owner=full.Player1;owner.Hero[GameTag.CLASS]=(int)CardClass.PALADIN;
        for(int i=0;i<9;i++)Core2026Proof.Add(owner,"CS2_182");
        Core2026Proof.Play(full,owner,"TIME_016");
        // Fill the final slot while the real Discover is suspended, before delivery.
        Core2026Proof.Add(owner,"CS2_182");
        var offered=owner.Choice.Choices.ToArray();Core2026Proof.Check(Answer(full,offered[0]),"full hand Neon answer");
        Core2026Proof.Check(owner.HandZone.Count==10&&!owner.HandZone.Any(e=>offered.Contains(e.Id)),"Neon full hand burns generated card");
        var empty=Fresh(1,CandidatePools("ETC_373"));var before=empty.Player1.HandZone.Count;
        Core2026Proof.Play(empty,empty.Player1,"TIME_016");
        Core2026Proof.Check(empty.Player1.HandZone.Count==before&&empty.Player1.Choice==null,"empty exact Paladin Mech bucket yields nothing");
    }
    private static void Privacy(Game g,Controller owner,IEnumerable<int> ids)
    {
        int[] hidden=ids.ToArray();var authoritative=g.SaveSnapshot();
        var opponent=HistoryFairOriginProof.FairGame(FairBotState.Create(g,owner.Opponent.PlayerId,97,DeckStyle.Value));
        foreach(int id in hidden)Core2026Proof.Check((!opponent.IdEntityDic.TryGetValue(id,out var hiddenActor)||hiddenActor is Unknown),"opponent fair simulation hides generated actor "+id);
        var fair=g.CloneExact(history:true);fair.ProjectHistoryForFairView(owner.Opponent.PlayerId);
        foreach(int id in hidden) {
            bool denied=false;try{_ = fair.HistoryFacts.Query.Residence(id,Zone.DECK);}catch(NotSupportedException){denied=true;}
            Core2026Proof.Check(denied,"opponent fair history denies hidden residence");
        }
        Core2026Proof.Check(authoritative.SequenceEqual(g.SaveSnapshot()),"privacy projection leaves authority unchanged");
    }
    private static void AlternateAndPrivacy()
    {
        foreach(bool second in new[]{false,true}) {
            var g=Fresh(29,CandidatePools("ETC_373"));if(second)Core2026Proof.End(g);var p=g.CurrentPlayer;
            foreach(var e in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(e));
            foreach(var e in p.DeckZone.ToArray())p.SetasideZone.Add(p.DeckZone.Remove(e));
            Core2026Proof.Add(p,"CS2_182");Entity.FromCard(p,Cards.FromId("CS2_182"),zone:p.DeckZone);
            Core2026Proof.Play(g,p,"TIME_707");
            var hand=p.HandZone.Single();var deck=p.DeckZone.Single();
            Core2026Proof.Check(hand.Card.Id=="ETC_373"&&deck.Card.Id=="ETC_373"&&hand.Cost==6&&deck.Cost==6,"Alternate actual hand/deck Drum replacements cost one less");
            var branches=hand.ChooseOnePlayables.Concat(deck.ChooseOnePlayables).Select(e=>e.Id).ToArray();
            Privacy(g,p,new[]{hand.Id,deck.Id}.Concat(branches));
            var own=HistoryFairOriginProof.FairGame(FairBotState.Create(g,p.PlayerId,97,DeckStyle.Value));
            Core2026Proof.Check((!own.IdEntityDic.TryGetValue(deck.Id,out var hiddenDeck)||hiddenDeck is Unknown)&&deck.ChooseOnePlayables.All(b=>!own.IdEntityDic.TryGetValue(b.Id,out var actor)||actor is Unknown),"own hidden deck and branch actors remain unknown");
            foreach(int branch in new[]{1,2}) {
                var simulation=g.CloneExact(history:true);var player=simulation.CurrentPlayer;var ally=Core2026Proof.Summon(player,"CS2_182");player.BaseMana=10;player.UsedMana=0;
                var fair=FairBotState.Create(simulation,player.PlayerId,97,DeckStyle.Value);
                var action=fair.Actions.First(a=>a.Source==hand.Id&&a.ChooseOne==branch);
                var child=HistoryFairOriginProof.FairGame(fair.Apply(action));var friend=(Minion)child.IdEntityDic[ally.Id];
                Core2026Proof.Check(branch==1?child.CurrentPlayer.BoardZone.Count==6:friend.AttackDamage==6&&friend.Health==9&&friend.HasTaunt,"fair bot Apply executes each Drum branch");
            }
            Resume(g,next=> {
                var owner=next.CurrentPlayer;var drawn=Generic.Draw(owner);Core2026Proof.Check(drawn.Id==deck.Id&&drawn.Cost==6,"drawing replacement retains -1 cost");
                foreach(int branch in new[]{1,2}) foreach(int sourceId in new[]{hand.Id,deck.Id}) {
                    var fork=next.CloneExact(history:true);var player=fork.CurrentPlayer;var friend=Core2026Proof.Summon(player,"CS2_182");
                    Play(fork,player,fork.IdEntityDic[sourceId],branch);
                    Core2026Proof.Check(player.UsedMana==6 && (branch==1?player.BoardZone.Count==6:friend.AttackDamage==6&&friend.Health==9&&friend.HasTaunt),"both branches from hand/deck replacement pay six and realize actual outcome");
                }
            });
        }
    }
    private static void BuffLifecycle()
    {
        var g=Fresh(47);var p=g.Player1;
        var shot=(Minion)Core2026Proof.Play(g,p,"YOD_010");
        var bubble=(Minion)Core2026Proof.Play(g,p,"TSC_059");
        Core2026Proof.Add(p,"YOD_010");var wing=(Minion)Core2026Proof.Play(g,p,"NX2_022");
        Core2026Proof.Play(g,p,"TSC_656");var plant=p.BoardZone.Single(m=>m.Card.Id=="TSC_656t");
        Core2026Proof.Play(g,p,"SCH_609");
        var held=p.HandZone.OfType<Minion>().Single(m=>m.Card.Id=="YOD_010");int deck=p.DeckZone.First().Id;
        int shotAttack=shot.AttackDamage, plantSize=plant.AttackDamage;
        Resume(g,next=> {
            var owner=next.Player1;var coin=Core2026Proof.Add(owner,"GAME_005");coin[GameTag.REWIND]=1;Play(next,owner,coin);
            Core2026Proof.Check(RewindTestHelpers.IsRewindOffered(next),"Rewind with all modern layered buffs");
            RewindTestHelpers.AnswerRewind(next);RewindTestHelpers.KeepIfOffered(next);owner=next.Player1;
            var savedShot=(Minion)next.IdEntityDic[shot.Id];var savedWing=(Minion)next.IdEntityDic[wing.Id];var savedPlant=(Minion)next.IdEntityDic[plant.Id];
            Core2026Proof.Check(savedShot.AttackDamage==shotAttack&&savedShot.HasDivineShield&&savedShot.HasTaunt&&savedWing.HasWindfury
                &&savedPlant.AttackDamage==plantSize&&savedPlant.HasTaunt&&((Minion)next.IdEntityDic[held.Id]).AttackDamage==6
                &&((Minion)next.IdEntityDic[deck]).AttackDamage==8,"Rewind retains board/hand/deck stats and keywords");
            new SabberStoneCore.Tasks.SimpleTasks.ControlTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET,true).Process(next,owner,owner.Hero,savedShot);
            next.ProcessTasks();next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(savedShot.Controller==owner.Opponent&&savedShot.AttackDamage==shotAttack&&savedShot.HasTaunt&&savedShot.HasDivineShield,"control change keeps recipient's buffs");
            savedShot.Silence();next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(savedShot.AttackDamage==2&&savedShot.Health==2&&!savedShot.HasTaunt&&!savedShot.HasDivineShield,"controlled recipient Silence removes all buffs");
            Generic.ReturnToHandBlock(owner,savedWing);next.ProcessTasks();next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(!savedWing.HasWindfury&&savedWing.AttackDamage==3,"leave-play clears Goldwing Battlecry and Survival buff");
        });
    }
    private static void RewindState()
    {
        BuffLifecycle();
        foreach(string id in Ids.Skip(3)) {
            var g=Fresh(41,CandidatePools(id));var p=g.Player1;Core2026Proof.Summon(p,"CS2_182");Core2026Proof.Play(g,p,"TIME_704");
            Core2026Proof.Check(Answer(g,p.Choice.Choices[0]),"Rewind Mentor teaching");
            int pupil=p.HandZone.Single().Id;
            var coin=Core2026Proof.Add(p,"GAME_005");coin[GameTag.REWIND]=1;Play(g,p,coin);
            Core2026Proof.Check(RewindTestHelpers.IsRewindOffered(g),"Rewind offered with taught Pupil");RewindTestHelpers.AnswerRewind(g);RewindTestHelpers.KeepIfOffered(g);
            Core2026Proof.Check(PayloadRules.Entries(g.IdEntityDic[pupil],1).Single().Card.Id==id,"Rewind preserves each Pupil's taught definition");
            Play(g,g.Player1,g.IdEntityDic[pupil]);Core2026Proof.Check(g.HistoryFacts.Query.Casts().Any(c=>c.DbfId==Cards.FromId(id).AssetId),"Pupil casts after Rewind");
        }
    }
}
