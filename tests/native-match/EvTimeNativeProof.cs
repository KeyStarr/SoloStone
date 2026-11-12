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

internal static partial class Program
{
    [GroupProof("ev-time-native", ProofKind.Native)]
    private static void EvTimeNative()
    {
        EvTimeFairTimelines();
        foreach (bool opponent in new[] { false, true })
        foreach (string id in new[] { "TIME_EVENT_998", "CATA_EVENT_001", "TIME_EVENT_999" })
        {
            var g = new SabberStoneCore.Model.Game(new GameConfig { RandomSeed = 17, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                History = true, Logging = false, Player1HeroClass = CardClass.ROGUE, Player2HeroClass = CardClass.MAGE,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            g.StartGame();
            if (opponent) Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "EVENT opponent turn");
            var p = g.CurrentPlayer; p.BaseMana = 10;
            foreach (var c in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(c));
            var capture = new Capture(); var match = new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, g);
            void Flush() { typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(match, new object[] { g.PowerHistory.Last }); g.PowerHistory.Last.Clear(); }
            void Answer(int chosen) {
                var d = g.GetPendingDecisions().Single();
                Check(g.TryAnswer(new DecisionAnswer(d.Id, d.ControllerId, new[] { chosen }), out var error), "EVENT answer " + error);
            }
            Flush();
            var hidden = Generic.DrawCard(p, Cards.FromId("CS2_231"));
            var source = Generic.DrawCard(p, Cards.FromId(id));
            Flush();
            Check(g.Process(PlayCardTask.Any(p, source)), "EVENT native play " + id);
            Flush();
            var offers = p.Choice?.Choices.ToArray() ?? Array.Empty<int>();
            if (offers.Length != 0)
            {
                if (!opponent)
                {
                    typeof(NativeMatch).GetMethod("SendHumanChoice", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(match, null);
                    var choice = capture.Last<EntityChoices>(17);
                    Check(choice.CountMin == 1 && choice.CountMax == 1 && choice.Entities.SequenceEqual(offers), "EVENT exact mandatory choices");
                    Check(choice.ChoiceType == (id == "CATA_EVENT_001" ? 3 : 2), "EVENT native TARGET/GENERAL choice shape");
                }
                Answer(offers[0]); Flush();
            }
            if (id == "TIME_EVENT_999") { Answer(g.Rewind.RewindEntityId); Flush(); Answer(p.Choice == null ? g.CurrentPlayer.Choice.Choices[0] : p.Choice.Choices[0]); Flush(); p = g.CurrentPlayer; }
            var before = g.SaveSnapshot();
            var fair = FairBotState.Create(g, p.Opponent.PlayerId, 83, DeckStyle.Value);
            var view = (SabberStoneCore.Model.Game)typeof(FairBotState).GetField("game", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(fair);
            FairViewHiddenOfferProof.AssertHiddenActors(g, view, p.PlayerId, hidden.Id);
            Check(before.SequenceEqual(g.SaveSnapshot()), "EVENT fair view leaves live state unchanged");
            if (id == "TIME_EVENT_998")
            {
                for (int i = 0; i < 4; i++) { Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "EVENT return clock"); Flush(); }
                Check(hidden.Zone == p.HandZone && ((Minion)hidden).AttackDamage == 6, "Runi native return delivers buffed original");
            }
            var rows = capture.Packets.Select(x => x.Message).OfType<PowerHistory>().SelectMany(x => x.List).ToArray();
            if (id == "TIME_EVENT_998")
            {
                var buffs = rows.Where(x => x.HasSubSpellStart && x.SubSpellStart.SpellPrefabGuid == HandBuffPrefab).ToArray();
                Check(opponent ? buffs.Length == 0 : buffs.Length == 1 && buffs[0].SubSpellStart.TargetEntityIds.SequenceEqual(new[] { hidden.Id }),
                    "Runi return has one explicit visible buff target and no opponent hidden target");
            }
            var actors = rows.SelectMany(x => new[] { x.FullEntity, x.ShowEntity, x.ChangeEntity }).Where(x => x != null).ToArray();
            if (opponent)
            {
                var privateIds = p.HandZone.Select(c => c.Id).Concat(offers).Append(hidden.Id).ToHashSet();
                Check(actors.Where(x => privateIds.Contains(x.Entity)).All(x => string.IsNullOrEmpty(x.Name)), "EVENT opponent card/offer identities hidden");
                var attachments = g.IdEntityDic.Values.OfType<SabberStoneCore.Model.Entities.Enchantment>().Where(e => e.Target.Id == hidden.Id).Select(e => e.Id).ToHashSet();
                Check(actors.Where(x => attachments.Contains(x.Entity)).All(x => string.IsNullOrEmpty(x.Name)), "EVENT opponent hand attachments hidden");
            }
            Check(rows.All(x => !x.HasTagChange || !TurnQueueRules.IsInternalTag((GameTag)x.TagChange.Tag) && !PayloadRules.IsInternalTag((GameTag)x.TagChange.Tag)), "EVENT internal changes filtered");
            Check(actors.All(x => x.Tags.All(t => !TurnQueueRules.IsInternalTag((GameTag)t.Name) && !PayloadRules.IsInternalTag((GameTag)t.Name))), "EVENT internal snapshots filtered");
            if (id == "CATA_EVENT_001")
            {
                for (int i = 0; i < 5; i++) { Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "EVENT fire clock"); Flush(); }
                var expiry = capture.Packets.Select(x => x.Message).OfType<PowerHistory>().SelectMany(x => x.List).ToArray();
                Check(expiry.Any(x => x.ShowEntity?.Entity == hidden.Id && x.ShowEntity.Name == hidden.Card.Id), "Phoenix expiry publicly reveals discarded fuel");
                Check(g.IdEntityDic[hidden.Id].Zone.Type == SabberStoneCore.Enums.Zone.GRAVEYARD && p.BoardZone.Count(m => m.Card.Id == id) == 2, "Phoenix native expiry effect result");
            }
        }
        Console.WriteLine("PASS Unpinned_EVENT native choices, escrow return and both-seat hidden-card privacy");
    }

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
