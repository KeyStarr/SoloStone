using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AfflictedDevastatorProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_459", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); var small = Summon(p, "EX1_506a");
        var enemy = Summon(p.Opponent, "CS2_182"); var shield = Summon(p, "EX1_008"); shield.HasDivineShield = true;
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 6 && source.Health == 6 && source.Damage == 0 && source.Card.GetRawRace() == Race.DRAGON, "source excluded from friendly damage");
        Core2026Proof.Check(friendly.Damage == 3 && small.Zone.Type == Zone.GRAVEYARD && shield.Damage == 0 && !shield.HasDivineShield && enemy.Damage == 0, "Battlecry damages only other friendlies and respects shield");
        var enemySmall = Summon(p.Opponent, "EX1_506a"); Kill(g, source);
        Core2026Proof.Check(enemy.Damage == 3 && enemySmall.Zone.Type == Zone.GRAVEYARD && friendly.Damage == 3 && p.Opponent.Hero.Damage == 0, "Deathrattle damages only enemy minions");
        source = Summon(p, id); source.Silence(); Kill(g, source); Core2026Proof.Check(enemy.Damage == 3, "silence removes Deathrattle");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "empty boards leave heroes untouched");
    });
}
