using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BitterEndProof
{
    internal static void Run() => StandardSetsProof.Case("END_023", (g, p, id) => {
        var outsideLeft = Summon(p.Opponent, "CS2_182"); outsideLeft.Damage = 1;
        var left = Summon(p.Opponent, "CS2_182"); left.Damage = 1;
        var target = Summon(p.Opponent, "CS2_182"); var right = Summon(p.Opponent, "CS2_182"); right.Damage = 2;
        var outsideRight = Summon(p.Opponent, "CS2_182"); outsideRight.Damage = 1;
        var friend = Summon(p, "CS2_182"); TargetRules(g, p, id, target, friend, false);
        Play(g, p, id, target);
        Core2026Proof.Check(target.IsFrozen && target.Zone.Type == Zone.PLAY && target.Damage == 0 && left.IsFrozen && right.IsFrozen && left.Zone.Type == Zone.GRAVEYARD && right.Zone.Type == Zone.GRAVEYARD, "freeze target and both neighbours, destroy already-damaged neighbours");
        Core2026Proof.Check(!outsideLeft.IsFrozen && !outsideRight.IsFrozen && outsideLeft.Zone.Type == Zone.PLAY && outsideRight.Zone.Type == Zone.PLAY && !friend.IsFrozen, "no effect beyond original adjacent trio or on opposing side");
        ClearBoard(g, p.Opponent); var edge = Summon(p.Opponent, "CS2_182"); edge.Damage = 1; edge.HasDivineShield = true;
        var neighbour = Summon(p.Opponent, "CS2_182"); var far = Summon(p.Opponent, "CS2_182"); Play(g, p, id, edge);
        Core2026Proof.Check(edge.Zone.Type == Zone.GRAVEYARD && edge.IsFrozen && neighbour.IsFrozen && neighbour.Zone.Type == Zone.PLAY && !far.IsFrozen, "edge target: destroys damaged target through Shield, freezes single healthy neighbour");
        Play(g, p, id, friend); Core2026Proof.Check(friend.IsFrozen && friend.Zone.Type == Zone.PLAY, "isolated friendly healthy target frozen, not destroyed");
        friend.Damage = 1; Play(g, p, id, friend); Core2026Proof.Check(friend.Zone.Type == Zone.GRAVEYARD, "already-frozen damaged target destroyed");
        Cards.FromId(id).Power.PowerTask.Process(g,p,Add(p,id),friend);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(friend.Zone.Type == Zone.GRAVEYARD && !far.IsFrozen,
            "a target removed before the spell body has no remaining neighbours");
    });
}
