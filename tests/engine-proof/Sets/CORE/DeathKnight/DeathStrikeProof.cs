using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static DeathKnightC1Proof;
using static PoolPilotProof;

internal static class DeathStrikeProof
{
    internal static void Run() => DeathKnightC1Proof.Case("RLK_024", (g, p, id) => {
        var source = Add(p, id);
        Core2026Proof.Check(!HasPlay(p, source), "no minions: cannot play");
        var enemy = Summon(p.Opponent, "CS2_200"); var friendly = Summon(p, "CS2_182");
        var options = p.Options().Where(t => t.Source?.Id == source.Id).ToArray();
        Core2026Proof.Check(options.Length == 2 && options.All(t => t.Target is Minion), "either side minion only, heroes excluded");
        p.Hero.Damage = 12; PlayEntity(g, p, source, enemy);
        Core2026Proof.Check(enemy.Damage == 6 && p.Hero.Damage == 6, "six damage and Lifesteal healing");
        var shield = Summon(p.Opponent, "CS2_101t"); shield.HasDivineShield = true;
        Play(g, p, id, shield);
        Core2026Proof.Check(shield.Damage == 0 && p.Hero.Damage == 6, "prevented damage gives no Lifesteal");
        var spellDamage = Summon(p, "EX1_012");
        LocationProofs.Location(p); while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Resume(g, next => Play(next, next.Player1, id, (Minion)next.IdEntityDic[friendly.Id]));
        Core2026Proof.Check(friendly.Zone.Type == Zone.GRAVEYARD && p.Hero.Damage == 0, "spell damage and overkill Lifesteal on friendly target, mixed full board");
    });
}
