using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b15-game-setup", ProofKind.PoweredUp)]
    private static void B15SetupGlow()
    {
        var f = new Fixture();
        foreach(var id in new[]{"CORE_ETC_111","CORE_REV_946","EDR_000","EDR_260","EDR_845","JAIL_384","JAIL_443","TIME_730"})
        { f.ClearHand(); Check(!PoweredUp.IsMet(f.Draw(id)),"setup unconditional effect has no hand glow: "+id); }
    }
    [GroupProof("native-b15-game-setup", ProofKind.Native)]
    private static void B15SetupPrivacy()
    {
        foreach(bool enemy in new[]{false,true}) {
            var f=new Fixture(); var g=f.Game;
            if(enemy) f.Process(EndTurnTask.Any(f.Player));
            var p=enemy?g.Player2:g.Player1;
            var source=Generic.DrawCard(p,Cards.FromId("TIME_730")); p.BaseMana=10; p.UsedMana=0;
            f.Process(PlayCardTask.Any(p,source));
            while(p.Choice!=null) { var d=g.GetPendingDecisions().Single(); Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(d.Id,d.ControllerId,new[]{p.Choice.Choices[0]}),out _),"native Cultivator answer"); f.Flush(); }
            var hidden=p.DeckZone.Where(e=>e.AppliedEnchantments?.Any(x=>x.Card.Id=="TIME_730e")==true).Select(e=>e.Id).ToArray();
            Check(hidden.Length==2,"Cultivator native pair");
            var actors=f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null).ToArray();
            if(enemy) Check(actors.Where(e=>hidden.Contains(e.Entity)).All(e=>string.IsNullOrEmpty(e.Name)),"opponent generated deck pair private");
            Check(actors.All(e=>e.Tags.All(t=>t.Name!=32600)) && f.Entries.Where(e=>e.HasTagChange).All(e=>e.TagChange.Tag!=32600),"setup internal tags filtered");
        }
        B15HiddenDeckEffects(); B15NativeOpening();
        Console.WriteLine("PASS B15 native privacy and choices");
    }

    private static void B15HiddenDeckEffects()
    {
        foreach(bool enemy in new[]{false,true}) {
            var f=new Fixture(); var g=f.Game;
            if(enemy) f.Process(EndTurnTask.Any(f.Player));
            var p=enemy?g.Player2:g.Player1;
            var seller=f.Summon("CORE_ETC_111",p); f.Flush();
            seller.Power.Trigger.SingleTask.Process(g,p,seller,null,new SabberStoneCore.Model.TaskStack());
            var hidden=p.Opponent.DeckZone.TopCard;
            var plague=f.Summon("JAIL_443",p); 
            p.Opponent.Hero.TakeDamage(plague,8);f.Flush();
            var ids=p.Opponent.DeckZone.Where(e=>e.Card.Id=="JAIL_443t").Select(e=>e.Id).Append(hidden.Id).ToArray();
            Check(ids.Length==9,"native hidden Merch and Blight entities");
            var actors=f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null&&ids.Contains(e.Entity)).ToArray();
            Check(actors.All(e=>string.IsNullOrEmpty(e.Name)),"generated deck cards hidden from both seats");
            var setupSource=f.Summon("EDR_000",p);setupSource[StartOfGameRules.Pending]=-1;f.Flush();
            Check(!f.Match.SnapshotVisibleTransportTags(setupSource).ContainsKey(32600),"played setup source retains private phase without projecting it");
        }
    }
    private static void B15NativeOpening()
    {
        foreach(var fixture in new[]{(Hero:"HERO_01",Card:"JAIL_384"),(Hero:"HERO_06",Card:"EDR_845")}) {
            var choices=new System.Collections.Generic.List<PegasusGame.EntityChoices>();
            var match=new NativeMatch((id,message)=>{ if(id==17) choices.Add((PegasusGame.EntityChoices)message); },
                Cards.FromId(fixture.Hero),Enumerable.Repeat(Cards.FromId("CS2_182"),30),23);
            typeof(NativeMatch).GetField("playerCards",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .SetValue(match,new[]{fixture.Card,"EDR_000"}.Concat(Enumerable.Repeat("CS2_182",28)).Select(Cards.FromId).ToList());
            match.Start(); var g=match.Game;
            Check(g.Player1[GameTag.MAXRESOURCES]!=15 && g.Player2[GameTag.MAXRESOURCES]!=15,"native Ysera waits for mulligan");
            Check(choices.Count>0 && g.Player1.Choice!=null,"native mulligan emitted after setup");
            match.SendChoices(choices.Last().Id,new System.Collections.Generic.List<int>());
            Check(g.Player1.Choice==null && g.State==State.RUNNING,"native mulligan answer completes without setup stall");
            Check(g.Player1[GameTag.MAXRESOURCES]==15 && g.Player2[GameTag.MAXRESOURCES]==15,"native Ysera after mulligan");
        }
    }
}
