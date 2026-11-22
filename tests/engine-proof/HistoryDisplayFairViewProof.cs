using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

// #81: a history display (a numeric text tag such as Lotus Troublemaker's shot count) is presentation only. In a fair view the
// display of a card in the OTHER player's hand needs private hand residence the viewer cannot know, and the history refuses it;
// that refusal must not abort the viewer's own simulated action. A real game computes the display as before.
internal static class HistoryDisplayFairViewProof
{
    private const GameTag Shots = GameTag.TAG_SCRIPT_DATA_NUM_1;

    private static void Verify(bool condition, string message) => Core2026Proof.Check(condition, "HistoryDisplayFairView: " + message);

    private static (Game game, IPlayable lotus) Staged()
    {
        var game = HistoryFactsProof.Fixture();
        // Player 2's Lotus sits in hand while player 1 is to act.
        var lotus = Entity.FromCard(game.Player2, Cards.FromId("JAIL_470"), zone: game.Player2.HandZone);
        game.AuraUpdate();
        return (game, lotus);
    }

    internal static void Run()
    {
        // Real game: the display counts the owner's own two-Mana plays while the Lotus is in hand.
        var (real, realLotus) = Staged();
        int start = realLotus[Shots];
        End(real);
        Play(real, real.Player2, "CS2_120");
        Play(real, real.Player2, "CS2_120");
        Verify(realLotus[Shots] == start + 2, "real game: Lotus shot count follows two-Mana plays: " + start + " -> " + realLotus[Shots]);

        // Fair view of player 1: player 2's known Lotus must not make player 1's action throw, and keeps its tag.
        var (source, _) = Staged();
        var view = source.CloneExact();
        view.ProjectHistoryForFairView(1);
        var viewLotus = view.Player2.HandZone.Single(c => c.Card.Id == "JAIL_470");
        int before = viewLotus[Shots];
        Play(view, view.Player1, "CS2_120");
        Play(view, view.Player1, "CS2_182");
        view.AuraUpdate();
        Verify(viewLotus[Shots] == before, "fair view: the opponent's Lotus display keeps its value");
        End(view);
        Verify(view.CurrentPlayer == view.Player2, "fair view: end turn settles with a known opponent Lotus in hand");

        // Fair view of the owner: its own Lotus is not skipped by the opponent rule and the game proceeds.
        var own = source.CloneExact();
        own.ProjectHistoryForFairView(2);
        Play(own, own.Player1, "CS2_120");
        End(own);
        Console.WriteLine("PASS history display in a fair view never fails an action");
    }
}
