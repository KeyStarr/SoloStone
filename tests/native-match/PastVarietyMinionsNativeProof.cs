using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using EngineGame = SabberStoneCore.Model.Game;

internal static partial class Program
{
    [GroupProof("native-pastvar-minions", ProofKind.Native)]
    private static void PastVarietyMinionsPrivacy()
    {
        foreach (int seat in new[] { 1, 2 })
        foreach (string id in new[] { "BRM_031", "GVG_082", "DAL_185", "DRG_054" })
        {
            var game = new EngineGame(new GameConfig { RandomSeed = 17, StartPlayer = 1,
                Shuffle = false, SkipMulligan = true, History = true, Logging = false,
                FormatType = FormatType.FT_WILD,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame(); if (seat == 2) Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "minion privacy seat two");
            var player = game.CurrentPlayer;
            foreach (var owner in new[] { player, player.Opponent })
                foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
            var capture = new Capture(); var match = new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            FlushHistory(match); game.PowerHistory.Last.Clear();
            var source = Generic.DrawCard(player, Cards.FromId(id)); player.BaseMana = 10; player.UsedMana = 0;
            Check(game.Process(PlayCardTask.Any(player, source)), "privacy generated legacy play " + id);
            if (id == "BRM_031") new DrawTask().Process(game, player, source, null);
            if (id == "GVG_082") { ((Minion)source).ToBeDestroyed = true; game.DeathProcessingAndAuraUpdate(); }
            if (id == "DAL_185")
            {
                player.Hero.Damage = 9; var drawn = player.DeckZone.AddGenerated(Cards.FromId(id), player.Hero);
                Generic.Draw(player, drawn); Check(drawn.Zone == player.HandZone && player.Hero.Damage == 5, "Broodmother native hand residence and heal");
            }
            typeof(EngineGame).GetMethod("ProcessTasks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(game, null);
            FlushHistory(match);
            Check(player.HandZone.Count == (id == "BRM_031" ? 2 : 1), id + " hidden card effect realized");
            var fair = FairBotState.Create(game, player.Opponent.PlayerId, 39, DeckStyle.Value);
            FairViewHiddenOfferProof.AssertHiddenActors(game, fair.FingerprintGame, seat,
                player.HandZone.Select(c => c.Id).ToArray());
            Check(fair.Facts.Opponent.Hand.All(c => c.Unknown && string.IsNullOrEmpty(c.CardId)), id + " opponent fair facts hide identity");
            var simulated = fair.FingerprintGame.CloneExact(history: false);
            Check((seat == 1 ? simulated.Player1 : simulated.Player2).HandZone.All(c => c is Unknown), id + " bot simulation keeps hidden cards anonymous");
            var rows = NativeHistory(capture).ToArray();
            foreach (var card in player.HandZone)
            {
                var shown = rows.SelectMany(r => new[] { r.FullEntity, r.ShowEntity, r.ChangeEntity })
                    .Where(e => e != null && e.Entity == card.Id).ToArray();
                Check(seat == 1 ? shown.Any(e => e.Name == card.Card.Id)
                    : shown.All(e => string.IsNullOrEmpty(e.Name) && HasOnlyHiddenTags(e.Tags)), id + " native owner/opponent identity boundary");
            }
            capture.SerializeAll();
            Check(game.Process(ConcedeTask.Any(player)), "privacy scenario Concede");
        }
        Console.WriteLine("PASS Past minions: generated hidden hand identities; both seats; fair facts, bot simulation and native projection");
    }
}
