using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Existing legacy generation and rebalanced powers retained (owner decision, 2026-10-21).
internal static class TirionFordringProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_383", (g, p, id) => {
        Play(g, p, "CS2_091");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 8 && source.Health == 8 && source.HasDivineShield && source.HasTaunt && source.HasDeathrattle, "8/8 with all printed keywords");
        Play(g, p, "CS2_008", source); Core2026Proof.Check(source.Damage == 0 && !source.HasDivineShield, "Divine Shield prevents first hit");
        PoolPilotProof.Resume(g, next => {
            End(next); Kill(next, (Minion)next.IdEntityDic[source.Id]);
            var weapon = next.Player1.Hero.Weapon;
            Core2026Proof.Check(weapon.Card.Id == "EX1_383t" && weapon.Card.AssetId == 1730 && weapon.AttackDamage == 5 && weapon.Durability == 3 && next.Player2.Hero.Weapon == null,
                "death on opponent turn replaces friendly weapon with exact 5/3 Ashbringer");
        });
        End(g); source = (Minion)Play(g, p, id);
        Play(g, p, "CS2_091"); Play(g, p, "EX1_144", source);
        Core2026Proof.Check(source.Zone.Type == Zone.HAND && p.Hero.Weapon.Card.Id == "CS2_091", "leaving play without death does not trigger Deathrattle");
        source = (Minion)Play(g, p, id); source.Silence();
        Play(g, p, "CS2_091"); Kill(g, source);
        Core2026Proof.Check(p.Hero.Weapon.Card.Id == "CS2_091", "Silence removes Deathrattle");
    }, legacyPoolMember: true);
}
