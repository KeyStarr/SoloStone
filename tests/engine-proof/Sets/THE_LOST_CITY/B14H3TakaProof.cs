using System.Linq;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static B14H3LifecycleProof;
using static StandardSetsProof;

internal static class B14H3TakaProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_430",Unpinned_TakaStorageReconcile);
    private static void Unpinned_TakaStorageReconcile(Game g,Controller p,string id)
    {
        p.ExtraBattlecry = true;
        var source = Add(p,id); PoolPilotProof.FillHand(p);
        PlayEntity(g,p,source);
        B14H3LifecycleProof.Check(p.Choice != null && p.HandZone.Count == 9,"Discover with full pre-play hand");
        var first = g.IdEntityDic[p.Choice.Choices[0]].Card;
        B14H3LifecycleProof.Check(p.Choice.Choices.All(x => g.IdEntityDic[x].Card.ColossalAppendageIds == null),"37.0 excludes Colossal");
        Answer(g); var second = g.IdEntityDic[p.Choice.Choices[0]].Card; Answer(g); p.ExtraBattlecry = false;
        B14H3LifecycleProof.Check(p.HandZone.Count == 9 && PayloadRules.Entries(source,1).Length == 2,"stores two definitions without hand delivery");
        B14H3LifecycleProof.Check(((Minion)source).AttackDamage == 2+first.ATK+second.ATK && ((Minion)source).Health == 2+first.Health+second.Health,
            "additive stats use buff layer");
        var copy = (Minion)Generic.Copy(p,p.Hero,source,Zone.PLAY);
        B14H3LifecycleProof.Check(PayloadRules.Entries(copy,1).Length == 2,"exact board copy retains memory");
        copy.Silence(); B14H3LifecycleProof.Check(copy.AttackDamage == 2 && copy.Health == 2 && PayloadRules.Entries(copy).Length == 0,"Silence clears stats/memory");
        ParkBoard(p);
        // Put original back without resetting its state; death instance remains the resurrection source.
        var location = LocationProofs.Location(p); var left = Summon(p,"CS2_182");
        Generic.SummonBlock(g,(Minion)p.SetasideZone.Remove(source),2,p.Hero);
        var right = Summon(p,"CS2_182"); g.DeathProcessingAndAuraUpdate();
        PoolPilotProof.Resume(g,next => {
            var taka = (Minion)next.IdEntityDic[source.Id]; Kill(next,taka);
            var summons = next.Player1.BoardZone.Where(x => x.Id != left.Id && x.Id != right.Id).ToArray();
            B14H3LifecycleProof.Check(summons.Select(x => x.Card.Id).SequenceEqual(new[] {first.Id,second.Id}),"ordered fresh Deathrattle summons");
            CheckDeathrattleSlot(next.Player1,((BoardObject)next.IdEntityDic[location.Id],(Minion)next.IdEntityDic[left.Id],(Minion)next.IdEntityDic[right.Id]),summons,
                "Taka uses physical middle death slot with a location");
            ParkBoard(next.Player1);
            PoolPilotProof.Resolve(next,taka,death:true);
            B14H3LifecycleProof.Check(next.Player1.BoardZone.Select(x => x.Card.Id).SequenceEqual(new[] {first.Id,second.Id}),"repeated Deathrattle reuses memory");
            ParkBoard(next.Player1);
            var resurrected = (Minion)Generic.Copy(next.Player1,next.Player1.Hero,taka,Zone.PLAY);
            B14H3LifecycleProof.Check(resurrected.AttackDamage == 2 && resurrected.Health == 2 && PayloadRules.Entries(resurrected,1).Length == 2,
                "death-instance resurrection retains only memory, printed stats");
            Kill(next,resurrected);
            B14H3LifecycleProof.Check(next.Player1.BoardZone.Select(x => x.Card.Id).SequenceEqual(new[] {first.Id,second.Id}),"resurrected Taka Deathrattle reuses memory");
        });
        ParkBoard(p); var fresh = Summon(p,id); Kill(g,fresh); B14H3LifecycleProof.Check(p.BoardZone.IsEmpty,"unlinked fresh Taka has no payload");
        var dead = (Minion)g.IdEntityDic[source.Id];
        new SummonCopyTask(EntityType.TARGET).Process(g,p,p.Hero,dead,new TaskStack());
        var revived = p.BoardZone.Single();
        B14H3LifecycleProof.Check(PayloadRules.Entries(revived,1).Length == 2,"SummonCopyTask death-instance resurrection retains memory");
        while (!p.BoardZone.IsFull) Summon(p,"CS2_182");
        PoolPilotProof.Resolve(g,revived,death:true); B14H3LifecycleProof.Check(p.BoardZone.Count == 7,"full board loses payload summon attempts");
        DeathInstanceResummons();
    }
    private static void DeathInstanceResummons()
    {
        var failures = new System.Collections.Generic.List<string>();
        foreach (bool history in new[] { false,true })
        foreach (bool opponent in new[] { false,true })
        foreach (bool reborn in new[] { true,false })
        {
            var game = HistoryFactsProof.Fixture(history); if (opponent) End(game);
            var p = game.CurrentPlayer; var taka = (Minion)Play(game,p,"DINO_430");
            string stored = game.IdEntityDic[p.Choice.Choices[0]].Card.Id; Answer(game);
            PayloadRules.StoreCard(taka,99,Cards.FromId("CS2_231"));
            if (reborn) Play(game,p,"CAP_804",taka);
            foreach (var next in new[] { game.CloneExact(history:history),Game.LoadSnapshot(game.SaveSnapshot()),game })
            {
                var owner = next.CurrentPlayer; Kill(next,(Minion)next.IdEntityDic[taka.Id]);
                if (!reborn) { ParkBoard(owner); Kill(next,Summon(owner,"EDR_892")); }
                var returned = owner.BoardZone.Where(m => m.Card.Id == "DINO_430").ToArray();
                string path = (opponent ? "opponent " : "friendly ")+(reborn ? "Reborn" : "Ferocious Felbat");
                bool retained = returned.Length == (reborn ? 1 : 2) && returned.All(m =>
                    PayloadRules.Entries(m,1).Select(e => e.Card.Id).SequenceEqual(new[] {stored})
                    && PayloadRules.Entries(m,99).Length == 0 && m.AttackDamage == 2 && m.Health == (reborn ? 1 : 2));
                if (!retained) failures.Add(path+": memories="+string.Join(",",returned.Select(m => PayloadRules.Entries(m,1).Length)));
                ParkBoard(owner);
                foreach (var body in returned)
                {
                    Generic.SummonBlock(next,(Minion)owner.SetasideZone.Remove(body),-1,owner.Hero);
                    Kill(next,body);
                }
                if (!owner.BoardZone.Select(m => m.Card.Id).SequenceEqual(Enumerable.Repeat(stored,returned.Length)))
                    failures.Add(path+": returned Deathrattles lost stored summons");
            }
        }
        B14H3LifecycleProof.Check(failures.Count == 0,"death-instance resummons retain only opted-in memory: "+string.Join("; ",failures));
    }
}
