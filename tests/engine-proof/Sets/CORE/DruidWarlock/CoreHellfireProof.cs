using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz; retain existing current-text power.
internal static class CoreHellfireProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_062", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var location = LocationProofs.Location(p);
        Play(g, p, id);
        Core2026Proof.Check(friend.Damage == 3 && enemy.Damage == 3 && p.Hero.Damage == 3 && p.Opponent.Hero.Damage == 3, "three damage to every character on both sides");
        Core2026Proof.Check(location.Zone == p.BoardZone && location[GameTag.DAMAGE] == 0, "location is not a character");
        ParkBoard(p); ParkBoard(p.Opponent); Summon(p, "CS2_142");
        friend = Summon(p, "CS2_182"); enemy = Summon(p.Opponent, "CS2_182"); var shield = Summon(p.Opponent, "EX1_008");
        Play(g, p, id);
        Core2026Proof.Check(friend.Damage == 4 && enemy.Damage == 4 && p.Hero.Damage == 7 && p.Opponent.Hero.Damage == 7, "Spell Damage scales every hit");
        Core2026Proof.Check(!shield.HasDivineShield && shield.Damage == 0, "Divine Shield prevents damage");
    }, legacyPoolMember: true);
}
