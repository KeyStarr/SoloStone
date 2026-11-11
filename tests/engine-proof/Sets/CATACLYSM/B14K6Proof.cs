using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static DiscoverRealProof;

internal static class B14K6CataclysmProof
{
    internal static Game NewRogue(long seed) { var g=DiscoverRealProof.New(seed); g.Player1.AddHeroAndPower(Cards.FromId("HERO_03")); return g; }
    internal static void Run()
    {
        StandardSetsProof.Case("CATA_202", Unpinned_StolenPower);
        StandardSetsProof.Case("CATA_786", Unpinned_ChaosSupplicant);
    }
    internal static OfflinePoolFilter Filter(string id) => OfflinePoolDeclarations.Cards.Single(d=>d.CardId==id).Pools.Single().Filter;
    internal static Card[] Pool(Game g,string id,int? cost=null) => OfflineCardPools.Candidates(g,g.Player1.HeroClass,
        cost.HasValue?Filter(id).WithCost(cost.Value):Filter(id),Cards.FromId(id).AssetId).ToArray();
    // card-state/shatter-stolen-{already-combined,export-pool,later-resplit,runtime-pool}.yaml:
    // PINNED/PINNED-RULE-ONLY/owner-approved assumptions; tier C wiki.gg/Shatter, Stolen_Power; tier D export.
    private static void Unpinned_StolenPower(Game g,Controller p,string id)
    {
        foreach(string hero in new[]{"HERO_03","HERO_08"})
        {
            p.AddHeroAndPower(Cards.FromId(hero));
            var pool=Pool(g,id);
            Core2026Proof.Check(pool.Length>=3 && pool.All(c=>c.Shatter?.ParentId==c.Id && c.Class!=p.HeroClass && c.Class!=CardClass.NEUTRAL && c.Id!=id),"admitted other-class Shatter parents only");
            var expected=g.CloneExact(history:true); string wanted=pool[expected.Random.Next(pool.Length)].Id;
            var source=Play(g,p,id); var result=p.HandZone.Single();
            Core2026Proof.Check(result.Card.Id==wanted && result.Card.Type==CardType.SPELL && result.Cost==result.Card.Cost
                && result[ShatterRules.AlreadyCombined]==1 && result[(GameTag)4239]==0,
                "seeded uniform parent; combined before initial hand entry");
            Saved(g,next=>{
                var owner=next.Player1;var parent=next.IdEntityDic[result.Id];
                var copy=Generic.Copy(owner,next.IdEntityDic[source.Id],parent,Zone.HAND);
                owner.DeckZone.Add(owner.HandZone.Remove(copy));Generic.Draw(owner,copy);
                Core2026Proof.Check(copy.Card.Id==wanted && copy[ShatterRules.AlreadyCombined]==1 && owner.HandZone.Count==2,
                    "combined copy survives deck return and redraw with room for halves");
            });
            foreach(var held in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(held));
        }
        for(int occupied=9;occupied<=10;occupied++)
        {
            var next=NewRogue(g.Seed);var owner=next.Player1;
            while(owner.HandZone.Count<occupied)Add(owner,"CS2_182");
            var source=(Spell)Entity.FromCard(owner,Cards.FromId(id));Generic.CastSpell(owner,next,source,null,0);
            Core2026Proof.Check(owner.HandZone.Count==10 && owner.HandZone.Count(c=>c.Card.Shatter!=null)==(occupied==9?1:0),"combined generation respects hand capacity");
        }
        var empty=NewRogue(g.Seed);
        LostCityPoolProof.WithPool(empty,id,Array.Empty<string>(),()=>{
            Play(empty,empty.Player1,id);Core2026Proof.Check(empty.Player1.HandZone.IsEmpty,"empty Shatter pool gives nothing");
        });
    }
    // cost-sampling/{aftercast-paid-cost,effect-cast-qualification}.yaml and auto-cast/random-cast-target-fizzle.yaml:
    // owner-approved extension to Supplicant; tier C Advanced_rulebook/Cast_spell/Yogg; tier E forum reports.
    // card-state/shatter-stolen-runtime-pool.yaml supplies current-controller-class exclusion (tier C/D; approved assumption).
    // D2: exact empty bucket produces nothing, never nearest Cost.
    private static void Unpinned_ChaosSupplicant(Game original,Controller ignored,string id)
    {
        var sizes=Enumerable.Range(0,31).Select(cost=>Pool(original,id,cost).Length).ToArray();
        Console.WriteLine("K6 Supplicant Rogue bucket sizes 0..30: "+string.Join(",",sizes));
        Core2026Proof.Check(sizes[1]>=3 && sizes[30]==0,"ordinary 1-Cost bucket; exact empty 30-Cost bucket");
        foreach(int paid in new[]{0,1,2,30})
        {
            var g=NewRogue(original.Seed);var p=g.Player1;var source=Summon(p,id);
            var spell=Add(p,"CS2_005");spell.Cost=paid;
            var candidates=Pool(g,id,paid);var expected=g.CloneExact(history:true);
            string wanted=candidates.Length==0?null:candidates[expected.Random.Next(candidates.Length)].Id;
            if(paid==30) { p.BaseMana=30;p.UsedMana=0;Core2026Proof.Check(g.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(p,spell)),"30-Cost paid spell"); }
            else { p.BaseMana=10;p.UsedMana=0;Core2026Proof.Check(g.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(p,spell)),"modified-Cost spell accepted"); }
            var casts=g.HistoryFacts.Query.Casts().Where(f=>f.CasterKind==HistoryCastKind.Effect && f.Outcome==HistoryCastOutcome.Started).ToArray();
            Core2026Proof.Check(wanted==null?casts.Length==0:casts.Length>0 && casts[0].DbfId==Cards.FromId(wanted).AssetId,
                "committed modified Cost, seeded pool choice or empty bucket; no recursive player trigger");
            if(wanted!=null)Core2026Proof.Check(Cards.FromAssetId(casts[0].DbfId).Cost==paid,"generated spell exact printed Cost");
            var generated=g.IdEntityDic.Values.Where(c=>c[GameTag.CREATOR]==source.Id && c.Card.Type==CardType.SPELL).ToArray();
            Core2026Proof.Check(generated.Length==(wanted==null?0:1) && generated.All(c=>{
                var member=g.OfflinePools.Standard.Single(m=>m.CardId==c.Card.Id);
                return c.Card.Cost==paid && c.Card.Id!=id && !member.Classes.Contains("NEUTRAL") && !member.Classes.Contains(p.HeroClass.ToString());
            }),"one root spell from exact-Cost other-class pool; source excluded");
        }
        foreach(string inactive in new[]{"silence","leave","opponent","effect"})
        {
            var g=NewRogue(original.Seed);var p=g.Player1;var source=Summon(p,id);
            if(inactive=="silence")source.Silence();
            if(inactive=="leave")p.SetasideZone.Add(p.BoardZone.Remove(source));
            if(inactive=="opponent") { End(g);p=p.Opponent; }
            if(inactive=="effect")Generic.CastSpell(p,g,(Spell)Entity.FromCard(p,Cards.FromId("CS2_005")),null,0);
            else Play(g,p,"CS2_005");
            Core2026Proof.Check(g.HistoryFacts.Query.Casts().Count(f=>f.CasterKind==HistoryCastKind.Effect && f.Outcome==HistoryCastOutcome.Started)==(inactive=="effect"?1:0),"no trigger for "+inactive);
        }
        var saved=NewRogue(original.Seed);Summon(saved.Player1,id);End(saved);End(saved);
        Saved(saved,next=>{
            LostCityPoolProof.WithPool(next,id,new[]{"CORE_CS2_004"},()=>{
                var a=Summon(next.Player1,"CS2_182");var b=Summon(next.Player2,"CS2_231");
                var extra=Summon(next.Player1,"CS2_200");
                if(original.Seed%2==0)next.Player1.BoardZone.Swap(a,extra);
                var pool=Pool(next,id,1);var expected=next.CloneExact(history:true);expected.Random.Next(pool.Length);
                var probe=(Spell)Entity.FromCard(expected.Player1,Cards.FromId("CORE_CS2_004"));
                var legal=probe.ValidPlayTargets.Where(probe.IsValidPlayTarget).ToArray();int wanted=legal[expected.Random.Next(legal.Length)].Id;
                Play(next,next.Player1,"CS2_005");
                Core2026Proof.Check(((Minion)next.IdEntityDic[wanted]).Health==((Minion)next.IdEntityDic[wanted]).Card.Health+2,"seeded legal automatic target on reordered board; clone trigger survives turns");
                Core2026Proof.Check(next.HistoryFacts.Query.Casts().Count(f=>f.DbfId==Cards.FromId("CORE_CS2_004").AssetId && f.Outcome==HistoryCastOutcome.Started)==1,"generated spell casts once");
            });
        });
        var fizzle=NewRogue(original.Seed);Summon(fizzle.Player1,id);
        LostCityPoolProof.WithPool(fizzle,id,new[]{"CORE_CS2_108"},()=>{
            Play(fizzle,fizzle.Player1,"CS2_005");
            Core2026Proof.Check(fizzle.Player1.BoardZone.Single().Card.Id==id && fizzle.Player2.BoardZone.IsEmpty
                && fizzle.Player1.GraveyardZone.Any(c=>c.Card.Id=="CORE_CS2_108"),"no damaged target: chosen Execute fizzles without reroll");
        });
    }
}
