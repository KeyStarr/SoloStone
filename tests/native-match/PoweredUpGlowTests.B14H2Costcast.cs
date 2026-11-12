using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b14-h2-costcast", ProofKind.PoweredUp)]
    private static void B14H2CostcastGlow()
    {
        Scenario("EDR_846", (f,card,met) => {
            card.Cost = 2;
            if(met) f.Process(PlayCardTask.Any(f.Player,f.Draw("CS2_182")));
        }, (f,card) => PlayCardTask.Any(f.Player,card), (f,card) =>
            f.Player.HandZone.Any(c => c.Card.Id.StartsWith("EDR_846t",StringComparison.Ordinal)));
    }
}

internal static partial class Program
{
    [GroupProof("b14-h2-costcast-native", ProofKind.Native)]
    private static void B14H2CostcastNative()
    {
        foreach(bool bot in new[]{false,true})
        {
            var (match,capture) = AnimationMatch(); var g=match.Game;
            if(bot) Check(g.Process(EndTurnTask.Any(g.Player1)),"H2 other seat");
            var owner=bot?g.Player2:g.Player1;
            foreach(var c in owner.HandZone.ToArray())owner.SetasideZone.Add(owner.HandZone.Remove(c));
            foreach(var c in owner.Opponent.HandZone.ToArray())owner.Opponent.SetasideZone.Add(owner.Opponent.HandZone.Remove(c));
            void Play(IPlayable source,ICharacter target=null)
            {
                owner.BaseMana=10;owner.UsedMana=0;
                Check(g.Process(PlayCardTask.Any(owner,source,target)),"H2 native play "+source.Card.Id);
                FlushFieldHistory(match);
            }
            var secret=Generic.DrawCard(owner.Opponent,Cards.FromId("CS2_182"));
            var deja=Generic.DrawCard(owner,Cards.FromId("TIME_039"));
            FlushFieldHistory(match);int cursor=capture.Packets.Count;
            Play(deja);
            var choices=g.GetPendingDecisions().Single();
            int offer=choices.Options.Single().OptionId;
            Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(choices.Id,choices.ControllerId,new[]{offer}),out var error),"H2 private choice: "+error);
            FlushFieldHistory(match);
            var copy=owner.HandZone.Single();
            var rows=History(capture,cursor).ToArray();
            Check(copy.Card.Id==secret.Card.Id && secret.Zone==owner.Opponent.HandZone,"H2 native copy keeps original");
            if(bot) Check(rows.Where(e=>SnapshotEntity(e)==offer || SnapshotEntity(e)==copy.Id).All(e=>string.IsNullOrEmpty(SnapshotName(e))),"opponent offers and result remain hidden");
            else Check(rows.Any(e=>SnapshotEntity(e)==offer && SnapshotName(e)==secret.Card.Id),"chooser sees private offer identity");
            var held=Generic.DrawCard(owner,Cards.FromId("EDR_846"));held.Cost=2;
            Play(copy);Check(CostRules.QualifiedHeldDreams(held),"native private qualification");
            var half=(Minion)Generic.DrawCard(owner,Cards.FromId("CS2_182"));
            Play(Generic.DrawCard(owner,Cards.FromId("TIME_030")));
            FlushFieldHistory(match);
            foreach(var entry in History(capture,cursor))
            {
                Check(!entry.HasTagChange || !CostRules.IsInternalTag((GameTag)entry.TagChange.Tag),"cost bookkeeping never reaches either seat");
                var entity=entry.HasFullEntity?entry.FullEntity:entry.HasShowEntity?entry.ShowEntity:entry.HasChangeEntity?entry.ChangeEntity:null;
                if(entity!=null) Check(entity.Tags.All(t=>!CostRules.IsInternalTag((GameTag)t.Name)),"snapshot omits cost internals");
            }
            if(bot)
                Check(History(capture,cursor).Where(e=>SnapshotEntity(e)==held.Id || SnapshotEntity(e)==half.Id)
                    .All(e=>string.IsNullOrEmpty(SnapshotName(e))),"opponent held/split identities remain hidden");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS H2 native: both seats; private offers/copies/splits; payment latch tags filtered");
    }
}
