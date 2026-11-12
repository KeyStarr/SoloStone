using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    // ChoiceCardMgr leaves the chosen GENERAL card on screen until a zone change moves it (HideChosen=false).
    // Live 2026-10-25: Ancient Raptor's Flaming Claws and Xavius's deck proxy stayed as big cards all match.
    private static void ChoiceActorRetirement()
    {
        // Adapt-style fixed options stay SETASIDE in the engine: the chosen card must leave the client.
        {
            var (capture, match) = ChoiceRetirementMatch(CardClass.ROGUE);
            var game = match.Game; var player = game.Player1;
            var raptor = Generic.DrawCard(player, Cards.FromId("TLC_245"));
            FlushFieldHistory(match);
            var choice = ChoiceRetirementPlay(match, capture, raptor, 1);
            Check(choice.Entities.Count == 3 && choice.Source == raptor.Id && !choice.HideChosen, "Raptor native GENERAL prompt");
            int answerStart = capture.Packets.Count;
            int claws = choice.Entities[0];
            Check(game.IdEntityDic[claws].Card.Id == "UNG_999t3", "first Raptor option is Flaming Claws");
            match.SendChoices(choice.Id, new List<int> { claws });
            Check(((Minion)raptor).AttackDamage == 5, "Flaming Claws grants +3 Attack");
            Check(game.IdEntityDic[claws].Zone?.Type == EngineZone.SETASIDE, "engine keeps the spent option SETASIDE");
            var answer = DeckDiscoverWireHistory(capture, answerStart);
            int chosenAck = capture.Packets.FindIndex(answerStart, p => p.Id == 13);
            int retire = capture.Packets.FindIndex(answerStart, p => p.Id == 19 && ((PowerHistory)p.Message).List.Any(e => IsZone(e, claws, EngineZone.REMOVEDFROMGAME)));
            Check(chosenAck >= 0 && retire > chosenAck, "chosen option retires after EntitiesChosen and its effect history");
            Check(answer.Count(e => IsZone(e, claws, EngineZone.REMOVEDFROMGAME)) == 1, "chosen option retired exactly once");
            Check(choice.Entities.Skip(1).All(id => !answer.Any(e => SnapshotEntity(e) == id || e.HasTagChange && e.TagChange.Entity == id)),
                "unchosen options keep ChoiceCardMgr's own hide; no extra wire");
            var after = DeckDiscoverWireState(DeckDiscoverWireHistory(capture, 0));
            Check(after[claws].Zone == (int)EngineZone.REMOVEDFROMGAME, "client graph ends with the spent option removed");
            // A later engine reference must recreate the destroyed client entity before using it.
            int reuseStart = capture.Packets.Count;
            player.HandZone.Add(player.SetasideZone.Remove((IPlayable)game.IdEntityDic[claws]));
            FlushFieldHistory(match);
            var reuse = DeckDiscoverWireHistory(capture, reuseStart);
            int create = reuse.FindIndex(e => e.HasFullEntity && e.FullEntity.Entity == claws
                && SnapshotTag(e, GameTag.ZONE) == (int)EngineZone.SETASIDE);
            int hand = reuse.FindIndex(e => IsZone(e, claws, EngineZone.HAND));
            Check(create >= 0 && hand > create, "reused retired option is recreated SETASIDE before entering HAND");
            capture.SerializeAll();
            Console.WriteLine("CHECK choice retirement: Raptor Flaming Claws retired, reuse recreated");
        }
        // Ordinary Discover moves the chosen card to HAND itself: no retirement (retail fly-to-hand stays intact).
        {
            var (capture, match) = ChoiceRetirementMatch(CardClass.ROGUE);
            var game = match.Game; var player = game.Player1;
            var spell = Generic.DrawCard(player, Cards.FromId("EDR_528"));
            FlushFieldHistory(match);
            var choice = ChoiceRetirementPlay(match, capture, spell, 0);
            var chosen = new List<int>();
            int answerStart = capture.Packets.Count;
            while (game.Player1.Choice != null)
            {
                choice = capture.Last<EntityChoices>(17);
                chosen.Add(choice.Entities[0]);
                match.SendChoices(choice.Id, new List<int> { choice.Entities[0] });
            }
            var answer = DeckDiscoverWireHistory(capture, answerStart);
            Check(chosen.Count > 0 && chosen.All(id => player.HandZone.Any(c => c.Id == id)), "Discover delivers the chosen card to HAND");
            Check(!answer.Any(e => e.HasTagChange && e.TagChange.Tag == (int)GameTag.ZONE && e.TagChange.Value == (int)EngineZone.REMOVEDFROMGAME
                && chosen.Contains(e.TagChange.Entity)), "hand-bound Discover choice is never retired");
            capture.SerializeAll();
            Console.WriteLine($"CHECK choice retirement: EDR_528 Discover chosen={string.Join(",", chosen)} kept for hand flight");
        }
    }

    private static (Capture, NativeMatch) ChoiceRetirementMatch(CardClass hero)
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, Cards.HeroCard(hero), Enumerable.Repeat(Cards.FromId("CS2_182"), 30), 1337);
        match.Start();
        match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
        var player = match.Game.Player1;
        player.BaseMana = 10; player.UsedMana = 0;
        foreach (var old in player.HandZone.ToArray()) player.SetasideZone.Add(old.Zone.Remove(old));
        FlushFieldHistory(match);
        return (capture, match);
    }

    private static EntityChoices ChoiceRetirementPlay(NativeMatch match, Capture capture, IPlayable source, int position)
    {
        typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
        var options = capture.Last<AllOptions>(14);
        int play = options.Options.FindIndex(o => o.MainOption?.Id == match.WireReturnId(source.Id) && o.MainOption.PowerKeyword == 0);
        Check(play >= 0, source.Card.Id + " native play option");
        int before = capture.Packets.Count;
        match.SendOption(options.Id, play, 0, -1, position);
        Check(match.Game.Player1.Choice != null && capture.Packets.Skip(before).Any(p => p.Id == 17), source.Card.Id + " opens a native choice");
        return capture.Last<EntityChoices>(17);
    }
}
