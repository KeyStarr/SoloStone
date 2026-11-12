using System;
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
using EngineGame = SabberStoneCore.Model.Game;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    [GroupProof("native-b14-f3-past-pools", ProofKind.Native)]
    private static void B14F3PastPoolsNative()
    {
        AlternateRealityRepeatedReplacementPrivacy();
        foreach(bool opponent in new[]{false,true})
        foreach(string id in new[]{"BE_588","END_027","TIME_013","TIME_016","TIME_444","TIME_446","TIME_704","TIME_707","TIME_711","TIME_857"})
        {
            var g=new EngineGame(new GameConfig { RandomSeed=8,StartPlayer=1,Shuffle=false,SkipMulligan=true,
                History=true,Logging=false,FormatType=FormatType.FT_STANDARD,
                Player1HeroClass=CardClass.MAGE,Player2HeroClass=CardClass.MAGE,
                Player1Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList(),
                Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList() });
            g.StartGame(); if(opponent) Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)),"Past native opponent turn");
            var p=g.CurrentPlayer;
            foreach(var owner in new[]{p,p.Opponent})
                foreach(var e in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(e));
            var capture=new Capture(); var match=new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match,g);
            void Flush() { FlushHistory(match); g.PowerHistory.Last.Clear(); }
            Flush();
            if(id=="BE_588") Generic.SummonBlock(g,(Minion)EngineEntity.FromCard(p,Cards.FromId("GVG_058")),-1,null);
            if(id=="TIME_707") Generic.DrawCard(p,Cards.FromId("CS2_182"));
            p.BaseMana=10;p.UsedMana=0;
            var source=Generic.DrawCard(p,Cards.FromId(id));
            Check(g.Process(PlayCardTask.Any(p,source)),"Past native play "+id);
            if(id=="TIME_446") Check(LocationRules.Activate(p,(BoardObject)source),"Hold activation");
            if(id=="TIME_444") { p.UsedMana=0; Check(g.Process(PlayCardTask.Any(p,Generic.DrawCard(p,Cards.FromId("CS2_106")))),"Glaive replacement"); }
            if(id=="TIME_013") { p.UsedMana=0; Check(g.Process(PlayCardTask.Any(p,Generic.DrawCard(p,Cards.FromId("GAME_005")))),"Wo spell"); }
            Flush();
            var allOffers=new System.Collections.Generic.List<int>();
            while(p.Choice!=null)
            {
                allOffers.AddRange(p.Choice.Choices);
                var decision=g.GetPendingDecisions().Single();
                Check(g.TryAnswer(new DecisionAnswer(decision.Id,decision.ControllerId,new[]{p.Choice.Choices[0]}),out var error),"Past native answer "+error);
                Flush();
            }
            var hidden=g.IdEntityDic.Values.Where(e=>e.Zone==e.Controller.DeckZone
                || e[PayloadRules.Stored]!=0 && !PayloadRules.SharedWith(e,1) || opponent && (e.Zone==p.HandZone || allOffers.Contains(e.Id)))
                .Select(e=>e.Id).ToHashSet();
            foreach(var e in g.IdEntityDic.Values.Where(e=>hidden.Contains(e[GameTag.PARENT_CARD]))) hidden.Add(e.Id);
            var rows=NativeHistory(capture).ToArray();
            foreach(var row in rows)
            {
                foreach(var e in new[]{row.FullEntity,row.ShowEntity,row.ChangeEntity}.Where(e=>e!=null))
                {
                    Check(e.Tags.All(t=>!PayloadRules.IsInternalTag((GameTag)t.Name) && !Generic.IsDecisionInternalTag((GameTag)t.Name)
                        && t.Name!=(int)CardStateBoundary.ReplacedHiddenZone),"Past internal tags filtered");
                    if(hidden.Contains(e.Entity)) Check(string.IsNullOrEmpty(e.Name) && HasOnlyHiddenTags(e.Tags),id+" hidden identities and definition tags remain private");
                }
                if(row.HasTagChange && hidden.Contains(row.TagChange.Entity)) Check(IsPublicHiddenTag(row.TagChange.Tag),id+" hidden tag change");
            }
            if(!opponent && id=="TIME_704")
            {
                var pupil=p.HandZone.Single();
                // The client reads tag 2 as an entity id (CardTextBuilderType 23): the owner gets the taught spell as a named entity.
                var taught=PayloadRules.Entries(pupil,1).Single();
                Check(ReplayNativeEntityTags(rows,pupil.Id)[(int)GameTag.TAG_SCRIPT_DATA_NUM_1]==taught.Id
                    && rows.Any(r=>r.HasFullEntity && r.FullEntity.Entity==taught.Id && r.FullEntity.Name==taught.Card.Id),
                    "Pupil references its taught spell, named to the owner");
            }
            if(id=="TIME_707")
            {
                var drawn=new SabberStoneCore.EffectSystem.EffectContext(g,p.Id,source.Id).DrawEntity(); Flush();
                var after=NativeHistory(capture).ToArray();
                foreach(var option in drawn.ChooseOnePlayables)
                {
                    var snapshots=after.SelectMany(r=>new[]{r.FullEntity,r.ShowEntity,r.ChangeEntity}).Where(e=>e!=null && e.Entity==option.Id).ToArray();
                    Check(opponent ? snapshots.All(e=>string.IsNullOrEmpty(e.Name)) : snapshots.Any(e=>e.Name==option.Card.Id),
                        "draw reveals Choose One branches only to owner");
                }
            }
            capture.SerializeAll();
        }
        Console.WriteLine("PASS B14 F3 native generation, private hand/deck/offers/payload, owner Pupil display");
    }

    private static void AlternateRealityRepeatedReplacementPrivacy()
    {
        foreach (bool opponent in new[] { false, true })
        {
            var game = new EngineGame(new GameConfig { RandomSeed=8, StartPlayer=1, Shuffle=false, SkipMulligan=true,
                History=true, Logging=false, Player1HeroClass=CardClass.DRUID, Player2HeroClass=CardClass.DRUID,
                Player1Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList(),
                Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList() });
            game.StartGame();
            if (opponent) Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "replacement opponent turn");
            var owner = game.CurrentPlayer;
            Generic.DrawCard(owner,Cards.FromId("EX1_154"));
            var originalHidden = owner.DeckZone.Select(e=>e.Id)
                .Concat(opponent ? owner.HandZone.Select(e=>e.Id) : Array.Empty<int>()).ToArray();
            owner.BaseMana=10; owner.UsedMana=0;
            var first = Generic.DrawCard(owner, Cards.FromId("TIME_707"));
            Check(game.Process(PlayCardTask.Any(owner, first)), "first Alternate Reality cast");
            foreach (var fork in new[] { game, game.CloneExact(history:true), EngineGame.LoadSnapshot(game.SaveSnapshot()) })
            {
                var p = fork.CurrentPlayer;
                Check(originalHidden.All(id=>fork.IdEntityDic[id][CardStateBoundary.ReplacedHiddenZone]!=0),
                    "retired replacement privacy survives clone/snapshot");
                var hidden = p.DeckZone.Select(e=>e.Id).Concat(opponent ? p.HandZone.Select(e=>e.Id) : Array.Empty<int>()).ToHashSet();
                hidden.UnionWith(originalHidden);
                var capture = new Capture(); var match = new NativeMatch(capture.Send);
                typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match,fork);
                FlushHistory(match); fork.PowerHistory.Last.Clear();
                p.UsedMana=0;
                var second = Generic.DrawCard(p,Cards.FromId("TIME_707"));
                Check(fork.Process(PlayCardTask.Any(p,second)), "second Alternate Reality cast");
                FlushHistory(match); fork.PowerHistory.Last.Clear();
                foreach (var e in fork.IdEntityDic.Values.Where(e=>hidden.Contains(e[GameTag.PARENT_CARD]))) hidden.Add(e.Id);
                var rows = NativeHistory(capture).ToArray();
                foreach (var row in rows)
                {
                    foreach (var e in new[] { row.FullEntity,row.ShowEntity,row.ChangeEntity }.Where(e=>e!=null))
                    {
                        Check(e.Tags.All(t=>t.Name!=(int)CardStateBoundary.ReplacedHiddenZone), "replacement privacy tag never reaches client");
                        if (hidden.Contains(e.Entity)) Check(string.IsNullOrEmpty(e.Name) && HasOnlyHiddenTags(e.Tags),
                            "repeated Alternate Reality keeps replaced originals and branches private, seat="+p.PlayerId+" entity="+e.Entity);
                    }
                    if (row.HasTagChange && hidden.Contains(row.TagChange.Entity))
                        Check(IsPublicHiddenTag(row.TagChange.Tag), "retired replacement definition tag stays private");
                }
                Check(hidden.Any(id=>fork.IdEntityDic[id][GameTag.PARENT_CARD]>0), "second cast covers replaced Choose One branches");
                capture.SerializeAll();
            }
        }
    }
}
