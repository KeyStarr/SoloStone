using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class KabalMastermindProof
{
    internal static void Run() => StandardSetsProof.Case("CAP_406", (g, p, id) => {
        var prior = Summon(p, "CAP_400t2t"); var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasTaunt && prior.AttackDamage == 3 && prior.Health == 3, "Taunt; no retroactive buffs");
        Core2026Proof.Check(p.AppliedEnchantments.Count(e => e.Card.Id == "CAP_406e") == 1, "persistent enchantment attached to controller");
        var clone = g.Clone(); var cloneImp = Summon(clone.Player1, "CAP_400t2t");
        Core2026Proof.Check(cloneImp.AttackDamage == 5 && p.BoardZone.Count == 2 && prior.AttackDamage == 3, "controller trigger clone independent");
        var imp = Summon(p, "CAP_400t2t"); var ordinary = Summon(p, "EX1_008"); var enemy = Summon(p.Opponent, "CAP_400t2t");
        Core2026Proof.Check(imp.AttackDamage == 5 && imp.Health == 5 && imp.AppliedEnchantments.Any(e => e.Card.Id == "CAP_406e2")
            && ordinary.AttackDamage == 1 && enemy.AttackDamage == 3, "exact +2/+2 only to friendly Imp-formants");
        source.Silence(); Kill(g, source); imp.Silence();
        Core2026Proof.Check(imp.AttackDamage == 3 && imp.Health == 3, "silence removes received buff");
        imp = Summon(p, "CAP_400t2t"); Core2026Proof.Check(imp.AttackDamage == 5, "controller effect survives source silence and death");
        End(g); End(g); ClearBoard(g, p); source = (Minion)Play(g, p, id); imp = Summon(p, "CAP_400t2t");
        Core2026Proof.Check(imp.AttackDamage == 7 && imp.Health == 7 && p.AppliedEnchantments.Count(e => e.Card.Id == "CAP_406e") == 2, "effect persists across turns; two Battlecries stack");
        var played = (Minion)Play(g, p, "CAP_400t2t"); Core2026Proof.Check(played.AttackDamage == 7 && played.Health == 7, "played Imp-formant also counts as summon");
        ClearBoard(g, p); Play(g, p, "ICC_833"); imp = Summon(p, "CAP_400t2t");
        Core2026Proof.Check(imp.AttackDamage == 7 && imp.Health == 7 && p.AppliedEnchantments.Count(e => e.Card.Id == "CAP_406e") == 2, "controller effect survives hero replacement");
    });
}
