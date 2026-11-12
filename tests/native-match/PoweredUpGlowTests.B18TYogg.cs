using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("native-b18-t-yogg", ProofKind.Native)]
    private static void YoggNative()
    {
        var f = new Fixture(); f.Mana(10); var yogg = f.Draw("BE_009");
        int turn = f.Game.Turn;
        f.Process(PlayCardTask.Any(f.Player,yogg));
        Check(f.Game.CurrentPlayer != f.Player && f.Game.Turn == turn + 2,
            "native dispatch completes autonomous turn without human input");
        Check(f.Entries.Any(e => e.HasTagChange && e.TagChange.Entity == f.Player.Id
            && e.TagChange.Tag == (int)GameTag.AI_MAKES_DECISIONS_FOR_PLAYER && e.TagChange.Value == 1)
            && f.Entries.Any(e => e.HasTagChange && e.TagChange.Entity == f.Player.Id
            && e.TagChange.Tag == (int)GameTag.AI_MAKES_DECISIONS_FOR_PLAYER && e.TagChange.Value == 0),
            "native history brackets control with existing retail AI tag");
        YoggInputLock(); YoggConcession(); YoggHiddenAbilities();
        Console.WriteLine("PASS native Yogg: input lock, autonomous completion, AI tag, hidden transforms/draws, internal tags");
    }

    private static void YoggInputLock()
    {
        var f = new Fixture(); var g = f.Game; var p = f.Player;
        AutonomousTurnRules.Grant(p,p.Hero); EndTurnTask.Any(p).Process();
        Check(AutonomousTurnRules.IsActive(g),"native lock fixture enters controlled turn");
        byte[] before = g.SaveSnapshot();
        Action[] inputs = {
            () => f.Match.ValidateOption(-99,0,0,-1,0),
            () => f.Match.SendChoices(-99,new System.Collections.Generic.List<int>{0})
        };
        foreach(var input in inputs)
        {
            bool rejected = false;
            try { input(); } catch(InvalidOperationException e) { rejected = e.Message.Contains("Autonomous"); }
            Check(rejected && before.SequenceEqual(g.SaveSnapshot()),"native rejects normal human input without mutation");
        }
        foreach(string method in new[]{"SendHumanOptions","SendHumanChoice"})
            typeof(NativeMatch).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f.Match,null);
        Check(f.Match.HumanChoiceId == -1,"native does not expose an autonomous prompt");
        AutonomousTurnRules.Run(g); f.Flush();
        Check(!AutonomousTurnRules.IsActive(g),"native saved control can resume and release lock");
    }

    private static void YoggConcession()
    {
        foreach(bool opponent in new[] {false,true})
        foreach(bool pending in new[] {false,true})
        {
            var f = new Fixture(); var g = f.Game;
            if(opponent) g.Process(EndTurnTask.Any(f.Player));
            var p = g.CurrentPlayer; var god = f.Summon("BE_009",p);
            AutonomousTurnRules.Grant(p,god); EndTurnTask.Any(p).Process();
            p[GameTag.AI_MAKES_DECISIONS_FOR_PLAYER] = 1;
            p[(GameTag)33422] = 37;
            if(pending) Check(new ActivateOldGodTask(p,god).Process() && g.Resolution.HasSuspendedEffect,
                "native saved controlled turn has a pending choice");
            foreach(string method in new[]{"SendHumanOptions","SendHumanChoice"})
                typeof(NativeMatch).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f.Match,null);
            var rng = (Util.DeepCloneableRandom)typeof(SabberStoneCore.Model.Game).GetProperty("Random",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(g);
            var random = rng.CaptureState(); int turn = g.Turn;
            f.Flush(); f.Match.Concede();
            Check(g.State == State.COMPLETE && f.Player.PlayState == PlayState.LOST
                && f.Player.Opponent.PlayState == PlayState.WON,"native concession completes either controlled seat");
            Check(g.Player1.Choice == null && g.Player2.Choice == null && !g.Resolution.HasSuspendedEffect
                && g.GetPendingDecisions().Count == 0 && f.Match.HumanChoiceId == -1,
                "native concession clears pending choices and interactive prompt");
            Check(!AutonomousTurnRules.IsActive(g) && p[(GameTag)33421] == 0 && p[(GameTag)33422] == 0
                && p[GameTag.AI_MAKES_DECISIONS_FOR_PLAYER] == 0
                && !p.AppliedEnchantments.Any(e=>e.Card.Id=="BE_009e2"),"native concession releases control");
            Check(f.Entries.Any(e=>e.HasTagChange && e.TagChange.Entity==p.Id
                && e.TagChange.Tag==(int)GameTag.AI_MAKES_DECISIONS_FOR_PLAYER && e.TagChange.Value==0),
                "native concession publishes control release");
            Check(g.Turn == turn && rng.CaptureState().Equals(random),"native concession does not drain controlled turn");
        }
    }

    private static void YoggHiddenAbilities()
    {
        foreach(bool opponent in new[] {false,true})
        {
            var f = new Fixture(); var g = f.Game;
            if(opponent) g.Process(EndTurnTask.Any(f.Player));
            var p = g.CurrentPlayer;
            foreach(var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
            var hidden = Generic.DrawCard(p,Cards.FromId("CS2_046")); hidden.Cost = 2;
            var untouched = Generic.DrawCard(p,Cards.FromId("CORE_EX1_312")); untouched.Cost = 15;
            var drawn = SabberStoneCore.Model.Entities.Entity.FromCard(p,Cards.FromId("CORE_CS2_023"),zone:p.DeckZone);
            var god = f.Summon("BE_009",p); f.Flush();
            f.Process(new ActivateOldGodTask(p,god));
            int pick = p.Choice.Choices.Single(id=>g.IdEntityDic[id].Card.Id=="BE_009t4");
            f.Process(ChooseTask.Pick(p,pick));
            Check(drawn.Zone == p.HandZone,"Domain moves existing hidden deck spell to hand");
            f.Process(new ActivateOldGodTask(p,god));
            pick = p.Choice.Choices.Single(id=>g.IdEntityDic[id].Card.Id=="BE_009t3");
            f.Process(ChooseTask.Pick(p,pick));
            Check(hidden.Card.Cost == 5 && hidden.Cost == 2,"native Ascent preserves current price");
            var snapshots = f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null).ToArray();
            if(opponent)
                foreach(var card in new[]{hidden,untouched,drawn})
                    Check(snapshots.Where(e=>e.Entity==card.Id).All(e=>string.IsNullOrEmpty(e.Name)),
                        "opponent never receives hidden original, drawn or transformed identity");
            Check(snapshots.All(e=>e.Tags.All(t=>!AutonomousTurnRules.IsInternalTag((GameTag)t.Name)))
                && f.Entries.All(e=>!e.HasTagChange || !AutonomousTurnRules.IsInternalTag((GameTag)e.TagChange.Tag)),
                "autonomous private tags filtered from entity snapshots and deltas");
            Check(!PoweredUp.IsMet(god),"Yogg has no invented conditional glow");
        }
    }
}
