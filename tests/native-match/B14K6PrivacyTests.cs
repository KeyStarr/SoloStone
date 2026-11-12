using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using EngineGame = SabberStoneCore.Model.Game;
internal static partial class Program
{
    [GroupProof("native-b14-k6-rogue-warlock",ProofKind.Native)]
    private static void B14K6Privacy()
    {
        foreach(bool opponent in new[]{false,true})
        foreach(string id in new[]{"TLC_451","TLC_521"})
        {
            var g = new EngineGame(new GameConfig { RandomSeed=7,StartPlayer=1,Shuffle=false,SkipMulligan=true,
                History=true,Logging=false,FormatType=FormatType.FT_STANDARD,
                Player1HeroClass=CardClass.ROGUE,Player2HeroClass=CardClass.ROGUE,
                Player1Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList(),
                Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList() });
            g.StartGame(); if(opponent)Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)),"K6 other chooser turn");
            var p=g.CurrentPlayer;
            foreach(var owner in new[]{p,p.Opponent})
            {
                foreach(var held in owner.HandZone.ToArray())owner.SetasideZone.Add(owner.HandZone.Remove(held));
                Generic.DrawCard(owner,Cards.FromId("CS2_029"));Generic.DrawCard(owner,Cards.FromId("CS2_200"));
                EngineEntity.FromCard(owner,Cards.FromId("CS2_231"),zone:owner.DeckZone);
            }
            var capture=new Capture();var match=new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match,g);
            void Flush(){typeof(NativeMatch).GetMethod("SendHistory",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(match,new object[]{g.PowerHistory.Last});g.PowerHistory.Last.Clear();}
            Flush();p.BaseMana=10;p.UsedMana=0;
            var source=Generic.DrawCard(p,Cards.FromId(id));Check(g.Process(PlayCardTask.Any(p,source)),"K6 private play "+id);
            var offers=p.Choice.Choices.ToArray();Flush();
            if(!opponent)typeof(NativeMatch).GetMethod("SendHumanChoice",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(match,null);
            int selected=offers[0];var decision=g.GetPendingDecisions().Single();
            Check(g.TryAnswer(new DecisionAnswer(decision.Id,decision.ControllerId,new[]{selected}),out var error),"K6 answer "+error);Flush();
            var rows=capture.Packets.Select(t=>t.Message).OfType<PowerHistory>().SelectMany(t=>t.List).ToArray();
            var snapshots=rows.SelectMany(row=>new[]{row.FullEntity,row.ShowEntity,row.ChangeEntity}).Where(e=>e!=null).ToArray();
            foreach(int offer in offers)
                Check(snapshots.Where(e=>e.Entity==offer).Any() && snapshots.Where(e=>e.Entity==offer).All(e=>opponent?string.IsNullOrEmpty(e.Name):e.Name==g.IdEntityDic[offer].Card.Id),"K6 offer identity chooser only, after answer");
            var hidden=g.IdEntityDic.Values.Where(e=>e.Zone==e.Controller.DeckZone || e.Controller.PlayerId==2&&e.Zone==e.Controller.HandZone || PayloadRules.IsStored(e)).Select(e=>e.Id).ToHashSet();
            if(opponent)hidden.UnionWith(offers);
            bool Structural(int tag)=>tag==(int)GameTag.ENTITY_ID||tag==(int)GameTag.CONTROLLER||tag==(int)GameTag.ZONE||tag==(int)GameTag.ZONE_POSITION;
            foreach(var actor in snapshots.Where(e=>hidden.Contains(e.Entity)))
                Check(string.IsNullOrEmpty(actor.Name) && actor.Tags.All(t=>Structural(t.Name)),"K6 hidden actor has no identity or definition tags");
            Check(rows.Where(r=>r.HasTagChange&&hidden.Contains(r.TagChange.Entity)).All(r=>Structural(r.TagChange.Tag)),"K6 hidden definition tag changes filtered");
            foreach(var row in rows)
            {
                Check(!row.HasTagChange || !DiscoverRealRules.IsInternalTag((GameTag)row.TagChange.Tag)&&!PayloadRules.IsInternalTag((GameTag)row.TagChange.Tag),"K6 internal tag change filtered");
                foreach(var actor in new[]{row.FullEntity,row.ShowEntity,row.ChangeEntity}.Where(e=>e!=null))
                    Check(actor.Tags.All(t=>!DiscoverRealRules.IsInternalTag((GameTag)t.Name)&&!PayloadRules.IsInternalTag((GameTag)t.Name)),"K6 internal snapshot tags filtered");
            }
            Check(!opponent||OpposingPromptsHidden(capture,p.PlayerId),"K6 opponent prompt shows only card backs");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS K6 private choices: both choosers, originals, retired proxies, internal tags");
        B14K6GeneratedPrivacy();
        B14K6FrozenTransform();
    }
    private static void B14K6FrozenTransform()
    {
        foreach (bool opponent in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(); var g = match.Game;
            if (opponent) Check(g.Process(EndTurnTask.Any(g.Player1)), "Subjugator opponent turn");
            var p = g.CurrentPlayer;
            var neighbor = (Minion)EngineEntity.FromCard(p, Cards.FromId("CS2_182"), zone: p.BoardZone);
            neighbor.Silence();
            neighbor.IsFrozen = true;
            FlushFieldHistory(match);
            p.BaseMana = 10; p.UsedMana = 0;
            var source = Generic.DrawCard(p, Cards.FromId("BE_093"));
            Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "Subjugator transforms frozen neighbor");
            FlushFieldHistory(match);
            var tags = ReplayNativeEntityTags(NativeHistory(capture), neighbor.Id);
            Check(neighbor.Card.Id == "BE_093" && !neighbor.IsFrozen && !neighbor.IsSilenced
                && tags.GetValueOrDefault((int)GameTag.FROZEN) == 0
                && tags.GetValueOrDefault((int)GameTag.SILENCED) == 0, "transform clears prior Frozen/Silenced on engine and wire: card=" + neighbor.Card.Id + "; frozen=" + neighbor.IsFrozen + "; wire=" + tags.GetValueOrDefault((int)GameTag.FROZEN));
            capture.SerializeAll();
        }
    }
    private static void B14K6GeneratedPrivacy()
    {
        foreach(bool opponent in new[]{false,true})
        foreach(string id in new[]{"CATA_202","JAIL_474"})
        {
            var g = new EngineGame(new GameConfig { RandomSeed=7,StartPlayer=1,Shuffle=false,SkipMulligan=true,
                History=true,Logging=false,FormatType=FormatType.FT_STANDARD,
                Player1HeroClass=CardClass.ROGUE,Player2HeroClass=CardClass.ROGUE,
                Player1Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList(),
                Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList() });
            g.StartGame();if(opponent)Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)),"K6 generated opponent turn");
            var p=g.CurrentPlayer;
            foreach(var held in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(held));
            p.BaseMana=10;p.UsedMana=0;
            var paid=Generic.DrawCard(p,Cards.FromId("CS2_025"));Check(g.Process(PlayCardTask.Any(p,paid)),"K6 two-Mana history");
            var capture=new Capture();var match=new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match,g);
            void Flush(){typeof(NativeMatch).GetMethod("SendHistory",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(match,new object[]{g.PowerHistory.Last});g.PowerHistory.Last.Clear();}
            Flush();capture.Packets.Clear();
            var source=Generic.DrawCard(p,Cards.FromId(id));g.DeathProcessingAndAuraUpdate();Flush();
            if(id=="JAIL_474")Check(source[GameTag.TAG_SCRIPT_DATA_NUM_1]==1,"K6 hand displays total actual two-Mana count");
            p.UsedMana=0;Check(g.Process(PlayCardTask.Any(p,source)),"K6 generated private play");Flush();
            var generated=p.HandZone.ToArray();
            Check(generated.Length==(id=="CATA_202"?1:2),"K6 generated count");
            var rows=capture.Packets.Select(t=>t.Message).OfType<PowerHistory>().SelectMany(t=>t.List).ToArray();
            var snapshots=rows.SelectMany(row=>new[]{row.FullEntity,row.ShowEntity,row.ChangeEntity}).Where(e=>e!=null).ToArray();
            bool Structural(int tag)=>tag==(int)GameTag.ENTITY_ID||tag==(int)GameTag.CONTROLLER||tag==(int)GameTag.ZONE||tag==(int)GameTag.ZONE_POSITION;
            foreach(var card in generated)
            {
                var actors=snapshots.Where(e=>e.Entity==card.Id).ToArray();
                Check(actors.Length>0 && actors.All(e=>opponent?string.IsNullOrEmpty(e.Name)&&e.Tags.All(t=>Structural(t.Name)):e.Name==card.Card.Id),"K6 generated hand identity owner only");
                if(opponent)Check(rows.Where(r=>r.HasTagChange&&r.TagChange.Entity==card.Id).All(r=>Structural(r.TagChange.Tag)),"K6 generated hidden definition tags filtered");
                if(!opponent&&id=="JAIL_474")Check(card.Cost==7 && actors.Any(e=>e.Tags.Any(t=>t.Name==(int)GameTag.COST&&t.Value==7)),"K6 generated discount visible to owner");
            }
            Check(snapshots.All(e=>e.Tags.All(t=>!ShatterRules.IsInternalTag((GameTag)t.Name)))
                && rows.All(r=>!r.HasTagChange||!ShatterRules.IsInternalTag((GameTag)r.TagChange.Tag)),"K6 combined state private");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS K6 generated hand privacy: both controllers, combined state, discounted Cost");
    }
}
