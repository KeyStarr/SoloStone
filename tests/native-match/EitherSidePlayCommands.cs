using System;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

internal static partial class Program
{
    internal static void B17SideCommands()
    {
        foreach (string id in new[] { "CAP_004", "JAIL_452" })
        {
            var capture = new Capture(); var match = StartAtHumanTurn(capture); var g = match.Game; var p = g.Player1;
            p.BaseMana = 10; p.UsedMana = 0;
            var card = Generic.DrawCard(p, Cards.FromId("CS2_231"));
            card = Generic.ChangeEntityBlock(p, card, Cards.FromId(id), true);
            typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, new object[] { g.PowerHistory.Last });
            g.PowerHistory.Last.Clear();
            typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
            var options = capture.Last<AllOptions>(14);
            int index = options.Options.FindIndex(o => o.MainOption?.Id == card.Id);
            Check(index >= 0 && options.Options[index].SubOptions.Count == 2,
                "transformed either-side minion exposes both native side choices");
            match.SendOption(options.Id, index, 0, 1, 1);
            Check(g.IdEntityDic[card.Id].Controller == p.Opponent && g.IdEntityDic[card.Id].Zone == p.Opponent.BoardZone,
                "transformed side choice routes the original identity onto enemy board");
            capture.SerializeAll();
        }
        foreach(bool friendlyFull in new[]{false,true})
        {
            var capture=new Capture(); var match=StartAtHumanTurn(capture); var g=match.Game; var p=g.Player1;
            foreach(var c in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(c));
            p.BaseMana=10; p.UsedMana=0;
            if(friendlyFull) while(!p.BoardZone.IsFull)
                SabberStoneCore.Model.Entities.Entity.FromCard(p,Cards.FromId("CS2_182"),zone:p.BoardZone);
            var left=SabberStoneCore.Model.Entities.Entity.FromCard(p.Opponent,Cards.FromId("CS2_182"),zone:p.Opponent.BoardZone);
            var card=Generic.DrawCard(p,Cards.FromId("JAIL_461"));
            typeof(NativeMatch).GetMethod("SendHistory",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(match,new object[]{g.PowerHistory.Last});
            g.PowerHistory.Last.Clear();
            typeof(NativeMatch).GetMethod("SendHumanOptions",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(match,null);
            var options=capture.Last<AllOptions>(14);
            int index=options.Options.FindIndex(o=>o.MainOption?.Id==card.Id), sub=friendlyFull?0:1;
            Check(index>=0 && options.Options[index].SubOptions.Count==(friendlyFull?1:2),"full friendly board omits only friendly suboption");
            AssertRejectedWithoutMutation(match,capture,()=>match.SendOption(options.Id+1000,index,0,sub,1));
            AssertRejectedWithoutMutation(match,capture,()=>match.SendOption(options.Id,index,0,-1,1));
            AssertRejectedWithoutMutation(match,capture,()=>match.SendOption(options.Id,index,p.Opponent.Hero.Id,sub,1));
            AssertRejectedWithoutMutation(match,capture,()=>match.SendOption(options.Id,index,0,sub,0));
            AssertRejectedWithoutMutation(match,capture,()=>match.SendOption(options.Id,index,0,sub,3));
            Check(p.UsedMana==0 && card.Zone==p.HandZone,"invalid native commands never pay or remove hand entity");
            match.SendOption(options.Id,index,0,sub,2);
            Check(card.Controller==p.Opponent && card.Zone==p.Opponent.BoardZone && left.Zone.Type==SabberStoneCore.Enums.Zone.GRAVEYARD
                && p.UsedMana==3,"advertised list index routes same hand entity to enemy position");
            AssertRejectedWithoutMutation(match,capture,()=>match.SendOption(options.Id,index,0,sub,2));
            capture.SerializeAll();
        }
        {
            var capture=new Capture(); var match=StartAtHumanTurn(capture); var p=match.Game.Player1;
            p.BaseMana=10; p.UsedMana=0; var card=Generic.DrawCard(p,Cards.FromId("CAP_004"));
            typeof(NativeMatch).GetMethod("SendHumanOptions",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(match,null);
            var options=capture.Last<AllOptions>(14); int index=options.Options.FindIndex(o=>o.MainOption?.Id==card.Id);
            while(!p.Opponent.BoardZone.IsFull) SabberStoneCore.Model.Entities.Entity.FromCard(p.Opponent,Cards.FromId("CS2_182"),zone:p.Opponent.BoardZone);
            AssertRejectedWithoutMutation(match,capture,()=>match.SendOption(options.Id,index,0,1,1));
            Check(p.UsedMana==0 && card.Zone==p.HandZone,"newly full native destination rejects before payment");
        }
    }
}
