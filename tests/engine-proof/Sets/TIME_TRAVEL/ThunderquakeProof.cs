using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ThunderquakeProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_215", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, id);
        var shock = p.HandZone.Single();
        Core2026Proof.Check(friendly.Damage == 1 && enemy.Damage == 1 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "one damage to both boards, heroes excluded");
        Core2026Proof.Check(shock.Card.Id == "TIME_218" && shock.Cost == 0 && shock.Card.Implemented, "exact playable Static Shock generated");
        PlayEntity(g, p, shock, enemy);
        Core2026Proof.Check(enemy.Damage == 2 && p.Hero.AttackDamage == 1, "generated card executes its own effect");
        Summon(p, "TIME_856"); Play(g, p, id);
        Core2026Proof.Check(friendly.Damage == 4 && enemy.Zone == p.Opponent.GraveyardZone, "spell power and lethal damage");
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        int hand = p.HandZone.Count; Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == hand + 1, "empty board still generates card");
        var cast = Add(p, id);
        while (p.HandZone.Count < 10) Add(p, "CS2_008");
        PlayEntity(g, p, cast);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Last().Card.Id == "TIME_218", "generated card fills last hand slot");
    });
}
