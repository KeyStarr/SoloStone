using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    private static (NativeMatch Match, Capture Capture, int Quest, List<PowerHistoryData> History) QuestCompleteCase(bool bot, string id)
    {
        var (match, capture) = AnimationMatch(bot);
        var game = match.Game;
        var owner = game.CurrentPlayer;
        var quest = InstallHistoryQuest(match, id);
        int cursor = capture.Packets.Count;
        if (id == "TLC_602") InvokeProcess(match, owner.Options().Single(t => t.PlayerTaskType == PlayerTaskType.END_TURN));
        else
        {
            var murloc = EngineEntity.FromCard(owner, Cards.FromId("EX1_506"), zone: owner.HandZone);
            FlushFieldHistory(match);
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == murloc.Id));
        }
        FlushFieldHistory(match);
        return (match, capture, quest.Id, History(capture, cursor));
    }

    // Playtest 2026-11 (#26): when the opponent completed a quest, the client showed no quest-complete animation and
    // no history tile. Its QuestCompleteSpell needs a TRIGGER block for the quest that holds the reward FULL_ENTITY, and
    // it plays only when that entity's card id is the card the quest names (QUEST_REWARD_DATABASE_ID); the opponent's
    // reward was anonymous. Retail (opposing_maestra and friendly_maestra build 88998, transformed_on_board build 39954)
    // sends progress as a TRIGGER block at effectIndex 1 and completion as a sibling TRIGGER block at effectIndex 0 with
    // triggerKeyword QUEST, the reward named inside it. The reward is hidden again at the end of the block.
    private static void QuestCompleteHistory()
    {
        var failures = new List<string>();
        int cases = 0;
        foreach (bool bot in new[] { false, true })
        foreach (var (id, reward) in new[] { ("TLC_602", "TLC_602t"), ("UNG_942", "UNG_942t") })
        {
            string name = $"{id}-{(bot ? "opponent" : "own")}";
            var (match, capture, quest, h) = QuestCompleteCase(bot, id);
            int progress = h.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == quest && (int)e.PowerStart.Type == (int)BlockType.TRIGGER);
            if (progress < 0 || h[progress].PowerStart.EffectIndex != 1 || h[progress].PowerStart.HasTriggerKeyword)
            { failures.Add(name + ": progress must be a TRIGGER block at effectIndex 1 without keyword"); continue; }
            Check(!h[progress].PowerStart.ShowInHistory, name + ": progress has no history tile");
            int progressEnd = h.FindIndex(progress + 1, e => e.HasPowerEnd);
            var progressTags = h.Skip(progress + 1).Take(progressEnd - progress - 1).ToList();
            if (progressEnd < 0 || !progressTags.Any(e => e.HasTagChange && e.TagChange.Entity == quest && e.TagChange.Tag == (int)GameTag.QUEST_PROGRESS)
                || progressTags.Any(e => e.HasFullEntity || e.HasShowEntity))
                failures.Add(name + ": progress block holds only the progress tick");
            int complete = progressEnd < 0 ? -1 : h.FindIndex(progressEnd + 1, e => e.HasPowerStart && e.PowerStart.Source == quest);
            if (complete != progressEnd + 1 || h[complete].PowerStart.EffectIndex != 0
                || (int)h[complete].PowerStart.Type != (int)BlockType.TRIGGER || h[complete].PowerStart.TriggerKeyword != (int)GameTag.QUEST)
            { failures.Add(name + ": completion must be the sibling TRIGGER block at effectIndex 0 with keyword QUEST"); continue; }
            Check(h[complete].PowerStart.ShowInHistory, name + ": completion requests the history tile");
            Check(capture.HistorySnapshots().SelectMany(p => p.List).Any(e => e.HasPowerStart && e.PowerStart.Source == quest
                && e.PowerStart.TriggerKeyword == (int)GameTag.QUEST && e.PowerStart.ShowInHistory), name + ": tile flag survives wire serialization");
            Check(!h.Skip(complete + 1).Any(e => e.HasPowerStart && e.PowerStart.Source == quest
                && e.PowerStart.EffectIndex == 1), name + ": no progress spell after completion");
            int end = h.FindIndex(complete + 1, e => e.HasPowerEnd);
            var body = h.Skip(complete + 1).Take(end - complete - 1).ToList();
            var full = body.FirstOrDefault(e => e.HasFullEntity);
            if (full == null || full.FullEntity.Name != reward)
            { failures.Add(name + ": the first FULL_ENTITY of the completion block must be the quest's reward " + reward + ", got '" + full?.FullEntity.Name + "'"); continue; }
            var questCard = Cards.FromId(id);
            Check(questCard[GameTag.QUEST_REWARD_DATABASE_ID] == Cards.FromId(reward).AssetId, name + ": quest names this reward card");
            int rewardId = full.FullEntity.Entity;
            bool hidden = body.Any(e => e.HasHideEntity && e.HideEntity.Entity == rewardId);
            bool cleared = body.Any(e => e.HasTagChange && e.TagChange.Entity == rewardId && e.TagChange.Tag == (int)GameTag.REVEALED && e.TagChange.Value == 0);
            if (bot != hidden || bot != cleared)
                failures.Add(name + ": the opponent's reward is hidden again at the end of the block, the owner's stays: hide=" + hidden + " clear=" + cleared);
            if (bot && body.Where(e => e.HasShowEntity && e.ShowEntity.Entity == rewardId).Any(e => e.ShowEntity.Name != reward))
                failures.Add(name + ": the opponent's reward SHOW_ENTITY names the same card");
            // Nothing else of the opponent's hand is named from the completion block on.
            if (bot)
                foreach (var e in h.Skip(complete).Where(e => (e.HasFullEntity || e.HasShowEntity) && SnapshotName(e).Length > 0
                    && SnapshotTag(e, GameTag.ZONE) == (int)EngineZone.HAND && SnapshotTag(e, GameTag.CONTROLLER) == 2
                    && SnapshotEntity(e) != rewardId))
                    failures.Add(name + ": named opponent hand card " + SnapshotEntity(e) + " " + SnapshotName(e));
            failures.AddRange(NativeAdmission.ValidatePackets(capture.HistorySnapshots().Select(p => (IReadOnlyList<PowerHistoryData>)p.List))
                .Select(f => name + ": " + f));
            capture.SerializeAll();
            cases++;
        }
        Check(failures.Count == 0, string.Join("\n", failures));
        QuestRewardPrivacyHistory();
        QuestCombinedRewardHistory();
        QuestDirectAdvanceHistory();
        Console.WriteLine($"PASS quest complete history: {cases} completion cases, both seats; history tiles, reward privacy across packet splits, TLC_817 combination, direct Advance quests, no graveyard progress spell");
    }

    private static SabberStoneCore.Model.Entities.Spell InstallHistoryQuest(NativeMatch match, string id)
    {
        var owner = match.Game.CurrentPlayer;
        var quest = (SabberStoneCore.Model.Entities.Spell)EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.DeckZone);
        FlushFieldHistory(match);
        new SabberStoneCore.Tasks.SimpleTasks.PlayTask(SabberStoneCore.Tasks.SimpleTasks.PlayType.SPELL)
            .Process(match.Game, owner, owner.Hero, null, new TaskStack { Playables = new[] { (IPlayable)quest } });
        quest.QuestProgress = quest.QuestTotalProgress - 1;
        FlushFieldHistory(match);
        return quest;
    }

    private static void QuestCombinedRewardHistory()
    {
        var (match, capture) = AnimationMatch(true);
        var quest = InstallHistoryQuest(match, "TLC_817t");
        EngineEntity.FromCard(quest.Controller, Cards.FromId("TLC_817t4"), zone: quest.Controller.HandZone);
        FlushFieldHistory(match);
        int cursor = capture.Packets.Count;
        QuestRules.Advance(quest);
        FlushFieldHistory(match);
        var h = History(capture, cursor);
        var half = h.Single(e => e.HasFullEntity && e.FullEntity.Name == "TLC_817t3").FullEntity;
        var hides = h.Where(e => e.HasHideEntity && e.HideEntity.Entity == half.Entity).ToArray();
        Check(hides.Length > 0 && hides.All(e => e.HideEntity.Zone == (int)EngineZone.REMOVEDFROMGAME),
            "TLC_817: combined half never returns to HAND");
        Check(quest.Controller.HandZone.Count(c => c.Card.Id == "TLC_817t5") == 1
            && !quest.Controller.HandZone.Any(c => c.Card.Id == "TLC_817t3" || c.Card.Id == "TLC_817t4"), "TLC_817: engine combines halves");
        Check(h.Where(e => e.HasFullEntity && SnapshotTag(e, GameTag.ZONE) == (int)EngineZone.HAND)
            .All(e => e.FullEntity.Entity == half.Entity || e.FullEntity.Name == ""), "TLC_817: combined reward stays anonymous");
        capture.SerializeAll();
    }

    private static void QuestRewardPrivacyHistory()
    {
        // Exercise every packet boundary, including one after the named reward's FULL_ENTITY.
        foreach (int keyword in new[] { (int)GameTag.QUEST, (int)QuestRules.QuestlineTag })
        foreach (int split in new[] { 0, 1, 2, 3, 4 })
        {
            var (match, capture) = AnimationMatch(true);
            var quest = InstallHistoryQuest(match, "TLC_602");
            var owner = quest.Controller;
            var snapshots = new List<IPowerHistoryEntry>();
            foreach (string card in new[] { "TLC_602t", "CS2_182" })
            {
                var reward = EngineEntity.FromCard(owner, Cards.FromId(card), zone: owner.HandZone);
                reward[GameTag.DISPLAYED_CREATOR] = quest.Id;
                snapshots.Add(PowerHistoryBuilder.FullEntity(reward));
            }
            var rows = new List<IPowerHistoryEntry> { PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, quest.Id, "", -1, 0,
                effectIndex: 0, triggerKeyword: keyword) };
            rows.AddRange(snapshots);
            rows.Add(PowerHistoryBuilder.BlockEnd());
            match.Game.PowerHistory.Last.Clear();
            int cursor = capture.Packets.Count;
            SendHistoryEntries(match, rows.Take(split).ToList());
            SendHistoryEntries(match, rows.Skip(split).ToList());
            var h = History(capture, cursor);
            Check(h.Single(e => e.HasPowerStart).PowerStart.ShowInHistory, "quest and questline completion request history tile");
            Check(!h.Any(e => (e.HasFullEntity || e.HasShowEntity) && SnapshotName(e) == "CS2_182"),
                "quest-made random card remains anonymous: keyword=" + keyword + " split=" + split + " snapshots="
                + string.Join(";", h.Where(e => e.HasFullEntity || e.HasShowEntity).Select(e =>
                    SnapshotEntity(e) + ":" + SnapshotName(e) + ":zone=" + SnapshotTag(e, GameTag.ZONE) + ":owner=" + SnapshotTag(e, GameTag.CONTROLLER))));
            var named = h.Where(e => e.HasFullEntity && e.FullEntity.Name == "TLC_602t").ToArray();
            Check(named.Length == (split == 0 || split == rows.Count ? 1 : 0), "only complete same-send blocks reveal rewards");
            foreach (var packet in capture.HistorySnapshots(cursor))
                foreach (var reward in packet.List.Where(e => e.HasFullEntity && e.FullEntity.Name == "TLC_602t"))
                    Check(packet.List.Any(e => e.HasHideEntity && e.HideEntity.Entity == reward.FullEntity.Entity),
                        "named quest reward hidden in same send");
            capture.SerializeAll();
        }
    }

    private static void QuestDirectAdvanceHistory()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string id in new[] { "TLC_446", "TLC_433", "TLC_460" })
        {
            var (match, capture) = AnimationMatch(bot);
            var quest = InstallHistoryQuest(match, id);
            var game = match.Game;
            int cursor = capture.Packets.Count;
            if (id == "TLC_446")
            {
                var card = EngineEntity.FromCard(quest.Controller, Cards.FromId("CS2_182"), zone: quest.Controller.HandZone);
                TemporaryRules.Make(card);
                FlushFieldHistory(match);
                cursor = capture.Packets.Count;
                InvokeProcess(match, quest.Controller.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id));
            }
            else
            {
                game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, quest.Controller.Hero.Id, "", -1, 0));
                if (id == "TLC_433") QuestRules.CorpsesSpent(quest.Controller, 1);
                else QuestRules.Advance(quest);
                game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            }
            FlushFieldHistory(match);
            var h = History(capture, cursor);
            var starts = h.Where(e => e.HasPowerStart && e.PowerStart.Source == quest.Id).Select(e => e.PowerStart).ToArray();
            Check(starts.Length == 2 && starts[0].EffectIndex == 1 && !starts[0].ShowInHistory
                && starts[1].EffectIndex == 0 && starts[1].TriggerKeyword == (int)GameTag.QUEST && starts[1].ShowInHistory,
                id + ": direct Advance splits progress/completion inside caller");
            string reward = Cards.FromAssetId(quest.Card[GameTag.QUEST_REWARD_DATABASE_ID]).Id;
            Check(h.Any(e => e.HasFullEntity && e.FullEntity.Name == reward), id + ": completion names fixed reward in both seats");
            Check(!NativeAdmission.ValidatePackets(capture.HistorySnapshots().Select(p => (IReadOnlyList<PowerHistoryData>)p.List)).Any(),
                id + ": admission stays clean");
            capture.SerializeAll();
        }
    }
}
