using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MagmaHoundProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_953", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRush && source.IsRace(Race.BEAST) && source.IsRace(Race.ELEMENTAL)
            && source.Card.Races.Count == 2, "printed Rush and both minion types");
        var victim = Summon(p.Opponent, "CS2_171");
        var clone = g.CloneExact(history: true);
        Attack(clone, (Minion)clone.IdEntityDic[source.Id], (Minion)clone.IdEntityDic[victim.Id]);
        Core2026Proof.Check(clone.Player2.Hero.Damage == 5 && p.Opponent.Hero.Damage == 0 && victim.Zone == p.Opponent.BoardZone,
            "clone attack and random split isolated");
        var shieldedClone = g.CloneExact(history: true);
        var shieldedSource = (Minion)shieldedClone.IdEntityDic[source.Id];
        var shieldedVictim = (Minion)shieldedClone.IdEntityDic[victim.Id];
        var shieldedHero = shieldedClone.Player2.Hero;
        shieldedHero.HasDivineShield = true;
        Attack(shieldedClone, shieldedSource, shieldedVictim);
        Core2026Proof.Check(shieldedSource.Health == 7 && shieldedVictim.Zone == shieldedClone.Player2.GraveyardZone
            && shieldedClone.Player2.BoardZone.Count == 0, "surviving lethal attack leaves only enemy hero");
        Core2026Proof.Check(!shieldedHero.HasDivineShield && shieldedHero.Damage == shieldedSource.AttackDamage - 1,
            "separate pings consume hero Divine Shield then deal exactly Attack minus one damage");
        Attack(g, source, victim);
        Core2026Proof.Check(source.Health == 7 && p.Opponent.Hero.Damage == 5 && victim.Zone == p.Opponent.GraveyardZone,
            "surviving lethal attack splits five damage; dead victim excluded");
        End(g); End(g);
        Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 10, "hero attack causes no extra damage");
        End(g);
        var attacker = Summon(p.Opponent, "CS2_171"); Attack(g, attacker, source);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 10, "opponent attack into source does not trigger");
        End(g);
        Generic.AddEnchantmentBlock(g, SabberStoneCore.Model.Cards.FromId("CS2_011o"), p.Hero, source);
        victim = Summon(p.Opponent, "CS2_171"); Attack(g, source, victim);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 17, "split uses current buffed Attack seven");
        End(g); End(g);
        Core2026Proof.Check(source.AttackDamage == 5, "temporary Attack buff expires");
        source.Silence();
        victim = Summon(p.Opponent, "CS2_171"); Attack(g, source, victim);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 17 && source.ActivatedTrigger == null, "Silence removes after-attack trigger");
        Kill(g, source);
        Core2026Proof.Check(source.ActivatedTrigger == null, "source leaving play retains no trigger");
        source = (Minion)Play(g, p, id); victim = Summon(p.Opponent, "CS2_200");
        victim.AttackDamage = 8;
        Attack(g, source, victim);
        Core2026Proof.Check(source.Zone == p.GraveyardZone && p.Opponent.Hero.Damage == 17, "fatal retaliation prevents trigger");
        ClearBoard(g, p.Opponent);
        source = (Minion)Play(g, p, id);
        victim = Summon(p.Opponent, "CS2_182"); victim.BaseHealth = 30; victim.AttackDamage = 0;
        var other = Summon(p.Opponent, "CS2_182"); other.BaseHealth = 30;
        int before = p.Opponent.Hero.Damage;
        Attack(g, source, victim);
        Core2026Proof.Check(victim.Damage + other.Damage + p.Opponent.Hero.Damage - before == 10 && source.Damage == 0,
            "nonlethal minion attack splits exactly current Attack among enemy minions and hero only");
    });
}
