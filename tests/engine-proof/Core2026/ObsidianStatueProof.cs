using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class ObsidianStatueProof
{
    internal static void Run() => Case("CORE_ICC_214", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasTaunt && source.HasLifeSteal, "Statue has Taunt and Lifesteal"); Kill(g, source);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD, "Statue destroys enemy on death");
    });
}
