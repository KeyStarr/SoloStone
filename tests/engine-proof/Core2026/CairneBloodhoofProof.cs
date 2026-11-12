using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class CairneBloodhoofProof
{
    internal static void Run() => Case("CORE_EX1_110", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.HasTaunt, "Cairne has Taunt");
        Kill(g, source); var baine = p.BoardZone.Single();
        Core2026Proof.Check(baine.Card.Id == "EX1_110t" && baine.AttackDamage == 5 && baine.Health == 5, "current Baine is base 5/5");
        Play(g, p, "CS2_203", baine);
        Core2026Proof.Check(baine.AttackDamage == 5 && baine.Health == 5 && Cards.FromId("EX1_110t").ATK == 4, "silence retains current base stats; legacy token untouched");
    });
}
