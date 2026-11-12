using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class WagglePickProof
{
    internal static void Run() => Case("CORE_DAL_720", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); friend.IsExhausted = true; var source = Play(g, p, id);
        Play(g, p, "CS2_082");
        Core2026Proof.Check(friend.Zone.Type == Zone.HAND && friend.Cost == 2, "Pick deathrattle returns and discounts by 2");
        var changes = g.PowerHistory.Last.OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>();
        Core2026Proof.Check(changes.Any(change => change.EntityId == friend.Id && change.Tag == GameTag.COST && change.Value == 2)
            && changes.Any(change => change.EntityId == friend.Id && change.Tag == GameTag.EXHAUSTED && change.Value == 0), "bounce and discount reach transport");
    });
}
