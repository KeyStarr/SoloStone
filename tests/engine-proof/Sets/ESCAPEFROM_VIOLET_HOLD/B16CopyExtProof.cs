using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
internal static class B16CopyExtPriestProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("JAIL_430",Unpinned_Azalina);
        StandardSetsProof.Case("JAIL_432",Unpinned_MindSweeper);
        StandardSetsProof.Case("JAIL_433",Unpinned_UnshackleSoul);
        StandardSetsProof.Case("JAIL_434",Unpinned_EnthralledShade);
    }
    private static void Unpinned_Azalina(Game unused,Controller ignored,string id)
    {
        var definitions=new[]{"CS2_182","CS2_120","CS2_171","CS2_168"};
        var config=new SabberStoneCore.Config.GameConfig { RandomSeed=31, StartPlayer=1, SkipMulligan=true, Shuffle=false, History=true, Logging=false,
            Player1HeroClass=CardClass.PRIEST,Player2HeroClass=CardClass.PRIEST,FormatType=FormatType.FT_WILD,
            Player1Deck=new[]{Cards.FromId(id)}.Concat(Enumerable.Repeat(Cards.FromId("CS2_231"),19)).ToList(),
            Player2Deck=new[]{Cards.FromId(id)}.Concat(Enumerable.Range(0,29).Select(i=>Cards.FromId(definitions[i%4]))).ToList() };
        var game=new Game(config);var oracle=game.Random.Clone();
        var pools=new[]{game.Player2.DeckZone.Select(c=>c.Card.Id).ToList(),game.Player1.DeckZone.Select(c=>c.Card.Id).ToList()};
        var expected=new System.Collections.Generic.List<string>();
        for(int side=0;side<2;side++)for(int i=0;i<20;i++){int index=oracle.Next(pools[side].Count);if(side==0)expected.Add(pools[side][index]);pools[side].RemoveAt(index);}
        game.StartGame(stopBeforeShuffling:true);
        Core2026Proof.Check(game.Player1.DeckZone.Count==40 && game.Player2.DeckZone.Count==50,"Azalina copies twenty physical entries each mirror");
        Core2026Proof.Check(game.Player1.Hero.Health==40 && game.Player2.Hero.Health==40,"Azalina starting Health");
        var copied=game.Player1.DeckZone.Where(PayloadRules.CopiedFromOpponent).ToArray();
        Core2026Proof.Check(copied.Select(c=>c.Card.Id).SequenceEqual(expected) && oracle.CaptureState().Equals(game.Random.CaptureState()),"Azalina seeded physical order and exact RNG");
        Core2026Proof.Check(copied.Length==20 && copied.All(c=>definitions.Contains(c.Card.Id)||c.Card.Id==id),"mirror snapshots precede either expansion");
        game.NextStep=Step.BEGIN_FIRST;
        foreach(var branch in new[]{game.CloneExact(true),Game.LoadSnapshot(game.SaveSnapshot())})
            Core2026Proof.Check(branch.IdEntityDic.Values.Count(c=>c.Controller==branch.Player1 && PayloadRules.CopiedFromOpponent(c))==20,"setup copy lineage clone/load");
        foreach(int size in new[]{0,1,19,20,30,40})
        {
            config.Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),size).ToList();
            var shortGame=new Game(config);shortGame.StartGame(stopBeforeShuffling:true);
            Core2026Proof.Check(shortGame.Player1.DeckZone.Count==20+Math.Min(20,size)
                && shortGame.Player1.DeckZone.Count(PayloadRules.CopiedFromOpponent)==Math.Min(20,size),"Azalina short physical bucket "+size);
        }
        Console.WriteLine("Azalina physical buckets: 0/1/19/20/30/40 -> 0/1/19/20/20/20 copies; no catalog pool");
        var p=ignored;
        foreach(var card in p.DeckZone.ToArray()){p.DeckZone.Remove(card);p.SetasideZone.Add(card);}
        int damage=p.Hero.Damage;Play(unused,p,id);
        Core2026Proof.Check(p.Hero.Damage==damage,"empty-deck Battlecry does not fatigue");
    }
    private static IPlayable CopyAndPlay(Game g,Controller p)
    {
        var enemy=Add(p.Opponent,"CS2_231");
        var copy=Generic.Copy(p,p.Hero,enemy,Zone.HAND); PlayEntity(g,p,copy);return copy;
    }
    private static void Unpinned_MindSweeper(Game g,Controller p,string id)
    {
        g.Player1.DeckCards.Add(Cards.FromId(id)); PayloadRules.CaptureTracking(g);
        var held=Add(p,id); var enemy=Summon(p.Opponent,"CS2_182");
        var direct=Generic.Copy(p,p.Hero,Add(p.Opponent,"CS2_231"),Zone.HAND);
        var indirect=Generic.Copy(p,p.Hero,direct,Zone.HAND);PlayEntity(g,p,indirect);
        Core2026Proof.Check(!PoweredUp.IsMet(held),"own copy does not latch");
        PlayEntity(g,p,direct);
        Core2026Proof.Check(PoweredUp.IsMet(held),"direct enemy copy latches");
        Core2026Proof.Check(CopyPlayHistory.SourceControllerAt(p,p.PlayHistory.Count-1)==p.Opponent.PlayerId,"immutable copy play witness");
        foreach(var branch in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g})
        {
            PlayEntity(branch,branch.Player1,branch.IdEntityDic[held.Id]);
            Core2026Proof.Check(((Minion)branch.IdEntityDic[enemy.Id]).Damage==2,"Mind Sweeper 2 damage survives clone/load");
        }
        Generic.ReturnToHandBlock(p,(Minion)held);
        Core2026Proof.Check(!PoweredUp.IsMet(held),"new hand residence resets latch");
    }
    private static void Unpinned_UnshackleSoul(Game g,Controller p,string id)
    {
        g.Player1.DeckCards.Add(Cards.FromId(id)); PayloadRules.CaptureTracking(g);
        var held=Add(p,id);CostRules.AddModifier((Playable)held,EffectOperator.SUB,2);
        CopyAndPlay(g,p);
        Core2026Proof.Check(held.Cost==1,"conditional SET 1 wins after ordinary modifiers");
        var copy=Generic.Copy(p,p.Hero,held,Zone.HAND);
        Core2026Proof.Check(copy.Cost==3 && !PoweredUp.IsMet(copy),"fresh copy drops condition but keeps numeric discount");
        foreach(var branch in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot())})
            Core2026Proof.Check(branch.IdEntityDic[held.Id].Cost==1 && branch.IdEntityDic[copy.Id].Cost==3,"independent saved holding windows");
        var target=Summon(p.Opponent,"CS2_182"); PlayEntity(g,p,held,target);
        Core2026Proof.Check(target.Zone==p.Opponent.GraveyardZone,"Unshackle destroys legal target");
        CopyAndPlay(g,p);Core2026Proof.Check(copy.Cost==1,"later enemy copy qualifies new window");
    }
    private static void Unpinned_EnthralledShade(Game g,Controller p,string id)
    {
        g.Player1.DeckCards.Add(Cards.FromId(id)); PayloadRules.CaptureTracking(g);
        var direct=Generic.Copy(p,p.Hero,Add(p.Opponent,"CS2_182"),Zone.HAND);
        var indirect=Generic.Copy(p,p.Hero,direct,Zone.HAND);
        var shade=Summon(p,id);Kill(g,shade);
        Core2026Proof.Check(direct.Cost==3 && indirect.Cost==4,"Shade qualifies immediate source only");
        foreach(var branch in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot())})
        {
            Kill(branch,Summon(branch.Player1,id));
            Core2026Proof.Check(branch.IdEntityDic[direct.Id].Cost==2 && branch.IdEntityDic[indirect.Id].Cost==4,"Shade repeats and persists");
        }
        var silenced=Summon(p,id);silenced.Silence();Kill(g,silenced);
        Core2026Proof.Check(direct.Cost==3,"silenced Shade has no discount");
        var later=Generic.Copy(p,p.Hero,Add(p.Opponent,"CS2_231"),Zone.HAND);
        Core2026Proof.Check(later.Cost==later.Card.Cost,"later entrants excluded");
        for(int i=0;i<3;i++)Kill(g,Summon(p,id));
        Core2026Proof.Check(later.Cost==0 && direct.Cost==0,"numeric reductions floor at zero");
    }
}
