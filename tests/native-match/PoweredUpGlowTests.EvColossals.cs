using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("native-ev-colossals", ProofKind.Native)]
    private static void EvColossalsNative()
    {
        foreach(bool opponent in new[]{false,true}) {
            var f=new Fixture();var g=f.Game;if(opponent)Check(g.Process(EndTurnTask.Any(f.Player)),"Colossal opponent turn");
            var p=g.CurrentPlayer;p.BaseMana=10;p.UsedMana=0;
            foreach(var held in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(held));
            var unrelated=Generic.DrawCard(p,Cards.FromId("CS2_029"));
            var body=(Minion)Generic.DrawCard(p,Cards.FromId("TSC_660"));
            Check(g.Process(PlayCardTask.Any(p,body)),"native Nellie play");f.Flush();
            var offerIds=new System.Collections.Generic.List<int>();
            while(p.Choice!=null) {
                offerIds.AddRange(p.Choice.Choices);var d=g.GetPendingDecisions().Single();
                Check(g.TryAnswer(new DecisionAnswer(d.Id,d.ControllerId,new[]{p.Choice.Choices.Last()}),out _),"native crew choice");f.Flush();
            }
            var ship=ColossalRules.AppendagesOf(body).Single();
            var stored=g.IdEntityDic.Values.Where(PayloadRules.IsStored).Select(c=>c.Id).ToArray();
            Check(stored.Length==3,"native crew saved");
            var actors=f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null).ToArray();
            Check(actors.Where(e=>stored.Contains(e.Entity)).All(e=>string.IsNullOrEmpty(e.Name)),"payload definitions never projected");
            if(opponent) {
                Check(actors.Where(e=>offerIds.Contains(e.Entity)||e.Entity==unrelated.Id).All(e=>string.IsNullOrEmpty(e.Name)),"opponent offers and hand remain private");
            }
            Check(actors.All(e=>e.Tags.All(t=>!PayloadRules.IsInternalTag((GameTag)t.Name)&&!DiscoverRealRules.IsInternalTag((GameTag)t.Name))),"internal crew and choice tags filtered");
            ship.Destroy();g.DeathProcessingAndAuraUpdate();f.Flush();
            Check(p.HandZone.Where(c=>c.Id!=unrelated.Id).Count()==3 && p.HandZone.Where(c=>c.Id!=unrelated.Id).All(c=>c.Cost==1),"native crew delivered at one");
            if(opponent) {
                var grants=p.HandZone.Where(c=>c.Id!=unrelated.Id).Select(c=>c.Id).ToArray();
                Check(f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null&&grants.Contains(e.Entity)).All(e=>string.IsNullOrEmpty(e.Name)),"opponent delivered crew remains private");
            }
        }
        foreach(bool opponent in new[]{false,true}) {
            var f=new Fixture();var g=f.Game;if(opponent)Check(g.Process(EndTurnTask.Any(f.Player)),"Dredge opponent turn");
            var p=g.CurrentPlayer;p.BaseMana=10;p.UsedMana=0;
            foreach(var card in p.DeckZone.ToArray())p.SetasideZone.Add(p.DeckZone.Remove(card));
            for(int i=0;i<3;i++) {
                var original=(Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p,Cards.FromId("CS2_182"));
                original.AttackDamage=10+i;original.BaseHealth=12+i;original.Cost=i;p.DeckZone.Add(original);
            }
            var body=(Minion)Generic.DrawCard(p,Cards.FromId("TSC_030"));var enemy=f.Summon("CS2_200",p.Opponent);
            Check(g.Process(PlayCardTask.Any(p,body)),"native Leviathan play");
            Check(g.Process(p.Options().First(t=>t.PlayerTaskType==PlayerTaskType.MINION_ATTACK && t.Source?.Id==body.Id && t.Target?.Id==enemy.Id)),"native Leviathan attack");f.Flush();
            var offers=p.Choice.Choices.ToArray();int selected=g.IdEntityDic[offers.Last()][DiscoverRealRules.BoundEntity];
            for(int i=0;i<3;i++) {
                var offer=(Minion)g.IdEntityDic[offers[i]];
                Check(offer.AttackDamage==10+i && offer.BaseHealth==12+i && offer.Cost==i,"native Dredge binds modified duplicate display");
                if(!opponent) {
                    var tags=f.Match.SnapshotVisibleTransportTags(offer);
                    Check(tags[(int)GameTag.ATK]==10+i && tags[(int)GameTag.HEALTH]==12+i && tags[(int)GameTag.COST]==i,"owner native projection displays modified duplicate");
                }
            }
            if(opponent)Check(f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null&&offers.Contains(e.Entity)).All(e=>string.IsNullOrEmpty(e.Name)),"opponent Dredge offers private");
            if(opponent)Check(f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null&&offers.Contains(e.Entity))
                .All(e=>e.Tags.All(t=>!new[]{(int)GameTag.ATK,(int)GameTag.HEALTH,(int)GameTag.COST}.Contains(t.Name))),"opponent Dredge snapshots hide modified stats/cost");
            if(opponent)Check(!f.Entries.Any(e=>e.HasTagChange && offers.Contains(e.TagChange.Entity)
                && new[]{(int)GameTag.ATK,(int)GameTag.HEALTH,(int)GameTag.COST}.Contains(e.TagChange.Tag)),"opponent Dredge modified stats/cost deltas stay private");
            var d=g.GetPendingDecisions().Single();Check(g.TryAnswer(new DecisionAnswer(d.Id,d.ControllerId,new[]{offers.Last()}),out _),"native Dredge answer");f.Flush();
            Check(p.DeckZone.Last().Id==selected,"native Dredge puts actual chosen card on top");
            if(opponent)Check(f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null&&e.Entity==selected).All(e=>string.IsNullOrEmpty(e.Name)),"opponent Dredged card remains hidden after deck movement");
        }
        Console.WriteLine("PASS ev-colossals native crew/Dredge privacy both seats and internal tags");
    }
}
