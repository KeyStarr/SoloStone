using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ScalebreakerBulwarkProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_475", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var source = (Minion)Play(g, p, id); Summon(p, "CS2_052"); p.Opponent.Hero.HasDivineShield = true;
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[enemy.Id]).Health == 3 && !clone.Player2.Hero.HasDivineShield && enemy.Health == 5 && p.Opponent.Hero.HasDivineShield, "clone trigger isolated including hero shield");
        End(g); Core2026Proof.Check(enemy.Health == 3 && friend.Health == 5 && p.Hero.Health == 30 && p.Opponent.Hero.Health == 30 && !p.Opponent.Hero.HasDivineShield, "two damage to all enemies only; hero shield absorbs hit; spell damage excluded");
        End(g); Core2026Proof.Check(enemy.Health == 3 && p.Opponent.Hero.Health == 30, "opponent end does not trigger");
        End(g); Core2026Proof.Check(enemy.Health == 1 && p.Opponent.Hero.Health == 28, "next hit damages hero after shield consumed");
        End(g); source.Silence(); g.DeathProcessingAndAuraUpdate(); End(g); End(g);
        Core2026Proof.Check(enemy.Health == 1 && p.Opponent.Hero.Health == 28, "Silence stops damage trigger");
        source = (Minion)Play(g, p, id); Kill(g, source); End(g);
        Core2026Proof.Check(enemy.Health == 1 && p.Opponent.Hero.Health == 28, "dead source stops damage trigger");
    });
}
