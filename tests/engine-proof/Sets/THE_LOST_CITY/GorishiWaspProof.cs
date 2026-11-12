using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class GorishiWaspProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_630", (g, p, id) => {
        var held = (Minion)Add(p, id); var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.IsRush && source.IsRace(Race.BEAST), "printed Rush Beast");
        var clone = g.CloneExact(history: true); Play(clone, clone.Player1, "CS2_008", (Minion)clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(clone.Player1.HandZone.Count(m => m.Card.Id == "TLC_630t") == 1 && p.HandZone.Count == 1, "clone trigger isolated");
        Play(g, p, "CS2_008", source); var stinger = p.HandZone.Single(m => m.Card.Id == "TLC_630t");
        Core2026Proof.Check(stinger.Cost == 1 && stinger.Card.Implemented && !held.HasDivineShield, "damage creates pinned one-cost Stinger");
        PlayEntity(g, p, stinger, p.Opponent.Hero); var grub = p.BoardZone.Single(m => m.Card.Id == "TLC_903t");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 2 && grub.AttackDamage == 2 && grub.Health == 1 && grub.IsRush, "Stinger deals two and summons pinned Rush Grub");
        End(g); Play(g, p.Opponent, "CS2_008", source); Core2026Proof.Check(p.HandZone.Count(m => m.Card.Id == "TLC_630t") == 1, "enemy damage triggers"); End(g);
        Play(g, p, "BE_228", source); Play(g, p, "CS2_008", source); Core2026Proof.Check(p.HandZone.Count(m => m.Card.Id == "TLC_630t") == 1, "Silence removes trigger");
        ClearBoard(g, p); source = (Minion)Play(g, p, id); Play(g, p, "CS2_029", source); Play(g, p, "CS2_008", source);
        Core2026Proof.Check(source.Zone == p.GraveyardZone && p.HandZone.Count(m => m.Card.Id == "TLC_630t") == 3, "lethal damage still grants Stinger and removes trigger");
        while (p.HandZone.Count < 10) Add(p, "CS2_182"); source = Summon(p, id); source.TakeDamage(p.Opponent.Hero, 1); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(p.HandZone.Count == 10, "full hand cannot receive Stinger");
    });
}
