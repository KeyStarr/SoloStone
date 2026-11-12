using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-vh-window-events", ProofKind.PoweredUp)]
    private static void RunVhWindowEventsGlow()
    {
        foreach(string id in new[]{"BE_EVENT_100","BE_EVENT_101","BE_EVENT_102"})
        {
            Check(Cards.FromId(id).Implemented,id+" registered");
            var f=new Fixture(); var card=f.Draw(id);f.Flush();
            f.Expected(card,0,id+" unconditional spell, no PoweredUp bonus");
        }
    }

    [GroupProof("vh-window-events-native", ProofKind.Native)]
    private static void Unpinned_RunVhWindowEventsNative()
    {
        foreach(string id in new[]{"BE_EVENT_100","BE_EVENT_101"})
        {
            var f=new Fixture();var p=f.Player;
            var allowed=f.Draw(id=="BE_EVENT_100"?"CS2_182":"GAME_005");
            var blocked=f.Draw(id=="BE_EVENT_100"?"GAME_005":"CS2_182");
            var source=f.Draw(id);
            f.Process(PlayCardTask.Any(p,source));f.Flush();
            Check(f.Match.SnapshotVisibleTransportTags(blocked)[(int)GameTag.CANT_PLAY]==1
                && f.Match.SnapshotVisibleTransportTags(allowed).GetValueOrDefault((int)GameTag.CANT_PLAY)==0,
                "Unpinned_Arrival native retail CANT_PLAY reflects player restriction");
            Check(f.Entries.Any(e=>e.HasTagChange && e.TagChange.Entity==blocked.Id && e.TagChange.Tag==(int)GameTag.CANT_PLAY && e.TagChange.Value==1),"Arrival sends hand restriction update on wire");
            Check(f.Entries.Any(e=>e.HasShowEntity && e.ShowEntity.Name==id+"e"
                && e.ShowEntity.Tags.Any(t=>t.Name==(int)GameTag.ATTACHED && t.Value==p.Id)),"Arrival exported Watch attached to player");
            Check(p.Options().Any(t=>t is EndTurnTask) && p.Options().Any(t=>t.PlayerTaskType==PlayerTaskType.HERO_POWER),"Arrival native next decision remains actionable");
            f.Process(EndTurnTask.Any(p));f.Flush();
            Check(f.Match.SnapshotVisibleTransportTags(blocked).GetValueOrDefault((int)GameTag.CANT_PLAY)==0,"Arrival native expiry clears derived playability");

            Check(f.Entries.Any(e=>e.HasTagChange && e.TagChange.Entity==blocked.Id && e.TagChange.Tag==(int)GameTag.CANT_PLAY && e.TagChange.Value==0),"Arrival sends explicit zero on expiry");

            var hidden=new Fixture();var enemy=hidden.Game.Player2;
            hidden.Process(EndTurnTask.Any(hidden.Player));enemy.BaseMana=10;
            foreach(var c in enemy.HandZone.ToArray()) enemy.SetasideZone.Add(enemy.HandZone.Remove(c));
            foreach(var c in enemy.DeckZone.ToArray()) enemy.SetasideZone.Add(enemy.DeckZone.Remove(c));
            string cardId=id=="BE_EVENT_100"?"CS2_182":"GAME_005";
            var draws=Enumerable.Range(0,2).Select(_=>SabberStoneCore.Model.Entities.Entity.FromCard(enemy,Cards.FromId(cardId))).ToArray();
            foreach(var c in draws) enemy.DeckZone.Add(c);
            var cast=Generic.DrawCard(enemy,Cards.FromId(id));
            hidden.Process(PlayCardTask.Any(enemy,cast));hidden.Flush();
            Check(draws.All(c=>c.Zone==enemy.HandZone),"Arrival hidden physical draws delivered");
            var ids=draws.Select(c=>c.Id).ToArray();
            foreach(var e in hidden.Entries)
            {
                var entity=e.HasFullEntity?e.FullEntity:e.HasShowEntity?e.ShowEntity:e.HasChangeEntity?e.ChangeEntity:null;
                if(entity!=null && ids.Contains(entity.Entity))
                    Check(string.IsNullOrEmpty(entity.Name) && entity.Tags.All(t=>t.Name==(int)GameTag.ENTITY_ID || t.Name==(int)GameTag.CONTROLLER || t.Name==(int)GameTag.ZONE || t.Name==(int)GameTag.ZONE_POSITION),"Arrival opponent sees card backs only");
                if(e.HasTagChange && ids.Contains(e.TagChange.Entity))
                    Check(e.TagChange.Tag==(int)GameTag.ENTITY_ID || e.TagChange.Tag==(int)GameTag.CONTROLLER || e.TagChange.Tag==(int)GameTag.ZONE || e.TagChange.Tag==(int)GameTag.ZONE_POSITION,"Arrival opponent no hidden tags");
            }

        }
    }
}
