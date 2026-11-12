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
internal static class SwampLeechProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_GIL_558", (g, p, id) => {
        p.Hero.Damage = 8; p.Opponent.Hero.Armor = 5;
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 2 && source.Health == 1 && source.HasLifeSteal && source.IsRace(Race.BEAST), "2/1 Lifesteal Beast");
        PoolPilotProof.Resume(g, next => {
            Attack(next, (Minion)next.IdEntityDic[source.Id], next.Player2.Hero);
            Core2026Proof.Check(next.Player1.Hero.Damage == 6 && next.Player2.Hero.Armor == 3 && next.Player2.Hero.Damage == 0, "damage absorbed by Armor still heals two");
        });
        source.NumAttacksThisTurn = 0; var shield = Summon(p.Opponent, "GVG_085"); Attack(g, source, shield);
        Core2026Proof.Check(p.Hero.Damage == 6 && !shield.HasDivineShield, "prevented shield damage does not heal");
        source = Summon(p, id); source.Silence(); ParkBoard(p.Opponent); Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(!source.HasLifeSteal && p.Hero.Damage == 6, "silenced source does not heal");
        source = Summon(p, id); p.Hero.Damage = 1; p.Opponent.Hero.Armor = 0; Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 2, "healing caps at full Health; armor and Health damage both count");
    }, legacyPoolMember: true);
}
