using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz; retain existing current-text power.
internal static class CoreSiphonSoulProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_309", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); var friend = Summon(p, "CS2_182");
        TargetRules(g, p, id, enemy, friend, enemyOnly: false); PoolPilotProof.EmptyHand(p);
        p.Hero.Damage = 7; Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && p.Hero.Damage == 4 && p.Opponent.Hero.Damage == 0 && friend.Zone == p.BoardZone, "destroy only target and heal only own hero by three");
        p.Hero.Damage = 1; Play(g, p, id, friend);
        Core2026Proof.Check(friend.Zone.Type == Zone.GRAVEYARD && p.Hero.Health == 30, "friendly target allowed; healing capped at maximum");
        var shield = Summon(p.Opponent, "EX1_008"); Play(g, p, id, shield);
        Core2026Proof.Check(shield.Zone.Type == Zone.GRAVEYARD && p.Hero.Health == 30, "destroy bypasses Divine Shield; no overheal");
    }, legacyPoolMember: true);
}
