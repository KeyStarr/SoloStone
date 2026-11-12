using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AsphyxiodonProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_132", (g, p, id) => {
        var friend = Summon(p, "CS2_200");
        var first = Summon(p.Opponent, "CS2_200");
        var second = Summon(p.Opponent, "CS2_200");
        var source = (Minion)Play(g, p, id);
        var held = (Minion)Add(p, id);
        Core2026Proof.Check(source.HasTaunt && source.IsRace(Race.DEMON) && source.IsRace(Race.BEAST)
            && !source.IsRace(Race.DRAGON), "printed Taunt and both Demon/Beast types");
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(clone.Player2.BoardZone.Sum(m => m.Damage) == 5
            && first.Damage == 0 && second.Damage == 0, "clone end trigger isolated");
        End(g);
        Core2026Proof.Check(new[] { first.Damage, second.Damage }.Order().SequenceEqual(new[] { 0, 5 })
            && friend.Damage == 0 && source.Damage == 0 && held.Damage == 0
            && p.Opponent.Hero.Damage == 0, "one random enemy minion takes five; hand and friendly sources do not trigger");
        End(g);
        Core2026Proof.Check(first.Damage + second.Damage == 5, "opponent end does not trigger");
        source.Silence(); g.DeathProcessingAndAuraUpdate(); End(g); End(g);
        Core2026Proof.Check(!source.HasTaunt && source.IsRace(Race.DEMON) && source.IsRace(Race.BEAST)
            && first.Damage + second.Damage == 5, "Silence removes own trigger and Taunt, retains both types");
        var removed = (Minion)Play(g, p, id); Kill(g, removed); End(g); End(g);
        Core2026Proof.Check(first.Damage + second.Damage == 5, "dead source stops triggering");
        ClearBoard(g, p.Opponent);
        var only = Summon(p.Opponent, "CS2_200"); only.HasDivineShield = true;
        var active = (Minion)Play(g, p, id); End(g); End(g);
        Core2026Proof.Check(!only.HasDivineShield && only.Damage == 0, "random damage consumes Divine Shield");
        End(g); End(g);
        Core2026Proof.Check(only.Damage == 5, "trigger persists into later friendly turns");
        Kill(g, only); int heroDamage = p.Opponent.Hero.Damage; End(g); End(g);
        Core2026Proof.Check(p.Opponent.Hero.Damage == heroDamage && active.Zone == p.BoardZone,
            "empty enemy board never redirects to hero");
    });
}
