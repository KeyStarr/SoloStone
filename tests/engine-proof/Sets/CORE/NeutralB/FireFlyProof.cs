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
internal static class FireFlyProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_UNG_809", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var token = (Minion)p.HandZone.Single();
        Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 2 && source.IsRace(Race.ELEMENTAL)
            && token.Card.Id == "UNG_809t1" && token.Cost == 1 && token.AttackDamage == 1 && token.Health == 2 && token.IsRace(Race.ELEMENTAL), "Battlecry adds exact 1-Mana 1/2 Flame Elemental");
        PlayEntity(g, p, token); Core2026Proof.Check(p.HandZone.Count == 0 && token.Zone == p.BoardZone, "token playable without another generated card");
        Summon(p, id); Core2026Proof.Check(p.HandZone.Count == 0, "summoning Fire Fly does not generate token");
        PoolPilotProof.FillHand(p, 9); Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Count(c => c.Card.Id == "UNG_809t1") == 1, "playing from full hand frees exactly one slot for token");
        Core2026Proof.Check(p.Opponent.HandZone.Count == 0, "token belongs only to friendly hand");
    }, legacyPoolMember: true);
}
