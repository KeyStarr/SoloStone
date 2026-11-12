using System.Linq;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b14-f3-past-pools", ProofKind.PoweredUp)]
    private static void RunB14F3PastPools()
    {
        Scenario("BE_588", (f,card,met)=> {
            if(met) f.Summon("GVG_058",f.Player);
            else f.Summon("GVG_058",f.Game.Player2);
        }, (f,card)=>PlayCardTask.Minion(f.Player,card), (f,card)=> {
            bool discovered=f.Player.Choice!=null;
            if(discovered) f.Process(ChooseTask.Pick(f.Player,f.Player.Choice.Choices.First()));
            return discovered;
        });
    }
}
