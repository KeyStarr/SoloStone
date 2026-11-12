using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class QuestingAssistantProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_987", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182");
        var source = Add(p, id);
        Core2026Proof.Check(p.Options().Where(t => t.Source?.Id == source.Id).All(t => t.Target == null)
            && source.IsValidPlayTarget(null) && !source.IsValidPlayTarget(enemy), "no played quest means no targeting");
        PlayEntity(g, p, source);
        Core2026Proof.Check(enemy.Damage == 0, "no played quest means no damage");
        ClearBoard(g, p);
        Play(g, p, "TLC_426");
        source = Add(p, id);
        var options = p.Options().Where(t => t.Source?.Id == source.Id).ToArray();
        Core2026Proof.Check(options.Length > 0 && options.All(t => t.Target == enemy)
            && !source.IsValidPlayTarget(null) && source.IsValidPlayTarget(enemy), "played quest requires available enemy minion");
        PlayEntity(g, p, source, enemy);
        Core2026Proof.Check(enemy.Damage == 3, "qualified battlecry deals three");
        ClearBoard(g, p.Opponent); Play(g, p, id);
        foreach (string questId in new[] { "UNG_942", "TLC_426" }) Unpinned_CounteredQuestCounts(questId, id);
        var legacy = QuestLifecycleProof.Fixture();
        Play(legacy, legacy.Player1, "UNG_942");
        var victim = Summon(legacy.Player2, "CS2_182");
        Play(legacy, legacy.Player1, id, victim);
        Core2026Proof.Check(victim.Damage == 3, "legacy played quest qualifies");
    });

    private static void Unpinned_CounteredQuestCounts(string questId, string id)
    {
        var fresh = QuestLifecycleProof.Fixture(); var owner = fresh.Player1;
        // Counterspell belongs to the opponent and is active on the quest player's turn.
        End(fresh); Play(fresh, fresh.Player2, "EX1_287"); End(fresh);
        var quest = (Spell)Play(fresh, owner, questId);
        Core2026Proof.Check(quest.Zone == owner.GraveyardZone && owner.SecretZone.Quest == null
            && owner.SecretZone.ModernQuests.Count == 0, "countered quest installs nothing");
        var target = Summon(owner.Opponent, "CS2_182");
        Play(fresh, owner, id, target);
        Core2026Proof.Check(target.Damage == 3, "Unpinned_CounteredQuestCounts: " + questId);
    }
}
