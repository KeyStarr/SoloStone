using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class InfestationProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_902", (g, p, id) => {
        Play(g, p, id); var spells = p.HandZone.ToArray();
        Core2026Proof.Check(spells.Length == 2 && spells.All(c => c.Card.Id == "TLC_630t" && c.Cost == 1 && c.Card.Implemented), "two pinned 1-Cost Stingers");
        var enemy = Summon(p.Opponent, "CS2_182"); PlayEntity(g, p, spells[0], enemy); var grub = p.BoardZone.Single();
        Core2026Proof.Check(enemy.Health == 3 && grub.Card.Id == "TLC_903t" && grub.AttackDamage == 2 && grub.Health == 1 && grub.IsRush && grub.IsRace(Race.BEAST), "Stinger deals two and summons pinned 2/1 Rush Beast");
        Attack(g, grub, enemy); Core2026Proof.Check(enemy.Health == 1, "printed Rush can attack minion immediately");
        PlayEntity(g, p, spells[1], p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Health == 28, "Stinger can damage hero");
        while (p.BoardZone.Count < 7) Summon(p, "CS2_182"); Play(g, p, "TLC_630t", p.Opponent.Hero);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.Opponent.Hero.Health == 26, "full board still deals damage without summon");
        ClearBoard(g, p); p.Hero[GameTag.SPELLPOWER] = 1; int before = p.Opponent.Hero.Health;
        Play(g, p, "TLC_630t", p.Opponent.Hero); grub = p.BoardZone.Single();
        Core2026Proof.Check(p.CurrentSpellPower == 1 && p.Opponent.Hero.Health == before - 3 && grub.Card.Id == "TLC_903t" && grub.AttackDamage == 2 && grub.BaseHealth == 1 && grub.Health == 1, "Spell Damage +1 makes Stinger deal three while Grub stays 2/1");
        var source = Entity.FromCard(p, Cards.FromId(id)); while (p.HandZone.Count < 9) Add(p, "CS2_182"); source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Count(c => c.Card.Id == "TLC_630t") == 1, "near-full hand gets one Stinger");
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack()); Core2026Proof.Check(p.HandZone.Count == 10, "full hand no extra Stingers");
    });
}
