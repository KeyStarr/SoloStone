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
using RealEngineGame = SabberStoneCore.Model.Game;
using RealEngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    private static void DiscoverRealNative()
    {
        var minimumIdWins = new int[2, 2];
        foreach (bool opponent in new[] { false, true })
        foreach (string cardId in new[] { "JAIL_734", "CAP_403", "JAIL_303", "JAIL_851", "TIME_041", "TIME_701", "BE_555", "CATA_591" })
        foreach (int guessMode in cardId == "TIME_041" ? new[] { 0, 1, 2 } : new[] { 0 })
        foreach (int seed in cardId == "TIME_041" ? Enumerable.Range(1, 20) : new[] { 42 })
        {
            var game = new RealEngineGame(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                History = true, Logging = false, FormatType = FormatType.FT_STANDARD,
                Player1HeroClass = CardClass.HUNTER, Player2HeroClass = CardClass.HUNTER,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame();
            if (opponent) Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "private fixture turn");
            var owner = game.CurrentPlayer;
            foreach (var p in new[] { owner, owner.Opponent })
                foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
            owner.BaseMana = 10;
            var enemyHand = Enumerable.Range(0, guessMode == 2 ? 0 : 5).Select(i => Generic.DrawCard(owner.Opponent, Cards.FromId(i % 2 == 0 ? "CS2_029" : "CS2_200"))).ToArray();
            foreach (var p in new[] { owner, owner.Opponent })
                foreach (string id in new[] { "CS2_029", "CS2_200", "CS2_172" }) RealEngineEntity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
            if (guessMode == 1)
                foreach (var card in owner.Opponent.DeckZone.ToArray()) owner.Opponent.SetasideZone.Add(owner.Opponent.DeckZone.Remove(card));
            var capture = new Capture(); var match = new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            void Flush() => typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(match, new object[] { game.PowerHistory.Last });
            Flush(); game.PowerHistory.Last.Clear();
            var source = Generic.DrawCard(owner, Cards.FromId(cardId));
            Check(game.Process(PlayCardTask.Any(owner, source)), "real selection native play " + cardId);
            if (cardId == "CATA_591")
            {
                Check(game.Process(EndTurnTask.Any(owner)) && game.Process(EndTurnTask.Any(owner.Opponent)), "replacement reaches next normal draw");
            }
            Flush(); game.PowerHistory.Last.Clear();
            var offers = owner.Choice.Choices.ToArray();
            Check(offers.Length > 0 && offers.Length <= (cardId == "JAIL_851" ? 4 : 3), "native choice actor count");
            if (!opponent)
            {
                typeof(NativeMatch).GetMethod("SendHumanChoice", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
                var packet = capture.Last<EntityChoices>(17);
                Check(packet.ChoiceType == 2 && packet.CountMin == 1 && packet.CountMax == 1
                    && packet.Entities.SequenceEqual(offers), "retail-shaped mandatory GENERAL choice");
            }
            else
            {
                // Retail shows the other player's prompt as its card backs: one GENERAL packet of nameless actors.
                var shown = capture.Packets.Select(p => p.Message).OfType<EntityChoices>().ToList();
                var named = capture.Packets.Select(p => p.Message).OfType<PowerHistory>().SelectMany(p => p.List)
                    .SelectMany(row => new[] { row.FullEntity, row.ShowEntity, row.ChangeEntity }).Where(e => e != null && e.Name != "")
                    .Select(e => e.Entity).ToHashSet();
                Check(shown.Count == 1 && shown[0].PlayerId == owner.PlayerId && shown[0].ChoiceType == 2
                    && shown[0].Entities.Count == offers.Length && shown[0].Entities.All(id => !named.Contains(id)),
                    "other player's prompt shows only hidden choice actors");
            }
            var rows = capture.Packets.Select(p => p.Message).OfType<PowerHistory>().SelectMany(p => p.List).ToArray();
            var snapshots = rows.SelectMany(row => new[] { row.FullEntity, row.ShowEntity, row.ChangeEntity }).Where(e => e != null).ToArray();
            foreach (int id in offers)
                Check(snapshots.Where(e => e.Entity == id).Any()
                    && snapshots.Where(e => e.Entity == id).All(e => opponent ? string.IsNullOrEmpty(e.Name) : e.Name == game.IdEntityDic[id].Card.Id),
                    "offer identity visible only to chooser");
            if (!opponent)
                Check(snapshots.Where(e => enemyHand.Any(c => c.Id == e.Entity)).All(e => string.IsNullOrEmpty(e.Name)),
                    "private proxy never reveals original enemy-hand actor");
            foreach (var row in rows)
            {
                Check(!row.HasTagChange || !DiscoverRealRules.IsInternalTag((GameTag)row.TagChange.Tag), "no private binding/budget tag changes on wire");
                foreach (var e in new[] { row.FullEntity, row.ShowEntity, row.ChangeEntity }.Where(e => e != null))
                    Check(e.Tags.All(t => !DiscoverRealRules.IsInternalTag((GameTag)t.Name)), "no private binding/budget tags in snapshots");
            }
            int bound = game.IdEntityDic[offers[0]][DiscoverRealRules.BoundEntity];
            if (cardId == "TIME_041")
            {
                var guessView = FairBotState.Create(game, owner.PlayerId, 456, DeckStyle.Control);
                var projected = (RealEngineGame)typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(guessView);
                Check(offers.All(id => projected.IdEntityDic[id][DiscoverRealRules.CorrectAnswer] == 0
                    && projected.IdEntityDic[id][DiscoverRealRules.BoundEntity] == 0)
                    && projected.CurrentPlayer.Opponent.HandZone.All(c => c is Unknown), "fair guess view hides correct answer and original hand identities from chooser");
            }
            int selected = cardId == "TIME_041" ? offers.Min() : offers[0];
            var decision = game.GetPendingDecisions().Single();
            Check(game.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { selected }), out var error), "native engine answer: " + error);
            Flush();
            if (cardId == "TIME_041" && ((Minion)source).Health == 8)
            {
                Check(guessMode != 2, "empty-hand Forefather cannot reward a guess");
                minimumIdWins[opponent ? 1 : 0, guessMode]++;
            }
            var allOffers = offers.ToList();
            game.PowerHistory.Last.Clear();
            while (cardId == "BE_555" && owner.Choice != null)
            {
                var nextOffers = owner.Choice.Choices.ToArray(); allOffers.AddRange(nextOffers);
                if (!opponent)
                {
                    typeof(NativeMatch).GetMethod("SendHumanChoice", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
                    Check(capture.Last<EntityChoices>(17).Entities.SequenceEqual(nextOffers), "repeated Discover prompt uses current private actors");
                }
                game.IdEntityDic[nextOffers[0]].Cost = 8;
                decision = game.GetPendingDecisions().Single();
                Check(game.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { nextOffers[0] }), out error), "repeated native answer: " + error);
                Flush(); game.PowerHistory.Last.Clear();
            }
            Check(!opponent || OpposingPromptsHidden(capture, owner.PlayerId), "resolved opponent prompts show only card backs");
            // Inspect every emitted snapshot after resolution too, including retired offers and moved originals.
            rows = capture.Packets.Select(p => p.Message).OfType<PowerHistory>().SelectMany(p => p.List).ToArray();
            snapshots = rows.SelectMany(row => new[] { row.FullEntity, row.ShowEntity, row.ChangeEntity }).Where(e => e != null).ToArray();
            foreach (int offerId in allOffers)
                Check(snapshots.Where(e => e.Entity == offerId).All(e => opponent ? string.IsNullOrEmpty(e.Name) : e.Name == game.IdEntityDic[offerId].Card.Id),
                    "resolved/retired offer stays private to chooser " + cardId);
            bool HiddenTag(int tag) => tag == (int)GameTag.ENTITY_ID || tag == (int)GameTag.CONTROLLER
                || tag == (int)GameTag.ZONE || tag == (int)GameTag.ZONE_POSITION;
            var hiddenIds = game.IdEntityDic.Values.Where(e => e.Zone == e.Controller.DeckZone
                || e.Controller.PlayerId == 2 && e.Zone == e.Controller.HandZone).Select(e => e.Id)
                .Concat(opponent ? allOffers : Enumerable.Empty<int>()).ToHashSet();
            foreach (var snapshot in snapshots.Where(e => hiddenIds.Contains(e.Entity)))
                Check(string.IsNullOrEmpty(snapshot.Name) && snapshot.Tags.All(t => HiddenTag(t.Name)),
                    "offer/guess/move hides original identity and definition tags " + cardId);
            Check(rows.Where(r => r.HasTagChange && hiddenIds.Contains(r.TagChange.Entity)).All(r => HiddenTag(r.TagChange.Tag)),
                "offer/guess/move hides definition tag changes " + cardId);
            foreach (var row in rows)
            {
                Check(!row.HasTagChange || !DiscoverRealRules.IsInternalTag((GameTag)row.TagChange.Tag), "resolved state tags stay off wire");
                foreach (var e in new[] { row.FullEntity, row.ShowEntity, row.ChangeEntity }.Where(e => e != null))
                    Check(e.Tags.All(t => !DiscoverRealRules.IsInternalTag((GameTag)t.Name)), "resolved snapshots hide private state");
            }
            if (cardId is "JAIL_734" or "TIME_701" or "CATA_591")
                Check(game.IdEntityDic[bound].Zone == owner.HandZone && game.IdEntityDic[offers[0]].Zone == owner.SetasideZone,
                    "actual entity delivered, private proxy stays outside hand");
            if (opponent)
            {
                var fair = FairBotState.Create(game, 1, 123, DeckStyle.Control);
                var projected = (RealEngineGame)typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fair);
                FairViewHiddenOfferProof.AssertHiddenActors(game, projected, owner.PlayerId, offers);
                Check(projected.IdEntityDic.Values.Where(e => e.Controller.PlayerId == 2)
                    .All(e => e.NativeTags.Keys.All(tag => !DiscoverRealRules.IsInternalTag(tag))), "fair opponent view redacts saved linkage/budget/watch");
            }
            capture.SerializeAll();
        }
        Check(minimumIdWins.Cast<int>().All(wins => wins > 0 && wins < 20),
            "Forefather deck/fallback minimum wire entity ID must not identify correct answer across 20 seeds: " + string.Join(",", minimumIdWins.Cast<int>()));
        Console.WriteLine("PASS Forefather minimum-ID guesses per 20 seeds (human/bot, deck/fallback): " + string.Join(",", minimumIdWins.Cast<int>()));
        Console.WriteLine("PASS real entity native projection: both choosers, 8 cards, private identities/state, bound movement");
    }
}
