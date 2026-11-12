using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class StoryOfGalvadonProof
{
    internal static void Run() => Unpinned_StoryOfGalvadon();
    private static void Unpinned_StoryOfGalvadon() => StandardSetsProof.Case("TLC_444", (g, p, id) => {
        var m = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, m, false);
        Play(g, p, id, m);
        Core2026Proof.Check(BonusEffectsProof.Count(m) == 3 && m.AppliedEnchantments.Count == 3, "three distinct grants and actors");
        var saturated = Summon(p, "CS2_182"); BonusEffects.GiveRandom(saturated, p.Hero, 8);
        saturated.CantBeTargetedBySpells = false;
        int attachments = saturated.AppliedEnchantments.Count;
        Play(g, p, id, saturated);
        Core2026Proof.Check(saturated.AppliedEnchantments.Count == attachments + 1 && BonusEffectsProof.Count(saturated) == 8, "nearly saturated recipient receives only missing Elusive");
        m.Silence(); Core2026Proof.Check(BonusEffectsProof.Count(m) == 0, "Silence removes spell grants");
    });
}
