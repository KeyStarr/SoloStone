using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b16-old-gods", ProofKind.PoweredUp)]
    private static void OldGodGlow() {
        Scenario("BE_784", (f,card,met) => {
            if (met) for(int i=0;i<4;i++) {
                f.Mana(10); f.Process(PlayCardTask.Any(f.Player,f.Draw("CS2_200")));
                foreach(var body in f.Player.BoardZone.ToArray()) f.Player.SetasideZone.Add(f.Player.BoardZone.Remove(body));
            }
        }, (f,card) => PlayCardTask.Any(f.Player,card), (f,card) => ((Minion)card).IsImmune);
        var empty = new Fixture();
        foreach(var id in new[] { "BE_013","BE_017" }) Check(!PoweredUp.IsMet(empty.Draw(id)),"unconditional/scaling Old God has no hand glow");
    }
    [GroupProof("native-b16-old-gods", ProofKind.Native)]
    private static void OldGodNative() {
        foreach(bool opponent in new[] {false,true}) {
            var f = new Fixture(); var g = f.Game;
            if(opponent) Check(g.Process(EndTurnTask.Any(f.Player)),"Old God opponent turn");
            var owner = opponent ? g.Player2 : g.Player1;
            foreach(var old in owner.BoardZone.ToArray()) owner.SetasideZone.Add(owner.BoardZone.Remove(old));
            var hidden = Generic.DrawCard(owner,Cards.FromId("CS2_029"));
            var god = f.Summon("BE_784",owner); f.Flush();
            var botTasks = new System.Collections.Generic.List<PlayerTask> { EndTurnTask.Any(owner),new ActivateOldGodTask(owner,god) };
            var selectedBot = (PlayerTask)typeof(NativeMatch).GetMethod("ChooseBotTask",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f.Match,new object[] {botTasks});
            Check(selectedBot is ActivateOldGodTask,"bot uses available Old God action before ending turn");
            if(!opponent) {
                typeof(NativeMatch).GetMethod("SendHumanOptions",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f.Match,null);
                var opts=(SabberStoneCore.Kettle.PowerAllOptions)typeof(NativeMatch).GetField("humanOptions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f.Match);
                int index=opts.PowerOptionList.FindIndex(o=>o.MainOption?.EntityId==god.Id);
                Check(index>=0 && opts.PowerOptionList[index].MainOption.PowerKeyword==0,"native POWER option on Old God body");
                var task=f.Match.ValidateOption(opts.Index,index,0,-1,0);
                Check(task is ActivateOldGodTask,"native option dispatches shared action");
                f.Process(task);
            } else f.Process(new ActivateOldGodTask(owner,god));
            int pick=owner.Choice.Choices.Single(id=>g.IdEntityDic[id].Card.Id=="BE_784t2");
            f.Process(ChooseTask.Pick(owner,pick));
            Check(owner.Choice==null,"Spawn completes without stalled decision");
            Check(f.Entries.Any(x=>x.HasPowerStart && x.PowerStart.Source==pick),"selected exported ability actor sources POWER block");
            Check(g.HistoryFacts.Query.Plays().All(x=>x.EntityId!=god.Id && x.EntityId!=pick)
                && !g.HistoryFacts.Query.Casts().Any(x=>x.EntityId==pick),"activation records no card play or spell cast");
            var generated=owner.HandZone.Where(c=>c.Id!=hidden.Id).OrderBy(c=>c.Id).Last();
            Check(owner.BoardZone.Any(c=>c.Card.Id==generated.Card.Id),"same picked identity summoned and copied to hand");
            var raptor = f.Summon("CS2_172",owner);
            Generic.AddEnchantmentBlock(g,Cards.FromId("BE_017t6e"),god,raptor);
            Generic.Copy(owner,god,raptor,SabberStoneCore.Enums.Zone.PLAY);
            g.DeathProcessingAndAuraUpdate(); f.Flush();
            Check(owner[(GameTag)32702] == 1,"native fixture records granted Deathrattle summon correction");
            var snapshots=f.Entries.SelectMany(x=>new[]{x.FullEntity,x.ShowEntity,x.ChangeEntity}).Where(x=>x!=null).ToArray();
            if(opponent) {
                Check(snapshots.Where(x=>x.Entity==generated.Id).All(x=>string.IsNullOrEmpty(x.Name)),"opponent generated hand identity stays private");
                Check(snapshots.Where(x=>x.Entity==hidden.Id).All(x=>string.IsNullOrEmpty(x.Name)),"unrelated hidden hand identity stays private");
            }
            Check(snapshots.All(x=>x.Tags.All(t=>!OldGodRules.IsInternalTag((GameTag)t.Name)))
                && f.Entries.All(x=>!x.HasTagChange || !OldGodRules.IsInternalTag((GameTag)x.TagChange.Tag)),"Old God private tags filtered from snapshots and deltas");
            Check(f.Match.SnapshotVisibleTransportTags(god).ContainsKey(4754) && f.Match.SnapshotVisibleTransportTags(god).ContainsKey(4786),"printed Old God/action tags projected");
        }
        Console.WriteLine("PASS native Old Gods: POWER dispatch, both seats, hidden generated copy, internal tags, decision completion");
    }
}
