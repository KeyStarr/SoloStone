using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class B16StatGainWickerfangProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_139", Unpinned_WickerfangGainKindsAndLegOwnership);
    private static void Stats(Minion m,int a,int h,string why) => Core2026Proof.Check(m.AttackDamage==a && m.BaseHealth==h,why+$": got {m.AttackDamage}/{m.BaseHealth}");
    private static void Unpinned_WickerfangGainKindsAndLegOwnership(Game game,Controller player,string id)
    {
        Unpinned_WickerfangDyingLegSwap();
        var original=(Minion)Play(game,player,id);
        var legs=player.BoardZone.Where(m=>m!=original).ToArray();
        Core2026Proof.Check(legs.Select(m=>m.Card.Id).Order().SequenceEqual(new[]{"CATA_139t","CATA_139t2","CATA_139t3","CATA_139t4"}),"four exact exported leg variants");
        End(game); Stats(original,4,9,"each own-end leg grant mirrors once");
        End(game); Stats(original,4,9,"opponent end does not grow legs");
        Play(game,player,"TIME_003");
        var rewind=game.Rewind;
        Core2026Proof.Check(rewind != null && game.TryAnswer(new DecisionAnswer(rewind.OfferChoiceId,rewind.ControllerId,new[]{rewind.RewindEntityId}),out _),
            "Wickerfang board survives real Rewind");
        var restoredBody=(Minion)game.IdEntityDic[original.Id];
        new Enchant(Effects.AttackHealth_N(1)).ActivateTo(game.IdEntityDic[legs[0].Id]);
        Stats(restoredBody,5,10,"restored body reacts once after Rewind");
        foreach(bool history in new[]{false,true})
        foreach(bool opponent in new[]{false,true})
        {
            var g=B14H1StatsProof.NewGame(history);if(opponent) End(g);
            var p=g.CurrentPlayer;
            var first=Summon(p,id);
            var leg=p.BoardZone.First(m=>m!=first);
            // Free space for another body. Its new legs belong to the same friendly card family.
            foreach(var extra in p.BoardZone.Where(m=>m!=first && m!=leg).ToArray()) Kill(g,extra);
            var second=Summon(p,id);
            var order=new List<int>();
            TriggerManager.TriggerHandler observer=_=>{};
            g.TriggerManager.StatGainedTrigger+=observer;
            g.TriggerManager.Events.Trace=(value,_,index)=> {
                if(value is StatGainedEvent gain && index==0 && (gain.TargetId==first.Id || gain.TargetId==second.Id)) order.Add(gain.TargetId);
            };
            new Enchant(Effects.Attack_N(2),Effects.Health_N(3)).ActivateTo(leg);
            g.TriggerManager.StatGainedTrigger-=observer;g.TriggerManager.Events.Trace=null;
            Core2026Proof.Check(order.SequenceEqual(new[]{first.Id,second.Id}),"friendly bodies resolve in play order");
            Stats(first,2,8,"first body mirrors combined positive grant"); Stats(second,2,8,"second body also mirrors non-parent leg");
            new Enchant(Effects.SetAttackHealth(1)).ActivateTo(first);
            new Enchant(Effects.Attack_N(3),Effects.Health_N(-1)).ActivateTo(leg);
            Stats(first,4,1,"Pinned_WickerfangAfterLowerSetter: fresh grant survives earlier lower setter");
            Stats(second,5,8,"mixed grant mirrors only positive component");
            var temp=new Enchant(Effects.Attack_N(2)){IsOneTurnEffect=true};temp.ActivateTo(leg);
            Stats(first,6,1,"temporary gain mirrors permanently");
            // Silence other legs so their own-end growth cannot obscure expiry checks.
            foreach(var extra in p.BoardZone.Where(m=>m!=first && m!=second && m!=leg).ToArray())extra.Silence();
            leg.Silence(); End(g); Stats(first,6,1,"expiry never retracts mirrored stats; leg Silence is not gain");
            leg.Damage=1;leg.TakeFullHeal(p.Hero);Stats(first,6,1,"damage/heal excluded");
            var aura=Effects.Attack_N(5);aura.ApplyAuraTo(leg);aura.RemoveAuraFrom(leg);Stats(first,6,1,"aura-only changes excluded");
            new Enchant(Effects.SetAttackHealth(10)).ActivateTo(leg);
            // Leg was reset to 0/2 by Silence; setter contributes +10 Attack and +8 max Health.
            Stats(first,16,9,"positive setter deltas mirror once even on silenced leg");
            foreach(var future in B16StatGainEventProof.Futures(g))
            {
                var a=(Minion)future.IdEntityDic[first.Id];var b=(Minion)future.IdEntityDic[second.Id];var l=(Minion)future.IdEntityDic[leg.Id];
                new Enchant(Effects.AttackHealth_N(1)).ActivateTo(l);Stats(a,17,10,"clone/snapshot retains body listener");
                b.Silence();new Enchant(Effects.AttackHealth_N(1)).ActivateTo(l);Stats(a,18,11,"other body Silence does not disable this body");Stats(b,0,5,"silenced body no longer mirrors");
                new ControlTask(EntityType.TARGET).Process(future,l.Controller.Opponent,l.Controller.Opponent.Hero,l);
                new Enchant(Effects.AttackHealth_N(1)).ActivateTo(l);Stats(a,18,11,"stolen leg no longer benefits former controller");
                var enemy=Summon(l.Controller,id);new Enchant(Effects.AttackHealth_N(1)).ActivateTo(l);
                Stats(enemy,1,6,"stolen silenced leg benefits new controller body regardless of creator");
                Kill(future,a);new Enchant(Effects.AttackHealth_N(1)).ActivateTo(l);Stats(a,18,11,"dead body no longer reacts");
            }
        }
        var full=B14H1StatsProof.NewGame();for(int i=0;i<5;i++)Summon(full.Player1,"CS2_231");
        var partial=Summon(full.Player1,id);
        Core2026Proof.Check(full.Player1.BoardZone.Count==7 && full.Player1.BoardZone.Count(m=>m.Card.Id.StartsWith("CATA_139t"))==1,"Colossal fits only one leg at six occupied slots");
        Kill(full,full.Player1.BoardZone.First());full.AuraUpdate();
        Core2026Proof.Check(full.Player1.BoardZone.Count==6,"missing legs are never deferred or refilled");
    }

    private static void Unpinned_WickerfangDyingLegSwap()
    {
        foreach (bool history in new[] { false, true })
        foreach (bool opponent in new[] { false, true })
        {
            var g = B14H1StatsProof.NewGame(history);
            if (opponent) End(g);
            var body = Summon(g.CurrentPlayer, "CATA_139");
            int legId = body.Controller.BoardZone.First(m => m != body).Id;
            foreach (var future in B16StatGainEventProof.Futures(g))
            {
                var owner = future.CurrentPlayer;
                var leg = (Minion)future.IdEntityDic[legId];
                var gains = new List<StatGainedEvent>();
                TriggerManager.TriggerHandler observer = _ => { };
                future.TriggerManager.StatGainedTrigger += observer;
                future.TriggerManager.Events.Trace = (value, _, index) => {
                    if (value is StatGainedEvent gain && gain.TargetId == legId && index == 0) gains.Add(gain);
                };
                var alchemist = Play(future, owner, "EX1_059", leg);
                future.TriggerManager.StatGainedTrigger -= observer;
                future.TriggerManager.Events.Trace = null;
                Stats((Minion)future.IdEntityDic[body.Id], 2, 5, "living Wickerfang mirrors dying Leg's positive swap delta");
                Core2026Proof.Check(leg.Zone == owner.GraveyardZone && gains.Count == 1
                    && gains[0].Attack == 2 && gains[0].Health == 0 && gains[0].Zone == Zone.PLAY
                    && gains[0].SourceId == alchemist.Id && gains[0].EffectId == "EX1_059e",
                    "0/2 to 2/0 publishes one attributed +2 Attack before Leg dies");
            }
        }
    }
}
