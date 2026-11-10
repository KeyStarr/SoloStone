using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static DriverTests;

internal static class FairnessClosureTests
{
    private static FairOptions Options() => new() { TrackOwnDeck = true, MaterializeOwnDeckTriggers = true, RemoveOpponentSecrets = true };
    private static FairBotState View(Game game, int seat, FairOptions options = null) =>
        FairBotState.Create(game, seat, 73, DeckStyle.Tempo, options ?? Options());
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (Action test in new Action[] { PhoenixPrivacyTests.Run, HiddenChildTopology, PublicOverdrawDeparture, SameEntityDeckTransform, OffboardSetaside, StormfuryDeparture, NabShuffle, EventTimelines, EventQueuedChoice, RetiredPublicBranches, PromotedPrivateOffer })
            try { test(); Console.WriteLine("PASS fairness closure " + test.Method.Name); }
            catch (Exception e) { failures.Add(test.Method.Name + ": " + e); }
        DriverTests.Check(failures.Count == 0, string.Join("\n", failures));
    }
    private static void PromotedPrivateOffer()
    {
        foreach (int seat in new[] { 1, 2 })
        {
            var game = DriverTests.Fixture(seat);
            var enemy = game.CurrentPlayer.Opponent;
            var offer = Entity.FromCard(enemy, Cards.FromId("TIME_014"), zone: enemy.SetasideZone);
            Generic.AddHandPhase(enemy, offer);
            DriverTests.Check(enemy.SetasideZone.Contains(offer) && enemy.HandZone.Contains(offer), "Fixture needs promoted offer alias");
            var before = game.SaveSnapshot();
            var view = View(game, seat);
            foreach (var copy in new[] { FairViewTests.Copy(view), FairViewTests.Copy(view.WithArtificialSeed(1)) })
            {
                DriverTests.Check(copy.IdEntityDic.Values.All(c => c.Card.Id != "TIME_014"), "Promoted private offer definition leaked");
                foreach (var player in new[] { copy.Player1, copy.Player2 })
                    DriverTests.Check(player.SetasideZone.All(c => ReferenceEquals(c, copy.IdEntityDic[c.Id])),
                        "Setaside alias can overwrite a later Discover actor during clone");
            }
            DriverTests.Check(before.SequenceEqual(game.SaveSnapshot()), "Promoted offer projection changed authority");
        }
    }
    private static void RetiredPublicBranches()
    {
        foreach (int seat in new[] { 1, 2 })
        {
            var game = DriverTests.Fixture(seat);
            var parent = Entity.FromCard(game.CurrentPlayer, Cards.FromId("NEW1_008"), zone: game.CurrentPlayer.GraveyardZone);
            var before = game.SaveSnapshot();
            var view = View(game, seat);
            var cloned = FairViewTests.Copy(view.WithArtificialSeed(1));
            DriverTests.Check(cloned.IdEntityDic[parent.Id].ChooseOnePlayables.All(c => c is Unknown
                && ReferenceEquals(c, cloned.IdEntityDic[c.Id])), "Retired public branches must remain anonymous and cloneable");
            DriverTests.Check(before.SequenceEqual(game.SaveSnapshot()), "Retired branch projection changed authority");
        }
    }
    private static void EventQueuedChoice()
    {
        foreach (int seat in new[] { 1, 2 })
        {
            var game = DriverTests.Fixture(seat); var bot = game.CurrentPlayer;
            DriverTests.Check(game.Process(PlayCardTask.Any(bot, Generic.DrawCard(bot, Cards.FromId("TLC_EVENT_400")))), "Storm quest fixture");
            for (int i = 0; i < 3; i++)
            {
                bot.UsedMana = 0;
                DriverTests.Check(game.Process(PlayCardTask.Any(bot, Generic.DrawCard(bot, Cards.FromId("CS2_172")))), "Storm beast fixture");
            }
            DriverTests.Check(bot.Choice?.NextChoice != null, "Storm fixture needs queued offers");
            var before = game.SaveSnapshot();
            var view = View(game, seat);
            var copy = FairViewTests.Copy(view);
            DriverTests.Check(copy.CurrentPlayer.Choice.NextChoice.Choices.All(id => copy.IdEntityDic[id] is Unknown),
                "Queued EVENT offers must retain anonymous slots");
            var next = view.Apply(view.Actions.First(a => a.Type == PlayerTaskType.CHOOSE));
            DriverTests.Check(!next.CanExpand, "EVENT search must stop before unseen queued offers");
            DriverTests.Check(before.SequenceEqual(game.SaveSnapshot()), "Queued EVENT projection changed authority");
        }
    }
    private static void EventTimelines()
    {
        foreach (int seat in new[] { 1, 2 })
        foreach (bool returning in new[] { false, true })
        {
            var game = DriverTests.Fixture(seat);
            var enemy = game.CurrentPlayer.Opponent;
            foreach (var card in enemy.HandZone.ToArray()) enemy.SetasideZone.Add(enemy.HandZone.Remove(card));
            var fuel = Generic.DrawCard(enemy, Cards.FromId("NEW1_008"));
            Generic.AddEnchantmentBlock(game, Cards.FromId("EX1_004e"), enemy.Hero, fuel);
            var source = Entity.FromCard(enemy, Cards.FromId(returning ? "TIME_EVENT_998" : "CATA_EVENT_001"), zone: enemy.BoardZone);
            if (returning) HandTimelineRules.SendMinions(source);
            else HandTimelineRules.Ignite(source, fuel);
            game.DeathProcessingAndAuraUpdate();
            var before = game.SaveSnapshot();
            var view = View(game, seat);
            foreach (var projected in new[] { FairViewTests.Copy(view), FairViewTests.Copy(view.WithArtificialSeed(1)) })
            {
                var owner = projected.CurrentPlayer.Opponent;
                var hidden = returning ? owner.SetasideZone.Single(PayloadRules.IsStored) : owner.HandZone.Single();
                DriverTests.Check(hidden is Unknown && hidden.ChooseOnePlayables == null
                    && hidden.AppliedEnchantments?.Any(a => a.Card.Id == "EX1_004e") != true,
                    "EVENT timeline exposes hidden definition, branches or buffs");
                if (returning)
                    DriverTests.Check(hidden[PayloadRules.Owner] == owner.AppliedEnchantments.Single(a => a.Card.Id == "TIME_EVENT_998e1").Id,
                        "Runi anonymous return lost its public schedule");
                else
                    DriverTests.Check(!HandTimelineRules.IsBurning(hidden)
                        && owner.AppliedEnchantments.Single(a => a.Card.Id == "CATA_EVENT_001e").Target == owner,
                        "Phoenix schedule must not identify a hidden hand slot");
            }
            DriverTests.Check(before.SequenceEqual(game.SaveSnapshot()), "EVENT projection changed authoritative state");
        }
    }
    private static void HiddenChildTopology()
    {
        foreach (int seat in new[] { 1, 2 })
        foreach (bool materialize in new[] { false, true })
        foreach (Zone zone in new[] { Zone.HAND, Zone.DECK, Zone.SETASIDE })
        {
            var game = DriverTests.Fixture(seat);
            var twin = game.CloneExact(history: true);
            twin.PowerHistory.Full.AddRange(game.PowerHistory.Full);
            foreach (var variant in new[] { game, twin })
            {
                var enemy = variant.CurrentPlayer.Opponent;
                Entity.FromCard(enemy, Cards.FromId(variant == game ? "EDR_525" : "CS2_182"),
                    zone: zone == Zone.HAND ? enemy.HandZone : zone == Zone.DECK ? enemy.DeckZone : enemy.SetasideZone);
                Entity.FromCard(enemy, Cards.FromId("CS2_182"), zone: enemy.HandZone);
            }
            var before = game.SaveSnapshot();
            var options = new FairOptions { TrackOwnDeck = materialize, MaterializeOwnDeckTriggers = materialize };
            var first = View(game, seat, options); var second = View(twin, seat, options);
            DriverTests.Check(FairViewTests.Fingerprint(first) == FairViewTests.Fingerprint(second)
                && AiFingerprint.Public(first) == AiFingerprint.Public(second), "Hidden EDR_525 child count leaks through fair actors/IDs: " + zone);
            DriverTests.Check(AiFingerprint.Public(first.WithArtificialSeed(2)) == AiFingerprint.Public(second.WithArtificialSeed(2)),
                "Cloning restores private topology");
            DriverTests.Check(first.Facts.Opponent.Hand.Select(c => c.Position).SequenceEqual(Enumerable.Range(0, first.Facts.Opponent.Hand.Count)),
                "Canonical hand slots must preserve public positions for determinization");
            DriverTests.Check(FairViewTests.Copy(first).NextId == FairViewTests.Copy(second).NextId, "Hidden child count leaks through future allocation");
            DriverTests.Check(before.SequenceEqual(game.SaveSnapshot()), "Topology projection changed authority");
        }
    }
    private static void PublicOverdrawDeparture()
    {
        foreach (int seat in new[] { 1, 2 })
        {
            var game = DriverTests.Fixture(seat, "CS2_182", "CS2_182", "CS2_182", "CS2_182", "CS2_182", "CS2_029", "CS2_024");
            var bot = game.CurrentPlayer; var options = Options();
            while (!bot.HandZone.IsFull) Generic.DrawCard(bot, Cards.FromId("CS2_182"));
            View(game, seat, options);
            var burned = bot.DeckZone.Single(c => c.Card.Id == "CS2_029");
            Generic.Draw(bot, burned);
            DriverTests.Check(burned.Zone == bot.GraveyardZone, "Fixture must overdraw into graveyard");
            for (int seed = 1; seed <= 12; seed++)
            {
                var view = FairBotState.Create(game, seat, seed, DeckStyle.Tempo, options);
                DriverTests.Check(!view.Facts.OwnDeckKnowledge.ContainsKey("CS2_029") && view.Facts.OwnDeckKnowledge.GetValueOrDefault("CS2_024") == 1
                    && view.Facts.OwnDeckKnowledge.Values.Sum() == bot.DeckZone.Count, "Public burn must remove its revealed departure, never a random card");
            }
        }
    }
    private static void SameEntityDeckTransform()
    {
        foreach (int seat in new[] { 1, 2 })
        {
            var game = DriverTests.Fixture(seat, "CS2_182", "CS2_182", "CS2_182", "CS2_182", "CS2_182", "CS2_029");
            var bot = game.CurrentPlayer; var options = Options();
            View(game, seat, options);
            var envoy = Generic.DrawCard(bot, Cards.FromId("EDR_873"));
            int[] ids = bot.DeckZone.Select(c => c.Id).ToArray();
            DriverTests.Check(game.Process(PlayCardTask.Any(bot, envoy)), "Generated Envoy fixture play");
            DriverTests.Check(ids.SequenceEqual(bot.DeckZone.Select(c => c.Id)), "Fixture must transform without replacing deck IDs");
            var first = View(game, seat, options);
            DriverTests.Check(!first.Facts.OwnDeckKnowledge.ContainsKey("CS2_182"), "Envoy leaves obsolete neutral definitions in ledger");
            DriverTests.Check(first.Facts.OwnDeckKnowledge.GetValueOrDefault("CS2_029") == 1
                && first.Facts.OwnDeckKnowledge.Values.Sum() == bot.DeckZone.Count
                && first.Facts.OwnDeckKnowledge.Keys.All(id => id == "CS2_029" || game.OfflinePools.Standard.Any(c => c.CardId == id && c.Classes.Contains("DRUID"))),
                "Envoy must sample Druid replacements and preserve unaffected Mage card: " + string.Join(",", first.Facts.OwnDeckKnowledge) + " deck=" + bot.DeckZone.Count);
            var twin = game.CloneExact(history: true);
            twin.PowerHistory.Full.AddRange(game.PowerHistory.Full);
            foreach (var card in twin.CurrentPlayer.DeckZone) card.Card = Cards.FromId("CS2_024");
            DriverTests.Check(FairViewTests.Fingerprint(first) == FairViewTests.Fingerprint(View(twin, seat)), "Envoy sampling reads hidden transformed definitions");
            var drawn = bot.DeckZone.First(c => c.Card.Id != "CS2_029");
            Generic.Draw(bot, drawn);
            first = View(game, seat, options);
            DriverTests.Check(first.Facts.OwnDeckKnowledge.GetValueOrDefault("CS2_029") == 1
                && first.Facts.OwnDeckKnowledge.Values.Sum() == bot.DeckZone.Count,
                "Observed transformed draw must consume a replacement slot, not the unaffected Mage card");
        }
    }
    private static void OffboardSetaside()
    {
        foreach (int seat in new[] { 1, 2 })
        foreach (bool irida in new[] { true, false })
        {
            var game = DriverTests.Fixture(seat, "CS2_182", "CS2_182", "CS2_182", "CS2_182", "CS2_182", "CS2_029", "CS2_024", "NEW1_008");
            var bot = game.CurrentPlayer;
            var known = Generic.DrawCard(bot, Cards.FromId("CS2_023"));
            View(game, seat);
            bot.HandZone.Remove(known); Generic.ShuffleIntoDeck(bot, bot.Hero, known);
            if (irida)
            {
                var card = Generic.DrawCard(bot, Cards.FromId("JAIL_719"));
                DriverTests.Check(game.Process(PlayCardTask.Any(bot, card)), "Irida fixture play");
            }
            else
            {
                Enchantment.GetInstance(bot, bot.Hero, bot, Cards.FromId("JAIL_509e"));
                while (!bot.HandZone.IsFull) Generic.DrawCard(bot, Cards.FromId("CS2_182"));
                foreach (var card in bot.DeckZone.Where(c => c.Card.Id != "CS2_182").ToArray()) Generic.Draw(bot, card);
            }
            var hidden = bot.SetasideZone.Where(PayloadRules.IsStored).ToArray();
            DriverTests.Check(hidden.Length > 1, "Fixture needs retained contents");
            var twin = game.CloneExact(history: true);
            var entries = hidden.Select(c => twin.IdEntityDic[c.Id]).ToArray();
            foreach (var card in entries)
            {
                card.Card = Cards.FromId(card.Card.Id == "CS2_029" ? "CS2_024" : "CS2_029");
                card[PayloadRules.Link] = entries.Last().Id;
                card[PayloadRules.Value] = 999;
                card.ZonePosition = 99;
                if (card.ChooseOnePlayables != null)
                    foreach (var branch in card.ChooseOnePlayables) branch.Card = Cards.FromId("CS2_029");
            }
            foreach (int viewer in new[] { seat, 3 - seat })
            {
                var before = game.SaveSnapshot();
                var first = View(game, viewer); var second = View(twin, viewer);
                DriverTests.Check(FairViewTests.Fingerprint(first) == FairViewTests.Fingerprint(second),
                    (irida ? "Irida" : "Godfrey") + " retained identity/order/links leak for viewer " + viewer);
                DriverTests.Check(before.SequenceEqual(game.SaveSnapshot()), "Offboard projection changed authority");
                var hiddenIds = new HashSet<int>(hidden.Select(c => c.Id));
                DriverTests.Check(!FairViewTests.Copy(first).HistoryFacts.Query.Events<ResidenceChangedFact>()
                    .Any(f => f.Sequence <= game.HistoryFacts.Query.Events<HistoryFact>().Last().Sequence && hiddenIds.Contains(f.EntityId)),
                    "History still links hidden offboard actors to their deck residences");
                if (viewer == seat)
                {
                    var copy = FairViewTests.Copy(first);
                    var owner = copy.CurrentPlayer;
                    var effect = owner.AppliedEnchantments.Single(e => e.Card.Id == (irida ? "JAIL_719e2" : "JAIL_509e"));
                    var samples = PayloadRules.Entries(effect);
                    DriverTests.Check(samples.Length == (irida ? hidden.Length / 2 : hidden.Length) && samples.All(c => !(c is Unknown)),
                        "Retained contents must remain sampled and deliverable");
                    DriverTests.Check(samples.All(c => c[PayloadRules.Link] == 0), "Retained sample links hidden originals");
                    if (irida) { copy.Process(EndTurnTask.Any(owner)); copy.Process(EndTurnTask.Any(copy.CurrentPlayer)); }
                    else owner.SetasideZone.Add(owner.HandZone.Remove(owner.HandZone[0]));
                    DriverTests.Check(PayloadRules.Entries(effect).Length == samples.Length - (irida ? 2 : 1)
                        && owner.HandZone.Count == (irida ? 3 : 10) && owner.HandZone.All(c => !(c is Unknown)),
                        "Retained replay must consume samples and deliver real cards");
                }
            }
        }
    }
    private static void StormfuryDeparture()
    {
        foreach (int seat in new[] { 1, 2 })
        foreach (bool generatedOffer in new[] { false, true })
        {
            var game = DriverTests.Fixture(seat, "CS2_182", "CS2_182", "CS2_182", "CS2_182", "CS2_182", generatedOffer ? "CS2_182" : "JAIL_805", "CS2_029");
            var bot = game.CurrentPlayer; var options = Options();
            var storm = generatedOffer ? Entity.FromCard(bot, Cards.FromId("JAIL_805"), zone: bot.SetasideZone)
                : bot.DeckZone.Single(c => c.Card.Id == "JAIL_805");
            if (generatedOffer)
            {
                bot.Choice = new Choice(bot) { ChoiceType = ChoiceType.GENERAL, ChoiceAction = ChoiceAction.HAND,
                    SourceId = bot.Hero.Id, Choices = new List<int> { storm.Id } };
                View(game, seat, options);
                bot.Choice = null;
                bot.SetasideZone.Remove(storm); Generic.ShuffleIntoDeck(bot, bot.Hero, storm);
            }
            Generic.Draw(bot, storm);
            for (int n = 0; n < 3; n++)
            {
                var coin = Generic.DrawCard(bot, Cards.FromId("GAME_005"));
                DriverTests.Check(game.Process(PlayCardTask.Any(bot, coin)), "Stormfury upgrade fixture");
            }
            DriverTests.Check(game.IdEntityDic[storm.Id].Card.Id == "JAIL_805t", "Stormfury did not upgrade");
            var state = View(game, seat, options);
            DriverTests.Check(!state.Facts.OwnDeckKnowledge.ContainsKey("JAIL_805") && !state.Facts.OwnDeckKnowledge.ContainsKey("JAIL_805t")
                && state.Facts.OwnDeckKnowledge["CS2_029"] == 1
                && state.Facts.OwnDeckKnowledge.Values.Sum() == bot.DeckZone.Count, "Upgraded form rewrote earlier deck departure");
            storm = game.IdEntityDic[storm.Id]; bot.HandZone.Remove(storm); Generic.ShuffleIntoDeck(bot, bot.Hero, storm);
            state = View(game, seat, options);
            DriverTests.Check(state.Facts.OwnDeckKnowledge.GetValueOrDefault("JAIL_805t") == 1
                && !state.Facts.OwnDeckKnowledge.ContainsKey("JAIL_805"), "Upgraded shuffle must use addition-time form");
            Generic.Draw(bot, storm);
            DriverTests.Check(!View(game, seat, options).Facts.OwnDeckKnowledge.ContainsKey("JAIL_805t"), "Second departure lost its event-time form");
        }
    }
    private static void NabShuffle()
    {
        foreach (int seat in new[] { 1, 2 })
        {
            var game = DriverTests.Fixture(seat); var bot = game.CurrentPlayer;
            var target = (Minion)Entity.FromCard(bot.Opponent, Cards.FromId("CS2_120"), zone: bot.Opponent.BoardZone);
            target.Damage = target.Health - 1;
            var nab = Generic.DrawCard(bot, Cards.FromId("JAIL_225"));
            DriverTests.Check(game.Process(PlayCardTask.Any(bot, nab, target)), "Nab fixture play");
            DriverTests.Check(View(game, seat).Facts.OwnDeckKnowledge.GetValueOrDefault("CS2_120") == 1,
                "Publicly revealed Nab shuffle absent from ledger");
            bot.DeckZone.AddGenerated(Cards.FromId("CS2_029"), bot.Opponent.Hero);
            var hidden = bot.DeckZone.Single(c => c.Card.Id == "CS2_029");
            var packet = SabberStoneCore.Kettle.PowerHistoryBuilder.ShowEntity(hidden);
            packet.Entity.Tags[Generic.PrivateDecisionOfferTag] = bot.Opponent.PlayerId;
            game.PowerHistory.Full.Add(packet);
            DriverTests.Check(!View(game, seat).Facts.OwnDeckKnowledge.ContainsKey("CS2_029"), "Private deck packet entered ledger");
        }
    }
}
