using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz; retain existing current-text power.
internal static class SpiritBombProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BOT_222", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); var friend = Summon(p, "CS2_182");
        TargetRules(g, p, id, enemy, friend, enemyOnly: false); PoolPilotProof.EmptyHand(p);
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Damage == 4 && p.Hero.Damage == 4 && p.Opponent.Hero.Damage == 0 && friend.Damage == 0, "four damage to target and own hero only");
        Play(g, p, id, friend); Core2026Proof.Check(friend.Damage == 4 && p.Hero.Damage == 8, "friendly minion may be targeted");
        Summon(p, "CS2_142"); var shield = Summon(p.Opponent, "EX1_008");
        Play(g, p, id, shield);
        Core2026Proof.Check(!shield.HasDivineShield && shield.Damage == 0 && p.Hero.Damage == 13, "Spell Damage scales both hits; shield prevents only target hit");
        ParkBoard(p); ParkBoard(p.Opponent); var spell = Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id), "no minion means no legal play");
    }, legacyPoolMember: true);
}
