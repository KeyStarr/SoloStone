using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AratortheRedeemerProof
{
    internal static void Run() => StandardSetsProof.Case("MEND_804", (g, p, id) => {
        var recruit = Summon(p, "CS2_101t"); recruit.AttackDamage = 3; recruit.BaseHealth = 5; recruit.Damage = 2;
        var ordinary = Summon(p, "CS2_120"); var enemy = Summon(p.Opponent, "CS2_101t"); var hand = (Minion)Add(p, "CS2_101t");
        Play(g, p, id); Core2026Proof.Check(recruit.AttackDamage == 6 && recruit.Health == 6 && recruit.HasTaunt, "doubles current remaining stats and gives Taunt");
        Core2026Proof.Check(ordinary.AttackDamage == 2 && ordinary.Health == 3 && !ordinary.HasTaunt && enemy.Health == 1 && hand.Health == 1, "only friendly board Recruits affected");
        Play(g, p, id); Core2026Proof.Check(recruit.AttackDamage == 12 && recruit.Health == 12, "repeated doubling stacks");
        Play(g, p, "BE_228", recruit); Core2026Proof.Check(recruit.AttackDamage == 1 && recruit.Health == 1 && !recruit.HasTaunt, "silence clears doubling and Taunt");
        ClearBoard(g, p); Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 1, "no Recruits safe");
        ClearBoard(g, p); recruit = Summon(p, "CS2_101t"); var totem = Summon(p, "EX1_565");
        Core2026Proof.Check(recruit.AttackDamage == 3, "Flametongue gives Recruit two aura Attack");
        Play(g, p, id); Core2026Proof.Check(recruit.AttackDamage == 4, "doubling excludes aura Attack");
        Kill(g, totem); Core2026Proof.Check(recruit.AttackDamage == 2, "aura removal leaves doubled base Attack only");
    });
}
