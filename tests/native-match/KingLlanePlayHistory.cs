using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Actions;

internal static partial class Program
{
    // #79: King Llane's Battlecry shuffles him from play back into the deck. The client flies him there with a hand actor of his
    // premium and waits for its summon-in; a Diamond or Signature summon-in never finishes once the HIDE that follows turns him
    // into a HiddenCard, and the board stopped for both players (live: lane 1, Signature and Diamond; Golden and Normal fine).
    // Every seat, every premium, both the ordinary card and Garona's Start of Game transfer: when the client applies the move
    // into the deck it must hold no quality flag for him, so it builds a Golden or Normal flight.
    private static void KingLlanePlayHistory()
    {
        foreach (bool transferred in new[] { false, true })
        foreach (bool bot in new[] { false, true })
        foreach (int premium in new[] { 0, 1, 2, 3 })
        {
            string name = $"king-llane-{(transferred ? "transferred" : "ordinary")}-{(bot ? "opponent" : "owner")}-premium{premium}";
            Capture capture;
            NativeMatch match;
            if (transferred)
            {
                // Garona's owner is the other seat: Llane hides in the recipient's deck.
                (match, capture) = SogStart(bot ? new[] { "TIME_875" } : Array.Empty<string>(), bot ? Array.Empty<string>() : new[] { "TIME_875" });
                SogComplete(match, capture);
            }
            else
            {
                capture = new Capture();
                match = new NativeMatch(capture.Send, "mage", "mage", 8427);
                match.Start();
                match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            }
            var game = match.Game;
            if (bot) { InvokeProcess(match, EndTurnTask.Any(game.Player1)); FlushFieldHistory(match); }
            var owner = game.CurrentPlayer;
            var llane = transferred ? owner.DeckZone.Single(c => c.Card.Id == "TIME_875t")
                : SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("TIME_875t"), zone: owner.DeckZone);
            Check(llane[GameTag.CONTROLLER] == owner.PlayerId, name + ": fixture Llane belongs to the player");
            if (premium != 0)
                ((Dictionary<int, int>)typeof(NativeMatch).GetField("entityPremiums", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(match))[llane.Id] = premium;
            // Llane on top: the next draw is him.
            owner.DeckZone.Remove(llane); owner.DeckZone.Add(llane);
            FlushFieldHistory(match);
            Generic.Draw(owner);
            FlushFieldHistory(match);
            Check(llane.Zone == owner.HandZone, name + ": the player drew Llane");
            owner.BaseMana = 10; owner.UsedMana = 0;
            FlushFieldHistory(match);
            int hand = owner.HandZone.Count, deck = owner.DeckZone.Count, cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == llane.Id));
            FlushFieldHistory(match);
            DumpDrawHistory(name, capture, cursor);
            Check(llane.Zone == owner.DeckZone && llane[GameTag.CONTROLLER] == owner.PlayerId, name + ": Llane shuffles back into the player's deck");
            Check(owner.HandZone.Count == hand && owner.DeckZone.Count == deck, name + ": Llane drew one card and returned");
            Check(ControllerAdmissionInvariant.Faults(game).Count == 0, name + ": engine Controller and CONTROLLER tag agree at every admission");
            var rows = History(capture, cursor).ToList();
            Check(rows.Count(e => e.HasPowerStart) == rows.Count(e => e.HasPowerEnd), name + ": every block closes");
            // Replay what the client holds for Llane, row by row, up to his move into the deck.
            var held = new Dictionary<int, int>();
            bool shown = false, moved = false;
            foreach (var row in rows)
            {
                var snapshot = row.HasShowEntity ? row.ShowEntity : row.HasFullEntity ? row.FullEntity : row.HasChangeEntity ? row.ChangeEntity : null;
                if (snapshot?.Entity == llane.Id) { foreach (var tag in snapshot.Tags) held[tag.Name] = tag.Value; shown |= row.HasShowEntity; }
                if (row.HasTagChange && row.TagChange.Entity == llane.Id) held[row.TagChange.Tag] = row.TagChange.Value;
                bool move = row.HasTagChange && row.TagChange.Entity == llane.Id && row.TagChange.Tag == (int)GameTag.ZONE
                    && row.TagChange.Value == (int)SabberStoneCore.Enums.Zone.DECK
                    || row.HasHideEntity && row.HideEntity.Entity == llane.Id && row.HideEntity.Zone == (int)SabberStoneCore.Enums.Zone.DECK;
                if (!move || moved) continue;
                moved = true;
                // EntityBase.GetPremiumType: Diamond needs HAS_DIAMOND_QUALITY (else Signature), Signature HAS_SIGNATURE_QUALITY (else Golden).
                held.TryGetValue((int)GameTag.PREMIUM, out int effective);
                if (effective == 2 && (!held.TryGetValue(1932, out int diamond) || diamond == 0)) effective = 3;
                if (effective == 3 && (!held.TryGetValue(2589, out int signature) || signature == 0)) effective = 1;
                Check(effective == 0 || effective == 1, name + ": Llane flies into the deck as Normal or Golden, not premium " + effective);
                Check(effective == (premium == 0 ? 0 : 1), name + ": the flight keeps the most the client can fly: " + effective);
            }
            Check(shown && moved, name + ": the client saw Llane played and moved into the deck");
            if (!bot && premium != 0)
            {
                // The flight drops the quality flags; drawing him again must show his real premium.
                owner.DeckZone.Remove(llane); owner.DeckZone.Add(llane);
                FlushFieldHistory(match);
                int drawCursor = capture.Packets.Count;
                Generic.Draw(owner);
                FlushFieldHistory(match);
                var show = History(capture, drawCursor).Last(e => e.HasShowEntity && e.ShowEntity.Entity == llane.Id).ShowEntity;
                int Tag(int tag) => show.Tags.Where(t => t.Name == tag).Select(t => t.Value).DefaultIfEmpty(0).Last();
                Check(llane.Zone == owner.HandZone && Tag((int)GameTag.PREMIUM) == premium
                    && (premium != 2 || Tag(1932) == 1) && (premium != 3 || Tag(2589) == 1),
                    name + ": the redrawn Llane shows premium " + premium + " with its quality flag again");
            }
            // The human always needs a way on: options after the play (own turn), or after the bot's turn ends.
            if (bot) { InvokeProcess(match, EndTurnTask.Any(owner)); FlushFieldHistory(match); }
            var options = capture.Last<AllOptions>(14);
            Check(game.CurrentPlayer == game.Player1 && options.Options.Any(o => o.Type_ == PegasusGame.Option.Type.END_TURN),
                name + ": the human has End Turn after Llane");
            capture.SerializeAll();
        }
        DeckFlightQualitySweep();
        RewindDeckReturnQuality();
        Console.WriteLine("PASS King Llane: every premium, both seats, ordinary and transferred; no quality flag on the deck flight (#79)");
    }

    // The same client flight serves every minion shuffled into a deck from play or from the graveyard, whatever card does it.
    private static void DeckFlightQualitySweep()
    {
        foreach (bool bot in new[] { false, true })
        foreach (bool fromGraveyard in new[] { false, true })
        foreach (int premium in new[] { 2, 3 })
        {
            string name = $"deck-flight-{(fromGraveyard ? "graveyard" : "play")}-{(bot ? "opponent" : "owner")}-premium{premium}";
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, "mage", "mage", 8427);
            match.Start();
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            var game = match.Game; var owner = bot ? game.Player2 : game.Player1;
            var minion = (SabberStoneCore.Model.Entities.Minion)SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CS2_182"));
            ((Dictionary<int, int>)typeof(NativeMatch).GetField("entityPremiums", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(match))[minion.Id] = premium;
            Generic.SummonBlock(game, minion, -1, null);
            game.DeathProcessingAndAuraUpdate();
            FlushFieldHistory(match);
            if (fromGraveyard) { minion.ToBeDestroyed = true; game.DeathProcessingAndAuraUpdate(); FlushFieldHistory(match); }
            int cursor = capture.Packets.Count;
            var zone = fromGraveyard ? (SabberStoneCore.Model.Zones.IZone)owner.GraveyardZone : owner.BoardZone;
            Check(minion.Zone == zone, name + ": fixture minion in place");
            zone.Remove(minion);
            if (!fromGraveyard) minion.Reset();
            Check(Generic.ShuffleIntoDeck(owner, minion, minion), name + ": shuffled");
            FlushFieldHistory(match);
            var held = new Dictionary<int, int>();
            foreach (var packet in capture.Packets.Take(cursor).Where(p => p.Id == 19))
                foreach (var row in ((PowerHistory)packet.Message).List) Hold(row);
            bool moved = false;
            foreach (var row in History(capture, cursor))
            {
                bool move = row.HasTagChange && row.TagChange.Entity == minion.Id && row.TagChange.Tag == (int)GameTag.ZONE
                    && row.TagChange.Value == (int)SabberStoneCore.Enums.Zone.DECK
                    || row.HasHideEntity && row.HideEntity.Entity == minion.Id && row.HideEntity.Zone == (int)SabberStoneCore.Enums.Zone.DECK;
                if (move && !moved)
                {
                    moved = true;
                    held.TryGetValue((int)GameTag.PREMIUM, out int effective);
                    if (effective == 2 && (!held.TryGetValue(1932, out int d) || d == 0)) effective = 3;
                    if (effective == 3 && (!held.TryGetValue(2589, out int g) || g == 0)) effective = 1;
                    Check(effective == 1, name + ": flies into the deck as Golden, not premium " + effective);
                }
                Hold(row);
            }
            Check(moved, name + ": the client saw the move into the deck");
            capture.SerializeAll();

            void Hold(PowerHistoryData row)
            {
                var snapshot = row.HasShowEntity ? row.ShowEntity : row.HasFullEntity ? row.FullEntity : row.HasChangeEntity ? row.ChangeEntity : null;
                if (snapshot?.Entity == minion.Id) foreach (var tag in snapshot.Tags) held[tag.Name] = tag.Value;
                if (row.HasTagChange && row.TagChange.Entity == minion.Id) held[row.TagChange.Tag] = row.TagChange.Value;
            }
        }
    }

    // A Rewind reset that puts a minion from play back into the deck (the discarded attempt had pulled it out) must not
    // hand the client a Diamond or Signature flight either.
    private static void RewindDeckReturnQuality()
    {
        foreach (int premium in new[] { 2, 3 })
        {
            string name = "rewind-deck-return-premium" + premium;
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, "mage", "mage", 8427);
            match.Start();
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
            var game = match.Game; var owner = game.Player1;
            owner.BaseMana = 10; owner.UsedMana = 0;
            var minion = (SabberStoneCore.Model.Entities.Minion)SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.DeckZone);
            ((Dictionary<int, int>)typeof(NativeMatch).GetField("entityPremiums", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(match))[minion.Id] = premium;
            var announcer = Generic.DrawCard(owner, Cards.FromId("TIME_034"));
            FlushFieldHistory(match);
            typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
            var before = capture.Last<AllOptions>(14);
            int play = before.Options.FindIndex(o => o.MainOption?.Id == match.WireReturnId(announcer.Id) && o.MainOption.PowerKeyword == 0);
            Check(play >= 0, name + ": native play option");
            match.SendOption(before.Id, play, 0, -1, 1);
            Check(game.Rewind != null && owner.Choice?.ChoiceAction == ChoiceAction.REWIND, name + ": Rewind offered");
            // The attempt pulls the premium minion from the deck into play (as a deck-summon would).
            owner.DeckZone.Remove(minion);
            Generic.SummonBlock(game, minion, -1, null);
            game.DeathProcessingAndAuraUpdate();
            FlushFieldHistory(match);
            var held = new Dictionary<int, int>();
            foreach (var packet in capture.Packets.Where(p => p.Id == 19))
                foreach (var row in ((PowerHistory)packet.Message).List) Hold(row);
            Check(held.TryGetValue((int)GameTag.ZONE, out int zone) && zone == (int)SabberStoneCore.Enums.Zone.PLAY
                && held.TryGetValue((int)GameTag.PREMIUM, out int shown) && shown == premium, name + ": the client shows the premium minion in play");
            int cursor = capture.Packets.Count;
            match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int> { game.Rewind.RewindEntityId });
            FlushFieldHistory(match);
            Check(game.IdEntityDic[minion.Id].Zone == game.Player1.DeckZone, name + ": the reset returns the minion to the deck");
            bool moved = false;
            foreach (var row in History(capture, cursor))
            {
                held.TryGetValue((int)GameTag.ZONE, out int from);
                Hold(row);
                held.TryGetValue((int)GameTag.ZONE, out int to);
                if (moved || from == to || to != (int)SabberStoneCore.Enums.Zone.DECK) continue;
                moved = true;
                held.TryGetValue((int)GameTag.PREMIUM, out int effective);
                if (effective == 2 && (!held.TryGetValue(1932, out int d) || d == 0)) effective = 3;
                if (effective == 3 && (!held.TryGetValue(2589, out int g) || g == 0)) effective = 1;
                Check(effective <= 1, name + ": the reset flies the minion into the deck as Normal or Golden, not premium " + effective);
            }
            Check(moved, name + ": the client saw the minion return to the deck");
            capture.SerializeAll();

            void Hold(PowerHistoryData row)
            {
                var snapshot = row.HasShowEntity ? row.ShowEntity : row.HasFullEntity ? row.FullEntity : row.HasChangeEntity ? row.ChangeEntity : null;
                if (snapshot?.Entity == minion.Id) foreach (var tag in snapshot.Tags) held[tag.Name] = tag.Value;
                if (row.HasTagChange && row.TagChange.Entity == minion.Id) held[row.TagChange.Tag] = row.TagChange.Value;
                if (row.HasHideEntity && row.HideEntity.Entity == minion.Id) held[(int)GameTag.ZONE] = row.HideEntity.Zone;
            }
        }
    }
}
