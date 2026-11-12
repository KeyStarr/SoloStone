using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RunawayBlackwingProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_YOP_034", (g, p, id) => {
        var hand = Add(p, id); var enemy = Summon(p.Opponent, "CS2_182");
        End(g); End(g); Core2026Proof.Check(enemy.Damage == 0, "hand source does not trigger");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRace(Race.DRAGON) && source.AttackDamage == 10 && source.Health == 10 && enemy.Damage == 0, "Dragon 10/10 has no battlecry damage");
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(clone.Player2.BoardZone.Count == 0 && enemy.Zone == p.Opponent.BoardZone, "clone turn-end damage leaves original untouched");
        End(g); Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone, "own turn end kills enemy minion");
        enemy = Summon(p.Opponent, "CS2_182"); End(g);
        Core2026Proof.Check(enemy.Damage == 0, "opponent turn end ignored");
        enemy.BaseHealth = 20; var other = Summon(p.Opponent, "CS2_182"); other.BaseHealth = 20;
        var randomClone = g.CloneExact(history: true); End(randomClone);
        Core2026Proof.Check(enemy.Damage == 0 && other.Damage == 0, "cloned random selection leaves original unchanged");
        End(g);
        Core2026Proof.Check(((Minion)randomClone.IdEntityDic[enemy.Id]).Damage == enemy.Damage
            && ((Minion)randomClone.IdEntityDic[other.Id]).Damage == other.Damage, "random selection uses cloned game RNG deterministically");
        Core2026Proof.Check(new[] { enemy.Damage, other.Damage }.Order().SequenceEqual(new[] { 0, 10 }), "exactly one random enemy minion takes ten");
        End(g); source.Silence(); g.DeathProcessingAndAuraUpdate();
        int damage = enemy.Damage + other.Damage; End(g); End(g);
        Core2026Proof.Check(enemy.Damage + other.Damage == damage, "Silence removes future turn-end damage");
        var removed = Summon(p, id); Kill(g, removed); End(g); End(g);
        Core2026Proof.Check(enemy.Damage + other.Damage == damage, "dead source does not trigger");
        ClearBoard(g, p.Opponent); Summon(p, id); int heroDamage = p.Opponent.Hero.Damage;
        End(g); Core2026Proof.Check(p.Opponent.Hero.Damage == heroDamage, "empty enemy board never damages hero");
    });
}
