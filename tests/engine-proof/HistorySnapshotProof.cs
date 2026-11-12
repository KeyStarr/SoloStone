using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
internal static class HistorySnapshotProof
{
    private static void Check(bool value,string message)=>Core2026Proof.Check(value,"history snapshot: "+message);
    private static Card DecisionCard;
    internal static void Register()
    {
        if(DecisionCard!=null)return;
        DecisionCard=HistoryCanonicalProof.Consumer("CS2_231");
        typeof(Card).GetProperty("Id").SetValue(DecisionCard,"HISTORY_PROOF_DISCOVER");
        typeof(Card).GetField("<AssetId>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(DecisionCard,-22001);
        DecisionCard.Power=new Power { PowerTask=new DecisionBattlecryTask("proof/history/discover/v1",
            new DiscoverThenContinueTask(CardType.MINION),new ArmorTask(1)) };
        var data=typeof(Cards).GetField("Data",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        var cards=(Dictionary<string,Card>)data.GetType().GetProperty("Cards",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(data);
        cards.Add(DecisionCard.Id,DecisionCard);
        var drawn=Cards.FromId("GAME_005").Clone();
        typeof(Card).GetProperty("Id").SetValue(drawn,"HISTORY_PROOF_DRAWN");
        drawn.Tags[GameTag.CASTSWHENDRAWN]=1;
        drawn.Power=new Power { PowerTask=drawn.Power.PowerTask,TopdeckTask=new CustomTask((g,c,s,t,stack)=>{
            Generic.CastSpell(c,g,(Spell)s,null,0);
            var nested=(Spell)Entity.FromCard(c,Cards.FromId("GAME_005"));
            Generic.CastSpell(c,g,nested,null,0);
        }) };
        cards.Add(drawn.Id,drawn);
    }
    private static void Answer(Game g)
    {
        var decision=g.GetPendingDecisions().Single();
        Check(g.TryAnswer(new DecisionAnswer(decision.Id,decision.ControllerId,new[]{decision.Options[0].OptionId}),out var error),"answer "+error);
    }
    private static string Future(Game g)
    {
        if(g.Resolution.HasSuspendedEffect) Answer(g);
        Add(g.Player1,"GAME_005");
        return g.HistoryFacts.CanonicalText()+Canonical.State(g)+Canonical.History(g);
    }
    internal static void Child(string directory)
    {
        Register();
        foreach(var path in Directory.GetFiles(directory,"*.snapshot"))
        {
            var bytes=File.ReadAllBytes(path);var g=Game.LoadSnapshot(bytes);
            Check(bytes.SequenceEqual(g.SaveSnapshot()),"fresh-process save/load/save identical");
            foreach(var death in g.HistoryFacts.Query.Deaths().Where(f=>f.DbfId==Cards.FromId("JAIL_974").AssetId))
                Check(death.Definition.Materialize().Power.Trigger.GetType()==Cards.FromId("JAIL_974").Power.Trigger.GetType(),
                    "fresh-process registered trigger materialization");
            Check(Future(g)==File.ReadAllText(path+".future"),"fresh-process future identical");
        }
        Console.WriteLine("PASS history fresh-process snapshot: ordinary + suspended decision, bytes and future");
    }
    private static void Sharing(Game source,Game fork)
    {
        var field=typeof(HistoryFacts).GetField("_chunks",BindingFlags.Instance|BindingFlags.NonPublic);
        var a=(List<HistoryFact[]>)field.GetValue(source.HistoryFacts);var b=(List<HistoryFact[]>)field.GetValue(fork.HistoryFacts);
        Check(!ReferenceEquals(a,b) && a.Count==b.Count && a.Zip(b).All(pair=>ReferenceEquals(pair.First,pair.Second)),"all sealed chunks shared, pointer lists private");
        var tail=typeof(HistoryFacts).GetField("_tail",BindingFlags.Instance|BindingFlags.NonPublic);
        var at=(HistoryFact[])tail.GetValue(source.HistoryFacts);var bt=(HistoryFact[])tail.GetValue(fork.HistoryFacts);
        Check(!ReferenceEquals(at,bt) && at.Zip(bt).All(pair=>ReferenceEquals(pair.First,pair.Second)),"tail slots private, record payloads shared");
        var index=typeof(HistoryFacts).GetField("_indexChunks",BindingFlags.Instance|BindingFlags.NonPublic);
        var ai=(System.Collections.IList)index.GetValue(source.HistoryFacts);var bi=(System.Collections.IList)index.GetValue(fork.HistoryFacts);
        Check(!ReferenceEquals(ai,bi) && ai.Count==bi.Count && Enumerable.Range(0,ai.Count).All(i=>ReferenceEquals(ai[i],bi[i])),"sealed index pages shared, pointer lists private");
        var indexTail=typeof(HistoryFacts).GetField("_indexTail",BindingFlags.Instance|BindingFlags.NonPublic);
        var ait=(Array)indexTail.GetValue(source.HistoryFacts);var bit=(Array)indexTail.GetValue(fork.HistoryFacts);
        Check(!ReferenceEquals(ait,bit) && Enumerable.Range(0,ait.Length).All(i=>ReferenceEquals(ait.GetValue(i),bit.GetValue(i))),"index tail slots private, immutable index roots shared");
    }
    private static void CloneFamily()
    {
        foreach(int size in new[]{100,1000,10000})
        {
            var g=HistoryFactsProof.Fixture(false);
            while(g.HistoryFacts.Count<size) {var m=Summon(g.Player1,"CS2_231");Kill(g,m);}
            long allocationBefore=GC.GetAllocatedBytesForCurrentThread();
            var root=g.HistoryFacts.Fork();
            long allocated=GC.GetAllocatedBytesForCurrentThread()-allocationBefore;
            Check(allocated<4096+16L*(g.HistoryFacts.Count/64),"Fork allocation bounded by chunk pointers plus tail, no per-record copies");
            if(size==1000)
            {
                var bytes=g.SaveSnapshot();var restored=Game.LoadSnapshot(bytes);
                Check(bytes.SequenceEqual(restored.SaveSnapshot()) && g.HistoryFacts.CanonicalText()==restored.HistoryFacts.CanonicalText(),
                    ">256 records across many flat chunks snapshot byte-identical");
            }
            foreach(Func<Game,Game> clone in new Func<Game,Game>[] { x=>x.Clone(),x=>x.CloneExact(),x=>x.CloneForSimulation(42) })
            {
                var a=clone(g);var b=clone(g);Sharing(g,a);Sharing(g,b);
                var before=g.HistoryFacts.CanonicalText();
                Add(a.Player1,"GAME_005");Check(g.HistoryFacts.CanonicalText()==before && b.HistoryFacts.CanonicalText()==before,"first sibling isolated");
                Add(b.Player2,"CS2_231");Check(g.HistoryFacts.CanonicalText()==before && a.HistoryFacts.Query.HandEntries().Last().ControllerId==g.Player1.Id,"second sibling isolated");
                Add(g.Player1,"CS2_182");Add(g.Player1,"CS2_182");
                Check(a.HistoryFacts.CanonicalText()!=b.HistoryFacts.CanonicalText() && a.HistoryFacts.CanonicalText()!=g.HistoryFacts.CanonicalText(),"source and both siblings append independently");
            }
        }
    }
    private static void Invalid()
    {
        void Reject(Action<Game> damage,string name)
        {
            var g=HistoryFactsProof.Fixture(false);Play(g,g.Player1,"GAME_005");
            damage(g);var bytes=g.SaveSnapshot();bool rejected=false;
            try {Game.LoadSnapshot(bytes);} catch(SerializationException){rejected=true;}
            Check(rejected,"invalid "+name+" rejected at load");
        }
        void Store(Game g,string field,object value)=>typeof(HistoryFacts).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).SetValue(g.HistoryFacts,value);
        Reject(g=>Store(g,"_version",2),"version");
        Reject(g=>Store(g,"_nextSequence",1L),"sequence");
        Reject(g=>Store(g,"CurrentTurn",g.Turn+5),"turn");
        Reject(g=>Store(g,"NextCast",1L),"cast link");
        Reject(g=>{ Generic.ShuffleIntoDeck(g.Player1,g.Player1.Hero,Entity.FromCard(g.Player1,Cards.FromId("CS2_231")));Store(g,"NextOperation",1L); },"shuffle operation link");
        Reject(g=>typeof(HistoryDefinition).GetField("_card",BindingFlags.Instance|BindingFlags.NonPublic)
            .SetValue(g.HistoryFacts.Query.Plays().Single().Definition,null),"definition graph");
        Reject(g=>typeof(HistoryCardFact).GetField("_definition",BindingFlags.Instance|BindingFlags.NonPublic)
            .SetValue(g.HistoryFacts.Query.Plays().Single(),null),"private definition on authoritative store");
        Reject(g=>{
            var forged=Cards.FromId("GAME_005").Clone();
            typeof(Card).GetField("<AssetId>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(forged,9999999);
            typeof(HistoryDefinition).GetField("_card",BindingFlags.Instance|BindingFlags.NonPublic)
                .SetValue(g.HistoryFacts.Query.Plays().Single().Definition,forged);
        },"definition dbf mismatch");
        Reject(g=>Store(g,"_initial",new Dictionary<int,int>{{999999,2}}),"membership");
        Reject(g=>Store(g,"_unavailableOrigins",new HashSet<int>{g.Player1.Hero.Id}),"private origins on authoritative store");
        Reject(g=>typeof(HistoryFact).GetField("<ControllerId>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)
            .SetValue(g.HistoryFacts.Query.Plays().Single(),99),"controller");
    }
    internal static void Run()
    {
        Register();CloneFamily();Invalid();
        string directory=Path.Combine(Path.GetTempPath(),"hs-history-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            foreach(bool suspended in new[]{false,true})
            {
                var g=HistoryFactsProof.Fixture();
                Entity.FromCard(g.Player1,HistoryCanonicalProof.Consumer());
                if(!suspended) for(int i=0;i<150;i++){var body=Summon(g.Player1,"CS2_231");Kill(g,body);}
                Kill(g,Summon(g.Player1,"JAIL_974"));
                var composed=Card.CreateZombeastCard(Cards.FromId("CS2_182"),Cards.FromId("CS2_120"),true);
                var custom=Entity.FromCard(g.Player1,composed,zone:g.Player1.HandZone);
                PlayEntity(g,g.Player1,custom);Kill(g,(Minion)custom);
                var death=g.HistoryFacts.Query.Deaths().Single(f=>f.EntityId==custom.Id);var def=death.Definition.Materialize();
                Check(g.HistoryFacts.Query.Plays().Single(f=>f.EntityId==custom.Id).Definition.Data==death.Definition.Data,
                    "CardPlayed and MinionDied share complete composed definition identity");
                Check(def.Name=="Zombeast" && def.ATK==composed.ATK && def.Health==composed.Health && def.Tags[GameTag.MODULAR_ENTITY_PART_2]==composed.Tags[GameTag.MODULAR_ENTITY_PART_2],"composed definition frozen beyond dbf identity");
                def.Tags[GameTag.ATK]=999;
                Check(death.Definition.Materialize().Tags[GameTag.ATK]!=999,"query materialization cannot mutate fact");
                if(suspended)
                {
                    var source=Entity.FromCard(g.Player1,DecisionCard,zone:g.Player1.HandZone);
                    PlayEntity(g,g.Player1,source);
                    Check(g.Resolution.HasSuspendedEffect && !g.HistoryFacts.Query.Discoveries().Any(),"supported suspended Discover boundary");
                    var decision=g.GetPendingDecisions().Single();var bytesBefore=g.SaveSnapshot();
                    Check(!g.TryAnswer(new DecisionAnswer(decision.Id,decision.ControllerId,new[]{int.MaxValue}),out _) && bytesBefore.SequenceEqual(g.SaveSnapshot()),"rejected suspended answer byte identical");
                }
                var canonicalHash=Canonical.Sha(Canonical.State(g));
                var bytes=g.SaveSnapshot();var loaded=Game.LoadSnapshot(bytes);var clone=g.CloneExact(history:true);
                Check(canonicalHash==Canonical.Sha(Canonical.State(loaded)) && canonicalHash==Canonical.Sha(Canonical.State(clone)),
                    "composed history definition survives gated canonical hash, clone and snapshot");
                Check(bytes.SequenceEqual(loaded.SaveSnapshot()) && g.HistoryFacts.CanonicalText()==clone.HistoryFacts.CanonicalText(),"same-build snapshot and clone identical facts");
                Sharing(g,clone);
                var path=Path.Combine(directory,suspended?"suspended.snapshot":"ordinary.snapshot");File.WriteAllBytes(path,bytes);
                File.WriteAllText(path+".future",Future(loaded));
                Check(Future(g)==File.ReadAllText(path+".future"),"snapshot same-process continuation");
                if(suspended) Check(g.HistoryFacts.Query.Discoveries().Count()==1 && g.Player1.Hero.Armor==1,"suspended Discover continuation once");
            }
            var resolution=HistoryResolutionProof.Populated(true);
            var resolutionPath=Path.Combine(directory,"resolution.snapshot");
            File.WriteAllBytes(resolutionPath,resolution.SaveSnapshot());
            File.WriteAllText(resolutionPath+".future",Future(resolution));
            var start=new ProcessStartInfo(Environment.ProcessPath){RedirectStandardOutput=true,RedirectStandardError=true};
            if(Path.GetFileNameWithoutExtension(Environment.ProcessPath)=="dotnet")start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--history-child");start.ArgumentList.Add(directory);
            using var child=Process.Start(start);var output=child.StandardOutput.ReadToEnd();var error=child.StandardError.ReadToEnd();
            Check(child.WaitForExit(30000)&&child.ExitCode==0,"child: "+output+error);Console.Write(output);
        }
        finally {Directory.Delete(directory,true);}
        Console.WriteLine("PASS history snapshot: 100/1000/10000 events, shared chunks, source/two siblings, composed definitions, malformed blobs");
    }
}
