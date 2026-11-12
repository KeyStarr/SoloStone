using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

// After-attack effects fire before death is processed, so an attacker that dies in this very combat still triggers
// them. SI:7 Supplier (CAP_003) and Rehgar (CORE_CATA_004) wrongly required the attacker to survive (found in the
// attack-trigger sweep, see AttackTriggerSweepProof); the other cards here are the same lethal-attack case already
// working. JAIL_030 and FIR_953 are the exceptions: their text requires survival, so they must NOT fire.
internal static class AttackLethalTriggerProof
{
    [GroupProof("attack-lethal-triggers")]
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (string id in new[] { "CAP_003", "CORE_CATA_004", "CORE_CFM_344", "TLC_247", "TSC_029", "TLC_840", "JAIL_030", "FIR_953" })
        {
            try
            {
                var g = B15DrawReplacementProof.NewGame(); var p = g.Player1;
                if (id == "CORE_CFM_344")
                    for (int i = 0; i < 2; i++) Entity.FromCard(p, Cards.FromId("EX1_506"), zone: p.DeckZone);
                var source = Summon(p, id); source.Damage = source.BaseHealth - 1;
                var victim = Summon(p.Opponent, "CS2_182");
                victim.BaseHealth = id is "CORE_CFM_344" or "TLC_247" ? 1 : 100;
                int hand = p.HandZone.Count, face = p.Opponent.Hero.Damage;
                Attack(g, source, victim);
                Core2026Proof.Check(source.Zone == p.GraveyardZone, "attacker actually dies in combat");
                bool effect = id switch {
                    "CAP_003" => p.HandZone.Count == hand + 1,
                    "CORE_CATA_004" => p.HandZone.Count(c => c.Card.Id == "CORE_EX1_238") == 1,
                    "CORE_CFM_344" => p.BoardZone.Count(m => m.Card.Id == "EX1_506") == 2,
                    "TLC_247" => p.HandZone.Any(c => c.Card.Id == "CS2_182"),
                    "TSC_029" => p.Opponent.Hero.Damage == face + 1,
                    "TLC_840" => p.Opponent.Hero.Damage == face + 2,
                    "JAIL_030" => p.HandZone.Count == hand,
                    "FIR_953" => p.Opponent.Hero.Damage == face,
                    _ => false
                };
                Core2026Proof.Check(effect, "after-attack effect / explicit-survival exception");
                Console.WriteLine("LETHAL-ATTACK " + id + " PASS");
            }
            catch (Exception error) { failures.Add(id + ": " + error.Message); Console.WriteLine("LETHAL-ATTACK " + id + " FAIL " + error.Message); }
        }
        Core2026Proof.Check(failures.Count == 0, string.Join("\n", failures));
    }
}
