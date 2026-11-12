using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    private static void Unpinned_FairIllusionDamage()
    {
        foreach (bool enemy in new[] { false, true })
        {
            var g = NativeTailGame();
            if (enemy) Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "illusion opponent fixture");
            var p = g.CurrentPlayer; p.BaseMana = 10;
            var source = Generic.DrawCard(p, Cards.FromId("EDR_780"));
            Check(g.Process(PlayCardTask.Any(p, source)), "fair illusion pair");
            int[] bodies = p.BoardZone.Select(c => c.Id).ToArray();
            var truth = g.IdEntityDic.Values.Where(e => PayloadRules.IsStored(e) && e[PayloadRules.Key] == NativeTailRules.IllusionKey)
                .OrderBy(e => e.Id).ToArray();
            int[] original = truth.Select(e => e[PayloadRules.Value]).ToArray();
            foreach (int viewer in new[] { p.Opponent.PlayerId, p.PlayerId })
            foreach (int seed in new[] { 1, 17, 42 })
            {
                int[] belief = null;
                foreach (bool flip in new[] { false, true })
                {
                    for (int i = 0; i < truth.Length; i++) truth[i][PayloadRules.Value] = flip ? 1 - original[i] : original[i];
                    var fair = FairBotState.Create(g, viewer, seed, DeckStyle.Tempo);
                    var view = (SabberStoneCore.Model.Game)typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fair);
                    var entries = view.IdEntityDic.Values.Where(e => PayloadRules.IsStored(e) && e[PayloadRules.Key] == NativeTailRules.IllusionKey)
                        .OrderBy(e => e.Id).ToArray();
                    foreach (var simulation in new[] { view, view.CloneExact() })
                    {
                        foreach (int id in bodies) ((Minion)simulation.IdEntityDic[id]).TakeDamage(simulation.Player1.Hero, 1);
                        simulation.DeathProcessingAndAuraUpdate();
                        Check(simulation.ControllerById(p.Id).BoardZone.Count == 1, "owner/opponent simulation damage destroys exactly one illusion");
                    }
                    Check(entries.Length == 2, "both viewpoints retain correlated anonymous illusion behavior");
                    Check(entries.All(e => string.IsNullOrEmpty(e.Card.Id)), "fair illusion payload identities are anonymous for both owners");
                    int[] sampled = entries.Select(e => e[PayloadRules.Value]).ToArray();
                    Check(sampled.Sum() == 1, "exactly one designated member per belief");
                    if (belief != null) Check(sampled.SequenceEqual(belief), "belief cannot depend on authoritative designation");
                    belief = sampled;
                    Check(truth.Select(e => e[PayloadRules.Value]).SequenceEqual(flip ? original.Select(v => 1 - v) : original), "fair sampling leaves authority untouched");
                }
            }
        }
    }

    private static SabberStoneCore.Model.Game NativeTailGame(bool timed=false)
    {
        var deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList();if(timed)deck[0]=Cards.FromId("CS3_035");
        var g=new SabberStoneCore.Model.Game(new GameConfig {RandomSeed=17,StartPlayer=1,Shuffle=false,SkipMulligan=true,
            History=true,Logging=false,Player1Deck=deck,Player2Deck=deck.ToList()});g.StartGame();return g;
    }
}
