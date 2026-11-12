using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SeekerofPreyProof
{
    internal static void Run() => StandardSetsProof.Case("BE_998", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.CantBeTargetedBySpells && source.CantBeTargetedByHeroPowers, "printed Elusive");
        Generic.DamageCharFunc(source, source, 1, false); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.Health == 7, "empty enemy board no-op");
        var enemy = Summon(p.Opponent, "CS2_182"); var friend = Summon(p, "CS2_182");
        Generic.DamageCharFunc(source, friend, 1, false); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(enemy.Zone == p.Opponent.BoardZone, "other friendly damage excluded");
        Generic.DamageCharFunc(enemy, enemy, 1, false); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(enemy.Zone == p.Opponent.BoardZone, "opponent damage excluded");
        source.HasDivineShield = true; Generic.DamageCharFunc(source, source, 1, false); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(enemy.Zone == p.Opponent.BoardZone && source.Health == 7, "shielded damage does not trigger");
        var clone = g.Clone(); var cs = (Minion)clone.IdEntityDic[source.Id]; Generic.DamageCharFunc(cs, cs, 1, false); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(clone.Player2.BoardZone.Count == 0 && enemy.Zone == p.Opponent.BoardZone && source.Health == 7, "damage trigger clone isolated");
        Generic.DamageCharFunc(source, source, 1, false); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && friend.Zone == p.BoardZone, "self damage destroys sole enemy only");
        var first = Summon(p.Opponent, "CS2_182"); var second = Summon(p.Opponent, "CS2_182");
        Generic.DamageCharFunc(source, source, 1, false); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(p.Opponent.BoardZone.Count == 1 && (first.Zone.Type == Zone.GRAVEYARD || second.Zone.Type == Zone.GRAVEYARD), "one random enemy destroyed");
        Play(g, p, "BE_228", source); Generic.DamageCharFunc(source, source, 1, false); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.IsSilenced && p.Opponent.BoardZone.Count == 1, "silence removes own damage trigger");
        Kill(g, source); Generic.DamageCharFunc(friend, friend, 1, false); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(p.Opponent.BoardZone.Count == 1, "dead source inactive");
        var fresh = Summon(p, id); Kill(g, fresh);
        Core2026Proof.Check(fresh.ActivatedTrigger == null, "death removes unsilenced source trigger");
    });
}
