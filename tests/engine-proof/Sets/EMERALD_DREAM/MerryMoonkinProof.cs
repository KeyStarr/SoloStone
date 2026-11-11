using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using static Core2026Proof;

internal static class MerryMoonkinProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_940", (g, p, id) => {
        StarsurgeProof.Hidden(id);
        var source = Play(g, p, id);
        End(g); Core2026Proof.Check(p.Hero.Armor == 1, "base armor without Wisps");
        End(g); Core2026Proof.Check(p.Hero.Armor == 1, "opponent turn does not trigger");
        var a = Summon(p, "EDR_851t"); var b = Summon(p, "CS2_231");
        Summon(p.Opponent, "EDR_851t"); Summon(p, "CS2_182");
        Core2026Proof.Check(source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "display reflects two controlled Wisps only");
        var clone = g.Clone(); Kill(clone, (SabberStoneCore.Model.Entities.Minion)clone.IdEntityDic[a.Id]); End(clone);
        Core2026Proof.Check(clone.Player1.Hero.Armor == 3 && p.Hero.Armor == 1, "clone recalculates current Wisps independently");
        End(g); Core2026Proof.Check(p.Hero.Armor == 4, "base plus one per Wisp");
        End(g); Kill(g, a);
        Core2026Proof.Check(source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "display falls when Wisp dies");
        ((SabberStoneCore.Model.Entities.Minion)source).Silence();
        End(g); Core2026Proof.Check(p.Hero.Armor == 4, "silence removes armor trigger and display aura");
    });
}
