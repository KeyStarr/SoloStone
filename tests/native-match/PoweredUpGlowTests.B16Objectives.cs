using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Objectives;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b16-objectives", ProofKind.PoweredUp)]
    private static void B16ObjectivesGlow() => Scenario("BE_090",
        (f,card,met)=>{if(met)f.Player[ObjectiveState.CompletedRituals]=1;},
        (f,card)=>PlayCardTask.Minion(f.Player,card),
        (f,card)=>f.Player.Choice?.Choices.Count==3);
    [GroupProof("native-b16-objectives", ProofKind.Native)]
    private static void B16ObjectivesNative()
    {
        var f=new Fixture();var actor=f.Draw("BE_726");f.Process(PlayCardTask.Spell(f.Player,actor));
        var tags=f.Match.SnapshotVisibleTransportTags(actor);
        Check(tags[2311]==1 && tags[4960]==1 && tags[(int)GameTag.ZONE]==(int)SabberStoneCore.Enums.Zone.SECRET && tags[(int)GameTag.QUEST_PROGRESS_TOTAL]==2,"objective actor, ritual keyword and public progress");
        for(int i=0;i<4;i++)f.Process(EndTurnTask.Any(f.Game.CurrentPlayer));
        Check(f.Player.Choice?.Choices.Count==4 && f.Game.Step==Step.MAIN_START_TRIGGERS,"due native choice stops before ordinary draw");
        int selected=f.Player.Choice.Choices.Single(id=>f.Game.IdEntityDic[id].Card.Id=="BE_013t4");
        f.Process(ChooseTask.Pick(f.Player,selected));
        Check(f.Player.Choice==null && f.Game.Step==Step.MAIN_ACTION && f.Player.BoardZone.Count==2,"native choice resumes turn without stall");
        var enemy=f.Game.Player2;
        var foreign=Generic.DrawCard(enemy,Cards.FromId("CAP_405tb2"));
        foreign[GameTag.MODULAR_ENTITY_PART_1]=Cards.FromId("CAP_405t4").AssetId;
        foreign[GameTag.MODULAR_ENTITY_PART_2]=Cards.FromId("CAP_405t7").AssetId;
        var privateIds=new System.Collections.Generic.HashSet<int>{foreign.Id};f.Flush();
        bool hidden=false;try{f.Match.SnapshotVisibleTransportTags(foreign);}catch(InvalidOperationException){hidden=true;}
        Check(hidden,"hidden Trial cannot produce visible transport snapshot");
        foreach(var entry in f.Entries) {
            var entity=entry.HasFullEntity?entry.FullEntity:entry.HasShowEntity?entry.ShowEntity:entry.HasChangeEntity?entry.ChangeEntity:null;
            if(entity!=null && privateIds.Contains(entity.Entity))Check(string.IsNullOrEmpty(entity.Name) && entity.Tags.All(t=>t.Name!=471 && t.Name!=472),"hidden Trial identity/composition never disclosed");
            if(entity!=null)Check(entity.Tags.All(t=>t.Name<32900 || t.Name>32902),"internal objective tags filtered");
            if(entry.HasTagChange) {
                Check(entry.TagChange.Tag<32900 || entry.TagChange.Tag>32902,"internal tag deltas filtered");
                if(privateIds.Contains(entry.TagChange.Entity))Check(entry.TagChange.Tag!=471 && entry.TagChange.Tag!=472,"hidden Trial component deltas never disclosed");
            }
        }
        ObjectiveRules.Install((SabberStoneCore.Model.Entities.Spell)foreign);f.Flush();
        tags=f.Match.SnapshotVisibleTransportTags(foreign);
        Check(tags[(int)GameTag.MODULAR_ENTITY_PART_1]==132838 && tags[(int)GameTag.MODULAR_ENTITY_PART_2]==132851 && tags[2311]==1,"opponent active Trial exposes exact retail composition");
        foreach(var held in enemy.HandZone.ToArray())enemy.SetasideZone.Add(enemy.HandZone.Remove(held));
        var drawIds=enemy.DeckZone.Reverse().Take(4).Select(x=>x.Id).ToArray();
        f.Process(EndTurnTask.Any(f.Game.CurrentPlayer));
        foreach(var entry in f.Entries) {
            var entity=entry.HasFullEntity?entry.FullEntity:entry.HasShowEntity?entry.ShowEntity:entry.HasChangeEntity?entry.ChangeEntity:null;
            if(entity!=null && drawIds.Contains(entity.Entity))Check(string.IsNullOrEmpty(entity.Name),"opponent Trial-generated draws stay private: "+entity.Entity+"="+entity.Name+" zone="+f.Game.IdEntityDic[entity.Entity].Zone?.Type);
        }
        Check(foreign.Zone==enemy.GraveyardZone,"opponent one-start Trial completes");
    }
}
