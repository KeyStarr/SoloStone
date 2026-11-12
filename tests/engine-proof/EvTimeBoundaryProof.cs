using System.Reflection;
using System.Xml.Linq;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class EvTimeBoundaryProof
{
    private static void Check(bool ok, string message) => Core2026Proof.Check(ok, message);
    private static Game New(int seed = 1) => EvTimeProof.Fixture(seed);
    private static void Answer(Game g, int id) => EvTimeProof.Answer(g, id);
    private static void Ends(Game g, int n) { for (int i = 0; i < n; i++) End(g); }
    private static (IPlayable Fuel, Minion Source) Fire(Game g, string fuel = "GAME_005")
    {
        var p = g.CurrentPlayer; var card = Add(p, fuel); var source = (Minion)Play(g, p, "CATA_EVENT_001"); Answer(g, card.Id);
        return (card, source);
    }
    private static void Unpinned_PhoenixLifecycle()
    {
        foreach (string exit in new[] { "play", "discard", "shuffle", "runi" })
        {
            var g = New(); var p = g.Player1; var (fuel, source) = Fire(g, "CS2_231");
            if (exit == "play") { PlayEntity(g,p,fuel); Generic.ReturnToHandBlock(p,(Minion)fuel); }
            if (exit == "discard") Generic.DiscardBlock(p, fuel);
            if (exit == "shuffle") p.DeckZone.Add(p.HandZone.Remove(fuel));
            if (exit == "runi") Play(g,p,"TIME_EVENT_998");
            Kill(g,source); Ends(g,5);
            Check(p.BoardZone.All(m => m.Card.Id != "CATA_EVENT_001") && !HandTimelineRules.IsBurning(fuel), "Phoenix cancels on " + exit);
        }
        foreach (bool crossType in new[] { false, true })
        {
            var g = New(); var p = g.Player1; var (fuel, source) = Fire(g, "CS2_231");
            Ends(g,2);
            var transformed = Generic.ChangeEntity(p, fuel, Cards.FromId(crossType ? "GAME_005" : "CS2_182"), true);
            Check(HandTimelineRules.IsBurning(transformed) && transformed.Id == fuel.Id, "same-entity hand transform retains Phoenix fire");
            Kill(g,source); Ends(g,3);
            Check(transformed.Zone == p.GraveyardZone && p.BoardZone.Single().Card.Id == "CATA_EVENT_001", "transformed fuel expires at original countdown");
        }
    }
    private static void Unpinned_PhoenixCopiesBrannCapacity()
    {
        var g = New(); var p = g.Player1; var (fuel, source) = Fire(g);
        Ends(g,2); var copy = Generic.Copy(p, source, fuel, Zone.HAND);
        Check(copy.Id != fuel.Id && HandTimelineRules.IsBurning(copy), "copied fuel independently burning");
        Generic.DiscardBlock(p,fuel); Kill(g,source); Ends(g,3);
        Check(copy.Zone == p.GraveyardZone && p.BoardZone.Single().Card.Id == "CATA_EVENT_001", "copied countdown survives original discard");

        g = New(); p = g.Player1; Summon(p,"LOE_077");
        var a = Add(p,"GAME_005"); var b = Add(p,"CS2_231"); source = (Minion)Play(g,p,"CATA_EVENT_001");
        Answer(g,a.Id);
        Check(p.Choice.Choices.SequenceEqual(new[] { b.Id }), "Brann excludes already burning card");
        Answer(g,b.Id); Ends(g,5);
        Check(a.Zone == p.GraveyardZone && b.Zone == p.GraveyardZone && p.BoardZone.Count(m => m.Card.Id == "CATA_EVENT_001") == 3, "Brann independent schedules both summon");

        g = New(); p = g.Player1; (fuel,source) = Fire(g);
        while (!p.BoardZone.IsFull) Summon(p,"CS2_231"); Ends(g,5);
        Check(fuel.Zone == p.GraveyardZone && p.BoardZone.Count(m => m.Card.Id == "CATA_EVENT_001") == 1, "full board discards fuel but loses summon");
        g = New(); p = g.Player1; Play(g,p,"CATA_EVENT_001");
        Check(p.Choice == null && p.BoardZone.Count == 1, "empty hand Phoenix legal, no decision");
    }
    private static void Unpinned_RuniOverflowOrderCancellation()
    {
        foreach (bool full in new[] { false, true })
        {
            var g = New(); var p = g.Player1;
            var a = Add(p,"CS2_231"); var b = Add(p,"CS2_182"); var removed = Add(p,"CS2_200");
            Play(g,p,"TIME_EVENT_998");
            p.GraveyardZone.Add(p.SetasideZone.Remove(removed));
            Ends(g,2);
            while(p.HandZone.Count < (full ? 10 : 9)) Add(p,"GAME_005");
            Ends(g,2);
            Check(a.Zone.Type == (full ? Zone.GRAVEYARD : Zone.HAND) && b.Zone == p.GraveyardZone && removed.Zone == p.GraveyardZone,
                "Runi ordered return before draw, overflow lost, moved payload canceled");
            var count=p.HandZone.Count; Ends(g,2);
            Check(p.HandZone.Count == count && p.SetasideZone.All(c => c.Id != a.Id && c.Id != b.Id), "Runi no deferred overflow retry");
        }
        var empty=New(); Play(empty,empty.Player1,"TIME_EVENT_998");
        Check(empty.Player1.AppliedEnchantments?.Any(e=>e.Card.Id=="TIME_EVENT_998e1") != true,"empty Runi allocates no schedule");
    }
    private static void Unpinned_RollbackAndConcede()
    {
        foreach (string id in new[] { "TIME_EVENT_998", "CATA_EVENT_001" })
        {
            var g=New(); var p=g.Player1; var fuel=Add(p,"CS2_231"); var source=Add(p,id); source[GameTag.REWIND]=1;
            PlayEntity(g,p,source); if(id=="CATA_EVENT_001") Answer(g,fuel.Id);
            Check(g.Rewind != null,"timeline Rewind offered");
            Answer(g,g.Rewind.RewindEntityId); p=g.Player1;
            if(id=="CATA_EVENT_001") Answer(g,fuel.Id);
            Check(g.Rewind==null && p.UsedMana==source.Card.Cost && p.NumCardsPlayedThisTurn==1,"timeline rollback single net play");
            if(id=="TIME_EVENT_998") {
                Check(p.AppliedEnchantments.Count(e=>e.Card.Id=="TIME_EVENT_998e1")==1,"rollback one Runi batch");
                Ends(g,4); Check(((Minion)g.IdEntityDic[fuel.Id]).AttackDamage==6,"rollback no double Runi buff");
            } else {
                Check(g.IdEntityDic[fuel.Id].AppliedEnchantments.Count(e=>e.Card.Id=="CATA_EVENT_001e")==1,"rollback one ignition");
                Ends(g,5); Check(p.BoardZone.Count(m=>m.Card.Id==id)==2,"rollback one Phoenix summon");
            }
        }
        foreach(string id in new[]{"CATA_EVENT_001","TIME_EVENT_999"}) {
            var g=New(); Add(g.Player1,"GAME_005"); Play(g,g.Player1,id);
            Check(g.Player1.Choice!=null && g.Process(ConcedeTask.Any(g.Player1)) && g.State==State.COMPLETE,"Concede while timeline choice pending");
        }
    }
    private static void Unpinned_SandsPoolsRng()
    {
        var shapes=new HashSet<string>();
        foreach(int seed in new[]{3,17,41}) {
            var g=New(seed); var p=g.Player1; var source=Add(p,"TIME_EVENT_999");
            var expected=g.CloneExact(); var pool=OfflineCardPools.Candidates(expected,p.HeroClass,SandsOfTime.AnyClassSpells(),121675).ToArray();
            Check(pool.Length>=3 && pool.All(c=>c.Class!=CardClass.NEUTRAL),"Sands actual pool class domain");
            var indices=Enumerable.Range(0,pool.Length).ToArray(); var ids=new List<string>();
            for(int i=0;i<3;i++){ int j=i+expected.Random.Next(indices.Length-i); (indices[i],indices[j])=(indices[j],indices[i]); ids.Add(pool[indices[i]].Id); }
            PlayEntity(g,p,source);
            var actual=p.Choice.Choices.Select(x=>g.IdEntityDic[x].Card.Id).ToArray();
            Check(actual.SequenceEqual(ids),"Sands seeded ordered distinct sample, not always-first"); shapes.Add(string.Join(",",actual));
            string chosen=g.IdEntityDic[p.Choice.Choices[1]].Card.Id; Answer(g,p.Choice.Choices[1]); Answer(g,g.Rewind.KeepEntityId);
            Check(p.HandZone.Single().Card.Id==chosen,"Sands Keep commits selected spell");
        }
        Check(shapes.Count==3,"Sands seeds vary offers");
        var fixture=New();
        Console.WriteLine("Sands any-class bucket="+OfflineCardPools.Candidates(fixture,CardClass.ROGUE,SandsOfTime.AnyClassSpells(),121675).Count);
        foreach(var cls in SandsOfTime.Classes) Console.WriteLine("Sands "+cls+" bucket="+OfflineCardPools.Candidates(fixture,cls,SandsOfTime.ClassSpells(cls),122549).Count);
    }
    private static void Unpinned_ShortEmptyFullAndMorchie()
    {
        foreach(int count in new[]{0,1,2}) {
            var g=New(); var p=g.Player1;
            var catalog=PinnedPoolFixture.Catalog();
            var members=catalog.Root.Element("Members");
            var keep=members.Elements("Card").Where(x=>(string)x.Attribute("scope")=="Standard" && (string)x.Attribute("type")=="SPELL"
                && x.Elements("Class").Any(c=>(string)c.Attribute("name")=="ROGUE")).Take(count).ToArray();
            members.Elements("Card").Where(x=>(string)x.Attribute("scope")=="Standard" && !keep.Contains(x)).Remove();
            var manifest=OfflinePoolPolicy.Parse(catalog).BuildManifest();
            typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(g,manifest);
            Play(g,p,"TIME_EVENT_999");
            Check(count==0 ? g.Rewind!=null : p.Choice.Choices.Count==count,"Sands exact short/empty offer count");
            if(count>0) Answer(g,p.Choice.Choices.Last());
            Answer(g,g.Rewind.RewindEntityId); p=g.Player1;
            if(count>0) Answer(g,p.Choice.Choices.First());
            Check(g.Rewind==null && p.HandZone.Count==(count==0?0:1),"Sands empty/short replay result");
        }
        var full=New(); var owner=full.Player1; Play(full,owner,"TIME_EVENT_999");
        while(!owner.HandZone.IsFull) Add(owner,"GAME_005");
        int chosen=owner.Choice.Choices[0]; Answer(full,chosen); Answer(full,full.Rewind.KeepEntityId);
        Check(owner.HandZone.Count==10 && full.IdEntityDic[chosen].Zone==owner.GraveyardZone,"Sands full hand loses chosen card");

        var both=New(); owner=both.Player1;
        QuestRules.AddPlayerEffect(owner,owner.Hero,"END_036e"); Play(both,owner,"TIME_EVENT_999");
        Answer(both,owner.Choice.Choices[0]); Answer(both,both.Rewind.RewindEntityId);
        Check(owner.Choice.Choices.All(x=>OfflineCardPools.Candidates(both,owner.HeroClass,SandsOfTime.ClassSpells(owner.HeroClass),122549)
            .Any(c=>c.Id==both.IdEntityDic[x].Card.Id)),"Morchie second outcome still uses YOUR class");
        Answer(both,owner.Choice.Choices[0]);
        Check(owner.HandZone.Count==2 && owner.UsedMana==1,"shared Morchie exception retains both outcomes once");
    }
    private static void Unpinned_TransferAndExtraTurns()
    {
        bool transferred=false;
        for(int seed=1;seed<=10 && !transferred;seed++) {
            var g=New(seed); var p=g.Player1; var (fuel,source)=Fire(g);
            foreach(var old in p.Opponent.HandZone.ToArray()) p.Opponent.SetasideZone.Add(p.Opponent.HandZone.Remove(old));
            Add(p.Opponent,"GAME_005"); SwapRules.ShuffleHands(p);
            if(fuel.Controller==p) continue;
            transferred=true; Kill(g,source); Ends(g,5);
            Check(fuel.Zone==p.Opponent.HandZone && fuel.AppliedEnchantments.Single()[GameTag.TAG_SCRIPT_DATA_NUM_2]==1,"transferred fire ticks new holder only");
            End(g);
            Check(fuel.Zone==p.Opponent.GraveyardZone && p.Opponent.BoardZone.Single().Card.Id=="CATA_EVENT_001","transferred fire summons for holder");
        }
        Check(transferred,"seed fixture exercises hand theft");
        var extra=New(); var owner=extra.Player1; var card=Add(owner,"CS2_231"); Play(extra,owner,"TIME_EVENT_998");
        TurnQueueRules.Prepend(extra,owner,owner); End(extra);
        Check(card.Zone==owner.SetasideZone,"Runi first extra owner turn still delayed"); End(extra);
        Check(card.Zone==owner.HandZone && ((Minion)card).AttackDamage==6,"Runi second extra owner turn returns");
    }
    [GroupProof("ev-time-boundaries")]
    internal static void Run()
    {
        var errors=new List<string>();
        foreach(var proof in new Action[]{Unpinned_PhoenixLifecycle,Unpinned_PhoenixCopiesBrannCapacity,Unpinned_RuniOverflowOrderCancellation,Unpinned_RollbackAndConcede,Unpinned_SandsPoolsRng,Unpinned_ShortEmptyFullAndMorchie,Unpinned_TransferAndExtraTurns,B14F1HistoryDefinitionProof.Run})
            try { proof(); Console.WriteLine("PASS "+proof.Method.Name); } catch(Exception e) { errors.Add(proof.Method.Name+": "+e); }
        Check(errors.Count==0,string.Join("\n",errors));
    }
}
