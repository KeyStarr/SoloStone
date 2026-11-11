using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CannonmasterProof
{
    internal static void Run() => StandardSetsProof.Case("CAP_107", (g, p, id) => {
        foreach (string protection in new[] { "none", "shield", "immune" })
        {
            var branch = g.CloneExact(history: true); var owner = branch.Player1;
            var cannon = Summon(owner, "CAP_107t"); cannon.Poisonous = true;
            var victim = Summon(owner.Opponent, "CS2_182");
            victim.HasDivineShield = protection == "shield"; victim.IsImmune = protection == "immune";
            for (int shot = 0; shot < 24; shot++)
            {
                End(branch); End(branch);
                owner.Opponent.Hero.Damage = 0;
                if (victim.Damage > 0 || victim.Zone.Type == Zone.GRAVEYARD
                    || protection == "shield" && !victim.HasDivineShield) break;
            }
            if (protection == "none")
                Core2026Proof.Check(victim.Zone.Type == Zone.GRAVEYARD && victim.Damage == 1, "Poisonous Cannoneer destroys after one actual damage");
            else
                Core2026Proof.Check(victim.Zone.Type == Zone.PLAY && victim.Damage == 0
                    && (protection != "shield" || !victim.HasDivineShield), "Poisonous shot respects " + protection);
        }
        var source = (Minion)Play(g, p, id); var token = (Minion)p.HandZone.Single();
        Core2026Proof.Check(source.IsRace(Race.DRAENEI) && source.IsRace(Race.PIRATE) && source.AttackDamage == 3 && source.Health == 1, "both printed minion types through card");
        Core2026Proof.Check(token.Card.Id == "CAP_107t" && token.Cost == 1 && token.AttackDamage == 1 && token.Health == 1, "Battlecry gives exact Cannoneer to hand");
        PlayEntity(g, p, token); int health = p.Opponent.Hero.Health; End(g);
        Core2026Proof.Check(p.Opponent.Hero.Health == health - 1 && p.Hero.Damage == 0, "own turn-end fires at enemy");
        health = p.Opponent.Hero.Health; End(g); Core2026Proof.Check(p.Opponent.Hero.Health == health, "opponent turn-end does not fire");
        var enemy = Summon(p.Opponent, "CS2_182"); var clone = g.CloneExact(history: true); End(clone); End(g);
        Core2026Proof.Check(clone.Player2.Hero.Damage == p.Opponent.Hero.Damage && ((Minion)clone.IdEntityDic[enemy.Id]).Damage == enemy.Damage && p.Opponent.Hero.Health + enemy.Health == health + 4, "random shot uses cloned game RNG and totals one");
        var deadBranch = g.CloneExact(history: true); Kill(deadBranch, (Minion)deadBranch.IdEntityDic[token.Id]);
        int total = deadBranch.Player2.Hero.Health + ((Minion)deadBranch.IdEntityDic[enemy.Id]).Health;
        End(deadBranch); End(deadBranch);
        Core2026Proof.Check(deadBranch.Player2.Hero.Health + ((Minion)deadBranch.IdEntityDic[enemy.Id]).Health == total, "unsilenced dead Cannoneer cannot fire");
        var dormantBranch = g.CloneExact(history: true); dormantBranch.IdEntityDic[token.Id][GameTag.DORMANT] = 1;
        End(dormantBranch); End(dormantBranch);
        Core2026Proof.Check(dormantBranch.Player2.Hero.Health + ((Minion)dormantBranch.IdEntityDic[enemy.Id]).Health == total, "Dormant Cannoneer cannot fire");
        token.Silence(); health = p.Opponent.Hero.Health + enemy.Health; End(g); End(g);
        Core2026Proof.Check(p.Opponent.Hero.Health + enemy.Health == health, "Silence removes turn-end firing");
        Kill(g, token); End(g); End(g); Core2026Proof.Check(p.Opponent.Hero.Health + enemy.Health == health, "source leaves play removes firing");
        var held = (Minion)Add(p, id); End(g); End(g); Core2026Proof.Check(!p.HandZone.Any(c => c.Card.Id == "CAP_107t"), "held Cannonmaster does not generate repeatedly");
        End(g); while (p.HandZone.Count < 10) Add(p, "CS2_182"); PlayEntity(g, p, held);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Count(c => c.Card.Id == "CAP_107t") == 1, "Battlecry respects hand capacity");
    });
}
