using System;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b15-combat-context", ProofKind.PoweredUp)]
    private static void RunB15CombatContext()
    {
        var f=new Fixture();
        foreach(var id in new[]{"BE_151","CATA_185","CATA_464","CORE_CATA_004","CORE_RLK_086","CORE_YOD_026","DINO_401","EDR_258","EDR_453","EDR_484","EDR_819","JAIL_315"})
        {
            f.ClearHand(); var card=f.Draw(id); f.Flush();
            Check(!PoweredUp.HasCondition(card.Card),"no conditional hand bonus: "+id);
            f.Expected(card,0,id);
        }
        Console.WriteLine("PASS b15 combat context: all twelve have unconditional hand glow");
    }
}
