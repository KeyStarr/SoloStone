using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    // Deckbuilding cards add starting deck cards before the opening draw: Azalina Soulsever 20 copies of the opponent's cards,
    // Beatrix (Pure Paladin NPC) 10 copies of her sideboard pick. Azalina's copies were created outside any zone, so a copy went
    // out as a FULL_ENTITY with its card id, and every such card was created inside the opening TRIGGER block, after the game.
    // The client placed it at the deck zone's transform, so a copy dealt into the opening hand showed face up half off the right
    // edge of the screen during the VS intro (Steam Deck playtest 2026-11-15). Every such card must look like an original deck
    // card: an anonymous DECK FULL_ENTITY before the opening block, revealed only by the SHOW_ENTITY that deals it into a hand.
    [GroupProof("azalina-opening-deck", ProofKind.Native)]
    private static void AzalinaOpeningDeckHistory()
    {
        var own = new[] { "JAIL_430" }.Concat(Enumerable.Repeat("CS2_231", 19)).Select(Cards.FromId).ToList();
        var bot = LocalDecks.Get("npc-pure-paladin");
        Check(bot != null && bot.CardIds.Contains(DeckbuildingRules.Beatrix), "fixture opponent must be the Beatrix Pure Paladin deck");
        int handCopies = 0, deckCopies = 0, beatrixCopies = 0;
        foreach (int seat in new[] { 1, 2 })
        for (int seed = 1; seed <= 6; seed++)
        {
            var capture = new Capture();
            // The fixture constructor takes exactly 30 cards; an Azalina deck has 20, set like SogStart does.
            var match = new NativeMatch(capture.Send, Cards.FromId("HERO_09"), Enumerable.Repeat(Cards.FromId("CS2_231"), 30), seed)
                { StartingPlayer = seat };
            typeof(NativeMatch).GetField("playerCards", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(match, own);
            typeof(NativeMatch).GetField("opponentDeck", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(match, bot);
            match.Start();
            var game = match.Game;
            string run = " (seed " + seed + ", seat " + seat + ")";
            // Everything sent up to and including the human mulligan choice; the first packet holds the game and the opening block.
            var opening = ((PowerHistory)capture.Packets.First(p => p.Id == 19).Message).List;
            int block = opening.FindIndex(r => r.HasPowerStart);
            Check(block > 0 && opening.Take(block).Any(r => r.HasCreateGame), "first packet must open with the game, then a block" + run);
            var rows = capture.Packets.Where(p => p.Id == 19).SelectMany(p => ((PowerHistory)p.Message).List).ToList();
            var late = opening.Skip(block).Where(r => r.HasFullEntity && TagValue(r.FullEntity.Tags, GameTag.ZONE) == (int)EngineZone.DECK)
                .Select(r => r.FullEntity.Entity).ToArray();
            Check(late.Length == 0, "starting deck cards created inside the opening block: " + string.Join(",", late) + run);

            int azalina = game.IdEntityDic.Values.Single(e => e.Controller == game.Player1 && e.Card.Id == "JAIL_430").Id;
            var copies = game.IdEntityDic.Values.Where(e => e.Controller == game.Player1 && e.Id != azalina
                && e[GameTag.CREATOR] == azalina).ToArray();
            Check(copies.Length == 20, "Azalina must add 20 copies, got " + copies.Length + run);
            foreach (var copy in copies)
            {
                string where = copy.Card.Id + "#" + copy.Id + run;
                var fulls = rows.Where(r => r.HasFullEntity && r.FullEntity.Entity == copy.Id).ToArray();
                Check(fulls.Length == 1 && opening.IndexOf(fulls[0]) is int index && index >= 0 && index < block,
                    "copy must be created once, before the opening block: " + where);
                Check(string.IsNullOrEmpty(fulls[0].FullEntity.Name), "copy FULL_ENTITY names its card: " + where);
                Check(TagValue(fulls[0].FullEntity.Tags, GameTag.ZONE) == (int)EngineZone.DECK,
                    "copy FULL_ENTITY is not created in DECK (zone " + TagValue(fulls[0].FullEntity.Tags, GameTag.ZONE) + "): " + where);
                Check(!rows.Any(r => r.HasTagChange && r.TagChange.Entity == copy.Id && r.TagChange.Tag == (int)GameTag.ZONE
                    && r.TagChange.Value == (int)EngineZone.DECK), "copy moves into DECK after creation: " + where);
                var shows = rows.Where(r => r.HasShowEntity && r.ShowEntity.Entity == copy.Id).ToArray();
                if (copy.Zone.Type == EngineZone.HAND)
                {
                    handCopies++;
                    Check(shows.Length == 1 && TagValue(shows[0].ShowEntity.Tags, GameTag.ZONE) == (int)EngineZone.HAND
                        && shows[0].ShowEntity.Name == copy.Card.Id, "opening-hand copy must be revealed once, into HAND: " + where);
                }
                else
                {
                    deckCopies++;
                    Check(copy.Zone.Type == EngineZone.DECK && shows.Length == 0, "deck copy revealed in the opening: " + where);
                }
            }
            int beatrix = game.IdEntityDic.Values.Single(e => e.Controller == game.Player2 && e.Card.Id == DeckbuildingRules.Beatrix).Id;
            var sideboard = game.IdEntityDic.Values.Where(e => e.Controller == game.Player2 && e[GameTag.CREATOR] == beatrix
                && (e.Zone.Type == EngineZone.DECK || e.Zone.Type == EngineZone.HAND)).ToArray();
            Check(sideboard.Length == 10, "Beatrix must add 10 copies, got " + sideboard.Length + run);
            foreach (var copy in sideboard)
            {
                beatrixCopies++;
                Check(!rows.Any(r => r.HasFullEntity && r.FullEntity.Entity == copy.Id && !string.IsNullOrEmpty(r.FullEntity.Name))
                    && !rows.Any(r => r.HasShowEntity && r.ShowEntity.Entity == copy.Id && !string.IsNullOrEmpty(r.ShowEntity.Name)),
                    "NPC Beatrix copy identity sent to the human: " + copy.Card.Id + "#" + copy.Id + run);
            }
        }
        Check(handCopies > 0 && deckCopies > 0, "fixture must deal copies into the opening hand and keep others in the deck");
        int npcCopies = AzalinaOpeningNpcCopies();
        Console.WriteLine("PASS azalina opening deck: " + handCopies + " opening-hand copies revealed once into HAND, "
            + deckCopies + " deck copies, " + beatrixCopies + " NPC Beatrix copies and " + npcCopies
            + " NPC Azalina copies anonymous, all created with the game");
    }

    // The NPC plays Azalina: its copies of the human's cards stay anonymous for the human, in its deck and in its opening hand.
    private static int AzalinaOpeningNpcCopies()
    {
        var npcDeck = new LocalDeck("Azalina NPC fixture", "HERO_09", new[] { "JAIL_430" }.Concat(Enumerable.Repeat("CS2_120", 19)).ToArray());
        int total = 0, inHand = 0;
        foreach (int seat in new[] { 1, 2 })
        for (int seed = 1; seed <= 3; seed++)
        {
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, Cards.FromId("HERO_01"), Enumerable.Repeat(Cards.FromId("CS2_182"), 30), seed)
                { StartingPlayer = seat };
            typeof(NativeMatch).GetField("opponentDeck", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(match, npcDeck);
            match.Start();
            var game = match.Game;
            string run = " (NPC Azalina, seed " + seed + ", seat " + seat + ")";
            var opening = ((PowerHistory)capture.Packets.First(p => p.Id == 19).Message).List;
            int block = opening.FindIndex(r => r.HasPowerStart);
            var rows = capture.Packets.Where(p => p.Id == 19).SelectMany(p => ((PowerHistory)p.Message).List).ToList();
            int azalina = game.IdEntityDic.Values.Single(e => e.Controller == game.Player2 && e.Card.Id == "JAIL_430").Id;
            var copies = game.IdEntityDic.Values.Where(e => e.Controller == game.Player2 && e.Id != azalina
                && e[GameTag.CREATOR] == azalina).ToArray();
            Check(copies.Length == 20, "NPC Azalina must add 20 copies, got " + copies.Length + run);
            foreach (var copy in copies)
            {
                total++;
                if (copy.Zone.Type == EngineZone.HAND) inHand++;
                string where = copy.Card.Id + "#" + copy.Id + run;
                var full = rows.Single(r => r.HasFullEntity && r.FullEntity.Entity == copy.Id);
                Check(opening.IndexOf(full) is int index && index >= 0 && index < block && string.IsNullOrEmpty(full.FullEntity.Name)
                    && TagValue(full.FullEntity.Tags, GameTag.ZONE) == (int)EngineZone.DECK,
                    "NPC copy must be an anonymous DECK FULL before the opening block: " + where);
                Check(!rows.Any(r => r.HasShowEntity && r.ShowEntity.Entity == copy.Id && !string.IsNullOrEmpty(r.ShowEntity.Name)),
                    "NPC Azalina copy identity sent to the human: " + where);
            }
        }
        Check(inHand > 0, "NPC fixture must deal Azalina copies into the NPC's opening hand");
        return total;
    }
}
