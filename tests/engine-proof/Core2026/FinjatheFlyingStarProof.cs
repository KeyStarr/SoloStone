using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class FinjatheFlyingStarProof
{
    internal static void Run() => Case("CORE_CFM_344", (g, p, id) => {
        Entity.FromCard(p, Cards.FromId("EX1_506"), zone: p.DeckZone);
        Entity.FromCard(p, Cards.FromId("EX1_509"), zone: p.DeckZone);
        var victim = Summon(p.Opponent, "EX1_506a");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasStealth, "Finja starts stealthed");
        Attack(g, source, victim);
        Core2026Proof.Check(p.BoardZone.Count == 3 && p.BoardZone.Count(m => m.IsRace(Race.MURLOC)) == 3, "kill recruits two Murlocs");
    });
}
