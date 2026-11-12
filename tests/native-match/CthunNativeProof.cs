using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    private static void CthunNativeProof()
    {
        foreach(bool bot in new[] { false,true })
        {
        var (match,capture) = AnimationMatch(botTurn:bot);
        var game = match.Game; var owner = game.CurrentPlayer;
        foreach(var old in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(old));
        foreach(var old in owner.Opponent.BoardZone.ToArray()) owner.Opponent.SetasideZone.Add(owner.Opponent.BoardZone.Remove(old));
        owner.BaseMana = 10; owner.UsedMana = 0;
        var source = EngineEntity.FromCard(owner,Cards.FromId("BE_778"),zone:owner.HandZone);
        FlushFieldHistory(match); int cursor = capture.Packets.Count;
        InvokeProcess(match,PlayCardTask.Spell(owner,source)); FlushFieldHistory(match);
        if (bot)
            Check(History(capture,cursor).Where(e => owner.Choice.Choices.Contains(SnapshotEntity(e)))
                .All(e => SnapshotName(e) == ""),"bot's unselected ability offers stay private");
        int first = owner.Choice.Choices.Single(id => game.IdEntityDic[id].Card.Id == "BE_013t4");
        InvokeProcess(match,ChooseTask.Pick(owner,first)); FlushFieldHistory(match);
        Check(owner.BoardZone.Count == 2 && owner.Choice.Choices.Count == 3,"native Watch Over completes first effect before second prompt");
        int second = owner.Choice.Choices.Single(id => game.IdEntityDic[id].Card.Id == "BE_013t1");
        int priorDamage = owner.Opponent.Hero.Damage;
        InvokeProcess(match,ChooseTask.Pick(owner,second)); FlushFieldHistory(match);
        Check(owner.Choice == null && owner.Opponent.Hero.Damage == priorDamage + 7,"native second choice resolves exact base Wrath");
        var entries = History(capture,cursor);
        Check(entries.Any(e => e.HasFullEntity && SnapshotName(e) == "BE_013t5"),"native Fanatic actor uses exported identity");
        Check(entries.Any(e => SnapshotEntity(e) == first && SnapshotName(e) == "BE_013t4")
            && entries.Any(e => SnapshotEntity(e) == second && SnapshotName(e) == "BE_013t1"),"both players expose fixed ability actors used as native effect sources: bot=" + bot + " first=" + first + " second=" + second + " snapshots=" + string.Join(",",entries.Where(e => SnapshotEntity(e)==first || SnapshotEntity(e)==second).Select(e => SnapshotEntity(e)+":"+SnapshotName(e))) + " marker=" + game.IdEntityDic[first][Generic.PrivateDecisionOfferTag]);
        Check(entries.Any(e => e.HasPowerStart && e.PowerStart.Source == first) && entries.Any(e => e.HasPowerStart && e.PowerStart.Source == second),"selected ability actors source native POWER blocks");
        Check(entries.Count(e => e.HasMetaData && (int)e.MetaData.Type == 1) == 7,"Wrath projects seven separate damage packets through its native ability POWER");
        Check(!entries.Any(e => e.HasTagChange && CthunRules.IsInternalTag((GameTag)e.TagChange.Tag)),"C'Thun private implementation tags never reach native deltas");
        foreach(var entity in owner.BoardZone.Cast<SabberStoneCore.Model.Entities.IPlayable>().Append(source))
            Check(!match.SnapshotVisibleTransportTags(entity).Keys.Any(t => CthunRules.IsInternalTag((GameTag)t)),"C'Thun private implementation tags never reach snapshots");
        capture.SerializeAll();
        }
        Console.WriteLine("PASS native C'Thun: sequential choices, base effects, source actors/damage packets, private tags");
    }
}
