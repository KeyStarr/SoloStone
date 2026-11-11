using System.Xml.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B18ONativeProof
{
    [GroupProof("b18-o-native")]
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var test in new Action[] { Unpinned_PackCollation, Nozdormu, Illusionist, Guard, B18ONativeIllusionProof.Unpinned_Edges, B18ONativeGuardProof.Unpinned_Edges, Unpinned_RewindPreservesState, Unpinned_AvatarCapacity })
            try { test(); Console.WriteLine("PASS " + test.Method.Name); }
            catch (Exception e) { failures.Add(test.Method.DeclaringType.Name + "." + test.Method.Name + ": " + (e.InnerException ?? e)); }
        Core2026Proof.Check(failures.Count == 0, string.Join("; ", failures));
    }
    internal static Game Start(bool first, bool second)
    {
        var a = Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList(); var b = a.ToList();
        if(first) a[0]=Cards.FromId("CS3_035"); if(second) b[0]=Cards.FromId("CS3_035");
        var g=new Game(new GameConfig { RandomSeed=17, StartPlayer=1, Shuffle=false, SkipMulligan=true,
            History=true, Logging=false, Player1Deck=a, Player2Deck=b }); g.StartGame(); return g;
    }
    internal static void Nozdormu()
    {
        foreach(bool a in new[]{false,true}) foreach(bool b in new[]{false,true})
        {
            var g=Start(a,b);
            Core2026Proof.Check((g.Player1[GameTag.TIMEOUT]==15)==(a&&b) && (g.Player2[GameTag.TIMEOUT]==15)==(a&&b),"both initial decks gate timeout");
            foreach(var next in B16AutoplayProof.Futures(g))
            {
                Core2026Proof.Check(next.Player1[GameTag.TIMEOUT]==g.Player1[GameTag.TIMEOUT],"saved timeout");
                var m=Summon(next.Player1,"CS3_035"); m.Silence(); m.Destroy(); next.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(next.Player1[GameTag.TIMEOUT]==g.Player1[GameTag.TIMEOUT],"Nozdormu lifecycle cannot toggle timer");
            }
        }
    }
    internal static void Unpinned_PackCollation()
    {
        foreach (int seed in new[] { 1, 8, 19, 37 })
        foreach (bool reverse in new[] { false, true })
        {
            var g=LocationProofs.Game(seed);
            if(reverse)
            {
                var xml=OfflinePoolFixtures.Catalog(); var rows=xml.Root.Element("Members");
                rows.ReplaceNodes(rows.Elements().Reverse().ToArray());
                typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(g,OfflinePoolFixtures.Manifest(xml));
            }
            g.SetRandomSeed(seed); var random=g.Random.Clone(); var expected=new List<string>();
            for(int slot=0;slot<5;slot++)
            {
                int roll=random.Next(100);
                string rarity=slot==0 ? roll<75?"RARE":roll<95?"EPIC":"LEGENDARY"
                    :roll<75?"COMMON":roll<95?"RARE":roll<99?"EPIC":"LEGENDARY";
                var pool=g.OfflinePools.Standard.Where(m=>m.Set!="CORE" && m.Set!="EVENT" && m.Rarity==rarity && m.DbfId!=127595).OrderBy(m=>m.DbfId).ToArray();
                if(pool.Length>0)expected.Add(pool[random.Next(pool.Length)].CardId);
            }
            foreach(var next in B16AutoplayProof.Futures(g))
            {
                var pack=OfflinePoolPolicy.OpenStandardPack(next,127595);
                Core2026Proof.Check(pack.Select(c=>c.Id).SequenceEqual(expected),"seeded ordered pack identities seed="+seed);
                Core2026Proof.Check(next.Random.CaptureState().Equals(random.CaptureState()),"pack exact RNG consumption");
                Core2026Proof.Check(pack.Length==5 && pack[0].Rarity!=Rarity.COMMON,"five cards with rare-or-better slot");
            }
        }
        foreach(var rarity in new[]{Rarity.COMMON,Rarity.RARE,Rarity.EPIC,Rarity.LEGENDARY})
            Console.WriteLine("PACK BUCKET "+rarity+"="+OfflineCardPools.Candidates(LocationProofs.Game(),CardClass.NEUTRAL,OfflinePoolPolicy.StandardPackFilter(rarity),127595).Count);
        var empty=LocationProofs.Game(); var fixture=OfflinePoolFixtures.Catalog();
        fixture.Root.Element("Members").Elements().Where(e=>(string)e.Attribute("set")!="CORE").Remove();
        // Dependencies still reference one another, so retain their rows; only generator membership shrinks.
        typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(empty,OfflinePoolFixtures.Manifest(fixture));
        Core2026Proof.Check(OfflinePoolPolicy.OpenStandardPack(empty,127595).Length==0,"empty exact rarity buckets yield nothing");
    }

    internal static void Illusionist()
    {
        var g=LocationProofs.Game(17); var p=g.Player1;
        Play(g,p,"EDR_780");
        Core2026Proof.Check(p.BoardZone.Count==2,"Illusionist summons copy");
        var bodies=p.BoardZone.ToArray();
        foreach(var next in B16AutoplayProof.Futures(g))
        {
            foreach(var body in bodies) ((Minion)next.IdEntityDic[body.Id]).TakeDamage(next.Player2.Hero,1);
            next.ProcessTasks(); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(next.Player1.BoardZone.Count==1,"exactly one designated body dies on damage after clone/load");
        }
    }
    internal static void Guard()
    {
        var g=LocationProofs.Game(); var p=g.Player1;
        var guard=Summon(p,"JAIL_703"); guard.Destroy();g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p[(GameTag)33200]==1 && p.Opponent[(GameTag)33200]==0,"Sorry scoped to Deathrattle controller");
        foreach(var next in B16AutoplayProof.Futures(g)) Core2026Proof.Check(next.Player1[(GameTag)33200]==1,"saved Sorry unlock");
    }
    internal static void Unpinned_RewindPreservesState()
    {
        var g=Start(true,true);var p=g.Player1;p.BaseMana=10;
        var guard=Summon(p,"JAIL_703");Kill(g,guard);End(g);End(g);
        var source=(Minion)Play(g,p,"EDR_780");
        var ids=p.BoardZone.Select(m=>m.Id).ToArray();
        string Flags(Game game)=>string.Join(",",ids.SelectMany(id=>PayloadRules.Entries(game.IdEntityDic[id],NativeTailRules.IllusionKey)).Select(e=>e[PayloadRules.Value]));
        string flags=Flags(g);
        Play(g,p,"TIME_001");
        Core2026Proof.Check(g.Rewind!=null,"real Rewind offer");
        var d=g.GetPendingDecisions().Single();
        Core2026Proof.Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(d.Id,d.ControllerId,new[]{g.Rewind.RewindEntityId}),out var error),"Rewind answer "+error);
        Core2026Proof.Check(g.Player1[NativeTailRules.SorryEnabled]==1 && g.Player1[GameTag.TIMEOUT]==15 && Flags(g)==flags,"Rewind preserves prior controller and private pair state");
        if(g.Rewind!=null)
        {
            d=g.GetPendingDecisions().Single();Core2026Proof.Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(d.Id,d.ControllerId,new[]{g.Rewind.KeepEntityId}),out error),"keep retried spell");
        }
    }
    internal static void Unpinned_AvatarCapacity()
    {
        foreach(bool fullBoard in new[]{false,true})
        {
            var g=LocationProofs.Game(19);var p=g.Player1;
            var avatar=Summon(p,"CORE_WON_145");
            while(!p.HandZone.IsFull)Add(p,"CS2_182");
            if(fullBoard)while(!p.BoardZone.IsFull)Summon(p,"CS2_182");
            g.SetRandomSeed(19);var expected=OfflinePoolPolicy.OpenStandardPack(g.CloneExact(true),127595).Select(c=>c.Id).ToArray();
            foreach(var next in B16AutoplayProof.Futures(g))
            {
                var owner=next.Player1;var a=next.IdEntityDic[avatar.Id];
                a.Power.PowerTask.Process(next,owner,a,null,new TaskStack());
                next.ProcessTasks();next.DeathProcessingAndAuraUpdate();
                var generated=next.IdEntityDic.Values.Where(e=>e[GameTag.DISPLAYED_CREATOR]==a.Id).OrderBy(e=>e.Id).Take(5).ToArray();
                Core2026Proof.Check(generated.Select(e=>e.Card.Id).SequenceEqual(expected),"full hand/board still attempts five ordered pack cards");
                Core2026Proof.Check(!next.Resolution.HasSuspendedEffect && owner.Choice==null,"pack cannot leave decisions pending");
                Core2026Proof.Check(owner.UsedMana==0,"automatic pack spends no mana");
            }
        }
    }

}
