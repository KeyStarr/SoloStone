using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b14-h4-identity", ProofKind.PoweredUp)]
    private static void RunB14H4Glow()
    {
        var edge = new Fixture();
        edge.Draw("CS2_182");
        var echoes = edge.Draw("END_005");
        edge.Flush(); edge.Expected(echoes, 1, "Bygone Echoes at hand edge without corpses");
        edge.Draw("CS2_182");
        edge.Flush(); edge.Expected(echoes, 0, "Bygone Echoes leaves edge without corpses");
        CorpseRules.Gain(edge.Player, 4);
        edge.Flush(); edge.Expected(echoes, 1, "Bygone Echoes qualifies in middle with four corpses");
        Check(CorpseRules.TrySpend(edge.Player, 4), "spend Bygone Echoes glow fixture corpses");
        edge.Flush(); edge.Expected(echoes, 0, "Bygone Echoes loses both conditions");
        Scenario("END_005",(f,card,met)=>{
            f.Player.SetasideZone.Add(f.Player.HandZone.Remove(card));
            f.Draw("CS2_182");f.Player.HandZone.Add(f.Player.SetasideZone.Remove(card));f.Draw("CS2_182");
            CorpseRules.Gain(f.Player,met?4:3);
        },(f,card)=>PlayCardTask.Any(f.Player,card),(f,card)=>f.Player.BoardZone.Count==2);
        Scenario("CATA_140",(f,card,met)=>{
            var target=f.Summon("CS2_200",f.Player.Opponent);target.BaseHealth=100;
            for(int i=0;i<6;i++) { f.Mana(10);var spell=f.Draw("CS2_029");f.Process(PlayCardTask.Any(f.Player,spell,target)); }
            if(met) { f.Mana(10);f.Process(PlayCardTask.Any(f.Player,f.Draw("CS2_171"))); }
        },(f,card)=>PlayCardTask.Any(f.Player,card),(f,card)=>f.Player.HandZone.Count==10 && f.Player.HandZone.All(c=>c.Cost==1));
    }
    [GroupProof("native-b14-h4-identity", ProofKind.Native)]
    private static void RunB14H4Privacy()
    {
        foreach(bool opponent in new[]{false,true})
        foreach(string id in new[]{"EDR_270","CATA_140","CORE_CS2_023","BE_781"})
        {
            var f=new Fixture();var g=f.Game;
            if(opponent)Check(g.Process(EndTurnTask.Any(f.Player)),"opponent turn");
            var owner=opponent?g.Player2:g.Player1;
            foreach(var held in owner.HandZone.ToArray())owner.SetasideZone.Add(owner.HandZone.Remove(held));
            var card=Generic.DrawCard(owner,Cards.FromId(id));owner.BaseMana=10;owner.UsedMana=0;f.Flush();
            if(id=="BE_781")
            {
                var rune=Generic.DrawCard(owner,Cards.FromId("RLK_060"));
                Check(g.Process(PlayCardTask.Any(owner,rune)),"transform by owner Unholy card");
                Check(card.Card.Id=="BE_781t8","hand transformed");
            }
            else Check(g.Process(PlayCardTask.Any(owner,card)),"privacy card played");
            f.Flush();
            if(owner.Choice!=null)
            {
                var offers=owner.Choice.Choices.ToArray();
                if(opponent)Check(f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity})
                    .Where(e=>e!=null && offers.Contains(e.Entity)).All(e=>string.IsNullOrEmpty(e.Name)),
                    "opponent unselected Discover offers remain hidden");
                var decision=g.GetPendingDecisions().Single();
                Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id,decision.ControllerId,new[]{owner.Choice.Choices[0]}),out _),"answer Discover");
                f.Flush();
            }
            var handIds=owner.HandZone.Select(c=>c.Id).ToArray();
            var entities=f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null && handIds.Contains(e.Entity)).ToArray();
            if(opponent)
            {
                bool Public(int tag)=>tag==(int)GameTag.ENTITY_ID || tag==(int)GameTag.CONTROLLER || tag==(int)GameTag.ZONE || tag==(int)GameTag.ZONE_POSITION;
                Check(entities.All(e=>string.IsNullOrEmpty(e.Name) && e.Tags.All(t=>Public(t.Name))),"opponent hand identity/tags remain hidden: "+id);
                Check(f.Entries.Where(e=>e.HasTagChange && handIds.Contains(e.TagChange.Entity)).All(e=>Public(e.TagChange.Tag)),"opponent progress/cost tags remain hidden: "+id);
            }
            else Check(handIds.All(i=>entities.Any(e=>e.Entity==i && !string.IsNullOrEmpty(e.Name))),"owner sees generated/transformed/drawn identities: "+id);
        }
        foreach(bool opponent in new[]{false,true})
        {
            var f=new Fixture();var g=f.Game;
            if(opponent)Check(g.Process(EndTurnTask.Any(f.Player)),"opposing Wolf turn");
            var owner=opponent?g.Player2:g.Player1;owner.BaseMana=10;owner.UsedMana=0;
            var source=Generic.DrawCard(owner,Cards.FromId("EDR_233"));
            f.Flush();Check(g.Process(PlayCardTask.Any(owner,source,chooseOne:1)),"Wolf branch played");f.Flush();
            foreach(var wolf in owner.BoardZone)
            {
                var snapshot=f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Last(e=>e!=null && e.Entity==wolf.Id);
                Check(snapshot.Name=="EX1_tk11" && snapshot.Tags.Single(t=>t.Name==(int)GameTag.CARDRACE).Value==11
                    && snapshot.Tags.Single(t=>t.Name==2542).Value==1,"captured current Wolf has Undead and Beast metadata on wire");
            }
            Check(Cards.FromId("EX1_tk11")[GameTag.CARDRACE]==11 && Cards.FromId("EX1_tk11")[(GameTag)2542]==1
                && !ReferenceEquals(owner.BoardZone.First().Card,Cards.FromId("EX1_tk11")),
                "scoped Wolf snapshot stays distinct from current-qualified Feral Spirit definition");
            var returned=owner.BoardZone.Last();
            Check(Generic.ReturnToHandBlock(owner,returned),"return captured Wolf to hand");f.Flush();
            owner.UsedMana=0;Check(g.Process(PlayCardTask.Any(owner,returned)),"replay captured Wolf");f.Flush();
            var replayed=f.Entries.Where(e=>e.HasShowEntity && e.ShowEntity.Entity==returned.Id).Last().ShowEntity;
            Check(replayed.Tags.Single(t=>t.Name==200).Value==11 && replayed.Tags.Single(t=>t.Name==2542).Value==1,
                "bounced and replayed Wolf retains captured Undead and Beast on wire");
            var sheep=owner.BoardZone.First();
            owner.UsedMana=0;
            var polymorph=Generic.DrawCard(owner,Cards.FromId("CS2_022"));
            f.Process(PlayCardTask.Any(owner,polymorph,sheep));
            var changed=f.Entries.Where(e=>e.HasChangeEntity && e.ChangeEntity.Entity==sheep.Id).Last().ChangeEntity;
            Check(changed.Name=="CS2_tk1" && changed.Tags.Single(t=>t.Name==200).Value==20
                && changed.Tags.Single(t=>t.Name==2542).Value==0,"Wolf transforms into Beast Sheep on wire");
            owner.UsedMana=0;
            var portal=Generic.DrawCard(owner,Cards.FromId("TIME_000"));
            f.Process(PlayCardTask.Any(owner,portal));
            Check(g.Rewind!=null,"Portal offers Rewind after Wolf transformation");
            int cursor=f.Entries.Count();
            f.Process(ChooseTask.Pick(owner,g.Rewind.RewindEntityId));
            Check(f.Entries.Skip(cursor).Any(e=>e.HasResetGame),"native Rewind emits reset");
            var resetSheep=f.Entries.Skip(cursor).Where(e=>e.HasFullEntity && e.FullEntity.Entity==sheep.Id).Single().FullEntity;
            Check(resetSheep.Name=="CS2_tk1" && resetSheep.Tags.Single(t=>t.Name==200).Value==20
                && resetSheep.Tags.Where(t=>t.Name==2542).Select(t=>t.Value).SingleOrDefault()==0,"native reset publishes Sheep races");
            var restored=f.Game.IdEntityDic[sheep.Id];
            var tags=f.Match.SnapshotVisibleTransportTags(restored);
            Check(restored.Card.Id=="CS2_tk1" && tags[200]==20 && (!tags.TryGetValue(2542,out int beast) || beast==0),
                "Rewind reset keeps Sheep races, never stale Wolf overrides");
        }
        Console.WriteLine("PASS native b14-h4-identity privacy and Wolf snapshot races, both seats");
    }
}
