using SabberStoneCore.Model;
using static Core2026Proof;
internal static class HistoryWindowProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "history window: " + message);
    internal static void Run()
    {
        foreach(bool history in new[]{false,true})
        {
            var g=HistoryFactsProof.Fixture(history);var p=g.Player1;var opponent=g.Player2;
            var q=g.HistoryFacts.Query;
            void Counts(int own,int other) => Check(q.TurnsTaken(p.Id)==own && q.TurnsTaken(opponent.Id)==other,
                "TurnsTaken includes current for both controllers");
            Counts(1,0);
            Check(!q.Plays(q.PreviousTurnOf(p.Id)).Any() && !q.Plays(q.PreviousTurnOf(opponent.Id)).Any(),
                "turn one has no previous turn for either controller");
            Play(g,p,"GAME_005");var first=q.Plays().Single();
            Check(q.Plays(q.ThisTurn).Single().PlayId==first.PlayId,"ThisTurn includes current play");
            Check(!q.BeforeThisPlay(first.PlayId).Plays().Any(),"BeforeThisPlay excludes current first play");
            Check(!q.BeforeThisPlay(first.PlayId).BeforeThisPlay(first.PlayId).Plays().Any(),
                "chained BeforeThisPlay retains the exclusive cursor");
            p.NumTurnsLeft=2;
            int visible=0;
            g.TriggerManager.TurnStartTrigger += _=>{
                Check(q.Events<TurnStartedFact>().Last().Turn==g.Turn
                    && q.Events<TurnStartedFact>().Last().ControllerId==g.CurrentPlayer.Id,
                    "TurnStarted visible inside actual turn-start trigger");visible++;
            };
            End(g);Counts(2,0);
            Check(g.CurrentPlayer==p && q.Plays(q.PreviousTurnOf(p.Id)).Single().PlayId==first.PlayId
                && !q.Plays(q.PreviousTurnOf(opponent.Id)).Any(),"extra consecutive turn uses owner chronology");
            Check(!q.Plays(q.ThisTurn).Any(),"new ThisTurn starts empty");
            Play(g,p,"GAME_005");var extra=q.Plays().Last();
            Check(q.Plays(q.ThisTurn).Single().PlayId==extra.PlayId
                && q.BeforeThisPlay(extra.PlayId).Plays().Single().PlayId==first.PlayId,
                "BeforeThisPlay retains prior occurrence but excludes current");
            End(g);Counts(2,1);
            Check(g.CurrentPlayer==opponent && q.Plays(q.PreviousTurnOf(p.Id)).Single().PlayId==extra.PlayId
                && !q.Plays(q.PreviousTurnOf(opponent.Id)).Any(),"first opponent turn has no previous opponent turn");
            Play(g,opponent,"GAME_005");var opposing=q.Plays().Last();
            Check(q.Plays(q.ThisTurn).Single().PlayId==opposing.PlayId,"ThisTurn follows opponent controller");
            End(g);Counts(3,1);
            Check(q.Plays(q.PreviousTurnOf(p.Id)).Single().PlayId==extra.PlayId
                && q.Plays(q.PreviousTurnOf(opponent.Id)).Single().PlayId==opposing.PlayId,
                "both previous windows independent after extra turn");
            Play(g,p,"GAME_005");var latest=q.Plays().Last();End(g);Counts(3,2);
            Check(q.Plays(q.PreviousTurnOf(p.Id)).Single().PlayId==latest.PlayId
                && q.Plays(q.PreviousTurnOf(opponent.Id)).Single().PlayId==opposing.PlayId
                && !q.Plays(q.ThisTurn).Any(),"both owner windows exclude current turn");
            Check(visible==4,"one event per begun turn including extra");
        }
        Console.WriteLine("PASS history windows: both controllers, ThisTurn/PreviousTurnOf, extra/no-previous turn, current TurnsTaken, BeforeThisPlay");
    }
}
