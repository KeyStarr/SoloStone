using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineGame = SabberStoneCore.Model.Game;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    [GroupProof("native-pastvar-spells", ProofKind.Native)]
    private static void PastVarietySpellsPrivacy()
    {
        PastSpellTokenRaces();
        foreach(bool second in new[]{false,true}) {
            var g=new EngineGame(new GameConfig { RandomSeed=8,StartPlayer=1,Shuffle=false,SkipMulligan=true,
                History=true,Logging=false,FormatType=FormatType.FT_WILD,
                Player1Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList(),Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList() });
            g.StartGame();if(second)Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)),"Past spell privacy second seat");
            var p=g.CurrentPlayer;foreach(var c in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(c));
            var capture=new Capture();var match=new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match,g);
            void Flush(){FlushHistory(match);g.PowerHistory.Last.Clear();}
            Flush();
            foreach(var id in "NEW1_007 EX1_164 EX1_165 EX1_166 EX1_178 NEW1_008 EX1_573 EX1_155".Split(' ')) {
                Generic.DrawCard(p,Cards.FromId(id));
                EngineEntity.FromCard(p,Cards.FromId(id),zone:p.DeckZone);
            }
            Flush();
            var hidden=p.DeckZone.Select(c=>c.Id).Concat(second ? p.HandZone.Select(c=>c.Id) : Array.Empty<int>()).ToHashSet();
            foreach(var c in g.IdEntityDic.Values.Where(e=>hidden.Contains(e[GameTag.PARENT_CARD])))hidden.Add(c.Id);
            Check(hidden.Any(i=>g.IdEntityDic[i][GameTag.PARENT_CARD]>0),"negative privacy case includes Choose One branches");
            foreach(var row in NativeHistory(capture)) {
                foreach(var e in new[]{row.FullEntity,row.ShowEntity,row.ChangeEntity}.Where(e=>e!=null && hidden.Contains(e.Entity)))
                    Check(string.IsNullOrEmpty(e.Name) && HasOnlyHiddenTags(e.Tags),"hidden Past Choose One parent/branch identity, seat="+p.PlayerId+" actor="+e.Entity);
                if(row.HasTagChange && hidden.Contains(row.TagChange.Entity))Check(IsPublicHiddenTag(row.TagChange.Tag),"hidden branch tag change");
            }
            var drawn=new SabberStoneCore.EffectSystem.EffectContext(g,p.Id,p.Hero.Id).DrawEntity();Flush();
            foreach(var branch in drawn.ChooseOnePlayables) {
                var actors=NativeHistory(capture).SelectMany(r=>new[]{r.FullEntity,r.ShowEntity,r.ChangeEntity}).Where(e=>e!=null && e.Entity==branch.Id).ToArray();
                Check(second ? actors.All(e=>string.IsNullOrEmpty(e.Name)) : actors.Any(e=>e.Name==branch.Card.Id),"draw reveals branches only to owner");
            }
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Past variety spells native parent privacy, both seats and draw");
    }
    private static void PastSpellTokenRaces()
    {
        foreach(var id in new[]{"EX1_248","ICC_085"})
        foreach(bool second in new[]{false,true}) {
            var g=new EngineGame(new GameConfig { RandomSeed=8,StartPlayer=1,Shuffle=false,SkipMulligan=true,
                History=true,Logging=false,FormatType=FormatType.FT_WILD,
                Player1Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList(),Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList() });
            g.StartGame();if(second)Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)),"token race second seat");
            var p=g.CurrentPlayer;p.BaseMana=10;
            var source=Generic.DrawCard(p,Cards.FromId(id));
            foreach(var fork in new[]{g,g.CloneExact(history:true),EngineGame.LoadSnapshot(g.SaveSnapshot())}) {
                var owner=fork.CurrentPlayer;
                Check(fork.Process(PlayCardTask.Any(owner,(IPlayable)fork.IdEntityDic[source.Id],id=="ICC_085" ? owner.Opponent.Hero : null)),"legacy token spell native path after clone/snapshot");
                var capture=new Capture();var match=new NativeMatch(capture.Send);
                typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match,fork);
                FlushHistory(match);
                string token=id=="EX1_248" ? "EX1_tk11" : "ICC_085t";
                var bodies=fork.CurrentPlayer.BoardZone.Where(m=>m.Card.Id==token).ToArray();
                Check(bodies.Length==(id=="EX1_248" ? 2 : 1),"actual current token identity/count");
                foreach(var body in bodies) {
                    var snapshots=NativeHistory(capture).SelectMany(r=>new[]{r.FullEntity,r.ShowEntity,r.ChangeEntity}).Where(e=>e!=null && e.Entity==body.Id && e.Name==token).ToArray();
                    Check(snapshots.Length>0 && snapshots.All(e=>e.Tags.Single(t=>t.Name==(int)GameTag.CARDRACE).Value==11),"public native token CARDRACE is Undead for both seats and restored games");
                    Check(body.Card.GetRawRace()==Race.UNDEAD && body.Card[GameTag.CARDRACE]==11 && body.Card.IsRace(Race.UNDEAD),"engine token race agrees with native actor");
                }
                capture.SerializeAll();
            }
        }
    }

}
