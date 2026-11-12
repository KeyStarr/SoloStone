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
    [GroupProof("powered-up-b18-t-misc", ProofKind.PoweredUp)]
    private static void RunB18TMiscGlow()
    {
        foreach (string id in new[] { "BE_004", "TLC_452", "END_018", "TIME_432", "YOG_514" }) {
            var f=new Fixture(); var card=f.Draw(id); f.Flush(); f.Expected(card,0,"unconditional "+id);
        }
    }
    [GroupProof("native-b18-t-misc", ProofKind.Native)]
    private static void Unpinned_B18TMiscNative()
    {
        foreach(bool opponent in new[] {false,true}) {
            var f=new Fixture();var g=f.Game;
            if(opponent) Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)),"opponent turn");
            var p=g.CurrentPlayer;
            foreach(var old in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(old));
            var target=Generic.DrawCard(p,Cards.FromId("CS2_200"));
            var host=Generic.DrawCard(p,Cards.FromId("END_018"));p.BaseMana=10;p.UsedMana=0;f.Flush();
            Check(g.Process(PlayCardTask.Any(p,host)),"mark play");f.Flush();
            Check(target.Cost==int.MaxValue,"native numeric infinity");
            if(!opponent) Check(f.Match.SnapshotVisibleTransportTags(target)[(int)GameTag.COST]==int.MaxValue,"ordinary COST wire tag");
            ((Minion)host).ToBeDestroyed=true;g.DeathProcessingAndAuraUpdate();f.Flush();
            Check(target.Cost==6,"native restoration");
            var osk=Generic.DrawCard(p,Cards.FromId("TLC_452"));f.Flush();
            Check(osk.Card.Id.StartsWith("TLC_452t"),"held change entity");
            var fate=Generic.DrawCard(p,Cards.FromId("TIME_432"));p.UsedMana=0;f.Flush();
            Check(g.Process(PlayCardTask.Any(p,fate)),"Fate play");f.Flush();
            var hidden=g.Player2.DeckZone.Select(c=>c.Id).ToHashSet();
            while(p.Choice!=null) {
                if(opponent) foreach(int id in p.Choice.Choices) hidden.Add(id);
                var d=g.GetPendingDecisions().Single();
                Check(g.TryAnswer(new DecisionAnswer(d.Id,p.Id,new[]{p.Choice.Choices[0]}),out var error),"Fate choice: "+error);f.Flush();
            }
            if(opponent) foreach(var card in p.HandZone) hidden.Add(card.Id);
            var actors=f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null).ToArray();
            Check(actors.Where(e=>hidden.Contains(e.Entity)).All(e=>string.IsNullOrEmpty(e.Name)),"hidden originals/copies/offers remain private");
            Check(f.Entries.All(e=>!e.HasTagChange || !InfinityRules.IsInternalTag((GameTag)e.TagChange.Tag)),"private cost journal tagchanges");
            Check(actors.All(e=>e.Tags.All(t=>!InfinityRules.IsInternalTag((GameTag)t.Name))),"private cost journal snapshots");
            var first=f.Summon("CS2_200",p.Opponent);var second=f.Summon("CS2_182",p.Opponent);
            g.DeathProcessingAndAuraUpdate();
            var banish=Generic.DrawCard(p,Cards.FromId("TLC_452t21"));p.UsedMana=0;f.Flush();
            f.Process(PlayCardTask.Any(p,banish,first));
            Check(p.Choice!=null && p.Choice.Choices.Contains(second.Id),"native second banish target offered");
            f.Process(ChooseTask.Pick(p,second.Id));
            Check(p.Choice==null && first[GameTag.ZONE]==(int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME && second[GameTag.ZONE]==(int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME,"native dispatcher completes both banishes");
            Check(f.Entries.Any(e=>e.HasTagChange && e.TagChange.Entity==first.Id && e.TagChange.Tag==(int)GameTag.ZONE && e.TagChange.Value==(int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME),"native banish projects removed zone");
            var definition=SabberStoneCore.Model.Card.CreateAssembledCard(Cards.FromId("CATA_133"),Cards.FromId("CATA_133"),Cards.FromId("CATA_999"));
            var power=BasicHeroPowerRules.InstallPassive(p,host,definition);f.Flush();
            Check(power.IsPassiveHeroPower && !power.IsPlayableByPlayer,"native passive cannot be clicked");
            Check(p.Options().All(task=>task.PlayerTaskType!=PlayerTaskType.HERO_POWER),"no passive power option to stall native play");
            var tags=f.Match.SnapshotVisibleTransportTags(power);
            Check(tags.TryGetValue(4572,out int firstHalf) && firstHalf==Cards.FromId("CATA_133").AssetId && tags.ContainsKey(4573),"retail composed power halves");
        }
        Console.WriteLine("PASS B18 miscellaneous native actors, decisions, private copies and cost journals");
    }
}
