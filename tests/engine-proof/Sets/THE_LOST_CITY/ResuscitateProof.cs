using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ResuscitateProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_818", (g, p, id) => {
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 0, "empty death pool summons nothing");
        var one = Summon(p, "CS2_171"); var two = Summon(p, "CS2_120"); var three = Summon(p, "CS2_125");
        Kill(g, one); Kill(g, two); Kill(g, three); ClearBoard(g, p);
        var burned = Add(p, "CS2_173"); Generic.DiscardBlock(p, burned);
        var enemy = Summon(p.Opponent, "CS2_168"); Kill(g, enemy);
        var left = Summon(p, "CS2_182");
        int observed = 0;
        void Observe(IEntity summoned)
        {
            observed++;
            Core2026Proof.Check(((Minion)summoned)[GameTag.REBORN] == 0
                && p.BoardZone.Skip(1).All(m => m[GameTag.REBORN] == 0),
                "all resurrection summon observers run before granted Reborn");
        }
        g.TriggerManager.AfterSummonTrigger += Observe;
        Play(g, p, id);
        g.TriggerManager.AfterSummonTrigger -= Observe;
        Core2026Proof.Check(observed == 3, "observer sees all three resurrected minions");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Card.Id).SequenceEqual(new[] {"CS2_182", "CS2_171", "CS2_120", "CS2_125"}), "resurrect costs one, two, three in order after existing board");
        var raised = p.BoardZone.Skip(1).ToArray();
        Core2026Proof.Check(raised.All(m => m[GameTag.REBORN] == 1) && raised[0].AttackDamage == 1 && raised[0].Health == 1 && raised[1].AttackDamage == 2 && raised[1].Health == 3 && raised[2].HasTaunt, "fresh printed stats, abilities and granted Reborn");
        var clone = g.Clone(); Kill(clone, (Minion)clone.IdEntityDic[raised[0].Id]); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(raised[0].Zone == p.BoardZone && clone.Player1.BoardZone.Any(m => m.Card.Id == "CS2_171" && m.Id != raised[0].Id && m.Health == 1 && m[GameTag.REBORN] == 0), "granted Reborn works and clone isolated");
        raised[1].Silence(); Kill(g, raised[1]); Core2026Proof.Check(!p.BoardZone.Any(m => m.Card.Id == "CS2_120"), "Silence removes granted Reborn");
        while (p.BoardZone.Count > 0) ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Last().Card.Cost == 1 && p.BoardZone.Last()[GameTag.REBORN] == 1, "board cap keeps first available cost only");
        End(g);
    });
}
