using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    private static void EvTimeFairTimelines()
    {
        var failures = new System.Collections.Generic.List<string>();
        foreach (int seat in new[] { 1, 2 })
        foreach (string scenario in new[] { "return", "overflow", "fire", "cancel-fire", "full-board" })
        {
            try
            {
                var g = new SabberStoneCore.Model.Game(new GameConfig { RandomSeed = 17, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                    History = true, Logging = false, Player1HeroClass = CardClass.ROGUE, Player2HeroClass = CardClass.MAGE,
                    Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                    Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
                g.StartGame();
                if (seat == 2) Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "fair timeline seat");
                var p = g.CurrentPlayer; p.BaseMana = 10;
                foreach (var c in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(c));
                var fuel = Generic.DrawCard(p, Cards.FromId("CS2_231"));
                Generic.AddEnchantmentBlock(g, Cards.FromId("EX1_004e"), p.Hero, fuel);
                bool runi = scenario == "return" || scenario == "overflow";
                var source = Generic.DrawCard(p, Cards.FromId(runi ? "TIME_EVENT_998" : "CATA_EVENT_001"));
                Check(g.Process(PlayCardTask.Any(p, source)), "fair timeline play");
                if (!runi)
                {
                    var d = g.GetPendingDecisions().Single();
                    Check(g.TryAnswer(new DecisionAnswer(d.Id, d.ControllerId, new[] { fuel.Id }), out var error), "fair timeline ignition " + error);
                    if (scenario == "cancel-fire") Generic.DiscardBlock(p, fuel);
                    if (scenario == "full-board")
                        while (!p.BoardZone.IsFull) Generic.SummonBlock(g, (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p, Cards.FromId("CS2_231")), -1, source);
                }
                for (int i = 0; i < 3; i++) Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "fair timeline advance");
                if (scenario == "overflow")
                    while (!p.HandZone.IsFull) Generic.DrawCard(p, Cards.FromId("GAME_005"));
                var bytes = g.SaveSnapshot();
                var fair = FairBotState.Create(g, p.Opponent.PlayerId, 83, DeckStyle.Value);
                Check(bytes.SequenceEqual(g.SaveSnapshot()), "fair timeline projection leaves live state unchanged");
                var initial = (SabberStoneCore.Model.Game)typeof(FairBotState).GetField("game", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(fair);
                var projectedOwner = seat == 1 ? initial.Player1 : initial.Player2;
                int maskedId = scenario == "cancel-fire" ? fuel.Id : runi
                    ? projectedOwner.SetasideZone.Single(c => c[PayloadRules.Key] == HandTimelineRules.RuniKey).Id
                    : projectedOwner.HandZone[fuel.ZonePosition].Id;
                if (!runi && scenario != "cancel-fire")
                    Check(!projectedOwner.HandZone.Any(HandTimelineRules.IsBurning)
                        && projectedOwner.AppliedEnchantments.Single(e => e.Card.Id == "CATA_EVENT_001e").Target == projectedOwner,
                        "opponent fire schedule has no private hand target");
                var next = fair.Apply(fair.Actions.Single(a => a.Type == PlayerTaskType.END_TURN));
                var view = (SabberStoneCore.Model.Game)typeof(FairBotState).GetField("game", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(next);
                Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "live timeline boundary");
                if (!runi)
                {
                    bool refused = false;
                    try
                    {
                        var reply = next.Determinize(Enumerable.Repeat("CS2_231",
                            view.CurrentPlayer.HandZone.Count(c => c is Unknown)).ToArray(), 83).ForReply();
                        reply.ForReplyEndTurn();
                    }
                    catch (NotSupportedException e) when (e.Message == "Private historical Phoenix ignition target is unavailable in a fair view.") { refused = true; }
                    Check(refused == (scenario != "cancel-fire"), "active anonymous fire stops before private fuel resolution");
                    Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "live fire expiry");
                }
                var simulated = seat == 1 ? view.Player1 : view.Player2;
                var masked = view.IdEntityDic[maskedId];
                Check(scenario == "cancel-fire" || masked is Unknown && masked.Card.Id == "", "timeline never reveals hidden fuel definition");
                Check(scenario == "cancel-fire" || masked.AppliedEnchantments?.Any(a => a.Card.Id == "EX1_004e") != true, "timeline never retains hidden buffs");
                if (runi || scenario == "cancel-fire")
                    Check(masked.Zone.Type == fuel.Zone.Type && simulated.HandZone.Count == p.HandZone.Count,
                        $"{scenario} seat {seat}: live hand={p.HandZone.Count} zone={fuel.Zone.Type}, simulated hand={simulated.HandZone.Count} zone={masked.Zone.Type}");
                if (runi)
                    Check(masked.Zone.Type == (scenario == "return" ? SabberStoneCore.Enums.Zone.HAND : SabberStoneCore.Enums.Zone.GRAVEYARD), "fair Runi return/overflow result");
                else
                {
                    Check(p.BoardZone.Count(m => m.Card.Id == "CATA_EVENT_001") == (scenario == "fire" ? 2 : 1)
                        && p.BoardZone.Where(m => m.Card.Id == "CATA_EVENT_001").All(m => m.AttackDamage == 5 && m.Health == 5), "live Phoenix base summon/cancellation/capacity result");
                    Check(!HandTimelineRules.IsBurning(masked)
                        && masked.Zone.Type == (scenario == "cancel-fire" ? SabberStoneCore.Enums.Zone.GRAVEYARD : SabberStoneCore.Enums.Zone.HAND),
                        "public cancellation is retained; private expiry cannot move an arbitrary hand slot");
                }
                var restored = SabberStoneCore.Model.Game.LoadSnapshot(bytes);
                Check(bytes.SequenceEqual(restored.SaveSnapshot()), "timeline original snapshot remains round-trippable");
                Console.WriteLine($"PASS fair timeline {scenario} seat {seat}");
            }
            catch (Exception e) { failures.Add($"{scenario} seat {seat}: {e.Message}"); }
        }
        Check(failures.Count == 0, string.Join("\n", failures));
    }
}
