using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CrystalsmithCultistProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_RLK_814", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 2, "empty hand no buff"); Kill(g, source);
        Add(p, "CS2_182"); Add(p, "CS2_004"); source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 2, "minion and Holy spell do not qualify"); Kill(g, source);
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        Add(p.Opponent, "CS2_234"); source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.Health == 2, "opponent Shadow hand excluded"); Kill(g, source);
        Add(p, "CS2_234"); source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 2 && source.Health == 3, "legacy Shadow spell qualifies despite missing school tag");
        source.Silence(); Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 2, "Silence removes buff"); Kill(g, source);
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        Add(p, "CORE_EX1_197"); Add(p, "CORE_BAR_311"); source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 2 && source.Health == 3 && p.HandZone.Count == 2, "modern Shadow tags qualify; one buff; spells retained");
        End(g); End(g); Core2026Proof.Check(source.Health == 3, "buff permanent");
    });
}
