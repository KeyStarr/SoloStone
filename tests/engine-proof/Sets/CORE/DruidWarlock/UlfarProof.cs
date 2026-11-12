using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_UlfarProof
{
    internal static void Run() => Unpinned_CostMemory();
    // Owner-approved working assumptions: docs/design/costmemory.md.
    private static void Unpinned_CostMemory() => StandardSetsProof.Case("CORE_CATA_006", (g, p, id) => {
        var body = Summon(p, "CS2_182"); body.Cost = 2;
        Play(g, p, id);
        Core2026Proof.Check(body.HasDeathrattle && body.AppliedEnchantments.Any(e => e.Card.Id == "CORE_CATA_006e"), "granted real Deathrattle");
        body.Cost = 3;
        int position = body.ZonePosition;
        Kill(g, body);
        var result = p.BoardZone.Single(m => m.Card.Id != id);
        Core2026Proof.Check(result.Card.Cost == 3 && result.ZonePosition == position, "modified death Cost, not grant-time, and original slot");
        g.DeathProcessingAndAuraUpdate();
        ParkBoard(p);
        body = Summon(p, "CS2_182"); Play(g, p, id);
        body.Silence(); long before = g.Random.DrawCount; Kill(g, body);
        Core2026Proof.Check(p.BoardZone.Count == 1 && g.Random.DrawCount == before, "Silence removes granted Deathrattle");
        ParkBoard(p);
        body = Summon(p, "CS2_182"); Play(g, p, id); body.Cost = 3;
        var branch = Game.LoadSnapshot(g.SaveSnapshot());
        Kill(branch, (Minion)branch.IdEntityDic[body.Id]); branch.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(branch.Player1.BoardZone.Any(m => m.Card.Cost == 3) && body.Zone == p.BoardZone,
            "granted Deathrattle and future cost sample survive independent snapshot");
        ParkBoard(p);
        var dormant = Summon(p, "CORE_BT_156");
        Core2026Proof.Check(dormant.Untouchable, "Dormant recipient control");
        Play(g, p, id);
        Core2026Proof.Check(dormant.AppliedEnchantments == null || dormant.AppliedEnchantments.All(e => e.Card.Id != "CORE_CATA_006e"),
            "Dormant minions cannot receive Ulfar's grant");
        ParkBoard(p);
    });
}
