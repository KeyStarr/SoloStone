using System;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    [GroupProof("native-b18-o-native", ProofKind.Native)]
    private static void B18ONative()
    {
        var tick=typeof(NativeMatch).GetMethod("TickTurnTimer",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(long)},null);
        Check(tick!=null,"real native deadline polling absent");
        var g=new SabberStoneCore.Model.Game(new GameConfig { RandomSeed=17,StartPlayer=1,Shuffle=false,SkipMulligan=true,
            History=true,Logging=false,Player1Deck=Enumerable.Repeat(Cards.FromId("CS3_035"),30).ToList(),
            Player2Deck=Enumerable.Repeat(Cards.FromId("CS3_035"),30).ToList() });g.StartGame();
        var capture=new Capture();var match=new NativeMatch(capture.Send) { NativeHumanInputReady = () => true };
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match,g);
        FlushHistory(match);g.PowerHistory.Last.Clear();
        int turn=g.Turn;
        tick.Invoke(match,new object[]{0L});tick.Invoke(match,new object[]{14999L});
        Check(g.Turn==turn,"deadline cannot end early");
        tick.Invoke(match,new object[]{15000L});
        Check(g.Turn>turn,"deadline really ends turn");
        Check(NativeHistory(capture).Any(row=>row.HasTagChange && row.TagChange.Tag==7 && row.TagChange.Value==15),"retail TIMEOUT player tag projected");
        capture.SerializeAll();
        Unpinned_IllusionPrivacyAndEmotes();
        Unpinned_TimeoutChoice();
        Unpinned_DelayedNativeTurn();
    }
    private static void Unpinned_DelayedNativeTurn()
    {
        var g = NativeTailGame(true);
        var capture = new Capture(); var match = new NativeMatch(capture.Send);
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, g);
        bool queueDrained = false, busy = true, synchronized = false;
        Func<bool> ready = () => queueDrained && !busy && synchronized;
        match.NativeHumanInputReady = ready;
        Check(g.Process(EndTurnTask.Any(g.Player1)), "delayed fixture enters bot turn");
        typeof(NativeMatch).GetMethod("DriveUntilHumanInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
        Check(g.CurrentPlayer == g.Player1 && capture.Packets.Any(p => p.Id == 19), "bot turn publishes queued animation history before returning human input");
        int turn = g.Turn;
        long start = (long)(System.Diagnostics.Stopwatch.GetTimestamp() * (1000.0 / System.Diagnostics.Stopwatch.Frequency));
        match.TickTurnTimer(start + 30000);
        Check(g.Turn == turn, "30 seconds of queued bot/start animations cannot expire the human turn");
        queueDrained = true;
        match.TickTurnTimer(start + 40000);
        Check(g.Turn == turn, "drained queue still waits for client busy state");
        busy = false;
        match.TickTurnTimer(start + 50000);
        Check(g.Turn == turn, "idle client still waits for synchronized turn and options");
        synchronized = true;
        match.TickTurnTimer(start + 60000);
        match.TickTurnTimer(start + 74999);
        Check(g.Turn == turn, "full 15 seconds begin only at native readiness");
        busy = true;
        match.TickTurnTimer(start + 80000);
        Check(g.Turn == turn, "expired clock cannot fire while client animates");
        busy = false;
        match.TickTurnTimer(start + 80001);
        Check(g.Turn > turn, "ready client expires without refreshing the started deadline");
    }

    private static SabberStoneCore.Model.Game NativeTailGame(bool timed=false)
    {
        var deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList();if(timed)deck[0]=Cards.FromId("CS3_035");
        var g=new SabberStoneCore.Model.Game(new GameConfig {RandomSeed=17,StartPlayer=1,Shuffle=false,SkipMulligan=true,
            History=true,Logging=false,Player1Deck=deck,Player2Deck=deck.ToList()});g.StartGame();return g;
    }
    private static void Unpinned_IllusionPrivacyAndEmotes()
    {
        foreach(bool enemy in new[]{false,true})
        {
            var g=NativeTailGame();if(enemy)Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)),"opponent turn");
            var p=g.CurrentPlayer;p.BaseMana=10;
            var capture=new Capture();var match=new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match,g);
            FlushHistory(match);g.PowerHistory.Last.Clear();capture.Packets.Clear();
            var source=Generic.DrawCard(p,Cards.FromId("EDR_780"));
            Check(g.Process(PlayCardTask.Any(p,source)),"native Illusionist play");
            FlushHistory(match);g.PowerHistory.Last.Clear();
            var payloadIds=g.IdEntityDic.Values.Where(PayloadRules.IsStored).Select(e=>e.Id).ToHashSet();
            foreach(var row in NativeHistory(capture))
            {
                foreach(var e in new[]{row.FullEntity,row.ShowEntity,row.ChangeEntity}.Where(e=>e!=null))
                {
                    Check(e.Tags.All(t=>!NativeTailRules.IsInternalTag((GameTag)t.Name) && !PayloadRules.IsInternalTag((GameTag)t.Name)),"private illusion/flag tags filtered");
                    if(payloadIds.Contains(e.Entity))Check(string.IsNullOrEmpty(e.Name) && HasOnlyHiddenTags(e.Tags),"neither player sees designated payload identity");
                }
                if(row.HasTagChange)Check(!NativeTailRules.IsInternalTag((GameTag)row.TagChange.Tag) && !PayloadRules.IsInternalTag((GameTag)row.TagChange.Tag),"private deltas filtered");
            }
            var actors=NativeHistory(capture).Where(r=>r.ShowEntity?.Name=="EDR_780e1").Select(r=>r.ShowEntity).ToArray();
            Check(actors.Select(a=>a.Entity).Distinct().Count()==2 && actors.All(a=>!a.Tags.Any(t=>t.Name==2 && t.Value!=0)),"both public actors equally ambiguous: "+string.Join(",",actors.Select(a=>a.Entity+":"+string.Join("/",a.Tags.Select(t=>t.Name+"="+t.Value)))));
            // A copied hidden stored card shuffled to the opponent deck must not be exposed by the new facility.
            capture.Packets.Clear();
            var stored=PayloadRules.Entries(source,NativeTailRules.IllusionKey).Single();
            var hidden=Generic.Copy(p,source,stored,SabberStoneCore.Enums.Zone.DECK);FlushHistory(match);g.PowerHistory.Last.Clear();
            Check(NativeHistory(capture).SelectMany(r=>new[]{r.FullEntity,r.ShowEntity,r.ChangeEntity}).Where(e=>e!=null && e.Entity==hidden.Id)
                .All(e=>string.IsNullOrEmpty(e.Name) && HasOnlyHiddenTags(e.Tags)),"generated-to-deck identity remains hidden");
            Check(!match.TryEmote(p.Id,NativeMatch.OfflineEmote.Sorry),"Sorry rejected before unlock");
            var guard=(Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p,Cards.FromId("JAIL_703"));Generic.SummonBlock(g,guard,-1,null);guard.Destroy();g.DeathProcessingAndAuraUpdate();
            FlushHistory(match);g.PowerHistory.Last.Clear();
            Check(match.AvailableEmotes(p.Id).Contains(NativeMatch.OfflineEmote.Sorry) && !match.AvailableEmotes(p.Id).Contains(NativeMatch.OfflineEmote.Wow),"semantic menu replaces only Wow");
            Check(match.AvailableEmotes(p.Opponent.Id).Contains(NativeMatch.OfflineEmote.Wow),"other controller menu unchanged");
            Check(match.TryEmote(p.Id,NativeMatch.OfflineEmote.Sorry) && !match.TryEmote(p.Id,NativeMatch.OfflineEmote.Sorry),"semantic Sorry enabled with rate limit");
            match.EmotesSquelched=true;Check(!match.TryEmote(p.Opponent.Id,NativeMatch.OfflineEmote.Greetings),"squelch retained");
            Check(!match.NativeSorryTransportAvailable,"unqualified raw mapping explicitly disabled");
            capture.SerializeAll();
        }
    }
    private static void Unpinned_TimeoutChoice()
    {
        var g=NativeTailGame(true);var p=g.Player1;p.BaseMana=10;
        var capture=new Capture();var match=new NativeMatch(capture.Send) { NativeHumanInputReady = () => true };
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match,g);FlushHistory(match);g.PowerHistory.Last.Clear();
        match.TickTurnTimer(0);int turn=g.Turn;
        var discover=Generic.DrawCard(p,Cards.FromId("MEND_307"));Check(g.Process(PlayCardTask.Any(p,discover)),"mandatory Discover before deadline");
        Check(g.GetPendingDecisions().Any(),"real pending choice fixture");FlushHistory(match);g.PowerHistory.Last.Clear();
        typeof(NativeMatch).GetMethod("SendHumanChoice", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
        int published = capture.Last<EntityChoices>(17).Id;
        match.TickTurnTimer(15000);
        Check(capture.Last<EntitiesChosen>(13).ChooseEntities.Id == published, "timeout acknowledges the published choice ID");
        Check(g.Turn>turn && !g.Resolution.HasSuspendedEffect && p.Choice==null,"expiry answers choice and cannot stall");
        var ordinary=NativeTailGame();match=new NativeMatch(capture.Send);typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match,ordinary);
        int untimed=ordinary.Turn;match.TickTurnTimer(long.MaxValue-1);Check(ordinary.Turn==untimed,"ordinary matches remain untimed");
    }

}
