using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Actions;

internal static partial class Program
{
    // #156: a minion that leaves the board for the deck (King Llane, Entomb, Typhoon) is moved with retail's rows: ZONE_POSITION=0, HIDE_ENTITY, then the ZONE change. The client hides the card before it
    // flies it into the deck; a ZONE change first starts the flight on a revealed entity, and Llane jerked and vanished.
    private static string LeaveRow(PowerHistoryData e, int id)
    {
        if (e.HasTagChange && e.TagChange.Entity == id)
            return e.TagChange.Tag == (int)GameTag.ZONE ? "ZONE=" + e.TagChange.Value
                : e.TagChange.Tag == (int)GameTag.ZONE_POSITION ? "POS=" + e.TagChange.Value : null;
        if (e.HasHideEntity && e.HideEntity.Entity == id) return "HIDE=" + e.HideEntity.Zone;
        if (e.HasShowEntity && e.ShowEntity.Entity == id) return "SHOW";
        return null;
    }

    private static (NativeMatch Match, Capture Capture, Controller Owner) LeaveBoardStart(bool bot)
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, "mage", "mage", 8427);
        match.Start();
        match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
        var game = match.Game;
        if (bot) { InvokeProcess(match, EndTurnTask.Any(game.Player1)); FlushFieldHistory(match); }
        var owner = game.CurrentPlayer;
        owner.BaseMana = 10; owner.UsedMana = 0;
        FlushFieldHistory(match);
        return (match, capture, owner);
    }

    private static Minion LeaveBoardSummon(NativeMatch match, Controller owner, string id)
    {
        var m = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId(id));
        Generic.SummonBlock(match.Game, m, -1, null);
        match.Game.DeathProcessingAndAuraUpdate();
        FlushFieldHistory(match);
        return m;
    }

    // The move into the deck: ZONE_POSITION=0, HIDE(DECK), ZONE=DECK in that order, nothing between HIDE and ZONE, no later HIDE.
    private static void CheckDeckMoveRows(Capture capture, int cursor, int id, string name)
    {
        var rows = History(capture, cursor).Select(e => LeaveRow(e, id)).Where(r => r != null).ToList();
        int pos = rows.IndexOf("POS=0"), hide = rows.IndexOf("HIDE=" + (int)SabberStoneCore.Enums.Zone.DECK),
            zone = rows.IndexOf("ZONE=" + (int)SabberStoneCore.Enums.Zone.DECK);
        string shown = string.Join(",", rows);
        Check(pos >= 0 && hide > pos && zone == hide + 1, name + ": ZONE_POSITION=0, HIDE(DECK), ZONE=DECK in order: " + shown);
        Check(rows.Count(r => r.StartsWith("HIDE")) == 1 && rows.Last() == "ZONE=" + (int)SabberStoneCore.Enums.Zone.DECK,
            name + ": one HIDE and none after the ZONE change: " + shown);
    }

    private static void CheckPlayAdmission(Capture capture, string name)
    {
        var faults = NativeAdmission.ValidatePackets(capture.HistorySnapshots().Select(p => (IReadOnlyList<PowerHistoryData>)p.List));
        foreach (var fault in faults.Where(f => f.Rule == "R7")) Check(false, name + " R7: " + fault);
    }

    private static void LeaveBoardHistory()
    {
        foreach (bool bot in new[] { false, true })
        {
            string seat = bot ? "opponent" : "owner";
            {   // King Llane's Battlecry
                var (match, capture, owner) = LeaveBoardStart(bot);
                var llane = SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("TIME_875t"), zone: owner.DeckZone);
                owner.DeckZone.Remove(llane); owner.DeckZone.Add(llane);
                FlushFieldHistory(match);
                Generic.Draw(owner); FlushFieldHistory(match);
                int cursor = capture.Packets.Count;
                InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == llane.Id));
                FlushFieldHistory(match);
                Check(llane.Zone == owner.DeckZone, "llane-" + seat + ": back in the deck");
                CheckDeckMoveRows(capture, cursor, llane.Id, "llane-" + seat);
                CheckPlayAdmission(capture, "llane-" + seat);
            }
            foreach (string spellId in new[] { "LOE_104", "EDR_846t2", "EDR_232" })
            {   // a MoveToDeck card (Entomb), Corrupted Dream, Typhoon
                string name = spellId + "-" + seat;
                var (match, capture, owner) = LeaveBoardStart(bot);
                var victim = LeaveBoardSummon(match, spellId == "LOE_104" ? owner.Opponent : owner, "CS2_182");
                var spell = Generic.DrawCard(owner, Cards.FromId(spellId));
                FlushFieldHistory(match);
                int cursor = capture.Packets.Count;
                var task = owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id
                    && (spellId == "EDR_232" || t.Target?.Id == victim.Id));
                InvokeProcess(match, task);
                FlushFieldHistory(match);
                Check(victim.Zone == victim.Controller.DeckZone, name + ": minion went into a deck, now " + victim.Zone?.Type);
                CheckDeckMoveRows(capture, cursor, victim.Id, name);
                CheckPlayAdmission(capture, name);
            }
            CorruptedDreamFullDeck(bot);
        }
        Console.WriteLine("PASS leaving the board for the deck: Llane, Entomb, Corrupted Dream, Typhoon send retail's order on both seats");
    }

    private static void CorruptedDreamFullDeck(bool bot)
    {
        string name = "corrupted-dream-full-deck-" + (bot ? "opponent" : "owner");
        var (match, capture, caster) = LeaveBoardStart(bot);
        var owner = caster.Opponent;
        var victim = LeaveBoardSummon(match, owner, "EX1_096"); // Loot Hoarder's Deathrattle draws a card.
        while (!owner.DeckZone.IsFull)
            SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CS2_231"), zone: owner.DeckZone);
        var spell = Generic.DrawCard(caster, Cards.FromId("EDR_846t2"));
        int handCount = owner.HandZone.Count;
        FlushFieldHistory(match);
        int cursor = capture.Packets.Count;
        InvokeProcess(match, caster.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD
            && t.Source?.Id == spell.Id && t.Target?.Id == victim.Id));
        FlushFieldHistory(match);
        Check(victim.Zone == owner.GraveyardZone && owner.GraveyardZone.Contains(victim), name + ": target moved to its graveyard");
        Check(owner.HandZone.Count == handCount && owner.DeckZone.IsFull, name + ": no Deathrattle draw");
        Check(!History(capture, cursor).Any(e => e.HasHideEntity && e.HideEntity.Entity == victim.Id
            && e.HideEntity.Zone == (int)SabberStoneCore.Enums.Zone.DECK), name + ": no deck HIDE on overflow");
        CheckPlayAdmission(capture, name);
        Console.WriteLine("PASS " + name);
    }
}
