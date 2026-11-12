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
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    [GroupProof("native-b16-autoplay", ProofKind.Native)]
    private static void B16AutoplayNative()
    {
        foreach (bool opponent in new[] { false, true })
        foreach (string id in new[] { "CATA_477", "TIME_436t2" })
        {
            var game = new SabberStoneCore.Model.Game(new GameConfig {
                RandomSeed = 17, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                History = true, Logging = false, FormatType = FormatType.FT_STANDARD,
                Player1HeroClass = CardClass.PRIEST, Player2HeroClass = CardClass.PRIEST,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame();
            if (opponent) Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "autoplay native opponent setup");
            var p = game.CurrentPlayer; p.BaseMana = 10;
            var capture = new Capture(); var match = new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            void Flush() { FlushHistory(match); game.PowerHistory.Last.Clear(); }
            Flush();
            var held = Generic.DrawCard(p, Cards.FromId("CS2_189"));
            var source = (BoardObject)Generic.DrawCard(p, Cards.FromId(id));
            Check(game.Process(PlayCardTask.Any(p, source)), "native location play");
            source[GameTag.DAMAGE] = source[GameTag.HEALTH] - 1;
            Check(game.Process(new ActivateLocationTask(p, source)), "native resumable activation");
            Check(p.Choice != null, "activation exposes actual decision");
            var offers = p.Choice.Choices.ToArray();
            Flush();
            var decision = game.GetPendingDecisions().Single();
            int selected = id == "CATA_477" ? held.Id : offers[0];
            Check(game.TryAnswer(new DecisionAnswer(decision.Id, p.Id, new[] { selected }), out var error), "native answer " + error);
            Flush();
            Check(source.Zone == p.GraveyardZone && p.Choice == null && !game.Resolution.HasSuspendedEffect, "final activation cannot stall");
            var hidden = game.IdEntityDic.Values.Where(e => e.Zone == e.Controller.DeckZone || PayloadRules.IsStored(e)
                || opponent && (e.Zone == p.HandZone || offers.Contains(e.Id) && e.Zone == p.SetasideZone)).Select(e => e.Id).ToHashSet();
            foreach (var row in NativeHistory(capture))
            {
                foreach (var entity in new[] { row.FullEntity, row.ShowEntity, row.ChangeEntity }.Where(e => e != null))
                {
                    Check(entity.Tags.All(t => !AutoPlayRules.IsInternalTag((GameTag)t.Name)), "autoplay internal tags filtered");
                    if (hidden.Contains(entity.Entity)) Check(string.IsNullOrEmpty(entity.Name) && HasOnlyHiddenTags(entity.Tags), "hidden hand/deck/offer identity stays private");
                }
                if (row.HasTagChange)
                {
                    Check(!AutoPlayRules.IsInternalTag((GameTag)row.TagChange.Tag), "private pending tag never on wire");
                    if (hidden.Contains(row.TagChange.Entity)) Check(IsPublicHiddenTag(row.TagChange.Tag), "hidden identity tag absent");
                }
            }
            capture.SerializeAll();
        }
        Console.WriteLine("PASS b16 autoplay native location decisions, completion and both-seat privacy");
    }
}
