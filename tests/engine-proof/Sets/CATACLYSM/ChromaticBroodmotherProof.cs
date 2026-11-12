using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ChromaticBroodmotherProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_469", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var enemy = Summon(p.Opponent, "CS2_182"); enemy.AttackDamage = 0;
        Core2026Proof.Check(source.IsRace(Race.DRAGON) && source.IsRace(Race.UNDEAD) && source.IsRush, "both printed types and Rush");
        source.AttackDamage = 4; p.UsedMana = 7; p.OverloadLocked = 1; var clone = g.Clone();
        Attack(clone, (Minion)clone.IdEntityDic[source.Id], (Minion)clone.IdEntityDic[enemy.Id]);
        Core2026Proof.Check(clone.Player1.UsedMana == 3 && p.UsedMana == 7, "clone Attack-based refresh isolated");
        Attack(g, source, enemy);
        Core2026Proof.Check(p.UsedMana == 3 && p.BaseMana == 10 && p.OverloadLocked == 1, "refresh current four Attack; preserves locked and max Mana");
        End(g); var foe = Summon(p.Opponent, "CS2_120"); p.UsedMana = 6; Attack(g, foe, source);
        Core2026Proof.Check(p.UsedMana == 6, "being attacked by opponent does not refresh"); End(g);
        p.UsedMana = 1; p.OverloadLocked = 1; Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(p.UsedMana == 0 && p.RemainingMana == 9, "refresh capped at spent Mana, never unlocks crystal");
        source.Silence(); g.DeathProcessingAndAuraUpdate(); End(g); End(g); p.UsedMana = 5;
        Attack(g, source, p.Opponent.Hero); Core2026Proof.Check(p.UsedMana == 5, "Silence removes own attack trigger");
        Kill(g, source); var friend = Summon(p, "CS2_182"); p.UsedMana = 5; Attack(g, friend, p.Opponent.Hero);
        Core2026Proof.Check(p.UsedMana == 5, "dead source and other friendly attacks do not refresh");
    });
}
