using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TerroroftheWastesProof
{
    internal static void Run() => StandardSetsProof.Case("BE_564", (g, p, id) => {
        p.Opponent.Hero.Armor = 100;
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Armor == 100, "no friendly death no attack");
        Kill(g, Summon(p.Opponent, "CS2_182")); Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 100 && p.NumFriendlyMinionsThatDiedThisTurn == 0, "opponent death does not satisfy condition");
        Kill(g, source); Core2026Proof.Check(p.NumFriendlyMinionsThatDiedThisTurn == 1, "core updates friendly death fact");
        source = (Minion)Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Armor == 93 && source.Health == 5, "forced attack at sole enemy hero");
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.HasTaunt = true; enemy.HasStealth = true;
        int armor = p.Opponent.Hero.Armor; source = (Minion)Play(g, p, id);
        Core2026Proof.Check((enemy.Zone.Type == Zone.GRAVEYARD && source.Health == 1 && p.Opponent.Hero.Armor == armor) || (enemy.Health == 5 && source.Health == 5 && p.Opponent.Hero.Armor == armor - 7), "random forced attack uses enemy minion or hero and deals reciprocal damage");
        End(g); End(g); armor = p.Opponent.Hero.Armor; int damage = enemy.Damage;
        Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Armor == armor && enemy.Damage == damage && p.NumFriendlyMinionsThatDiedThisTurn == 0, "core resets death condition across turn");
    });
}
