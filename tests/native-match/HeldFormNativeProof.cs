using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    [GroupProof("b15-held-form-native", ProofKind.Native)]
    private static void HeldFormNativeProof()
    {
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var game = match.Game; var owner = game.CurrentPlayer;
            foreach (var c in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(c));
            var demise = Generic.ChangeEntityBlock(owner, Generic.DrawCard(owner, Cards.FromId("GAME_005")), Cards.FromId("CORE_RLK_567"), true);
            var informant = Generic.ChangeEntityBlock(owner, Generic.DrawCard(owner, Cards.FromId("CS2_182")), Cards.FromId("CATA_614"), true);
            var hidden = Generic.ChangeEntityBlock(owner.Opponent, Generic.DrawCard(owner.Opponent, Cards.FromId("CS2_182")), Cards.FromId("CATA_614"), true);
            FlushFieldHistory(match);
            int selected = informant[GameTag.TAG_SCRIPT_DATA_NUM_1];
            var shown = ReplayNativeEntityTags(NativeHistory(capture), informant.Id);
            Check(bot ? !shown.ContainsKey(2) : shown.GetValueOrDefault(2) == selected,
                "Informant selected class uses native script tag 2 only for visible owner");
            Check(bot || !ReplayNativeEntityTags(NativeHistory(capture), hidden.Id).ContainsKey(2), "opponent selected class private");
            int slot = demise.ZonePosition;
            var coin = Generic.DrawCard(owner, Cards.FromId("GAME_005"));
            InvokeProcess(match, PlayCardTask.Spell(owner, coin)); FlushFieldHistory(match);
            Check(demise.Card.Id == "GAME_005" && demise.ZonePosition == slot, "real cast changes held identity in place");
            var entries = NativeHistory(capture);
            if (bot)
            {
                Check(entries.Where(e => SnapshotEntity(e) == demise.Id || SnapshotEntity(e) == informant.Id)
                    .All(e => SnapshotName(e) == ""), "hidden held forms never publish identities");
                Check(!entries.Any(e => SnapshotName(e) == "RLK_567e2"), "hidden recurrence identity never published");
            }
            else
                Check(entries.Any(e => e.HasChangeEntity && e.ChangeEntity.Entity == demise.Id && e.ChangeEntity.Name == "GAME_005"),
                    "owner receives retail CHANGE_ENTITY identity");
            InvokeProcess(match, PlayCardTask.Minion(owner, informant)); FlushFieldHistory(match);
            Check(owner.Choice != null && owner.Choice.Choices.Count == 3, "selected-class native Discover cannot stall");
            int[] offers = owner.Choice.Choices.ToArray();
            Check(offers.All(id => (int)game.IdEntityDic[id].Card.Class == selected), "native Discover matches displayed class");
            InvokeProcess(match, ChooseTask.Pick(owner, offers[0])); FlushFieldHistory(match);
            Check(owner.Choice == null && game.IdEntityDic[offers[0]].Zone == owner.HandZone, "native answer delivers selected spell");
            if (bot)
                Check(NativeHistory(capture).Where(e => offers.Contains(SnapshotEntity(e))).All(e => SnapshotName(e) == ""),
                    "opponent offers and generated hand identity remain private");
            entries = NativeHistory(capture);
            foreach (var e in entries)
            {
                Check(!e.HasTagChange || !HandProgressRules.IsInternalTag((GameTag)e.TagChange.Tag), "internal tag delta filtered");
                var entity = e.HasFullEntity ? e.FullEntity : e.HasShowEntity ? e.ShowEntity : e.HasChangeEntity ? e.ChangeEntity : null;
                Check(entity == null || entity.Tags.All(t => !HandProgressRules.IsInternalTag((GameTag)t.Name)), "internal snapshot tags filtered");
            }
            capture.SerializeAll();
        }
        Console.WriteLine("PASS held forms: owner CHANGE/text class and negative hidden identity/attachment/tag privacy");
    }
}
