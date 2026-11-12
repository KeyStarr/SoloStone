using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b14-k3-mage", ProofKind.PoweredUp)]
    private static void RunB14K3Mage()
    {
        Scenario("JAIL_735", (f, card, met) => {
            if (met)
                for (int i = 0; i < 3; i++)
                {
                    f.Mana(10);
                    f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw("CS2_008"), f.Player.Hero));
                }
        }, (f, card) => PlayCardTask.Spell(f.Player, card), (f, card) => {
            var summons = f.Player.BoardZone.Where(m => m.Card.Cost == 8).ToArray();
            Check(summons.Length == 1 || summons.Length == 2, "Code Violet eight-cost output");
            return summons.Length == 2;
        });
    }
}
