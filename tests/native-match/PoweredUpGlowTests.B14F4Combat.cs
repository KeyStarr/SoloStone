using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.PlayerTasks;
internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b14-f4-combat", ProofKind.PoweredUp)]
    private static void RunB14F4Combat()
    {
        Scenario("TLC_107",(f,card,met)=> {
            var previous=f.Draw(met?"CS2_033":"CS2_182");
            f.Process(PlayCardTask.Minion(f.Player,previous));
            f.Process(EndTurnTask.Any(f.Player)); f.Process(EndTurnTask.Any(f.Game.Player2)); f.Mana(10);
        },(f,card)=>PlayCardTask.Minion(f.Player,card),(f,card)=>((SabberStoneCore.Model.Entities.Minion)card).IsRush);
        Scenario("DINO_136",(f,card,met)=> {
            if(!met) {
                f.Player.SetasideZone.Add(f.Player.HandZone.Remove(card)); f.Draw("CS2_182");
                f.Player.HandZone.Add(f.Player.SetasideZone.Remove(card)); f.Draw("CS2_182");
            }
        },(f,card)=>PlayCardTask.Spell(f.Player,card),(f,card)=>f.Player.BoardZone
            .Where(m=>m.Card.Id=="DINO_136t").Count(m=>m[GameTag.IMMUNE_WHILE_ATTACKING]>0)==3);
    }
}
