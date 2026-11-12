using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class GnomeMuncherProof
{
    internal static void Run() => DeathKnightC2Proof.Case("RLK_720", (g, p, id) => {
        p.Hero.Damage = 10;
        var source = (Minion)Play(g, p, id); source.IsFrozen = true;
        Core2026Proof.Check(source.AttackDamage == 5 && source.Health == 6 && source.HasTaunt && source.HasLifeSteal,
            "5/6 Taunt Lifesteal");
        var target = Summon(p.Opponent, "CS2_182"); target.Damage = 4; target.HasStealth = true;
        var taunt = Summon(p.Opponent, "CS2_182"); taunt.HasTaunt = true;
        var dormant = Summon(p.Opponent, "CS2_182"); dormant[GameTag.DORMANT] = 1; dormant.Damage = 4;
        LocationProofs.Location(p.Opponent);
        DeathKnightC2Proof.Continue(g, copy => {
            End(copy); var cp = copy.Player1; var attacker = (Minion)copy.IdEntityDic[source.Id];
            Core2026Proof.Check(copy.IdEntityDic[target.Id].Zone == cp.Opponent.GraveyardZone && attacker.Damage == 4
                && cp.Hero.Damage == 5 && ((Minion)copy.IdEntityDic[taunt.Id]).Damage == 0,
                "fresh frozen minion forced-attacks lowest Health through Stealth/Taunt, takes retaliation and heals full damage");
            End(copy); Core2026Proof.Check(cp.Hero.Damage == 5, "no opponent end-turn attack");
            attacker.Silence(); copy.DeathProcessingAndAuraUpdate(); End(copy);
            Core2026Proof.Check(attacker.Damage == 4 && cp.Opponent.Hero.Damage == 0, "Silence removes trigger");
        });
        End(g); StandardSetsProof.ClearBoard(g, p.Opponent); source = (Minion)Play(g, p, id);
        End(g);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 5 && p.Hero.Damage == 0, "sole enemy hero attacked and Lifesteal heals");
        End(g); p.Opponent.Hero.Damage = 25; p.Opponent.Hero.Armor = 20;
        var tied = Summon(p.Opponent, "CS2_182");
        DeathKnightC2Proof.Continue(g, copy => {
            long draws = copy.Random.CaptureState().DrawCount;
            End(copy);
            bool heroHit = copy.Player2.Hero.Armor == 15;
            Core2026Proof.Check((heroHit && copy.IdEntityDic[tied.Id].Zone == copy.Player2.BoardZone)
                || (!heroHit && copy.IdEntityDic[tied.Id].Zone == copy.Player2.GraveyardZone), "tie hits exactly one lowest-Health enemy");
            Core2026Proof.Check(copy.Random.CaptureState().DrawCount > draws, "hero/minion tie consumes selection RNG");
        });
    });
}
