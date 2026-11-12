using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class LingeringSpiritProof
{
    internal static void Run() => StandardSetsProof.Case("CAP_803", (g, p, id) => {
        foreach (var row in new[] { (missing: 5, doubled: false, expectedHeal: 3, expectedDamage: 0),
            (missing: 1, doubled: false, expectedHeal: 1, expectedDamage: 2),
            (missing: 0, doubled: false, expectedHeal: 0, expectedDamage: 3),
            (missing: 1, doubled: true, expectedHeal: 1, expectedDamage: 5) })
        {
            var branch = g.CloneExact(history: true); var owner = branch.Player1;
            owner.Hero.Damage = row.missing;
            if (row.doubled) owner.ControllerAuraEffects[GameTag.ALL_HEALING_DOUBLE] = 1;
            var spirit = Summon(owner, id);
            Core2026Proof.Check(spirit.Health == 3 && spirit.AttackDamage == 3 && spirit[GameTag.REBORN] == 1 && spirit.IsRace(Race.UNDEAD), "printed Spirit stats and Reborn");
            Kill(branch, spirit);
            Core2026Proof.Check(owner.Hero.Damage == row.missing - row.expectedHeal && owner.Opponent.Hero.Damage == row.expectedDamage, "actual healing and excess account for healing multiplier");
            Core2026Proof.Check(owner.BoardZone.Count == 1 && owner.BoardZone[0].Card.Id == id && owner.BoardZone[0].Health == 1 && owner.BoardZone[0][GameTag.REBORN] == 0, "normal one-health Reborn after Deathrattle");
        }
        bool poisonHit = false;
        for (int attempt = 0; attempt < 12 && !poisonHit; attempt++)
        {
            var branch = g.CloneExact(history: true);
            for (int i = 0; i < attempt; i++) branch.Random.Next();
            var victim = Summon(branch.Player2, "CS2_200");
            var spirit = Summon(branch.Player1, id); spirit.Poisonous = true; Kill(branch, spirit);
            poisonHit = victim.Damage > 0;
            if (poisonHit) Core2026Proof.Check(victim.Zone.Type == Zone.GRAVEYARD, "Poisonous Spirit excess damage destroys minion");
        }
        Core2026Proof.Check(poisonHit, "seeded excess damage exercises enemy minion");
        var converted = g.CloneExact(history: true); converted.Player1.RestoreToDamage = true;
        Kill(converted, Summon(converted.Player1, id));
        Core2026Proof.Check(converted.Player1.Hero.Damage == 3 && converted.Player2.Hero.Damage == 0, "converted healing causes no excess healing");
        var shielded = g.CloneExact(history: true); shielded.Player2.Hero.HasDivineShield = true;
        Kill(shielded, Summon(shielded.Player1, id));
        Core2026Proof.Check(shielded.Player2.Hero.Damage == 0 && !shielded.Player2.Hero.HasDivineShield, "excess damage respects hero shield");
        var silenced = Summon(p, id); silenced.Silence(); Kill(g, silenced);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 0 && p.BoardZone.Count == 0, "Silence removes Deathrattle and Reborn");
    });
}
