using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, current text/tags; unchanged legacy power.
internal static class PrimordialDrakeProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_UNG_848", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var shield = Summon(p.Opponent, "GVG_085");
        var victim = Summon(p.Opponent, "CS2_171"); var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 4 && source.Health == 8 && source.Damage == 0 && source.HasTaunt && source.IsRace(Race.DRAGON), "4/8 Taunt Dragon excludes itself");
        Core2026Proof.Check(friend.Damage == 2 && enemy.Damage == 2 && !shield.HasDivineShield && shield.Damage == 0
            && victim.Zone == p.Opponent.GraveyardZone && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "two damage to other minions on both sides, shield prevention and lethal; heroes untouched");
        ParkBoard(p); ParkBoard(p.Opponent); source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.Health == 8 && p.BoardZone.Count == 1 && p.Opponent.BoardZone.Count == 0, "empty-board Battlecry does not damage source");
        source.Silence(); Core2026Proof.Check(!source.HasTaunt, "Silence removes Taunt");
    }, legacyPoolMember: true);
}
