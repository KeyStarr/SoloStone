using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static CostSliceProof;

internal static class KeymasterAlabasterBlockerProof
{
    internal static void Run()
    {
        var game = HeroDivineShieldProof.NewGame(true); var p = game.Player1;
        var original = (Playable)Entity.FromCard(p, Cards.FromId("CS2_182"));
        p.DeckZone.Add(original); CostRules.SetCostThisTurn(original, 0);
        Generic.Draw(p, original);
        var copy = Generic.Copy(p.Opponent, p.Hero, original, Zone.HAND,
            initialize: card => Effects.SetCost(1).ApplyTo(card));
        // A9 overrides the copied entity's later permanent SET: Alabaster cannot guarantee cost 1.
        Visible(copy, 0);
        CostRules.ExpireTurn(game, game.Turn); Visible(copy, 1);
        Console.WriteLine("PASS CORE_SCH_717 blocker: copied temporary SET masks permanent SET 1 under A9");
    }
}
