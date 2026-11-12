using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
internal static class B18ODeckProof
{
    internal static string[] Beasts() => Cards.All.Where(c => DeckbuildingRules.Eligible("JAIL_831", c, FormatType.FT_STANDARD))
        .OrderBy(c => c.Id, StringComparer.Ordinal).Take(3).Select(c => c.Id).ToArray();
    internal static Game New(string source, string[] slots, int seed = 19, bool start = true)
    {
        var g = new Game(new GameConfig { StartPlayer=1, RandomSeed=seed, Shuffle=true, SkipMulligan=true, History=true, Logging=false,
            FormatType=FormatType.FT_WILD, Player1HeroClass=source=="JAIL_397"?CardClass.PALADIN:CardClass.HUNTER,
            Player1Deck=new[]{source}.Concat(Enumerable.Repeat("CS2_182",29)).Select(Cards.FromId).ToList(),
            Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList(),
            Player1Sideboards=new Dictionary<string,string[]> { [source]=slots } });
        if(start) { g.StartGame(); ClearHand(g.Player1); ClearHand(g.Player2); }
        return g;
    }
    internal static void ClearHand(Controller p) { foreach(var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card)); }
    internal static bool Answer(Game game, int entity)
    {
        var decision=game.GetPendingDecisions().Single();
        return game.TryAnswer(new DecisionAnswer(decision.Id,decision.ControllerId,new[]{entity}),out _);
    }
    internal static void Reject(Action action, string message)
    {
        bool rejected=false;
        try { action(); } catch(InvalidOperationException) { rejected=true; }
        Core2026Proof.Check(rejected,message);
    }
    [GroupProof("b18-o-deck")]
    internal static void Run()
    {
        Pinned_BeatrixThirtyMainPlusTenBeforeOpeningDraw();
        Unpinned_SelectionDomains();
        Unpinned_UnderbellyControllerSlots();
        Unpinned_UnderbellyCapacityAndRewind();
        Console.WriteLine("PASS b18-o-deck: setup, sideboards, clone/load, Rewind, capacity, domains");
    }
    private static void Pinned_BeatrixThirtyMainPlusTenBeforeOpeningDraw()
    {
        var original=New("JAIL_397",new[]{"EX1_015"},start:false);
        foreach(var g in new[]{original, original.CloneExact(history:true)})
        {
            g.StartGame(stopBeforeShuffling:true);
            Core2026Proof.Check(g.Player1.DeckZone.Count==40 && g.Player1.HandZone.Count==0,"Beatrix adds ten before opening draws");
            Core2026Proof.Check(g.Player1.DeckCards.Count==30 && g.Player1.DeckCards.All(c=>c.Id!="EX1_015"),"main list ignores generated ten");
            var added=g.Player1.DeckZone.Where(p=>p.Card.Id=="EX1_015").ToArray();
            Core2026Proof.Check(added.Length==10 && added.Select(p=>p.Id).Distinct().Count()==10 && added.All(p=>p[GameTag.DISPLAYED_CREATOR]!=0),"ten fresh created physical entries");
            Core2026Proof.Check(g.Player2.DeckZone.Count==30,"opponent unchanged");
        }
        var started=New("JAIL_397",new[]{"EX1_015"});
        foreach(var saved in new[]{started.CloneExact(history:true),Game.LoadSnapshot(started.SaveSnapshot())})
            Core2026Proof.Check(saved.Player1.DeckCards.Count==30 && saved.IdEntityDic.Values.Count(c=>c.Card.Id=="EX1_015" && c[GameTag.DISPLAYED_CREATOR]!=0)==10,
                "expanded physical deck and immutable main list survive clone/snapshot");
        var openings=new List<string>(); bool seen=false;
        for(int seed=1;seed<=3;seed++)
        {
            string Draw() { var g=New("JAIL_397",new[]{"EX1_015"},seed,false);g.StartGame();return string.Join(",",g.Player1.HandZone.Select(p=>p.Card.Id)); }
            var first=Draw();Core2026Proof.Check(first==Draw(),"seeded opening order reproducible"); openings.Add(first);seen|=first.Contains("EX1_015");
        }
        Core2026Proof.Check(seen && openings.Distinct().Count()>1,"generated copies eligible for seeded opening draws");
    }
    private static void Unpinned_SelectionDomains()
    {
        bool Eligible(string source,string id,FormatType format=FormatType.FT_WILD)=>DeckbuildingRules.Eligible(source,Cards.FromId(id),format);
        Core2026Proof.Check(Eligible("JAIL_397","EX1_015") && Eligible("JAIL_397","EX1_012"),"neutral 2-cost including legendary accepted");
        Core2026Proof.Check(!Eligible("JAIL_397","CS2_182") && !Eligible("JAIL_397","EX1_015",FormatType.FT_STANDARD) && !Eligible("JAIL_397","CS2_029"),"printed cost/type/rotation restrictions");
        Reject(()=>DeckbuildingRules.Validate(new[]{"JAIL_397","EX1_015"},new Dictionary<string,string[]> { ["JAIL_397"]=new[]{"EX1_015"} },FormatType.FT_WILD),"main-list selection exclusion");
        Reject(()=>DeckbuildingRules.Validate(new[]{"JAIL_397"},null,FormatType.FT_WILD),"missing Beatrix choice rejected");
        Core2026Proof.Check(!Eligible("JAIL_831","CS2_171") && !Eligible("JAIL_831","EX1_531"),"neutral and Hunter Beasts excluded");
        var beasts=Beasts();Core2026Proof.Check(beasts.Length==3,"implemented Standard contraband available");
        Reject(()=>DeckbuildingRules.Validate(new[]{"JAIL_831"},new Dictionary<string,string[]> { ["JAIL_831"]=beasts.Take(2).ToArray() },FormatType.FT_STANDARD),"three exact slots required");
        Reject(()=>DeckbuildingRules.Validate(new[]{"JAIL_831"},new Dictionary<string,string[]> { ["JAIL_831"]=Enumerable.Repeat(beasts[0],3).ToArray() },FormatType.FT_STANDARD),"sideboard copy cap");
        Console.WriteLine("Deckbuilding buckets (selection, no RNG pools): Beatrix Standard="+Cards.All.Count(c=>DeckbuildingRules.Eligible("JAIL_397",c,FormatType.FT_STANDARD))+"; contraband Standard="+Cards.All.Count(c=>DeckbuildingRules.Eligible("JAIL_831",c,FormatType.FT_STANDARD)));
    }
    private static void Unpinned_UnderbellyControllerSlots()
    {
        var beasts=Beasts();var g=New("JAIL_831",beasts);
        var source=Play(g,g.Player1,"JAIL_831");
        Core2026Proof.Check(g.Player1.Choice.Choices.Select(id=>g.IdEntityDic[id].Card.Id).SequenceEqual(beasts),"offers preserve configured slot order");
        foreach(var x in new[]{g,g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())})
        {
            var p=x.Player1;int chosen=p.Choice.Choices[1];
            Core2026Proof.Check(Answer(x,chosen),"answer pending choice after clone/load");
            var reward=p.HandZone.Single(c=>c.Card.Id==beasts[1]);
            Core2026Proof.Check(reward.Cost==Math.Max(0,reward.Card.Cost-3),"persistent additive three-cost discount");
            Core2026Proof.Check(DeckbuildingRules.Remaining(p,"JAIL_831").Select(c=>c.Card.Id).SequenceEqual(new[]{beasts[0],beasts[2]}),"consume chosen slot, retain ordered remainder");
            ((Minion)x.IdEntityDic[source.Id]).Silence();
            Core2026Proof.Check(DeckbuildingRules.Remaining(p,"JAIL_831").Length==2,"Silence cannot replenish controller slots");
            End(x);Play(x,x.Player2,"JAIL_831");
            Core2026Proof.Check(x.Player2.Choice==null && x.Player2.HandZone.All(c=>!beasts.Contains(c.Card.Id)),"generated enemy King never inherits another controller's slots");
            End(x);
            p.HandZone.Add(p.BoardZone.Remove((Minion)x.IdEntityDic[source.Id]));
            PlayEntity(x,p,x.IdEntityDic[source.Id]);
            Core2026Proof.Check(p.Choice.Choices.Count==2,"bounce/replay sees remainder");
            Answer(x,p.Choice.Choices[0]);
            Core2026Proof.Check(DeckbuildingRules.Remaining(p,"JAIL_831").Length==1,"second battlecry consumes once");
            Play(x,p,"JAIL_831");Core2026Proof.Check(p.Choice.Choices.Count==1,"last slot remains a selectable offer");
            Core2026Proof.Check(Answer(x,p.Choice.Choices[0]),"last slot accepted");
            Play(x,p,"JAIL_831");
            Core2026Proof.Check(p.Choice==null && DeckbuildingRules.Remaining(p,"JAIL_831").Length==0,"exhausted controller never replenishes slots");
        }
        var common=Cards.All.First(c=>DeckbuildingRules.Eligible("JAIL_831",c,FormatType.FT_STANDARD) && c.Rarity!=Rarity.LEGENDARY).Id;
        var dup=New("JAIL_831",new[]{common,common,beasts.First(id=>id!=common)});
        Play(dup,dup.Player1,"JAIL_831");Core2026Proof.Check(dup.Player1.Choice.Choices.Count==2,"duplicate slots yield distinct offers");
        Answer(dup,dup.Player1.Choice.Choices.First(id=>dup.IdEntityDic[id].Card.Id==common));
        Core2026Proof.Check(DeckbuildingRules.Remaining(dup.Player1,"JAIL_831").Count(c=>c.Card.Id==common)==1,"only one duplicate slot consumed");
    }
    private static void Unpinned_UnderbellyCapacityAndRewind()
    {
        var retained=New("JAIL_831",Beasts());var owner=retained.Player1;
        Generic.AddEnchantmentBlock(retained,Cards.FromId("JAIL_509e"),owner.Hero,owner);
        Play(retained,owner,"JAIL_831");
        while(!owner.HandZone.IsFull) Add(owner,"CS2_182");
        Core2026Proof.Check(Answer(retained,owner.Choice.Choices[0]),"Godfrey overflow choice accepted");
        Core2026Proof.Check(DeckbuildingRules.Remaining(owner,"JAIL_831").Length==2 && PayloadRules.Entries(owner.AppliedEnchantments.First(e=>e.Card.Id=="JAIL_509e")).Length==1,
            "Godfrey retains result without replenishing consumed slot");
        var g=New("JAIL_831",Beasts());var p=g.Player1;
        Play(g,p,"JAIL_831");var chosen=g.IdEntityDic[p.Choice.Choices[0]].Card.Id;
        while(!p.HandZone.IsFull) Add(p,"CS2_182");
        Core2026Proof.Check(Answer(g,p.Choice.Choices[0]),"full-hand choice resolves");
        Core2026Proof.Check(p.HandZone.Count==10 && DeckbuildingRules.Remaining(p,"JAIL_831").Length==2 && p.GraveyardZone.Any(c=>c.Card.Id==chosen),"overflow burns result and consumes slot");
        ClearHand(p);
        Play(g,p,"TIME_001");Core2026Proof.Check(g.Rewind!=null,"Rewind offered");
        var rewind=g.Rewind;
        Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(rewind.OfferChoiceId,rewind.ControllerId,new[]{rewind.RewindEntityId}),out _),"Rewind accepted");
        while(g.Rewind!=null) {rewind=g.Rewind;Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(rewind.OfferChoiceId,rewind.ControllerId,new[]{rewind.KeepEntityId}),out _),"keep accepted");}
        p=g.Player1;
        foreach(var x in new[]{g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())})
            Core2026Proof.Check(DeckbuildingRules.Remaining(x.Player1,"JAIL_831").Length==2,"consumption survives Rewind/clone/load");
        while(p.BoardZone.Count<7) Summon(p,"CS2_182");
        var king=Add(p,"JAIL_831");p.BaseMana=10;p.UsedMana=0;
        Core2026Proof.Check(!p.Options().Any(t=>t.PlayerTaskType==PlayerTaskType.PLAY_CARD && t.Source?.Id==king.Id),"full board blocks King play");
        Core2026Proof.Check(DeckbuildingRules.Remaining(p,"JAIL_831").Length==2,"failed play consumes nothing");
    }
}
