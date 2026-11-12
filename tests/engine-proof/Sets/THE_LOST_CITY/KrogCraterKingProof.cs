using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class KrogCraterKingProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_480", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); enemy.Damage = 3;
        var shield = Summon(p.Opponent, "CS2_182"); shield.HasDivineShield = true;
        var source = (Minion)Play(g, p, id); var hand = (Minion)Add(p, id);
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(enemy.Health == 2 && ((Minion)clone.IdEntityDic[enemy.Id]).Health == 1, "clone end trigger isolated");
        End(g); Core2026Proof.Check(enemy.AttackDamage == 1 && enemy.Health == 1 && enemy.Damage == 0 && shield.Health == 1 && shield.HasDivineShield, "set enemies to 1/1, clear damage without popping shield");
        Core2026Proof.Check(friend.AttackDamage == 4 && source.AttackDamage == 8 && hand.AttackDamage == 8 && p.Opponent.Hero.Health == 30, "friendly, hand and heroes excluded");
        var later = Summon(p.Opponent, "CS2_182"); End(g);
        Core2026Proof.Check(later.AttackDamage == 4, "opponent turn end does not trigger");
        source.Silence(); g.DeathProcessingAndAuraUpdate(); End(g); End(g);
        Core2026Proof.Check(later.AttackDamage == 4, "Silence removes end trigger");
        source = (Minion)Play(g, p, id); Kill(g, source); End(g); End(g);
        Core2026Proof.Check(later.AttackDamage == 4, "removed source stops trigger");
        enemy.Silence(); Core2026Proof.Check(enemy.AttackDamage == 4 && enemy.Health == 5, "Silence removes pinned 1/1 enchantment");
    });
}
