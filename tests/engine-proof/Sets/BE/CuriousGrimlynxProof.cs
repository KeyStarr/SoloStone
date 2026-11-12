using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CuriousGrimlynxProof
{
    internal static void Run() => StandardSetsProof.Case("BE_850t", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182"); var source = (Minion)Add(p, id);
        source.AttackDamage = 3; source.BaseHealth = 7;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle Grimlynx play");
        var beast = p.BoardZone.Single(m => m.Card.Id == "BE_850");
        Core2026Proof.Check(source.HasTaunt && beast.AttackDamage == 7 && beast.Health == 3 && beast.IsRush && beast.IsRace(Race.BEAST), "pinned Twisted Grimlynx opposite current stats and printed Rush");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, beast.Id, right.Id }), "Beast immediately right");
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && !p.BoardZone.Any(m => m.Card.Id == "BE_850"), "full board suppresses Beast");
        ClearBoard(g, p);
        left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182"); Kill(g, source);
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack()); beast = p.BoardZone.Single(m => m.Card.Id == "BE_850");
        Core2026Proof.Check(beast.AttackDamage == 5 && beast.Health == 2 && p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, right.Id, beast.Id }), "removed source appends exact 5/2 token");
        g.DeathProcessingAndAuraUpdate();
    });
}
