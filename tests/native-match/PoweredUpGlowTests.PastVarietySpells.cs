using System.Linq;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-pastvar-spells", ProofKind.PoweredUp)]
    private static void PastVarietySpellsGlow()
    {
        Scenario("DRG_219", (f,card,met)=> {
            f.Summon("CS2_182",f.Game.Player2);
            f.Summon("CS2_182",f.Game.Player2);
            f.Summon("CS2_182",f.Game.Player2);
            if(met) f.Draw("EX1_563");
        }, (f,card)=>PlayCardTask.Any(f.Player,card,f.Game.Player2.BoardZone[1]),
        (f,card)=>f.Game.Player2.BoardZone.All(m=>m.Damage==4));
    }
}
