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
internal static class WitchwoodGrizzlyProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_GIL_623", (g, p, id) => {
        PoolPilotProof.FillHand(p, 5);
        var empty = (Minion)Play(g, p, id);
        Core2026Proof.Check(empty.Health == 12 && empty.Damage == 0 && empty.HasTaunt, "empty enemy hand loses no Health; own hand ignored");
        PoolPilotProof.FillHand(p.Opponent, 4);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.Health == 8 && source.BaseHealth == 8 && source.Damage == 0
            && source.AppliedEnchantments.Any(e => e.Card.Id == "GIL_623e"), "four opposing cards reduce maximum Health, not damage");
        PoolPilotProof.EmptyHand(p.Opponent);
        Core2026Proof.Check(source.Health == 8, "Battlecry reduction does not track later hand changes");
        source.Silence(); Core2026Proof.Check(source.Health == 12 && !source.HasTaunt, "Silence restores maximum Health and removes Taunt");
        PoolPilotProof.FillHand(p.Opponent); source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.Health == 2 && source.BaseHealth == 2 && source.Damage == 0, "ten-card hand boundary leaves 2 Health");
        var summoned = Summon(p, id); Core2026Proof.Check(summoned.Health == 12, "summoned Grizzly skips Battlecry");
    }, legacyPoolMember: true);
}
