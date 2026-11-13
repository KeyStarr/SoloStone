using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

// Issue #86: Explosive Trap fires when the hero is attacked, before the hit. A 1- or 2-Health attacker dies, the attack is
// cancelled and the hero takes nothing. The history the client draws from must show the trap first: since #32 the attack's
// ATTACKING and DEFENDING tags are sent only after the attack triggers resolve, and never for a cancelled attack.
internal static class ExplosiveTrapAttackOrderProof
{
    [GroupProof("explosive-trap-attack-order", ProofKind.Cards)]
    internal static void Run()
    {
        foreach (string trapId in new[] { "EX1_610", "CORE_EX1_610" })
        foreach (var (attackerId, dies) in new[] { ("CS2_189", true), ("CS2_172", true), ("CS2_182", false) })
        {
            var game = DeathKnightC1Proof.NewGame(3); var attackerOwner = game.Player1; var defender = game.Player2;
            Core2026Proof.End(game);
            var trap = Core2026Proof.Play(game, defender, trapId);
            Core2026Proof.End(game);
            Core2026Proof.Check(trap.Zone == defender.SecretZone, trapId + " is up");
            var attacker = Core2026Proof.Summon(attackerOwner, attackerId);
            int health = defender.Hero.Health, historyStart = game.PowerHistory.Full.Count;
            Core2026Proof.Attack(game, attacker, defender.Hero);
            var history = game.PowerHistory.Full.Skip(historyStart).ToList();
            Core2026Proof.Check(trap.Zone != defender.SecretZone, trapId + " fired on the attack");
            int trapDamage = Index(history, e => e is PowerHistoryTagChange t && t.EntityId == attacker.Id && t.Tag == GameTag.DAMAGE && t.Value > 0);
            Core2026Proof.Check(trapDamage >= 0, trapId + " damaged the attacker");
            int attacking = Index(history, e => e is PowerHistoryTagChange t && t.EntityId == attacker.Id && t.Tag == GameTag.ATTACKING && t.Value == 1);
            int heroHit = Index(history, e => e is PowerHistoryTagChange t && t.EntityId == defender.Hero.Id && t.Tag == GameTag.DAMAGE && t.Value > 0);
            if (dies)
            {
                Core2026Proof.Check(attacker.IsDead || attacker.Zone == attackerOwner.GraveyardZone, attackerId + " dies to " + trapId);
                Core2026Proof.Check(defender.Hero.Health == health && heroHit < 0, "hero takes no attack damage from " + attackerId);
                Core2026Proof.Check(attacking < 0, "a cancelled attack never tells the client " + attackerId + " is attacking");
            }
            else
            {
                Core2026Proof.Check(defender.Hero.Health == health - attacker.AttackDamage, "a surviving attacker still hits the hero");
                Core2026Proof.Check(attacking > trapDamage && heroHit > trapDamage, "the trap resolves before ATTACKING and the hit");
            }
        }
    }

    private static int Index(List<IPowerHistoryEntry> history, Func<IPowerHistoryEntry, bool> match)
    {
        for (int i = 0; i < history.Count; i++) if (match(history[i])) return i;
        return -1;
    }
}
