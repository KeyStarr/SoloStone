using System.Text.Json;
using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using static Core2026Proof;
internal static class HistoryReplayProof
{
    private static void Check(bool value,string message)=>Core2026Proof.Check(value,"history replay: "+message);
    internal static void Run()
    {
        PoolSnapshots.Save();
        var record=new GameRecord {Id="history-2a",Suite="classic",Player="mage",Opponent="warrior",Seed=7,
            ClonePoints=new List<int>{0,2,15}};
        Game live=null,clone=null;byte[] snapshot=null;
        var facts=new List<string>();int checkpoint=20;
        var original=Simulation.Run(record,started:g=>live=g,processed:_=>{
            facts.Add(live.HistoryFacts.CanonicalText());
            if(facts.Count==checkpoint) {clone=live.CloneExact(history:true);snapshot=clone.SaveSnapshot();}
        });
        Simulation.Finish(record,original);
        var copy=JsonSerializer.Deserialize<GameRecord>(JsonSerializer.Serialize(record));
        int step=0;
        var replay=Simulation.Run(copy,replay:true,started:g=>live=g,processed:_=>{
            Check(live.HistoryFacts.CanonicalText()==facts[step++],"schema-3 reexecution matches full logical log after each recorded action");
        });
        Simulation.Finish(copy,replay);
        Check(record.ReplaySchema==3 && original.HistoryFacts.Query.Plays().Any() && original.HistoryFacts.Query.Deaths().Any()
            && original.HistoryFacts.Query.Casts().Any() && original.HistoryFacts.Query.Damage().Any()
            && original.HistoryFacts.Query.HealthChanges().Any() && original.HistoryFacts.Query.Payments().Any()
            && original.HistoryFacts.Query.AttacksFinished().Any() && original.HistoryFacts.Query.Events<ResidenceChangedFact>().Any() && step==record.Actions.Count,
            "schema-3 fixture has real actions, casts and deaths");
        Check(original.HistoryFacts.CanonicalText()==replay.HistoryFacts.CanonicalText(),"schema-3 final full facts identical");
        Check(record.LegalShas.SequenceEqual(copy.LegalShas) && record.HistorySha==copy.HistorySha
            && record.StateSha==copy.StateSha && record.Winner==copy.Winner && record.Turns==copy.Turns,
            "recorded legal/state/history hashes and future winner reproduced");
        Check(snapshot!=null && clone!=null && record.Actions.Count>checkpoint,"nonempty recorded future suffix");
        foreach(var continuation in new[]{clone,Game.LoadSnapshot(snapshot)})
        {
            for(int i=checkpoint;i<record.Actions.Count;i++)
            {
                var legal=Simulation.Legal(continuation);
                Check(Canonical.Sha(Canonical.Value(legal.Select(Simulation.TaskKey).ToArray()))==record.LegalShas[i],
                    "snapshot/clone future legal task hash");
                Check(Simulation.Process(continuation,legal[record.Actions[i]]),"recorded future action accepted");
                Check(continuation.HistoryFacts.CanonicalText()==facts[i],"snapshot/clone entire future fact suffix identical");
            }
            var outcome=new GameRecord();Simulation.Finish(outcome,continuation);
            Check(continuation.State==State.COMPLETE && outcome.StateSha==record.StateSha && outcome.Winner==record.Winner
                && outcome.Turns==record.Turns,"snapshot/clone future state and winner identical");
        }
        copy.PoolManifestHash="wrong";bool rejected=false;
        try {Simulation.Run(copy,replay:true);}catch(InvariantException e){rejected=e.Invariant=="replay.pool";}
        Check(rejected,"schema-3 mismatched pool identity rejected");
        Console.WriteLine("PASS history replay: schema-3 recorded reexecution, per-action CanonicalText, clone/snapshot future suffix, hashes, winner, pool rejection");
    }
}
