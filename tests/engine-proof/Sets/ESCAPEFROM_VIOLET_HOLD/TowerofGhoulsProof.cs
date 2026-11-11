using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TowerofGhoulsProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_440", (g, p, id) => {

        var left = Summon(p, "CS2_182"); var source = (Minion)Add(p, id); var right = Summon(p, "CS2_182");
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle Tower play accepted");
        source.TakeDamage(p.Opponent.Hero, 1); g.DeathProcessingAndAuraUpdate();
        var tokens = p.BoardZone.Where(m => m.Card.Id == "HERO_11bpt").OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(tokens.Length == 2 && tokens.All(m => m.AttackDamage == 1 && m.Health == 1 && m.HasCharge && m.IsRace(Race.UNDEAD) && m.Card.Implemented), "damage summons two exact Frail Ghouls with printed Charge");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id }.Concat(tokens.Select(m => m.Id)).Append(right.Id)), "tokens right of actual Tower position in creation order");
        var clone = g.Clone(); ((Minion)clone.IdEntityDic[source.Id]).TakeDamage(clone.Player2.Hero, 1); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(clone.Player1.BoardZone.Count == 7 && p.BoardZone.Count == 5, "damage trigger cloned independently");
        right.TakeDamage(p.Opponent.Hero, 1); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(p.BoardZone.Count == 5, "other minion damage does not trigger");
        source.HasDivineShield = true; source.TakeDamage(p.Opponent.Hero, 1); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(p.BoardZone.Count == 5, "prevented damage does not trigger");
        End(g); Core2026Proof.Check(p.BoardZone.Count == 3 && tokens.All(m => m.Zone == p.GraveyardZone), "Ghouls die at owner end turn");
        source.TakeDamage(p.Opponent.Hero, 1); g.DeathProcessingAndAuraUpdate(); tokens = p.BoardZone.Where(m => m.Card.Id == "HERO_11bpt").ToArray();
        End(g); Core2026Proof.Check(tokens.All(m => m.Zone == p.BoardZone), "Ghouls made on enemy turn survive that end turn");
        source.Silence(); source.TakeDamage(p.Opponent.Hero, 1); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(p.BoardZone.Count == 5, "silence removes Tower trigger");
        End(g); End(g); ClearBoard(g, p); source = Summon(p, id); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        source.TakeDamage(p.Opponent.Hero, 1); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "HERO_11bpt") == 1, "one free board slot permits only one Ghoul");
        source.TakeDamage(p.Opponent.Hero, 1); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(p.BoardZone.Count == 7, "full board suppresses both Ghouls");
        End(g); End(g); Kill(g, source); int board = p.BoardZone.Count; p.Hero.TakeDamage(p.Opponent.Hero, 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count == board, "dead Tower has no lingering damage trigger");
        ClearBoard(g, p);
        foreach (bool removed in new[] { false, true })
        {
            left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182");
            if (removed) Kill(g, source); else left[GameTag.DORMANT] = 1;
            source.Card.Power.Trigger.SingleTask.Process(g, p, source, null, new TaskStack());
            tokens = p.BoardZone.Where(m => m.Card.Id == "HERO_11bpt").OrderBy(m => m.Id).ToArray();
            var expected = removed ? new[] { left.Id, right.Id }.Concat(tokens.Select(m => m.Id))
                : new[] { left.Id, source.Id }.Concat(tokens.Select(m => m.Id)).Append(right.Id);
            Core2026Proof.Check(tokens.Length == 2 && p.BoardZone.Select(m => m.Id).SequenceEqual(expected), removed ? "removed Tower appends summons" : "Dormant neighbour counted in actual source position");
            left[GameTag.DORMANT] = 0; source.Silence(); ClearBoard(g, p);
        }
    });
}
