using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Existing legacy generation and rebalanced powers retained (owner decision, 2026-10-21).
internal static class ObsidianStatueProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_ICC_214", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var enemy = Summon(p.Opponent, "CS2_200");
        var friend = Summon(p, "CS2_182"); p.Hero.Damage = 12;
        Core2026Proof.Check(source.HasTaunt && source.HasLifeSteal && source.HasDeathrattle && source.AttackDamage == 4 && source.Health == 8, "printed 4/8 with all keywords");
        Attack(g, source, enemy); Core2026Proof.Check(enemy.Damage == 4 && p.Hero.Damage == 8, "combat damage heals controller");
        p.Opponent.Hero.Armor = 4; Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 0 && p.Opponent.Hero.Damage == 0 && p.Hero.Damage == 4, "Lifesteal includes armor damage");
        var shield = Summon(p.Opponent, "GVG_085"); Attack(g, source, shield);
        Core2026Proof.Check(!shield.HasDivineShield && p.Hero.Damage == 4, "prevented damage does not heal");
        PoolPilotProof.Resume(g, next => {
            End(next); Kill(next, (Minion)next.IdEntityDic[source.Id]);
            Core2026Proof.Check(next.Player2.BoardZone.Count == 1 && next.Player1.BoardZone.Single().Id == friend.Id, "Deathrattle destroys exactly one random enemy, never friend, on opponent turn");
        });
        End(g); ParkBoard(p.Opponent); source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Single().Id == friend.Id && p.Opponent.BoardZone.Count == 0, "no enemy Deathrattle does nothing to friends");
        source = (Minion)Play(g, p, id); enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, "EX1_144", source);
        Core2026Proof.Check(source.Zone.Type == Zone.HAND && enemy.Zone.Type == Zone.PLAY, "leaving play without death does not destroy enemy");
        source = (Minion)Play(g, p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(enemy.Zone.Type == Zone.PLAY && !source.HasLifeSteal && !source.HasTaunt, "Silence removes Deathrattle and keywords");
    }, legacyPoolMember: true);
}
