using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DiveTheGolakkaDepthsProof
{
    internal static int Stacks(Controller p) => p.AppliedEnchantments?.SingleOrDefault(e => e.Card.Id == "TLC_426e")?.ScriptTag1 ?? 0;
    internal static void Run() => StandardSetsProof.Case("TLC_426", (g, p, id) => {
        var quest = (Spell)Play(g, p, id); var trigger = quest.ActivatedTrigger;
        Summon(p, "CS2_182"); Summon(p.Opponent, "CS2_168");
        Core2026Proof.Check(quest.QuestProgress == 0, "non-Murloc and opponent summons ignored");
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        PinnedRule_SixthMurlocStack(g, p, quest, trigger);
        var opponent = Summon(p.Opponent, "CS2_168");
        var otherRace = Summon(p, "CS2_182");
        Core2026Proof.Check(opponent.AttackDamage == 2 && opponent.Health == 1 && otherRace.AttackDamage == 4
            && otherRace.Health == 5 && quest.QuestProgress == 1, "reward ignores opponent and non-Murloc summons");
        Core2026Proof.Check(p.AppliedEnchantments.Single(e => e.Card.Id == "TLC_426e").Target == p, "reward attached to controller");
    });

    private static void PinnedRule_SixthMurlocStack(Game g, Controller p, Spell quest, SabberStoneCore.Triggers.Trigger trigger)
    {
        for (int i = 1; i <= 19; i++)
        {
            var m = i % 2 == 0 ? (Minion)Play(g, p, "CS2_168") : Summon(p, "CS2_168");
            int oldStacks = (i - 1) / 6;
            Core2026Proof.Check(m.AttackDamage == 2 + oldStacks && m.Health == 1 + oldStacks,
                "PinnedRule_SixthMurlocStack: summon uses pre-completion stack at " + i);
            Core2026Proof.Check((m.AppliedEnchantments?.Count(e => e.Card.Id == "TLC_426e2") ?? 0) == (oldStacks > 0 ? 1 : 0),
                "Unpinned_SingleScaledEnchantment");
            Core2026Proof.Check(Stacks(p) == i / 6 && quest.QuestProgress == i % 6, $"PinnedRule_MultipleCompletionsAndWrap i={i} progress={quest.QuestProgress} stacks={Stacks(p)}");
            Core2026Proof.Check(quest.Id == p.SecretZone.ModernQuests.Single().Id && quest.ActivatedTrigger == trigger,
                "repeat keeps identity, slot and trigger");
            Kill(g, m);
        }
    }
}
