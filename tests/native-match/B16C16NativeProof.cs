using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
internal static partial class Program
{
    [GroupProof("b16-c16-native", ProofKind.Native)]
    private static void B16C16Native()
    {
        var g=DrawFixture(); Check(g.Process(EndTurnTask.Any(g.Player1)),"opponent turn");
        var p=g.Player2;
        var capture=new Capture(); var match=new NativeMatch(capture.Send);
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match,g);
        void Flush()
        {
            typeof(NativeMatch).GetMethod("SendHistory",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(match,new object[]{g.PowerHistory.Last});
            g.PowerHistory.Last.Clear();
        }
        Flush();
        var shokk=Generic.DrawCard(p,Cards.FromId("TLC_830t")); Flush();
        p.BaseMana=10;p.UsedMana=0;
        Check(g.Process(PlayCardTask.Any(p,shokk)),"opponent Shokk play"); Flush();
        int choices=0;
        while(p.Choice!=null)
        {
            choices++;
            var offers=p.Choice.Choices.ToArray();
            foreach(int offer in offers) AssertHiddenDraw(capture,offer,"Shokk opponent Discover");
            var decision=g.GetPendingDecisions().Single();
            Check(g.TryAnswer(new DecisionAnswer(decision.Id,decision.ControllerId,new[]{offers[0]}),out var error),error);
            Flush(); AssertHiddenDraw(capture,offers[0],"Shokk generated hand");
        }
        Check(choices==3 && p.Options().Count>0,"Shokk completes three decisions without stall");
        SabberStoneCore.Model.Entities.Enchantment.GetInstance(p,p.Hero,p,Cards.FromId("JAIL_509e"));
        while(!p.HandZone.IsFull) Generic.DrawCard(p,Cards.FromId("CS2_182"));
        var retained=EngineEntity.FromCard(p,Cards.FromId("CS2_029"),zone:p.DeckZone);
        Generic.Draw(p,retained); Flush();
        AssertHiddenDraw(capture,retained.Id,"Godfrey retained overdraw");
        p.SetasideZone.Add(p.HandZone.Remove(p.HandZone[0])); Flush();
        Check(retained.Zone==p.HandZone && retained.Cost==3,"Godfrey return");
        AssertHiddenDraw(capture,retained.Id,"Godfrey returned hand");
        Check(DrawRows(capture).Where(r=>r.HasTagChange).All(r=>!PayloadRules.IsInternalTag((GameTag)r.TagChange.Tag)),"private payload tags filtered");
        capture.SerializeAll();
        Console.WriteLine("PASS C16 opponent Discover/generated hand/backlog privacy and decision completion");
    }
}
