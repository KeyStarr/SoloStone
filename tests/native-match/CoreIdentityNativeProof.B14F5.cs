using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    [GroupProof("native-b14-f5-core-identity", ProofKind.Native)]
    private static void CoreIdentityNativeProof()
    {
        CoreChoiceDeckPrivacy();
        foreach (bool bot in new[] { false, true })
        foreach (string id in new[] { "EDR_001", "CORE_BT_035", "Core_UNG_072" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var game = match.Game; var owner = game.CurrentPlayer;
            foreach (var old in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(old));
            owner.BaseMana = 10; owner.UsedMana = 0;
            var source = EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone);
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            InvokeProcess(match, PlayCardTask.Any(owner, source)); FlushFieldHistory(match);
            if (owner.Choice != null)
            {
                var offers = owner.Choice.Choices.ToArray();
                Check(offers.Length == 3 && offers.All(n => game.IdEntityDic[n].Card.Taunt), "native Stonehill offers three distinct Taunt identities");
                var snapshots = History(capture, cursor).Where(e => offers.Contains(SnapshotEntity(e))).ToArray();
                Check(snapshots.Any() && snapshots.All(e => bot ? SnapshotName(e) == "" : SnapshotName(e) == game.IdEntityDic[SnapshotEntity(e)].Card.Id),
                    "opponent never sees private Discover offer identities; owner sees exact export IDs");
                InvokeProcess(match, ChooseTask.Pick(owner, offers[0])); FlushFieldHistory(match);
            }
            var generated = owner.HandZone.ToArray();
            Check(generated.Length == 1, "one generated or drawn hand card");
            foreach (var output in generated)
            {
                var snapshots = History(capture, cursor).Where(e => SnapshotEntity(e) == output.Id &&
                    (e.HasFullEntity || e.HasShowEntity || e.HasChangeEntity)).ToArray();
                Check(snapshots.Length > 0 && snapshots.All(e => bot ? SnapshotName(e) == "" : SnapshotName(e) == output.Card.Id),
                    "private generated/drawn identities: " + id + "; bot=" + bot);
                if (bot)
                    Check(snapshots.All(e => HasOnlyHiddenTags(e.HasFullEntity ? e.FullEntity.Tags : e.HasShowEntity ? e.ShowEntity.Tags : e.ChangeEntity.Tags)),
                        "private generated/drawn tags omit identity and stats");
            }
            capture.SerializeAll();
        }
        foreach (bool bot in new[] { false, true })
        {
        var (choiceMatch, choiceCapture) = AnimationMatch(botTurn: bot);
        var player = choiceMatch.Game.CurrentPlayer;
        foreach (var row in new[] {
            ("CORE_AT_037", "AT_037a", "AT_037b"), ("CORE_EX1_154", "EX1_154a", "EX1_154b"),
            ("CORE_EX1_160", "EX1_160a", "EX1_160b"), ("CORE_OG_047", "OG_047a", "OG_047b"),
            ("CORE_ONY_018", "ONY_018t", "ONY_018t2"), ("CORE_TSC_650", "TSC_650a", "TSC_650d"),
            ("Core_LOE_115", "LOE_115a", "LOE_115b") })
        {
            foreach (var old in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(old));
            player.BaseMana = 10; player.UsedMana = 0;
            var source = EngineEntity.FromCard(player, Cards.FromId(row.Item1), zone: player.HandZone);
            Check(source.ChooseOnePlayables.Select(p => p.Card.Id).SequenceEqual(new[] { row.Item2, row.Item3 }), "native Core option actors");
            FlushFieldHistory(choiceMatch);
            foreach (var option in source.ChooseOnePlayables)
            {
                var snapshots = History(choiceCapture, 0).Where(e => SnapshotEntity(e) == option.Id &&
                    (e.HasFullEntity || e.HasShowEntity || e.HasChangeEntity)).ToArray();
                Check((bot || snapshots.Length > 0) && snapshots.All(e => bot ? SnapshotName(e) == "" : SnapshotName(e) == option.Card.Id),
                    "choice option visibility follows parent owner: " + option.Card.Id + " bot=" + bot + " names=" + string.Join(",", snapshots.Select(SnapshotName)));
            }
        }
        choiceCapture.SerializeAll();
        }
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var owner = match.Game.CurrentPlayer;
            foreach (var old in owner.BoardZone.ToArray()) owner.SetasideZone.Add(owner.BoardZone.Remove(old));
            var minion = (SabberStoneCore.Model.Entities.Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_200"));
            SabberStoneCore.Actions.Generic.SummonBlock(match.Game, minion, -1, null);
            var shell = EngineEntity.FromCard(owner, Cards.FromId("RLK_048"), zone: owner.HandZone);
            owner.BaseMana = 10; owner.UsedMana = 0; FlushFieldHistory(match);
            InvokeProcess(match, PlayCardTask.Spell(owner, shell)); FlushFieldHistory(match);
            var tags = match.SnapshotVisibleTransportTags(minion);
            Check(tags.GetValueOrDefault((int)GameTag.CANT_BE_TARGETED_BY_SPELLS) == 1
                && tags.GetValueOrDefault((int)GameTag.CANT_BE_TARGETED_BY_HERO_POWERS) == 1,
                "Shell projects both native targeting protections");
            minion.Silence(); FlushFieldHistory(match);
            tags = match.SnapshotVisibleTransportTags(minion);
            Check(tags.GetValueOrDefault((int)GameTag.CANT_BE_TARGETED_BY_SPELLS) == 0
                && tags.GetValueOrDefault((int)GameTag.CANT_BE_TARGETED_BY_HERO_POWERS) == 0,
                "Shell Silence clears both native targeting protections");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS native Core identity: exact option actors; both-side Dream/draw/Discover privacy");
    }

    private static void CoreChoiceDeckPrivacy()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string id in new[] { "CORE_AT_037", "CORE_EX1_154", "CORE_EX1_160", "CORE_OG_047",
            "CORE_ONY_018", "CORE_TSC_650", "Core_LOE_115" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var owner = match.Game.CurrentPlayer;
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var source = EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.DeckZone);
            FlushFieldHistory(match);
            var ids = source.ChooseOnePlayables.Select(option => option.Id).Append(source.Id).ToArray();
            var snapshots = History(capture, cursor).Where(e => ids.Contains(SnapshotEntity(e)) &&
                (e.HasFullEntity || e.HasShowEntity || e.HasChangeEntity)).ToArray();
            Check(snapshots.Any() && snapshots.All(e => SnapshotName(e) == "" &&
                HasOnlyHiddenTags(e.HasFullEntity ? e.FullEntity.Tags : e.HasShowEntity ? e.ShowEntity.Tags : e.ChangeEntity.Tags)),
                "deck-hidden parent and options expose neither identities nor parent links: " + id + " bot=" + bot);

            foreach (var restored in new[] { match.Game.CloneExact(history: true), SabberStoneCore.Model.Game.LoadSnapshot(match.Game.SaveSnapshot()) })
            {
                cursor = capture.Packets.Count;
                var reset = new List<SabberStoneCore.Kettle.IPowerHistoryEntry> {
                    SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(BlockType.GAME_RESET, source.Id, "", 0, 0),
                    SabberStoneCore.Kettle.PowerHistoryBuilder.ResetGame(restored)
                };
                reset.AddRange(restored.IdEntityDic.Values.OrderBy(entity => entity.Id)
                    .Select(entity => SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(entity, reset: true)));
                reset.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockEnd());
                SendHistoryEntries(match, reset);
                snapshots = History(capture, cursor).Where(e => ids.Contains(SnapshotEntity(e)) &&
                    (e.HasFullEntity || e.HasShowEntity || e.HasChangeEntity)).ToArray();
                Check(snapshots.Any() && snapshots.All(e => SnapshotName(e) == "" &&
                    HasOnlyHiddenTags(e.HasFullEntity ? e.FullEntity.Tags : e.HasShowEntity ? e.ShowEntity.Tags : e.ChangeEntity.Tags)),
                    "clone/snapshot reset keeps deck options and parent links private: " + id);
            }

            cursor = capture.Packets.Count;
            owner.HandZone.Add(owner.DeckZone.Remove(source));
            FlushFieldHistory(match);
            var reveals = History(capture, cursor).Where(e => ids.Contains(SnapshotEntity(e)) && e.HasShowEntity).ToArray();
            Check(bot ? reveals.All(e => SnapshotName(e) == "") : source.ChooseOnePlayables.All(option =>
                reveals.Any(e => SnapshotEntity(e) == option.Id && SnapshotName(e) == option.Card.Id)),
                "drawing reveals options only to owner: " + id);

            cursor = capture.Packets.Count;
            owner.DeckZone.Add(owner.HandZone.Remove(source));
            FlushFieldHistory(match);
            var history = History(capture, cursor);
            Check(bot || source.ChooseOnePlayables.All(option => history.Any(e => e.HasHideEntity && e.HideEntity.Entity == option.Id)),
                "returning parent to deck hides its known options: " + id);

            cursor = capture.Packets.Count;
            owner.GraveyardZone.Add(owner.DeckZone.Remove(source));
            FlushFieldHistory(match);
            reveals = History(capture, cursor).Where(e => ids.Contains(SnapshotEntity(e)) && e.HasShowEntity).ToArray();
            Check(source.ChooseOnePlayables.All(option => reveals.Any(e => SnapshotEntity(e) == option.Id && SnapshotName(e) == option.Card.Id)),
                "public parent reveals both players' options: " + id);
            capture.SerializeAll();
        }
    }
}
