using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static partial class B14H4IdentityProof
{
    [GroupProof("b14-h4-identity-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("CORE_CS2_023", ArcaneIntellect, legacyPoolMember: true);
        StandardSetsProof.Case("CORE_BOT_256", Astromancer);
        EmeraldDream(); TimeTravel(); Beyond(); Cataclysm(); VioletHold();
    }
    internal static void Future(Game g, Action<Game> action) => B14K3BEProof.Future(g, action);
    internal static void EmptyHand(Controller p)
    { foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card)); }
    internal static void Pools(Game g, string id)
    {
        var declaration = OfflinePoolDeclarations.Cards.Single(d => d.CardId == id);
        foreach (var pool in declaration.Pools)
        {
            foreach (string cls in pool.ClassDomain.Contains("NEUTRAL") ? PoolRequirement.PlayableClasses : pool.ClassDomain)
            {
                var hero = cls == "DEMONHUNTER" ? (CardClass)14 : Enum.Parse<CardClass>(cls);
                var costs = pool.CostBinding == PoolCostBinding.None ? new[] { -1 } : Enumerable.Range(0, Math.Max(10, OfflineCardPools.Candidates(g,hero,pool.Filter,declaration.DbfId).Select(c=>c.Cost).DefaultIfEmpty(0).Max())+2).ToArray();
                var sizes = costs.Select(cost => OfflineCardPools.Candidates(g, hero, cost < 0 ? pool.Filter : pool.Filter.WithCost(cost), declaration.DbfId).Count).ToArray();
                Core2026Proof.Check(pool.CostBinding != PoolCostBinding.None || sizes.All(n => n >= 3), "non-dynamic pool minimum: " + id);
                Console.WriteLine($"h4 pool {id}/{pool.Name} {cls}: costs={string.Join(",", costs)} sizes={string.Join(",", sizes)}");
            }
        }
    }
    private static void ArcaneIntellect(Game g, Controller p, string id)
    {
        var top = p.DeckZone.Reverse().Take(2).Select(c => c.Id).ToArray();
        int deck = p.DeckZone.Count; Play(g,p,id);
        Core2026Proof.Check(p.DeckZone.Count == deck-2 && p.HandZone.Count == 2 && top.All(i => p.HandZone.Any(c => c.Id == i)), "draw exact top two");
        EmptyHand(p); while(p.HandZone.Count<9) Add(p,"CS2_182");
        deck=p.DeckZone.Count; Play(g,p,id);
        Core2026Proof.Check(p.HandZone.Count==10 && p.DeckZone.Count==deck-2, "second draw burns at hand cap");
        int fullDeck=p.DeckZone.Count;
        new DrawTask(count:2).Process(g,p,p.Hero,null);
        Core2026Proof.Check(p.HandZone.Count==10 && p.DeckZone.Count==fullDeck-2,"two failed draws both burn at full hand");
        EmptyHand(p); while(p.DeckZone.Count>0) p.SetasideZone.Add(p.DeckZone.Remove(p.DeckZone.TopCard));
        int damage=p.Hero.Damage; Play(g,p,id);
        Core2026Proof.Check(p.Hero.Damage==damage+3 && p.Hero.Fatigue==2,"two empty draws fatigue sequentially");
    }
    private static void Astromancer(Game g, Controller p, string id)
    {
        Pools(g,id);
        Core2026Proof.Check(RebalancedCore2026.ByDbfId[129846]=="BOT_256" && !StandardSets.CollectibleIds.Contains("BOT_256"),
            "Core alias retains original mapping without duplicate Standard parity");
        for(int n=0;n<=10;n++)
        {
            EmptyHand(p); ParkBoard(p); for(int i=0;i<n;i++) Add(p,"CS2_182");
            if(n==10) p.SetasideZone.Add(p.HandZone.Remove(p.HandZone[0]));
            int count=p.HandZone.Count;
            var filter=OfflinePoolDeclarations.Cards.Single(d=>d.CardId==id).Pools.Single().Filter.WithCost(count);
            var expected=OfflineCardPools.Pick(g.CloneExact(),filter,p.HeroClass,Cards.FromId(id).AssetId);
            Future(g,next=>{
                var owner=next.Player1; Play(next,owner,id);
                var generated=owner.BoardZone.Where(m=>m.Card.Id!=id).ToArray();
                Core2026Proof.Check(expected==null ? generated.Length==0 : generated.Length==1 && generated[0].Card.Id==expected.Id && generated[0].Card.Cost==count,"seeded exact remaining-hand bucket");
            });
        }
        ParkBoard(p); EmptyHand(p); Add(p,"CS2_182"); var legacy=Play(g,p,"BOT_256");
        Core2026Proof.Check(p.BoardZone.Count==2 && p.BoardZone.Any(m=>m.Id!=legacy.Id && m.Card.Cost==1),"legacy alias remains usable");
    }
}
