using System.Linq;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b18-t-autoplay2", ProofKind.PoweredUp)]
    private static void B18Autoplay2Glow()
    {
        Scenario("TLC_100", (f, card, met) => {
            f.Player.DeckCards.Clear();
            f.Player.DeckCards.AddRange((met ? new[] { "CS2_231", "CS2_189", "CS2_172", "CS2_121", "CS2_182", "CS2_187", "CS2_200", "CS2_201", "EX1_298", "EX1_561", "EX1_586" } : new[] { "CS2_182" }).Select(Cards.FromId));
        }, (f, card) => PlayCardTask.Any(f.Player, card), (f, card) => f.Player.Choice != null);
    }
}
