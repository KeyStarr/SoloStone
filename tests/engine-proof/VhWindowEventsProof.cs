using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class VhWindowEventsProof
{
    internal static IPlayable Cast(Game game, Controller p, string id)
    {
        var source=Add(p,id);
        Core2026Proof.Check(game.Process(PlayCardTask.Any(p,source)),"Arrival cast accepted: "+id);
        return source;
    }
    internal static void Deck(Controller p, params string[] ids)
    {
        foreach(var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        foreach(var id in ids) p.DeckZone.Add(Entity.FromCard(p,Cards.FromId(id)));
    }
    internal static void Unpinned_Arrival(Game g, Controller p, string id)
    {
        var type=id=="BE_EVENT_100"?CardType.MINION:CardType.SPELL;
        Deck(p,"CS2_182","CS2_029","CS2_231","GAME_005","CS2_120","CS2_024","CS2_179","CS2_023");
        var oracle=g.CloneExact();
        var candidates=oracle.Player1.DeckZone.Where(c=>c.Card.Type==type).ToList();
        var expected=new List<int>();
        for(int i=0;i<2;i++) { int n=oracle.Random.Next(candidates.Count); expected.Add(candidates[n].Id); candidates.RemoveAt(n); }
        int cost=Cards.FromId(id).Cost;
        Cast(g,p,id);
        Core2026Proof.Check(p.HandZone.Select(c=>c.Id).SequenceEqual(expected),"Unpinned_Arrival seeded order-sensitive two-of-four physical tutors");
        Core2026Proof.Check(p.UsedMana==0 && p.TemporaryMana==0 && p.RemainingMana==10,"Unpinned_Arrival refresh paid cost without permanent growth");
        new EffectContext(g,p.Id,p.Hero.Id).RefreshMana(2);
        Core2026Proof.Check(p.RemainingMana<=p.BaseMana,"Arrival then a free refresh never exceeds crystal count");
        Core2026Proof.Check(p.AppliedEnchantments.Count(e=>e.Card.Id==id+"e")==1,"Arrival exact player Watch token");
        var other=Add(p,type==CardType.MINION?"GAME_005":"CS2_182");
        Core2026Proof.Check(other[GameTag.CANT_PLAY]==1 && !other.IsPlayableByPlayer && !p.Options().Any(t=>t is PlayCardTask && t.Source==other),"Unpinned_Arrival blocked type in predicate, tag and bot options");
        foreach(var next in new[]{g.CloneExact(history:true),Game.LoadSnapshot(g.SaveSnapshot())})
        {
            Core2026Proof.Check(next.IdEntityDic[other.Id][GameTag.CANT_PLAY]==1,"Arrival clone/load lock");
            End(next);
            Core2026Proof.Check(next.Player1.AppliedEnchantments.All(e=>e.Card.Id!=id+"e") && next.IdEntityDic[other.Id][GameTag.CANT_PLAY]==0,"Unpinned_Arrival expiry before opponent acts");
        }
        var before=Canonical.State(g);
        Core2026Proof.Check(!g.Process(PlayCardTask.Any(p,other,skipPrePhase:true)) && before==Canonical.State(g),"Arrival forced invalid play is inert");
        End(g); End(g);
        PoolPilotProof.EmptyHand(p);
        Deck(p,type==CardType.MINION?"GAME_005":"CS2_182");
        p.UsedMana=0; p.TemporaryMana=0;
        int fatigue=p.Hero.Damage;
        Cast(g,p,id);
        Core2026Proof.Check(p.HandZone.Count==0 && p.DeckZone.Count==1 && p.Hero.Damage==fatigue,"Unpinned_Arrival absent type no fallback or fatigue");
        End(g); End(g); PoolPilotProof.EmptyHand(p); p.UsedMana=0; p.TemporaryMana=0;
        Deck(p,type==CardType.MINION?"CS2_182":"GAME_005");
        Cast(g,p,id);
        Core2026Proof.Check(p.HandZone.Count==1 && p.DeckZone.IsEmpty,"Unpinned_Arrival short bucket draws what exists");
        End(g); End(g); PoolPilotProof.EmptyHand(p); p.UsedMana=0; p.TemporaryMana=0;
        Deck(p,type==CardType.MINION?"CS2_182":"GAME_005",type==CardType.MINION?"CS2_231":"CS2_023");
        while(p.HandZone.Count<9) Add(p,"GAME_005");
        Cast(g,p,id);
        Core2026Proof.Check(p.HandZone.Count==10 && p.DeckZone.IsEmpty && p.GraveyardZone.Any(c=>c.Card.Type==type),"Unpinned_Arrival full-hand second draw burns");
        End(g); End(g); PoolPilotProof.EmptyHand(p); p.UsedMana=0; p.TemporaryMana=0;
        while(!p.BoardZone.IsFull) Summon(p,"CS2_182");
        Deck(p,type==CardType.MINION?"CS2_182":"GAME_005",type==CardType.MINION?"CS2_231":"CS2_023");
        Cast(g,p,id);
        Core2026Proof.Check(p.HandZone.Count==2 && p.BoardZone.IsFull && p.DeckZone.IsEmpty,"Arrival full board still tutors and refreshes");
        Console.WriteLine(id+" physical candidate buckets: 4,0,1,2; offline generation pools: none");
    }

    [GroupProof("vh-window-events")]
    internal static void Run()
    {
        foreach(string id in new[]{"BE_EVENT_100","BE_EVENT_101"})
        foreach(bool history in new[]{false,true})
        foreach(bool secondSeat in new[]{false,true})
        {
            var g=PlayLockRulesProof.Fixture(history); if(secondSeat) End(g);
            var p=g.CurrentPlayer; p.BaseMana=10;p.UsedMana=0;p.TemporaryMana=0;
            Core2026Proof.Check(Cards.FromId(id).Implemented,"Arrival facility consumer registered");
            Cast(g,p,id);
            var held=new[]{"CS2_182","GAME_005","CS2_106","CORE_EX1_323","CORE_REV_990"}.Select(s=>Add(p,s)).ToArray();
            foreach(var card in held)
            {
                bool allow=card.Card.Type==(id=="BE_EVENT_100"?CardType.MINION:CardType.SPELL);
                Core2026Proof.Check(card.IsPlayableByPlayer==allow && card[GameTag.CANT_PLAY]==(allow?0:1),"Unpinned_Arrival all five hand types");
            }
            Core2026Proof.Check(p.Hero.HeroPower.IsPlayableByPlayer && p.Options().Any(t=>t.PlayerTaskType==PlayerTaskType.HERO_POWER),"Unpinned_Arrival hero powers remain legal");
            var opposite=(Spell)Add(p,id=="BE_EVENT_100"?"BE_EVENT_101":"BE_EVENT_100");
            new EffectContext(g,p.Id,opposite.Id).CastSpell(opposite.Id,HistoryCastKind.Effect);
            Core2026Proof.Check(p.HandZone.All(c=>c[GameTag.CANT_PLAY]==1),"Unpinned_Arrival indirect cast bypasses lock and opposite restrictions intersect");
            Core2026Proof.Check(g.Process(ConcedeTask.Any(p.Opponent)),"Arrival Concede always available");
        }
        Unpinned_TurnAndControl();
        Unpinned_RefreshBoundaries();
        Unpinned_FairDraws();
        Unpinned_Rewind();
        Console.WriteLine("PASS vh-window-events facility");
    }
    private static void Unpinned_TurnAndControl()
    {
        foreach(string id in new[]{"BE_EVENT_100","BE_EVENT_101"})
        {
            var g=PlayLockRulesProof.Fixture();var p=g.Player1;
            Cast(g,p,id);
            var attacker=Summon(p,"CS2_182");attacker.IsExhausted=false;
            var location=LocationProofs.Location(p);
            Core2026Proof.Check(p.Options().Any(t=>t.PlayerTaskType==PlayerTaskType.MINION_ATTACK)
                && p.Options().Any(t=>t is ActivateLocationTask),"Unpinned_Arrival attacks and existing location activation remain legal");
            attacker.Silence();Kill(g,attacker);
            var held=Add(p,id=="BE_EVENT_100"?"GAME_005":"CS2_182");
            Core2026Proof.Check(held[GameTag.CANT_PLAY]==1,"Arrival unaffected by unrelated Silence/leave-play");
            p.HandZone.Remove(held);held.Controller=p.Opponent;held[GameTag.CONTROLLER]=p.Opponent.PlayerId;p.Opponent.HandZone.Add(held);
            Core2026Proof.Check(held[GameTag.CANT_PLAY]==0,"Arrival hand transfer consults recipient restriction");
            p.Opponent.HandZone.Remove(held);held.Controller=p;held[GameTag.CONTROLLER]=p.PlayerId;p.HandZone.Add(held);
            Core2026Proof.Check(held[GameTag.CANT_PLAY]==1,"Arrival returned hand card consults original controller again");
            var enemyHeld=Add(p.Opponent,id=="BE_EVENT_100"?"GAME_005":"CS2_182");
            Core2026Proof.Check(enemyHeld[GameTag.CANT_PLAY]==0,"Arrival does not restrict opponent");
            var repeat=(Spell)Add(p,id);
            new EffectContext(g,p.Id,repeat.Id).CastSpell(repeat.Id,HistoryCastKind.Effect);
            Core2026Proof.Check(p.AppliedEnchantments.Count(e=>e.Card.Id==id+"e")==2 && held[GameTag.CANT_PLAY]==1,"Unpinned_Arrival repeated watches stack idempotently");
            End(g);
            Core2026Proof.Check(held[GameTag.CANT_PLAY]==0,"Arrival all duplicate watches expire together");
            var offTurn=(Spell)Add(p,id);
            new EffectContext(g,p.Id,offTurn.Id).CastSpell(offTurn.Id,HistoryCastKind.Effect);
            Core2026Proof.Check(held[GameTag.CANT_PLAY]==1,"Unpinned_Arrival opponent-turn indirect cast attaches to caster");
            End(g);
            Core2026Proof.Check(held[GameTag.CANT_PLAY]==0,"Unpinned_Arrival opponent-turn restriction expires at that turn end");
        }
    }

    private static void Unpinned_RefreshBoundaries()
    {
        foreach(string id in new[]{"BE_EVENT_100","BE_EVENT_101"})
        foreach(var setup in new[]{(10,8,0,0),(10,2,0,3),(10,8,3,2),(6,5,1,0),(10,0,0,0)})
        {
            var g=PlayLockRulesProof.Fixture();var p=g.Player1;
            p.BaseMana=setup.Item1;p.UsedMana=setup.Item2;p.TemporaryMana=setup.Item3;p.OverloadLocked=setup.Item4;p.OverloadOwed=2;
            var source=(Spell)Add(p,id);
            var before=p.RemainingMana;
            new EffectContext(g,p.Id,source.Id).CastSpell(source.Id,HistoryCastKind.Effect);
            int expected=Math.Min(Cards.FromId(id).Cost,setup.Item2);
            Core2026Proof.Check(p.RemainingMana==before+expected && p.BaseMana==setup.Item1 && p.UsedMana==setup.Item2-expected && p.TemporaryMana==setup.Item3 && p.OverloadLocked==setup.Item4 && p.OverloadOwed==2,
                "Unpinned_Refresh cap/shortage/locked/pending/temporary boundary");
        }
    }
    private static void Unpinned_FairDraws()
    {
        foreach(int seat in new[]{1,2})
        foreach(string id in new[]{"BE_EVENT_100","BE_EVENT_101"})
        {
            var g=PlayLockRulesProof.Fixture(); if(seat==2) End(g);
            var p=g.CurrentPlayer;p.BaseMana=10;
            Deck(p,"CS2_182","GAME_005","CS2_231","CS2_029");
            var source=Add(p,id);
            var observed=g.CloneExact(history:true);
            Core2026Proof.Check(observed.Process(PlayCardTask.Any(observed.CurrentPlayer,observed.IdEntityDic[source.Id])),"Arrival authoritative privacy fixture");
            var opposing=HistoryFairOriginProof.FairGame(FairBotState.Create(observed,p.Opponent.PlayerId,88,DeckStyle.Control));
            Core2026Proof.Check(opposing.ControllerById(p.Id).HandZone.All(c=>c is Unknown),"Arrival opponent fair view masks all drawn identities on either seat");
            var a=FairBotState.Create(g,p.PlayerId,88,DeckStyle.Control);
            var altered=g.CloneExact();
            foreach(var card in altered.CurrentPlayer.DeckZone.ToArray()) Generic.ChangeEntityBlock(altered.CurrentPlayer,card,Cards.FromId("EX1_561"),false);
            var b=FairBotState.Create(altered,p.PlayerId,88,DeckStyle.Control);
            Core2026Proof.Check(a.Evaluate()==b.Evaluate() && a.Actions.Select(x=>x.ToString()).SequenceEqual(b.Actions.Select(x=>x.ToString())),"Arrival bot cannot inspect matching hidden deck categories");
            foreach(var fair in new[]{a,b})
            {
                var next=HistoryFairOriginProof.FairGame(fair);var owner=next.CurrentPlayer;
                Core2026Proof.Check(next.Process(PlayCardTask.Any(owner,next.IdEntityDic[source.Id])),"Arrival fair cast");
                Core2026Proof.Check(owner.HandZone.Count(c=>c is Unknown)==2 && !fair.CanExpand,"Arrival unknown draw stops bot expansion without exposing type");
            }
        }
    }
    private static void Unpinned_Rewind()
    {
        foreach(string id in new[]{"BE_EVENT_100","BE_EVENT_101"})
        {
            var state=PinnedPoolFixture.Use(PlayLockRulesProof.Fixture());var owner=state.Player1;
            Deck(owner,"CS2_182","GAME_005","CS2_231","CS2_029");
            var arrival=Add(owner,id);arrival[GameTag.REWIND]=1;
            Core2026Proof.Check(state.Process(PlayCardTask.Any(owner,arrival)),"Arrival with test-granted Rewind");
            foreach(var branch in new[]{state.CloneExact(history:true),PinnedPoolFixture.LoadSnapshot(state.SaveSnapshot()),state})
            {
                var transaction=branch.Rewind;
                Core2026Proof.Check(transaction!=null && branch.TryAnswer(new DecisionAnswer(transaction.OfferChoiceId,transaction.ControllerId,new[]{transaction.RewindEntityId}),out _),"Arrival creation rollback/replay");
                Core2026Proof.Check(branch.Player1.AppliedEnchantments.Count(e=>e.Card.Id==id+"e")==1 && branch.Player1.HandZone.Count==2 && branch.Player1.DeckZone.Count==2,"Arrival replay restores two draws and one Watch, not duplicated state");
            }
        }
        var g=PinnedPoolFixture.Use(PlayLockRulesProof.Fixture());var p=g.Player1;
        Cast(g,p,"BE_EVENT_101");
        var source=Add(p,"TIME_001");p.UsedMana=0;p.TemporaryMana=0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p,source)),"Arrival Rewind spell played");
        foreach(var next in new[]{g.CloneExact(history:true),PinnedPoolFixture.LoadSnapshot(g.SaveSnapshot()),g})
        {
            var t=next.Rewind;Core2026Proof.Check(t!=null,"Arrival Rewind offered");
            Core2026Proof.Check(next.TryAnswer(new DecisionAnswer(t.OfferChoiceId,t.ControllerId,new[]{t.RewindEntityId}),out var error),"Arrival Rewind accepted "+error);
            Core2026Proof.Check(next.Player1.AppliedEnchantments.Count(e=>e.Card.Id=="BE_EVENT_101e")==1 && Add(next.Player1,"CS2_182")[GameTag.CANT_PLAY]==1,"Arrival Watch survives rollback exactly once");
        }
    }
}
