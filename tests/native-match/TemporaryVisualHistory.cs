using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    // The engine marks Temporary cards with its own tag 3630, which the pinned client does not know. The client's
    // Temporary hand look and ghost discard (Card.GetBestNullZoneSpell) key off GHOSTLY (785), so transport projects
    // Temporary to GHOSTLY and keeps it through the end-of-turn discard. Echo's GHOSTLY stays independent, and an
    // opponent's hidden hand stays private.
    private static void TemporaryVisualHistory(string onlySource = null)
    {
        foreach (bool opponent in new[] { false, true })
        foreach (string sourceId in new[] { "TLC_451", "JAIL_986", "BOT_568" }.Where(id => onlySource == null || id == onlySource))
        {
            var (match, capture) = AnimationMatch(botTurn: opponent);
            var owner = match.Game.CurrentPlayer;
            foreach (var old in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(old));
            var source = Generic.DrawCard(owner, Cards.FromId(sourceId));
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            if (!opponent)
            {
                typeof(NativeMatch).GetMethod("SendHumanOptions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(match, null);
                var options = capture.Last<AllOptions>(14);
                int play = options.Options.FindIndex(o => o.MainOption?.Id == source.Id);
                Check(play >= 0, "Temporary source has native play option");
                match.SendOption(options.Id, play, 0, -1, source is Minion ? 1 : 0);
            }
            else
            {
                InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
                FlushFieldHistory(match);
            }
            if (sourceId == "TLC_451")
            {
                Check(owner.Choice?.Choices.Count > 0, "Catacombs offers deck cards");
                if (opponent)
                {
                    InvokeProcess(match, ChooseTask.Pick(owner, owner.Choice.Choices[0]));
                    FlushFieldHistory(match);
                }
                else
                {
                    var choices = capture.Last<EntityChoices>(17);
                    match.SendChoices(choices.Id, new List<int> { choices.Entities[0] });
                }
            }
            var card = owner.HandZone.First(TemporaryRules.IsTemporary);
            var helper = card.AppliedEnchantments.Single(e => e.Card.Id == "GBL_999e");
            var grant = capture.HistorySnapshots(cursor).SelectMany(p => p.List).ToList();
            Check(!grant.Any(e => e.HasTagChange && e.TagChange.Tag == (int)TemporaryRules.Temporary),
                "Engine Temporary tag must be projected to a retail tag");
            bool HasGhost(PowerHistoryData e, int value) => e.HasTagChange && e.TagChange.Entity == card.Id
                && e.TagChange.Tag == (int)GameTag.GHOSTLY && e.TagChange.Value == value
                || SnapshotEntity(e) == card.Id && SnapshotTag(e, GameTag.GHOSTLY) == value && value != 0;
            if (!opponent)
            {
                Check(grant.Any(e => HasGhost(e, 1)), sourceId + " owner Temporary needs retail GHOSTLY=1 on wire");
                Check(TemporaryWireGhost(capture, card.Id) == 1,
                    sourceId + " endpoint refresh must retain Temporary hand visual");
                Check(grant.Any(e => SnapshotEntity(e) == helper.Id && SnapshotName(e) == "GBL_999e"
                    && SnapshotTag(e, GameTag.ATTACHED) == card.Id), "Owner sees attached Temporary helper");
            }
            else
            {
                Check(!grant.Any(e => HasGhost(e, 1)), "Hidden opponent Temporary marker stays private");
                Check(grant.Where(e => SnapshotEntity(e) == card.Id || SnapshotEntity(e) == helper.Id)
                    .All(e => string.IsNullOrEmpty(SnapshotName(e)) && SnapshotTag(e, GameTag.ATTACHED) == 0),
                    "Hidden opponent card/helper have no identity or attachment leak");
            }
            cursor = capture.Packets.Count;
            InvokeProcess(match, EndTurnTask.Any(owner));
            FlushFieldHistory(match);
            var expiry = capture.HistorySnapshots(cursor).SelectMany(p => p.List).ToList();
            Check(card.Zone == owner.GraveyardZone && owner.DiscardedEntities.Contains(card.Id)
                && !TemporaryRules.IsTemporary(card), "Temporary expires through real engine discard");
            Check(expiry.Any(e => IsZone(e, card.Id, EngineZone.GRAVEYARD)
                || SnapshotEntity(e) == card.Id && SnapshotTag(e, GameTag.ZONE) == (int)EngineZone.GRAVEYARD),
                "Expiry sends native HAND to GRAVEYARD discard transition");
            Check(expiry.Any(e => SnapshotEntity(e) == card.Id && SnapshotName(e) == card.Card.Id),
                "Discard reveals card to either observer");
            if (!opponent)
            {
                int ghost = 1;
                foreach (var row in expiry)
                {
                    if (row.HasTagChange && row.TagChange.Entity == card.Id && row.TagChange.Tag == (int)GameTag.GHOSTLY)
                        ghost = row.TagChange.Value;
                    if (SnapshotEntity(row) == card.Id)
                    {
                        var snapshot = row.HasFullEntity ? row.FullEntity : row.HasShowEntity ? row.ShowEntity : row.ChangeEntity;
                        var tag = snapshot.Tags.FirstOrDefault(t => t.Name == (int)GameTag.GHOSTLY);
                        if (tag != null) ghost = tag.Value;
                    }
                    if (IsZone(row, card.Id, EngineZone.GRAVEYARD))
                        Check(ghost == 1, "Friendly discard must retain GHOSTLY for Card.GetBestNullZoneSpell (127)");
                }
                owner.HandZone.Add(owner.GraveyardZone.Remove(card));
                FlushFieldHistory(match);
                Check(TemporaryWireGhost(capture, card.Id) == 0, "Reused discard must lose its old Temporary visual");
            }
            capture.SerializeAll();
        }
        TemporaryCopyTransformVisualHistory();
        TemporaryAndEchoVisualHistory();
        TemporaryRevealedOpponentVisualHistory();
        Console.WriteLine("PASS Temporary visuals: Catacombs, Forger, Soularium, both observers/reveal, discard/reuse, copy/transform, Echo overlap");
    }

    private static void TemporaryCopyTransformVisualHistory()
    {
        foreach (string targetId in new[] { "CS2_200", "CS2_029" })
        {
            var (match, capture) = AnimationMatch();
            var owner = match.Game.Player1;
            var card = Generic.DrawCard(owner, Cards.FromId("CS2_182"));
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            TemporaryRules.Make(card);
            // Leave marking, copy, transform and expiry in one flush: endpoint getters cannot supply past state.
            var copy = Generic.Copy(owner, card, card, EngineZone.HAND);
            var transformed = Generic.ChangeEntityBlock(owner, card, Cards.FromId(targetId), true);
            Check(TemporaryRules.IsTemporary(copy) && TemporaryRules.IsTemporary(transformed), "Temporary copy/transform fixture");
            var beforeExpiry = match.Game.PowerHistory.Last.ToArray();
            InvokeProcess(match, EndTurnTask.Any(owner));
            match.Game.PowerHistory.Last.InsertRange(0, beforeExpiry);
            FlushFieldHistory(match);
            var rows = capture.HistorySnapshots(cursor).SelectMany(p => p.List).ToList();
            Check(rows.Any(e => e.HasChangeEntity && e.ChangeEntity.Entity == transformed.Id
                && SnapshotTag(e, GameTag.GHOSTLY) == 1), "Transform retains Temporary in its historical definition delta");
            foreach (var target in new[] { transformed, copy })
            {
                Check(rows.Any(e => SnapshotEntity(e) == target.Id && SnapshotTag(e, GameTag.GHOSTLY) == 1
                    || e.HasTagChange && e.TagChange.Entity == target.Id && e.TagChange.Tag == (int)GameTag.GHOSTLY && e.TagChange.Value == 1),
                    "Frozen history must retain Temporary visual before same-batch expiry: entity=" + target.Id);
                Check(rows.Any(e => IsZone(e, target.Id, EngineZone.GRAVEYARD)
                    || SnapshotEntity(e) == target.Id && SnapshotTag(e, GameTag.ZONE) == (int)EngineZone.GRAVEYARD), "Copy/transformed card discards");
            }
            capture.SerializeAll();
        }
    }


    private static void TemporaryRevealedOpponentVisualHistory()
    {
        var (match, capture) = AnimationMatch(botTurn: true);
        var card = Generic.DrawCard(match.Game.CurrentPlayer, Cards.FromId("CS2_029"));
        TemporaryRules.Make(card);
        FlushFieldHistory(match);
        int cursor = capture.Packets.Count;
        var show = SabberStoneCore.Kettle.PowerHistoryBuilder.ShowEntity(card);
        show.Entity.Tags[GameTag.REVEALED] = 1;
        match.Game.PowerHistory.Add(show);
        FlushFieldHistory(match);
        Check(capture.HistorySnapshots(cursor).SelectMany(p => p.List).Any(e => e.HasShowEntity
            && e.ShowEntity.Entity == card.Id && e.ShowEntity.Name == card.Card.Id
            && SnapshotTag(e, GameTag.GHOSTLY) == 1), "Explicit opponent hand reveal includes Temporary visual");
        capture.SerializeAll();
    }

    private static int TemporaryWireGhost(Capture capture, int id)
    {
        int ghost = 0;
        foreach (var row in capture.HistorySnapshots().SelectMany(packet => packet.List))
        {
            if (row.HasTagChange && row.TagChange.Entity == id && row.TagChange.Tag == (int)GameTag.GHOSTLY)
                ghost = row.TagChange.Value;
            if (SnapshotEntity(row) != id) continue;
            var snapshot = row.HasFullEntity ? row.FullEntity : row.HasShowEntity ? row.ShowEntity : row.ChangeEntity;
            var tag = snapshot.Tags.FirstOrDefault(t => t.Name == (int)GameTag.GHOSTLY);
            if (tag != null) ghost = tag.Value;
        }
        return ghost;
    }
    private static void TemporaryAndEchoVisualHistory()
    {
        var (match, capture) = AnimationMatch();
        var owner = match.Game.Player1;
        var card = Generic.DrawCard(owner, Cards.FromId("CS2_029"));
        card[GameTag.GHOSTLY] = 1;
        FlushFieldHistory(match);
        TemporaryRules.Make(card);
        FlushFieldHistory(match);
        int cursor = capture.Packets.Count;
        owner.SetasideZone.Add(owner.HandZone.Remove(card));
        FlushFieldHistory(match);
        var rows = capture.HistorySnapshots(cursor).SelectMany(p => p.List).ToList();
        Check(!rows.Any(e => e.HasTagChange && e.TagChange.Entity == card.Id
            && e.TagChange.Tag == (int)GameTag.GHOSTLY && e.TagChange.Value == 0),
            "Clearing Temporary must preserve independent Echo GHOSTLY");
        owner.HandZone.Add(owner.SetasideZone.Remove(card));
        TemporaryRules.Make(card);
        FlushFieldHistory(match);
        cursor = capture.Packets.Count;
        card[GameTag.GHOSTLY] = 0;
        FlushFieldHistory(match);
        Check(capture.HistorySnapshots(cursor).SelectMany(p => p.List).Any(e => e.HasTagChange
            && e.TagChange.Entity == card.Id && e.TagChange.Tag == (int)GameTag.GHOSTLY && e.TagChange.Value == 1),
            "Clearing Echo must preserve independent Temporary visual");
        owner.SetasideZone.Add(owner.HandZone.Remove(card));
        FlushFieldHistory(match);
        Check(TemporaryWireGhost(capture, card.Id) == 0, "Ordinary hand exit clears Temporary visual");
        capture.SerializeAll();
    }

}
